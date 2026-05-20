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

# ───── demo mode — zero-friction first-touch path ─────
# Set MUSIC_VIDEO_DEMO_MODE=1 to bypass the Pexels API + the
# operator-supplied music file requirement.  Sources come from
# scripts/fetch-demo-broll.sh (CC-BY-3.0 Blender Foundation clips)
# and scripts/fetch-demo-music.sh (CC-BY 4.0 Kevin MacLeod tracks).
# Both fetchers are idempotent; first run populates the cache.
#
# Why this exists: a brand-new clone with no PEXELS_API_KEY and no
# Suno-generated music should still produce a real music-video as
# its first interaction, so the "see a demo" promise in the README
# is satisfied with zero accounts.  See docs/roadmap.md Next #1.
DEMO_MODE="${MUSIC_VIDEO_DEMO_MODE:-0}"
FIXTURE_DIR="${FIXTURE_DIR:-/tmp/smoke}"

if [[ "$DEMO_MODE" == "1" ]]; then
  [[ -z "$SHORT_ID" ]] && SHORT_ID="demo"
  log_info "[demo-mode] populating CC-BY caches"
  "$REPO_ROOT/scripts/fetch-demo-broll.sh" >/dev/null || {
    log_err "demo B-roll fetch failed — check network"
    exit 65
  }
  if [[ -z "$MUSIC_FILE" ]]; then
    "$REPO_ROOT/scripts/fetch-demo-music.sh" >/dev/null || {
      log_err "demo music fetch failed — check network"
      exit 65
    }
    # First track in the cache is the deterministic default.
    MUSIC_FILE="$(ls -1 "$FIXTURE_DIR/demo-music"/*.mp3 2>/dev/null | head -1)"
    [[ -z "$MUSIC_FILE" ]] && { log_err "no demo music tracks available"; exit 65; }
    log_info "[demo-mode] using $MUSIC_FILE"
  fi
fi

if [[ -z "$SHORT_ID" || -z "$MUSIC_FILE" ]]; then
  log_err "usage: $0 <short_id> <music_file> [keywords_csv]"
  log_err "       (or set MUSIC_VIDEO_DEMO_MODE=1 to use the bundled demo cache)"
  exit 64
fi
[[ -f "$MUSIC_FILE" ]] || { log_err "music file not found: $MUSIC_FILE"; exit 64; }
require_bin "$FFMPEG_BIN" "$FFPROBE_BIN" aubiotrack aubioonset jq curl
if [[ "$DEMO_MODE" != "1" ]]; then
  require_env PEXELS_API_KEY RECORDS_DIR
else
  require_env RECORDS_DIR
fi

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

