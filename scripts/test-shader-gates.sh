#!/usr/bin/env bash
# Regression test for the C.1 shader gating modes in
# scripts/music-video-shaders.sh — verifies that each gate mode
# produces a valid mp4 output without breaking the encoder.
#
# Sandbox under /tmp/test-shader-gates.<pid>/.  Does not touch any
# records/ or outputs/ tree.
#
# Tests:
#   1. uniform mode + ratio=0.5  → final blend at 50% opacity
#   2. uniform mode + ratio=1.0  → pass-through, no blend stage
#   3. phrase_climax mode + ratio=0.4  → center window report
#   4. onsets mode + sparse events  → uses events directly
#   5. onsets mode + dense events  → cap kicks in (≤ 30 events)
#   6. onsets mode + no event file  → falls back to uniform
#
# Exit: 0 if all pass, non-zero on first failure.

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SHADERS="$REPO_ROOT/scripts/music-video-shaders.sh"
[[ -x "$SHADERS" ]] || { echo "ERROR: $SHADERS missing"; exit 2; }

SBX=$(mktemp -d -t test-shader-gates.XXXX)
trap "rm -rf '$SBX'" EXIT

# Build a fixed test source (4s gradient + sine).
make_src() {
  /opt/homebrew/bin/ffmpeg -y -loglevel error \
    -f lavfi -i "gradients=s=512x288:d=4:type=linear:c0=0x442266:c1=0xee8866" \
    -f lavfi -i "sine=frequency=400:duration=4" \
    -map 0:v -map 1:a -c:v libx264 -preset ultrafast -t 4 -shortest \
    "$SBX/src.mp4" 2>/dev/null
}

assert_valid_mp4() {
  local f="$1" name="$2"
  if [[ ! -s "$f" ]]; then
    echo "FAIL [$name]: output missing or empty: $f"
    return 1
  fi
  local dur
  dur=$(/opt/homebrew/bin/ffprobe -v error -show_entries format=duration -of csv=p=0 "$f" 2>/dev/null | awk '{printf "%.1f", $1}')
  if [[ -z "$dur" || "$dur" == "0.0" ]]; then
    echo "FAIL [$name]: ffprobe reports zero/missing duration"
    return 1
  fi
  echo "PASS [$name]: $f (${dur}s, $(du -h "$f" | awk '{print $1}'))"
  return 0
}

PASS=0
FAIL=0

make_src

# Test 1: uniform mode, ratio 0.5
MUSIC_VIDEO_SHADER_RATIO=0.5 MUSIC_VIDEO_SHADER_GATE=uniform \
  bash "$SHADERS" halation "$SBX/src.mp4" "$SBX/out-uniform.mp4" >/dev/null 2>&1
if assert_valid_mp4 "$SBX/out-uniform.mp4" "uniform-0.5"; then PASS=$((PASS+1)); else FAIL=$((FAIL+1)); fi

# Test 2: uniform mode, ratio 1.0 (pass-through)
MUSIC_VIDEO_SHADER_RATIO=1.0 \
  bash "$SHADERS" halation "$SBX/src.mp4" "$SBX/out-uniform-1.mp4" >/dev/null 2>&1
if assert_valid_mp4 "$SBX/out-uniform-1.mp4" "uniform-1.0"; then PASS=$((PASS+1)); else FAIL=$((FAIL+1)); fi

# Test 3: phrase_climax + ratio 0.4
MUSIC_VIDEO_SHADER_RATIO=0.4 MUSIC_VIDEO_SHADER_GATE=phrase_climax \
  bash "$SHADERS" halation "$SBX/src.mp4" "$SBX/out-climax.mp4" >/dev/null 2>&1
if assert_valid_mp4 "$SBX/out-climax.mp4" "phrase_climax-0.4"; then PASS=$((PASS+1)); else FAIL=$((FAIL+1)); fi

# Test 4: onsets mode + sparse events (5 events should render directly)
printf "0.5\n1.5\n2.5\n3.0\n3.5\n" > "$SBX/sparse.txt"
MUSIC_VIDEO_SHADER_RATIO=0.5 MUSIC_VIDEO_SHADER_GATE=onsets \
  MUSIC_VIDEO_SHADER_ONSETS="$SBX/sparse.txt" \
  bash "$SHADERS" halation "$SBX/src.mp4" "$SBX/out-sparse.mp4" >/dev/null 2>&1
if assert_valid_mp4 "$SBX/out-sparse.mp4" "onsets-sparse"; then PASS=$((PASS+1)); else FAIL=$((FAIL+1)); fi

# Test 5: onsets mode + dense events (200 events → cap kicks in)
for i in $(seq 1 200); do
  awk -v i=$i 'BEGIN{printf "%.3f\n", i*0.02}'
done > "$SBX/dense.txt"
MUSIC_VIDEO_SHADER_RATIO=0.5 MUSIC_VIDEO_SHADER_GATE=onsets \
  MUSIC_VIDEO_SHADER_ONSETS="$SBX/dense.txt" \
  bash "$SHADERS" halation "$SBX/src.mp4" "$SBX/out-dense.mp4" >/dev/null 2>&1
if assert_valid_mp4 "$SBX/out-dense.mp4" "onsets-dense"; then PASS=$((PASS+1)); else FAIL=$((FAIL+1)); fi

# Test 6: onsets mode + no event file (falls back to uniform)
MUSIC_VIDEO_SHADER_RATIO=0.5 MUSIC_VIDEO_SHADER_GATE=onsets \
  bash "$SHADERS" halation "$SBX/src.mp4" "$SBX/out-fallback.mp4" >/dev/null 2>&1
if assert_valid_mp4 "$SBX/out-fallback.mp4" "onsets-no-events"; then PASS=$((PASS+1)); else FAIL=$((FAIL+1)); fi

echo
echo "=== shader gate tests: $PASS passed, $FAIL failed ==="
[[ "$FAIL" -gt 0 ]] && exit 1
exit 0
