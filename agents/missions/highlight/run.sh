#!/usr/bin/env bash
# End-to-end highlight mission harness.
# Usage: ./agents/missions/highlight/run.sh <url_or_path>

set -euo pipefail

# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/env.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/log.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/ollama.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/whisper.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/ffmpeg.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/attribution.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/retry.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/copyright.sh"

SOURCE="${1:-}"
if [[ -z "$SOURCE" ]]; then
  log_err "usage: $0 <url_or_path>"
  exit 64
fi

require_bin "$FFMPEG_BIN" "$FFPROBE_BIN" "$WHISPER_CLI_BIN" "$YT_DLP_BIN" jq curl
require_env OLLAMA_HOST OLLAMA_MODEL_HIGHLIGHT WHISPER_MODEL RECORDS_DIR

# Copyright gate — refuse remote URLs that aren't on the permissive-domain
# allowlist.  Local file paths are skipped here; their licensing is handled
# by the fixture catalog instead.
if ! ALLOWED_LICENSE=$(check_source_allowed "$SOURCE"); then
  log_err "copyright gate refused source — see config/copyright-allowlist.yaml"
  exit 67
fi
log_info "copyright gate: $ALLOWED_LICENSE"

# --- Mission workspace ---------------------------------------------------
MISSION_ID="highlight-$(date +%H%M%S)"
MDIR="$RECORDS_DIR/missions/$(date +%Y-%m-%d)/$MISSION_ID"

mkdir -p "$MDIR/resources" "$MDIR/outputs"
METRICS_PATH="$MDIR/metrics.json"
MISSION_T0=$(python3 -c "import time; print(time.time())")
STAGE_TIMES=()
stage_mark() {
  local name="$1" t1=$(python3 -c "import time; print(time.time())")
  STAGE_TIMES+=("$name=$(awk -v a="${STAGE_T0:-$MISSION_T0}" -v b="$t1" 'BEGIN{printf "%.3f", b-a}')")
  STAGE_T0="$t1"
}
STAGE_T0="$MISSION_T0"

log_step "mission workspace: $MDIR"

PLAN_PATH="$MDIR/plan.md"
cat > "$PLAN_PATH" <<MDEOF
# Plan: highlight short from $SOURCE

## Goal
Extract a 30-60s standalone highlight from the source, render 9:16 with burned captions.

## Acceptance criteria
- [ ] outputs/short.mp4 exists
- [ ] duration in [30, 60] seconds
- [ ] resolution = 1080x1920
- [ ] audio stream present
- [ ] captions burned in
- [ ] file size < 50 MB

## Steps
1. resourcer: yt-dlp / copy → resources/source.mp4
2. resourcer: whisper.cpp → resources/transcript.json
3. editor: ollama selects window → resources/selection.json
4. editor: ffmpeg cut+crop+caption → outputs/short.mp4
5. qa: ffprobe + presence checks → qa-report.md
MDEOF

# --- Step 1: source --------------------------------------------------------
log_step "1/5 resourcer: fetch source"
SRC="$MDIR/resources/source.mp4"
if [[ -f "$SOURCE" ]]; then
  cp "$SOURCE" "$SRC"
  log_ok "copied local file → $SRC"
else
  "$YT_DLP_BIN" -f "best[ext=mp4][height<=720]/best[height<=720]/best" -o "$SRC" "$SOURCE" >&2
  log_ok "downloaded → $SRC"
fi

# Resolve source attribution + license (see agents/lib/attribution.sh).
# Burned into the rendered output and recorded in outputs/SOURCES.txt so the
# short can never be reposted without crediting where it came from.
resolve_source_attribution "$SOURCE"
resolve_final_license "$SOURCE" "$ALLOWED_LICENSE" "$MDIR"
log_info "final license: ${FIXTURE_LICENSE:-unknown}"

SRC_DURATION=$(ffmpeg_duration "$SRC")
log_info "source duration: ${SRC_DURATION}s"
if awk -v d="$SRC_DURATION" 'BEGIN{ exit !(d < 60) }'; then
  log_warn "source shorter than 60s — highlight extraction may not pick a great window"
fi

# --- Step 2: transcript ----------------------------------------------------
log_step "2/5 resourcer: transcribe"
TRANSCRIPT=$(whisper_transcribe "$SRC" "$MDIR/resources/transcript")
log_ok "transcript: $TRANSCRIPT"
stage_mark "transcribe"

SEGS="$MDIR/resources/segments.json"
whisper_segments "$TRANSCRIPT" > "$SEGS"
SEG_COUNT=$(jq 'length' "$SEGS")
log_info "segments: $SEG_COUNT"
if (( SEG_COUNT == 0 )); then
  log_err "no segments — source may have no speech"
  exit 65
fi

