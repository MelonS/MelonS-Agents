# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-18-all.md`](2026-05-18-all.md)
**Generated**: 2026-05-18 14:32:34 KST

## Summary (from audit)


Seventeenth audit of the session; HEAD = `39c5db3` (2026-05-18, ~14:23 KST).
Supersedes the sixteenth audit at HEAD `f86c2f0` (also 2026-05-18, 03:08 KST).
Full-scope audit covering all six dimensions: architecture vs documentation drift,
roadmap freshness, operator-contract compliance, cost-model accuracy, stale TODOs /
dead code, and security / secrets.  Six commits landed since the prior audit:
`a1846f6` (daily-queue fix), `aa10ba0` (onboarding bootstrap), `5e831fe` (fresh-clone
PASS log), `303949a` (readme polish), `9d7f954` (readme pricing/path update),
`39c5db3` (audit fix — resolved all three medium findings from the prior run).
All prior medium findings are now resolved.  Two low findings remain open:
`outputs/publish/.gitkeep` (§8 carry-forward) and a stale "current active goal"
reference in `docs/for-analysts.md:78` (new).  Security, secrets, model assignments,
roadmap commit-hash validity, and §5 marker compliance all pass clean.

## Critical / High findings

_(no critical/high findings — verdict is DRIFT_DETECTED but only medium-or-lower findings)_

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
