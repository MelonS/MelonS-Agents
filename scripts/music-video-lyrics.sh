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
ALIGN_AUDIO=""    # --align-to-audio=AUDIO  → derive LRC from plain text via whisper alignment

# Parse trailing flags if present
for arg in "$@"; do
  case "$arg" in
    --genre=*)         GENRE="${arg#*=}" ;;
    --align-to-audio=*) ALIGN_AUDIO="${arg#*=}" ;;
  esac
done

if [[ -z "$SRC" || -z "$DST" || -z "$LYRICS_FILE" ]]; then
  echo "usage: $0 <input.mp4> <output.mp4> <lyrics_file> [--genre=NAME] [--align-to-audio=AUDIO]" >&2
  exit 64
fi
[[ -f "$SRC" ]]   || { echo "❌ input not found: $SRC" >&2; exit 64; }
[[ -f "$LYRICS_FILE" ]] || { echo "❌ lyrics file not found: $LYRICS_FILE" >&2; exit 64; }

# §8 exception: ffmpeg parameter-expansion default — same pattern as
# scripts/music-video-shaders.sh:103.  Registered in operator-contract.md §8.

# Optional alignment: derive an LRC file from plain text + vocal audio
# via whisper-based vocal-onset alignment.  Operator-set LYRIC_LEAD_MS
# (default 200) gives a small early lead so the line is visible by the
# time the vocal hits.  See docs/research/2026-05-22-music-video-quality-bar.md
# §A.2.
if [[ -n "$ALIGN_AUDIO" ]]; then
  if [[ ! -f "$ALIGN_AUDIO" ]]; then
    echo "❌ --align-to-audio target not found: $ALIGN_AUDIO" >&2
    exit 64
  fi
  if grep -qE '^\[[0-9]+:[0-9]+(\.[0-9]+)?\]' "$LYRICS_FILE"; then
    echo "→ lyrics file already has LRC timing; skipping --align-to-audio" >&2
  else
    REPO_ROOT_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
    ALIGNED_LRC="${TMPDIR:-/tmp}/lyric-aligned-$$.$(basename "$LYRICS_FILE" .txt).lrc"
    echo "→ aligning lyrics to vocal onsets in $(basename "$ALIGN_AUDIO")"
    if bash "$REPO_ROOT_SCRIPT/scripts/music-video-lyric-align.sh" \
         "$ALIGN_AUDIO" "$LYRICS_FILE" "$ALIGNED_LRC"; then
      LYRICS_FILE="$ALIGNED_LRC"
    else
      echo "⚠️  alignment failed — falling back to auto-spaced timing" >&2
    fi
  fi
fi

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

# Escape text for ffmpeg drawtext: when wrapped in single quotes,
# embedded apostrophes need '\'' (close, escape, reopen) form,
# colons need \:.  Backslashes need doubling first to not interfere.
escape_text() {
  python3 - <<PY
import sys
s = """$1"""
# Backslashes first
s = s.replace('\\\\', '\\\\\\\\')
# Single quote: 'close, \\' literal, reopen' (the ffmpeg-standard escape)
s = s.replace("'", "'\\\\''")
# Colons need escape inside drawtext args
s = s.replace(':', '\\\\:')
# Percent signs (used by some sequences)
s = s.replace('%', '\\\\%')
sys.stdout.write(s)
PY
}

# Build drawtext filter chain — 4-position rotation per line.
# Use bracket-based chain ([v_n] → [v_n+1]) instead of comma-chain so
# commas inside between()/if() expressions don't get treated as
# filter-graph separators by newer ffmpeg (≥8.x is stricter).
# Positions (x, y) for 1080×1920:
#   0: top-center, 1: bottom-center, 2: left-aligned, 3: right-aligned
POSITIONS=(
  "(w-text_w)/2:h*0.18"
  "(w-text_w)/2:h*0.78"
  "w*0.08:h*0.35"
  "w-text_w-w*0.08:h*0.62"
)

FILTER_CHAIN=""
i=0
while IFS=$'\t' read -r T0 T1 TXT; do
  TXT_E=$(escape_text "$TXT")
  POS="${POSITIONS[$((i % 4))]}"
  X="${POS%%:*}"
  Y="${POS##*:}"

  PEAK_START=$(awk -v s="$T0" -v f="$FADE" 'BEGIN{printf "%.3f", s+f}')
  PEAK_END=$(awk   -v e="$T1" -v f="$FADE" 'BEGIN{printf "%.3f", e-f}')

  # Use single quotes (NOT backslash escapes) for comma-containing
  # expressions — ffmpeg parser strips the outer quotes and treats
  # everything between as literal value, so commas don't conflict
  # with filter-chain separators.  Bracket-chain output tag needs
  # a space separator after the closing quote so it's not consumed
  # as part of the enable value.
  ALPHA="if(between(t,${T0},${PEAK_START}),(t-${T0})/${FADE},if(between(t,${PEAK_START},${PEAK_END}),1,if(between(t,${PEAK_END},${T1}),(${T1}-t)/${FADE},0)))"
  ENABLE="between(t,${T0},${T1})"

  IN_TAG=$([[ $i -eq 0 ]] && echo "[in]" || echo "[v${i}]")
  OUT_TAG="[v$((i+1))]"

  DT="${IN_TAG}drawtext=fontfile='${FONT}':text='${TXT_E}':fontsize=${SIZE}:fontcolor=${PRIMARY%@*}:alpha='${ALPHA}':x=${X}:y=${Y}:shadowcolor=${SHADOW%@*}:shadowx=4:shadowy=4:borderw=2:bordercolor=${BORDER%@*}:enable='${ENABLE}' ${OUT_TAG}"

  if [[ -z "$FILTER_CHAIN" ]]; then
    FILTER_CHAIN="$DT"
  else
    FILTER_CHAIN="${FILTER_CHAIN};${DT}"
  fi
  i=$((i + 1))
done < "$PARSED"

# Final output tag is whichever the last drawtext wrote to
FINAL_TAG="[v${i}]"
# Rewrite first input label from [in] to [0:v] (ffmpeg's stream label)
FILTER_CHAIN="${FILTER_CHAIN/\[in\]/[0:v]}"

"$FFMPEG" -y -loglevel warning -stats \
  -i "$SRC" \
  -filter_complex "${FILTER_CHAIN}" \
  -map "$FINAL_TAG" -map "0:a" \
  -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"

dur=$("$FFPROBE" -v error -show_entries format=duration -of csv=p=0 "$DST" 2>/dev/null | awk '{printf "%.1f", $1}')
size=$(du -h "$DST" 2>/dev/null | awk '{print $1}')
echo "✓ lyrics: $DST (${dur}s, $size)  ${LINE_COUNT} lines"
