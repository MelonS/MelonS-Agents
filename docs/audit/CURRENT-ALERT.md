# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-18-all.md`](2026-05-18-all.md)
**Generated**: 2026-05-18 03:08:32 KST

## Summary (from audit)


Sixteenth audit of the session; HEAD = `f86c2f0` (2026-05-18, latest commit as of audit time).
Supersedes the fifteenth audit at HEAD `e793662` (2026-05-17-contract.md).
Full-scope audit covering all six dimensions: architecture vs documentation drift,
roadmap freshness, operator-contract compliance, cost-model accuracy, stale TODOs /
dead code, and security / secrets.  Seven commits landed since the prior audit:
`ab6555e` (plist templating — closes prior §8 plist finding), `a8291f5`, `6a460b0`,
`23832fa`, `36323eb`, `65c35a6`, `f86c2f0`.  Key new surfaces: `scripts/music-video-shaders.sh`,
`scripts/daily-music-video.sh`, `assets/music/SOURCES.md`, and `outputs/publish/`.
Three medium findings identified: architecture docs describe the faceless-short script
stage as Tier-1-Claude-primary, but the code defaults to Tier-2 ollama (opt-in override
required); roadmap "Now" is stale after goal completion; CURRENT-ALERT.md was not
auto-cleared after the §8 plist fix landed.  The prior §8 plist finding is fully
resolved by `ab6555e`; all other prior high/critical findings remain clean.

## Critical / High findings

_(no critical/high findings — verdict is DRIFT_DETECTED but only medium-or-lower findings)_

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
