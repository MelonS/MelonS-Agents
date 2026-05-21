#!/usr/bin/env bash
# morning-brief.sh — one-command "what happened overnight" digest.
#
# Lever-9 companion (docs/research/2026-05-22-intervention-reduction.md).
# After an autonomous overnight run the operator wakes up and types
# `scripts/morning-brief.sh` instead of asking the agent for status.
# Prints a compact one-page summary to stdout combining:
#
#   - doctor health verdict (actionable count only)
#   - audit alert state
#   - intervention chart trend annotation (7-day deltas)
#   - new commits + agent vs user attribution since last operator session
#   - autonomous-decisions log entries from today
#   - review-queue pending count
#   - any unresolved blockers / open todo
#
# Usage:
#   scripts/morning-brief.sh
#   scripts/morning-brief.sh --since "yesterday"   # custom commit window
#
# Reads only — never modifies anything.  Safe to run any number of times.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

SINCE="${2:-12 hours ago}"
if [[ "${1:-}" == "--since" ]]; then SINCE="$2"; fi

# Colors when stdout is a TTY.
if [[ -t 1 ]]; then
  C_HDR=$'\033[1;36m'; C_OK=$'\033[32m'; C_WARN=$'\033[33m'; C_ERR=$'\033[31m'
  C_DIM=$'\033[2m'; C_RST=$'\033[0m'; C_BOLD=$'\033[1m'
else
  C_HDR=""; C_OK=""; C_WARN=""; C_ERR=""; C_DIM=""; C_RST=""; C_BOLD=""
fi

print_hdr() { printf "%s──── %s ────%s\n" "$C_HDR" "$1" "$C_RST"; }

printf "%s%s   Morning brief   %s\n" "$C_BOLD" "$C_HDR" "$C_RST"
printf "%s%s   $(TZ=Asia/Seoul date '+%Y-%m-%d %H:%M KST')   %s%s\n\n" "$C_DIM" "$C_HDR" "$C_RST" "$C_RST"

# ───── 1. Doctor verdict ───────────────────────────────────────────
print_hdr "Health (scripts/doctor.sh)"
if [[ -x "$SCRIPT_DIR/doctor.sh" ]]; then
  out=$("$SCRIPT_DIR/doctor.sh" --quiet 2>/dev/null || true)
  printf "  %s\n" "$out"
else
  printf "  (doctor.sh not found)\n"
fi

# ───── 2. Audit alert state ────────────────────────────────────────
print_hdr "Audit"
if [[ -f "$REPO_ROOT/docs/audit/CURRENT-ALERT.md" ]]; then
  verdict=$(grep -m1 '^\*\*Verdict\*\*:' "$REPO_ROOT/docs/audit/CURRENT-ALERT.md" | sed 's/\*\*//g' | sed 's/Verdict://')
  generated=$(grep -m1 '^\*\*Generated\*\*:' "$REPO_ROOT/docs/audit/CURRENT-ALERT.md" | sed 's/\*\*//g')
  printf "  %s${C_WARN}%s${C_RST}\n" "$verdict" ""
  printf "  ${C_DIM}%s${C_RST}\n" "$generated"
  printf "  ${C_DIM}→ docs/audit/CURRENT-ALERT.md${C_RST}\n"
else
  printf "  ${C_OK}CLEAN${C_RST}  (no current alert)\n"
fi

# ───── 3. Intervention trend ───────────────────────────────────────
print_hdr "Intervention trend (7-day)"
if [[ -f "$REPO_ROOT/docs/metrics/intervention.json" ]] && command -v jq >/dev/null 2>&1; then
  jq -r '
    .trend_7d as $t |
    "  user-ratio %    : " + (($t.user_ratio_pct.last7_avg|tostring) +
       (if $t.user_ratio_pct.delta != null then " (Δ " + ($t.user_ratio_pct.delta|tostring) + " vs prior 7d)" else " (no prior window yet)" end)),
    "  leverage ratio  : " + (($t.leverage_ratio.last7_avg|tostring) +
       (if $t.leverage_ratio.delta != null then " (Δ " + ($t.leverage_ratio.delta|tostring) + ")" else "" end)),
    "  prompts / day   : " + (($t.operator_prompts.last7_avg|tostring) +
       (if $t.operator_prompts.delta != null then " (Δ " + ($t.operator_prompts.delta|tostring) + ")" else "" end)),
    "  active min / day: " + (($t.active_session_minutes.last7_avg|tostring) +
       (if $t.active_session_minutes.delta != null then " (Δ " + ($t.active_session_minutes.delta|tostring) + ")" else "" end)),
    (if ($t.direction|length) > 0 then "  direction: " + ($t.direction|join(" · ")) else "" end)
  ' "$REPO_ROOT/docs/metrics/intervention.json"
  printf "  ${C_DIM}→ docs/metrics/intervention.png${C_RST}\n"
