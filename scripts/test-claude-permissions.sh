#!/usr/bin/env bash
# Edge-case smoke for scripts/install-claude-permissions.sh.
#
# Why this exists: the production version of the script had a jq
# string-concatenation bug (using `+` instead of `\(…)`) that
# silently wrote an empty file to the user's settings.  The bug
# escaped review because the validation step (`jq empty`) treats
# an empty file as valid input.  This script codifies the edge
# cases that catch that class of failure, plus the three normal-path
# invariants (idempotency, preservation of pre-existing user data,
# graceful no-op when Claude Code isn't installed).
#
# Usage:
#   scripts/test-claude-permissions.sh
#
# Runs in an isolated $HOME under /tmp; never touches the operator's
# real ~/.claude/.  Exits 0 on all-PASS, 1 on any FAIL.

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

if ! command -v jq >/dev/null 2>&1; then
  echo "❌ jq required" >&2
  exit 1
fi

SANDBOX="$(mktemp -d -t claude-perm-test-XXXXXX)"
trap 'rm -rf "$SANDBOX"' EXIT

PASS=0
FAIL=0
FAILED=()

check() {
  local name="$1"; shift
  if "$@"; then
    PASS=$((PASS + 1))
    printf "  [PASS] %s\n" "$name"
  else
    FAIL=$((FAIL + 1))
    FAILED+=("$name")
    printf "  [FAIL] %s\n" "$name" >&2
  fi
}

reset_home() {
  rm -rf "$SANDBOX/.claude"
  mkdir -p "$SANDBOX/.claude"
}

run_installer() {
  HOME="$SANDBOX" ./scripts/install-claude-permissions.sh --yes >/dev/null 2>&1
}

echo "=== install-claude-permissions edge-case smoke ==="
echo "  sandbox HOME: $SANDBOX"

# Test 1 — empty {} input → installs all project entries
echo "[1/5] empty user settings → install"
reset_home
echo '{}' > "$SANDBOX/.claude/settings.json"
run_installer
project_count=$(jq '.permissions.allow | length' "$REPO_ROOT/.claude/settings.json")
user_count=$(jq '.permissions.allow | length' "$SANDBOX/.claude/settings.json" 2>/dev/null || echo 0)
check "user file is non-empty after install" test "$(stat -f '%z' "$SANDBOX/.claude/settings.json" 2>/dev/null || stat -c '%s' "$SANDBOX/.claude/settings.json")" -gt 100
check "user allow list count == project count" test "$user_count" -eq "$project_count"
check "_notes.melons_agents.source recorded" \
  bash -c "jq -er '._notes.melons_agents.source' '$SANDBOX/.claude/settings.json' >/dev/null"

# Test 2 — idempotency (second run adds 0)
echo "[2/5] idempotency (second run adds 0)"
run_installer
user_count_2=$(jq '.permissions.allow | length' "$SANDBOX/.claude/settings.json")
check "second-run count unchanged" test "$user_count_2" -eq "$user_count"

# Test 3 — pre-existing user entries preserved
echo "[3/5] user data preservation"
reset_home
cat > "$SANDBOX/.claude/settings.json" <<JSON
{
  "permissions": {
    "allow": ["Bash(my-custom-tool *)"],
    "deny":  ["Bash(evil-tool *)"]
  },
  "model": "opus"
}
JSON
run_installer
check "custom allow entry survives" \
  bash -c "jq -er '.permissions.allow | any(. == \"Bash(my-custom-tool *)\")' '$SANDBOX/.claude/settings.json' >/dev/null"
check "deny list survives" \
  bash -c "jq -er '.permissions.deny | any(. == \"Bash(evil-tool *)\")' '$SANDBOX/.claude/settings.json' >/dev/null"
check "model field survives" \
  bash -c "[[ \"\$(jq -r '.model' '$SANDBOX/.claude/settings.json')\" == 'opus' ]]"

# Test 4 — missing ~/.claude/ → graceful no-op (exit 0)
echo "[4/5] missing ~/.claude/ → graceful no-op"
rm -rf "$SANDBOX/.claude"
# Capture output first to avoid SIGPIPE + pipefail interaction:
# `grep -q` closes stdin on first match → upstream gets SIGPIPE (rc 141)
# → pipefail propagates that as the pipeline rc, even though grep itself
# found the match.  Capturing to a variable sidesteps the pipeline.
no_claude_out=$(HOME="$SANDBOX" ./scripts/install-claude-permissions.sh --yes 2>&1)
no_claude_rc=$?
check "exit 0 when ~/.claude/ missing" test "$no_claude_rc" -eq 0
check "skip message printed" \
  bash -c "echo '$no_claude_out' | grep -q 'Skipping user-level permission install'"
check "no settings.json created" bash -c "test ! -f '$SANDBOX/.claude/settings.json'"

# Test 5 — --dry-run writes nothing, prints diff
echo "[5/5] --dry-run writes nothing"
reset_home
echo '{}' > "$SANDBOX/.claude/settings.json"
before_hash=$(shasum -a 256 "$SANDBOX/.claude/settings.json" | awk '{print $1}')
HOME="$SANDBOX" ./scripts/install-claude-permissions.sh --dry-run >/dev/null 2>&1
after_hash=$(shasum -a 256 "$SANDBOX/.claude/settings.json" | awk '{print $1}')
check "file unchanged after --dry-run" test "$before_hash" = "$after_hash"

# Summary
echo
TOTAL=$((PASS + FAIL))
echo "=== ${PASS}/${TOTAL} PASS ==="
if (( FAIL > 0 )); then
  echo "failed:" >&2
  for n in "${FAILED[@]}"; do echo "  - $n" >&2; done
  exit 1
fi
exit 0
