# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-25-all.md`](2026-05-25-all.md)
**Generated**: 2026-05-25 03:08:28 KST

## Summary (from audit)


All-focus audit against HEAD `f78e283` (2026-05-25).  All six dimensions
checked: (1) architecture vs documentation drift, (2) roadmap freshness,
(3) operator-contract compliance, (4) cost-model accuracy, (5) stale TODOs
/ dead code, (6) security / secrets.  The primary finding is a **persistent
model-assignment drift now entering its 5th consecutive unresolved audit
cycle**: `.claude/agents/planner.md:5` and `.claude/agents/resourcer.md:5`
both carry `model: opus` (set by commit `2778316`, 2026-05-22 ~17:50 KST),
but `docs/for-analysts.md`, `docs/architecture.md`, and `docs/cost-model.md`
still describe them as `sonnet`.  Four commits have landed since the
previous all-audit (`2026-05-24-all.md`, HEAD `7edba96`) — all are
README/site refresh and gitignore work; none resolve the open findings
and none introduce new contract violations.  Security scan is clean: no
secrets, no PII, `.env` not tracked in git, all §8 exception-registry
files retain their `# §8 exception:` markers.  Skill-activation drift
report returns 0 findings.  Roadmap Done gap has grown from 31 to 35
commits since the last Done entry.

## Critical / High findings

- **[high]** Model-assignment drift (5th consecutive unresolved cycle) —

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
