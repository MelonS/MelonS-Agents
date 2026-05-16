#!/usr/bin/env bash
# Faceless-short mission — script-driven 60-second 9:16 short.
#
# Pipeline:
#   1. ollama writes the 60s narration script for the supplied topic
#   2. Kokoro TTS (or `say` fallback) synthesizes the narration → WAV
#   3. whisper.cpp transcribes the synthesized narration → SRT
#   4. ollama extracts visual search terms from the script
#   5. scripts/pexels-fetch.sh pulls one B-roll per term (Pexels License)
#   6. ffmpeg trims, concatenates, 9:16-letterbox-blurs, overlays
#      narration audio, burns captions, and adds source-attribution overlay
#
# Usage:
#   ./agents/missions/faceless-short/run.sh <short_id> "<topic_prompt>"
#
# Examples:
#   ./agents/missions/faceless-short/run.sh hittites \
#     "The Hittites — a biblical kingdom dismissed by historians as legend until 1906"
#
#   ./agents/missions/faceless-short/run.sh hydrogen \
#     "Hydrogen — 75 percent of the universe and 10 percent of your body"

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/log.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/ollama.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/whisper.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/ffmpeg.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/tts.sh"

SHORT_ID="${1:-}"
TOPIC="${2:-}"
if [[ -z "$SHORT_ID" || -z "$TOPIC" ]]; then
  log_err "usage: $0 <short_id> \"<topic_prompt>\""
  exit 64
fi

require_bin "$FFMPEG_BIN" "$FFPROBE_BIN" "$WHISPER_CLI_BIN" jq curl
require_env OLLAMA_HOST OLLAMA_MODEL_HIGHLIGHT WHISPER_MODEL RECORDS_DIR PEXELS_API_KEY

VOICE="${FACELESS_VOICE:-am_michael}"
NUM_BROLL="${FACELESS_NUM_BROLL:-6}"

MISSION_ID="faceless-${SHORT_ID}-$(date +%H%M%S)"
MDIR="$RECORDS_DIR/missions/$(date +%Y-%m-%d)/$MISSION_ID"
mkdir -p "$MDIR/resources/broll" "$MDIR/outputs"

log_step "mission workspace: $MDIR"

# --- Stage 1: script generation ----------------------------------------
log_step "1/6  ollama → narration script"

SCRIPT_PROMPT="You are writing a 60-second faceless YouTube short narration.

Topic: $TOPIC

Requirements:
- 130 to 160 words total (60 seconds at typical narration speed)
- Open with a HOOK — one striking sentence or question
- 3 middle beats that develop the topic with concrete detail
- Close with one impactful, memorable sentence
- Neutral documentary tone, conversational English
- Plain prose only — no markdown, no labels, no stage directions
- Factually grounded — avoid sweeping unverifiable claims

Output ONLY the narration text, nothing else."

SCRIPT_PATH="$MDIR/resources/script.txt"
if [[ -n "${FACELESS_SCRIPT_OVERRIDE:-}" && -f "$FACELESS_SCRIPT_OVERRIDE" ]]; then
  log_info "  script override: $FACELESS_SCRIPT_OVERRIDE"
  cp "$FACELESS_SCRIPT_OVERRIDE" "$SCRIPT_PATH"
else
  ollama_generate "$OLLAMA_MODEL_HIGHLIGHT" "$SCRIPT_PROMPT" false > "$SCRIPT_PATH"
fi
# Strip leading blank lines + any trailing whitespace
awk 'NF || found{found=1; print}' "$SCRIPT_PATH" | sed 's/[[:space:]]*$//' > "$SCRIPT_PATH.tmp"
mv "$SCRIPT_PATH.tmp" "$SCRIPT_PATH"

WORD_COUNT=$(wc -w < "$SCRIPT_PATH" | tr -d ' ')
log_ok "script: $WORD_COUNT words"

# --- Stage 2: TTS narration --------------------------------------------
log_step "2/6  TTS → narration.wav"

NARRATION_WAV="$MDIR/resources/narration.wav"
tts_synthesize "$SCRIPT_PATH" "$NARRATION_WAV" "$VOICE"

NARRATION_DUR=$("$FFPROBE_BIN" -v error -show_entries format=duration -of default=nw=1:nk=1 "$NARRATION_WAV" | awk '{printf "%.2f",$1}')
log_ok "narration: ${NARRATION_DUR}s"

# --- Stage 3: whisper → caption SRT ------------------------------------
log_step "3/6  whisper → captions"

TRANSCRIPT_PREFIX="$MDIR/resources/transcript"
SEGMENTS_JSON="$MDIR/resources/segments.json"
SRT_PATH="$MDIR/resources/captions.srt"

