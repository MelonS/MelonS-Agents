#!/usr/bin/env bash
# Faceless-short mission — script-driven 60-second 9:16 short.
#
# Pipeline:
#   1. ollama writes the 60s narration script for the supplied topic
#   2. TTS synthesizes the narration → WAV (edge-tts neural for *Neural voices
#      incl. Korean ko-KR-*; else Kokoro-ONNX, or `say` on macOS)
#   3. captions → SRT: prefer edge-tts's script-aligned .edge.srt; otherwise
#      whisper.cpp ASR + script-aware correction
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

# Pre-render disk-space guard.  A single faceless-short render writes
# ~300-500 MB of intermediates (B-roll downloads, narration WAV,
# concat-noaudio.mp4) before scripts/cleanup-records.sh prunes them.
# Aborting BEFORE the pipeline starts beats discovering "device full"
# four minutes into ffmpeg.  Override with FACELESS_MIN_FREE_GB=<n> in
# .env for an explicit threshold; default 3 GB.
FACELESS_MIN_FREE_GB="${FACELESS_MIN_FREE_GB:-3}"
_free_kb=$(df -k . | awk '$NF=="/" || NR==2 {print $4; exit}')
_free_gb=$(( _free_kb / 1024 / 1024 ))
if (( _free_gb < FACELESS_MIN_FREE_GB )); then
  log_err "ABORT: only ${_free_gb} GB free; need >= ${FACELESS_MIN_FREE_GB} GB for a render."
  log_err "  scripts/cleanup-records.sh    # records/ intermediates (~3 GB typical)"
  log_err "  scripts/disk-watch.sh         # current state + alert files"
  exit 65
fi
unset _free_kb _free_gb

VOICE="${FACELESS_VOICE:-am_michael}"
NUM_BROLL="${FACELESS_NUM_BROLL:-8}"

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

# --- Stage 3: caption SRT (prefer edge-tts aligned SRT, else whisper ASR) ---
log_step "3/6  captions"

SRT_PATH="$MDIR/resources/captions.srt"
SRT_CORRECTED="${SRT_PATH%.srt}.corrected.srt"
# edge-tts (ko-KR-*Neural etc.) drops a script-aligned SRT beside the WAV.
EDGE_SRT="${NARRATION_WAV%.wav}.edge.srt"

if [[ -f "$EDGE_SRT" && -z "${FACELESS_FORCE_WHISPER:-}" ]]; then
  # The edge-tts SRT's text IS the narration input (zero ASR drift) and it's
  # already timed to the audio. Skip whisper + script-correction and use it
  # directly — more accurate (no mis-transcription of Korean / proper nouns)
  # and faster (no ASR pass). Set FACELESS_FORCE_WHISPER=1 to override.
  log_info "  captions: edge-tts aligned SRT (script-exact, no ASR)"
  cp -f "$EDGE_SRT" "$SRT_CORRECTED"
else
  # Whisper ASR path (non-edge voices). Whisper gives us TIMING; the script is
  # the ground truth for TEXT, so run script-aware caption correction so proper
  # nouns like "Hattusa" don't leak through as "Hadusa" (whisper drift on names).
  log_info "  captions: whisper ASR + script correction"
  TRANSCRIPT_PREFIX="$MDIR/resources/transcript"
  SEGMENTS_JSON="$MDIR/resources/segments.json"
  SRT_RAW="${SRT_PATH%.srt}.raw.srt"
  TRANSCRIPT_JSON=$(WHISPER_LANG="${WHISPER_LANG:-en}" whisper_transcribe "$NARRATION_WAV" "$TRANSCRIPT_PREFIX")
  whisper_segments "$TRANSCRIPT_JSON" > "$SEGMENTS_JSON"
  ffmpeg_segments_to_srt "$SEGMENTS_JSON" 0 "$NARRATION_DUR" "$SRT_RAW"
  if python3 "$REPO_ROOT/scripts/correct-captions.py" \
        --script "$SCRIPT_PATH" \
        --srt-in "$SRT_RAW" \
        --srt-out "$SRT_CORRECTED" \
        --verbose 2> "$MDIR/resources/caption-corrections.log"; then
    CORR_COUNT=$(grep -c "→" "$MDIR/resources/caption-corrections.log" || true)
    log_info "  corrected $CORR_COUNT cues against source script"
  else
    log_warn "  caption correction failed — using raw whisper output"
    cp "$SRT_RAW" "$SRT_CORRECTED"
  fi