# --- Steps 3-5 wrapped in a QA-feedback retry loop -------------------------
# On QA FAIL, the next iteration re-prompts the model with the previous
# qa-report inlined (see qa_feedback_block in agents/lib/retry.sh).  Cap is
# QA_RETRY_MAX (default 2 retries → up to 3 total attempts).
PROMPT_FILE="$(dirname "${BASH_SOURCE[0]}")/select-highlight.prompt.md"
CLAMP_JQ="$REPO_ROOT/agents/lib/clamp-window.jq"
SRT="$MDIR/outputs/captions.srt"
FINAL="$MDIR/outputs/short.mp4"

VERDICT=""
QA_FEEDBACK=""
ATTEMPT=0
MAX_ATTEMPTS=$((QA_RETRY_MAX + 1))
ollama_ensure_model "$OLLAMA_MODEL_HIGHLIGHT"

while (( ATTEMPT < MAX_ATTEMPTS )); do
  ATTEMPT=$((ATTEMPT + 1))
  if (( ATTEMPT > 1 )); then
    log_warn "attempt $ATTEMPT/$MAX_ATTEMPTS — previous QA FAILED, retrying with feedback"
    QA_FEEDBACK=$(qa_extract_feedback "$MDIR/qa-report.md")
  fi

  # Step 3: highlight selection ---------------------------------------------
  log_step "3/5 editor: select highlight via ollama (attempt $ATTEMPT)"
  PROMPT="$(cat "$PROMPT_FILE")$(qa_feedback_block)

INPUT SEGMENTS:
$(cat "$SEGS")"

  SELECTION_RAW=$(ollama_generate "$OLLAMA_MODEL_HIGHLIGHT" "$PROMPT" true)
  echo "$SELECTION_RAW" > "$MDIR/resources/selection.attempt-${ATTEMPT}.raw.json"

  SELECTION=$(echo "$SELECTION_RAW" | jq -c 'if type == "object" then . else (.. | objects | select(has("start") and has("end"))) end' 2>/dev/null | head -1)
  if [[ -z "$SELECTION" ]]; then
    log_err "couldn't parse selection from ollama output (attempt $ATTEMPT)"
    VERDICT=FAIL
    # Write a stub qa-report so the retry has feedback to send next time.
    cat > "$MDIR/qa-report.md" <<STUB
# QA report — $MISSION_ID (attempt $ATTEMPT)

**Verdict**: FAIL

## Acceptance criteria
- [ ] model output parseable — model returned non-JSON or missing start/end
STUB
    continue
  fi
  echo "$SELECTION" > "$MDIR/resources/selection.json"

  RAW_START=$(echo "$SELECTION" | jq -r '.start')
  RAW_END=$(echo "$SELECTION" | jq -r '.end')
  REASON=$(echo "$SELECTION" | jq -r '.reason // ""')
  log_info "model picked: ${RAW_START}s → ${RAW_END}s (${REASON})"

  WINDOW=$(jq -cn \
    --argjson rs "$RAW_START" --argjson re "$RAW_END" \
    --argjson src_dur "$SRC_DURATION" \
    --slurpfile segs "$SEGS" \
    -f "$CLAMP_JQ")
  START=$(echo "$WINDOW" | jq -r '.start')
  END=$(echo "$WINDOW" | jq -r '.end')
  DUR_W=$(awk -v s="$START" -v e="$END" 'BEGIN{ printf "%.2f", e - s }')
  log_ok "clamped window: ${START}s → ${END}s (${DUR_W}s)"
  stage_mark "select"
  jq -n --arg s "$START" --arg e "$END" --arg r "$REASON" \
        --argjson rs "$RAW_START" --argjson re "$RAW_END" --argjson att "$ATTEMPT" \
     '{start: ($s|tonumber), end: ($e|tonumber), reason: $r, model_start: $rs, model_end: $re, attempt: $att}' \
     > "$MDIR/resources/selection.json"

  # Step 4: render ----------------------------------------------------------
  log_step "4/5 editor: cut + crop 9:16 + burn captions (attempt $ATTEMPT)"
  ffmpeg_segments_to_srt "$SEGS" "$START" "$END" "$SRT"
  ffmpeg_render_short "$SRC" "$START" "$END" "$SRT" "$FINAL" "$SOURCE_ATTRIBUTION"
  log_ok "rendered → $FINAL"
  stage_mark "render"

  # Step 5: QA --------------------------------------------------------------
  log_step "5/5 qa: validate (attempt $ATTEMPT)"
  DUR=$(ffmpeg_duration "$FINAL")
  RES=$("$FFPROBE_BIN" -v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 "$FINAL")
  HAS_AUDIO=$("$FFPROBE_BIN" -v error -select_streams a:0 -show_entries stream=index -of csv=p=0 "$FINAL" | head -1)
  SIZE_BYTES=$(stat -f%z "$FINAL" 2>/dev/null || stat -c%s "$FINAL")
  SIZE_MB=$(awk -v b="$SIZE_BYTES" 'BEGIN{ printf "%.2f", b/1048576 }')

  # Tolerance accommodates h264_videotoolbox GOP-aligned overshoot:
  # asking ffmpeg for exactly 60.000s of output often produces 60.005s →
  # without tolerance the QA flags a render that satisfied the contract.
  DUR_OK=$(awk -v d="$DUR" -v dmin="${QA_DUR_MIN:-30}" -v dmax="${QA_DUR_MAX:-60}" -v tol="${QA_DUR_TOLERANCE_S:-0.5}" 'BEGIN{ print (d >= dmin - tol && d <= dmax + tol) ? "PASS" : "FAIL" }')
  RES_OK=$([[ "$RES" == "1080,1920" ]] && echo PASS || echo FAIL)
  AUDIO_OK=$([[ -n "$HAS_AUDIO" ]] && echo PASS || echo FAIL)
  SIZE_OK=$(awk -v m="$SIZE_MB" 'BEGIN{ print (m < 50) ? "PASS" : "FAIL" }')
  EXIST_OK=$([[ -f "$FINAL" ]] && echo PASS || echo FAIL)
  SRT_OK=$([[ -s "$SRT" ]] && echo PASS || echo FAIL)

  VERDICT=PASS
  for v in "$EXIST_OK" "$DUR_OK" "$RES_OK" "$AUDIO_OK" "$SIZE_OK" "$SRT_OK"; do
    [[ "$v" == "FAIL" ]] && VERDICT=FAIL
  done

  cat > "$MDIR/qa-report.md" <<MDEOF
