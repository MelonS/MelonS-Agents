# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-20-contract.md`](2026-05-20-contract.md)
**Generated**: 2026-05-20 19:53:49 KST

## Summary (from audit)


Twenty-eighth contract audit. HEAD = `b3fc7f3`
("chore(gitignore): ignore .claude/statusline.sh.* backup variants") on `main`,
fully synced with `origin/main`. All twelve hard rules in
`docs/operator-contract.md` were walked against committed code, configuration,
git history, and `.claude/agents/*.md` frontmatter. This audit supersedes the
prior same-day report (`2026-05-20-contract.md`, HEAD `a90bc9d`, generated at
00:30 KST) — seventeen commits landed between the two audits, including
feat/skill-job-hunt merge, v0.4.0 tag, README cadence batch, fresh-clone
regression test, and a gitignore fix. Both open findings from the 00:30
report are now resolved (`d1c279d` rewrote roadmap "Now"; `for-analysts.md`
CI claim was already fixed before that audit). Two new findings are introduced:
one [medium] §9 issue (roadmap "Now" still lists "Land v0.4.0" as the active
subgoal even though v0.4.0 tag was pushed and Done entry is absent) and one
[low] §6 description drift (gate 3 table row in the contract says "`agents/`"
broadly but the implementation in `pre-merge-check.sh` uses `agents/[^/]+\.md`
— the script was fixed in `0652092`/`aa56f5f` but the contract table was not
updated). No secrets detected. All six subagent model assignments match the
`docs/for-analysts.md` table.

## Critical / High findings

_(no critical/high findings — verdict is DRIFT_DETECTED but only medium-or-lower findings)_

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
