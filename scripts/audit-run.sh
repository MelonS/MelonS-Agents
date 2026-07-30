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
# Designed to be cron-friendly: writes only to docs/audit/<date>-<focus>.md
# and docs/audit/CURRENT-ALERT.md, and commits exactly those paths itself so
# the trail survives a machine swap.  Nothing else in the tree is touched —
# `git commit --only` leaves whatever the operator has staged untouched.
# Set AUDIT_NO_COMMIT=1 to leave the report uncommitted.
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

# Locate the claude CLI.  Under launchd the PATH is minimal and does not
# include nvm-managed node bins where Claude Code typically lives, so we
# fall back to a glob of the standard nvm install location and a couple of
# other common bin dirs.  Once found, we put it on PATH for any child
# processes the auditor subagent might spawn.
#
# §8 exception: `/opt/homebrew/bin/claude` and `/usr/local/bin/claude` are
# absolute paths but appear ONLY as fallback candidates inside a probe loop,
# never as the resolved binary.  Same documented-exception pattern as
# `agents/lib/env.sh`'s ffmpeg-full keg discovery — listed for completeness
# under launchd's minimal PATH.  When `command -v claude` succeeds (PATH is
# correct), the loop is skipped entirely.
if ! command -v claude >/dev/null 2>&1; then
  for candidate in \
      "$HOME"/.nvm/versions/node/*/bin/claude \
      "$HOME"/.local/bin/claude \
      /opt/homebrew/bin/claude \
      /usr/local/bin/claude; do
    if [[ -x "$candidate" ]]; then
      export PATH="$(dirname "$candidate"):$PATH"
      break
    fi
  done
fi
if ! command -v claude >/dev/null 2>&1; then
  echo "❌ claude CLI not found on PATH or in known locations" >&2
  echo "   searched: ~/.nvm/versions/node/*/bin, ~/.local/bin, /opt/homebrew/bin, /usr/local/bin" >&2
  echo "   add the install dir to LAYOUT PATH in scripts/com.melons.agents.auditor.plist" >&2
  exit 65
fi

log_step "audit: focus=$FOCUS"
log_info "writing to: $OUT_FILE"

# Pre-flight: run the skill-activation drift check.  Findings (if any)
# are passed to the auditor as a pre-computed observation so the agent
# can incorporate them in its findings list under §13 (skill manifest
# drift).  The script itself never fails the audit — it is read-only.
SKILL_DRIFT_REPORT=""
if [[ -x "$REPO_ROOT/scripts/audit-skill-drift.sh" ]]; then
  SKILL_DRIFT_REPORT="$("$REPO_ROOT/scripts/audit-skill-drift.sh" 2>&1 || true)"
fi

# Compose the prompt.  The auditor agent definition has the full
# contract; we just point it at the right focus and remind it to write
# to the expected path.
PROMPT="You are running a scheduled audit on the MelonS-Agents repository.

Focus for this run: ${FOCUS}

