# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-16-contract.md`](2026-05-16-contract.md)
**Generated**: 2026-05-16 17:09:55 KST

## Summary (from audit)


Audited all 12 hard rules and the conventions block in `docs/operator-contract.md` against
committed code, configuration, and the working tree.  This is a second contract audit pass
for 2026-05-16, running after a prior contract audit at 16:59 KST produced a DRIFT_DETECTED
verdict and triggered working-tree fixes that were applied but never committed.  The committed
code retains two rule violations: §8 (hardcoded machine-specific path in `scripts/contact-sheet.sh`)
and the §6/§9 scope drifts (committed `docs/operator-contract.md` still missing `docs/` from §6
scope; committed `CLAUDE.md` still missing `auditor` from the subagent-exclusion list).
Working-tree fixes for all three exist and are correct, but the auto-commit protocol (§6)
was not followed, leaving three modified files and two untracked doc files uncommitted.
No secrets, PII, or output artifacts were found in committed content.  All six subagent model
assignments match `docs/for-analysts.md`.  Roadmap Done entries verified against git history.

## Critical / High findings

- **[high]** §8 violation — hardcoded machine-specific path in committed `contact-sheet.sh` — `scripts/contact-sheet.sh:9`
- **[high]** §6 violation — working-tree fixes not committed or pushed — `git status`

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
