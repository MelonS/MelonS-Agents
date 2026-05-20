#!/usr/bin/env bash
# scripts/cover-letter-draft.sh — Phase 2.5 cover-letter draft.
#
# Reads a single posting (JSON on stdin or as $1) and an operator
# profile, produces a 200-300 word cover-letter draft tuned to
# the posting and the operator's stated application-style
# preferences.  The output is a starting point, not a final
# submission — operator must edit before sending.
#
# Status (2026-05-20): SCAFFOLD MODE BY DEFAULT.  Gated on
# JH_COVER_LETTER_LIVE=1.  Mirrors the fit-score.sh pattern.
#
# Cost shape (live):
#   ~500-800 input tokens (posting + full profile)
#   ~300-500 output tokens (the letter body)
#   ~0 USD on Max plan; one call per posting.
#
# Usage:
#   echo '<posting-json>' | scripts/cover-letter-draft.sh
#   scripts/cover-letter-draft.sh path/to/posting.json
#
# Env:
#   JH_COVER_LETTER_LIVE=1   issue the Claude call
#   JH_PROFILE_PATH          override operator profile path
#   JH_COVER_TONE            "formal" | "casual" — defaults to "neutral"
#
# Exit codes: 0 success, 2 config, 3 live call failed, 10 scaffold.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

PROFILE_PATH="${JH_PROFILE_PATH:-$SKILL_DIR/config/operator-profile.md}"
TONE="${JH_COVER_TONE:-neutral}"
POSTING_SRC=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --profile=*)  PROFILE_PATH="${1#--profile=}"; shift ;;
    --profile)    PROFILE_PATH="${2:-}"; shift 2 ;;
    --tone=*)     TONE="${1#--tone=}"; shift ;;
    --tone)       TONE="${2:-}"; shift 2 ;;
    --help|-h)
      cat <<'EOF'
cover-letter-draft.sh — Claude-drafted cover letter (scaffold mode by default).

Env:
  JH_COVER_LETTER_LIVE=1  issue the Claude call
  JH_COVER_TONE           formal | neutral | casual (default: neutral)
  JH_PROFILE_PATH         override operator profile path

Output: 200-300 word markdown cover letter, ready to copy + edit.
The skill explicitly avoids: marketing superlatives, "passionate
about", "team player", humble-brag.  Operator's profile
"Application style preference" section tunes this further.
EOF
      exit 0
      ;;
    -*)           echo "[cover-letter] unknown flag: $1" >&2; exit 2 ;;
    *)            POSTING_SRC="$1"; shift ;;
  esac
done

# ----- load posting -----
if [[ -n "$POSTING_SRC" ]]; then
  [[ -f "$POSTING_SRC" ]] || { echo "[cover-letter] posting file not found: $POSTING_SRC" >&2; exit 2; }
  posting_json=$(cat "$POSTING_SRC")
else
  posting_json=$(cat)
fi
echo "$posting_json" | jq -e '.title and .company and .url' >/dev/null 2>&1 || {
  echo "[cover-letter] posting JSON missing required fields" >&2
  exit 2
}

# ----- load operator profile -----
if [[ ! -f "$PROFILE_PATH" ]]; then
  example_path="$SKILL_DIR/config/operator-profile.example.md"
  if [[ "${JH_COVER_LETTER_LIVE:-0}" != "1" && -f "$example_path" ]]; then
    echo "[cover-letter] profile not found at $PROFILE_PATH — scaffold mode falling back to $example_path" >&2
    PROFILE_PATH="$example_path"
  else
    echo "[cover-letter] operator profile not found: $PROFILE_PATH" >&2
    exit 2
  fi
fi
profile_text=$(cat "$PROFILE_PATH")

# ----- compose prompt -----
posting_title=$(echo "$posting_json" | jq -r '.title')
posting_company=$(echo "$posting_json" | jq -r '.company')
posting_summary=$(echo "$posting_json" | jq -r '.summary // ""')

system_prompt="You draft cover letters in a neutral, evidence-first style. Avoid marketing superlatives, 'passionate about', 'team player', humble-brag, and stock phrases. The output is markdown, 200-300 words, ready for operator review."

user_prompt=$(cat <<EOF
Draft a cover letter for the operator to send to the company below.
Markdown output.  200-300 words.  Tone preference: $TONE.

POSTING
title: $posting_title
company: $posting_company
summary: $posting_summary

OPERATOR PROFILE
$profile_text

Structure (no headings; flowing paragraphs):
1. One sentence hook that names the role + one concrete artifact from
   the operator's profile that directly demonstrates fit.
2. One short paragraph (50-80 words) matching 1-2 specific posting
   requirements to 1-2 specific operator strengths.  Use evidence
   (shipped projects, years, tools), not adjectives.
3. One sentence on why this specific company / problem space is
   interesting to the operator.  Avoid "passionate about"; prefer
   "the work that interests me here is X".
4. One sentence asking for a specific next step (call / interview).

Constraint: if the operator's profile flags a gap that the posting
requires, do NOT hide it.  Address it with one concrete sentence
describing how the operator would close it.
EOF
)

if [[ "${JH_COVER_LETTER_LIVE:-0}" != "1" ]]; then
  cat <<EOF >&2
[cover-letter] SCAFFOLD MODE — JH_COVER_LETTER_LIVE not set.  No Claude call.
[cover-letter] Preview of what would be sent to Claude follows on stdout.
[cover-letter] Set JH_COVER_LETTER_LIVE=1 to issue the live call.
EOF
  jq -n \
    --arg sys "$system_prompt" \
    --arg usr "$user_prompt" \
    '{scaffold_mode: true, would_send: {model: "claude-sonnet-4-6", system: $sys, user: $usr}}'
  exit 10
fi

if ! command -v claude >/dev/null 2>&1; then
  echo "[cover-letter] JH_COVER_LETTER_LIVE=1 but \`claude\` CLI not on PATH" >&2
  exit 3
fi

# Cover-letter is a creative-writing stage — Sonnet is the right
# Tier-1 model (more careful word choice than Haiku for this task).
response=$(echo "$user_prompt" | claude \
  --model claude-sonnet-4-6 \
  --append-system-prompt "$system_prompt" \
  --output-format text 2>/dev/null) || {
    echo "[cover-letter] claude CLI failed" >&2
    exit 3
}

echo "$response"