# Demo-mode prepopulate: cycle through the demo-broll cache so each
# unique keyword maps deterministically to one cached clip.  The
# downstream code's `if [[ ! -f "$RAW" ]]` check then short-circuits
# the Pexels API entirely (no PEXELS_API_KEY needed).  Motif keyword
# (KEYWORDS[0]) still reuses the same clip across all its segments
# because cache_key is keyword-derived.
if [[ "$DEMO_MODE" == "1" ]]; then
  # Build a stable unique-keyword list (preserves first-appearance order).
  # bash 3.2 + `set -u` errors on "${arr[@]}" when arr is empty; the
  # `${arr[@]+...}` guard sidesteps the expansion entirely in that case.
  UNIQ_KW=()
  for ((j=0; j<SEG_TOTAL; j++)); do
    seen=0
    for u in ${UNIQ_KW[@]+"${UNIQ_KW[@]}"}; do
      [[ "$u" == "${SEG_KW[$j]}" ]] && { seen=1; break; }
    done
    [[ "$seen" == "0" ]] && UNIQ_KW+=("${SEG_KW[$j]}")
  done

  # Demo clips available, ordered.
  DEMO_CLIPS=()
  while IFS= read -r c; do
    DEMO_CLIPS+=("$c")
  done < <(ls -1 "$FIXTURE_DIR/demo-broll"/*.mp4 2>/dev/null)
  [[ "${#DEMO_CLIPS[@]}" -eq 0 ]] && { log_err "no demo-broll clips at $FIXTURE_DIR/demo-broll/"; exit 65; }

  log_info "  [demo-mode] mapping ${#UNIQ_KW[@]} keyword(s) → ${#DEMO_CLIPS[@]} demo clip(s)"
  for ((j=0; j<${#UNIQ_KW[@]}; j++)); do
    kw="${UNIQ_KW[$j]}"
    cache_key=$(echo "$kw" | tr ' ' '_')
    RAW="$CLIPS_DIR/raw-${cache_key}.mp4"
    SRC="${DEMO_CLIPS[$((j % ${#DEMO_CLIPS[@]}))]}"
    cp "$SRC" "$RAW"
    # Carry the sidecar too so attribution can be rolled up later.
    src_meta="${SRC%.mp4}.meta.json"
    [[ -f "$src_meta" ]] && cp "$src_meta" "${RAW%.mp4}.meta.json"
  done
fi

# Custom B-roll override: when MUSIC_VIDEO_BROLL_DIR is set, cycle its
# *.mp4 files across unique keywords the same way demo-mode does.
# Bypasses Pexels API + the demo-mode fetchers entirely.  Useful when
# the operator has a specific aesthetic (e.g., anime footage) that no
# stock library covers.  Sidecar JSONs (.meta.json next to each mp4)
# are honoured if present.
if [[ -z "${MUSIC_VIDEO_BROLL_DIR:-}" ]]; then
  CUSTOM_BROLL_DIR=""
else
  CUSTOM_BROLL_DIR="$MUSIC_VIDEO_BROLL_DIR"
fi
if [[ -n "$CUSTOM_BROLL_DIR" && "$DEMO_MODE" != "1" ]]; then
  if [[ ! -d "$CUSTOM_BROLL_DIR" ]]; then
    log_err "MUSIC_VIDEO_BROLL_DIR not found: $CUSTOM_BROLL_DIR"
    exit 65
  fi
  UNIQ_KW=()
  for ((j=0; j<SEG_TOTAL; j++)); do
    seen=0
    for u in ${UNIQ_KW[@]+"${UNIQ_KW[@]}"}; do
      [[ "$u" == "${SEG_KW[$j]}" ]] && { seen=1; break; }
    done
    [[ "$seen" == "0" ]] && UNIQ_KW+=("${SEG_KW[$j]}")
  done

  CUSTOM_CLIPS=()
  while IFS= read -r c; do
    CUSTOM_CLIPS+=("$c")
  done < <(ls -1 "$CUSTOM_BROLL_DIR"/*.mp4 2>/dev/null)
  [[ "${#CUSTOM_CLIPS[@]}" -eq 0 ]] && { log_err "no *.mp4 in $CUSTOM_BROLL_DIR"; exit 65; }

  log_info "  [custom-broll] mapping ${#UNIQ_KW[@]} keyword(s) → ${#CUSTOM_CLIPS[@]} custom clip(s) from $CUSTOM_BROLL_DIR"
  for ((j=0; j<${#UNIQ_KW[@]}; j++)); do
    kw="${UNIQ_KW[$j]}"
    cache_key=$(echo "$kw" | tr ' ' '_')
    RAW="$CLIPS_DIR/raw-${cache_key}.mp4"
    SRC="${CUSTOM_CLIPS[$((j % ${#CUSTOM_CLIPS[@]}))]}"
    cp "$SRC" "$RAW"
    src_meta="${SRC%.mp4}.meta.json"
    [[ -f "$src_meta" ]] && cp "$src_meta" "${RAW%.mp4}.meta.json"
  done
fi

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

# ───── 5. concat + final mix (with v6 vintage-lofi treatment) ─────
log_step "5/5  concat + final mix"
CONCAT="$MDIR/resources/concat-noaudio.mp4"
"$FFMPEG_BIN" -y -loglevel error -f concat -safe 0 -i "$TRIM_LIST" -c copy "$CONCAT"

# v6 visual treatment.  All three effects default ON; tune or disable
# per render via env vars.  When all three are disabled, the final stage
# falls back to a fast c:v-copy mux.
#
#   MUSIC_VIDEO_FILM_GRAIN_INTENSITY  default 8    (0 = disabled)
#   MUSIC_VIDEO_VIGNETTE_ANGLE        default PI/5 ('none'/'off'/empty = disabled)
#   MUSIC_VIDEO_ZOOM_PULSE_AMP        default 0.08 (0 = disabled)
#   MUSIC_VIDEO_ZOOM_PULSE_SIGMA      default 0.18 (bell width in seconds)
#
# The zoom-pulse rings the visual on each glitch onset — a subtle 0.6-s
# bell that visually re-asserts the music's "scratch" moment.  If a music
# track is melodic-only (no onsets detected), this falls through to no zoom.
GRAIN_INTENSITY="${MUSIC_VIDEO_FILM_GRAIN_INTENSITY:-8}"
# Vignette default PI/5; recognize 'none'/'off' explicit disable so the
# genre-aware wrapper (scripts/music-video-genre.sh) can suppress vignette
# for genres that forbid it (synthwave, techno).  Empty also disables,
# but uses the - operator (not :-) elsewhere to distinguish.
_vraw="${MUSIC_VIDEO_VIGNETTE_ANGLE-PI/5}"
case "$_vraw" in
  none|off|OFF|NONE) VIGNETTE_ANGLE="" ;;
  "")                VIGNETTE_ANGLE="" ;;
  *)                 VIGNETTE_ANGLE="$_vraw" ;;
esac
ZOOM_AMP="${MUSIC_VIDEO_ZOOM_PULSE_AMP:-0.08}"
ZOOM_SIGMA="${MUSIC_VIDEO_ZOOM_PULSE_SIGMA:-0.18}"

# Build the zoom-pulse expression by summing a Gaussian bell at every
# non-empty glitch onset.  In the no-glitch case, ZOOM_EXPR = "1".
ZOOM_EXPR="1"
if [[ "$ZOOM_AMP" != "0" && "$ZOOM_AMP" != "0.0" ]]; then
  zoom_bells=""
  for ((i=0; i<SEG_TOTAL; i++)); do
    g="${SEG_GLITCH[$i]}"
    if [[ -n "$g" ]]; then
      zoom_bells="${zoom_bells}+${ZOOM_AMP}*exp(-((t-${g})/${ZOOM_SIGMA})*((t-${g})/${ZOOM_SIGMA}))"
    fi
  done
  if [[ -n "$zoom_bells" ]]; then
    ZOOM_EXPR="1${zoom_bells}"
  fi
fi

OUT="$MDIR/outputs/short.mp4"

# Decide whether to apply any filters.  If everything is off, fast-path
# the original c:v copy.
ANY_FILTER=0
[[ "$GRAIN_INTENSITY" != "0" ]] && ANY_FILTER=1
[[ -n "$VIGNETTE_ANGLE" ]] && ANY_FILTER=1
[[ "$ZOOM_EXPR" != "1" ]] && ANY_FILTER=1

if (( ANY_FILTER == 0 )); then
  log_info "  (no v6 filters — fast-path c:v copy mux)"
  "$FFMPEG_BIN" -y -loglevel error \
    -i "$CONCAT" \
    -stream_loop -1 -i "$MUSIC_FILE" \
    -map "0:v" -map "1:a" \
    -c:v copy -c:a aac -b:a 192k \
    -t "$TARGET_DUR" \
    "$OUT"
else
  filter_parts=()
  [[ "$GRAIN_INTENSITY" != "0" ]] && filter_parts+=("noise=alls=${GRAIN_INTENSITY}:allf=t")
  [[ -n "$VIGNETTE_ANGLE" ]] && filter_parts+=("vignette=${VIGNETTE_ANGLE}")
  if [[ "$ZOOM_EXPR" != "1" ]]; then
    filter_parts+=("scale=w='1080*(${ZOOM_EXPR})':h='1920*(${ZOOM_EXPR})':eval=frame")
    filter_parts+=("crop=1080:1920")
  fi

  # Comma-join filter parts into a single chain
  IFS=','; FILTER_CHAIN="${filter_parts[*]}"; IFS=' '

  log_info "  v6 filters: grain=${GRAIN_INTENSITY} vignette=${VIGNETTE_ANGLE} zoom_pulses=$(( ${#filter_parts[@]} >= 3 ? 1 : 0 ))"
  "$FFMPEG_BIN" -y -loglevel error \
    -i "$CONCAT" \
    -stream_loop -1 -i "$MUSIC_FILE" \
    -filter_complex "[0:v]${FILTER_CHAIN}[outv]" \
    -map "[outv]" -map "1:a" \
    -c:v libx264 -preset medium -crf 22 -c:a aac -b:a 192k \
    -t "$TARGET_DUR" -r 30 \
    "$OUT"
fi

dur=$("$FFPROBE_BIN" -v error -show_entries format=duration -of default=nw=1:nk=1 "$OUT" | awk '{printf "%.1f", $1}')
size=$(du -h "$OUT" | awk '{print $1}')
log_ok "rendered: $OUT (${dur}s, $size)"

# Caption-verify still frame at mid-mission (re-uses faceless-short convention)
MID_T=$(awk -v d="$dur" 'BEGIN{printf "%.2f", d/2}')
"$FFMPEG_BIN" -y -loglevel error -ss "$MID_T" -i "$OUT" -frames:v 1 \
  "$MDIR/outputs/preview-frame.jpg" 2>/dev/null || true

# ───── attribution rollup ─────
# Collect every clip / track that contributed to this render with its
# CC license and credit string.  Demo mode is where this matters most
# (CC-BY attribution is required for publish) but it's harmless in
# Pexels mode too.
SOURCES_OUT="$MDIR/outputs/SOURCES.txt"
{
  echo "# Sources for $MISSION_ID"
  echo
  echo "## Music"
  if [[ "$DEMO_MODE" == "1" ]]; then
    music_meta="${MUSIC_FILE%.mp3}.meta.json"
    if [[ -f "$music_meta" ]]; then
      attr=$(jq -r '.attribution_string' "$music_meta" 2>/dev/null)
      lic=$(jq -r '.license' "$music_meta" 2>/dev/null)
      page=$(jq -r '.page_url' "$music_meta" 2>/dev/null)
      echo "- $attr — $lic — $page"
    else
      echo "- $(basename "$MUSIC_FILE") (operator-supplied; no sidecar)"
    fi
  else
    echo "- $(basename "$MUSIC_FILE") (operator-supplied)"
  fi
  echo
  echo "## B-roll"
  # One line per *unique* attribution+license pair.  Multiple
  # keywords often map to the same demo clip; SOURCES.txt should
  # carry one credit line per source, not per slot.
  for meta in "$CLIPS_DIR"/raw-*.meta.json; do
    [[ -f "$meta" ]] || continue
    attr=$(jq -r '.attribution_string' "$meta" 2>/dev/null)
    lic=$(jq -r '.license' "$meta" 2>/dev/null)
    [[ -n "$attr" && "$attr" != "null" ]] && echo "- $attr — $lic"
  done | sort -u
} > "$SOURCES_OUT"
log_info "  attribution rollup: $SOURCES_OUT"

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
