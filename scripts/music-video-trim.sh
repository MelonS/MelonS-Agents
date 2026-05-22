#!/usr/bin/env bash
# music-video-trim.sh — trim a long music-video to platform-specific
# length without re-encoding.  Uses stream-copy for speed (~1s wall
# for any source size) when start=0; falls back to a fast re-encode
# only when --start>0 forces non-keyframe seek.
#
# Platform presets:
#   --short      60 seconds  (TikTok / Reels / YT Shorts compact)
#   --shorts-max 180 seconds (YT Shorts maximum as of 2026)
#   --duration=N arbitrary
#
# Usage:
#   scripts/music-video-trim.sh <src.mp4> [--short|--shorts-max|--duration=N] [--start=N] [out]
#
# Default output: <src-basename>-trim.<DURATION>s.mp4 next to the src.

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh" 2>/dev/null || true

SRC=""
OUT=""
DUR=""
START="0"

for arg in "$@"; do
  case "$arg" in
    --short)        DUR=60 ;;
    --shorts-max)   DUR=180 ;;
    --duration=*)   DUR="${arg#*=}" ;;
    --start=*)      START="${arg#*=}" ;;
    -h|--help)
      sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    -*) echo "unknown flag: $arg" >&2; exit 64 ;;
    *)  if [[ -z "$SRC" ]]; then SRC="$arg"; else OUT="$arg"; fi ;;
  esac
done

if [[ -z "$SRC" || ! -f "$SRC" ]]; then
  echo "usage: $0 <src.mp4> [--short|--shorts-max|--duration=N] [--start=N] [out]" >&2
  exit 64
fi
if [[ -z "$DUR" ]]; then
  echo "specify length: --short, --shorts-max, or --duration=N" >&2
  exit 64
fi

# Resolve output name.
if [[ -z "$OUT" ]]; then
  OUT="${SRC%.mp4}-trim.${DUR}s.mp4"
fi

# ffprobe duration check — refuse to trim if source is already short.
SRC_DUR=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error \
  -show_entries format=duration -of csv=p=0 "$SRC" 2>/dev/null | awk '{printf "%.0f", $1}')
if [[ "$SRC_DUR" -le "$DUR" ]]; then
  echo "source is ${SRC_DUR}s; ≤ requested ${DUR}s — copying unchanged" >&2
  cp "$SRC" "$OUT"
  echo "[trim] copied: $OUT"
  exit 0
fi

# Choose codec strategy.  When start=0 we can stream-copy (fastest).
# Otherwise must re-encode because non-keyframe seek would corrupt.
if [[ "$START" == "0" ]]; then
  echo "[trim] stream-copy: ${SRC_DUR}s → ${DUR}s (start=0)"
  "${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}" -y -loglevel error \
    -i "$SRC" -t "$DUR" \
    -c:v copy -c:a copy \
    "$OUT"
else
  echo "[trim] fast re-encode: ${SRC_DUR}s → ${DUR}s (start=${START}s)"
  "${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}" -y -loglevel error \
    -ss "$START" -i "$SRC" -t "$DUR" \
    -c:v libx264 -preset fast -crf 22 -c:a aac -b:a 192k \
    "$OUT"
fi

if [[ ! -s "$OUT" ]]; then
  echo "trim failed" >&2
  exit 2
fi

out_dur=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error \
  -show_entries format=duration -of csv=p=0 "$OUT" 2>/dev/null | awk '{printf "%.1f", $1}')
size=$(du -h "$OUT" 2>/dev/null | awk '{print $1}')
echo "[trim] wrote $OUT (${out_dur}s, ${size})"
