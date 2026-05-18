# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-19-contract.md`](2026-05-19-contract.md)
**Generated**: 2026-05-19 00:49:55 KST

## Summary (from audit)


Contract-focused audit at HEAD `c9ecb15` on branch `feat/skill-music-video`
(2026-05-19 ~00:41 KST, "docs(ideas): park A/B test — planner + resourcer
at Opus vs Sonnet").  This is the twenty-first audit overall; the most recent
prior audit was the all-scope run that wrote `docs/audit/2026-05-18-all.md`
at HEAD `65a5045` on `feat/post-merge-detection`.  Current `origin/main` =
`a537018` (GitHub Actions main-protection); local `feat/skill-music-video`
is five commits ahead with structural work: §8 portability principles
codified, Skill #1 shipped, `config/claude-settings.template.json` +
`scripts/install-claude-local.sh` created, `scripts/bootstrap.sh` wired.
All five structural commits are correctly isolated on the feat branch and
have NOT been merged to `origin/main` — confirming §6 is being followed for
current work.

**Note on the previous contract audit file at this same path**: an earlier
run today (~00:41 KST) wrote an incorrect [medium] finding asserting that
commits `a993753`, `912d61c`, and `40aeab1` "are on `main`."  `git log
feat/skill-music-video ^main` disproves this: those three commits appear
exclusively on the feat branch.  The earlier auditor appears to have read
`git log` output from the feat branch and mistaken "visible in history"
for "on main."  This report supersedes that erroneous run.

Key positives: §8 `.claude/settings.json` hardcode finding (carried since
2026-05-17) resolved in `912d61c`; all six agent frontmatter models match
`docs/for-analysts.md` table; no secrets in any committed file; no §5
marker violations (all `.claude/agents/*.md` edits predate the convention).
Remaining concerns: one [medium] contract-process violation (pre-merge gate
bypassed for `feat/post-merge-detection` → `origin/main`), and four [low]
items.

## Critical / High findings

_(no critical/high findings — verdict is DRIFT_DETECTED but only medium-or-lower findings)_

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