TRANSCRIPT_JSON=$(WHISPER_LANG="${WHISPER_LANG:-en}" whisper_transcribe "$NARRATION_WAV" "$TRANSCRIPT_PREFIX")
whisper_segments "$TRANSCRIPT_JSON" > "$SEGMENTS_JSON"

# Whisper gives us TIMING; the script is the ground truth for TEXT.
# Run script-aware caption correction so proper nouns like "Hattusa"
# don't leak through as "Hadusa" (small-model whisper drift on names).
SRT_RAW="${SRT_PATH%.srt}.raw.srt"
ffmpeg_segments_to_srt "$SEGMENTS_JSON" 0 "$NARRATION_DUR" "$SRT_RAW"
if python3 "$REPO_ROOT/scripts/correct-captions.py" \
      --script "$SCRIPT_PATH" \
      --srt-in "$SRT_RAW" \
      --srt-out "$SRT_PATH" \
      --verbose 2> "$MDIR/resources/caption-corrections.log"; then
  CORR_COUNT=$(grep -c "→" "$MDIR/resources/caption-corrections.log" || true)
  log_ok "captions: $(grep -c '^[0-9]' "$SRT_PATH") segments (corrected $CORR_COUNT)"
else
  log_warn "caption correction failed — using raw whisper output"
  cp "$SRT_RAW" "$SRT_PATH"
  log_ok "captions: $(grep -c '^[0-9]' "$SRT_PATH") segments (uncorrected)"
fi

# --- Stage 4: visual keyword extraction --------------------------------
log_step "4/6  ollama → B-roll search terms"

KW_PROMPT="Read this 60-second narration script:

$(cat "$SCRIPT_PATH")

Extract exactly ${NUM_BROLL} concrete visual search terms.  Each term:
- 2 to 4 words
- Concrete imagery (not abstract concepts)
- Searchable on a stock-footage site like Pexels

