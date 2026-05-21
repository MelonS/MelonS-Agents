#!/usr/bin/env bash
# Regression test for scripts/music-video-qa-anchor.sh — the A.3
# QA gate that scores a finished render's B-roll keyword set against
# the genre's lang_anchor.
#
# Builds synthetic mission directories with known B-roll filenames and
# verifies the gate's verdict + exit codes are correct.
#
# Asserts:
#   1. neutral anchor → PASS regardless of keywords
#   2. ko anchor + all-KR keywords → PASS, ratio 1.0
#   3. ko anchor + zero anchor-matching → FAIL exit 2
#   4. en anchor + ≥30% anchor-matching → PASS
#   5. mixed anchor + mixed-asian keywords → PASS
#   6. missing genre arg → defaults to neutral with warning
#
# Sandbox under /tmp/test-qa-anchor.<pid>/.

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/music-video-qa-anchor.sh"
[[ -x "$SCRIPT" ]] || { echo "ERROR: $SCRIPT not executable"; exit 2; }

SBX=$(mktemp -d -t test-qa-anchor.XXXX)
trap "rm -rf '$SBX'" EXIT

PASS=0
FAIL=0

# Helper: build a fake mission dir with given clip filenames.
make_mission() {
  local dir="$1"; shift
  mkdir -p "$dir/resources/clips"
  for kw in "$@"; do
    touch "$dir/resources/clips/raw-${kw}.mp4"
  done
}

# Test 1: neutral anchor (lofi_hiphop) → PASS
M1="$SBX/m1"
make_mission "$M1" "rainy_window" "vintage_turntable" "vinyl_record" "warm_lights"
if "$SCRIPT" "$M1" --genre=lofi_hiphop >/dev/null 2>&1; then
  echo "PASS [neutral]"
  PASS=$((PASS+1))
else
  echo "FAIL [neutral]: exit non-zero"
  FAIL=$((FAIL+1))
fi

# Test 2: ko anchor (kpop_ballad) + all-KR keywords → PASS ratio 1.0
M2="$SBX/m2"
make_mission "$M2" "korean_woman_cafe" "seoul_night_street" "asian_man_walking" "rainy_seoul_window"
if out=$("$SCRIPT" "$M2" --genre=kpop_ballad 2>&1) && echo "$out" | grep -q "1.00"; then
  echo "PASS [ko-all-match]"
  PASS=$((PASS+1))
else
  echo "FAIL [ko-all-match]: expected ratio 1.00"
  FAIL=$((FAIL+1))
fi

# Test 3: ko anchor + zero matching → FAIL exit 2
M3="$SBX/m3"
make_mission "$M3" "candle_flame" "warm_lights" "soft_textures" "cozy_room"
if "$SCRIPT" "$M3" --genre=kpop_ballad >/dev/null 2>&1; then
  echo "FAIL [ko-zero-match]: expected exit non-zero"
  FAIL=$((FAIL+1))
else
  rc=$?
  if [[ "$rc" -eq 2 ]]; then
    echo "PASS [ko-zero-match]: exit 2 (FAIL verdict)"
    PASS=$((PASS+1))
  else
    echo "FAIL [ko-zero-match]: exit $rc, expected 2"
    FAIL=$((FAIL+1))
  fi
fi

# Test 4: en anchor (uspop) + matching → PASS
M4="$SBX/m4"
make_mission "$M4" "new_york_skyline" "california_beach" "manhattan_street" "polaroid_camera"
if "$SCRIPT" "$M4" --genre=uspop >/dev/null 2>&1; then
  echo "PASS [en-match]"
  PASS=$((PASS+1))
else
  echo "FAIL [en-match]"
  FAIL=$((FAIL+1))
fi

# Test 5: mixed (citypop) + asian keywords → PASS
M5="$SBX/m5"
make_mission "$M5" "tokyo_neon_street" "shibuya_crossing" "warm_city_evening" "korean_cafe"
if "$SCRIPT" "$M5" --genre=citypop >/dev/null 2>&1; then
  echo "PASS [mixed-asian]"
  PASS=$((PASS+1))
else
  echo "FAIL [mixed-asian]"
  FAIL=$((FAIL+1))
fi

# Test 6: missing genre → defaults to neutral
M6="$SBX/m6"
make_mission "$M6" "anything_at_all" "another_clip"
if out=$("$SCRIPT" "$M6" 2>&1) && echo "$out" | grep -q "verdict:  PASS"; then
  echo "PASS [missing-genre]"
  PASS=$((PASS+1))
else
  echo "FAIL [missing-genre]: expected PASS verdict with neutral fallback"
  FAIL=$((FAIL+1))
fi

echo
echo "=== qa-anchor tests: $PASS passed, $FAIL failed ==="
[[ "$FAIL" -gt 0 ]] && exit 1
exit 0
