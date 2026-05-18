# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-18-all.md`](2026-05-18-all.md)
**Generated**: 2026-05-18 14:42:36 KST

## Summary (from audit)


Eighteenth audit of the session; HEAD = `d40abd3` (2026-05-18, ~14:34 KST).
Supersedes the seventeenth audit at HEAD `39c5db3` (2026-05-18, ~14:23 KST).
Full-scope audit covering all six dimensions: architecture vs documentation drift,
roadmap freshness, operator-contract compliance, cost-model accuracy, stale TODOs /
dead code, and security / secrets.  Two commits landed since the prior audit run
that generated `CURRENT-ALERT.md` (14:32:34 KST): `39c5db3` (already captured) and
`d40abd3` (audit-fix — resolved both [low] findings from the seventeenth audit).
Both previously-flagged [low] findings are now fully resolved: `docs/for-analysts.md:78`
rephrased correctly and `outputs/publish/.gitkeep` carries the `<!-- §8 operator-directed
deviation -->` marker with a matching row in the `docs/architecture.md` Layers table.
Three new [low] findings: (1) `docs/audit/CURRENT-ALERT.md` still shows DRIFT_DETECTED
for findings resolved ~40 seconds after the alert was written; (2) `docs/roadmap.md`
Done section missing the `d40abd3` entry per §9 contract; (3) `.claude/scheduled_tasks.lock`
is an untracked Claude Code runtime file not covered by `.gitignore`, creating persistent
noise in `git status`.  Security, secrets, model assignments, roadmap commit-hash
validity, and §5 marker compliance all pass clean.

## Critical / High findings

_(no critical/high findings — verdict is DRIFT_DETECTED but only medium-or-lower findings)_

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
