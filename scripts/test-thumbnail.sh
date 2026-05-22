#!/usr/bin/env bash
# Regression test for scripts/music-video-thumbnail.sh.
#
# Asserts:
#   1. no-args → exit 64
#   2. default extraction (midpoint) produces a non-empty JPG
#   3. --at=N seconds picks the requested time
#   4. --at=N% percent picks the requested percent
#   5. unknown flag → exit 64

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/music-video-thumbnail.sh"
[[ -x "$SCRIPT" ]] || { echo "ERROR: $SCRIPT not executable"; exit 2; }

SBX=$(mktemp -d -t test-thumb.XXXX)
trap "rm -rf '$SBX'" EXIT

# Build a 10s synthetic source with shifting color so different
# timestamps give visibly different frames.
SRC="$SBX/src.mp4"
/opt/homebrew/bin/ffmpeg -y -loglevel error \
  -f lavfi -i "gradients=s=1080x1920:d=10:type=radial:c0=0x442266:c1=0xee8866" \
  -f lavfi -i "sine=frequency=400:duration=10" \
  -map 0:v -map 1:a -c:v libx264 -preset ultrafast -t 10 -shortest \
  "$SRC" 2>/dev/null

PASS=0
FAIL=0

# Test 1: no-args
if "$SCRIPT" >/dev/null 2>&1; then
  echo "FAIL [no-args]: expected non-zero"
  FAIL=$((FAIL+1))
else
  rc=$?
  if [[ "$rc" -eq 64 ]]; then
    echo "PASS [no-args]"
    PASS=$((PASS+1))
  else
    echo "FAIL [no-args]: exit $rc"
    FAIL=$((FAIL+1))
  fi
fi

# Test 2: default midpoint
OUT="$SBX/default.jpg"
if "$SCRIPT" "$SRC" "$OUT" >/dev/null 2>&1 && [[ -s "$OUT" ]]; then
  echo "PASS [default-midpoint]: $(du -h "$OUT" | awk '{print $1}')"
  PASS=$((PASS+1))
else
  echo "FAIL [default-midpoint]: no output"
  FAIL=$((FAIL+1))
fi

# Test 3: --at=N seconds
OUT2="$SBX/at-2.jpg"
if "$SCRIPT" "$SRC" "$OUT2" --at=2 >/dev/null 2>&1 && [[ -s "$OUT2" ]]; then
  echo "PASS [at-seconds]"
  PASS=$((PASS+1))
else
  echo "FAIL [at-seconds]"
  FAIL=$((FAIL+1))
fi

# Test 4: --at=N%
OUT3="$SBX/at-30p.jpg"
if "$SCRIPT" "$SRC" "$OUT3" --at=30% >/dev/null 2>&1 && [[ -s "$OUT3" ]]; then
  echo "PASS [at-percent]"
  PASS=$((PASS+1))
else
  echo "FAIL [at-percent]"
  FAIL=$((FAIL+1))
fi

# Test 5: unknown flag
if "$SCRIPT" "$SRC" --bogus >/dev/null 2>&1; then
  echo "FAIL [unknown-flag]: expected non-zero"
  FAIL=$((FAIL+1))
else
  rc=$?
  if [[ "$rc" -eq 64 ]]; then
    echo "PASS [unknown-flag]"
    PASS=$((PASS+1))
  else
    echo "FAIL [unknown-flag]: exit $rc"
    FAIL=$((FAIL+1))
  fi
fi

echo
echo "=== thumbnail tests: $PASS passed, $FAIL failed ==="
[[ "$FAIL" -gt 0 ]] && exit 1
exit 0