else
  printf "  (intervention.json missing or jq unavailable)\n"
fi

# ───── 4. Commits since window ─────────────────────────────────────
print_hdr "Commits in last window (${SINCE})"
since_arg="--since=$SINCE"
n_commits=$(git log "$since_arg" --oneline --no-merges 2>/dev/null | wc -l | tr -d ' ')
printf "  %d commits total\n" "$n_commits"

if (( n_commits > 0 )); then
  # Quick attribution: count commits whose body matches user patterns.
  # Use the same heuristic as scripts/generate-intervention-chart.py
  # (lightweight version for stdout).
  user_count=0
  agent_count=0
  while IFS= read -r sha; do
    [[ -z "$sha" ]] && continue
    body=$(git log -1 --format=%B "$sha" 2>/dev/null)
    if echo "$body" | grep -qiE '^Requested-by:\s*user\s*$|operator (surfaced|flagged|picked|asked|requested|chose|surveys?|said|feedback|approved|directs?)|user (surfaced|flagged|picked|asked|requested|chose)|operator-(asked|flagged|requested|driven|surfaced)' \
       || echo "$body" | grep -q '["“][[:space:]]*[가-힣]'; then
      user_count=$((user_count + 1))
    else
      agent_count=$((agent_count + 1))
    fi
  done < <(git log "$since_arg" --no-merges --pretty=format:%H 2>/dev/null)
  printf "  attribution: ${C_OK}%d agent${C_RST} / ${C_WARN}%d user${C_RST}\n" "$agent_count" "$user_count"
  printf "  ${C_DIM}most recent 5:${C_RST}\n"
  git log "$since_arg" --no-merges --oneline 2>/dev/null | head -5 | sed 's/^/    /'
fi

# ───── 5. Today's autonomous decisions ─────────────────────────────
print_hdr "Today's autonomous decisions"
if [[ -f "$REPO_ROOT/docs/autonomous-decisions.md" ]]; then
  today=$(TZ=Asia/Seoul date +%Y-%m-%d)
  # Extract today's section.
  awk -v today="$today" '
    /^## / {in_today = 0}
    /^## / && match($0, "^## " today) { in_today = 1; next }
    in_today && /^- / { print "  " $0 }
  ' "$REPO_ROOT/docs/autonomous-decisions.md" | head -20
  if ! grep -q "^## $today" "$REPO_ROOT/docs/autonomous-decisions.md"; then
    printf "  ${C_DIM}(no entries for today)${C_RST}\n"
  fi
else
  printf "  (autonomous-decisions.md missing)\n"
fi

# ───── 6. Review queue ─────────────────────────────────────────────
print_hdr "Review queue"
if [[ -d "$REPO_ROOT/outputs/review-queue/pending" ]]; then
  n_pending=$(find "$REPO_ROOT/outputs/review-queue/pending" -name "*.json" 2>/dev/null | wc -l | tr -d ' ')
  if (( n_pending > 0 )); then
    printf "  ${C_WARN}%d pending${C_RST}  → scripts/review-queue-digest.sh\n" "$n_pending"
  else
    printf "  ${C_OK}empty${C_RST}\n"
  fi
else
  printf "  (no pending/ directory)\n"
fi

# ───── 7. Blockers ─────────────────────────────────────────────────
print_hdr "Blockers"
today_dir="$REPO_ROOT/records/blockers/$(date +%Y-%m-%d)"
if [[ -d "$today_dir" ]] && [[ -n "$(find "$today_dir" -type f 2>/dev/null)" ]]; then
  printf "  ${C_ERR}%d new${C_RST}  → records/blockers/$(date +%Y-%m-%d)/\n" \
    "$(find "$today_dir" -type f | wc -l | tr -d ' ')"
else
  printf "  ${C_OK}none today${C_RST}\n"
fi

printf "\n%s%s═════════════════════════════════════════%s\n" "$C_DIM" "$C_HDR" "$C_RST"
printf "%snext step: read docs/daily/$(date +%Y-%m-%d)*.md for narrative,%s\n" "$C_DIM" "$C_RST"
printf "%s            or scan docs/roadmap.md 'Now' section.%s\n" "$C_DIM" "$C_RST"
