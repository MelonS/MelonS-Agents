#!/usr/bin/env bash
# test-all.sh — run every fast smoke / regression test under scripts/.
#
# Excludes (slow / external) by default:
#   test-demo-mode.sh         (5+ min — clones the public repo)
#   test-fresh-clone.sh       (3+ min — clones the public repo)
#   test-fresh-clone-linux.sh (5+ min — spawns a Docker container)
#
# Use --all to include them.  Each test is run with output captured;
# only the pass/fail tally per test is shown by default.  --verbose
# streams full output.
#
# Usage:
#   scripts/test-all.sh                     # fast tests only
#   scripts/test-all.sh --all               # everything (slow ones included)
#   scripts/test-all.sh --verbose           # stream every test's output
#
# Exit: 0 if every test passes, 1 if any test fails.

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

INCLUDE_SLOW=0
VERBOSE=0
for arg in "$@"; do
  case "$arg" in
    --all)      INCLUDE_SLOW=1 ;;
    --verbose|-v) VERBOSE=1 ;;
    -h|--help)
      sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *) echo "unknown arg: $arg" >&2; exit 64 ;;
  esac
done

# Discover tests.
ALL_TESTS=()
while IFS= read -r f; do
  ALL_TESTS+=("$f")
done < <(ls scripts/test-*.sh 2>/dev/null | sort)

# Filter slow tests by name pattern.
SLOW_PATTERN="(test-demo-mode|test-fresh-clone)"
RUN=()
SKIP=()
for t in "${ALL_TESTS[@]}"; do
  base=$(basename "$t" .sh)
  if [[ "$INCLUDE_SLOW" -eq 0 && "$base" =~ $SLOW_PATTERN ]]; then
    SKIP+=("$base")
  else
    RUN+=("$t")
  fi
done

echo "═══════════════════════════════════════════════════════════════"
echo " test-all"
echo "═══════════════════════════════════════════════════════════════"
echo "discovered: ${#ALL_TESTS[@]}  (run: ${#RUN[@]}, skip-slow: ${#SKIP[@]})"
[[ ${#SKIP[@]} -gt 0 ]] && echo "  skipped:  ${SKIP[*]}"
echo "  (pass --all to include slow tests)"
echo

PASS=0
FAIL=0
FAILED_TESTS=()
TOTAL_START=$(date +%s)

for t in "${RUN[@]}"; do
  name=$(basename "$t" .sh)
  start=$(date +%s)
  printf "  %-30s " "$name"
  if [[ "$VERBOSE" -eq 1 ]]; then
    echo
    bash "$t"
    rc=$?
    echo
  else
    out=$(bash "$t" 2>&1) || rc=$?
    rc="${rc:-0}"
  fi
  end=$(date +%s)
  elapsed=$(( end - start ))
  if [[ "$rc" -eq 0 ]]; then
    printf "PASS  (%ds)\n" "$elapsed"
    PASS=$((PASS+1))
  else
    printf "FAIL  (%ds, exit %d)\n" "$elapsed" "$rc"
    FAIL=$((FAIL+1))
    FAILED_TESTS+=("$name")
    if [[ "$VERBOSE" -eq 0 ]]; then
      echo "    └── (run with --verbose to see output)"
    fi
  fi
done

TOTAL_END=$(date +%s)
TOTAL_ELAPSED=$(( TOTAL_END - TOTAL_START ))

echo
echo "═══════════════════════════════════════════════════════════════"
echo " summary: $PASS passed, $FAIL failed in ${TOTAL_ELAPSED}s"
echo "═══════════════════════════════════════════════════════════════"
if [[ "$FAIL" -gt 0 ]]; then
  echo "failed:"
  for n in "${FAILED_TESTS[@]}"; do echo "  - $n"; done
  exit 1
fi
exit 0
