#!/usr/bin/env bash
# Post-commit hook — fires a focused contract audit when the last commit
# touched files that are most prone to drift (agent definitions, mission
# templates, configs, the operator contract itself).  Catches DRIFT
# within seconds of it landing rather than waiting for the daily 03:00
# auditor cron.
#
# Install with:
#   scripts/install-hooks.sh install
#
# Disable with:
#   scripts/install-hooks.sh uninstall      # remove the hook
#   AUDIT_HOOK_DISABLED=1 git commit ...    # one-off skip
#
# Design notes:
# - Runs the audit in the BACKGROUND (`&`) so `git commit` returns to
#   the user immediately.  The audit completes asynchronously and writes
#   to docs/audit/<date>-contract.md + (if non-CLEAN) CURRENT-ALERT.md.
# - Uses the `contract` focus by default — narrowest, cheapest audit
#   focus, runs in ~30s.  Override with AUDIT_HOOK_FOCUS=all if you
#   want the full sweep on every drift-risk commit.
# - Only fires when changed files actually intersect the drift-risk
#   set.  Pure docs commits (e.g., roadmap Done entries) skip the hook
#   even though docs/ is in §6 scope — they don't change behaviour.
# - Reads $REPO_ROOT/.git/HEAD via `git rev-parse` rather than hard-
#   coding paths so the hook works under any clone location.
set -u

if [[ "${AUDIT_HOOK_DISABLED:-0}" == "1" ]]; then
  exit 0
fi

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)"
[[ -z "$REPO_ROOT" || ! -d "$REPO_ROOT/.git" ]] && exit 0

# Files changed in the just-landed commit.  Empty result is harmless
# (e.g., merge commits with no per-file diff).
CHANGED=$(git diff-tree --no-commit-id --name-only -r HEAD 2>/dev/null)
[[ -z "$CHANGED" ]] && exit 0

# Drift-risk paths.  Any change inside these triggers the audit.
# Extend if a new high-drift surface emerges.
RISK_RE='^(\.claude/agents/|agents/|config/|CLAUDE\.md$|docs/operator-contract\.md$|scripts/audit-run\.sh$|\.claude/settings\.json$)'

if ! echo "$CHANGED" | grep -qE "$RISK_RE"; then
  exit 0
fi

FOCUS="${AUDIT_HOOK_FOCUS:-contract}"
LOG_DIR="$REPO_ROOT/records/audit"
mkdir -p "$LOG_DIR"
TRIGGER_LOG="$LOG_DIR/hook-trigger.log"
STAMP=$(date '+%Y-%m-%d %H:%M:%S')
SHA=$(git rev-parse --short HEAD)

# Log the trigger.  Echo back to the user too — the post-commit hook
# stdout is shown alongside the commit confirmation.
echo "[$STAMP] $SHA → audit-run.sh $FOCUS (drift-risk paths changed)" >> "$TRIGGER_LOG"
echo "[audit-hook] firing audit-run.sh $FOCUS in background after $SHA" >&2

# Fire the audit detached from the commit shell.  `setsid` (linux) or
# the double-fork pattern (mac) ensures the audit survives git's
# process group cleanup.  Output goes to a per-commit log.
RUN_LOG="$LOG_DIR/hook-run-$(date +%Y%m%d-%H%M%S)-$SHA.log"
nohup bash -c "
  cd '$REPO_ROOT'
  ./scripts/audit-run.sh '$FOCUS' > '$RUN_LOG' 2>&1
  echo \"[\$(date '+%Y-%m-%d %H:%M:%S')] $SHA audit \$? done (log: $RUN_LOG)\" >> '$TRIGGER_LOG'
" >/dev/null 2>&1 </dev/null &

exit 0
