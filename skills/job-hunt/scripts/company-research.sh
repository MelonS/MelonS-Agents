#!/usr/bin/env bash
# scripts/company-research.sh — Phase 2.5 company brief.
#
# Reads a company name (and optionally a posting summary to give
# context) and produces a short structured brief: one-line summary,
# product/domain, team size estimate, recent signals, engineering
# culture hints, risk factors.  Useful as a pre-application
# briefing or pre-interview cram doc.
#
# Status (2026-05-20): SCAFFOLD MODE BY DEFAULT.  Gated on
# JH_COMPANY_RESEARCH_LIVE=1.  Mirrors fit-score.sh + cover-letter
# patterns.
#
# Cost shape (live):
#   ~200 input tokens (company name + context)
#   ~400-600 output tokens (structured brief)
#   ~0 USD on Max plan; one call per company.
#
# Note on factual accuracy: Claude's knowledge cutoff means recent
# events (last 3-6 months) may be missing.  The brief explicitly
# flags fields where knowledge is stale and recommends operator
# verification.
#
# Usage:
#   scripts/company-research.sh "company name"
#   scripts/company-research.sh --posting=<json> "company name"
#   echo '<posting-json>' | scripts/company-research.sh
#
# Env:
#   JH_COMPANY_RESEARCH_LIVE=1   issue the Claude call
#
# Exit codes: 0 success, 2 config, 3 live call failed, 10 scaffold.

set -uo pipefail

COMPANY=""
POSTING_SRC=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --posting=*)  POSTING_SRC="${1#--posting=}"; shift ;;
    --posting)    POSTING_SRC="${2:-}"; shift 2 ;;
    --help|-h)
      cat <<'EOF'
company-research.sh — Claude-generated company brief (scaffold mode by default).

  scripts/company-research.sh "Rebeat"
  scripts/company-research.sh --posting=path/to/posting.json "Hackle"
  echo '<posting-json>' | scripts/company-research.sh        # company name pulled from posting.company

Env:
  JH_COMPANY_RESEARCH_LIVE=1   issue the Claude call

Output sections:
  - One-line summary
  - Product / domain
  - Team size estimate
  - Recent signals (news, funding, hiring)
  - Engineering culture hints
  - Risk factors
  - Stale-data flags + operator-verification recommendation
EOF
      exit 0
      ;;
    -*)           echo "[company-research] unknown flag: $1" >&2; exit 2 ;;
    *)            COMPANY="$1"; shift ;;
  esac
done

# ----- derive company name from posting if not given as arg -----
if [[ -z "$COMPANY" ]]; then
  if [[ -n "$POSTING_SRC" ]]; then
    [[ -f "$POSTING_SRC" ]] || { echo "[company-research] posting file not found: $POSTING_SRC" >&2; exit 2; }
    posting_json=$(cat "$POSTING_SRC")
  else
    # Read from stdin (orchestrator pipe).
    posting_json=$(cat)
  fi
  COMPANY=$(echo "$posting_json" | jq -r '.company // empty')
  if [[ -z "$COMPANY" ]]; then
    echo "[company-research] no company name given and posting carries no .company field" >&2
    exit 2
  fi
fi

# Optional context from posting summary.
posting_context=""
if [[ -n "${posting_json:-}" ]]; then
  posting_context=$(echo "$posting_json" | jq -r '"role posted: \(.title // "")\nposting summary: \(.summary // "")"')
fi

# ----- compose prompt -----
system_prompt="You produce concise, factual company briefs for an operator considering applying. Sections are fixed; output is plain markdown; do NOT inflate claims or invent data — explicitly flag fields where your knowledge is stale or absent and recommend operator verification."

user_prompt=$(cat <<EOF
Produce a company brief for the operator.  Markdown output.
~250-400 words total.

COMPANY: $COMPANY

${posting_context:+CONTEXT FROM POSTING\n$posting_context\n}

Required sections (use these exact headings):

## One-liner
(one sentence — what does the company do?)

## Product / domain
(2-3 sentences — what's their actual product, who's the customer,
what stage are they at?)

## Team / stage
(team size if known, funding stage if known, location.  If unknown,
say so explicitly and recommend operator verification.)

## Recent signals
(news / funding / hiring patterns in the last ~12 months.  Flag
stale-data risk clearly.)

## Engineering culture hints
(if there are observable signals — public blog, engineering posts,
GitHub presence, conference talks — list them.  If not, say so.)

## Risk factors
(2-3 bullets on things the operator should consider before applying:
late-stage uncertainty, recent layoffs, culture mismatch flags,
domain incompatibility with operator's stated anti-targets, etc.)

## Verification recommended
(end with a 1-line note: which sections the operator should verify
against the company's current website / LinkedIn / news search
before relying on this brief.)
EOF
)

if [[ "${JH_COMPANY_RESEARCH_LIVE:-0}" != "1" ]]; then
  cat <<EOF >&2
[company-research] SCAFFOLD MODE — JH_COMPANY_RESEARCH_LIVE not set.  No Claude call.
[company-research] Preview of what would be sent to Claude follows on stdout.
[company-research] Set JH_COMPANY_RESEARCH_LIVE=1 to issue the live call.
EOF
  jq -n \
    --arg sys "$system_prompt" \
    --arg usr "$user_prompt" \
    '{scaffold_mode: true, would_send: {model: "claude-sonnet-4-6", system: $sys, user: $usr}}'
  exit 10
fi

if ! command -v claude >/dev/null 2>&1; then
  echo "[company-research] JH_COMPANY_RESEARCH_LIVE=1 but \`claude\` CLI not on PATH" >&2
  exit 3
fi

# Sonnet for the factual / nuanced writing.  Haiku risks
# overconfident claims about company-specific facts.
response=$(echo "$user_prompt" | claude \
  --model claude-sonnet-4-6 \
  --append-system-prompt "$system_prompt" \
  --output-format text 2>/dev/null) || {
    echo "[company-research] claude CLI failed" >&2
    exit 3
}

echo "$response"
