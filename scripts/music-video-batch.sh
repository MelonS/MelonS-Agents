#!/usr/bin/env bash
# music-video-batch.sh — re-render multiple music tracks with the
# current music-video pipeline.  Auto-pairs each track with its
# lyric file (when present), passes through gate-mode + duration env.
#
# Lyric pairing convention:
#   vocal-citypop-eng-*.mp3      → assets/lyrics/citypop-eng.txt
#   vocal-citypop-kr-*.mp3       → assets/lyrics/citypop-kr.txt (if exists)
#   vocal-kpop-ballad-*.mp3      → assets/lyrics/kpop-ballad.txt
#   vocal-kpop-dance-*.mp3       → assets/lyrics/kpop-dance.txt
#   vocal-rnb-*.mp3              → assets/lyrics/rnb.txt
#   vocal-uspop-*.mp3            → assets/lyrics/uspop.txt
#   (others fall through to no-lyrics render)
#
# Usage:
#   scripts/music-video-batch.sh                       # all assets/music/vocal-*.mp3
#   scripts/music-video-batch.sh "assets/music/vocal-kpop*.mp3"
#   scripts/music-video-batch.sh --dry-run             # show planned renders, no compute
#
# Env passthrough:
#   MUSIC_VIDEO_DURATION              default 60
#   MUSIC_VIDEO_SHADER_GATE           default uniform; phrase_climax / onsets / beats
#   FFMPEG_THROTTLE                   inherited (per .env)

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

DRY_RUN=0
PATTERN="$REPO_ROOT/assets/music/vocal-*.mp3"
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY_RUN=1 ;;
    -h|--help)
      sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    -*) echo "unknown flag: $arg" >&2; exit 64 ;;
    *)  PATTERN="$arg" ;;
  esac
done

# Resolve lyric file by track filename pattern.
lyric_for() {
  local mp3="$1"
  local bn
  bn=$(basename "$mp3")
  case "$bn" in
    vocal-citypop-eng-*)   echo "$REPO_ROOT/assets/lyrics/citypop-eng.txt" ;;
    vocal-citypop-kr-*)    echo "$REPO_ROOT/assets/lyrics/citypop-kr.txt" ;;
    vocal-kpop-ballad-*)   echo "$REPO_ROOT/assets/lyrics/kpop-ballad.txt" ;;
    vocal-kpop-dance-*)    echo "$REPO_ROOT/assets/lyrics/kpop-dance.txt" ;;
    vocal-rnb-*)           echo "$REPO_ROOT/assets/lyrics/rnb.txt" ;;
    vocal-uspop-*)         echo "$REPO_ROOT/assets/lyrics/uspop.txt" ;;
    *)                     echo "" ;;
  esac
}

# Resolve a short_id from filename (lowercase, no special chars).
short_id_for() {
  local mp3="$1"
  local bn
  bn=$(basename "$mp3" .mp3)
  printf '%s' "$bn" | tr 'A-Z ' 'a-z-' | tr -cd 'a-z0-9-' | cut -c1-40
}

# Enumerate tracks (glob expanded by shell).
TRACKS=()
for f in $PATTERN; do
  [[ -f "$f" ]] || continue
  TRACKS+=("$f")
done

if [[ "${#TRACKS[@]}" -eq 0 ]]; then
  echo "no tracks match pattern: $PATTERN" >&2
  exit 1
fi

echo "=== music-video-batch ==="
echo "tracks:    ${#TRACKS[@]}"
echo "pattern:   $PATTERN"
echo "duration:  ${MUSIC_VIDEO_DURATION:-60}s"
echo "gate:      ${MUSIC_VIDEO_SHADER_GATE:-uniform}"
echo "throttle:  ${FFMPEG_THROTTLE:-0}"
echo

# Plan
PLAN=()
for mp3 in "${TRACKS[@]}"; do
  lyr=$(lyric_for "$mp3")
  sid=$(short_id_for "$mp3")
  if [[ -n "$lyr" && -f "$lyr" ]]; then
    PLAN+=("$sid → $(basename "$mp3") + $(basename "$lyr")")
  else
    PLAN+=("$sid → $(basename "$mp3") (no lyrics)")
  fi
done

i=0
for line in "${PLAN[@]}"; do
  i=$((i+1))
  printf "  %02d/%02d  %s\n" "$i" "${#TRACKS[@]}" "$line"
done
echo

if [[ "$DRY_RUN" -eq 1 ]]; then
  echo "(dry-run — exiting before render)"
  exit 0
fi

read -r -p "Render ${#TRACKS[@]} tracks? [Y/n] " confirm
if [[ "${confirm:-Y}" =~ ^[Nn] ]]; then
  echo "aborted."
  exit 0
fi

START_TS=$(date +%s)
FAIL=0
DONE=0
for mp3 in "${TRACKS[@]}"; do
  DONE=$((DONE+1))
  sid=$(short_id_for "$mp3")
  lyr=$(lyric_for "$mp3")
  echo
  echo "============================================================"
  echo " [$DONE/${#TRACKS[@]}] $sid"
  echo "============================================================"

  CMD=("$REPO_ROOT/scripts/music-video-auto.sh" "$mp3" "--short-id=$sid")
  if [[ -n "$lyr" && -f "$lyr" ]]; then
    CMD+=("--with-lyrics=$lyr")
  fi

  if bash "${CMD[@]}"; then
    echo "  ✓ done"
  else
    rc=$?
    echo "  ✗ failed (exit $rc) — continuing batch"
    FAIL=$((FAIL+1))
  fi
done

END_TS=$(date +%s)
ELAPSED=$(( END_TS - START_TS ))

echo
echo "============================================================"
echo "Batch complete in ${ELAPSED}s — $DONE done, $FAIL failed"
echo "============================================================"

[[ "$FAIL" -gt 0 ]] && exit 1
exit 0
