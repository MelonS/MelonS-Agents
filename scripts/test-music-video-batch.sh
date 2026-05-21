#!/usr/bin/env bash
# Regression test for scripts/music-video-batch.sh dry-run logic.
# Verifies the lyric-pairing convention without spending compute on
# actual renders (that's covered by the music-video skill tests).
#
# Asserts:
#   1. --help exits 0 and prints usage
#   2. --dry-run on default glob enumerates ≥ 1 track
#   3. dry-run output shows lyric pairing (citypop-eng → citypop-eng.txt etc)
#   4. unknown flag → exit 64
#
# Usage:  ./scripts/test-music-video-batch.sh

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/music-video-batch.sh"
[[ -x "$SCRIPT" ]] || { echo "ERROR: $SCRIPT not executable"; exit 2; }

PASS=0
FAIL=0

# Test 1: --help
if out=$("$SCRIPT" --help 2>&1) && echo "$out" | grep -q "music-video-batch"; then
  echo "PASS [help]"
  PASS=$((PASS+1))
else
  echo "FAIL [help]"
  FAIL=$((FAIL+1))
fi

# Test 2: --dry-run on default pattern returns ≥1 track
if out=$("$SCRIPT" --dry-run 2>&1); then
  count=$(echo "$out" | grep -cE '^  [0-9]+/[0-9]+ ')
  if [[ "$count" -ge 1 ]]; then
    echo "PASS [dry-run-enumerate] ($count tracks)"
    PASS=$((PASS+1))
  else
    echo "FAIL [dry-run-enumerate]: 0 tracks enumerated (assets/music/vocal-*.mp3 missing?)"
    FAIL=$((FAIL+1))
  fi
else
  echo "FAIL [dry-run-enumerate]: --dry-run exited non-zero"
  FAIL=$((FAIL+1))
fi

# Test 3: lyric pairing convention (skip if no vocal-uspop-*.mp3 in repo)
if ls "$REPO_ROOT/assets/music/"vocal-uspop-*.mp3 >/dev/null 2>&1; then
  if out=$("$SCRIPT" --dry-run 2>&1); then
    if echo "$out" | grep -q "vocal-uspop.*uspop.txt"; then
      echo "PASS [lyric-pairing]"
      PASS=$((PASS+1))
    else
      echo "FAIL [lyric-pairing]: uspop track not paired with uspop.txt"
      FAIL=$((FAIL+1))
    fi
  fi
else
  echo "SKIP [lyric-pairing]: no vocal-uspop-*.mp3 in assets/music/"
fi

# Test 4: unknown flag → 64
if "$SCRIPT" --bogus-flag >/dev/null 2>&1; then
  echo "FAIL [unknown-flag]: should have exited non-zero"
  FAIL=$((FAIL+1))
else
  rc=$?
  if [[ "$rc" -eq 64 ]]; then
    echo "PASS [unknown-flag]"
    PASS=$((PASS+1))
  else
    echo "FAIL [unknown-flag]: exit $rc, expected 64"
    FAIL=$((FAIL+1))
  fi
fi

echo
echo "=== music-video-batch tests: $PASS passed, $FAIL failed ==="
[[ "$FAIL" -gt 0 ]] && exit 1
exit 0
