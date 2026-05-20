#!/usr/bin/env bash
# scripts/interview-prep.sh - Phase 2.5 interview-question predictor.
#
# Given a posting + operator profile, produces a tailored interview
# prep doc: likely questions (technical / behavioral / scenario),
# operator-strengths-based talking points, gap-mitigation strategies,
# and operator-side questions to ask the interviewer.
#
# Status (2026-05-20): SCAFFOLD MODE BY DEFAULT.  Gated on
# JH_INTERVIEW_PREP_LIVE=1.  Mirrors fit-score / cover-letter /
# company-research scaffold pattern.
#
# Cost shape (live):
#   ~500-700 input tokens (posting + profile)
#   ~800-1200 output tokens (multi-section prep doc)
#   ~0 USD on Max plan.
#
# Usage:
#   echo '<posting-json>' | scripts/interview-prep.sh
#   scripts/interview-prep.sh path/to/posting.json
#
# Env:
#   JH_INTERVIEW_PREP_LIVE=1   issue the Claude call
#   JH_PROFILE_PATH            override operator profile path
#   JH_PREP_STAGE              phone-screen | tech | onsite (default: tech)
#
# Exit codes: 0 success, 2 config, 3 live call failed, 10 scaffold.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

PROFILE_PATH="${JH_PROFILE_PATH:-$SKILL_DIR/config/operator-profile.md}"
PREP_STAGE="${JH_PREP_STAGE:-tech}"
POSTING_SRC=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --profile=*)  PROFILE_PATH="${1#--profile=}"; shift ;;
    --profile)    PROFILE_PATH="${2:-}"; shift 2 ;;
    --stage=*)    PREP_STAGE="${1#--stage=}"; shift ;;
    --stage)      PREP_STAGE="${2:-}"; shift 2 ;;
    --help|-h)
      printf '%s\n' \
        "interview-prep.sh - Claude-generated interview prep (scaffold mode by default)." \
        "" \
        "Env:" \
        "  JH_INTERVIEW_PREP_LIVE=1   issue the Claude call" \
        "  JH_PREP_STAGE              phone-screen | tech | onsite (default: tech)" \
        "  JH_PROFILE_PATH            override operator profile path" \
        "" \
        "Output sections:" \
        "  - Likely questions (technical / behavioral / scenario)" \
        "  - Strength-based talking points to weave in" \
        "  - Gap-mitigation strategies" \
        "  - Questions for the operator to ask the interviewer" \
        "  - Day-of checklist (logistics + profile-tuned items)"
      exit 0
      ;;
    -*)           echo "[interview-prep] unknown flag: $1" >&2; exit 2 ;;
    *)            POSTING_SRC="$1"; shift ;;
  esac
done

# ----- load posting -----
if [[ -n "$POSTING_SRC" ]]; then
  [[ -f "$POSTING_SRC" ]] || { echo "[interview-prep] posting file not found: $POSTING_SRC" >&2; exit 2; }
  posting_json=$(cat "$POSTING_SRC")
else
  posting_json=$(cat)
fi
echo "$posting_json" | jq -e '.title and .company and .url' >/dev/null 2>&1 || {
  echo "[interview-prep] posting JSON missing required fields" >&2
  exit 2
}

# ----- load operator profile -----
if [[ ! -f "$PROFILE_PATH" ]]; then
  example_path="$SKILL_DIR/config/operator-profile.example.md"
  if [[ "${JH_INTERVIEW_PREP_LIVE:-0}" != "1" && -f "$example_path" ]]; then
    echo "[interview-prep] profile not found at $PROFILE_PATH - scaffold mode falling back to $example_path" >&2
    PROFILE_PATH="$example_path"
  else
    echo "[interview-prep] operator profile not found: $PROFILE_PATH" >&2
    exit 2
  fi
fi
profile_text=$(cat "$PROFILE_PATH")

# ----- compose prompt (printf-based to sidestep heredoc-in-subshell quirks) -----
posting_title=$(echo "$posting_json" | jq -r '.title')
posting_company=$(echo "$posting_json" | jq -r '.company')
posting_summary=$(echo "$posting_json" | jq -r '.summary // ""')

system_prompt="You produce concrete, tailored interview prep - likely questions tied to the specific posting, company, and operator profile. Output is markdown. Prefer practical scenario questions over CS-fundamentals trivia. Surface the operator gap items honestly and propose mitigation, do not hide them."

user_prompt=$(printf '%s\n' \
  "Produce interview prep for the operator.  Markdown output." \
  "~400-600 words across the sections below." \
  "" \
  "POSTING" \
  "title: $posting_title" \
  "company: $posting_company" \
  "summary: $posting_summary" \
  "" \
  "OPERATOR PROFILE" \
  "$profile_text" \
  "" \
  "INTERVIEW STAGE: $PREP_STAGE" \
  "  (phone-screen = ~30 min screening; tech = ~60 min technical/portfolio" \
  "   conversation; onsite = ~2-4 hours, multi-interview loop)" \
  "" \
  "Required sections (use these headings):" \
  "" \
  "## Likely questions (5-8 items)" \
  "Mix of:" \
  "- 1-2 background questions (career narrative, why this role)" \
  "- 2-3 scenario questions calibrated to this company likely product context" \
  "- 1-2 questions probing the operator gap items (per their profile)" \
  "- 1 reverse - what would you ask the operator to assess fit" \
  "" \
  "For each question, add a 1-line note on what the interviewer is" \
  "likely probing for." \
  "" \
  "## Strength-based talking points (3-4 items)" \
  "Concrete artifact-backed stories the operator should be ready to" \
  "tell, mapped to specific posting requirements.  Use the profile" \
  "artifact links." \
  "" \
  "## Gap-mitigation strategies (1-3 items)" \
  "For each gap the profile flags that is likely to come up in this" \
  "interview, suggest a concrete mitigation framing - what does the" \
  "operator actually say when the gap is probed.  No hand-waving." \
  "" \
  "## Questions for the operator to ask" \
  "3-5 questions the operator should ask the interviewer.  Calibrated" \
  "to the stage:" \
  "- phone-screen: high-level fit + process" \
  "- tech: specific work patterns, tooling, decision-making" \
  "- onsite: team dynamics, growth, risk factors" \
  "" \
  "## Day-of checklist" \
  "- Logistics (time, location/link, who is interviewing)" \
  "- Profile-tuned reminders (e.g., if profile says avoid humble-brag," \
  "  remind the operator)" \
)

if [[ "${JH_INTERVIEW_PREP_LIVE:-0}" != "1" ]]; then
  printf '%s\n' \
    "[interview-prep] SCAFFOLD MODE - JH_INTERVIEW_PREP_LIVE not set.  No Claude call." \
    "[interview-prep] Preview of what would be sent to Claude follows on stdout." \
    "[interview-prep] Set JH_INTERVIEW_PREP_LIVE=1 to issue the live call." >&2
  jq -n \
    --arg sys "$system_prompt" \
    --arg usr "$user_prompt" \
    '{scaffold_mode: true, would_send: {model: "claude-sonnet-4-6", system: $sys, user: $usr}}'
  exit 10
fi

if ! command -v claude >/dev/null 2>&1; then
  echo "[interview-prep] JH_INTERVIEW_PREP_LIVE=1 but claude CLI not on PATH" >&2
  exit 3
fi

response=$(echo "$user_prompt" | claude \
  --model claude-sonnet-4-6 \
  --append-system-prompt "$system_prompt" \
  --output-format text 2>/dev/null) || {
    echo "[interview-prep] claude CLI failed" >&2
    exit 3
}

echo "$response"
