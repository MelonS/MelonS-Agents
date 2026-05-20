#!/usr/bin/env bash
# scripts/fit-score.sh — per-posting fit score via Claude (Tier-1).
#
# Reads a single posting (JSON on stdin or as $1) and an operator
# profile file ($JH_PROFILE_PATH, default
# skills/job-hunt/config/operator-profile.md).  Emits a fit-score
# JSON block on stdout: { score, strengths, gaps, rationale }.
#
# Status (2026-05-20): SCAFFOLD MODE BY DEFAULT.  The Claude call
# is gated on JH_FIT_SCORE_LIVE=1.  Without the flag, prints the
# would-send context for operator review without burning any
# Anthropic tokens.  Matches the kr-*-live-flag pattern from
# Phase 2.1: the live HTTP path is written and ready, awaiting
# operator validation + budget OK.
#
# Cost shape (when live):
#   Tier-1 call per posting: ~200-400 input tokens
#                            ~100-200 output tokens
#   Typical posting budget: ~0 USD on the operator's Max plan
#                            (no incremental dollar; subscription quota).
#   Caller controls cardinality via the orchestrator's posting cap.
#
# Usage:
#   echo '<posting-json>' | scripts/fit-score.sh
#   scripts/fit-score.sh <posting-json-file>
#   scripts/fit-score.sh --profile=path/to/profile.md <posting-json-file>
#
# Env:
#   JH_FIT_SCORE_LIVE=1      Issue the Claude call.  Default: scaffold mode.
#   JH_PROFILE_PATH=<path>   Override operator profile location.
#
# Exit codes:
#   0   score JSON written to stdout
#   2   config error (no profile file, malformed posting, etc.)
#   3   Claude call failed (when JH_FIT_SCORE_LIVE=1)
#   10  scaffold-mode marker (preview printed; no live call attempted)

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

PROFILE_PATH="${JH_PROFILE_PATH:-$SKILL_DIR/config/operator-profile.md}"
POSTING_SRC=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --profile=*)  PROFILE_PATH="${1#--profile=}"; shift ;;
    --profile)    PROFILE_PATH="${2:-}"; shift 2 ;;
    --help|-h)
      cat <<'EOF'
fit-score.sh — per-posting Claude fit-scoring (scaffold mode by default).

  echo '<posting-json>' | scripts/fit-score.sh
  scripts/fit-score.sh path/to/posting.json
  scripts/fit-score.sh --profile=path/to/profile.md path/to/posting.json

Env:
  JH_FIT_SCORE_LIVE=1   issue the Claude call (Tier-1)
  JH_PROFILE_PATH       override operator profile path

Without the LIVE flag, prints a preview of the prompt + posting
context that *would* be sent to Claude, so operators can review
before committing budget.
EOF
      exit 0
      ;;
    -*)           echo "[fit-score] unknown flag: $1" >&2; exit 2 ;;
    *)            POSTING_SRC="$1"; shift ;;
  esac
done

# ----- load posting -----
if [[ -n "$POSTING_SRC" ]]; then
  if [[ ! -f "$POSTING_SRC" ]]; then
    echo "[fit-score] posting file not found: $POSTING_SRC" >&2
    exit 2
  fi
  posting_json=$(cat "$POSTING_SRC")
else
  posting_json=$(cat)
fi

if ! echo "$posting_json" | jq -e '.title and .company and .url' >/dev/null 2>&1; then
  echo "[fit-score] posting JSON missing required fields (title, company, url)" >&2
  exit 2
fi

# ----- load operator profile -----
# In scaffold mode (no live Claude call) the example file is an
# acceptable fallback so the preview is meaningful out of the box.
# In live mode the real operator-profile.md is required — scoring
# against the example template's placeholder strengths is misleading.
if [[ ! -f "$PROFILE_PATH" ]]; then
  example_path="$SKILL_DIR/config/operator-profile.example.md"
  if [[ "${JH_FIT_SCORE_LIVE:-0}" != "1" && -f "$example_path" ]]; then
    echo "[fit-score] operator profile not found at $PROFILE_PATH" >&2
    echo "[fit-score] scaffold mode — falling back to $example_path" >&2
    PROFILE_PATH="$example_path"
  else
    echo "[fit-score] operator profile not found: $PROFILE_PATH" >&2
    echo "[fit-score] copy skills/job-hunt/config/operator-profile.example.md → operator-profile.md and edit." >&2
    exit 2
  fi
fi
profile_text=$(cat "$PROFILE_PATH")

# ----- load company-tier table (optional input to hire_prob) -----
TIERS_PATH="$SKILL_DIR/data/company-tiers.yaml"
tier_text=""
if [[ -f "$TIERS_PATH" ]]; then
  tier_text=$(cat "$TIERS_PATH")
fi

# ----- compose prompt -----
posting_title=$(echo "$posting_json" | jq -r '.title')
posting_company=$(echo "$posting_json" | jq -r '.company')
posting_region=$(echo "$posting_json" | jq -r '.region // ""')
posting_summary=$(echo "$posting_json" | jq -r '.summary // ""')
posting_url=$(echo "$posting_json" | jq -r '.url')

