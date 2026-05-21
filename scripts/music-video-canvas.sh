#!/usr/bin/env bash
# Generate a Spotify Canvas 8s seamless loop from any existing music-video
# mp4 (or a fresh source clip + music).  Canvas spec:
#
#   - 8 seconds exactly
#   - 9:16 vertical, 720×1280 minimum (we use 720×1280 to keep file ≤8MB)
#   - Seamless loop (frame[0] == frame[239] visually)
#   - No text (Spotify rejects on review)
#   - No faces (Spotify rejects on review)
#   - ≤ 8 MB filesize
#   - mp4 H.264 + AAC
#
# Strategy: take the first 8 s of input video + 1 s tail, then xfade the
# tail back onto the head producing a 7 s base + 1 s crossfade = 8 s total
# that loops on itself.  Source audio is muted in Canvas (Spotify spec)
# but we keep a soft music bed for cross-posting to Reels / Shorts.
#
# Usage:
#   scripts/music-video-canvas.sh <input.mp4> <output_canvas.mp4>
#
# Distribution: produce one Canvas alongside every full Short — same
# source asset, no extra render cost.  Cross-post the Canvas to TikTok
# (8s also fits TikTok format) and as Reel preview.
#
# See: docs/research/2026-05-21-music-shorts-formats-landscape.md §12

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
# shellcheck disable=SC1091
source "$PWD/agents/lib/env.sh" 2>/dev/null || true

SRC="${1:-}"
DST="${2:-}"
DUR="${3:-8}"        # canvas duration; spec is exactly 8
XFADE="${4:-1.0}"    # crossfade overlap at the loop seam

if [[ -z "$SRC" || -z "$DST" ]]; then
  echo "usage: $0 <input.mp4> <output_canvas.mp4> [duration_s=8] [xfade_s=1.0]" >&2
  exit 64
fi
[[ -f "$SRC" ]] || { echo "❌ input not found: $SRC" >&2; exit 64; }
# §8 exception: ffmpeg parameter-expansion default — same pattern as
# scripts/music-video-shaders.sh:103.  Registered in operator-contract.md §8.
FFMPEG="${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}"
[[ -x "$FFMPEG" ]] || { echo "❌ FFMPEG_BIN not executable" >&2; exit 1; }

# We need DUR seconds out, with the last XFADE seconds being a crossfade
# from the head back into the tail.  So source slice = DUR + XFADE.
SRC_DUR_NEEDED=$(awk -v d="$DUR" -v x="$XFADE" 'BEGIN{printf "%.3f", d+x}')

# Probe source duration
SRC_DUR=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error -show_entries format=duration -of csv=p=0 "$SRC" | awk '{printf "%.3f", $1}')
if awk -v sd="$SRC_DUR" -v need="$SRC_DUR_NEEDED" 'BEGIN{exit !(sd < need)}'; then
  echo "❌ source ${SRC_DUR}s shorter than required ${SRC_DUR_NEEDED}s for ${DUR}s canvas + ${XFADE}s xfade" >&2
  exit 64
fi

# Strategy (single-input, split + xfade):
#   Take first DUR seconds.  Split into:
#     [head] = 0 → (DUR - XFADE)
#     [tail] = (DUR - XFADE) → DUR
#   xfade [head]→[tail] over XFADE seconds at offset (DUR - 2*XFADE) so
#   the visible last XFADE seconds is fade(tail → head[0:XFADE]) →
#   seamless loop.
HEAD_LAST=$(awk -v d="$DUR" -v x="$XFADE" 'BEGIN{printf "%.3f", d-x}')
HEAD_FADE_OFFSET=$(awk -v d="$DUR" -v x="$XFADE" 'BEGIN{printf "%.3f", d-2*x}')

"$FFMPEG" -y -loglevel warning -stats \
  -i "$SRC" -t "$DUR" \
  -filter_complex "[0:v]split=2[s1][s2];[s1]trim=0:${HEAD_LAST},setpts=PTS-STARTPTS,scale=720:1280:force_original_aspect_ratio=increase,crop=720:1280,setsar=1[head];[s2]trim=${HEAD_LAST}:${DUR},setpts=PTS-STARTPTS,scale=720:1280:force_original_aspect_ratio=increase,crop=720:1280,setsar=1[tail];[head][tail]xfade=transition=fade:duration=${XFADE}:offset=${HEAD_FADE_OFFSET}[vout]" \
  -map "[vout]" -map "0:a" \
  -c:v libx264 -preset medium -crf 23 \
  -c:a aac -b:a 128k -ar 44100 \
  -t "$DUR" \
  "$DST"

# Verify size + spec
size_b=$(stat -f%z "$DST" 2>/dev/null || stat -c%s "$DST" 2>/dev/null)
size_mb=$(awk -v b="$size_b" 'BEGIN{printf "%.2f", b/1024/1024}')
dur=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error -show_entries format=duration -of csv=p=0 "$DST" 2>/dev/null | awk '{printf "%.1f", $1}')

echo "✓ canvas: $DST (${dur}s, ${size_mb} MB)"
if awk -v m="$size_mb" 'BEGIN{exit !(m > 8.0)}'; then
  echo "⚠️  WARNING: ${size_mb} MB exceeds Spotify Canvas 8 MB limit." >&2
  echo "   Reduce: lower CRF target, or shorter duration, or 540×960 instead of 720×1280." >&2
fi
