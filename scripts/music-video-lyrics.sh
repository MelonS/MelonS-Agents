#!/usr/bin/env bash
# Designer-style on-screen lyrics overlay for vocal music tracks.
#
# Distinct from `music-video-typography.sh` (mood phrases for instrumental):
#   - Lyrics are full-line vocal text, timing-critical
#   - Position varies per line (4-position rotation) — not always bottom
#   - Larger font, genre-themed colors, stroke + glow for legibility
#   - Designed-look not documentary-subtitle look
#
# Usage:
#   scripts/music-video-lyrics.sh <input.mp4> <output.mp4> <lyrics_file> [--genre=NAME]
#
# Lyrics file formats supported (auto-detected):
#
#   1. LRC format (timestamped):
#        [00:00.00]rainy window of the late train
#        [00:04.50]the city lights are blurring
#        [00:09.00]somewhere we used to know
#
#   2. Plain text (one line per scene, auto-spaced across duration):
#        rainy window of the late train
#        the city lights are blurring
#        somewhere we used to know
#
# --genre flag applies the preset's LUT direction as base color:
#     citypop      → warm cream / violet shadow
#     jazz         → warm cream / deep brown shadow
#     lofi_hiphop  → warm amber / black shadow
#     synthwave    → hot pink / cyan shadow
#     ambient      → cool desat white / blue shadow
#     (others)     → fall through to clean white / black shadow
#
# Environment overrides:
#   LYRICS_FONT      font file path (default: SF Pro Display Black if installed,
#                    else SF Mono Regular, else Menlo)
#   LYRICS_SIZE      base font size px (default: 88 for 1080×1920)
#   LYRICS_FADE      fade in/out duration s (default: 0.4)

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
# shellcheck disable=SC1091
source "$PWD/agents/lib/env.sh" 2>/dev/null || true

SRC="${1:-}"
DST="${2:-}"
LYRICS_FILE="${3:-}"
GENRE=""

# Parse trailing --genre flag if present
for arg in "$@"; do
  case "$arg" in
    --genre=*) GENRE="${arg#*=}" ;;
  esac
done

if [[ -z "$SRC" || -z "$DST" || -z "$LYRICS_FILE" ]]; then
  echo "usage: $0 <input.mp4> <output.mp4> <lyrics_file> [--genre=NAME]" >&2
  exit 64
fi
[[ -f "$SRC" ]]   || { echo "❌ input not found: $SRC" >&2; exit 64; }
[[ -f "$LYRICS_FILE" ]] || { echo "❌ lyrics file not found: $LYRICS_FILE" >&2; exit 64; }

FFMPEG="${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}"
FFPROBE="${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}"

DUR=$("$FFPROBE" -v error -show_entries format=duration -of csv=p=0 "$SRC" | awk '{printf "%.3f", $1}')

# Font selection — auto-detect Korean content for CJK font fallback.
# Korean lyrics need AppleSDGothicNeo or similar; pure Latin can use
# the bolder SF Pro Display Black for stronger designed look.
FONT="${LYRICS_FONT:-}"
HAS_KOREAN=0
if grep -qE '[가-힣]' "$LYRICS_FILE"; then HAS_KOREAN=1; fi

if [[ -z "$FONT" ]]; then
  if [[ "$HAS_KOREAN" == 1 ]]; then
    # CJK-capable fonts first; bold weights for designed look
    for cand in \
      "/System/Library/Fonts/AppleSDGothicNeo.ttc" \
      "/System/Library/Fonts/AppleGothic.ttf" \
      "/System/Library/Fonts/Supplemental/NotoSansCJK-Bold.ttc" \
      "/System/Library/Fonts/Helvetica.ttc"; do
      if [[ -f "$cand" ]]; then FONT="$cand"; break; fi
    done
  else
    for cand in \
      "$HOME/Library/Fonts/SF-Pro-Display-Black.otf" \
      "/System/Library/Fonts/SFCompactDisplay-Black.otf" \
      "$HOME/Library/Fonts/SF-Mono-Regular.otf" \
      "/System/Library/Fonts/Menlo.ttc"; do
      if [[ -f "$cand" ]]; then FONT="$cand"; break; fi
    done
  fi
fi
[[ -f "$FONT" ]] || { echo "❌ no font found (set LYRICS_FONT=PATH)" >&2; exit 1; }

SIZE="${LYRICS_SIZE:-88}"
FADE="${LYRICS_FADE:-0.4}"

# Genre → color scheme
case "$GENRE" in
  citypop)     PRIMARY="white@0.95"     ; SHADOW="#221033@0.85" ; BORDER="#FFFFFF@0.10" ;;
  jazz)        PRIMARY="#FFEBC8@0.95"   ; SHADOW="#1A0F08@0.85" ; BORDER="#7A4920@0.30" ;;
  lofi_hiphop) PRIMARY="#FFE8B8@0.95"   ; SHADOW="#000000@0.80" ; BORDER="#000000@0.30" ;;
  synthwave)   PRIMARY="#FF3FB5@0.95"   ; SHADOW="#003BFF@0.85" ; BORDER="#FFFFFF@0.20" ;;
  ambient)     PRIMARY="#E8E8F8@0.95"   ; SHADOW="#0B1840@0.85" ; BORDER="#FFFFFF@0.10" ;;
  cottagecore) PRIMARY="#FFF4D6@0.95"   ; SHADOW="#3B1A00@0.85" ; BORDER="#FFFFFF@0.10" ;;
  classical)   PRIMARY="#F4E8D0@0.95"   ; SHADOW="#1A1208@0.85" ; BORDER="#FFFFFF@0.10" ;;
  *)           PRIMARY="white@0.95"     ; SHADOW="black@0.85"   ; BORDER="black@0.30"   ;;
