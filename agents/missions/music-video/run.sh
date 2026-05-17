#!/usr/bin/env bash
# Music-video mission — music-as-primary-audio shorts.
#
# Pipeline (no narration, no captions — distinct from faceless-short):
#   1. aubiotrack on music → real-beat timestamps → phrase boundaries
#      (every Nth beat, default 12 ≈ 7.5s at 95 BPM)
#   2. aubioonset on music → drum-hit timestamps for glitch placement
#   3. Per-segment, fetch a Pexels portrait clip matching one mood keyword
#   4. Variable per-clip playback speed (setpts) by keyword class
#      (calm / slow / natural / active)
#   5. Glitch micro-edit (reverse + jump-cut) at a strong onset inside
#      static-camera clips only
#   6. Concat segments, mix music as sole audio, output 9:16 mp4
#
# Why this exists alongside faceless-short:
#   The "music shorts" niche turned out to be music-as-feature, not
#   narration-about-music.  faceless-short renders narration-driven
#   shorts (TTS + captions + per-window narration B-roll).  music-video
#   renders music-driven shorts (no TTS, no captions, mood-keyword B-roll
#   + phrase-aligned cuts + onset-aligned glitches).  Validated 2026-05-17.
#   See docs/pilots/decision-log.md "Music-video mode (post-pivot)".
#
# Usage:
#   agents/missions/music-video/run.sh <short_id> <music_file> [keywords_csv]
#
# Example:
#   agents/missions/music-video/run.sh velvet1 \
#     "assets/music/Velvet Turntable1.mp3" \
#     "lofi cafe,vintage turntable,rainy neon window,coffee shop interior,vinyl record spinning,rooftop city night,cozy reading room,warm soft lights"
#
# If keywords_csv omitted, falls back to the validated lo-fi/cafe pool.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/log.sh"

SHORT_ID="${1:-}"
MUSIC_FILE="${2:-}"
KEYWORDS_CSV="${3:-lofi cafe,vintage turntable,rainy neon window,coffee shop interior,vinyl record spinning,rooftop city night,cozy reading room,warm soft lights}"

if [[ -z "$SHORT_ID" || -z "$MUSIC_FILE" ]]; then
  log_err "usage: $0 <short_id> <music_file> [keywords_csv]"
  exit 64
fi
[[ -f "$MUSIC_FILE" ]] || { log_err "music file not found: $MUSIC_FILE"; exit 64; }
require_bin "$FFMPEG_BIN" "$FFPROBE_BIN" aubiotrack aubioonset jq curl
require_env PEXELS_API_KEY RECORDS_DIR

# Pre-render disk-space guard (mirrors faceless-short).
MIN_FREE_GB="${MUSIC_VIDEO_MIN_FREE_GB:-3}"
_free_gb=$(( $(df -k . | awk '$NF=="/" || NR==2 {print $4; exit}') / 1024 / 1024 ))
if (( _free_gb < MIN_FREE_GB )); then
  log_err "ABORT: only ${_free_gb} GB free; need >= ${MIN_FREE_GB} GB for a render."
  log_err "  scripts/cleanup-records.sh"
  exit 65
fi

MISSION_ID="music-video-${SHORT_ID}-$(date +%H%M%S)"
MDIR="$RECORDS_DIR/missions/$(date +%Y-%m-%d)/$MISSION_ID"
CLIPS_DIR="$MDIR/resources/clips"
mkdir -p "$CLIPS_DIR" "$MDIR/outputs"

# ───── 1. beat extraction + phrase boundaries ─────
log_step "1/5  beat extraction (aubiotrack)"
RAW_BEATS="$MDIR/resources/beats-raw.txt"
REAL_BEATS="$MDIR/resources/beats-real.txt"
aubiotrack -i "$MUSIC_FILE" 2>/dev/null > "$RAW_BEATS"
# Filter sub-beats: keep only beats spaced >= 0.4s apart (rejects 16th-note false positives)
awk 'BEGIN{prev=0} {if ($1 - prev >= 0.4) {print $1; prev=$1}}' "$RAW_BEATS" > "$REAL_BEATS"
BEAT_COUNT=$(wc -l < "$REAL_BEATS" | tr -d ' ')
# Estimate BPM from median of first-30 beat intervals
BPM=$(awk 'NR<=30 {print}' "$REAL_BEATS" | awk 'NR>1 {print $1-prev} {prev=$1}' | sort -n | awk 'NR==15 {printf "%.1f", 60/$1}')
log_info "  $BEAT_COUNT real beats, ~$BPM BPM"

