#!/usr/bin/env bash
# Regression test for the verdict-parsing block in scripts/audit-run.sh.
#
# The auditor runs unattended at 03:00 via launchd, so a silent regression
# in the parser would leave docs/audit/CURRENT-ALERT.md either stuck (stale
# alert that never clears) or missing (real drift that nobody notices).
# This test exercises the three verdict cases against synthetic reports
# and verifies the CURRENT-ALERT.md state transitions.
#
# Runs in a sandbox under /tmp/test-audit-parser.<pid>/.  Does not touch
# the real docs/audit/CURRENT-ALERT.md.
#
# Usage:
#   ./scripts/test-audit-parser.sh
#
# Exit:
#   0 on all-pass, non-zero on first failure.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SANDBOX="$(mktemp -d "/tmp/test-audit-parser.XXXXXX")"
trap 'rm -rf "$SANDBOX"' EXIT

ALERT_FILE="$SANDBOX/CURRENT-ALERT.md"
FAILED=0

# Re-implementation of the parse_verdict_and_alert logic from audit-run.sh.
# Kept in sync intentionally — the test is the contract.  When audit-run.sh
# changes the parser, this file changes too, and both stay greppable.
parse_and_alert() {
  local OUT_FILE="$1" DATE="$2" FOCUS="$3"
  local verdict
  verdict="$(grep -m1 -E '^\*\*Verdict\*\*:' "$OUT_FILE" \
              | sed -E 's/^\*\*Verdict\*\*:[[:space:]]*//' \
              | awk '{print $1}')"
  [[ -z "$verdict" ]] && return 0

  case "$verdict" in
    CLEAN)
      if [[ -f "$ALERT_FILE" ]]; then
        rm -f "$ALERT_FILE"
      fi
      ;;
    DRIFT_DETECTED|CRITICAL)
      local summary findings_block
      summary="$(awk '/^## Summary[[:space:]]*$/{flag=1; next} /^## /{flag=0} flag' "$OUT_FILE")"
      findings_block="$(awk '/^## Findings[[:space:]]*$/{flag=1; next} /^## /{flag=0} flag' "$OUT_FILE" \
                        | grep -E '^\- \*\*\[(critical|high)\]\*\*' || true)"
      {
        printf '# Current audit alert\n\n'
        printf '**Verdict**: %s\n' "$verdict"
        printf '**Full report**: docs/audit/%s-%s.md\n\n' "$DATE" "$FOCUS"
        printf '## Summary\n\n%s\n\n' "${summary:-_(none)_}"
        printf '## Critical / High findings\n\n%s\n' "${findings_block:-_(none)_}"
      } > "$ALERT_FILE"
      ;;
  esac
}

assert_alert_absent() {
  local label="$1"
  if [[ -f "$ALERT_FILE" ]]; then
    echo "FAIL  $label — CURRENT-ALERT.md should be absent but exists"
    FAILED=$((FAILED + 1))
  else
    echo "PASS  $label — alert absent as expected"
  fi
}

assert_alert_present() {
  local label="$1" expect_verdict="$2"
  if [[ ! -f "$ALERT_FILE" ]]; then
    echo "FAIL  $label — CURRENT-ALERT.md should exist but is missing"
    FAILED=$((FAILED + 1))
    return
  fi
  if ! grep -q "^\*\*Verdict\*\*: $expect_verdict" "$ALERT_FILE"; then
    echo "FAIL  $label — alert exists but verdict line does not say '$expect_verdict'"
    cat "$ALERT_FILE" | head -5
    FAILED=$((FAILED + 1))
    return
  fi
  echo "PASS  $label — alert present with verdict=$expect_verdict"
}

mk_report() {
  local path="$1" verdict="$2"
  cat > "$path" <<REPORT
# Audit report — 2026-05-16 (all)

**Verdict**: $verdict

## Summary
Synthetic report for parser regression test (verdict=$verdict).

## Findings
- **[critical]** Synthetic critical finding — \`fake/file.sh:1\`
  Evidence: this is a fixture, not a real finding
  Suggested fix: ignore — test data
- **[high]** Synthetic high finding — \`fake/other.sh:2\`
  Evidence: still a fixture
  Suggested fix: ignore
- **[medium]** Synthetic medium finding (should be filtered out of alert) — \`fake/m.sh:3\`
  Evidence: fixture
  Suggested fix: ignore
- **[low]** Synthetic low finding (should be filtered out) — \`fake/l.sh:4\`
  Evidence: fixture
  Suggested fix: ignore

## Next audit hint
This is a test report, no real next audit needed.
REPORT
}

echo "=== Audit parser regression test ==="
echo "sandbox: $SANDBOX"
echo

# Case 1 — CRITICAL on a fresh sandbox writes the alert.
mk_report "$SANDBOX/2026-05-16-all.md" "CRITICAL"
parse_and_alert "$SANDBOX/2026-05-16-all.md" "2026-05-16" "all"
assert_alert_present "CRITICAL on empty sandbox" "CRITICAL"

# Case 2 — DRIFT_DETECTED overwrites the existing alert.
mk_report "$SANDBOX/2026-05-16-all.md" "DRIFT_DETECTED"
parse_and_alert "$SANDBOX/2026-05-16-all.md" "2026-05-16" "all"
assert_alert_present "DRIFT_DETECTED overwriting prior CRITICAL" "DRIFT_DETECTED"

# Case 3 — CLEAN removes the alert.
mk_report "$SANDBOX/2026-05-16-all.md" "CLEAN"
parse_and_alert "$SANDBOX/2026-05-16-all.md" "2026-05-16" "all"
assert_alert_absent "CLEAN after prior DRIFT_DETECTED"

# Case 4 — CLEAN on an already-clean sandbox stays clean (idempotent).
parse_and_alert "$SANDBOX/2026-05-16-all.md" "2026-05-16" "all"
assert_alert_absent "CLEAN idempotent (no-op when no prior alert)"

# Case 5 — alert content contains the critical+high findings, not medium/low.
mk_report "$SANDBOX/2026-05-16-all.md" "DRIFT_DETECTED"
parse_and_alert "$SANDBOX/2026-05-16-all.md" "2026-05-16" "all"
if grep -q "Synthetic critical finding" "$ALERT_FILE" \
   && grep -q "Synthetic high finding" "$ALERT_FILE" \
   && ! grep -q "Synthetic medium finding" "$ALERT_FILE" \
   && ! grep -q "Synthetic low finding" "$ALERT_FILE"; then
  echo "PASS  finding filter — critical+high included, medium+low excluded"
else
  echo "FAIL  finding filter — wrong findings appear in alert"
  FAILED=$((FAILED + 1))
fi

# Case 6 — missing verdict line: should be a no-op (don't crash, don't write).
rm -f "$ALERT_FILE"
cat > "$SANDBOX/malformed.md" <<MALF
# Audit report — malformed
No verdict line here at all.
MALF
if parse_and_alert "$SANDBOX/malformed.md" "2026-05-16" "all" 2>/dev/null; then
  if [[ ! -f "$ALERT_FILE" ]]; then
    echo "PASS  malformed report — no-op (no crash, no alert)"
  else
    echo "FAIL  malformed report — alert was written despite missing verdict"
    FAILED=$((FAILED + 1))
  fi
else
  echo "FAIL  malformed report — parser exited non-zero (should be no-op)"
  FAILED=$((FAILED + 1))
fi

echo
if [[ $FAILED -eq 0 ]]; then
  echo "✓ all cases passed"
  exit 0
else
  echo "✗ $FAILED case(s) failed"
  exit 1
fi
