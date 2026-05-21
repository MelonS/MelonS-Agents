#!/usr/bin/env bash
# Regression test for scripts/first-touch.sh — exercises the --check
# mode and verifies the expected output structure.  Doesn't run the
# full render (~3-5 min); that's covered by scripts/test-demo-mode.sh.
#
# Asserts:
#   1. --help exits 0 and prints the usage block
#   2. --check exits 0 on a tooled machine + reports all required bins
#   3. unknown arg exits 64 with an error
#
# Usage:
#   ./scripts/test-first-touch.sh

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/first-touch.sh"
[[ -x "$SCRIPT" ]] || { echo "ERROR: $SCRIPT not executable"; exit 2; }

PASS=0
FAIL=0
SBX=$(mktemp -d -t test-first-touch.XXXX)
trap "rm -rf '$SBX'" EXIT

# Test 1: --help
if out=$("$SCRIPT" --help 2>&1) && echo "$out" | grep -q "first-run wizard"; then
  echo "PASS [help]"
  PASS=$((PASS+1))
else
  echo "FAIL [help]: --help did not print usage banner"
  FAIL=$((FAIL+1))
fi

# Test 2: --check exits 0 on a tooled machine + reports platform
if out=$("$SCRIPT" --check 2>&1); then
  if echo "$out" | grep -q "platform:" && echo "$out" | grep -q "prereq check complete"; then
    echo "PASS [check-mode]"
    PASS=$((PASS+1))
  else
    echo "FAIL [check-mode]: --check ran but didn't report platform or completion"
    FAIL=$((FAIL+1))
  fi
else
  echo "FAIL [check-mode]: --check exited non-zero (missing required tools?)"
  FAIL=$((FAIL+1))
fi

# Test 3: unknown arg → exit 64
if "$SCRIPT" --bogus-flag >/dev/null 2>&1; then
  echo "FAIL [unknown-arg]: --bogus-flag should have exited non-zero"
  FAIL=$((FAIL+1))
else
  rc=$?
  if [[ "$rc" -eq 64 ]]; then
    echo "PASS [unknown-arg]: exit 64 as expected"
    PASS=$((PASS+1))
  else
    echo "FAIL [unknown-arg]: exit $rc, expected 64"
    FAIL=$((FAIL+1))
  fi
fi

echo
echo "=== first-touch tests: $PASS passed, $FAIL failed ==="
[[ "$FAIL" -gt 0 ]] && exit 1
exit 0
