#!/usr/bin/env bash
# Build a single composite JPEG showing the first frame of every short
# in a shorts-batch mission. Useful for at-a-glance review.
# Usage: scripts/contact-sheet.sh <mission-dir>
set -u
MDIR="${1:-}"
[[ -z "$MDIR" || ! -d "$MDIR/outputs" ]] && { echo "usage: $0 <records/missions/.../shorts-batch-*>"; exit 64; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
: "${FFMPEG_BIN:=/Users/melons/.local/opt/ffmpeg-static/ffmpeg}"

# Extract one frame per short
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
i=0
for mp4 in "$MDIR"/outputs/short-*.mp4; do
  [[ -f "$mp4" ]] || continue
  i=$((i+1))
  "$FFMPEG_BIN" -y -loglevel error -ss 2 -i "$mp4" -frames:v 1 -vf scale=540:960 "$TMP/$(printf '%02d' $i).jpg"
done
[[ $i -eq 0 ]] && { echo "no shorts found"; exit 1; }

# Tile horizontally (up to 4 per row)
OUT="$MDIR/outputs/contact-sheet.jpg"
"$FFMPEG_BIN" -y -loglevel error -pattern_type glob -framerate 1 -i "$TMP/*.jpg" \
  -vf "tile=${i}x1" -frames:v 1 -q:v 4 "$OUT"
echo "wrote $OUT"
