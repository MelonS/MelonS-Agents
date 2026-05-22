#!/usr/bin/env bash
# music-video-thumbnail.sh — extract a representative still frame
# from a rendered music-video mp4 for YT/TikTok/Reels upload
# metadata.
#
# Strategy: pick a frame from inside the phrase_climax / midpoint
# window (where the shader is most active + the B-roll is typically
# at its visual peak).  Default: t = 50% of duration.  Operator can
# override via --at=N (seconds) or --at=N% (percent of duration).
#
# Output is a single JPG sized to 1080×1920 (9:16 portrait), suitable
# for YT thumbnail upload (max 2 MB).
#
# Usage:
#   scripts/music-video-thumbnail.sh <video.mp4> [<out.jpg>]
#   scripts/music-video-thumbnail.sh <video.mp4> --at=20
#   scripts/music-video-thumbnail.sh <video.mp4> --at=45%
#
# Default <out.jpg>: <video-basename>-thumb.jpg next to the source.

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh" 2>/dev/null || true

SRC=""
OUT=""
AT=""
for arg in "$@"; do
  case "$arg" in
    --at=*) AT="${arg#*=}" ;;
    -h|--help)
      sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    -*) echo "unknown flag: $arg" >&2; exit 64 ;;
    *)  if [[ -z "$SRC" ]]; then SRC="$arg"; else OUT="$arg"; fi ;;
  esac
done

if [[ -z "$SRC" || ! -f "$SRC" ]]; then
  echo "usage: $0 <video.mp4> [out.jpg] [--at=SECONDS|PERCENT%]" >&2
  exit 64
fi

# Default output: alongside source.
if [[ -z "$OUT" ]]; then
  OUT="${SRC%.mp4}-thumb.jpg"
fi

# Resolve frame time.
DUR=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error \
  -show_entries format=duration -of csv=p=0 "$SRC" 2>/dev/null \
  | awk '{printf "%.3f", $1}')
if [[ -z "$DUR" || "$DUR" == "0.000" ]]; then
  echo "ffprobe couldn't read duration of $SRC" >&2
  exit 2
fi

if [[ -z "$AT" ]]; then
  # default: 50% of duration
  TS=$(awk -v d="$DUR" 'BEGIN{printf "%.3f", d*0.5}')
  CHOSEN_DESC="midpoint (50% of ${DUR}s)"
elif [[ "$AT" == *% ]]; then
  PCT="${AT%\%}"
  TS=$(awk -v d="$DUR" -v p="$PCT" 'BEGIN{printf "%.3f", d*(p/100)}')
  CHOSEN_DESC="${PCT}% of ${DUR}s"
else
  TS="$AT"
  CHOSEN_DESC="t=${AT}s of ${DUR}s"
fi

echo "[thumb] extracting frame at $CHOSEN_DESC"

# Read at TS, scale to 1080×1920 (9:16) with letterbox/pillarbox if
# aspect-ratio mismatch.  -q:v 2 = high-quality JPEG.  Most music-
# video renders are already 1080×1920 so the scale is a no-op.
"${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}" -y -loglevel error \
  -ss "$TS" -i "$SRC" -frames:v 1 \
  -vf "scale=1080:1920:force_original_aspect_ratio=decrease,pad=1080:1920:(ow-iw)/2:(oh-ih)/2:color=black" \
  -q:v 2 "$OUT"

if [[ ! -s "$OUT" ]]; then
  echo "thumbnail extraction failed" >&2
  exit 2
fi

size=$(du -h "$OUT" 2>/dev/null | awk '{print $1}')
echo "[thumb] wrote $OUT (${size})"
