#!/usr/bin/env bash
# test-install-claude-local.sh — idempotency regression test for the
# operator-style block render path.
#
# Protects the 2026-05-22 fix (commit 7f44c59) of the bug where each
# install run prepended one more decorative `<!-- ┌─` opener line to
# ~/.claude/CLAUDE.md.  After 9 runs the operator's file had 9
# stacked openers.
#
# This test runs install-claude-local.sh against a sandboxed HOME, with
# the legacy decorative-box format pre-seeded, asserts:
#
#   1. After 1 run: file has 1 BEGIN marker, 0 stacked openers, 0
#      unresolved @@…@@ placeholders.
#   2. After 3 runs: file md5 is identical to the after-1-run md5
#      (true idempotency).
#   3. After 3 runs: legacy decorative content is gone.
#
# Usage: scripts/test-install-claude-local.sh
# Exit: 0 if all asserts pass, 1 if any fail.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

SANDBOX="$(mktemp -d -t install-claude-local-test-XXXX)"
trap 'rm -rf "$SANDBOX"' EXIT

pass=0
fail=0
say_pass() { echo "  ✓ $1"; pass=$((pass + 1)); }
say_fail() { echo "  ✗ $1"; fail=$((fail + 1)); }

echo "[test] sandbox HOME = $SANDBOX"
mkdir -p "$SANDBOX/.claude"

# Pre-seed sandbox CLAUDE.md with the legacy decorative-box format AND
# 5 stacked opener lines (simulating the operator's bug state).
cat > "$SANDBOX/.claude/CLAUDE.md" <<'LEGACY'
# Global Claude Code Instructions

Test fixture — pre-existing operator content stays above the block.

---

<!-- ┌──────────────────────────────────────────────────────────────┐
<!-- ┌──────────────────────────────────────────────────────────────┐
<!-- ┌──────────────────────────────────────────────────────────────┐
<!-- ┌──────────────────────────────────────────────────────────────┐
<!-- ┌──────────────────────────────────────────────────────────────┐
     │ BEGIN repo-managed operator-style block                       │
     │ (legacy decorative format)                                    │
     └──────────────────────────────────────────────────────────────┘ -->

## Operator style — applies to all projects

(stale legacy body content here — should be replaced cleanly)

<!-- END repo-managed operator-style block -->
LEGACY

# Run install with sandbox HOME.
run_install() {
  HOME="$SANDBOX" "$REPO_ROOT/scripts/install-claude-local.sh" >/dev/null 2>&1
}

echo "[test] run 1 — replace legacy block with new single-line marker"
run_install

# Assertion 1: exactly 1 BEGIN line
n_begin=$(grep -c '^<!-- BEGIN repo-managed' "$SANDBOX/.claude/CLAUDE.md" || true)
[[ "$n_begin" == "1" ]] && say_pass "1 BEGIN marker (got $n_begin)" \
                          || say_fail "expected 1 BEGIN marker, got $n_begin"

# Assertion 2: zero stacked legacy openers
n_legacy=$(grep -c '^<!-- ┌─' "$SANDBOX/.claude/CLAUDE.md" || true)
[[ "$n_legacy" == "0" ]] && say_pass "0 legacy stacked openers (got $n_legacy)" \
                            || say_fail "expected 0 stacked openers, got $n_legacy"

# Assertion 3: no unresolved @@…@@ placeholders
n_unresolved=$(grep -c '@@[A-Z_]\+@@' "$SANDBOX/.claude/CLAUDE.md" || true)
[[ "$n_unresolved" == "0" ]] && say_pass "0 unresolved @@…@@ placeholders" \
                                || say_fail "found $n_unresolved unresolved placeholders"

# Assertion 4: pre-existing operator content above block is preserved
grep -q "Test fixture — pre-existing operator content" "$SANDBOX/.claude/CLAUDE.md" \
  && say_pass "pre-existing operator content preserved above block" \
  || say_fail "pre-existing operator content was stripped"

# Assertion 5: legacy body ("stale legacy body content here") is gone
grep -q "stale legacy body content here" "$SANDBOX/.claude/CLAUDE.md" \
  && say_fail "legacy body content leaked through replacement" \
  || say_pass "legacy body content correctly stripped"

# Idempotency — md5 stable across 3 reruns.
md5_after1=$(md5 -q "$SANDBOX/.claude/CLAUDE.md" 2>/dev/null \
             || md5sum "$SANDBOX/.claude/CLAUDE.md" | awk '{print $1}')
echo "[test] run 2"; run_install
md5_after2=$(md5 -q "$SANDBOX/.claude/CLAUDE.md" 2>/dev/null \
             || md5sum "$SANDBOX/.claude/CLAUDE.md" | awk '{print $1}')
echo "[test] run 3"; run_install
md5_after3=$(md5 -q "$SANDBOX/.claude/CLAUDE.md" 2>/dev/null \
             || md5sum "$SANDBOX/.claude/CLAUDE.md" | awk '{print $1}')

[[ "$md5_after1" == "$md5_after2" && "$md5_after2" == "$md5_after3" ]] \
  && say_pass "md5 stable across 3 runs ($md5_after1)" \
  || say_fail "md5 drifted: after1=$md5_after1 after2=$md5_after2 after3=$md5_after3"

# Assertion 7: still 1 BEGIN after 3 runs (no accumulation)
n_begin_final=$(grep -c '^<!-- BEGIN repo-managed' "$SANDBOX/.claude/CLAUDE.md" || true)
[[ "$n_begin_final" == "1" ]] && say_pass "still 1 BEGIN marker after 3 runs" \
                                || say_fail "BEGIN count drifted to $n_begin_final after 3 runs"

echo
echo "[test] result: $pass passed, $fail failed"
exit $(( fail > 0 ? 1 : 0 ))
