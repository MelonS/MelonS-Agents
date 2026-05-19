#!/usr/bin/env bash
# Smoke test for the job-hunt skill.
#
# Mirrors skills/music-video/tests/smoke.sh in shape.  Validates
# structural invariants (frontmatter, file presence, bash syntax)
# plus end-to-end pipeline behavior using the deterministic mock
# source.
#
# Usage: skills/job-hunt/tests/smoke.sh
#
# Exit codes:
#   0   — all checks pass (skill is functionally testable;
#         live sources are mock-fallback by default but the
#         pipeline produces a real digest from mock data)
#   1   — a required file is missing or invalid; or end-to-end
#         pipeline failed to produce a digest

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

PASS=0
FAIL=0
note() { echo "[smoke] $*"; }
check() {
  local label="$1"; shift
  if "$@" >/dev/null 2>&1; then
    note "✓ $label"
    PASS=$((PASS+1))
  else
    note "✗ $label"
    FAIL=$((FAIL+1))
  fi
}

# ----- 1. Required files present -----
check "SKILL.md exists"                          test -f "$SKILL_DIR/SKILL.md"
check "scripts/run.sh exists + executable"       test -x "$SKILL_DIR/scripts/run.sh"
check "scripts/digest.sh exists + executable"    test -x "$SKILL_DIR/scripts/digest.sh"
check "scripts/apply-assist.sh exists"           test -f "$SKILL_DIR/scripts/apply-assist.sh"
check "config/filters.example.yaml exists"       test -f "$SKILL_DIR/config/filters.example.yaml"
check "sources/README.md exists"                 test -f "$SKILL_DIR/sources/README.md"
check "sources/_mock.sh exists"                  test -f "$SKILL_DIR/sources/_mock.sh"
check "sources/kr-wanted.sh exists"              test -f "$SKILL_DIR/sources/kr-wanted.sh"
check "sources/kr-programmers.sh exists"         test -f "$SKILL_DIR/sources/kr-programmers.sh"
check "sources/kr-jobkorea.sh exists"            test -f "$SKILL_DIR/sources/kr-jobkorea.sh"
check "sources/kr-saramin.sh exists"             test -f "$SKILL_DIR/sources/kr-saramin.sh"

# ----- 2. SKILL.md frontmatter sanity -----
check "SKILL.md has 'name:' frontmatter"         grep -qE '^name: job-hunt$' "$SKILL_DIR/SKILL.md"
check "SKILL.md has 'description:' frontmatter"  grep -qE '^description:' "$SKILL_DIR/SKILL.md"
check "SKILL.md has 'license:' frontmatter"      grep -qE '^license:' "$SKILL_DIR/SKILL.md"

# ----- 3. Bash syntax (every shipped .sh) -----
for f in \
  "$SKILL_DIR/scripts/run.sh" \
  "$SKILL_DIR/scripts/digest.sh" \
  "$SKILL_DIR/scripts/apply-assist.sh" \
  "$SKILL_DIR/sources/_mock.sh" \
  "$SKILL_DIR/sources/kr-wanted.sh" \
  "$SKILL_DIR/sources/kr-programmers.sh" \
  "$SKILL_DIR/sources/kr-jobkorea.sh" \
  "$SKILL_DIR/sources/kr-saramin.sh"
do
  check "$(basename "$f") bash syntax OK" bash -n "$f"
done

# ----- 4. YAML parser available (yq / python3+pyyaml / ruby) -----
if command -v yq >/dev/null 2>&1; then
  check "filters.example.yaml parses with yq" yq -e '.' "$SKILL_DIR/config/filters.example.yaml"
elif command -v python3 >/dev/null 2>&1 && python3 -c "import yaml" >/dev/null 2>&1; then
  check "filters.example.yaml parses with python yaml" python3 -c "import yaml; yaml.safe_load(open('$SKILL_DIR/config/filters.example.yaml'))"
elif command -v ruby >/dev/null 2>&1; then
  check "filters.example.yaml parses with ruby" ruby -ryaml -e "YAML.load_file('$SKILL_DIR/config/filters.example.yaml')"
else
  note "(skip) yaml parser check — no yq / python3+pyyaml / ruby on host"
fi

# ----- 5. End-to-end pipeline (mock source) -----
#    Run the orchestrator in dry-run mode with the deterministic
#    mock source; verify it writes a digest.md file containing
#    the expected sections.
DIGEST_PATH=""
if DIGEST_PATH=$("$SKILL_DIR/scripts/run.sh" --sources=_mock --dry-run --quiet 2>/dev/null); then
  check "orchestrator produced digest file"  test -f "$DIGEST_PATH"
  check "digest.md has '# Job-hunt digest' header" grep -qE '^# Job-hunt digest' "$DIGEST_PATH"
  check "digest.md has 'All postings' section"     grep -qE '^## All postings' "$DIGEST_PATH"
  check "index.json sibling exists"          test -f "$(dirname "$DIGEST_PATH")/index.json"
  check "raw/_mock.json sibling exists"      test -f "$(dirname "$DIGEST_PATH")/raw/_mock.json"
  # 5 of 8 mock postings survive include/exclude filter + 1 dedupe.
  EXPECTED=5
  ACTUAL=$(jq -r '.postings_total' "$(dirname "$DIGEST_PATH")/index.json" 2>/dev/null || echo 0)
  check "filtered+deduped count = $EXPECTED (got $ACTUAL)" test "$ACTUAL" = "$EXPECTED"
else
  note "✗ orchestrator failed end-to-end"
  FAIL=$((FAIL+1))
fi

# ----- 6. Five-source aggregation -----
#    All five sources (mock-fallback for kr-* + _mock) should
#    aggregate cleanly and dedupe URLs across them.
if FIVE_PATH=$("$SKILL_DIR/scripts/run.sh" --sources=_mock,kr-wanted,kr-programmers,kr-jobkorea,kr-saramin --dry-run --quiet 2>/dev/null); then
  F_INDEX="$(dirname "$FIVE_PATH")/index.json"
  check "five-source run produced index.json" test -f "$F_INDEX"
  FIVE_TOTAL=$(jq -r '.postings_total' "$F_INDEX" 2>/dev/null || echo 0)
  # _mock filters to 5 + kr-wanted 3 + kr-programmers 2 + kr-jobkorea 2 + kr-saramin 2 = 14
  check "five-source aggregated count = 14 (got $FIVE_TOTAL)" test "$FIVE_TOTAL" = "14"
  SRC_COUNT=$(jq -r '.sources | length' "$F_INDEX" 2>/dev/null || echo 0)
  check "five-source sources list len = 5 (got $SRC_COUNT)" test "$SRC_COUNT" = "5"
else
  note "✗ five-source aggregation failed"
  FAIL=$((FAIL+1))
fi

note ""
note "Result: $PASS pass, $FAIL fail"

if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi

# All checks pass: the skill is functionally testable end-to-end
# against mock data.  Live HTTP integration is per-plugin gated.
exit 0
