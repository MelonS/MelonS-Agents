# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-22-contract.md`](2026-05-22-contract.md)
**Generated**: 2026-05-22 01:08:02 KST

## Summary (from audit)


Thirty-fifth contract audit (second pass today; prior run at 01:01 KST had HEAD
`a122f99` — this run is HEAD `c04d371`, one commit later).  Checked all twelve hard
rules and the Conventions block in `docs/operator-contract.md` against code,
configuration, and recent git history.  Seven findings: three medium (carry-forward
— `outputs/publish/upload-meta-v2/` committed while architecture table claims
otherwise; portability gap from `~/.claude/CLAUDE.md` split having no bootstrap path;
roadmap Done now 26 commits behind HEAD) and four low (carry-forward — §8 exception
comment absent in `scripts/ffmpeg-throttled.sh:33`, same comment absent in three
2026-05-21 music-video scripts, `docs/for-analysts.md:93` date label 5 days stale,
§8 exception registry line numbers for `agents/lib/env.sh` off by 2 lines).  §5
CLEAN — no commits to `.claude/agents/*.md` or `agents/*.md` since 2026-05-17.  §7
CLEAN — no `&&`-compound git calls in tracked scripts.  §12 CLEAN — secret scan
returned only comments and tokenization code.  New commit `c04d371`
(`feat(audit): skill-activation drift check`) adds `scripts/audit-skill-drift.sh`
with no hardcoded paths; the pre-flight skill-drift report passed with zero findings.
Subagent model assignments: all six agents match the `docs/for-analysts.md` table.

## Critical / High findings

_(no critical/high findings — verdict is DRIFT_DETECTED but only medium-or-lower findings)_

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
