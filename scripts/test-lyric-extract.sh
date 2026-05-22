#!/usr/bin/env bash
# Regression test for scripts/lyric-extract.sh.  Asserts:
#   1. --help exits 0 (best-effort: the script doesn't have a formal
#      --help block, so this is a no-arg usage exit-64 instead)
#   2. Running on a real vocal track produces ≥ 3 non-comment lines
#   3. ♪ markers + parenthetical scene notes are stripped from output
#   4. Output file's first two lines are the auto-extraction header
#      comments
#
# Uses /Users/melons/ai/assets/music/vocal-dreampop-blue-hours.mp3
# as the test fixture since it's known-present and English (matching
# whisper's default).  Skips silently if the fixture is missing.

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/lyric-extract.sh"
FIXTURE="$REPO_ROOT/assets/music/vocal-dreampop-blue-hours.mp3"

[[ -x "$SCRIPT" ]] || { echo "ERROR: $SCRIPT not executable"; exit 2; }

if [[ ! -f "$FIXTURE" ]]; then
  echo "SKIP: $FIXTURE not present; lyric-extract tests need a vocal track"
  exit 0
fi

SBX=$(mktemp -d -t test-lyric-extract.XXXX)
trap "rm -rf '$SBX'" EXIT

PASS=0
FAIL=0

# Test 1: no-arg usage → exit 64
if "$SCRIPT" >/dev/null 2>&1; then
  echo "FAIL [no-args]: expected non-zero, got 0"
  FAIL=$((FAIL+1))
else
  rc=$?
  if [[ "$rc" -eq 64 ]]; then
    echo "PASS [no-args]: exit 64"
    PASS=$((PASS+1))
  else
    echo "FAIL [no-args]: exit $rc, expected 64"
    FAIL=$((FAIL+1))
  fi
fi

# Test 2: real extraction produces non-empty output
OUT="$SBX/extracted.txt"
if "$SCRIPT" "$FIXTURE" "$OUT" --lang=en >/dev/null 2>&1; then
  if [[ -s "$OUT" ]]; then
    nlines=$(grep -cv '^#' "$OUT" 2>/dev/null || echo 0)
    if [[ "$nlines" -ge 3 ]]; then
      echo "PASS [extract]: $nlines lyric lines"
      PASS=$((PASS+1))
    else
      echo "FAIL [extract]: only $nlines non-comment lines (expected ≥3)"
      FAIL=$((FAIL+1))
    fi
  else
    echo "FAIL [extract]: output file empty"
    FAIL=$((FAIL+1))
  fi
else
  echo "FAIL [extract]: script exited non-zero"
  FAIL=$((FAIL+1))
fi

# Test 3: ♪ + parenthetical strips applied (no surviving ♪ or (upbeat music))
if [[ -s "$OUT" ]]; then
  if grep -qE '^♪|♪$' "$OUT" 2>/dev/null; then
    echo "FAIL [marker-strip]: ♪ found in output"
    FAIL=$((FAIL+1))
  elif grep -qE '^\([^)]+\)$' "$OUT" 2>/dev/null; then
    echo "FAIL [marker-strip]: parenthetical-only line found"
    FAIL=$((FAIL+1))
  else
    echo "PASS [marker-strip]: no ♪ or (note) survivors"
    PASS=$((PASS+1))
  fi
fi

# Test 4: header comments present
if head -1 "$OUT" 2>/dev/null | grep -q "Auto-extracted"; then
  echo "PASS [header]"
  PASS=$((PASS+1))
else
  echo "FAIL [header]: expected '# Auto-extracted...' on line 1"
  FAIL=$((FAIL+1))
fi

echo
echo "=== lyric-extract tests: $PASS passed, $FAIL failed ==="
[[ "$FAIL" -gt 0 ]] && exit 1
exit 0
