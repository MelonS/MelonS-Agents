#!/usr/bin/env bash
# Lightweight smoke test for the job-hunt skill — scaffold version.
#
# Mirrors skills/music-video/tests/smoke.sh in shape.  At scaffold
# stage, this test verifies only the structural invariants —
# nothing live is checked because nothing live is implemented.
#
# When sources are added, this smoke test grows to verify:
#   - Each source plugin loads (bash -n syntax check).
#   - Each source's fetch_postings() exists and is callable.
#   - The orchestrator parses the example filter file without
#     erroring.
#   - At least one source's mock-mode (offline) returns valid JSON.
#
# Usage: skills/job-hunt/tests/smoke.sh
#
# Exit codes:
#   0   — all structural checks pass
#   1   — a required file is missing or invalid
#   10  — scaffold marker matches and no live source is expected
#         (current state — this is the normal exit pre-implementation)

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

# 1. Required scaffold files present.
check "SKILL.md exists" test -f "$SKILL_DIR/SKILL.md"
check "scripts/run.sh exists" test -x "$SKILL_DIR/scripts/run.sh"
check "config/filters.example.yaml exists" test -f "$SKILL_DIR/config/filters.example.yaml"
check "sources/README.md exists" test -f "$SKILL_DIR/sources/README.md"

# 2. SKILL.md has required agentskills.io frontmatter keys.
check "SKILL.md has 'name:' frontmatter" grep -qE '^name: job-hunt$' "$SKILL_DIR/SKILL.md"
check "SKILL.md has 'description:' frontmatter" grep -qE '^description:' "$SKILL_DIR/SKILL.md"
check "SKILL.md has 'license:' frontmatter" grep -qE '^license:' "$SKILL_DIR/SKILL.md"

# 3. run.sh bash syntax valid.
check "scripts/run.sh bash syntax OK" bash -n "$SKILL_DIR/scripts/run.sh"

# 4. Filter schema YAML parses (if a parser is available; skip otherwise).
#    Prefer yq → python3+pyyaml → ruby+yaml.  If none, this check is
#    skipped, not failed — yaml-module availability is a host setup
#    concern, not a skill defect.
if command -v yq >/dev/null 2>&1; then
  check "filters.example.yaml parses with yq" yq -e '.' "$SKILL_DIR/config/filters.example.yaml"
elif command -v python3 >/dev/null 2>&1 && python3 -c "import yaml" >/dev/null 2>&1; then
  check "filters.example.yaml parses with python yaml" python3 -c "import yaml; yaml.safe_load(open('$SKILL_DIR/config/filters.example.yaml'))"
elif command -v ruby >/dev/null 2>&1; then
  check "filters.example.yaml parses with ruby" ruby -ryaml -e "YAML.load_file('$SKILL_DIR/config/filters.example.yaml')"
else
  note "(skip) yaml parser check — no yq / python3+pyyaml / ruby on host"
fi

# 5. Scaffold marker — run.sh should exit 10 (not-yet-implemented).
EXIT_CODE=0
"$SKILL_DIR/scripts/run.sh" >/dev/null 2>&1 || EXIT_CODE=$?
if [[ "$EXIT_CODE" == "10" ]]; then
  note "✓ run.sh returns scaffold marker exit 10 (expected pre-impl)"
  PASS=$((PASS+1))
else
  note "✗ run.sh returned $EXIT_CODE; expected 10 at scaffold stage"
  FAIL=$((FAIL+1))
fi

note ""
note "Result: $PASS pass, $FAIL fail"

if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi

# At scaffold stage, exit 10 to clearly signal "tests pass but
# no live implementation yet" — distinct from exit 0 which would
# imply the skill is functionally complete.
exit 10