Output JSON in this exact shape:
{\"terms\": [\"term1\", \"term2\", \"term3\", \"term4\", \"term5\", \"term6\"]}"

KEYWORDS_JSON=$(ollama_generate "$OLLAMA_MODEL_HIGHLIGHT" "$KW_PROMPT" true)
echo "$KEYWORDS_JSON" > "$MDIR/resources/keywords.json"

TERMS=()
while IFS= read -r __term; do
  [[ -n "$__term" ]] && TERMS+=("$__term")
done < <(echo "$KEYWORDS_JSON" | jq -r '.terms[]')
if (( ${#TERMS[@]} == 0 )); then
  log_err "ollama returned no terms — falling back to topic itself"
  TERMS=("$TOPIC")
fi
log_ok "search terms (${#TERMS[@]}): ${TERMS[*]}"

# --- Stage 5: B-roll fetching ------------------------------------------
log_step "5/6  pexels → ${#TERMS[@]} B-roll clips"

BROLL_DIR="$MDIR/resources/broll"
for i in "${!TERMS[@]}"; do
  TERM="${TERMS[$i]}"
  TERM_DIR="$BROLL_DIR/term-$i"
  mkdir -p "$TERM_DIR"
  log_info "  [$((i+1))/${#TERMS[@]}] fetching: $TERM"
  if ! FIXTURE_DIR="$TERM_DIR" "$REPO_ROOT/scripts/pexels-fetch.sh" "$TERM" 1 720 > /dev/null 2>&1; then
    log_warn "    no result for: $TERM"
  fi
done

BROLL_FILES=()
while IFS= read -r __f; do
  [[ -n "$__f" ]] && BROLL_FILES+=("$__f")
done < <(find "$BROLL_DIR" -name "*.mp4" -type f | sort)
if (( ${#BROLL_FILES[@]} < 3 )); then
  log_err "got only ${#BROLL_FILES[@]} B-roll clips (need at least 3)"
  exit 70
fi
log_ok "B-roll: ${#BROLL_FILES[@]} clips"

# --- Stage 6: stitch + render ------------------------------------------
log_step "6/6  ffmpeg → stitch + 9:16 fill + audio + captions"

PER_CLIP_DUR=$(awk -v n="${#BROLL_FILES[@]}" -v d="$NARRATION_DUR" 'BEGIN{printf "%.3f", d/n}')
log_info "  per-clip duration: ${PER_CLIP_DUR}s × ${#BROLL_FILES[@]} clips"

# Trim each clip + crop directly to 1080×1920 (9:16 fill).  Earlier
# versions kept clips at 1920×1080 landscape and then letterbox-blurred
# them into 9:16, but the result was a tiny strip of content in the
# middle of a mostly-blurred frame — Pexels stock is almost always
# landscape, so the letterbox dominates.  Crop-to-fill is what TikTok
# and Reels actually do; for abstract B-roll the subject is centered
# anyway so center-crop reads cleanly.
CONCAT_LIST="$MDIR/resources/concat-list.txt"
: > "$CONCAT_LIST"
for i in "${!BROLL_FILES[@]}"; do
  CLIP="${BROLL_FILES[$i]}"
  TRIMMED="$MDIR/resources/trimmed-$i.mp4"
  "$FFMPEG_BIN" -y -loglevel error -i "$CLIP" \
    -t "$PER_CLIP_DUR" \
    -r 30 \
    -vf "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,setsar=1" \
    -an \
    -c:v libx264 -preset ultrafast -crf 28 \
    "$TRIMMED"
  printf "file '%s'\n" "$(cd "$(dirname "$TRIMMED")" && pwd)/$(basename "$TRIMMED")" >> "$CONCAT_LIST"
done

CONCAT_NOAUDIO="$MDIR/resources/concat-noaudio.mp4"
"$FFMPEG_BIN" -y -loglevel error -f concat -safe 0 -i "$CONCAT_LIST" -c copy "$CONCAT_NOAUDIO"

# Build the ASS sidecar with the project's layout-engine constants.
ASS_PATH="${SRT_PATH%.srt}.ass"
ffmpeg_srt_to_ass "$SRT_PATH" "$ASS_PATH"
ASS_BASE="$(basename "$ASS_PATH")"
ASS_DIR="$(cd "$(dirname "$ASS_PATH")" && pwd)"

# Attribution overlay text.
ATTRIBUTION_TEXT="$MDIR/resources/source-attribution.txt"
{
  echo "B-roll: Pexels License — pexels.com"
  echo "Narration: ${TTS_LABEL:-Kokoro TTS} (synthetic)"
} > "$ATTRIBUTION_TEXT"
ATTR_BASE="$(basename "$ATTRIBUTION_TEXT")"

OUT="$MDIR/outputs/short.mp4"
ABS_OUT="$(cd "$(dirname "$OUT")" && pwd)/$(basename "$OUT")"
ABS_CONCAT="$(cd "$(dirname "$CONCAT_NOAUDIO")" && pwd)/$(basename "$CONCAT_NOAUDIO")"
ABS_NARRATION="$(cd "$(dirname "$NARRATION_WAV")" && pwd)/$(basename "$NARRATION_WAV")"

# Filter graph: concat clips are already 1080×1920 from the trim step,
# so the final filter only needs to burn captions (and optionally the
# top-left attribution overlay).  No blurred-background letterbox.
FILTER="[0:v]ass=${ASS_BASE}"
if [[ -f "${LAYOUT_DRAWTEXT_FONTFILE:-}" ]]; then
  FILTER="${FILTER},drawtext=fontfile=${LAYOUT_DRAWTEXT_FONTFILE}:textfile=${ATTR_BASE}:fontsize=22:fontcolor=white@0.75:box=1:boxcolor=black@0.45:boxborderw=10:x=40:y=40"
else
  log_warn "  LAYOUT_DRAWTEXT_FONTFILE not set or not found — skipping attribution overlay"
fi
FILTER="${FILTER}[outv]"

(
  cd "$ASS_DIR"
  "$FFMPEG_BIN" -y -loglevel error \
    -i "$ABS_CONCAT" \
    -i "$ABS_NARRATION" \
    -filter_complex "$FILTER" \
    -map "[outv]" -map 1:a \
    -c:v libx264 -preset medium -crf 23 -c:a aac -b:a 128k \
    -shortest -movflags +faststart \
    "$ABS_OUT"
)

# Caption-verify still frame (single representative frame at ~mid-mission).
MID_T=$(awk -v d="$NARRATION_DUR" 'BEGIN{printf "%.2f", d/2}')
"$FFMPEG_BIN" -y -loglevel error -ss "$MID_T" -i "$OUT" -frames:v 1 \
  "$MDIR/outputs/caption-verify.jpg"

OUT_SIZE_MB=$(( $(wc -c < "$OUT") / 1024 / 1024 ))
OUT_DUR=$("$FFPROBE_BIN" -v error -show_entries format=duration -of default=nw=1:nk=1 "$OUT" | awk '{printf "%.1f",$1}')

log_ok "rendered: $OUT (${OUT_DUR}s, ${OUT_SIZE_MB} MB)"
log_step "mission $MISSION_ID complete"

# Write minimal metadata + plan trace for the dashboard
cat > "$MDIR/metrics.json" <<EOF
{
  "total_s": ${NARRATION_DUR},
  "verdict": "PASS",
  "size_mb": ${OUT_SIZE_MB},
  "output_duration_s": ${OUT_DUR},
  "topic": "${TOPIC//\"/\\\"}",
  "voice": "${VOICE}",
  "broll_count": ${#BROLL_FILES[@]},
  "script_words": ${WORD_COUNT}
}
EOF

echo
echo "next: review $OUT and caption-verify frame at $MDIR/outputs/caption-verify.jpg"
