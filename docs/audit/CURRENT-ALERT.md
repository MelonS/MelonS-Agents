# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-22-contract.md`](2026-05-22-contract.md)
**Generated**: 2026-05-22 15:18:32 KST

## Summary (from audit)


Forty-fifth contract audit (HEAD `bec0f3b`, 2026-05-22).  All twelve hard rules and the
Conventions block in `docs/operator-contract.md` were checked against code, configuration,
git history, and the pre-computed skill-activation drift report (0 findings — clean).
Four findings from the 44th-cycle report (HEAD `33e7f81`) are RESOLVED in the current HEAD:
(1) `scripts/lyric-extract.sh` §8 medium finding — inline `# §8 exception:` comment and
registry entry added by `bec0f3b`; (2) unreachable commit hash `9c8b69e` in roadmap Done
— replaced with `f5d909a` (verified reachable) by `e8b4162`; (3) three production scripts
undocumented (`music-video-doctor.sh`, `music-video-trim.sh`, `music-video-upload-meta.sh`)
— added to `docs/for-analysts.md` inventory by `e8b4162`; (4) `docs/for-analysts.md:94`
date claim "unchanged since 2026-05-17" — file now correctly reads "unchanged since
2026-05-15".  Twelve new commits have landed since `abb45d0` (the last covered Done entry),
growing the Done gap from 6 to 12.  §5 CLEAN — zero commits to `.claude/agents/*.md` or
`agents/*.md` since `8570a9c` (2026-05-15, pre-marker-convention carry-forward exempt).
§7 CLEAN.  §8 CLEAN — all 12 registered files confirmed ≥1 `# §8 exception:` comment;
no output artifacts under `agents/` or `scripts/`.  §12 CLEAN — secret scan returned only
NLP tokenization code, CI annotation strings, and audit-drift token variables.  §3 money
firewall intact.  All six subagent model assignments match the `docs/for-analysts.md` table.
Skill-activation drift: 0 findings (pre-computed).  One [info] new finding: `scripts/shot-plan.sh`
(added `9c4a081`, 2026-05-22) is present in a research doc but absent from the operational
inventory in `docs/for-analysts.md`; it is an opt-in scaffold (MUSIC_VIDEO_USE_SHOT_PLAN=1
not yet wired) so the absence is low-urgency.  Verdict DRIFT_DETECTED is driven solely by the
structural medium: §9 roadmap "Now" stale, 7th consecutive cycle, operator-gated.

## Critical / High findings

_(no critical/high findings — verdict is DRIFT_DETECTED but only medium-or-lower findings)_

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
