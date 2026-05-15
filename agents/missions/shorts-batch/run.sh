#!/usr/bin/env bash
# Shorts-batch mission: one long video → N captioned 9:16 shorts.
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../../lib/env.sh"
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/log.sh"
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/ollama.sh"
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/whisper.sh"
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/ffmpeg.sh"
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/attribution.sh"
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/retry.sh"
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/copyright.sh"

SOURCE="${1:-}"
N="${2:-3}"
[[ -z "$SOURCE" ]] && { log_err "usage: $0 <url_or_path> [N=3]"; exit 64; }
require_bin "$FFMPEG_BIN" "$FFPROBE_BIN" "$WHISPER_CLI_BIN" "$YT_DLP_BIN" jq curl
require_env OLLAMA_HOST OLLAMA_MODEL_HIGHLIGHT WHISPER_MODEL RECORDS_DIR

if ! ALLOWED_LICENSE=$(check_source_allowed "$SOURCE"); then
  log_err "copyright gate refused source — see config/copyright-allowlist.yaml"
  exit 67
fi
log_info "copyright gate: $ALLOWED_LICENSE"

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

# 3-5: pick N + render + QA, wrapped in a retry loop.  Mission-level retry
# (not per-short) — on aggregate FAIL the whole pipeline re-runs with the
# previous qa-report inlined so the model knows which picks didn't survive
# render.  Cap is QA_RETRY_MAX (default 2 retries).
ollama_ensure_model "$OLLAMA_MODEL_HIGHLIGHT"
SINGLE_PROMPT_FILE="$(dirname "${BASH_SOURCE[0]}")/select-shorts-single.prompt.md"

VERDICT=""
QA_FEEDBACK=""
ATTEMPT=0
MAX_ATTEMPTS=$((QA_RETRY_MAX + 1))

while (( ATTEMPT < MAX_ATTEMPTS )); do
  ATTEMPT=$((ATTEMPT + 1))
  if (( ATTEMPT > 1 )); then
    log_warn "attempt $ATTEMPT/$MAX_ATTEMPTS — previous QA FAILED, retrying with feedback"
    QA_FEEDBACK=$(qa_extract_feedback "$MDIR/qa-report.md")
    # Reset any previous-attempt artifacts so the new run starts clean.
    rm -f "$MDIR/outputs/short-"*.mp4 "$MDIR/outputs/short-"*.srt
  fi

  # Pick N highlights — sequential single-pick loop with exclude list.
  EXCLUDE='[]'
  PICKS_ARR='[]'
  for i in $(seq 1 "$N"); do
    PROMPT="$(sed "s|\\\$EXCLUDE|$EXCLUDE|g" "$SINGLE_PROMPT_FILE")$(qa_feedback_block)

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
  echo "$PICKS" > "$MDIR/resources/picks.attempt-${ATTEMPT}.json"
  echo "$PICKS" > "$MDIR/resources/picks.json"
  PICK_COUNT=$(echo "$PICKS" | jq 'length')
  log_ok "picks: $PICK_COUNT (requested $N)"

  # Render each pick.
  VERDICT=PASS
  RENDERED=()
  for i in $(seq 0 $((PICK_COUNT - 1))); do
    RAW_START=$(echo "$PICKS" | jq -r ".[$i].start")
    RAW_END=$(echo "$PICKS" | jq -r ".[$i].end")
    REASON=$(echo "$PICKS" | jq -r ".[$i].reason // \"\"")

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

  # Aggregate QA verdict: PASS iff all requested shorts rendered.
  if (( ${#RENDERED[@]} < N )); then
    VERDICT=FAIL
  fi

  {
    echo "# QA report — $MISSION_ID (attempt $ATTEMPT of $MAX_ATTEMPTS)"
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

  [[ "$VERDICT" == PASS ]] && break
done

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

if [[ "$VERDICT" != "PASS" ]]; then
  BLOCKER=$(qa_write_blocker "$MISSION_ID" "$MDIR" "$ATTEMPT")
  log_err "mission $MISSION_ID FAIL after $ATTEMPT attempts — blocker: $BLOCKER"
  exit 1
fi
log_ok "mission $MISSION_ID PASS on attempt $ATTEMPT — ${#RENDERED[@]} shorts in $MDIR/outputs/"