# System message stays small (orchestration intent only).  Response
# asks Claude to emit a strict JSON block so the orchestrator can
# parse without LLM ambiguity.
system_prompt="You score job postings against an operator's profile and emit a strict JSON block."

user_prompt=$(cat <<'INNER_EOF'
Score how well this job posting fits the operator profile below.

You must produce TWO scores that together rank "best company the
operator can plausibly get into":

  - role_fit    (0-100) -- does the posting actual work + required
                          skills match what the operator does well?
                          ignore prestige here; this is a pure
                          match-the-work-to-the-person score.
  - hire_prob   (0-100) -- how likely is the operator to actually
                          PASS this company hiring bar?  high
                          prestige + huge applicant funnel + senior-
                          only specs -> low.  domestic + matching
                          tier + standard process -> high.  reflect
                          the gap between the operator level and
                          the company hire-bar honestly.

Final composite = round(role_fit * 0.6 + hire_prob * 0.4).

Return ONLY a JSON object on a single line.  No prose outside the JSON.

Schema:
{
  "score": <composite integer 0-100>,
  "role_fit": <integer 0-100>,
  "hire_prob": <integer 0-100>,
  "strengths": ["<concrete strength match>", ...up to 3 items],
  "gaps": ["<concrete gap or risk>", ...up to 3 items],
  "rationale": "<one short sentence summarising the composite score>"
}

POSTING
title: __POSTING_TITLE__
company: __POSTING_COMPANY__
region: __POSTING_REGION__
url: __POSTING_URL__
summary: __POSTING_SUMMARY__

OPERATOR PROFILE
__PROFILE_TEXT__

COMPANY-TIER TABLE (optional anchor for hire_prob — empty if not present)
__TIER_TEXT__

Score role_fit based on:
- does the posting actual work match the profile role target?
- do the operator listed strengths cover the posting required +
  preferred items?
- gap honesty: do not inflate if the profile explicitly flags a gap
  the posting requires.
- region / constraint compatibility (location, visa, employment type).

Score hire_prob based on:
- company tier vs operator stated hire-bar comfort (foundation
  labs / FAANG / unicorn / KR-domestic large / growth-stage / early-
  stage -- increasing hire probability roughly in that order).
- seniority gap: senior-only spec on a posting where the operator
  is mid-level -> low hire_prob.  Reverse -> high.
- applicant funnel: globally famous AI labs have orders of magnitude
  more applicants than equally interesting but less-famous companies.
- domain match: a domestic mid-tier player matching the operator
  background often beats a frontier-lab role the operator is
  underqualified for on the "best company they can plausibly get
  into" axis.
INNER_EOF
)
# Interpolate variables into the template (the heredoc above was quoted to
# avoid having to worry about shell-meta in the prompt body).
user_prompt=${user_prompt//__POSTING_TITLE__/$posting_title}
user_prompt=${user_prompt//__POSTING_COMPANY__/$posting_company}
user_prompt=${user_prompt//__POSTING_REGION__/$posting_region}
user_prompt=${user_prompt//__POSTING_URL__/$posting_url}
user_prompt=${user_prompt//__POSTING_SUMMARY__/$posting_summary}
user_prompt=${user_prompt//__PROFILE_TEXT__/$profile_text}
user_prompt=${user_prompt//__TIER_TEXT__/$tier_text}

# ----- scaffold vs live -----
if [[ "${JH_FIT_SCORE_LIVE:-0}" != "1" ]]; then
  cat <<EOF >&2
[fit-score] SCAFFOLD MODE — JH_FIT_SCORE_LIVE not set.  No Claude call.
[fit-score] Preview of what would be sent to Claude follows on stdout.
[fit-score] Set JH_FIT_SCORE_LIVE=1 to issue the live call.
EOF
  jq -n \
    --arg sys "$system_prompt" \
    --arg usr "$user_prompt" \
    '{scaffold_mode: true, would_send: {model: "claude-haiku-4-5", system: $sys, user: $usr}}'
  exit 10
fi

# ----- live Claude call -----
if ! command -v claude >/dev/null 2>&1; then
  echo "[fit-score] JH_FIT_SCORE_LIVE=1 but \`claude\` CLI not on PATH" >&2
  exit 3
fi

# `claude` CLI consumes the user message via stdin and prints model
# output to stdout.  --append-system-prompt keeps the system msg
# small without polluting CLAUDE.md.  Output is plain text from
# Claude; we expect it to be valid JSON per our prompt constraint.
response=$(echo "$user_prompt" | claude \
  --model claude-haiku-4-5 \
  --append-system-prompt "$system_prompt" \
  --output-format text 2>/dev/null) || {
    echo "[fit-score] claude CLI failed" >&2
    exit 3
}

# Validate the response is parseable JSON matching the schema.
if ! echo "$response" | jq -e '.score and .strengths and .gaps and .rationale' >/dev/null 2>&1; then
  echo "[fit-score] response did not match expected schema:" >&2
  echo "$response" | head -10 >&2
  exit 3
fi

echo "$response"
