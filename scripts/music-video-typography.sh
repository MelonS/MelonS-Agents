#!/usr/bin/env bash
# Overlay kinetic phrase typography on an existing music-video mp4.
#
# Solves the muted-autoplay problem identified in the 2026 formats
# landscape research: ~half of Shorts first-impressions are silent, so
# a music-primary video needs SOMETHING that works on mute.  Kinetic
# typography (4-6 mood phrases per track, fading in/out on phrase
# boundaries) gives mute viewers a hook without competing with vocals
# (instrumental tracks have no vocals to conflict).
#
# Usage:
#   scripts/music-video-typography.sh <input.mp4> <output.mp4> <phrases_csv>
#
# Examples:
#   scripts/music-video-typography.sh in.mp4 out.mp4 "rainy window,3 AM in Seoul,vinyl crackle,the last train"
#   scripts/music-video-typography.sh in.mp4 out.mp4 @phrases.txt   # @file = one phrase per line
#
# Defaults:
#   Phrase position    bottom-center (y = h*0.78)
#   Font               SF Mono Regular if installed; falls back to fc-match default
#   Font size          54 (≈ 4.2% of 1280px height)
#   Color              white with 0.85 alpha; soft black stroke for legibility
#   Fade               0.5s in, 0.5s out
#   Per-phrase hold    auto = (duration - 1.0) / N - 0.5 to space evenly
#
# Phrase typing guide (per research §kinetic typography):
#   - 3-5 words max per phrase
#   - Sensory / mood / place ("rain on glass", "3 AM in Seoul")
#   - NOT song titles, NOT artist names, NOT genre labels
#   - Instrumental-only — vocals create semantic conflict
#
# Environment overrides:
#   TYPO_FONT          font file path (default: SF Mono Regular)
#   TYPO_SIZE          font size px (default: 54)
#   TYPO_COLOR         color (default: white@0.85)
#   TYPO_Y_RATIO       vertical position 0-1 (default: 0.78)

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
# shellcheck disable=SC1091
source "$PWD/agents/lib/env.sh" 2>/dev/null || true

SRC="${1:-}"
DST="${2:-}"
PHRASES_RAW="${3:-}"

if [[ -z "$SRC" || -z "$DST" || -z "$PHRASES_RAW" ]]; then
  echo "usage: $0 <input.mp4> <output.mp4> <\"phrase1,phrase2,...\" | @phrases.txt>" >&2
  exit 64
fi
[[ -f "$SRC" ]] || { echo "❌ input not found: $SRC" >&2; exit 64; }
FFMPEG="${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}"
FFPROBE="${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}"

# Parse phrases — support @file or CSV
PHRASES=()
if [[ "$PHRASES_RAW" == @* ]]; then
  PHRASE_FILE="${PHRASES_RAW:1}"
  [[ -f "$PHRASE_FILE" ]] || { echo "❌ phrases file not found: $PHRASE_FILE" >&2; exit 64; }
  while IFS= read -r line; do
    [[ -n "$line" ]] && PHRASES+=("$line")
  done < "$PHRASE_FILE"
else
  IFS=',' read -r -a PHRASES <<< "$PHRASES_RAW"
fi
N="${#PHRASES[@]}"
(( N > 0 )) || { echo "❌ no phrases parsed" >&2; exit 64; }
(( N <= 12 )) || { echo "⚠️  $N phrases — recommend ≤ 8 for legibility" >&2; }

DUR=$("$FFPROBE" -v error -show_entries format=duration -of csv=p=0 "$SRC" | awk '{printf "%.3f", $1}')
echo "→ source duration $DUR s, $N phrases"

# Phrase scheduling — evenly space.  Each phrase displays for (DUR/N) seconds
# minus a 1.0s gap.  Fade in/out 0.5s each.
SLOT=$(awk -v d="$DUR" -v n="$N" 'BEGIN{printf "%.3f", d/n}')
HOLD=$(awk -v s="$SLOT" 'BEGIN{printf "%.3f", s - 1.0}')   # 1.0s gap between phrases
FADE_IN="0.5"
FADE_OUT="0.5"

# Font selection
FONT="${TYPO_FONT:-}"
if [[ -z "$FONT" ]]; then
  if [[ -f "$HOME/Library/Fonts/SF-Mono-Regular.otf" ]]; then
    FONT="$HOME/Library/Fonts/SF-Mono-Regular.otf"
  elif [[ -f "/System/Library/Fonts/Menlo.ttc" ]]; then
    FONT="/System/Library/Fonts/Menlo.ttc"
  else
    FONT="$(fc-match -f '%{file}' Sans 2>/dev/null || echo /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf)"
  fi
fi
[[ -f "$FONT" ]] || { echo "❌ font not found: $FONT" >&2; exit 1; }
echo "→ font: $FONT"

SIZE="${TYPO_SIZE:-54}"
COLOR="${TYPO_COLOR:-white@0.85}"
Y_RATIO="${TYPO_Y_RATIO:-0.78}"

# Escape special chars in drawtext text:  : ' \\ %
escape_text() {
  echo "$1" | sed "s/\\\\/\\\\\\\\/g; s/'/\\\\'/g; s/:/\\\\:/g; s/%/\\\\%/g"
}

# Build drawtext chain — one drawtext per phrase, gated by `enable='between(t,T0,T1)'`
# with fade keyed via alpha expression.  Concatenate into one filter chain.
FILTER=""
for ((i=0; i<N; i++)); do
  START=$(awk -v slot="$SLOT" -v idx="$i" 'BEGIN{printf "%.3f", slot*idx + 0.3}')
  END=$(awk -v start="$START" -v hold="$HOLD" 'BEGIN{printf "%.3f", start+hold}')
  PEAK_START=$(awk -v s="$START" -v f="$FADE_IN"  'BEGIN{printf "%.3f", s+f}')
  PEAK_END=$(awk   -v e="$END"   -v f="$FADE_OUT" 'BEGIN{printf "%.3f", e-f}')

  TXT=$(escape_text "${PHRASES[$i]}")

  # Alpha expression: triangle ramp 0 → 1 → 1 → 0 within [START, END]
  ALPHA="if(between(t,${START},${PEAK_START}),(t-${START})/${FADE_IN},if(between(t,${PEAK_START},${PEAK_END}),1,if(between(t,${PEAK_END},${END}),(${END}-t)/${FADE_OUT},0)))"

  DT="drawtext=fontfile='${FONT}':text='${TXT}':fontsize=${SIZE}:fontcolor=${COLOR%@*}:alpha='${ALPHA}':x=(w-text_w)/2:y=h*${Y_RATIO}:shadowcolor=black@0.65:shadowx=2:shadowy=2:borderw=1:bordercolor=black@0.55:enable='between(t,${START},${END})'"

  if [[ -z "$FILTER" ]]; then
    FILTER="$DT"
  else
    FILTER="${FILTER},${DT}"
  fi
done

"$FFMPEG" -y -loglevel warning -stats \
  -i "$SRC" \
  -vf "${FILTER}" \
  -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"

dur=$("$FFPROBE" -v error -show_entries format=duration -of csv=p=0 "$DST" 2>/dev/null | awk '{printf "%.1f", $1}')
size=$(du -h "$DST" 2>/dev/null | awk '{print $1}')
echo "✓ typography: $DST (${dur}s, $size)  ${N} phrases overlaid"
