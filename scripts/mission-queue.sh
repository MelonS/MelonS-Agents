#!/usr/bin/env bash
# Queue-based mission processor for autonomous (overnight) runs.
#
# Reads one source-spec per line from QUEUE_PENDING. For each entry:
#   1. Move line atomically to records/queue/in-progress.txt
#   2. Run the highlight mission against the source
#   3. Append outcome to records/queue/done.log (PASS/FAIL, mission dir)
#
# Lines starting with '#' or blank lines are skipped.
# Run with no args to drain the queue once. Exits 0 even if no pending items.
set -u
cd "$(dirname "${BASH_SOURCE[0]}")/.."

QUEUE_DIR="records/queue"
PENDING="$QUEUE_DIR/pending.txt"
INPROG="$QUEUE_DIR/in-progress.txt"
DONE_LOG="$QUEUE_DIR/done.log"
RUN_LOG="$QUEUE_DIR/run.log"
mkdir -p "$QUEUE_DIR"
touch "$PENDING" "$DONE_LOG" "$RUN_LOG"

ts() { date +%Y-%m-%dT%H:%M:%S%z; }
say() { printf '[%s] %s\n' "$(ts)" "$*" >>"$RUN_LOG"; printf '%s\n' "$*"; }

# Drain loop
while :; do
  # Pop first non-blank / non-comment line
  LINE=""
  while IFS= read -r raw; do
    trimmed="${raw%%#*}"
    trimmed="$(echo "$trimmed" | sed -E 's/^[[:space:]]+//;s/[[:space:]]+$//')"
    if [[ -n "$trimmed" ]]; then
      LINE="$trimmed"
      break
    fi
  done < "$PENDING"
  [[ -z "$LINE" ]] && { say "queue empty"; break; }

  # Remove that line from pending
  awk -v target="$LINE" 'BEGIN{removed=0}
    { if (!removed && $0 == target) { removed=1; next } print }' "$PENDING" > "$PENDING.tmp"
  mv "$PENDING.tmp" "$PENDING"
  echo "$LINE" > "$INPROG"
  say "▶ START: $LINE"

  if ./agents/missions/highlight/run.sh "$LINE" >>"$RUN_LOG" 2>&1; then
    LAST=$(ls -1dt records/missions/$(date +%Y-%m-%d)/highlight-* 2>/dev/null | head -1)
    say "✓ PASS: $LINE → $LAST"
    echo "$(ts) PASS $LINE $LAST" >>"$DONE_LOG"
  else
    RC=$?
    say "✗ FAIL($RC): $LINE"
    echo "$(ts) FAIL[$RC] $LINE" >>"$DONE_LOG"
  fi
  : > "$INPROG"
done
