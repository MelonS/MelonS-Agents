# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-21-contract.md`](2026-05-21-contract.md)
**Generated**: 2026-05-21 04:20:36 KST

## Summary (from audit)


Thirty-third contract audit (second run this date; supersedes the earlier instance).
Current HEAD: `6225afa` (`feat(skill-job-hunt): digest renders role_fit + hire_prob
breakdown`) on `main`, synced with `origin/main`.  Checked all twelve hard rules and
the Conventions block in `docs/operator-contract.md` against code, configuration, and
recent git history.  Five findings: one medium (output artifacts committed while
architecture.md claims none, unresolved from the prior 2026-05-20 audit), three lows
(§8 exception comments absent in four scripts; §8 exception-registry line numbers
stale in two entries; `docs/for-analysts.md` date label 4 days stale), and one low
(roadmap "Now" states "no further scaffolding needed" while three subsequent Done
entries show continued scaffolding).  §5 is CLEAN — both commits to
`.claude/agents/*.md` predate the 2026-05-17 marker convention and are exempt.  Secret
scan is CLEAN.  No §7 violations.  §12 is CLEAN.

## Critical / High findings

_(no critical/high findings — verdict is DRIFT_DETECTED but only medium-or-lower findings)_

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