# QA report — $MISSION_ID (attempt $ATTEMPT of $MAX_ATTEMPTS)

**Verdict**: $VERDICT

## Acceptance criteria
- [$([ "$EXIST_OK" = PASS ] && echo x || echo ' ')] outputs/short.mp4 exists — \`$FINAL\`
- [$([ "$DUR_OK" = PASS ] && echo x || echo ' ')] duration in [${QA_DUR_MIN:-30}, ${QA_DUR_MAX:-60}]s — observed \`${DUR}s\`
- [$([ "$RES_OK" = PASS ] && echo x || echo ' ')] resolution = 1080x1920 — observed \`${RES}\`
- [$([ "$AUDIO_OK" = PASS ] && echo x || echo ' ')] audio stream present — index \`${HAS_AUDIO:-none}\`
- [$([ "$SIZE_OK" = PASS ] && echo x || echo ' ')] file size < 50 MB — observed \`${SIZE_MB} MB\`
- [$([ "$SRT_OK" = PASS ] && echo x || echo ' ')] SRT captions sidecar shipped — \`$SRT\`

## Selection
\`\`\`json
$SELECTION
\`\`\`

## Source
- file: \`$SRC\`
- duration: ${SRC_DURATION}s
- segments: $SEG_COUNT
MDEOF
  stage_mark "qa"

  [[ "$VERDICT" == PASS ]] && break
done

# Machine-readable attribution record — written once on PASS (or final FAIL).
write_sources_record "$MISSION_ID" "$SOURCE" "$MDIR/outputs/SOURCES.txt"

MISSION_T1=$(python3 -c "import time; print(time.time())")
TOTAL_S=$(awk -v a="$MISSION_T0" -v b="$MISSION_T1" 'BEGIN{printf "%.3f", b-a}')
python3 - "$METRICS_PATH" "$TOTAL_S" "$VERDICT" "$SIZE_MB" "$DUR" "${STAGE_TIMES[@]}" <<PYME
import json, sys, pathlib
path = sys.argv[1]
total = float(sys.argv[2])
verdict = sys.argv[3]
size_mb = float(sys.argv[4])
duration_s = float(sys.argv[5])
stages = {}
for kv in sys.argv[6:]:
    k, v = kv.split("=", 1)
    stages[k] = float(v)
data = {"total_s": total, "verdict": verdict, "size_mb": size_mb, "output_duration_s": duration_s, "stages_s": stages}
pathlib.Path(path).write_text(json.dumps(data, indent=2))
PYME
cat > "$MDIR/summary.md" <<MDEOF
# Summary — $MISSION_ID

- source: $SOURCE
- selection: ${START}s → ${END}s — $REASON
- output: $FINAL ($SIZE_MB MB, ${DUR}s, $RES)
- verdict: $VERDICT

## Source & license
- attribution: $SOURCE_ATTRIBUTION
- license: ${FIXTURE_LICENSE:-unknown}
- record: $MDIR/outputs/SOURCES.txt
MDEOF

if [[ "$VERDICT" == PASS ]]; then
  log_ok "mission $MISSION_ID complete on attempt $ATTEMPT — $FINAL"
else
  BLOCKER=$(qa_write_blocker "$MISSION_ID" "$MDIR" "$ATTEMPT")
  log_err "mission $MISSION_ID FAILED after $ATTEMPT attempts — blocker: $BLOCKER"
  exit 1
fi
