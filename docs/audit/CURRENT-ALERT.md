# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-19-all.md`](2026-05-19-all.md)
**Generated**: 2026-05-19 00:51:43 KST

## Summary (from audit)


Twenty-second audit (first full-scope "all" run on 2026-05-19); HEAD = `401f9ff` on
branch `feat/skill-music-video` (6 commits ahead of `main` at `a537018`).  All six
dimensions audited: architecture vs documentation drift, roadmap freshness,
operator-contract compliance, cost-model accuracy, stale TODOs / dead code, and
security / secrets.  A contract-focused audit also ran today at HEAD `c9ecb15`
(`docs/audit/2026-05-19-contract.md`); findings from that report are incorporated
and updated for the current HEAD.

Since the prior "all" audit (`2026-05-18-all.md`, HEAD `65a5045`), substantial work
landed: branch strategy codified in `operator-contract.md` §6 (`a2a3807`), pre-merge
gate added (`22a45ea`), GitHub Actions CI added on `main` (`a537018`), and the
`feat/skill-music-video` branch started with 6 structural commits — most notably
Skill #1 music-video (`a993753`), `.claude/settings.json` portability fix
(`912d61c`), `scripts/install-claude-local.sh` (`912d61c`/`40aeab1`), and five
portability principles codified in `operator-contract.md` §8 (`5b1aafb`).  The
[medium] §8 finding from the prior audit (hardcoded `/Users/melons` in tracked
`.claude/settings.json`) is **resolved** by `912d61c` — `.claude/settings.json`
is now rendered from `config/claude-settings.template.json` using `@@HOME@@` /
`@@REPO_ROOT@@` placeholders and is properly gitignored.

Two new mediums: (1) the new `skills/` top-level directory is absent from all three
architecture reference documents; (2) `docs/roadmap.md` "Now" still reads "No active
goal" while `docs/goal.md` has had an active goal since 2026-05-19 ~00:55 KST.
No critical or high findings.  Security, cost model, model assignments, §5 marker
compliance, and `.gitignore` coverage all pass clean.

## Critical / High findings

_(no critical/high findings — verdict is DRIFT_DETECTED but only medium-or-lower findings)_

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
