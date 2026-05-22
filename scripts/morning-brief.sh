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
#   scripts/morning-brief.sh --lang ko             # Korean labels
#
# Defaults: --lang derives from $LANG / $LC_ALL (ko_KR* → ko, else en).
# Reads only — never modifies anything.  Safe to run any number of times.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

# Argument parsing: --lang and --since (positional fallback for --since).
LANG_OVERRIDE=""
SINCE="12 hours ago"
while (( $# )); do
  case "$1" in
    --lang) LANG_OVERRIDE="$2"; shift 2 ;;
    --lang=*) LANG_OVERRIDE="${1#*=}"; shift ;;
    --since) SINCE="$2"; shift 2 ;;
    --since=*) SINCE="${1#*=}"; shift ;;
    *) shift ;;
  esac
done

# Resolve language: explicit override > LANG/LC_ALL > en.
brief_lang="en"
if [[ -n "$LANG_OVERRIDE" ]]; then
  brief_lang="$LANG_OVERRIDE"
elif [[ "${LANG:-}${LC_ALL:-}" =~ ^ko_KR|.*\.ko_KR|^ko\. ]]; then
  brief_lang="ko"
fi

# Bilingual label dict.  Each section heading + recurring inline label.
if [[ "$brief_lang" == "ko" ]]; then
  L_TITLE="모닝 브리핑"
  L_HEALTH="헬스 체크 (scripts/doctor.sh)"
  L_AUDIT="감사"
  L_AUDIT_CLEAN="CLEAN  (현재 알림 없음)"
  L_TREND="개입 추세 (7일)"
  L_TREND_USER="운영자 주도 %  : "
  L_TREND_LEVERAGE="레버리지 비율  : "
  L_TREND_PROMPTS="프롬프트 / 일  : "
  L_TREND_MINUTES="활성 분 / 일   : "
  L_TREND_NOPREV=" (이전 7일 데이터 없음)"
  L_COMMITS="최근 윈도 커밋"
  L_COMMITS_TOTAL="commits total"
  L_COMMITS_ATTR="%d 에이전트 / %d 운영자\n"
  L_COMMITS_RECENT="최근 5건:"
  L_DECISIONS="오늘의 자율 결정"
  L_DECISIONS_NONE="(오늘 entry 없음)"
  L_REVIEW="리뷰 큐"
  L_REVIEW_PENDING="대기 중  → scripts/review-queue-digest.sh"
  L_REVIEW_EMPTY="비어 있음"
  L_BLOCKERS="블로커"
  L_BLOCKERS_NEW="개 신규  → records/blockers/$(date +%Y-%m-%d)/"
  L_BLOCKERS_NONE="오늘 없음"
  L_NEXT_LINE1="다음: docs/daily/$(date +%Y-%m-%d)*.md 의 narrative 읽기,"
  L_NEXT_LINE2="       또는 docs/roadmap.md 'Now' 섹션 스캔."
else
  L_TITLE="Morning brief"
  L_HEALTH="Health (scripts/doctor.sh)"
  L_AUDIT="Audit"
  L_AUDIT_CLEAN="CLEAN  (no current alert)"
  L_TREND="Intervention trend (7-day)"
  L_TREND_USER="user-ratio %    : "
  L_TREND_LEVERAGE="leverage ratio  : "
  L_TREND_PROMPTS="prompts / day   : "
  L_TREND_MINUTES="active min / day: "
  L_TREND_NOPREV=" (no prior window yet)"
  L_COMMITS="Commits in last window"
  L_COMMITS_TOTAL="commits total"
  L_COMMITS_ATTR="%d agent / %d user\n"
  L_COMMITS_RECENT="most recent 5:"
  L_DECISIONS="Today's autonomous decisions"
  L_DECISIONS_NONE="(no entries for today)"
  L_REVIEW="Review queue"
  L_REVIEW_PENDING="pending  → scripts/review-queue-digest.sh"
  L_REVIEW_EMPTY="empty"
  L_BLOCKERS="Blockers"
  L_BLOCKERS_NEW="new  → records/blockers/$(date +%Y-%m-%d)/"
  L_BLOCKERS_NONE="none today"
  L_NEXT_LINE1="next step: read docs/daily/$(date +%Y-%m-%d)*.md for narrative,"
  L_NEXT_LINE2="            or scan docs/roadmap.md 'Now' section."
