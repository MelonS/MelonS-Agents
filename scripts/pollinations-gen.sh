#!/usr/bin/env bash
# Pollinations.ai image generator → ffmpeg Ken Burns video
# Usage: pollinations-gen.sh "prompt text" output.mp4 [duration_sec=5]
set -euo pipefail

PROMPT="$1"
OUT_MP4="$2"
DUR="${3:-5}"
SEED="${SEED:-$RANDOM}"

WORK=$(mktemp -d /tmp/pollinations-XXXX)
STILL="$WORK/still.jpg"

# URL-encode prompt with python (safer than bash)
ENCODED=$(python3 -c "import sys, urllib.parse; print(urllib.parse.quote(sys.argv[1]))" "$PROMPT")
URL="https://image.pollinations.ai/prompt/${ENCODED}?width=1080&height=1920&seed=${SEED}&nologo=true&model=flux"

echo "[FETCH] seed=$SEED model=flux"
curl -sSL -o "$STILL" "$URL"
[[ ! -s "$STILL" ]] && { echo "[FAIL] no image returned"; exit 1; }

# Probe actual returned dimensions
DIM=$(ffprobe -v error -select_streams v -show_entries stream=width,height -of csv=p=0 "$STILL")
echo "[GOT] $DIM (target 1080x1920)"

FFMPEG_BIN="${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}"

# Ken Burns: 5-second zoom-in from 1.0x to 1.15x scale
# zoompan needs zoom step per frame: 0.15/(5s*30fps) = 0.001 per frame
FPS=30
FRAMES=$((DUR * FPS))
"$FFMPEG_BIN" -y -loglevel error \
  -loop 1 -i "$STILL" \
  -vf "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,zoompan=z='zoom+0.001':d=${FRAMES}:s=1080x1920:fps=${FPS},format=yuv420p" \
  -t "$DUR" -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p \
  -an "$OUT_MP4"

# Verify
RDIM=$(ffprobe -v error -select_streams v -show_entries stream=width,height -of csv=p=0 "$OUT_MP4")
RDUR=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT_MP4" | awk '{printf "%.1f", $1}')
echo "[DONE] $OUT_MP4 ${RDIM} ${RDUR}s"
rm -rf "$WORK"
