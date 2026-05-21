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

# Doctor signal — answers "what's the machine's health right now?" so
# the operator doesn't have to type a status-check prompt to find out.
# scripts/doctor.sh --json takes ~2s, far too slow for a 300ms refresh,
# so we cache and refresh in the background when stale (>60s old).
#
# Reduction lever 4 from
# docs/research/2026-05-22-intervention-reduction.md — absorbs
# "where are we?" / "any alerts?" prompts into the always-visible
# statusline.
DOCTOR_FLAG=""
DOCTOR_CACHE="/tmp/cc-doctor-cache.json"
DOCTOR_LOCK="/tmp/cc-doctor-cache.lock"
if [[ -x "$DIR/scripts/doctor.sh" ]]; then
  # Check cache age; regen in background if missing or older than 60s.
  needs_regen=0
  if [[ ! -f "$DOCTOR_CACHE" ]]; then
    needs_regen=1
  else
    # mtime in epoch seconds — `stat -f %m` (BSD) or `stat -c %Y` (GNU)
    mtime=$(stat -f %m "$DOCTOR_CACHE" 2>/dev/null \
            || stat -c %Y "$DOCTOR_CACHE" 2>/dev/null || echo 0)
    now=$(date +%s)
    if (( now - mtime > 60 )); then needs_regen=1; fi
  fi
  if (( needs_regen )) && [[ ! -f "$DOCTOR_LOCK" ]]; then
    # Lock file prevents concurrent regens.  Auto-removed when the
    # background job exits.  doctor.sh exits 0 on PASS, 1 on WARN, 2
    # on FAIL — all are valid signal we want to surface, so we accept
    # exit codes 0-2 and only treat 3+ as a real failure.
    (
      touch "$DOCTOR_LOCK"
      set +e
      "$DIR/scripts/doctor.sh" --json > "$DOCTOR_CACHE.tmp" 2>/dev/null
      rc=$?
      if (( rc <= 2 )) && [[ -s "$DOCTOR_CACHE.tmp" ]]; then
        mv "$DOCTOR_CACHE.tmp" "$DOCTOR_CACHE"
      else
        rm -f "$DOCTOR_CACHE.tmp"
      fi
      rm -f "$DOCTOR_LOCK"
    ) &
    disown 2>/dev/null || true
  fi
  if [[ -f "$DOCTOR_CACHE" ]]; then
    overall=$(jq -r '.overall // "?"' "$DOCTOR_CACHE" 2>/dev/null || echo "?")
    p=$(jq -r '.pass // 0' "$DOCTOR_CACHE" 2>/dev/null || echo 0)
    w=$(jq -r '.warn // 0' "$DOCTOR_CACHE" 2>/dev/null || echo 0)
    f=$(jq -r '.fail // 0' "$DOCTOR_CACHE" 2>/dev/null || echo 0)
    # Prefer actionable_warn (excludes opt-in env keys + git-tree
    # informational signal) over raw warn count.  Falls back to warn
    # for older cached output that predates the actionable_warn field.
    aw=$(jq -r '.actionable_warn // .warn // 0' "$DOCTOR_CACHE" 2>/dev/null || echo 0)
    case "$overall" in
      PASS) DOCTOR_FLAG="doctor:✓" ;;
      WARN)
        # If no actionable WARNs, show green checkmark with a small
        # subtext (informational drift only).
        if (( aw == 0 )); then
          DOCTOR_FLAG="doctor:✓ᵢ"
        else
          DOCTOR_FLAG="doctor:⚠${aw}"
        fi
        ;;
      FAIL) DOCTOR_FLAG="doctor:✗${f}" ;;
      *)    DOCTOR_FLAG="doctor:${overall}" ;;
    esac
    # If the audit dropped a CURRENT-ALERT, surface it explicitly —
    # this is the highest-signal flag the operator wants to see.
    if [[ -f "$DIR/docs/audit/CURRENT-ALERT.md" ]]; then
      DOCTOR_FLAG="${DOCTOR_FLAG}·audit⚠"
    fi
  fi
fi

# Goal signal — reduction lever 10 from
# docs/research/2026-05-22-intervention-reduction.md.  Reads the active
# goal's deliverable-subgoal progress via the goal-lock skill and
# surfaces a compact "goal:CHECKED/TOTAL" tag.  Goal file changes are
# infrequent so a 5-minute cache is fine.
GOAL_FLAG=""
GOAL_CACHE="/tmp/cc-goal-cache.json"
GOAL_LOCK="/tmp/cc-goal-cache.lock"
GOAL_SCRIPT="$DIR/skills/goal-lock/scripts/check-done.sh"
if [[ -x "$GOAL_SCRIPT" ]]; then
  needs_regen=0
  if [[ ! -f "$GOAL_CACHE" ]]; then
    needs_regen=1
  else
    mtime=$(stat -f %m "$GOAL_CACHE" 2>/dev/null \
            || stat -c %Y "$GOAL_CACHE" 2>/dev/null || echo 0)
    now=$(date +%s)
    if (( now - mtime > 300 )); then needs_regen=1; fi
  fi
  if (( needs_regen )) && [[ ! -f "$GOAL_LOCK" ]]; then
    (
      touch "$GOAL_LOCK"
      set +e
      bash "$GOAL_SCRIPT" --json > "$GOAL_CACHE.tmp" 2>/dev/null
      rc=$?
      if (( rc <= 1 )) && [[ -s "$GOAL_CACHE.tmp" ]]; then
        mv "$GOAL_CACHE.tmp" "$GOAL_CACHE"
      else
        rm -f "$GOAL_CACHE.tmp"
      fi
      rm -f "$GOAL_LOCK"
    ) &
    disown 2>/dev/null || true
  fi
  if [[ -f "$GOAL_CACHE" ]]; then
    g_checked=$(jq -r '.checked // "?"' "$GOAL_CACHE" 2>/dev/null || echo "?")
    g_total=$(jq -r '.total // "?"' "$GOAL_CACHE" 2>/dev/null || echo "?")
    g_done=$(jq -r '.all_done // false' "$GOAL_CACHE" 2>/dev/null || echo "false")
    if [[ "$g_done" == "true" ]]; then
      GOAL_FLAG="goal:✓${g_checked}/${g_total}"
    else
      GOAL_FLAG="goal:${g_checked}/${g_total}"
    fi
  fi
fi

# Assemble.  Single line, separators are bullet middots.
LINE="dir:$SHORT_DIR"
[[ -n "$BRANCH" ]]       && LINE="$LINE · git:$BRANCH"
[[ "$MODEL" != "?" ]]    && LINE="$LINE · model:$MODEL"
[[ -n "$COST_DISPLAY" ]] && LINE="$LINE · cost:$COST_DISPLAY"
[[ "$SESSION" != "?" ]]  && LINE="$LINE · sid:$SESSION"
[[ -n "$DOCTOR_FLAG" ]]  && LINE="$LINE · $DOCTOR_FLAG"
[[ -n "$GOAL_FLAG" ]]    && LINE="$LINE · $GOAL_FLAG"

echo "$LINE"