fi

# Enforce single-line caption rendering — split any cue whose text
# exceeds CHAR_MAX into multiple cues at natural punctuation breaks.
# Stops 2-line wrap overlap on mobile (BorderStyle=3 opaque boxes graze
# each other when stacked).
CAPTION_CHAR_MAX="${FACELESS_CAPTION_CHAR_MAX:-28}"
python3 "$REPO_ROOT/scripts/split-long-captions.py" \
  --srt-in "$SRT_CORRECTED" \
  --srt-out "$SRT_PATH" \
  --char-max "$CAPTION_CHAR_MAX" \
  --min-duration 1.0 2>> "$MDIR/resources/caption-corrections.log"

log_ok "captions: $(grep -c '^[0-9]' "$SRT_PATH") cues (single-line enforced)"

# --- Stages 4–5: B-roll source -----------------------------------------
# Two paths:
#   (a) Default: group caption SRT cues into NUM_BROLL temporal windows,
#       extract one search term per window via ollama, fetch one Pexels
#       clip per window.  Windows have variable duration (not equal slots)
#       so visuals track the narration's natural beat structure — the
#       clip showing while a caption is on screen actually matches that
#       caption's text.
#   (b) Reuse path (FACELESS_REUSE_BROLL=<source_mdir>): copy the per-
#       window source clips from a previous mission.  Used for localized
#       variants (same content, different language) so the A/B is purely
#       audio + captions, not also B-roll selection.
WINDOWS_JSON_PATH="$MDIR/resources/broll-windows.json"

if [[ -n "${FACELESS_REUSE_BROLL:-}" && -d "$FACELESS_REUSE_BROLL/resources" ]]; then
  log_step "4-5/6  reuse B-roll source clips from $FACELESS_REUSE_BROLL"
  cp -R "$FACELESS_REUSE_BROLL/resources/broll" "$MDIR/resources/broll" 2>/dev/null || true
  for f in "$FACELESS_REUSE_BROLL/resources/keywords.json" \
           "$FACELESS_REUSE_BROLL/resources/broll-windows.json"; do
    [[ -f "$f" ]] && cp "$f" "$MDIR/resources/"
  done
  # Re-plan windows from THIS mission's captions (Korean cues land at
  # different times than English even with identical script structure).
  python3 "$REPO_ROOT/scripts/plan-broll-windows.py" \
    --srt "$SRT_PATH" \
    --total-duration "$NARRATION_DUR" \
    --num-windows "$NUM_BROLL" \
    --out "$WINDOWS_JSON_PATH"
  BROLL_FILES=()
  while IFS= read -r __f; do
    [[ -n "$__f" ]] && BROLL_FILES+=("$__f")
  # sort -V: win-2 before win-10 — plain sort scrambles windows once NUM_BROLL
  # goes double-digit (bit us on the first 16-window mission).
  done < <(find "$MDIR/resources/broll" -name "*.mp4" -type f 2>/dev/null | sort -V)
  log_ok "reused ${#BROLL_FILES[@]} B-roll source clips, re-planned windows for this language"
