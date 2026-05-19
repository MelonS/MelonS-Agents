# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-20-contract.md`](2026-05-20-contract.md)
**Generated**: 2026-05-20 00:30:20 KST

## Summary (from audit)


Twenty-seventh contract audit. HEAD = `a90bc9d`
("chore: filter-repo backup branch deleted (Roadmap Next #2)") on `origin/main`,
fully synced. All twelve hard rules in `docs/operator-contract.md` were walked
against committed code, configuration, git history, and the `.claude/agents/*.md`
frontmatter table. Since the prior contract audit (`2026-05-19-contract.md`,
HEAD `81320a3`), four commits landed: `f752d72` (README/site framing), `95308f6`
(PII-scrub per §12), `aa56f5f` (audit-fix: closes L4 §5-regex, L5 v0.3.0-tag,
and L6 bootstrap-exception extension), and `a90bc9d` (branch deletion). The
`aa56f5f` fix commit resolved three of the four prior carry-forward findings.
Two findings remain: one [medium] roadmap-vs-goal drift (operator-edit-only)
and one [low] stale docs claim about CI (missed by `aa56f5f`). No new findings
introduced. All six subagent model assignments match `docs/for-analysts.md`.
No secrets detected. No §5-scope agent-file changes since 2026-05-15 (all
pre-marker-convention, exempt).

## Critical / High findings

_(no critical/high findings — verdict is DRIFT_DETECTED but only medium-or-lower findings)_

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