Read \`docs/operator-contract.md\`, \`docs/for-analysts.md\`,
\`docs/architecture.md\`, \`docs/roadmap.md\`, and any files under
\`.claude/agents/\` and \`agents/\` you need to make findings.

Pre-computed skill-activation drift report (from
\`scripts/audit-skill-drift.sh\`):

\`\`\`
${SKILL_DRIFT_REPORT:-(skill-drift script not run — no manifests yet, or script missing)}
\`\`\`

Treat any \`medium\` or \`low\` row above as a finding to include in
your report's Findings section with severity \`[low]\` (or \`[medium]\`
when the drift indicates a missing script or a hidden undeclared flag).
A clean drift report does not need to be cited as a finding.

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
log_info "감사 트레일은 이 스크립트가 직접 커밋한다 (생략: AUDIT_NO_COMMIT=1)"

# -----------------------------------------------------------------------------
# Active surface: extract verdict and maintain docs/audit/CURRENT-ALERT.md
#
# The auditor itself stays read-only and writes its full report to OUT_FILE.
# This wrapper post-processes that report: if the verdict is non-CLEAN, it
# writes a short alert file at a stable, prominent path so the next session
# notices the drift without having to scan dated audit files.  When the
# verdict returns to CLEAN, the alert file is auto-removed.
#
# All failure modes are non-fatal — the audit itself must never fail because
# of this parsing.  CURRENT-ALERT.md is committed (lives in docs/) so it
# survives a machine swap, same as the rest of the audit trail.
# -----------------------------------------------------------------------------
ALERT_FILE="$OUT_DIR/CURRENT-ALERT.md"

parse_verdict_and_alert() {
  local verdict
  # Verdict line format (from auditor.md): "**Verdict**: CLEAN | DRIFT_DETECTED | CRITICAL"
  verdict="$(grep -m1 -E '^\*\*Verdict\*\*:' "$OUT_FILE" \
              | sed -E 's/^\*\*Verdict\*\*:[[:space:]]*//' \
              | awk '{print $1}')"

  if [[ -z "$verdict" ]]; then
    log_warn "could not parse verdict from $OUT_FILE — leaving alert state as-is"
    return 0
  fi

  log_info "audit verdict: $verdict"

  case "$verdict" in
    CLEAN)
      if [[ -f "$ALERT_FILE" ]]; then
        rm -f "$ALERT_FILE"
        log_ok "cleared $ALERT_FILE (verdict returned to CLEAN)"
      fi
      ;;
    DRIFT_DETECTED|CRITICAL)
      write_alert_file "$verdict"
      log_warn "wrote $ALERT_FILE (verdict=$verdict)"
      ;;
    *)
      log_warn "unknown verdict '$verdict' — leaving alert state as-is"
      ;;
  esac
}

write_alert_file() {
  local verdict="$1"
  local summary findings_block
  # Pull the Summary section (between '## Summary' and next '## ').
  summary="$(awk '/^## Summary[[:space:]]*$/{flag=1; next} /^## /{flag=0} flag' "$OUT_FILE")"
  # Pull critical+high findings.  The auditor formats each finding as
  # "- **[severity]** ..." so we filter by severity prefix.
  findings_block="$(awk '/^## Findings[[:space:]]*$/{flag=1; next} /^## /{flag=0} flag' "$OUT_FILE" \
                    | grep -E '^\- \*\*\[(critical|high)\]\*\*' || true)"

  {
    printf '# Current audit alert\n\n'
    printf '> This file is written by `scripts/audit-run.sh` whenever the most\n'
    printf '> recent audit has a non-CLEAN verdict.  It is auto-removed on the\n'
    printf '> next CLEAN run.  Do not edit by hand — fix the underlying findings\n'
    printf '> and re-run the auditor.\n\n'
    printf '**Verdict**: %s\n' "$verdict"
    printf '**Full report**: [`docs/audit/%s-%s.md`](%s-%s.md)\n' "$DATE" "$FOCUS" "$DATE" "$FOCUS"
    printf '**Generated**: %s\n\n' "$(date '+%Y-%m-%d %H:%M:%S %Z')"
    printf '## Summary (from audit)\n\n%s\n\n' "${summary:-_(no summary block found in report)_}"
    printf '## Critical / High findings\n\n'
    if [[ -n "$findings_block" ]]; then
      printf '%s\n' "$findings_block"
    else
      printf '_(no critical/high findings — verdict is %s but only medium-or-lower findings)_\n' "$verdict"
    fi
    printf '\n## How to clear this alert\n\n'
    printf '1. Read the full report linked above.\n'
    printf '2. Resolve each critical / high finding (suggested fixes are in the report).\n'
    printf '3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.\n'
  } > "$ALERT_FILE"
}

# Non-fatal: parsing failures must never break the audit run itself.
parse_verdict_and_alert || log_warn "verdict parser failed — alert file may be stale"

# ── Audit-trail durability ───────────────────────────────────────────────
# The runner commits its own output.  Six consecutive cycles flagged
# "reports are generated but never committed" as CRITICAL, and every time
# the fix was a human noticing stray files in `git status` and committing
# them in a batch (27495cb, 52fda03, b8f7d26).  A step someone has to
# remember is a step that gets skipped, so it stopped being a step.
#
# Scope is deliberately narrow: only docs/audit/ paths are committed, via
# `git commit --only`, so whatever else the operator has staged is left
# exactly as it was.  Everything here is non-fatal — an audit must never
# fail because of git.
commit_audit_trail() {
  [[ "${AUDIT_NO_COMMIT:-0}" == "1" ]] && { log_info "AUDIT_NO_COMMIT=1 — 트레일 커밋 생략"; return 0; }
  command -v git >/dev/null 2>&1 || return 0
  git -C "$REPO_ROOT" rev-parse --git-dir >/dev/null 2>&1 || return 0

  # Never interfere with an in-progress merge/rebase/bisect.
  local gitdir; gitdir="$(git -C "$REPO_ROOT" rev-parse --git-dir)"
  for marker in MERGE_HEAD rebase-merge rebase-apply BISECT_LOG CHERRY_PICK_HEAD; do
    if [[ -e "$REPO_ROOT/$gitdir/$marker" ]]; then
      log_warn "git $marker 진행 중 — 감사 트레일 커밋 보류(다음 실행에서 함께 커밋된다)"
      return 0
    fi
  done

  # Anything to preserve?  (modified, or untracked-but-not-ignored)
  local pending
  pending="$(git -C "$REPO_ROOT" status --porcelain -- docs/audit 2>/dev/null)"
  [[ -z "$pending" ]] && { log_info "감사 트레일: 커밋할 변경 없음"; return 0; }

  # The post-commit hook can fire while another git process holds the lock.
  local try
  for try in 1 2 3; do
    [[ -e "$REPO_ROOT/$gitdir/index.lock" ]] || break
    log_info "index.lock 대기 ($try/3)"; sleep 3
  done

  git -C "$REPO_ROOT" add -- docs/audit >/dev/null 2>&1 || { log_warn "감사 트레일 add 실패"; return 0; }
  # `-m` must come BEFORE `--`; anything after the separator is a pathspec,
  # so the message would silently be treated as a file to commit.
  if git -C "$REPO_ROOT" commit --only -q \
       -m "chore(audit): $DATE $FOCUS 감사 산출물 보존 (자동)" \
       -m "audit-run.sh 가 자기 리포트와 CURRENT-ALERT 를 직접 커밋한다. 사람이 git status 에서 발견해 치우는 단계를 없앤 것 — 그 단계가 여섯 사이클 연속 빠졌다." \
       -- docs/audit >/dev/null 2>&1; then
    log_info "감사 트레일 커밋: $(git -C "$REPO_ROOT" rev-parse --short HEAD)"
  else
    log_warn "감사 트레일 커밋 실패 — 워킹트리에 남는다(다음 실행에서 재시도)"
    return 0
  fi

  # Push is best-effort: offsite durability is the point, but a network
  # blip must not turn into a failed audit or a half-done state.
  if [[ "$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD)" == "main" ]] \
     && git -C "$REPO_ROOT" remote get-url origin >/dev/null 2>&1; then
    if git -C "$REPO_ROOT" push -q origin HEAD:main >/dev/null 2>&1; then
      log_info "감사 트레일 푸시 완료"
    else
      log_warn "푸시 실패 — 커밋은 로컬에 남아 있다(다음 푸시에 함께 올라간다)"
    fi
  fi
}

commit_audit_trail || true
