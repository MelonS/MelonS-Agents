#!/usr/bin/env bash
# Genre-aware music-video wrapper.
#
# Reads the genre preset from skills/music-video/data/genre-presets.yaml,
# exports the corresponding pipeline env vars, runs
# agents/missions/music-video/run.sh, then applies the matching post-shader.
#
# Solves the diagnosed shader-song mismatch problem
# (docs/research/2026-05-21-shader-song-mismatch-diagnosis.md) by routing
# each genre to its visually-appropriate treatment.
#
# Usage:
#   scripts/music-video-genre.sh <genre> <short_id> <music_file> [keywords_csv]
#
# Example:
#   scripts/music-video-genre.sh lofi_hiphop rain01 assets/music/rain.mp3 \
#     "rainy window, lo-fi cafe, vinyl, warm lights"
#
# To list available genres:
#   scripts/music-video-genre.sh --list
#
# Behavior:
#   - Looks up genre in genre-presets.yaml (supports aliases)
#   - Exports MUSIC_VIDEO_PHRASE_BEATS, _FILM_GRAIN_INTENSITY,
#     _VIGNETTE_ANGLE, _ZOOM_PULSE_AMP from preset
#   - Runs run.sh with those overrides
#   - If preset has cut_mode=stillzoom, BYPASSES run.sh and calls
#     music-video-stillzoom.sh instead (needs --image flag — see below)
#   - Applies preset's shader as post-pass via music-video-shaders.sh
#
# Stillzoom requires a still image:
#   scripts/music-video-genre.sh --image=path/to/still.jpg ambient \
#     ambient01 assets/music/linen.mp3
#
# Backwards compatible: existing run.sh entry points work unchanged.

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh" 2>/dev/null || true

PRESETS="$REPO_ROOT/skills/music-video/data/genre-presets.yaml"
RUN_SH="$REPO_ROOT/agents/missions/music-video/run.sh"
SHADERS_SH="$REPO_ROOT/scripts/music-video-shaders.sh"
STILLZOOM_SH="$REPO_ROOT/scripts/music-video-stillzoom.sh"

if [[ ! -f "$PRESETS" ]]; then
  echo "❌ missing presets: $PRESETS" >&2
  exit 1
fi
command -v yq >/dev/null 2>&1 || {
  echo "❌ yq not installed — brew install yq" >&2
  exit 1
}

# Parse args
STILL_IMG=""
while [[ "${1:-}" == --* ]]; do
  case "$1" in
    --image=*) STILL_IMG="${1#--image=}"; shift ;;
    --list)
      echo "Available genres (from $PRESETS):"
      yq -r '.genres | keys | .[]' "$PRESETS"
      echo
      echo "Each genre may also have aliases (try --list-aliases)"
      exit 0
      ;;
    --list-aliases)
      yq -r '.genres | to_entries | .[] | "\(.key): \(.value.aliases // [] | join(", "))"' "$PRESETS"
      exit 0
      ;;
    --help)
      sed -n '1,/^set -uo/p' "$0" | grep '^#' | sed 's/^# //; s/^#//'
      exit 0
      ;;
    *) echo "❌ unknown flag: $1" >&2; exit 64 ;;
  esac
done

GENRE="${1:-}"
SHORT_ID="${2:-}"
MUSIC="${3:-}"
KEYWORDS="${4:-}"

if [[ -z "$GENRE" || -z "$SHORT_ID" || -z "$MUSIC" ]]; then
  echo "usage: $0 [--image=IMG] <genre> <short_id> <music_file> [keywords_csv]" >&2
  echo "       $0 --list" >&2
  exit 64
fi

