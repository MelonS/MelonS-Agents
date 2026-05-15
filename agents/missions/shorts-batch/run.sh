#!/usr/bin/env bash
# Shorts-batch mission: one long video → N captioned 9:16 shorts.
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../../lib/env.sh"
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/log.sh"
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/ollama.sh"
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/whisper.sh"
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/ffmpeg.sh"
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/attribution.sh"

SOURCE="${1:-}"
N="${2:-3}"
[[ -z "$SOURCE" ]] && { log_err "usage: $0 <url_or_path> [N=3]"; exit 64; }
require_bin "$FFMPEG_BIN" "$FFPROBE_BIN" "$WHISPER_CLI_BIN" "$YT_DLP_BIN" jq curl
require_env OLLAMA_HOST OLLAMA_MODEL_HIGHLIGHT WHISPER_MODEL RECORDS_DIR

MISSION_ID="shorts-batch-$(date +%H%M%S)"
MDIR="$RECORDS_DIR/missions/$(date +%Y-%m-%d)/$MISSION_ID"
mkdir -p "$MDIR/resources" "$MDIR/outputs"
log_step "mission: $MDIR (N=$N)"
T0=$(python3 -c "import time; print(time.time())")

# 1. Source
SRC="$MDIR/resources/source.mp4"
if [[ -f "$SOURCE" ]]; then cp "$SOURCE" "$SRC"; else "$YT_DLP_BIN" -f "best[ext=mp4][height<=720]/best[height<=720]/best" -o "$SRC" "$SOURCE" >&2; fi
SRC_DURATION=$(ffmpeg_duration "$SRC")
log_ok "source: ${SRC_DURATION}s"

# Resolve source attribution + license once; every short rendered below
# gets the same burned-in watermark and shares a single SOURCES.txt record.
resolve_source_attribution "$SOURCE"
write_sources_record "$MISSION_ID" "$SOURCE" "$MDIR/outputs/SOURCES.txt"

# 2. Transcribe
TRANSCRIPT=$(whisper_transcribe "$SRC" "$MDIR/resources/transcript")
SEGS="$MDIR/resources/segments.json"
whisper_segments "$TRANSCRIPT" > "$SEGS"
SEG_COUNT=$(jq 'length' "$SEGS")
log_ok "segments: $SEG_COUNT"

# 3. Pick N highlights — sequential loop with exclude list (more reliable
# than asking the 3B model for a JSON array in one shot)
ollama_ensure_model "$OLLAMA_MODEL_HIGHLIGHT"
SINGLE_PROMPT_FILE="$(dirname "${BASH_SOURCE[0]}")/select-shorts-single.prompt.md"
EXCLUDE='[]'
PICKS_ARR='[]'
for i in $(seq 1 "$N"); do
  PROMPT="$(sed "s|\\\$EXCLUDE|$EXCLUDE|g" "$SINGLE_PROMPT_FILE")

INPUT SEGMENTS:
$(cat "$SEGS")"
  RAW=$(ollama_generate "$OLLAMA_MODEL_HIGHLIGHT" "$PROMPT" true)
  PICK=$(echo "$RAW" | jq -c 'if type == "object" then . else (.. | objects | select(has("start") and has("end"))) end' 2>/dev/null | head -1)
  if [[ -z "$PICK" || "$PICK" == "null" ]]; then
    log_warn "pick $i: model returned no valid object — stopping"
    break
  fi
  PS=$(echo "$PICK" | jq -r '.start')
  PE=$(echo "$PICK" | jq -r '.end')
  # Check overlap with already-picked
  OVERLAP=$(echo "$PICKS_ARR" | jq --argjson s "$PS" --argjson e "$PE" \
    '[ .[] | select(.start < $e and .end > $s) ] | length')
  if [[ "$OVERLAP" != "0" ]]; then
    log_warn "pick $i overlaps existing — accepting anyway (best-effort)"
  fi
  PICKS_ARR=$(echo "$PICKS_ARR" | jq -c --argjson p "$PICK" '. + [$p]')
  EXCLUDE=$(echo "$EXCLUDE" | jq -c --argjson s "$PS" --argjson e "$PE" '. + [{start: $s, end: $e}]')
  log_ok "pick $i: ${PS}s → ${PE}s"
