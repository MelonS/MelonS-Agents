#!/usr/bin/env bash
# Generate a 60s stillzoom music-short — single image + slow Ken-Burns zoom.
#
# Covers the genre gap that the cut-based music-video pipeline serves WORST:
# ambient / dark jazz / drone / classical / shoegaze / dreamcore.  For these
# genres, ANY cut + ANY drum-onset zoom-pulse reads as a "띠용" violation of
# the music's contract (see docs/research/2026-05-21-shader-song-mismatch-
# diagnosis.md).
#
# Usage:
#   scripts/music-video-stillzoom.sh <image> <music> <output.mp4> [duration_s]
#
# Defaults:
#   duration_s    60
#   zoom rate     1.0 → 1.18 over duration (slow, contemplative)
#   resolution    1080×1920 (9:16 vertical)
#
# Output is a fully-formed mp4 ready to feed into scripts/music-video-shaders.sh
# for an additional post-shader pass (typically `halation` for ambient/jazz).

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
# shellcheck disable=SC1091
source "$PWD/agents/lib/env.sh" 2>/dev/null || true

IMG="${1:-}"
MUSIC="${2:-}"
DST="${3:-}"
DUR="${4:-60}"

if [[ -z "$IMG" || -z "$MUSIC" || -z "$DST" ]]; then
  echo "usage: $0 <image> <music> <output.mp4> [duration_s=60]" >&2
  exit 64
fi
[[ -f "$IMG" ]]   || { echo "❌ image not found: $IMG" >&2; exit 64; }
[[ -f "$MUSIC" ]] || { echo "❌ music not found: $MUSIC" >&2; exit 64; }
FFMPEG="${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}"
[[ -x "$FFMPEG" ]] || { echo "❌ FFMPEG_BIN not executable" >&2; exit 1; }

# zoompan needs total frames (d=) — at 30 fps that's 30 × DUR.
FRAMES=$(( DUR * 30 ))

# Zoom curve: 1.0 → 1.18 linear over the full duration.  Slow enough that
# the eye can rest; fast enough that something is always happening.
# Pan: very gentle right-drift (x = (iw-iw/zoom)/2 + small offset).
ZOOM_EXPR="1+0.18*on/${FRAMES}"

# Pre-scale image to landscape-fit-9:16 frame so zoompan has room to crop.
# Use 2160x3840 (2x output) so the zoom in to 1.18× still oversamples.
"$FFMPEG" -y -loglevel warning -stats \
  -loop 1 -i "$IMG" \
  -stream_loop -1 -i "$MUSIC" \
  -filter_complex "
    [0:v]scale=2160:3840:force_original_aspect_ratio=increase,
         crop=2160:3840,
         zoompan=z='${ZOOM_EXPR}':d=${FRAMES}:s=1080x1920:fps=30,
         format=yuv420p,
         setsar=1[vout]
  " \
  -map "[vout]" -map "1:a" \
  -c:v libx264 -preset medium -crf 22 \
  -c:a aac -b:a 192k \
  -t "$DUR" -r 30 \
  "$DST"

dur=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error -show_entries format=duration -of csv=p=0 "$DST" 2>/dev/null | awk '{printf "%.1f", $1}')
size=$(du -h "$DST" 2>/dev/null | awk '{print $1}')
echo "✓ stillzoom: $DST (${dur}s, ${size})"