fi

# Colors when stdout is a TTY.
if [[ -t 1 ]]; then
  C_HDR=$'\033[1;36m'; C_OK=$'\033[32m'; C_WARN=$'\033[33m'; C_ERR=$'\033[31m'
  C_DIM=$'\033[2m'; C_RST=$'\033[0m'; C_BOLD=$'\033[1m'
else
  C_HDR=""; C_OK=""; C_WARN=""; C_ERR=""; C_DIM=""; C_RST=""; C_BOLD=""
fi

print_hdr() { printf "%s──── %s ────%s\n" "$C_HDR" "$1" "$C_RST"; }

printf "%s%s   %s   %s\n" "$C_BOLD" "$C_HDR" "$L_TITLE" "$C_RST"
printf "%s%s   $(TZ=Asia/Seoul date '+%Y-%m-%d %H:%M KST')   %s%s\n\n" "$C_DIM" "$C_HDR" "$C_RST" "$C_RST"

# ───── 1. Doctor verdict ───────────────────────────────────────────
print_hdr "$L_HEALTH"
if [[ -x "$SCRIPT_DIR/doctor.sh" ]]; then
  out=$("$SCRIPT_DIR/doctor.sh" --quiet 2>/dev/null || true)
  printf "  %s\n" "$out"
else
  printf "  (doctor.sh not found)\n"
fi

# ───── 2. Audit alert state ────────────────────────────────────────
print_hdr "$L_AUDIT"
if [[ -f "$REPO_ROOT/docs/audit/CURRENT-ALERT.md" ]]; then
  verdict=$(grep -m1 '^\*\*Verdict\*\*:' "$REPO_ROOT/docs/audit/CURRENT-ALERT.md" | sed 's/\*\*//g' | sed 's/Verdict://')
  generated=$(grep -m1 '^\*\*Generated\*\*:' "$REPO_ROOT/docs/audit/CURRENT-ALERT.md" | sed 's/\*\*//g')
  printf "  %s${C_WARN}%s${C_RST}\n" "$verdict" ""
  printf "  ${C_DIM}%s${C_RST}\n" "$generated"
  printf "  ${C_DIM}→ docs/audit/CURRENT-ALERT.md${C_RST}\n"
else
  printf "  ${C_OK}%s${C_RST}\n" "$L_AUDIT_CLEAN"
fi

# ───── 3. Intervention trend ───────────────────────────────────────
print_hdr "$L_TREND"
if [[ -f "$REPO_ROOT/docs/metrics/intervention.json" ]] && command -v jq >/dev/null 2>&1; then
  jq -r --arg lU "$L_TREND_USER" --arg lL "$L_TREND_LEVERAGE" \
        --arg lP "$L_TREND_PROMPTS" --arg lM "$L_TREND_MINUTES" \
        --arg noprev "$L_TREND_NOPREV" '
    .trend_7d as $t |
    "  " + $lU + (($t.user_ratio_pct.last7_avg|tostring) +
       (if $t.user_ratio_pct.delta != null then " (Δ " + ($t.user_ratio_pct.delta|tostring) + ")" else $noprev end)),
    "  " + $lL + (($t.leverage_ratio.last7_avg|tostring) +
       (if $t.leverage_ratio.delta != null then " (Δ " + ($t.leverage_ratio.delta|tostring) + ")" else "" end)),
    "  " + $lP + (($t.operator_prompts.last7_avg|tostring) +
       (if $t.operator_prompts.delta != null then " (Δ " + ($t.operator_prompts.delta|tostring) + ")" else "" end)),
    "  " + $lM + (($t.active_session_minutes.last7_avg|tostring) +
       (if $t.active_session_minutes.delta != null then " (Δ " + ($t.active_session_minutes.delta|tostring) + ")" else "" end)),
    (if ($t.direction|length) > 0 then "  direction: " + ($t.direction|join(" · ")) else "" end)
  ' "$REPO_ROOT/docs/metrics/intervention.json"
  printf "  ${C_DIM}→ docs/metrics/intervention.png${C_RST}\n"
