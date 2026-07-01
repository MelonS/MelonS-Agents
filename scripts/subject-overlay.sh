#!/usr/bin/env bash
# subject-overlay.sh — channel branding + legal disclaimers for a REAL-subject
# short (idol/artist content about a real artist/idol group).
#
# Builds ON TOP of a finished faceless render (never re-encodes the base). Adds:
#   - a channel-name lower-third
#   - a corner brand mark
#   - a stacked disclaimer strip: fan-content ("unofficial / not affiliated")
#     + AI-narration. BOTH are legally BLOCKING for real-subject profiles.
#
# Real people → likeness is rights-protected, so the overlay is TEXT-based and
# does NOT depict the members. An operator who holds rights to a specific image
# may set brand.member_image in the subject file; otherwise it stays text-only.
#
# Usage:   scripts/subject-overlay.sh <subject_id> <in.mp4> <out.mp4>
# Reads:   config/subjects/<subject_id>.yaml
# Exit: 0 ok · 64 usage · 66 subject file missing · 70 ffmpeg failed

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh" 2>/dev/null || true
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/log.sh" 2>/dev/null || { log_step(){ echo "→ $*"; }; log_ok(){ echo "✓ $*"; }; log_info(){ echo "  $*"; }; log_warn(){ echo "⚠ $*" >&2; }; log_err(){ echo "❌ $*" >&2; }; }
set +e

SUBJECT_ID="${1:-}"; IN="${2:-}"; OUT="${3:-}"
if [[ -z "$SUBJECT_ID" || -z "$IN" || -z "$OUT" ]]; then
  log_err "usage: $0 <subject_id> <in.mp4> <out.mp4>"; exit 64
fi
SFILE="$REPO_ROOT/config/subjects/$SUBJECT_ID.yaml"
if [[ ! -f "$SFILE" ]]; then
  log_err "subject file not found: $SFILE"; exit 66
fi
if [[ ! -f "$IN" ]]; then
  log_err "input video not found: $IN"; exit 64
fi

