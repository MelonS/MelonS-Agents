#!/usr/bin/env bash
# Pad video stream to match audio duration (clone last frame).
# Fixes the chronic stream-duration mismatch where beat detection
# produces fewer segments than the audio length needs.
#
# Usage: video-audio-pad.sh input.mp4 output.mp4
set -euo pipefail

IN="$1"
OUT="$2"

VDUR=$(ffprobe -v error -select_streams v -show_entries stream=duration -of csv=p=0 "$IN" | awk '{printf "%.2f", $1}')
ADUR=$(ffprobe -v error -select_streams a -show_entries stream=duration -of csv=p=0 "$IN" | awk '{printf "%.2f", $1}')
DIFF=$(echo "$ADUR - $VDUR" | bc -l)
DIFF_INT=$(printf '%.0f' "$DIFF")

if (( DIFF_INT <= 1 )); then
  echo "[OK] no padding needed (v=${VDUR}s a=${ADUR}s)"
  cp "$IN" "$OUT"
  exit 0
fi

PAD_SECONDS=$(printf '%.2f' "$DIFF")
echo "[PAD] v=${VDUR}s a=${ADUR}s → padding ${PAD_SECONDS}s of cloned last frame"

FFMPEG="${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}"
"$FFMPEG" -y -loglevel error -i "$IN" \
  -vf "tpad=stop_mode=clone:stop_duration=${PAD_SECONDS}" \
  -c:v libx264 -preset medium -crf 22 -pix_fmt yuv420p -c:a copy -shortest "$OUT"

NEW_VDUR=$(ffprobe -v error -select_streams v -show_entries stream=duration -of csv=p=0 "$OUT" | awk '{printf "%.2f", $1}')
NEW_ADUR=$(ffprobe -v error -select_streams a -show_entries stream=duration -of csv=p=0 "$OUT" | awk '{printf "%.2f", $1}')
echo "[DONE] $OUT (v=${NEW_VDUR}s a=${NEW_ADUR}s)"