# Phrase boundaries = every PHRASE_BEATS beats; default 12 (≈3 bars at 4/4 in lofi tempo)
PHRASE_BEATS="${MUSIC_VIDEO_PHRASE_BEATS:-12}"
TARGET_DUR="${MUSIC_VIDEO_DURATION:-60}"
BOUNDARIES="$MDIR/resources/cuts.txt"
awk -v step="$PHRASE_BEATS" -v lim="$TARGET_DUR" 'NR % step == 1 && $1 <= lim+0.5 {print $1}' "$REAL_BEATS" > "$BOUNDARIES"
SEG_COUNT_BOUNDED=$(wc -l < "$BOUNDARIES" | tr -d ' ')
log_info "  $SEG_COUNT_BOUNDED phrase boundaries (every $PHRASE_BEATS beats, ≤${TARGET_DUR}s)"

# ───── 2. onset extraction (for glitch placement) ─────
log_step "2/5  onset extraction (aubioonset, drum-hit strength)"
ONSETS_FILE="$MDIR/resources/onsets.txt"
aubioonset -i "$MUSIC_FILE" -O complex -t 2.0 2>/dev/null | awk -v lim="$TARGET_DUR" '$1 <= lim' > "$ONSETS_FILE"
ONSET_COUNT=$(wc -l < "$ONSETS_FILE" | tr -d ' ')
log_info "  $ONSET_COUNT strong onsets in first ${TARGET_DUR}s"

# ───── 3. compute segment plan ─────
log_step "3/5  segment plan"
IFS=',' read -r -a KEYWORDS <<< "$KEYWORDS_CSV"
KW_COUNT="${#KEYWORDS[@]}"

# Read boundaries into array (bash 3.2 compatible — no mapfile).
BOUNDS=()
while IFS= read -r line; do
  [[ -n "$line" ]] && BOUNDS+=("$line")
done < "$BOUNDARIES"
[[ "${#BOUNDS[@]}" -lt 2 ]] && { log_err "not enough phrase boundaries (${#BOUNDS[@]})"; exit 1; }

# Build segment array.  Segments 0..N-1 are phrase blocks, segment N is the tail to TARGET_DUR.
declare -a SEG_START SEG_DUR SEG_KW SEG_SPEED SEG_GLITCH
# Segment 0: intro hold 0 → BOUNDS[0]
SEG_START[0]="0"
SEG_DUR[0]=$(awk -v e="${BOUNDS[0]}" 'BEGIN{printf "%.3f", e}')
SEG_KW[0]="${KEYWORDS[0]}"  # first keyword used as the intro motif

# Inner segments
seg_idx=1
for ((i=0; i<${#BOUNDS[@]}-1; i++)); do
  SEG_START[seg_idx]="${BOUNDS[$i]}"
  SEG_DUR[seg_idx]=$(awk -v s="${BOUNDS[$i]}" -v e="${BOUNDS[$((i+1))]}" 'BEGIN{printf "%.3f", e-s}')
  # Pick keyword for this segment, with the intro keyword acting as a recurring motif at every 3rd slot
  if (( seg_idx % 3 == 0 )); then
    SEG_KW[seg_idx]="${KEYWORDS[0]}"  # motif
  else
    pick=$(( (seg_idx % (KW_COUNT - 1)) + 1 ))
    SEG_KW[seg_idx]="${KEYWORDS[$pick]}"
  fi
  seg_idx=$((seg_idx + 1))
done

# Tail segment to fill out TARGET_DUR (bash 3.2: explicit last-index)
LAST_BOUND="${BOUNDS[$((${#BOUNDS[@]}-1))]}"
TAIL_DUR=$(awk -v t="$TARGET_DUR" -v lb="$LAST_BOUND" 'BEGIN{printf "%.3f", t-lb}')
if awk -v td="$TAIL_DUR" 'BEGIN{exit !(td > 0.5)}'; then
  SEG_START[seg_idx]="$LAST_BOUND"
  SEG_DUR[seg_idx]="$TAIL_DUR"
  SEG_KW[seg_idx]="${KEYWORDS[$((KW_COUNT-1))]}"  # closing keyword
  seg_idx=$((seg_idx + 1))
fi
SEG_TOTAL="$seg_idx"

# Per-keyword speed + glitch-eligibility heuristic.
#   speed ≤ 1: slow contemplative scenes (reading, coffee, rain, lights)
#   speed ≈ 1: natural-paced scenes (turntable, table-wiping, working)
#   speed > 1: not used by default (would feel rushed against the music)
# Glitch eligibility (static_camera): keyword suggests subject motion on a
# locked frame (turntable, coffee, reading, lights) — NOT camera motion
# (rain handheld, rooftop pan, neon handheld).
classify_kw() {
  local kw="$1"
  case "$kw" in
    *turntable*|*vinyl*|*record*)        echo "1.00 static" ;;
    *cafe*|*table*|*kitchen*|*working*)  echo "1.00 static" ;;
    *coffee*|*latte*)                    echo "0.55 static" ;;
    *reading*|*book*|*study*)            echo "0.55 static" ;;
    *rooftop*|*city*|*street*)           echo "0.80 motion" ;;
    *rain*|*neon*|*window*)              echo "0.70 motion" ;;
    *lights*|*candle*|*lamp*)            echo "0.70 motion" ;;
    *)                                   echo "0.80 motion" ;;  # default conservative
  esac
}