# Read a value by key name anywhere in the (nested) subject YAML; handles
# "quoted" + inline-comment forms.
yget() {
  awk -v k="$1" '$0 ~ "^[[:space:]]*"k":" {
      line=$0; sub(/^[^:]*:[[:space:]]*/,"",line)
      if (line ~ /^"/) { sub(/^"/,"",line); sub(/".*$/,"",line) }
      else { sub(/[[:space:]]*#.*$/,"",line); sub(/[[:space:]]+$/,"",line) }
      print line; exit }' "$SFILE"
}

DISPLAY_NAME="$(yget display_name)";    [[ -z "$DISPLAY_NAME" ]] && DISPLAY_NAME="$SUBJECT_ID"
LOWER_THIRD="$(yget lower_third_text)"; [[ -z "$LOWER_THIRD" ]] && LOWER_THIRD="$DISPLAY_NAME"
BRAND_MARK="$(yget brand_mark_text)";   [[ -z "$BRAND_MARK" ]] && BRAND_MARK="$DISPLAY_NAME"
ACCENT="$(yget accent_color)";          [[ -z "$ACCENT" ]] && ACCENT="0x7C4DFF"
FAN_DISC="$(yget fan_content)";         [[ -z "$FAN_DISC" ]] && FAN_DISC="비공식 팬 제작 / Unofficial fan-made"
AI_DISC="$(yget ai_narration)";         [[ -z "$AI_DISC" ]] && AI_DISC="AI 합성 나레이션 / Synthetic AI narration"
MEMBER_IMG="$(yget member_image)"
FONTHINT="$(yget font_hint)"

FONT="${FONTHINT:-${LAYOUT_DRAWTEXT_FONTFILE:-}}"
FFMPEG_BIN="${FFMPEG_BIN:-ffmpeg}"

log_step "subject overlay: $DISPLAY_NAME ($SUBJECT_ID)"

# Stage font + text as BASENAMES inside the output dir, then cd there to run
# ffmpeg.  Two cross-platform reasons (both bit us on Windows):
#  - drawtext text via textfile= (NOT text=) so colons (RE:SCENE), slashes,
#    spaces and Hangul need zero filter-escaping — the file bytes are taken
#    literally.  An inline text='RE:SCENE' makes ffmpeg read ':' as an option
#    separator and the whole filterchain fails to parse.
#  - a native-Windows ffmpeg can't open an absolute POSIX path (/c/Windows/..)
#    written INSIDE a filter string; only argv paths get MSYS-translated.  A
#    basename + cd sidesteps it.  IN/OUT stay absolute (they ARE argv).
STAGE_DIR="$(cd "$(dirname "$OUT")" && pwd)"
ABS_IN="$(cd "$(dirname "$IN")" && pwd)/$(basename "$IN")"
OUT_BASE="$(basename "$OUT")"

FONT_OPT=""
if [[ -n "$FONT" && -f "$FONT" ]]; then
  cp -f "$FONT" "$STAGE_DIR/_ov_font.${FONT##*.}"
  FONT_OPT="fontfile=_ov_font.${FONT##*.}:"
else
  log_warn "no usable unicode font (LAYOUT_DRAWTEXT_FONTFILE/font_hint) — Hangul may render as boxes"
  log_warn "  set LAYOUT_DRAWTEXT_FONTFILE in .env to a unicode .ttf to fix"
fi

# One textfile per overlay element (basenames; content is literal UTF-8).
printf '%s' "$LOWER_THIRD" > "$STAGE_DIR/_ov_lt.txt"
printf '%s' "$BRAND_MARK"  > "$STAGE_DIR/_ov_bm.txt"
printf '%s' "$FAN_DISC"    > "$STAGE_DIR/_ov_ds1.txt"
printf '%s' "$AI_DISC"     > "$STAGE_DIR/_ov_ds2.txt"

# 1080x1920 canvas. Lower-third (bottom-left), brand mark (top-right), and a
# two-line disclaimer block bottom-center (fan-content over AI-narration) —
# both legally blocking, so they are always burned in.
# Lower-third sits ABOVE the caption band.  The burned caption (faceless ASS)
# is bottom-centred at LAYOUT_SAFE_MARGIN_V≈280px from the bottom; a lower-third
# at y=h-320 collides with it.  h-490 gives the channel chyron its own band
# clear of both the caption and the bottom disclaimer strip.
LT="drawtext=${FONT_OPT}textfile=_ov_lt.txt:fontsize=44:fontcolor=white:box=1:boxcolor=${ACCENT}@0.85:boxborderw=18:x=48:y=h-490"
BM="drawtext=${FONT_OPT}textfile=_ov_bm.txt:fontsize=34:fontcolor=white@0.92:box=1:boxcolor=black@0.35:boxborderw=12:x=w-tw-48:y=56"
DS1="drawtext=${FONT_OPT}textfile=_ov_ds1.txt:fontsize=24:fontcolor=white@0.95:box=1:boxcolor=black@0.6:boxborderw=10:x=(w-tw)/2:y=h-128"
DS2="drawtext=${FONT_OPT}textfile=_ov_ds2.txt:fontsize=24:fontcolor=white@0.95:box=1:boxcolor=black@0.6:boxborderw=10:x=(w-tw)/2:y=h-90"
VF="${LT},${BM},${DS1},${DS2}"

if [[ -n "$MEMBER_IMG" && -f "$MEMBER_IMG" ]]; then
  log_warn "compositing operator-supplied member image: $MEMBER_IMG"
  log_warn "  → ensure you hold rights to this image (publicity/portrait + copyright)"
  ABS_MI="$(cd "$(dirname "$MEMBER_IMG")" && pwd)/$(basename "$MEMBER_IMG")"
  ( cd "$STAGE_DIR" && "$FFMPEG_BIN" -y -loglevel error -i "$ABS_IN" -i "$ABS_MI" \
      -filter_complex "[1:v]scale=-1:360[mi];[0:v][mi]overlay=40:H-360-330[bg];[bg]${VF}[outv]" \
      -map "[outv]" -map 0:a? -c:v libx264 -preset medium -crf 21 -c:a copy -movflags +faststart "$OUT_BASE" )
  RC=$?
else
  ( cd "$STAGE_DIR" && "$FFMPEG_BIN" -y -loglevel error -i "$ABS_IN" -vf "$VF" \
      -c:v libx264 -preset medium -crf 21 -c:a copy -movflags +faststart "$OUT_BASE" )
  RC=$?
fi

# Best-effort cleanup of the staged temp files.
rm -f "$STAGE_DIR"/_ov_font.* "$STAGE_DIR"/_ov_lt.txt "$STAGE_DIR"/_ov_bm.txt \
      "$STAGE_DIR"/_ov_ds1.txt "$STAGE_DIR"/_ov_ds2.txt 2>/dev/null

if [[ $RC -ne 0 || ! -f "$OUT" ]]; then
  log_err "ffmpeg overlay failed (rc=$RC)"; exit 70
fi
log_ok "subject overlay → $OUT"
