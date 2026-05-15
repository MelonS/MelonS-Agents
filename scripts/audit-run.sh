#!/usr/bin/env bash
# Trigger a repository-wide audit.  Builds the prompt + context, calls
# Claude Code with the `auditor` subagent, and persists the report to
# docs/audit/<ISO-date>-<focus>.md.
#
# Usage:
#   ./scripts/audit-run.sh             # full audit, all dimensions
#   ./scripts/audit-run.sh roadmap     # focus on roadmap drift only
#   ./scripts/audit-run.sh contract    # focus on operator-contract compliance
#   ./scripts/audit-run.sh security    # focus on secret leakage + .gitignore
#
# Designed to be cron-friendly: writes only to docs/audit/<date>-<focus>.md,
# never modifies anything else.  Commit is left to the human in the loop —
# audit reports go to git history so a future machine sees the trail.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/log.sh"

FOCUS="${1:-all}"
DATE="$(date +%Y-%m-%d)"
OUT_DIR="$REPO_ROOT/docs/audit"
OUT_FILE="$OUT_DIR/${DATE}-${FOCUS}.md"
mkdir -p "$OUT_DIR"

if ! command -v claude >/dev/null 2>&1; then
  echo "❌ claude CLI not found on PATH — install Claude Code first" >&2
  exit 65
fi

log_step "audit: focus=$FOCUS"
log_info "writing to: $OUT_FILE"

# Compose the prompt.  The auditor agent definition has the full
# contract; we just point it at the right focus and remind it to write
# to the expected path.
PROMPT="You are running a scheduled audit on the MelonS-Agents repository.

Focus for this run: ${FOCUS}

Read \`docs/operator-contract.md\`, \`docs/for-analysts.md\`,
\`docs/architecture.md\`, \`docs/roadmap.md\`, and any files under
\`.claude/agents/\` and \`agents/\` you need to make findings.

Write your full audit report to:

  ${OUT_FILE}

Use the exact structure specified in your agent definition
(.claude/agents/auditor.md).  Do not edit any file other than the
report path above.  Do not commit.  Verdict line at top.
"

# Run the auditor subagent.  --print emits the final assistant message
# to stdout; we capture it for log review.  The agent itself writes
# OUT_FILE via its Write tool — we don't redirect anything.
claude --agent auditor --print "$PROMPT" || {
  log_err "auditor invocation failed — check claude CLI"
  exit 1
}

if [[ ! -s "$OUT_FILE" ]]; then
  log_err "auditor did not produce $OUT_FILE — re-run interactively to inspect"
  exit 2
fi

log_ok "audit report ready: $OUT_FILE"
log_info "review then 'git add docs/audit/ && git commit' to preserve"