log_info "  $SEG_TOTAL segments planned:"
for ((i=0; i<SEG_TOTAL; i++)); do
  read -r SPEED CAM <<< "$(classify_kw "${SEG_KW[$i]}")"
  SEG_SPEED[$i]="$SPEED"

  # Find a strong onset inside this segment (in MUSIC seconds).
  # Skip glitch when:
  #   - clip has motion (camera not locked)
  #   - this segment is a motif appearance (KEYWORDS[0]) — motif should
  #     stay visually clean for identity reasons
  #   - this is the tail segment (i == SEG_TOTAL-1) — gentle close
  seg_end=$(awk -v s="${SEG_START[$i]}" -v d="${SEG_DUR[$i]}" 'BEGIN{printf "%.3f", s+d}')
  glitch_onset=""
  if [[ "$CAM" == "static" && -s "$ONSETS_FILE" \
        && "${SEG_KW[$i]}" != "${KEYWORDS[0]}" \
        && $i -ne $((SEG_TOTAL - 1)) ]]; then
    glitch_onset=$(awk -v s="${SEG_START[$i]}" -v e="$seg_end" '$1 > s+0.5 && $1 < e-0.5 {print $1; exit}' "$ONSETS_FILE")
  fi
  SEG_GLITCH[$i]="$glitch_onset"

  printf "    seg %02d  %-25s  start=%6.2fs  dur=%5.2fs  speed=%.2fx  cam=%s  glitch=%s\n" \
    "$i" "${SEG_KW[$i]}" "${SEG_START[$i]}" "${SEG_DUR[$i]}" "$SPEED" "$CAM" "${glitch_onset:-—}"
done

# ───── 4. Pexels fetch + trim/glitch per segment ─────
log_step "4/5  Pexels fetch + per-segment trim/glitch"
# Cache via filesystem (bash 3.2 has no associative arrays).  Same keyword
# always maps to the same raw clip path, so motif segments reuse the same
# Pexels download = intentional motif.
TRIM_LIST="$MDIR/concat.txt"
: > "$TRIM_LIST"

REV_DUR="0.20"
SKIP_FWD="0.20"

