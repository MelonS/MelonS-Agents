# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-19-contract.md`](2026-05-19-contract.md)
**Generated**: 2026-05-19 22:41:15 KST

## Summary (from audit)


Twenty-fifth contract audit (this run supersedes all same-day prior reports at this path). HEAD = `fdcedbc` (`v0.3.0`, "feat(music-video): MUSIC_VIDEO_BROLL_DIR + AI-anime B-roll generator") on `origin/main`, fully synced. All twelve contract hard rules walked against committed code, configuration, and git history. Three mediums, six lows, zero critical/high.

One **new** [medium] §8 finding was missed by the ~22:06 KST prior same-day run: `scripts/fetch-ai-anime-broll.sh:25` hardcodes `/Users/melons/.local/opt/ffmpeg-static/ffmpeg` without an inline `§8 exception:` comment and without a registry entry in `docs/operator-contract.md`. That prior run's compliance table incorrectly marked §8 hardcoded paths as ✅. Two §9 mediums carry forward: roadmap "Now" says "No active goal" while `docs/goal.md` has had an active goal since 2026-05-19 ~00:55 KST; roadmap Done section has no entries for the v0.3.0 milestone work. Six lows: stale "awaiting merge" in `docs/for-analysts.md:115`; stale "Not yet merged to main" in `docs/goal.md:358`; contract trigger-list text mismatch (`.claude/skills/*` vs `skills/<name>/`); §5 text vs gate-3 scope inconsistency; v0.3.0 tag convention deviation; carry-forward `a537018` CI-to-main. All six subagent model assignments match `docs/for-analysts.md`. No secrets. §5 marker compliance clean.

## Critical / High findings

_(no critical/high findings — verdict is DRIFT_DETECTED but only medium-or-lower findings)_

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