done
PICKS="$PICKS_ARR"
echo "$PICKS" > "$MDIR/resources/picks.json"
PICK_COUNT=$(echo "$PICKS" | jq 'length')
log_ok "picks: $PICK_COUNT (requested $N)"

# 4. Render each pick
VERDICT=PASS
RENDERED=()
for i in $(seq 0 $((PICK_COUNT - 1))); do
  RAW_START=$(echo "$PICKS" | jq -r ".[$i].start")
  RAW_END=$(echo "$PICKS" | jq -r ".[$i].end")
  REASON=$(echo "$PICKS" | jq -r ".[$i].reason // \"\"")

  # Clamp this pick using the shared clamp-window.jq
  WINDOW=$(jq -cn --argjson rs "$RAW_START" --argjson re "$RAW_END" \
    --argjson src_dur "$SRC_DURATION" --slurpfile segs "$SEGS" \
    -f "$REPO_ROOT/agents/lib/clamp-window.jq")
  START=$(echo "$WINDOW" | jq -r '.start')
  END=$(echo "$WINDOW" | jq -r '.end')

  NN=$(printf "%02d" $((i+1)))
  SRT="$MDIR/outputs/short-${NN}.srt"
  FINAL="$MDIR/outputs/short-${NN}.mp4"
  ffmpeg_segments_to_srt "$SEGS" "$START" "$END" "$SRT"
  ffmpeg_render_short "$SRC" "$START" "$END" "$SRT" "$FINAL" "$SOURCE_ATTRIBUTION" || { log_err "render $NN failed"; VERDICT=FAIL; continue; }

  DUR=$(ffmpeg_duration "$FINAL")
  RES=$("$FFPROBE_BIN" -v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 "$FINAL")
  SIZE_MB=$(awk -v b="$(stat -f%z "$FINAL" 2>/dev/null || stat -c%s "$FINAL")" 'BEGIN{printf "%.2f", b/1048576}')
  RENDERED+=("$NN|$START|$END|$DUR|$RES|$SIZE_MB|$REASON")
  log_ok "rendered short-$NN (${DUR}s, ${SIZE_MB}MB)"
done

# 5. QA
{
  echo "# QA report — $MISSION_ID"
  echo
  echo "**Verdict**: $VERDICT"
  echo
  echo "## Shorts produced ($PICK_COUNT requested, ${#RENDERED[@]} rendered)"
  echo
  echo "| # | Start | End | Duration | Resolution | Size (MB) | Reason |"
  echo "|---|-------|-----|----------|------------|-----------|--------|"
  for row in "${RENDERED[@]}"; do
    IFS='|' read -r nn s e d r sz why <<< "$row"
    printf "| %s | %ss | %ss | %ss | %s | %s | %s |\n" "$nn" "$s" "$e" "$d" "$r" "$sz" "$why"
  done
} > "$MDIR/qa-report.md"

T1=$(python3 -c "import time; print(time.time())")
TOTAL=$(awk -v a="$T0" -v b="$T1" 'BEGIN{printf "%.3f", b-a}')

jq -n --arg v "$VERDICT" --argjson n "$PICK_COUNT" --argjson tot "$TOTAL" --argjson rendered "${#RENDERED[@]}" \
   '{verdict: $v, mission_type: "shorts-batch", picks_requested: $n, picks_rendered: $rendered, total_s: $tot}' \
   > "$MDIR/metrics.json"

{
  echo "# Summary — $MISSION_ID"
  echo
  echo "- source: $SOURCE (${SRC_DURATION}s)"
  echo "- picks: $PICK_COUNT, rendered: ${#RENDERED[@]}"
  echo "- verdict: $VERDICT"
  echo "- total: ${TOTAL}s"
} > "$MDIR/summary.md"

[[ "$VERDICT" == "PASS" ]] || exit 1
log_ok "mission $MISSION_ID PASS — ${#RENDERED[@]} shorts in $MDIR/outputs/"
