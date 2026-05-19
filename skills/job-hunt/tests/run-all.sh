#!/usr/bin/env bash
# Aggregator: run all test suites for the job-hunt skill in sequence.
#
# Useful as a single command for pre-merge gate validation:
#   skills/job-hunt/tests/run-all.sh
#
# Returns 0 only if all suites pass.  Otherwise returns the first
# non-zero exit code.
#
# Suites:
#   - tests/smoke.sh         — structural + happy-path end-to-end (32 checks)
#   - tests/edge-cases.sh    — failure-mode + edge-input (20 checks)

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

OVERALL=0
SUITE_RESULTS=()

run_suite() {
  local name="$1" path="$2"
  echo ""
  echo "==================== $name ===================="
  if [[ ! -x "$path" ]]; then
    echo "[$name] FAIL — script not executable: $path"
    SUITE_RESULTS+=("$name: FAIL (not executable)")
    OVERALL=1
    return
  fi
  if "$path"; then
    SUITE_RESULTS+=("$name: PASS")
  else
    local rc=$?
    SUITE_RESULTS+=("$name: FAIL ($rc)")
    [[ "$OVERALL" == "0" ]] && OVERALL=$rc
  fi
}

run_suite "smoke"      "$SKILL_DIR/tests/smoke.sh"
run_suite "edge-cases" "$SKILL_DIR/tests/edge-cases.sh"

echo ""
echo "==================== summary ===================="
for r in "${SUITE_RESULTS[@]}"; do
  echo "  $r"
done

if [[ "$OVERALL" == "0" ]]; then
  echo ""
  echo "All suites pass."
fi

exit "$OVERALL"
