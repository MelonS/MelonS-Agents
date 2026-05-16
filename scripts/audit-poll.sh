#!/usr/bin/env bash
# Reactive auditor — layer 2.  Polls mission outputs every 15 min
# (via launchd) and fires audit-run.sh when an anomaly pattern matches.
#
# L1 (scripts/hooks/post-commit.sh) catches code drift on commit boundary.
# L2 (this script) catches RUNTIME anomalies — mission failures, blockers,
# QA-FAIL clusters — that don't show up in code state.
# L3 (com.melons.agents.auditor.plist) is the daily full sweep baseline.
#
# Patterns we look for (cheap, file-system reads only — no API):
#
#   1. NEW BLOCKER   — any *.md file in records/blockers/<date>/ that we
#                      haven't seen before.  Fires audit-run.sh all
#                      (a blocker means QA exhausted retries, which is
#                      structurally serious — audit the whole repo).
#
#   2. QA-FAIL BURST — ≥ 2 mission qa-report.md files with
#                      "Verdict: FAIL" lines whose mtimes fall within
#                      the same 60-minute window.  Fires audit-run.sh
#                      contract (signals a process / contract breakdown).
#
# Wall-time outlier detection is left for v2 — needs a per-mission-type
# baseline median which adds complexity for a marginal-value pattern.
#
# State persisted to records/audit/poll-state.json so each poll only
# evaluates NEW evidence since the last run.  State file is gitignored
# (records/ as a whole is).
#
# Usage:
#   scripts/audit-poll.sh           # one poll cycle
#   scripts/audit-poll.sh --dry-run # report what WOULD fire, don't run audit
#
# Schedule via scripts/install-scheduler.sh install audit-poll
# (15-min interval, defined in scripts/com.melons.agents.audit-poll.plist).

set -u

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

DRY_RUN=0
[[ "${1:-}" == "--dry-run" ]] && DRY_RUN=1

STATE_DIR="$REPO_ROOT/records/audit"
mkdir -p "$STATE_DIR"
STATE_FILE="$STATE_DIR/poll-state.json"
TRIGGER_LOG="$STATE_DIR/poll-trigger.log"

NOW=$(date '+%Y-%m-%d %H:%M:%S')
NOW_EPOCH=$(date +%s)

# Load state (initialize if missing).  First-run mode: when the state
# file doesn't exist, seed `seen_blockers` with whatever blockers
# currently exist on disk and DON'T fire audit.  Otherwise the first
# poll would falsely trigger on every pre-existing blocker (e.g., a
# week-old failure that's already documented and acted on).  Future
# polls only fire on blockers added AFTER this seed.
FIRST_RUN=0
if [[ -f "$STATE_FILE" ]]; then
  LAST_RUN_EPOCH=$(jq -r '.last_run_epoch // 0' "$STATE_FILE" 2>/dev/null || echo 0)
  SEEN_BLOCKERS=$(jq -r '.seen_blockers // [] | .[]' "$STATE_FILE" 2>/dev/null || echo "")
else
  FIRST_RUN=1
  LAST_RUN_EPOCH=0
  if [[ -d records/blockers ]]; then
    SEEN_BLOCKERS=$(find records/blockers -type f -name '*.md' 2>/dev/null)
  else
    SEEN_BLOCKERS=""
  fi
fi

# Compute new blockers (files under records/blockers/ not yet in SEEN_BLOCKERS).
NEW_BLOCKERS=()
if [[ -d records/blockers ]]; then
  while IFS= read -r f; do
    [[ -z "$f" ]] && continue
    if ! echo "$SEEN_BLOCKERS" | grep -Fxq "$f"; then
      NEW_BLOCKERS+=("$f")
    fi
  done < <(find records/blockers -type f -name '*.md' 2>/dev/null)
fi

# Compute QA-FAIL burst — count qa-report.md files with "Verdict: FAIL"
# whose mtime falls within the last 60 min.
QA_FAIL_RECENT=()
ONE_HOUR_AGO=$((NOW_EPOCH - 3600))
if [[ -d records/missions ]]; then
  while IFS= read -r f; do
    [[ -z "$f" ]] && continue
    mtime=$(stat -f %m "$f" 2>/dev/null || stat -c %Y "$f" 2>/dev/null || echo 0)
    if (( mtime >= ONE_HOUR_AGO )) && grep -q "Verdict:.*FAIL" "$f" 2>/dev/null; then
      QA_FAIL_RECENT+=("$f")
    fi
  done < <(find records/missions -type f -name 'qa-report.md' -mtime -1 2>/dev/null)
fi

# Decide what (if anything) to fire.
FOCUS=""
REASON=""
if (( ${#NEW_BLOCKERS[@]} > 0 )); then
  FOCUS="all"
  REASON="${#NEW_BLOCKERS[@]} new blocker(s): $(printf '%s ' "${NEW_BLOCKERS[@]}")"
elif (( ${#QA_FAIL_RECENT[@]} >= 2 )); then
  FOCUS="contract"
  REASON="${#QA_FAIL_RECENT[@]} QA-FAIL within 1 hour: $(printf '%s ' "${QA_FAIL_RECENT[@]}")"
fi

if (( FIRST_RUN == 1 )); then
  COUNT=$(echo "$SEEN_BLOCKERS" | grep -v '^$' | wc -l | tr -d ' ')
  echo "[$NOW] first-run seed — saw $COUNT existing blocker(s), not firing" >> "$TRIGGER_LOG"
  FOCUS=""  # explicit override even if patterns matched
fi

if [[ -z "$FOCUS" ]]; then
  if (( FIRST_RUN == 0 )); then
    echo "[$NOW] no anomaly (blockers=${#NEW_BLOCKERS[@]} qa_fail_1h=${#QA_FAIL_RECENT[@]})" >> "$TRIGGER_LOG"
  fi
else
  echo "[$NOW] FIRING audit-run.sh $FOCUS — $REASON" | tee -a "$TRIGGER_LOG" >&2
  if (( DRY_RUN == 0 )); then
    nohup bash -c "
      cd '$REPO_ROOT'
      ./scripts/audit-run.sh '$FOCUS' > '$STATE_DIR/poll-run-$(date +%Y%m%d-%H%M%S).log' 2>&1
      echo \"[\$(date '+%Y-%m-%d %H:%M:%S')] audit \$? complete\" >> '$TRIGGER_LOG'
    " >/dev/null 2>&1 </dev/null &
  fi
fi

# Persist new state — extend seen_blockers, update last_run_epoch.
UPDATED_SEEN=$(printf '%s\n' "$SEEN_BLOCKERS" "${NEW_BLOCKERS[@]:-}" | grep -v '^$' | sort -u)
jq -n \
  --argjson now "$NOW_EPOCH" \
  --arg     seen "$UPDATED_SEEN" \
  '{
    last_run_epoch: $now,
    seen_blockers: ($seen | split("\n") | map(select(length > 0)))
  }' > "$STATE_FILE.tmp" && mv "$STATE_FILE.tmp" "$STATE_FILE"
