#!/usr/bin/env bash
# All-in-one music-video producer: detect genre → resolve preset →
# generate short → apply matching post-shader → optionally produce a
# Canvas 8s variant + kinetic typography overlay.  Zero arguments
# beyond the music file.
#
# Usage:
#   scripts/music-video-auto.sh <music_file>
#   scripts/music-video-auto.sh <music_file> [--with-canvas] [--with-typography "phrase1,phrase2,..."]
#   scripts/music-video-auto.sh <music_file> --short-id=<id>
#   scripts/music-video-auto.sh <music_file> --image=<path>   # forced for stillzoom genres
#
# Examples:
#   scripts/music-video-auto.sh assets/music/track3-arcade.mp3
#     → detects synthwave, fetches synthwave keyword pool, runs pipeline,
#       applies scanline shader.  Final mp4 under records/missions/...
#
#   scripts/music-video-auto.sh assets/music/track5-linen.mp3 --image=assets/stills/linen.jpg
#     → detects ambient, requires stillzoom (image), applies halation.
#
# Compose with --with-canvas / --with-typography for the full output bundle.
#
# Reads MUSIC_VIDEO_GENRE env var as override (skips detection).

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh" 2>/dev/null || true

DETECT_SH="$REPO_ROOT/scripts/music-video-genre-detect.sh"
GENRE_SH="$REPO_ROOT/scripts/music-video-genre.sh"
CANVAS_SH="$REPO_ROOT/scripts/music-video-canvas.sh"
TYPO_SH="$REPO_ROOT/scripts/music-video-typography.sh"

# Parse flags
WITH_CANVAS=0
WITH_TYPO_FLAG=0     # --with-typography given (use genre pool)
WITH_TYPO=""         # explicit phrases CSV (overrides pool)
STILL_IMG=""
SHORT_ID=""
ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --with-canvas) WITH_CANVAS=1; shift ;;
    --with-typography)
      WITH_TYPO_FLAG=1
      # Peek at next arg — if non-flag, treat as phrases CSV; else use pool
      if [[ "${2:-}" != "" && "${2:-}" != --* ]]; then
        WITH_TYPO="$2"; shift 2
      else
        shift
      fi
      ;;
    --with-typography=*) WITH_TYPO_FLAG=1; WITH_TYPO="${1#*=}"; shift ;;
    --image=*) STILL_IMG="${1#*=}"; shift ;;
    --short-id=*) SHORT_ID="${1#*=}"; shift ;;
    --help)
      sed -n '1,/^set -uo/p' "$0" | grep '^#' | sed 's/^# \?//'
      exit 0
      ;;
    -*) echo "❌ unknown flag: $1" >&2; exit 64 ;;
    *) ARGS+=("$1"); shift ;;
  esac
done

if [[ "${#ARGS[@]}" -eq 0 ]]; then
  echo "usage: $0 <music_file> [--with-canvas] [--with-typography \"...\"] [--image=PATH] [--short-id=ID]" >&2
  exit 64
fi

MUSIC="${ARGS[0]}"
[[ -f "$MUSIC" ]] || { echo "❌ music not found: $MUSIC" >&2; exit 64; }

# Generate short ID from filename if not supplied
if [[ -z "$SHORT_ID" ]]; then
  SHORT_ID=$(basename "$MUSIC" | sed 's/\.[^.]*$//' | tr 'A-Z ' 'a-z_' | tr -cd 'a-z0-9_-' | cut -c1-32)
  [[ -z "$SHORT_ID" ]] && SHORT_ID="auto"
fi

# 1. Detect genre
echo "═══════════════════════════════════════════════════════════════"
echo " music-video-auto: $(basename "$MUSIC")"
echo "═══════════════════════════════════════════════════════════════"
GENRE=$(bash "$DETECT_SH" "$MUSIC")
echo "→ detected genre: $GENRE"
echo "→ short_id: $SHORT_ID"
echo

# 2. Run genre wrapper
EXTRA_FLAGS=()
[[ -n "$STILL_IMG" ]] && EXTRA_FLAGS+=(--image="$STILL_IMG")

bash "$GENRE_SH" "${EXTRA_FLAGS[@]}" "$GENRE" "$SHORT_ID" "$MUSIC"
RC=$?
[[ $RC -ne 0 ]] && { echo "❌ genre wrapper exit $RC" >&2; exit $RC; }

# 3. Locate final mp4
LATEST=$(ls -1t "${RECORDS_DIR:-$REPO_ROOT/records}/missions/$(date +%Y-%m-%d)/music-video-${SHORT_ID}-"*/outputs/short*.mp4 2>/dev/null | head -1)
if [[ -z "$LATEST" ]]; then
  echo "⚠️  could not locate output mp4 — skipping --with-* steps" >&2
  exit 0
fi
echo
echo "═══ base mp4: $LATEST"

OUTPUT_DIR="$(dirname "$LATEST")"

# 4. Canvas variant (optional)
if [[ "$WITH_CANVAS" == 1 ]]; then
  CANVAS_OUT="$OUTPUT_DIR/canvas.mp4"
  echo "→ canvas 8s variant"
  bash "$CANVAS_SH" "$LATEST" "$CANVAS_OUT"
  echo "✓ canvas: $CANVAS_OUT"
fi

# 5. Typography overlay (optional)
if [[ "$WITH_TYPO_FLAG" == 1 ]]; then
  # If no explicit phrases, pull from genre phrase_pool
  if [[ -z "$WITH_TYPO" ]]; then
    PHRASES_YAML="$REPO_ROOT/skills/music-video/data/genre-presets.yaml"
    WITH_TYPO=$(yq -r ".genres.${GENRE}.phrase_pool | join(\",\")" "$PHRASES_YAML" 2>/dev/null)
    if [[ -z "$WITH_TYPO" || "$WITH_TYPO" == "null" ]]; then
      echo "⚠️  no phrase_pool for $GENRE — skipping typography" >&2
      WITH_TYPO=""
    else
      echo "→ no phrases supplied — using $GENRE preset pool"
      echo "  ($WITH_TYPO)"
    fi
  fi
  if [[ -n "$WITH_TYPO" ]]; then
    TYPO_OUT="${LATEST%.mp4}-typography.mp4"
    echo "→ typography overlay"
    bash "$TYPO_SH" "$LATEST" "$TYPO_OUT" "$WITH_TYPO"
    echo "✓ typography: $TYPO_OUT"
  fi
fi

echo
echo "═══════════════════════════════════════════════════════════════"
echo " Done.  Outputs in: $OUTPUT_DIR"
echo "═══════════════════════════════════════════════════════════════"
ls -la "$OUTPUT_DIR" | awk 'NR>1 {printf "  %-30s %s\n", $NF, $5}'
