#!/usr/bin/env bash
# Minimal Claude Code statusline — no external npm deps.
#
# Claude Code feeds this script a JSON document on stdin every refresh
# (~300ms).  Whatever you print to stdout becomes the status line.
# Schema (subset we use):
#   {
#     "workspace": {"current_dir": "/abs/path"},
#     "model":     {"display_name": "Sonnet 4.7"},
#     "session_id": "uuid",
#     "cost":      {"total_cost_usd": 1.23, "total_lines_added": ...}
#   }
#
# Wire-up: ~/.claude/settings.json
#   "statusLine": {
#     "type": "command",
#     "command": "<absolute-path-to-repo>/scripts/statusline.sh"
#   }
# (settings.json doesn't expand shell vars; substitute the literal path
# from `git rev-parse --show-toplevel` or whatever location you cloned to.)
#
# Swap for chongdashu's cc-statusline (598⭐ on GitHub) if you want
# token-usage bars and burn-rate:
#   npx @chongdashu/cc-statusline@latest init
# That one adds context window %, time-until-reset, $/h, tokens/min.
# This script gives you the 80% (dir, branch, model, cost) at 0 deps.

set -euo pipefail

# Slurp the input JSON.
INPUT="$(cat)"

# Pull fields, defaulting to "?" when the key is missing.
get() {
  echo "$INPUT" | jq -r "$1 // \"?\""
}

DIR=$(get '.workspace.current_dir')
MODEL=$(get '.model.display_name')
COST=$(get '.cost.total_cost_usd')
SESSION=$(get '.session_id' | cut -c1-6)

# Replace $HOME with ~ for readability.
SHORT_DIR="${DIR/#$HOME/~}"

# Git branch (if we're in a repo).  Failures fall back silently.
BRANCH=""
if [[ -d "$DIR/.git" ]] || git -C "$DIR" rev-parse --git-dir >/dev/null 2>&1; then
  BRANCH=$(git -C "$DIR" rev-parse --abbrev-ref HEAD 2>/dev/null || true)
fi

# Format cost: blank if zero, else $X.XX.
COST_DISPLAY=""
if [[ "$COST" != "?" && "$COST" != "0" && "$COST" != "0.0" && "$COST" != "null" ]]; then
  COST_DISPLAY=$(printf '$%.2f' "$COST" 2>/dev/null || echo "")
fi

# Assemble.  Single line, separators are bullet middots.
LINE="dir:$SHORT_DIR"
[[ -n "$BRANCH" ]]       && LINE="$LINE · git:$BRANCH"
[[ "$MODEL" != "?" ]]    && LINE="$LINE · model:$MODEL"
[[ -n "$COST_DISPLAY" ]] && LINE="$LINE · cost:$COST_DISPLAY"
[[ "$SESSION" != "?" ]]  && LINE="$LINE · sid:$SESSION"

echo "$LINE"
