#!/usr/bin/env bash
# Append an entry to docs/autonomous-decisions.md.
#
# Lever 9 from docs/research/2026-05-22-intervention-reduction.md.
# When the agent makes a unilateral decision during autonomous work,
# call this so the operator can scan one page in the morning instead
# of typing status-check prompts.
#
# Usage:
#   scripts/log-decision.sh "decision summary in one line"
#   scripts/log-decision.sh --time HH:MM "decision summary"   # override timestamp
#
# Auto-creates the date header section if today isn't already there.
# Append-only; no in-place rewrites.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
LOG="$REPO_ROOT/docs/autonomous-decisions.md"

if [[ ! -f "$LOG" ]]; then
  echo "[log-decision] $LOG missing — did the file get moved?" >&2
  exit 1
fi

time_override=""
if [[ "${1:-}" == "--time" ]]; then
  time_override="${2:?--time needs HH:MM}"
  shift 2
fi

msg="${1:?usage: log-decision.sh [--time HH:MM] \"decision summary\"}"
if [[ -z "$time_override" ]]; then
  # KST clock — matches the log's existing entries.
  ts=$(TZ=Asia/Seoul date +%H:%M)
else
  ts="$time_override"
fi
today=$(TZ=Asia/Seoul date +%Y-%m-%d)

# If today's date header doesn't exist yet, insert a new section at the
# top of the log (between the "## How to interpret this log" anchor and
# the last "## YYYY-MM-DD" section).
if ! grep -q "^## ${today}" "$LOG"; then
  # Find the first existing dated section and insert ours above it.
  # If no dated section exists yet, append at end (degenerate case).
  if grep -q "^## 2026-" "$LOG"; then
    awk -v today="$today" '
      BEGIN { inserted = 0 }
      /^## 2026-/ && !inserted {
        printf "## %s (autonomous)\n\n", today
        inserted = 1
      }
      { print }
    ' "$LOG" > "$LOG.tmp" && mv "$LOG.tmp" "$LOG"
  else
    printf "\n## %s (autonomous)\n\n" "$today" >> "$LOG"
  fi
fi

# Append the entry under today's section.  awk inserts the bullet right
# after the date header (keeps chronological-within-day order: newest at
# top of each day's section).
awk -v today="$today" -v ts="$ts" -v msg="$msg" '
  /^## / && match($0, "^## " today) {
    print
    print ""
    print "- `" ts " KST` — " msg
    in_section = 1
    next
  }
  in_section && /^$/ {
    in_section = 0
    next
  }
  { print }
' "$LOG" > "$LOG.tmp" && mv "$LOG.tmp" "$LOG"

echo "[log-decision] $today $ts → $msg"