esac

# Parse lyrics file — detect LRC vs plain text
PARSED=$(mktemp)
trap "rm -f '$PARSED'" EXIT
HAS_TIMESTAMPS=0
if grep -qE '^\[[0-9]+:[0-9]+(\.[0-9]+)?\]' "$LYRICS_FILE"; then HAS_TIMESTAMPS=1; fi

if [[ "$HAS_TIMESTAMPS" == 1 ]]; then
  # LRC parse — emit "T_START T_END TEXT" per line
  python3 - <<PY > "$PARSED"
import re
lines = []
with open("$LYRICS_FILE", encoding="utf-8") as f:
    for ln in f:
        m = re.match(r'\[(\d+):(\d+(?:\.\d+)?)\](.+)', ln.strip())
        if not m: continue
        mm, ss, txt = m.groups()
        t = int(mm)*60 + float(ss)
        lines.append((t, txt.strip()))
# Add T_END as next line's T or duration
lines.sort()
DUR = float("$DUR")
for i, (t, txt) in enumerate(lines):
    end = lines[i+1][0] if i+1 < len(lines) else min(t + 5.0, DUR)
    print(f"{t:.3f}\t{end:.3f}\t{txt}")
PY
else
  # Plain text — auto-space across duration
  python3 - <<PY > "$PARSED"
lines = [l.strip() for l in open("$LYRICS_FILE", encoding="utf-8") if l.strip() and not l.strip().startswith('#') and not l.strip().startswith('[')]
DUR = float("$DUR")
N = len(lines)
if N == 0: raise SystemExit("no lyric lines")
slot = DUR / N
for i, txt in enumerate(lines):
    t0 = i * slot + 0.3
    t1 = (i+1) * slot - 0.3
    print(f"{t0:.3f}\t{t1:.3f}\t{txt}")
PY
fi

LINE_COUNT=$(wc -l < "$PARSED" | tr -d ' ')
echo "→ $LINE_COUNT lyric lines, font=$(basename "$FONT"), size=$SIZE, genre=${GENRE:-default}"

# Escape text for drawtext
escape_text() {
  echo "$1" | sed "s/\\\\/\\\\\\\\/g; s/'/\\\\'/g; s/:/\\\\:/g; s/%/\\\\%/g"
}

# Build drawtext filter chain — 4-position rotation per line
# Positions (x, y) for 1080×1920:
#   0: top-center,        y = h*0.18
#   1: bottom-center,     y = h*0.78
#   2: center-left aligned, y = h*0.35, x left-aligned with margin
#   3: center-right aligned, y = h*0.62, x right-aligned with margin
POSITIONS=(
  "(w-text_w)/2:h*0.18"
  "(w-text_w)/2:h*0.78"
  "w*0.08:h*0.35"
  "w-text_w-w*0.08:h*0.62"
)

FILTER=""
i=0
while IFS=$'\t' read -r T0 T1 TXT; do
  TXT_E=$(escape_text "$TXT")
  POS="${POSITIONS[$((i % 4))]}"
  X="${POS%%:*}"
  Y="${POS##*:}"

  PEAK_START=$(awk -v s="$T0" -v f="$FADE" 'BEGIN{printf "%.3f", s+f}')
  PEAK_END=$(awk   -v e="$T1" -v f="$FADE" 'BEGIN{printf "%.3f", e-f}')

  # Triangle fade: 0 → 1 → 1 → 0 within [T0, T1]
  ALPHA="if(between(t,${T0},${PEAK_START}),(t-${T0})/${FADE},if(between(t,${PEAK_START},${PEAK_END}),1,if(between(t,${PEAK_END},${T1}),(${T1}-t)/${FADE},0)))"

  DT="drawtext=fontfile='${FONT}':text='${TXT_E}':fontsize=${SIZE}:fontcolor=${PRIMARY%@*}:alpha='${ALPHA}':x=${X}:y=${Y}:shadowcolor=${SHADOW%@*}:shadowx=4:shadowy=4:borderw=2:bordercolor=${BORDER%@*}:enable='between(t,${T0},${T1})'"

  if [[ -z "$FILTER" ]]; then
    FILTER="$DT"
  else
    FILTER="${FILTER},${DT}"
  fi
  i=$((i + 1))
done < "$PARSED"

"$FFMPEG" -y -loglevel warning -stats \
  -i "$SRC" \
  -vf "${FILTER}" \
  -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"

dur=$("$FFPROBE" -v error -show_entries format=duration -of csv=p=0 "$DST" 2>/dev/null | awk '{printf "%.1f", $1}')
size=$(du -h "$DST" 2>/dev/null | awk '{print $1}')
echo "✓ lyrics: $DST (${dur}s, $size)  ${LINE_COUNT} lines"
