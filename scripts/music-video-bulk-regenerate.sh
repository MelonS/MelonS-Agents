#!/usr/bin/env bash
# Bulk regenerate the 5/20 ToddStudio batch with correct genre presets.
#
# Operator decision (logged in morning brief): retroactively fix the
# Linen + Rain mismatches?  Run this when the answer is "yes".
#
# What it does:
#   For each of the 5 source music files in assets/music/track[1-5]-*.mp3,
#   detect genre, render with correct genre preset, drop the output into
#   outputs/publish/2026-05-21-regen-v2/<NN>-<track>.mp4.
#
# Then operator can manually replace the 5/20 originals on YT Studio (or
# upload as new) using:
#   bash scripts/yt-batch-upload.sh outputs/publish/upload-meta-v2
#
# Usage:
#   scripts/music-video-bulk-regenerate.sh           # render all 5
#   scripts/music-video-bulk-regenerate.sh --dry-run # show plan only
#   scripts/music-video-bulk-regenerate.sh --only=4  # render only track #4

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh" 2>/dev/null || true

AUTO_SH="$REPO_ROOT/scripts/music-video-auto.sh"
DETECT_SH="$REPO_ROOT/scripts/music-video-genre-detect.sh"
OUT_DIR="$REPO_ROOT/outputs/publish/2026-05-21-regen-v2"
mkdir -p "$OUT_DIR"

# Source → desired output filename mapping (mirrors 5/20 batch metadata)
declare -a BATCH=(
  "1|track4-rain.mp3|01-rain-lofi-v2.mp4"
  "2|track5-linen.mp3|02-linen-minimal-v2.mp4"
  "3|track3-arcade.mp3|03-arcade-synthwave-v2.mp4"
  "4|track1-coastline.mp3|04-coastline-summer-v2.mp4"
  "5|track2-noir.mp3|05-noir-detective-v2.mp4"
)

DRY=0
ONLY=""
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY=1 ;;
    --only=*)  ONLY="${arg#*=}" ;;
    --help|-h) sed -n '1,/^set -uo/p' "$0" | grep '^#' | sed 's/^# \?//'; exit 0 ;;
    *) echo "❌ unknown flag: $arg" >&2; exit 64 ;;
  esac
done

echo "═══════════════════════════════════════════════════════════════"
echo " Bulk regenerate — 2026-05-20 batch → v2 with genre presets"
echo "═══════════════════════════════════════════════════════════════"
echo " Output dir: $OUT_DIR"
[[ -n "$ONLY" ]] && echo " Only track: $ONLY"
[[ "$DRY" == "1" ]] && echo " DRY RUN (no renders)"
echo

for entry in "${BATCH[@]}"; do
  IFS='|' read -r N SRC OUT <<< "$entry"
  if [[ -n "$ONLY" && "$N" != "$ONLY" ]]; then
    continue
  fi
  SRC_PATH="$REPO_ROOT/assets/music/$SRC"
  if [[ ! -f "$SRC_PATH" ]]; then
    echo "⚠️  skip #$N — music not found: $SRC_PATH" >&2
    continue
  fi
  GENRE=$(bash "$DETECT_SH" "$SRC_PATH" 2>/dev/null)
  echo "─── #$N $SRC → $OUT (genre=$GENRE) ───"

  if [[ "$DRY" == "1" ]]; then
    echo "  [dry] would run: scripts/music-video-auto.sh $SRC_PATH"
    continue
  fi

  # Stillzoom genres need a still image.  For ambient/classical/dreamcore,
  # auto-extract a frame from the existing 5/20 output as the still.
  CUT_MODE=$(yq -r ".genres.${GENRE}.cut_mode" "$REPO_ROOT/skills/music-video/data/genre-presets.yaml")
  AUTO_ARGS=("$SRC_PATH" "--short-id=regen-${N}")
  if [[ "$CUT_MODE" == "stillzoom" ]]; then
    # Locate original 5/20 mp4 to extract a still from
    case "$SRC" in
      track4-rain.mp3)        ORIG="$REPO_ROOT/outputs/publish/2026-05-20/2100-rain-lofi.mp4" ;;
      track5-linen.mp3)       ORIG="$REPO_ROOT/outputs/publish/2026-05-21/0900-linen-minimal.mp4" ;;
      track3-arcade.mp3)      ORIG="$REPO_ROOT/outputs/publish/2026-05-21/2100-arcade-synthwave.mp4" ;;
      track1-coastline.mp3)   ORIG="$REPO_ROOT/outputs/publish/2026-05-22/0900-coastline-summer.mp4" ;;
      track2-noir.mp3)        ORIG="$REPO_ROOT/outputs/publish/2026-05-22/2100-noir-detective.mp4" ;;
      *)                       ORIG="" ;;
    esac
    if [[ -n "$ORIG" && -f "$ORIG" ]]; then
      STILL_DIR="$OUT_DIR/_stills"
      mkdir -p "$STILL_DIR"
      STILL="$STILL_DIR/${N}-still.jpg"
      "${FFMPEG_BIN:-ffmpeg}" -y -loglevel error -ss 5 -i "$ORIG" -frames:v 1 "$STILL"
      AUTO_ARGS+=("--image=$STILL")
      echo "  stillzoom — extracted still from $ORIG"
    else
      echo "  ⚠️  stillzoom needed but no original mp4 to extract from — skipping" >&2
      continue
    fi
  fi

  if ! bash "$AUTO_SH" "${AUTO_ARGS[@]}"; then
    echo "  ❌ regen #$N failed" >&2
    continue
  fi

  # Move the latest record output into outputs/publish/...-v2/
  LATEST=$(ls -1t "${RECORDS_DIR:-$REPO_ROOT/records}/missions/$(date +%Y-%m-%d)/music-video-regen-${N}-"*/outputs/short*.mp4 2>/dev/null | head -1)
  if [[ -n "$LATEST" && -f "$LATEST" ]]; then
    cp "$LATEST" "$OUT_DIR/$OUT"
    echo "  ✓ $OUT_DIR/$OUT  ($(du -h "$OUT_DIR/$OUT" | awk '{print $1}'))"
  else
    echo "  ⚠️  could not locate regen output for #$N" >&2
  fi
  echo
done

echo
echo "Done.  v2 batch at: $OUT_DIR"
ls -la "$OUT_DIR"/*.mp4 2>/dev/null | awk '{printf "  %-45s %s\n", $NF, $5}'
