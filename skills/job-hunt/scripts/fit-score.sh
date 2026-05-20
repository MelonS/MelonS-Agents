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
if [[ ! -f "$PROFILE_PATH" ]]; then
  echo "[fit-score] operator profile not found: $PROFILE_PATH" >&2
  echo "[fit-score] copy skills/job-hunt/config/operator-profile.example.md → operator-profile.md and edit." >&2
  exit 2
fi
profile_text=$(cat "$PROFILE_PATH")

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

user_prompt=$(cat <<EOF
Score how well this job posting fits the operator profile below.
Return ONLY a JSON object on a single line.  No prose outside the JSON.

Schema:
{
  "score": <integer 0-100>,
  "strengths": ["<concrete strength match>", ...up to 3 items],
  "gaps": ["<concrete gap or risk>", ...up to 3 items],
  "rationale": "<one short sentence summarising the score>"
}

POSTING
title: $posting_title
company: $posting_company
region: $posting_region
url: $posting_url
summary: $posting_summary

OPERATOR PROFILE
$profile_text

Score based on:
- role-fit (does the posting's actual work match the profile's role target?)
- strengths alignment (does the operator's listed strengths cover the
  posting's required + preferred items?)
- gap-honesty (don't inflate the score if the operator's profile
  explicitly flags a gap that the posting requires)
- region/constraint compatibility
EOF
)

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
