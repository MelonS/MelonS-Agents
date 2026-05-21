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

# Coalesce overlapping audits.  Without this, a burst of drift-risk
# commits within the ~5-15 min an audit takes spawns one Claude CLI
# subprocess per commit — observed 2026-05-22 at 6+ concurrent audit
# processes during an autonomous overnight run.  Each one duplicates
# work + burns Max-plan token quota.
#
# Strategy: if any prior audit is still in-flight (sentinel file is
# present and the pid is alive), only update the sentinel to the new
# SHA and exit.  The running audit, when it finishes, checks the
# sentinel for a deferred SHA and re-fires once for it — coalescing
# any number of commits-during-audit into one extra run.
SENTINEL="$LOG_DIR/.hook.inflight"
if [[ -f "$SENTINEL" ]]; then
  inflight_pid=$(awk 'NR==1 {print $1}' "$SENTINEL" 2>/dev/null)
  if [[ -n "$inflight_pid" ]] && kill -0 "$inflight_pid" 2>/dev/null; then
    # Record this SHA as the deferred re-fire target.  Overwrite the
    # second line so only the latest queued SHA wins.
    awk 'NR==1' "$SENTINEL" > "$SENTINEL.tmp"
    echo "$SHA" >> "$SENTINEL.tmp"
    mv "$SENTINEL.tmp" "$SENTINEL"
    echo "[$STAMP] $SHA → audit-run.sh $FOCUS DEFERRED (in-flight pid $inflight_pid)" >> "$TRIGGER_LOG"
    echo "[audit-hook] $SHA deferred — audit pid $inflight_pid still running" >&2
    exit 0
  fi
  # Stale sentinel — pid dead.  Continue and overwrite below.
fi

# Log the trigger.  Echo back to the user too — the post-commit hook
# stdout is shown alongside the commit confirmation.
echo "[$STAMP] $SHA → audit-run.sh $FOCUS (drift-risk paths changed)" >> "$TRIGGER_LOG"
echo "[audit-hook] firing audit-run.sh $FOCUS in background after $SHA" >&2

# Fire the audit detached from the commit shell.  `setsid` (linux) or
# the double-fork pattern (mac) ensures the audit survives git's
# process group cleanup.  Output goes to a per-commit log.  The
# trampoline writes its own pid to SENTINEL on entry, removes SENTINEL
# on exit, and re-fires post-commit if a deferred SHA is queued.
RUN_LOG="$LOG_DIR/hook-run-$(date +%Y%m%d-%H%M%S)-$SHA.log"
nohup bash -c "
  cd '$REPO_ROOT'
  echo \"\$\$\" > '$SENTINEL'
  ./scripts/audit-run.sh '$FOCUS' > '$RUN_LOG' 2>&1
  rc=\$?
  echo \"[\$(date '+%Y-%m-%d %H:%M:%S')] $SHA audit \$rc done (log: $RUN_LOG)\" >> '$TRIGGER_LOG'

  # Check for a deferred re-fire.  If a SHA is on line 2 of the
  # sentinel AND it differs from the SHA we just audited, log that a
  # follow-up audit covering the deferred commits is recommended.
  # The L2 audit-poll (15-min cadence) or the L3 daily 03:00 audit
  # will cover them on their next pass — losing 15 min of latency on
  # rare deferred runs is the right trade for not spawning N concurrent
  # claude CLI processes.
  deferred=\$(awk 'NR==2' '$SENTINEL' 2>/dev/null)
  rm -f '$SENTINEL'
  if [[ -n \"\$deferred\" && \"\$deferred\" != '$SHA' ]]; then
    echo \"[\$(date '+%Y-%m-%d %H:%M:%S')] coalesced: \$deferred deferred behind $SHA — L2/L3 will catch on next cycle\" >> '$TRIGGER_LOG'
  fi
" >/dev/null 2>&1 </dev/null &

exit 0
