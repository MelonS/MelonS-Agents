#!/usr/bin/env bash
# Regression test for scripts/music-video-validate.sh — verifies the
# combined-gate logic exits 0/1/2 according to the worst sub-gate.
#
# Sandbox under /tmp/test-music-video-validate.<pid>/; constructs
# synthetic mission directories.

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/music-video-validate.sh"
[[ -x "$SCRIPT" ]] || { echo "ERROR: $SCRIPT not executable"; exit 2; }

SBX=$(mktemp -d -t test-mv-validate.XXXX)
trap "rm -rf '$SBX'" EXIT

PASS=0
FAIL=0

# Build a synthetic mission with a real (tiny) mp4 + given clip keywords.
make_mission() {
  local dir="$1"; shift
  mkdir -p "$dir/outputs" "$dir/resources/clips"
  /opt/homebrew/bin/ffmpeg -y -loglevel error \
    -f lavfi -i color=c=black:s=320x568:d=2 \
    -f lavfi -i sine=frequency=440:duration=2 \
    -map 0:v -map 1:a -c:v libx264 -preset ultrafast -t 2 -shortest \
    "$dir/outputs/short.mp4" 2>/dev/null
  for kw in "$@"; do
    touch "$dir/resources/clips/raw-${kw}.mp4"
  done
}

# Test 1: KR genre + all-matching keywords → exit 0
M1="$SBX/m1"
make_mission "$M1" "korean_cafe" "seoul_street" "asian_man" "rainy_seoul_window"
if "$SCRIPT" "$M1" --genre=kpop_ballad >/dev/null 2>&1; then
  echo "PASS [kr-all-match]: exit 0"
  PASS=$((PASS+1))
else
  rc=$?
  echo "FAIL [kr-all-match]: exit $rc, expected 0"
  FAIL=$((FAIL+1))
fi

# Test 2: KR genre + zero-matching → exit 2 (FAIL)
M2="$SBX/m2"
make_mission "$M2" "warm_candle" "soft_pillow" "cozy_blanket" "indoor_plant"
if "$SCRIPT" "$M2" --genre=kpop_ballad >/dev/null 2>&1; then
  echo "FAIL [kr-zero-match]: expected exit 2, got 0"
  FAIL=$((FAIL+1))
else
  rc=$?
  if [[ "$rc" -eq 2 ]]; then
    echo "PASS [kr-zero-match]: exit 2"
    PASS=$((PASS+1))
  else
    echo "FAIL [kr-zero-match]: exit $rc, expected 2"
    FAIL=$((FAIL+1))
  fi
fi

# Test 3: missing genre → WARN exit 1
M3="$SBX/m3"
make_mission "$M3" "generic_clip_a" "generic_clip_b"
if "$SCRIPT" "$M3" >/dev/null 2>&1; then
  echo "FAIL [no-genre]: expected exit 1 (WARN), got 0"
  FAIL=$((FAIL+1))
else
  rc=$?
  if [[ "$rc" -eq 1 ]]; then
    echo "PASS [no-genre]: exit 1 (WARN)"
    PASS=$((PASS+1))
  else
    echo "FAIL [no-genre]: exit $rc, expected 1"
    FAIL=$((FAIL+1))
  fi
fi

# Test 4: missing mp4 file → exit 2 (FAIL via file-integrity)
M4="$SBX/m4"
mkdir -p "$M4/outputs" "$M4/resources/clips"  # no short.mp4
touch "$M4/resources/clips/raw-korean_cafe.mp4"
if "$SCRIPT" "$M4" --genre=kpop_ballad >/dev/null 2>&1; then
  echo "FAIL [missing-mp4]: expected exit 2"
  FAIL=$((FAIL+1))
else
  rc=$?
  if [[ "$rc" -eq 2 ]]; then
    echo "PASS [missing-mp4]: exit 2"
    PASS=$((PASS+1))
  else
    echo "FAIL [missing-mp4]: exit $rc, expected 2"
    FAIL=$((FAIL+1))
  fi
fi

echo
echo "=== validate tests: $PASS passed, $FAIL failed ==="
[[ "$FAIL" -gt 0 ]] && exit 1
exit 0