for ((i=0; i<SEG_TOTAL; i++)); do
  kw="${SEG_KW[$i]}"
  cache_key=$(echo "$kw" | tr ' ' '_')
  RAW="$CLIPS_DIR/raw-${cache_key}.mp4"

  if [[ ! -f "$RAW" ]]; then
    log_info "  fetch [$i] '$kw'"
    enc=$(printf '%s' "$kw" | jq -sRr @uri)
    json="$CLIPS_DIR/win-${cache_key}.json"
    curl -sS "https://api.pexels.com/videos/search?query=${enc}&orientation=portrait&per_page=10&size=medium" \
      -H "Authorization: $PEXELS_API_KEY" > "$json"
    url=$(jq -r '.videos | sort_by(-.duration) | .[]? | .video_files[]? | select((.quality=="hd" or .quality=="sd") and .width<=1280) | .link' "$json" 2>/dev/null | head -1)
    if [[ -z "$url" || "$url" == "null" ]]; then
      url=$(jq -r '.videos[0].video_files[0].link // empty' "$json")
    fi
    if [[ -z "$url" ]]; then
      log_warn "  WARN no Pexels result for '$kw' — substituting first cached raw"
      first_raw="$(ls "$CLIPS_DIR"/raw-*.mp4 2>/dev/null | head -1)"
      [[ -z "$first_raw" ]] && { log_err "no raw fallback available"; exit 1; }
      RAW="$first_raw"
    else
      curl -sSL "$url" -o "$RAW"
    fi
  else
    log_info "  cache hit [$i] '$kw' → reusing $(basename "$RAW")"
  fi

  DUR="${SEG_DUR[$i]}"
  SPEED="${SEG_SPEED[$i]}"
  GLITCH="${SEG_GLITCH[$i]}"
  OUT="$CLIPS_DIR/seg-$(printf '%02d' "$i").mp4"
  SRC_DUR=$(awk -v d="$DUR" -v s="$SPEED" 'BEGIN{printf "%.3f", d*s}')

  # Vary in-point for recurring motif keyword (segment 3, 6 reuse intro keyword)
  START="0"
  if (( i > 0 )) && [[ "$kw" == "${KEYWORDS[0]}" ]]; then
    START=$(awk -v i="$i" 'BEGIN { printf "%.2f", 2.0 + i*1.5 }')
  fi

  if [[ -z "$GLITCH" ]]; then
    "$FFMPEG_BIN" -y -loglevel error \
      -ss "$START" -t "$SRC_DUR" -i "$RAW" \
      -vf "setpts=PTS/${SPEED},scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920" \
      -an -c:v libx264 -preset medium -crf 23 -r 30 \
      "$OUT"
  else
    OUT_OFF=$(awk -v o="$GLITCH" -v s="${SEG_START[$i]}" 'BEGIN{printf "%.3f", o-s}')
    SRC_OFF=$(awk -v oo="$OUT_OFF" -v sp="$SPEED" 'BEGIN{printf "%.3f", oo*sp}')
    PRE_END="$SRC_OFF"
    REV_FROM=$(awk -v pe="$PRE_END" -v rv="$REV_DUR" 'BEGIN{printf "%.3f", pe-rv}')
    POST_START=$(awk -v pe="$PRE_END" -v sk="$SKIP_FWD" 'BEGIN{printf "%.3f", pe+sk}')

    "$FFMPEG_BIN" -y -loglevel error \
      -ss "$START" -i "$RAW" \
      -filter_complex "
        [0:v]trim=0:${PRE_END},setpts=PTS-STARTPTS[a];
        [0:v]trim=${REV_FROM}:${PRE_END},setpts=PTS-STARTPTS,reverse[b];
        [0:v]trim=${POST_START}:${SRC_DUR},setpts=PTS-STARTPTS[c];
        [a][b][c]concat=n=3:v=1,setpts=PTS/${SPEED},scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920[outv]
      " \
      -map "[outv]" -an -c:v libx264 -preset medium -crf 23 -r 30 \
      "$OUT"
  fi

  echo "file '$(realpath "$OUT")'" >> "$TRIM_LIST"
done

# ───── 5. concat + final mix ─────
log_step "5/5  concat + final mix"
CONCAT="$MDIR/resources/concat-noaudio.mp4"
"$FFMPEG_BIN" -y -loglevel error -f concat -safe 0 -i "$TRIM_LIST" -c copy "$CONCAT"

OUT="$MDIR/outputs/short.mp4"
"$FFMPEG_BIN" -y -loglevel error \
  -i "$CONCAT" \
  -stream_loop -1 -i "$MUSIC_FILE" \
  -map "0:v" -map "1:a" \
  -c:v copy -c:a aac -b:a 192k \
  -t "$TARGET_DUR" \
  "$OUT"

dur=$("$FFPROBE_BIN" -v error -show_entries format=duration -of default=nw=1:nk=1 "$OUT" | awk '{printf "%.1f", $1}')
size=$(du -h "$OUT" | awk '{print $1}')
log_ok "rendered: $OUT (${dur}s, $size)"

# Caption-verify still frame at mid-mission (re-uses faceless-short convention)
MID_T=$(awk -v d="$dur" 'BEGIN{printf "%.2f", d/2}')
"$FFMPEG_BIN" -y -loglevel error -ss "$MID_T" -i "$OUT" -frames:v 1 \
  "$MDIR/outputs/preview-frame.jpg" 2>/dev/null || true

# Write mission-level metrics for the auditor + downstream tooling
cat > "$MDIR/metrics.json" <<EOF
{
  "mission_id": "$MISSION_ID",
  "mission_type": "music-video",
  "music_file": "$MUSIC_FILE",
  "bpm": $BPM,
  "real_beats": $BEAT_COUNT,
  "phrase_beats": $PHRASE_BEATS,
  "target_duration_s": $TARGET_DUR,
  "actual_duration_s": $dur,
  "segments": $SEG_TOTAL,
  "onsets_detected": $ONSET_COUNT,
  "output_path": "$OUT"
}
EOF

echo
log_info "  metrics: $MDIR/metrics.json"
open "$OUT" 2>/dev/null || true
