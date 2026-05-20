#!/usr/bin/env bash
# scripts/derive-profile.sh - Phase 2.4 auto operator-profile draft.
#
# Reads the operator's repo (or any directory) and drafts an
# operator-profile.md based on observable artifacts: CLAUDE.md,
# README, docs/, recent commit messages.  The resulting draft is
# always a starting point - operator reviews + edits before
# committing to operator-profile.md.
#
# Status (2026-05-20): SCAFFOLD MODE BY DEFAULT.  Gated on
# JH_DERIVE_PROFILE_LIVE=1.  Mirrors fit-score / cover-letter /
# company-research / interview-prep scaffold pattern.
#
# Cost shape (live):
#   ~2000-4000 input tokens (assembled repo context blob)
#   ~500-800 output tokens (markdown profile draft)
#   ~0 USD on Max plan; one call per derive.
#
# Privacy:
#   The repo context blob is sent to Claude.  If the repo carries
#   sensitive personal content the operator should redact before
#   running.  The output of this script is markdown the operator
#   reviews; nothing is persisted unless the operator saves it.
#
# Usage:
#   scripts/derive-profile.sh                  # uses current repo root
#   scripts/derive-profile.sh /path/to/repo
#
# Env:
#   JH_DERIVE_PROFILE_LIVE=1   issue the Claude call
#
# Exit codes: 0 success, 2 config, 3 live call failed, 10 scaffold.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

REPO_ROOT=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --help|-h)
      printf '%s\n' \
        "derive-profile.sh - Claude-drafted operator-profile.md (scaffold mode by default)." \
        "" \
        "Reads the operator's repo (default: pwd, walked up to nearest .git root)" \
        "and assembles a context blob from: CLAUDE.md, README, docs/, recent commits." \
        "Claude returns a draft operator-profile.md the operator reviews + edits." \
        "" \
        "Env:" \
        "  JH_DERIVE_PROFILE_LIVE=1   issue the Claude call" \
        "" \
        "Output: markdown profile draft on stdout.  Save with:" \
        "  scripts/derive-profile.sh > skills/job-hunt/config/operator-profile.md" \
        "(operator-profile.md is gitignored)."
      exit 0
      ;;
    -*)           echo "[derive-profile] unknown flag: $1" >&2; exit 2 ;;
    *)            REPO_ROOT="$1"; shift ;;
  esac
done

# ----- find repo root -----
if [[ -z "$REPO_ROOT" ]]; then
  REPO_ROOT=$(git -C "$PWD" rev-parse --show-toplevel 2>/dev/null || echo "$PWD")
fi
if [[ ! -d "$REPO_ROOT" ]]; then
  echo "[derive-profile] repo path not a directory: $REPO_ROOT" >&2
  exit 2
fi

# ----- assemble context blob -----
# Each piece is bounded so a giant repo doesn't blow the context.
# Order: highest signal first.
collect_section() {
  local label="$1" path="$2" max_lines="$3"
  if [[ -f "$path" ]]; then
    printf '\n=== %s (from %s) ===\n' "$label" "$path"
    head -n "$max_lines" "$path"
  elif [[ -d "$path" ]]; then
    printf '\n=== %s (from %s/) ===\n' "$label" "$path"
    # List files + concatenate first N lines of each.
    find "$path" -maxdepth 2 -type f -name "*.md" 2>/dev/null | head -5 | while read -r f; do
      printf '\n--- %s ---\n' "$f"
      head -n 30 "$f"
    done
  fi
}

context_blob=$(
  collect_section "CLAUDE.md (repo operating contract)" "$REPO_ROOT/CLAUDE.md" 150
  collect_section "README (project overview)" "$REPO_ROOT/README.md" 80
  collect_section "Engineering case studies" "$REPO_ROOT/docs/engineering-case-studies.md" 200
  collect_section "Architecture map" "$REPO_ROOT/docs/architecture.md" 60
  printf '\n=== recent commit subjects (last 30) ===\n'
  git -C "$REPO_ROOT" log -30 --format='%h %s' 2>/dev/null || echo "(no git log)"
)

# ----- compose prompt -----
system_prompt="You draft operator-profile.md files for the skills/job-hunt v2 fit-scoring pipeline.  You read evidence (committed files, commit messages, docs) from a repo and produce a SHORT markdown profile.  Never invent strengths or experience the evidence does not support.  Mark unknown sections with placeholder text + an explicit note for the operator to fill in."

user_prompt=$(printf '%s\n' \
  "Draft an operator-profile.md for the developer behind the repo below." \
  "Output: markdown only, matching the template structure in" \
  "skills/job-hunt/config/operator-profile.example.md." \
  "" \
  "Sections required (use the exact headings from the example template):" \
  "  ### Role target" \
  "  ### Location constraints" \
  "  ### Anti-targets" \
  "  ### Strengths (3-5 lines)" \
  "  ### Gaps / honest self-assessment" \
  "  ### Concrete artifacts to surface" \
  "  ### Application style preference" \
  "" \
  "Constraints:" \
  "- Strengths: only claims supported by direct evidence in the" \
  "  context blob (specific shipped artifacts, named tools, years" \
  "  of experience the docs state)." \
  "- Gaps: if you cannot infer gaps, write '(placeholder - operator" \
  "  to fill)' rather than inventing." \
  "- Location: leave as a placeholder unless the repo explicitly says." \
  "- Artifacts: include only URLs / paths you saw in the context." \
  "" \
  "Repo context blob follows.  It is sequential excerpts, not the full" \
  "repo - prioritize signals from CLAUDE.md + README + case studies +" \
  "recent commits.  Truncated content is normal; do your best with" \
  "what is shown." \
  "" \
  "REPO CONTEXT" \
  "$context_blob" \
)

if [[ "${JH_DERIVE_PROFILE_LIVE:-0}" != "1" ]]; then
  printf '%s\n' \
    "[derive-profile] SCAFFOLD MODE - JH_DERIVE_PROFILE_LIVE not set.  No Claude call." \
    "[derive-profile] Preview of context blob + prompt follows on stdout." \
    "[derive-profile] Set JH_DERIVE_PROFILE_LIVE=1 to issue the live call." >&2
  jq -n \
    --arg sys "$system_prompt" \
    --arg usr "$user_prompt" \
    --arg root "$REPO_ROOT" \
    '{scaffold_mode: true, repo_root: $root, would_send: {model: "claude-sonnet-4-6", system: $sys, user: $usr}}'
  exit 10
fi

if ! command -v claude >/dev/null 2>&1; then
  echo "[derive-profile] JH_DERIVE_PROFILE_LIVE=1 but claude CLI not on PATH" >&2
  exit 3
fi

# Sonnet for the careful drafting (not Haiku - this is one-shot,
# nuanced writing where word choice + honest gap-marking matter).
response=$(echo "$user_prompt" | claude \
  --model claude-sonnet-4-6 \
  --append-system-prompt "$system_prompt" \
  --output-format text 2>/dev/null) || {
    echo "[derive-profile] claude CLI failed" >&2
    exit 3
}

echo "$response"
