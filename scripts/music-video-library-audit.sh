#!/usr/bin/env bash
# Audit the operator's music library — list every mp3 in assets/music/
# with its detected genre, resolved preset shader, duration, and
# whether a render already exists in outputs/demos/ or outputs/publish/.
#
# Usage:
#   scripts/music-video-library-audit.sh
#
# Output: table to stdout suitable for operator triage.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
# shellcheck disable=SC1091
source agents/lib/env.sh 2>/dev/null || true

DETECT=scripts/music-video-genre-detect.sh
PRESETS=skills/music-video/data/genre-presets.yaml
MUSIC_DIR=assets/music
DEMOS=outputs/demos
PUBLISH=outputs/publish

printf "%-46s %-14s %-16s %6s %s\n" "FILE" "GENRE" "SHADER" "DUR" "RENDERED?"
printf "%s\n" "$(printf '%.0s─' {1..120})"

for f in "$MUSIC_DIR"/*.mp3; do
  [[ -f "$f" ]] || continue
  name=$(basename "$f")
  short=$(basename "$f" .mp3)

  genre=$(bash "$DETECT" "$f" 2>/dev/null || echo "?")
  shader=$(yq -r ".genres.${genre}.shader // \"?\"" "$PRESETS" 2>/dev/null)
  dur=$("${FFPROBE_BIN:-ffprobe}" -v error -show_entries format=duration -of csv=p=0 "$f" 2>/dev/null | awk '{printf "%.0fs", $1}')

  # Find any existing render
  rendered=""
  if compgen -G "$DEMOS/*/*${short}*.mp4" > /dev/null 2>&1; then rendered="demos"; fi
  if compgen -G "$PUBLISH/*/*${short}*.mp4" > /dev/null 2>&1; then rendered="${rendered:+$rendered+}publish"; fi
  [[ -z "$rendered" ]] && rendered="—"

  printf "%-46s %-14s %-16s %6s %s\n" "$name" "$genre" "$shader" "$dur" "$rendered"
done

echo ""
echo "Total mp3s: $(ls "$MUSIC_DIR"/*.mp3 2>/dev/null | wc -l | tr -d ' ')"
echo "Total disk: $(du -sh "$MUSIC_DIR" | awk '{print $1}')"
