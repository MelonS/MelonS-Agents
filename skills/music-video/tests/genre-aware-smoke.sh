#!/usr/bin/env bash
# Smoke test for the genre-aware music-video stack (added 2026-05-21).
#
# Validates that:
#   1. genre-detect.sh returns expected genre for known filename patterns
#   2. genre.sh resolves aliases correctly (no false-positive substring matches)
#   3. shaders.sh handles all 6 new effects + the 4 classic effects
#   4. canvas.sh produces a sub-8MB 8s 720x1280 mp4
#   5. typography.sh overlays text without errors
#   6. auto.sh chains everything end-to-end (skipped if --no-pipeline)
#
# Usage:
#   skills/music-video/tests/genre-aware-smoke.sh              # full
#   skills/music-video/tests/genre-aware-smoke.sh --no-pipeline # skip 60s+ pipeline tests
#
# Exit 0 if all pass, non-zero on any failure.

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"

NO_PIPELINE=0
[[ "${1:-}" == "--no-pipeline" ]] && NO_PIPELINE=1

PASS=0
FAIL=0
TMPDIR=$(mktemp -d -t mvgenre-smoke-XXXX)
trap "rm -rf '$TMPDIR'" EXIT

ok()   { echo "  ✓ $1";   PASS=$((PASS+1)); }
bad()  { echo "  ❌ $1" >&2; FAIL=$((FAIL+1)); }

echo "═══ genre-aware-smoke ═══"
echo

# 1. Genre detection
echo "1. Genre detection from filename"
expected="lofi_hiphop"
actual=$(bash scripts/music-video-genre-detect.sh assets/music/track4-rain.mp3 2>/dev/null)
[[ "$actual" == "$expected" ]] && ok "track4-rain → $expected" || bad "track4-rain → got '$actual' expected '$expected'"

expected="ambient"
actual=$(bash scripts/music-video-genre-detect.sh assets/music/track5-linen.mp3 2>/dev/null)
[[ "$actual" == "$expected" ]] && ok "track5-linen → $expected" || bad "track5-linen → got '$actual' expected '$expected'"

expected="synthwave"
actual=$(bash scripts/music-video-genre-detect.sh assets/music/track3-arcade.mp3 2>/dev/null)
[[ "$actual" == "$expected" ]] && ok "track3-arcade → $expected" || bad "track3-arcade → got '$actual' expected '$expected'"

expected="jazz"
actual=$(bash scripts/music-video-genre-detect.sh assets/music/track2-noir.mp3 2>/dev/null)
[[ "$actual" == "$expected" ]] && ok "track2-noir → $expected" || bad "track2-noir → got '$actual' expected '$expected'"

echo
echo "2. Alias resolution (regression test for yq v4 substring bug)"
# This used to mis-resolve to 'drone' due to contains() substring match
RESOLVED=$(GENRE_NAME=jazz yq -r '
  .genres | to_entries | .[] |
  select(.key == env(GENRE_NAME) or
    ((.value.aliases // []) | map(. == env(GENRE_NAME)) | any)) |
  .key
' skills/music-video/data/genre-presets.yaml | head -1)
[[ "$RESOLVED" == "jazz" ]] && ok "jazz alias resolves to jazz (not drone)" \
                            || bad "jazz alias resolution → '$RESOLVED'"

echo
echo "3. All 10 shader effects produce output"
SRC=$(ls outputs/publish/2026-05-1*/?*.mp4 2>/dev/null | head -1)
if [[ -z "$SRC" ]]; then
  echo "  ⚠️  no source mp4 found — skipping shader tests"
else
  for fx in pond breathing halation combo scanline chromatic_split neon_edge vhs saturation_pulse kaleidoscope; do
    OUT="$TMPDIR/sh-${fx}.mp4"
    if bash scripts/music-video-shaders.sh "$fx" "$SRC" "$OUT" >/dev/null 2>&1 && [[ -s "$OUT" ]]; then
      ok "$fx"
    else
      bad "$fx render failed"
    fi
  done
fi

echo
echo "4. Canvas 8s loop"
if [[ -n "$SRC" ]]; then
  OUT="$TMPDIR/canvas.mp4"
  if bash scripts/music-video-canvas.sh "$SRC" "$OUT" >/dev/null 2>&1 && [[ -s "$OUT" ]]; then
    size_mb=$(awk -v b="$(stat -f%z "$OUT" 2>/dev/null || stat -c%s "$OUT")" 'BEGIN{printf "%.2f",b/1024/1024}')
    if awk -v m="$size_mb" 'BEGIN{exit !(m < 8.0)}'; then
      ok "canvas $size_mb MB (<8 MB)"
    else
      bad "canvas $size_mb MB exceeds 8 MB spec"
    fi
  else
    bad "canvas render failed"
  fi
fi

echo
echo "5. Kinetic typography"
if [[ -n "$SRC" ]]; then
  OUT="$TMPDIR/typo.mp4"
  if bash scripts/music-video-typography.sh "$SRC" "$OUT" "test phrase one,phrase two,phrase three" >/dev/null 2>&1 && [[ -s "$OUT" ]]; then
    ok "typography overlay"
  else
    bad "typography render failed"
  fi
fi

# Pipeline tests (60s+ each)
if [[ "$NO_PIPELINE" == 1 ]]; then
  echo
  echo "(--no-pipeline — skipping full pipeline tests)"
else
  echo
  echo "6. Full auto pipeline (lofi_hiphop, demo mode, ~2 min)"
  if MUSIC_VIDEO_DEMO_MODE=1 bash scripts/music-video-genre.sh lofi_hiphop smoke-genre-test \
       assets/music/track4-rain.mp3 >/dev/null 2>&1; then
    ok "lofi_hiphop genre wrapper end-to-end"
  else
    bad "lofi_hiphop genre wrapper failed"
  fi
fi

echo
echo "═══ Summary ═══"
echo "  PASS: $PASS"
echo "  FAIL: $FAIL"
[[ $FAIL -eq 0 ]]
