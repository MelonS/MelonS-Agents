#!/usr/bin/env bash
# test-log-decision.sh — idempotency + correctness test for
# scripts/log-decision.sh.
#
# Per [[idempotency-test-first]] memory: append-only state-modifying
# scripts ship with a regression test in the same window as the
# script.  log-decision.sh's date-header insertion is the riskiest
# path — repeated same-day calls must nest under ONE header, not
# create a new one each call.
#
# Usage: scripts/test-log-decision.sh
# Exit: 0 on all PASS, 1 on any fail.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SANDBOX="$(mktemp -d -t log-decision-test-XXXX)"
trap 'rm -rf "$SANDBOX"' EXIT

# log-decision.sh writes to "$REPO_ROOT/docs/autonomous-decisions.md".
# To sandbox, run it from a fake repo containing a minimal copy of
# the file + the script.
mkdir -p "$SANDBOX/docs" "$SANDBOX/scripts"
cp "$REPO_ROOT/scripts/log-decision.sh" "$SANDBOX/scripts/"

# Minimal autonomous-decisions.md fixture — just the boilerplate
# header that log-decision.sh expects.
cat > "$SANDBOX/docs/autonomous-decisions.md" <<'INIT'
# Autonomous decisions log

(boilerplate)

## How to interpret this log

(boilerplate)
INIT

pass=0
fail=0
say_pass() { echo "  ✓ $1"; pass=$((pass + 1)); }
say_fail() { echo "  ✗ $1"; fail=$((fail + 1)); }

call_log() {
  local time="$1"
  local msg="$2"
  ( cd "$SANDBOX" && bash scripts/log-decision.sh --time "$time" "$msg" >/dev/null )
}

today=$(TZ=Asia/Seoul date +%Y-%m-%d)

# Three same-day calls.
call_log 10:00 "first decision"
call_log 11:00 "second decision"
call_log 12:00 "third decision"

# Assertion 1: exactly one date header for today.
n_headers=$(grep -c "^## ${today}" "$SANDBOX/docs/autonomous-decisions.md" || true)
[[ "$n_headers" == "1" ]] \
  && say_pass "exactly 1 date header for today (got $n_headers)" \
  || say_fail "expected 1 date header, got $n_headers"

# Assertion 2: all three entries present.
all_present=1
for kw in "first decision" "second decision" "third decision"; do
  grep -q "$kw" "$SANDBOX/docs/autonomous-decisions.md" || all_present=0
done
[[ "$all_present" == "1" ]] \
  && say_pass "all 3 decision entries present" \
  || say_fail "one or more decision entries missing"

# Assertion 3: newest-first ordering — third (12:00) appears before
# first (10:00) within today's section.
first_line=$(grep -n "first decision" "$SANDBOX/docs/autonomous-decisions.md" | cut -d: -f1)
third_line=$(grep -n "third decision" "$SANDBOX/docs/autonomous-decisions.md" | cut -d: -f1)
[[ -n "$first_line" && -n "$third_line" ]] && {
  [[ "$third_line" -lt "$first_line" ]] \
    && say_pass "newest-first ordering (third on line $third_line, first on line $first_line)" \
    || say_fail "third (line $third_line) should be before first (line $first_line)"
}

# Assertion 4: pre-existing boilerplate preserved.
grep -q "How to interpret this log" "$SANDBOX/docs/autonomous-decisions.md" \
  && say_pass "pre-existing boilerplate preserved" \
  || say_fail "boilerplate stripped during insertion"

# Assertion 5: duplicate-call protection — calling with identical
# args should NOT create a duplicate bullet (the script doesn't
# de-dupe today; this asserts current behavior so a future de-dupe
# can be detected as a behavior change).  Document the current
# count after one more identical call.
before_count=$(grep -c "^- " "$SANDBOX/docs/autonomous-decisions.md" || true)
call_log 12:00 "third decision"
after_count=$(grep -c "^- " "$SANDBOX/docs/autonomous-decisions.md" || true)
# Current behavior: no dedup, count grows by 1.  If you add dedup,
# update this expected_delta to 0.
expected_delta=1
actual_delta=$((after_count - before_count))
[[ "$actual_delta" == "$expected_delta" ]] \
  && say_pass "no dedup behavior (delta=$actual_delta as expected)" \
  || say_fail "dedup behavior changed (delta=$actual_delta, expected $expected_delta)"

echo
echo "[test] result: $pass passed, $fail failed"
exit $(( fail > 0 ? 1 : 0 ))
