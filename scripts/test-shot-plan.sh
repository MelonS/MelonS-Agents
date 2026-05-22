#!/usr/bin/env bash
# Regression test for scripts/shot-plan.sh.
#
# Asserts:
#   1. no-keywords → exit 64
#   2. valid args produce JSON to stdout with expected schema
#   3. --out PATH writes JSON file
#   4. cut_density mapping: kpop_ballad → sparse density label
#   5. cut_density mapping: kpop_dance → moderate or dense
#   6. motif slot fires at i % 3 == 0 (i > 0)
#   7. hook_position=1 for segments with t_end ≤ 5.0

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/shot-plan.sh"
[[ -x "$SCRIPT" ]] || { echo "ERROR: $SCRIPT not executable"; exit 2; }

SBX=$(mktemp -d -t test-shot-plan.XXXX)
trap "rm -rf '$SBX'" EXIT

PASS=0
FAIL=0

# Test 1: no-keywords → 64
if "$SCRIPT" --genre kpop_ballad --duration 60 >/dev/null 2>&1; then
  echo "FAIL [no-keywords]"
  FAIL=$((FAIL+1))
else
  rc=$?
  if [[ "$rc" -eq 64 ]]; then echo "PASS [no-keywords]"; PASS=$((PASS+1))
  else echo "FAIL [no-keywords]: exit $rc"; FAIL=$((FAIL+1)); fi
fi

# Test 2: stdout JSON has expected schema
if json=$("$SCRIPT" --keywords "a,b,c" --genre kpop_ballad --duration 30 2>/dev/null); then
  if echo "$json" | jq -e '.version, .genre, .segment_count, .segments' >/dev/null 2>&1; then
    echo "PASS [schema]"
    PASS=$((PASS+1))
  else
    echo "FAIL [schema]: missing required fields"
    FAIL=$((FAIL+1))
  fi
else
  echo "FAIL [schema]: script exited non-zero"
  FAIL=$((FAIL+1))
fi

# Test 3: --out PATH writes file
OUT="$SBX/plan.json"
"$SCRIPT" --keywords "a,b,c" --genre kpop_ballad --duration 30 --out "$OUT" >/dev/null 2>&1
if [[ -s "$OUT" ]]; then
  echo "PASS [out-path]"
  PASS=$((PASS+1))
else
  echo "FAIL [out-path]: file not created"
  FAIL=$((FAIL+1))
fi

# Test 4: kpop_ballad → sparse density
density=$(jq -r '.cut_density' "$OUT" 2>/dev/null)
if [[ "$density" == "sparse" ]]; then
  echo "PASS [density-sparse]"
  PASS=$((PASS+1))
else
  echo "FAIL [density-sparse]: got '$density', expected 'sparse'"
  FAIL=$((FAIL+1))
fi

# Test 5: kpop_dance → moderate density (phrase_beats=8)
"$SCRIPT" --keywords "a,b,c" --genre kpop_dance --duration 30 --out "$SBX/dance.json" >/dev/null 2>&1
ddens=$(jq -r '.cut_density' "$SBX/dance.json" 2>/dev/null)
if [[ "$ddens" == "moderate" ]]; then
  echo "PASS [density-moderate]"
  PASS=$((PASS+1))
else
  echo "FAIL [density-moderate]: got '$ddens', expected 'moderate'"
  FAIL=$((FAIL+1))
fi

# Test 6: motif_slot at i % 3 == 0
motif3=$(jq -r '.segments[3].motif_slot' "$OUT" 2>/dev/null)
if [[ "$motif3" == "1" ]]; then
  echo "PASS [motif-slot]"
  PASS=$((PASS+1))
else
  echo "FAIL [motif-slot]: segment[3].motif_slot = $motif3, expected 1"
  FAIL=$((FAIL+1))
fi

# Test 7: hook_position for first segment (typically t_end ≤ 5.0)
hook0=$(jq -r '.segments[0].hook_position' "$OUT" 2>/dev/null)
if [[ "$hook0" == "1" ]]; then
  echo "PASS [hook-position]"
  PASS=$((PASS+1))
else
  echo "FAIL [hook-position]: segment[0].hook_position = $hook0"
  FAIL=$((FAIL+1))
fi

echo
echo "=== shot-plan tests: $PASS passed, $FAIL failed ==="
[[ "$FAIL" -gt 0 ]] && exit 1
exit 0