else
  printf "  (intervention.json missing or jq unavailable)\n"
fi

# ───── 4. Commits since window ─────────────────────────────────────
print_hdr "$L_COMMITS (${SINCE})"
since_arg="--since=$SINCE"
n_commits=$(git log "$since_arg" --oneline --no-merges 2>/dev/null | wc -l | tr -d ' ')
printf "  %d %s\n" "$n_commits" "$L_COMMITS_TOTAL"

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
  printf "  ${C_OK}attribution${C_RST}: " ; printf "$L_COMMITS_ATTR" "$agent_count" "$user_count"
  printf "  ${C_DIM}%s${C_RST}\n" "$L_COMMITS_RECENT"
  git log "$since_arg" --no-merges --oneline 2>/dev/null | head -5 | sed 's/^/    /'
fi

# ───── 5. Today's autonomous decisions ─────────────────────────────
print_hdr "$L_DECISIONS"
if [[ -f "$REPO_ROOT/docs/autonomous-decisions.md" ]]; then
  today=$(TZ=Asia/Seoul date +%Y-%m-%d)
  # Extract today's section.
  awk -v today="$today" '
    /^## / {in_today = 0}
    /^## / && match($0, "^## " today) { in_today = 1; next }
    in_today && /^- / { print "  " $0 }
  ' "$REPO_ROOT/docs/autonomous-decisions.md" | head -20
  if ! grep -q "^## $today" "$REPO_ROOT/docs/autonomous-decisions.md"; then
    printf "  ${C_DIM}%s${C_RST}\n" "$L_DECISIONS_NONE"
  fi
else
  printf "  (autonomous-decisions.md missing)\n"
fi

# ───── 6. Review queue ─────────────────────────────────────────────
print_hdr "$L_REVIEW"
if [[ -d "$REPO_ROOT/outputs/review-queue/pending" ]]; then
  n_pending=$(find "$REPO_ROOT/outputs/review-queue/pending" -name "*.json" 2>/dev/null | wc -l | tr -d ' ')
  if (( n_pending > 0 )); then
    printf "  ${C_WARN}%d${C_RST} %s\n" "$n_pending" "$L_REVIEW_PENDING"
  else
    printf "  ${C_OK}%s${C_RST}\n" "$L_REVIEW_EMPTY"
  fi
else
  printf "  (no pending/ directory)\n"
fi

# ───── 7. Blockers ─────────────────────────────────────────────────
print_hdr "$L_BLOCKERS"
today_dir="$REPO_ROOT/records/blockers/$(date +%Y-%m-%d)"
if [[ -d "$today_dir" ]] && [[ -n "$(find "$today_dir" -type f 2>/dev/null)" ]]; then
  printf "  ${C_ERR}%d${C_RST} %s\n" \
    "$(find "$today_dir" -type f | wc -l | tr -d ' ')" "$L_BLOCKERS_NEW"
else
  printf "  ${C_OK}%s${C_RST}\n" "$L_BLOCKERS_NONE"
fi

printf "\n%s%s═════════════════════════════════════════%s\n" "$C_DIM" "$C_HDR" "$C_RST"
printf "%s%s%s\n" "$C_DIM" "$L_NEXT_LINE1" "$C_RST"
printf "%s%s%s\n" "$C_DIM" "$L_NEXT_LINE2" "$C_RST"