# Resolve aliases — search both top-level keys and aliases array.
# mikefarah/yq v4: contains() does *substring* matching on strings which
# falsely matches "jazz" against ["doom_jazz"].  Use explicit equality
# via map+any.
RESOLVED=$(GENRE_NAME="$GENRE" yq -r '
  .genres | to_entries | .[] |
  select(
    .key == env(GENRE_NAME) or
    ((.value.aliases // []) | map(. == env(GENRE_NAME)) | any)
  ) |
  .key
' "$PRESETS" | head -1)

if [[ -z "$RESOLVED" ]]; then
  echo "❌ unknown genre: $GENRE" >&2
  echo "   Available:" >&2
  yq -r '.genres | keys | .[]' "$PRESETS" | sed 's/^/     /' >&2
  exit 64
fi

if [[ "$RESOLVED" != "$GENRE" ]]; then
  echo "→ resolved alias '$GENRE' → '$RESOLVED'"
fi

# Extract preset fields
PHRASE_BEATS=$(yq -r ".genres.${RESOLVED}.phrase_beats" "$PRESETS")
GRAIN=$(yq -r ".genres.${RESOLVED}.grain_intensity" "$PRESETS")
VIGNETTE=$(yq -r ".genres.${RESOLVED}.vignette_angle" "$PRESETS")
ZOOM_AMP=$(yq -r ".genres.${RESOLVED}.zoom_pulse_amp" "$PRESETS")
SHADER_ACTIVE_RATIO=$(yq -r ".genres.${RESOLVED}.shader_active_ratio // 1.0" "$PRESETS")
SHADER=$(yq -r ".genres.${RESOLVED}.shader" "$PRESETS")
CUT_MODE=$(yq -r ".genres.${RESOLVED}.cut_mode" "$PRESETS")
LUT_DIR=$(yq -r ".genres.${RESOLVED}.lut_direction" "$PRESETS")

# Vignette "off" is passed through to run.sh which now recognizes
# 'none'/'off' as explicit disable (2026-05-21 run.sh patch).

echo "╔══════════════════════════════════════════════════════"
echo "║ Genre preset: $RESOLVED"
echo "║   cut_mode      = $CUT_MODE"
echo "║   phrase_beats  = $PHRASE_BEATS"
echo "║   grain         = $GRAIN"
echo "║   vignette      = ${VIGNETTE:-(off)}"
echo "║   zoom_pulse    = $ZOOM_AMP"
echo "║   shader        = $SHADER"
echo "║   LUT direction = $LUT_DIR"
echo "╚══════════════════════════════════════════════════════"

# Stillzoom branch — bypass run.sh, use stillzoom.sh instead
if [[ "$CUT_MODE" == "stillzoom" ]]; then
  if [[ -z "$STILL_IMG" ]]; then
    echo "❌ genre $RESOLVED requires stillzoom mode → pass --image=PATH" >&2
    echo "   Suggestion: fetch a single Pexels portrait photo matching the mood," >&2
    echo "   or use any 9:16 image you have." >&2
    exit 64
  fi
  TMPDIR=$(mktemp -d -t musicvideo-stillzoom-XXXXXX)
  RAW="$TMPDIR/raw.mp4"
  FINAL_DIR="${RECORDS_DIR:-$REPO_ROOT/records}/missions/$(date +%Y-%m-%d)/music-video-${SHORT_ID}-$(date +%H%M%S)/outputs"
  mkdir -p "$FINAL_DIR"
  FINAL="$FINAL_DIR/short.mp4"

  bash "$STILLZOOM_SH" "$STILL_IMG" "$MUSIC" "$RAW" 60

  if [[ "$SHADER" != "none" && "$SHADER" != "null" ]]; then
    echo "→ post-shader: $SHADER"
    bash "$SHADERS_SH" "$SHADER" "$RAW" "$FINAL"
    rm -f "$RAW"
  else
    mv "$RAW" "$FINAL"
  fi
  rmdir "$TMPDIR" 2>/dev/null || true

  echo "✓ stillzoom render: $FINAL"
  exit 0
fi

# Normal pipeline — export env overrides, run.sh, then shader
export MUSIC_VIDEO_PHRASE_BEATS="$PHRASE_BEATS"
export MUSIC_VIDEO_FILM_GRAIN_INTENSITY="$GRAIN"
export MUSIC_VIDEO_VIGNETTE_ANGLE="$VIGNETTE"
export MUSIC_VIDEO_ZOOM_PULSE_AMP="$ZOOM_AMP"

# Resolve lang_anchor — controls ethnicity/language match in B-roll +
# AI-still fetches (quality-bar #5/#6, 2026-05-22).  Values:
#   ko       Korean vocal — KR people, Seoul/KR contexts preferred
#   en       English vocal — Western contexts, no CJK signage
#   mixed    citypop family — both ko/jp/en contexts acceptable
#   neutral  instrumental — no language constraint
LANG_ANCHOR=$(yq -r ".genres.${RESOLVED}.lang_anchor // \"neutral\"" "$PRESETS")
export MUSIC_VIDEO_LANG_ANCHOR="$LANG_ANCHOR"
[[ "$LANG_ANCHOR" != "neutral" ]] && echo "→ lang_anchor: $LANG_ANCHOR"

# Use operator-supplied keywords or fall back to genre's keyword_pool
if [[ -z "$KEYWORDS" ]]; then
  KEYWORDS=$(yq -r ".genres.${RESOLVED}.keyword_pool | join(\",\")" "$PRESETS" 2>/dev/null)
  if [[ -n "$KEYWORDS" && "$KEYWORDS" != "null" ]]; then
    echo "→ no keywords supplied — using $RESOLVED preset pool"
    echo "  ($KEYWORDS)"
  else
    KEYWORDS=""
  fi
fi

echo "→ running pipeline with preset env vars"
if [[ -n "$KEYWORDS" ]]; then
  bash "$RUN_SH" "$SHORT_ID" "$MUSIC" "$KEYWORDS"
else
  bash "$RUN_SH" "$SHORT_ID" "$MUSIC"
fi
RUN_RC=$?
if [[ $RUN_RC -ne 0 ]]; then
  echo "❌ run.sh exit $RUN_RC" >&2
  exit $RUN_RC
fi

# Locate the freshly-produced short.mp4
LATEST=$(ls -1t "${RECORDS_DIR:-$REPO_ROOT/records}/missions/$(date +%Y-%m-%d)/music-video-${SHORT_ID}-"*/outputs/short.mp4 2>/dev/null | head -1)
if [[ -z "$LATEST" ]]; then
  echo "⚠️  could not locate run.sh output — skipping post-shader" >&2
  exit 0
fi

# Apply post-shader if not "none"
if [[ "$SHADER" != "none" && "$SHADER" != "null" ]]; then
  SHADED="${LATEST%.mp4}-${SHADER}.mp4"
  echo "→ post-shader: $SHADER (active_ratio=$SHADER_ACTIVE_RATIO)"
  # Quality-bar #2 (2026-05-22): pass the active-ratio to the shaders
  # script so non-pulse effects can self-gate their visibility window
  # via ffmpeg enable= expressions.
  #
  # C.1 Phase 3 (2026-05-22): also pass the beats file location so
  # `onsets` and `beats` gate modes can construct per-event gaussian
  # opacity envelopes.  Beats file is at <mission>/resources/beats-real.txt
  # alongside <mission>/resources/onsets.txt — both pre-computed by run.sh.
  MISSION_DIR="$(dirname "$(dirname "$LATEST")")"
  if [[ -f "$MISSION_DIR/resources/beats-real.txt" ]]; then
    export MUSIC_VIDEO_SHADER_BEATS="$MISSION_DIR/resources/beats-real.txt"
  fi
  if [[ -f "$MISSION_DIR/resources/onsets.txt" ]]; then
    export MUSIC_VIDEO_SHADER_ONSETS="$MISSION_DIR/resources/onsets.txt"
  fi
  MUSIC_VIDEO_SHADER_RATIO="$SHADER_ACTIVE_RATIO" bash "$SHADERS_SH" "$SHADER" "$LATEST" "$SHADED"
  echo "✓ final: $SHADED"
else
  echo "✓ final: $LATEST  (no post-shader for this preset)"
fi