else
  log_step "4/6  windowed → per-segment search terms"

  python3 "$REPO_ROOT/scripts/plan-broll-windows.py" \
    --srt "$SRT_PATH" \
    --total-duration "$NARRATION_DUR" \
    --num-windows "$NUM_BROLL" \
    --out "$WINDOWS_JSON_PATH"

  # Per-window keyword extraction.  Sends each window's text to ollama
  # one at a time with the topic as global context — small-model
  # context window stays well under limit and each search term is tied
  # to the local content.
  TERMS=()
  N_WINDOWS=$(jq 'length' "$WINDOWS_JSON_PATH")

  # Curated-terms path: when FACELESS_VISUAL_TERMS points at a file with one
  # search term per line (the research-team's curated visual_terms), use those
  # and SKIP per-window model extraction.  A 3B local model asked for English
  # stock terms from Korean narration drifts off-topic ("sword fight", "old
  # clock tower"); curated terms keep the B-roll on subject.
  if [[ -n "${FACELESS_VISUAL_TERMS:-}" && -s "${FACELESS_VISUAL_TERMS:-}" ]]; then
    CURATED_TERMS=()
    while IFS= read -r __t; do [[ -n "$__t" ]] && CURATED_TERMS+=("$__t"); done < "$FACELESS_VISUAL_TERMS"
    if (( ${#CURATED_TERMS[@]} > 0 )); then
      log_info "  using ${#CURATED_TERMS[@]} curated visual terms (skipping model extraction)"
      for w in $(seq 0 $((N_WINDOWS - 1))); do
        TERMS+=("${CURATED_TERMS[$(( w % ${#CURATED_TERMS[@]} ))]}")
        log_info "  window $w: ${TERMS[$w]}"
      done
    fi
  fi

  if (( ${#TERMS[@]} == 0 )); then
  for w in $(seq 0 $((N_WINDOWS - 1))); do
    WIN_TEXT=$(jq -r ".[$w].text" "$WINDOWS_JSON_PATH")
    KW_PROMPT="Topic: $TOPIC

Segment of narration:
$WIN_TEXT

Extract ONE concrete visual search term for this segment only.  Rules:
- 2 to 4 English words (Pexels search is English-only)
- Concrete imagery (not abstract concepts like 'history' or 'change')
- Match this segment's content specifically, not the whole topic
- Searchable on a stock-footage site

Output JSON: {\"term\": \"two to four words\"}"

    TERM=$(ollama_generate "$OLLAMA_MODEL_HIGHLIGHT" "$KW_PROMPT" true | jq -r '.term // empty')
    if [[ -z "$TERM" ]]; then
      log_warn "  window $w: no term, using topic fallback"
      TERM="$TOPIC"
    fi
    TERMS+=("$TERM")
    log_info "  window $w: $TERM"
  done
  fi

  jq -n --argjson w "$(cat "$WINDOWS_JSON_PATH")" --argjson t "$(printf '%s\n' "${TERMS[@]}" | jq -R . | jq -s .)" \
    '[range(0; $w | length) | { index: $w[.].index, start: $w[.].start, end: $w[.].end, text: $w[.].text, term: $t[.] }]' \
    > "$MDIR/resources/keywords.json"

  log_step "5/6  pexels → ${#TERMS[@]} B-roll clips (one per window)"

  BROLL_DIR="$MDIR/resources/broll"
  for i in "${!TERMS[@]}"; do
    TERM="${TERMS[$i]}"
    TERM_DIR="$BROLL_DIR/win-$i"
    mkdir -p "$TERM_DIR"
    log_info "  [$((i+1))/${#TERMS[@]}] fetching: $TERM"
    if ! FIXTURE_DIR="$TERM_DIR" "$REPO_ROOT/scripts/pexels-fetch.sh" "$TERM" 1 720 > /dev/null 2>&1; then
      log_warn "    no result for: $TERM"
    fi
  done

  # Walk windows in order; for each, take the first .mp4 found under
  # its win-<i> subdir.  This preserves window→clip alignment even when
  # some Pexels searches returned nothing.
  BROLL_FILES=()
  MISSING_WINDOWS=()
  for i in "${!TERMS[@]}"; do
    CLIP=$(find "$BROLL_DIR/win-$i" -name "*.mp4" -type f 2>/dev/null | head -1)
    if [[ -n "$CLIP" ]]; then
      BROLL_FILES+=("$CLIP")
    else
      MISSING_WINDOWS+=("$i")
      BROLL_FILES+=("")  # placeholder so indices stay aligned
    fi
  done

  # Backfill empty windows from neighbours so the timeline stays full.
  for i in "${!BROLL_FILES[@]}"; do
    if [[ -z "${BROLL_FILES[$i]}" ]]; then
      # Look right then left for the nearest filled window.
      for j in "${!BROLL_FILES[@]}"; do
        if [[ -n "${BROLL_FILES[$j]}" ]]; then
          BROLL_FILES[$i]="${BROLL_FILES[$j]}"
          log_warn "  window $i: backfilled from window $j"
          break
        fi
      done
    fi
  done

  FILLED=$(printf '%s\n' "${BROLL_FILES[@]}" | grep -c .)
  if (( FILLED < 3 )); then
    log_err "only $FILLED windows had any B-roll (need ≥ 3)"
    exit 70
  fi
  log_ok "B-roll: ${#BROLL_FILES[@]} windows filled ($FILLED unique)"
fi

# --- Stage 6: stitch + render ------------------------------------------
log_step "6/6  ffmpeg → stitch (variable-duration windows) + 9:16 fill + audio + captions"

# Per-window durations come from broll-windows.json (start/end carry
# whisper-derived narration timing).  Each clip trim length = its
# window's end - start, so the visual on screen tracks the caption
# being spoken.  Previous versions used a single PER_CLIP_DUR =
# NARRATION_DUR / N, which gave fixed 8–10s slots regardless of which
# beat of narration was playing — visuals drifted out of context.
CONCAT_LIST="$MDIR/resources/concat-list.txt"
: > "$CONCAT_LIST"
for i in "${!BROLL_FILES[@]}"; do
  CLIP="${BROLL_FILES[$i]}"
  WIN_DUR=$(jq -r ".[$i] | (.end - .start)" "$WINDOWS_JSON_PATH")
  # Floor at 1.0s — Pexels clips can be very short, and ffmpeg -t with
  # a sub-second value sometimes drops the clip entirely.
  WIN_DUR=$(awk -v d="$WIN_DUR" 'BEGIN{printf "%.3f", (d < 1.0 ? 1.0 : d)}')
  TRIMMED="$MDIR/resources/trimmed-$i.mp4"
  "$FFMPEG_BIN" -y -loglevel error -i "$CLIP" \
    -t "$WIN_DUR" \
    -r 30 \
    -vf "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,setsar=1" \
    -an \
    -c:v libx264 -preset ultrafast -crf 28 \
    "$TRIMMED"
  # Relative basename — NOT an absolute POSIX path.  ffmpeg's concat demuxer
  # resolves `file` entries against the list file's own dir, and a native
  # Windows ffmpeg mis-reads a leading-slash path (/g/..) inside a list FILE as
  # drive-root (G:\g\..).  Only argv paths get MSYS-translated, not file bytes.
  printf "file '%s'\n" "$(basename "$TRIMMED")" >> "$CONCAT_LIST"
done

CONCAT_NOAUDIO="$MDIR/resources/concat-noaudio.mp4"
# Run concat from the resources dir so the relative `file 'trimmed-N.mp4'`
# entries resolve on every platform (see the basename note above).
( cd "$MDIR/resources" && "$FFMPEG_BIN" -y -loglevel error -f concat -safe 0 \
    -i "$(basename "$CONCAT_LIST")" -c copy "$(basename "$CONCAT_NOAUDIO")" )

# Build the ASS sidecar with the project's layout-engine constants.
ASS_PATH="${SRT_PATH%.srt}.ass"
ffmpeg_srt_to_ass "$SRT_PATH" "$ASS_PATH"
ASS_BASE="$(basename "$ASS_PATH")"
ASS_DIR="$(cd "$(dirname "$ASS_PATH")" && pwd)"

# Stage the overlay font as a basename inside ASS_DIR.  drawtext (fontfile=) and
# libass (fontsdir=) both run after we `cd "$ASS_DIR"`, so a relative basename is
# portable: on Windows/Git-Bash an absolute POSIX path (/c/Windows/Fonts/..)
# inside a filter string is NOT path-translated and the native ffmpeg can't open
# it.  Copying the font in and referencing it by name works on macOS/Linux/Win.
OVERLAY_FONT_BASE=""
if [[ -f "${LAYOUT_DRAWTEXT_FONTFILE:-}" ]]; then
  OVERLAY_FONT_BASE="_overlay_font.${LAYOUT_DRAWTEXT_FONTFILE##*.}"
  cp -f "$LAYOUT_DRAWTEXT_FONTFILE" "$ASS_DIR/$OVERLAY_FONT_BASE"
fi

# Attribution overlay text.
ATTRIBUTION_TEXT="$MDIR/resources/source-attribution.txt"
{
  # B-roll attribution line is overridable: a Pexels render keeps the default,
  # an idol render that reuses official footage credits the real source.
  echo "${FACELESS_BROLL_ATTRIBUTION:-B-roll: Pexels License — pexels.com}"
  # TTS_LABEL (when set by tts.sh) already includes a "(synthetic ...)" note;
  # only append one for the default Kokoro case so it isn't doubled.
  echo "Narration: ${TTS_LABEL:-Kokoro TTS (synthetic)}"
} > "$ATTRIBUTION_TEXT"
ATTR_BASE="$(basename "$ATTRIBUTION_TEXT")"

OUT="$MDIR/outputs/short.mp4"
ABS_OUT="$(cd "$(dirname "$OUT")" && pwd)/$(basename "$OUT")"
ABS_CONCAT="$(cd "$(dirname "$CONCAT_NOAUDIO")" && pwd)/$(basename "$CONCAT_NOAUDIO")"
ABS_NARRATION="$(cd "$(dirname "$NARRATION_WAV")" && pwd)/$(basename "$NARRATION_WAV")"

# Filter graph: concat clips are already 1080×1920 from the trim step,
# so the final filter only needs to burn captions (and optionally the
# top-left attribution overlay).  No blurred-background letterbox.
# libass: fontsdir=. lets it find the staged font by family name on hosts
# without fontconfig (e.g. Windows) — harmless where fontconfig already works.
FILTER="[0:v]ass=${ASS_BASE}:fontsdir=."
# On-screen attribution overlay is OPT-IN (FACELESS_ATTRIBUTION_OVERLAY=1).
# Default OFF: disclosure/attribution lives in the video DESCRIPTION instead.
# None of our sources require an on-screen credit (Pexels License waives it;
# FLUX.1-schnell is Apache-2.0; Wan/edge-tts are permissive), and a burned
# overlay both clutters the frame and can mis-credit — e.g. a 100%-generative
# render is not "B-roll: Pexels".  source-attribution.txt is still written to
# resources/ as a provenance record regardless of this flag.
if [[ "${FACELESS_ATTRIBUTION_OVERLAY:-0}" == "1" ]]; then
  if [[ -n "$OVERLAY_FONT_BASE" ]]; then
    FILTER="${FILTER},drawtext=fontfile=${OVERLAY_FONT_BASE}:textfile=${ATTR_BASE}:fontsize=22:fontcolor=white@0.75:box=1:boxcolor=black@0.45:boxborderw=10:x=40:y=40"
  else
    log_warn "  attribution overlay requested but LAYOUT_DRAWTEXT_FONTFILE unset/missing — skipping"
  fi
fi
FILTER="${FILTER}[outv]"

# Background-music mix.  Set FACELESS_BGM_FILE to a path (absolute or
# relative to repo root) to layer a music track under the narration.
# Volume is 0.15 ≈ -16 dB by default — quiet enough to not compete with
# the TTS narration.  Looped if shorter than narration; truncated to
# narration length if longer (amix duration=first).
BGM_FILE_IN="${FACELESS_BGM_FILE:-}"
BGM_VOLUME="${FACELESS_BGM_VOLUME:-0.15}"
ABS_BGM=""
if [[ -n "$BGM_FILE_IN" ]]; then
  if [[ "$BGM_FILE_IN" = /* ]]; then
    ABS_BGM="$BGM_FILE_IN"
  else
    ABS_BGM="$REPO_ROOT/$BGM_FILE_IN"
  fi
  if [[ ! -f "$ABS_BGM" ]]; then
    log_warn "  FACELESS_BGM_FILE not found: $ABS_BGM — proceeding without BGM"
    ABS_BGM=""
  fi
fi

(
  cd "$ASS_DIR"
  if [[ -n "$ABS_BGM" ]]; then
    log_info "  BGM mix: $(basename "$ABS_BGM") (volume=$BGM_VOLUME, ~$(awk -v v="$BGM_VOLUME" 'BEGIN{printf "%.0f", 20*log(v)/log(10)}') dB)"
    "$FFMPEG_BIN" -y -loglevel error \
      -i "$ABS_CONCAT" \
      -i "$ABS_NARRATION" \
      -stream_loop -1 -i "$ABS_BGM" \
      -filter_complex "${FILTER};[2:a]volume=${BGM_VOLUME}[bgm];[1:a][bgm]amix=inputs=2:duration=first:dropout_transition=0[outa]" \
      -map "[outv]" -map "[outa]" \
      $(ffmpeg_h264_encode_args) -c:a aac -b:a 128k \
      -shortest -movflags +faststart \
      "$ABS_OUT"
  else
    "$FFMPEG_BIN" -y -loglevel error \
      -i "$ABS_CONCAT" \
      -i "$ABS_NARRATION" \
      -filter_complex "$FILTER" \
      -map "[outv]" -map 1:a \
      $(ffmpeg_h264_encode_args) -c:a aac -b:a 128k \
      -shortest -movflags +faststart \
      "$ABS_OUT"
  fi
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
