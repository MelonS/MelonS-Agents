#!/usr/bin/env bash
# check-done.sh — list unchecked deliverable subgoals of the active
# goal in docs/goal.md.  See ../SKILL.md for the full description.
#
# Usage:
#   bash skills/goal-lock/scripts/check-done.sh
#   bash skills/goal-lock/scripts/check-done.sh --quiet
#   bash skills/goal-lock/scripts/check-done.sh --json
#
# Exit codes:
#   0  at least one subgoal remains unchecked
#   1  all subgoals checked (goal probably done)
#   2  goal.md missing / malformed / active section empty

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
GOAL_FILE="${GOAL_FILE:-$REPO_ROOT/docs/goal.md}"

mode="full"
case "${1:-}" in
  --quiet) mode="quiet" ;;
  --json)  mode="json" ;;
  -h|--help)
    sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'
    exit 0
    ;;
esac

if [[ ! -r "$GOAL_FILE" ]]; then
  echo "[goal-lock] goal file not readable: $GOAL_FILE" >&2
  exit 2
fi

# Extract the first "### " subsection under "## Active goal", stopping at
# the next "## " heading.  awk is the simplest tool here.
active_block="$(
  awk '
    /^## Active goal/ { in_active = 1; next }
    in_active && /^## / { exit }
    in_active { print }
  ' "$GOAL_FILE"
)"

if [[ -z "$active_block" ]]; then
  echo "[goal-lock] no \"## Active goal\" section found in $GOAL_FILE" >&2
  exit 2
fi

# Within the active block, extract the first "### " subsection only.
first_subsection="$(
  printf '%s\n' "$active_block" | awk '
    /^### / { count++; if (count > 1) exit }
    count >= 1 { print }
  '
)"

if [[ -z "$first_subsection" ]]; then
  echo "[goal-lock] active goal section has no \"### \" subgoal block" >&2
  exit 2
fi

# Count checked / unchecked subgoals.  Match the exact markdown
# checkbox prefix (case-sensitive lowercase x for checked).
checked=$(printf '%s\n' "$first_subsection" | grep -c '^- \[x\]' || true)
unchecked=$(printf '%s\n' "$first_subsection" | grep -c '^- \[ \]' || true)
total=$((checked + unchecked))

# Capture unchecked items + the "Done when" line for the report.
unchecked_lines="$(printf '%s\n' "$first_subsection" | grep '^- \[ \]' || true)"
done_when_line="$(printf '%s\n' "$first_subsection" | grep -E '^\*\*Done when\*\*:|^Done when:' | head -1 || true)"
title_line="$(printf '%s\n' "$first_subsection" | grep -m1 '^### ' || true)"

# Determine exit code in advance (so the JSON and quiet modes match).
if (( unchecked == 0 )) && (( total > 0 )); then
  exit_code=1   # all checked
else
  exit_code=0   # at least one unchecked (or zero subgoals — treat as work-to-do)
fi

case "$mode" in
  quiet)
    printf 'goal: %d/%d subgoals checked, %d remaining\n' \
      "$checked" "$total" "$unchecked"
    ;;
  json)
    printf '{"checked":%d,"unchecked":%d,"total":%d,"all_done":%s}\n' \
      "$checked" "$unchecked" "$total" \
      "$([[ $exit_code == 1 ]] && echo true || echo false)"
    ;;
  full)
    cat <<EOF

goal-lock — active goal check  ($GOAL_FILE)
─────────────────────────────────────────────────────────────

${title_line:-(no title line)}

Subgoals: ${checked} / ${total} checked    Remaining: ${unchecked}

EOF
    if [[ -n "$unchecked_lines" ]]; then
      echo "Unchecked deliverables:"
      printf '%s\n' "$unchecked_lines"
      echo
    fi
    if [[ -n "$done_when_line" ]]; then
      printf '%s\n\n' "$done_when_line"
    fi
    echo "─────────────────────────────────────────────────────────────"
    if (( exit_code == 1 )); then
      echo "all subgoals checked — verify Done-when prose manually before declaring goal complete"
    else
      echo "work remaining"
    fi
    ;;
esac

exit "$exit_code"
