# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: CRITICAL
**Full report**: [`docs/audit/2026-07-03-all.md`](2026-07-03-all.md)
**Generated**: 2026-07-03 05:15:15    

## Summary (from audit)


All-focus audit against HEAD `46135c5` (2026-07-03), run from a Windows
checkout at `G:/ai/MelonS-Agents`. All six dimensions were checked:
architecture-vs-docs drift, roadmap freshness, operator-contract
compliance, cost-model accuracy, stale TODOs/dead code, and
security/secrets. Security is clean — no committed secrets, `.env` stays
untracked, `.env.example` is schema-only, and the `§8 exception:` marker
registry passes for all originally-registered files. The precomputed
skill-drift reports one `medium` row was investigated and shown to be a
false positive caused by a CRLF/blank-line parsing bug in
`scripts/audit-skill-drift.sh` on this Windows checkout, not an actual
missing script in `skills/job-hunt`.

The verdict is CRITICAL for a single, high-impact reason: the
audit automation itself has been completely dark for 39 days. The most
recent report before this one is `docs/audit/2026-05-25-all.md`, and
`docs/audit/CURRENT-ALERT.md` still points at it. Direct verification on
this machine shows all three documented trigger layers are inactive:
`.git/hooks/post-commit` does not exist (L1), and
`scripts/install-scheduler.sh status` reports every launchd plist —
`queue`, `auditor`, `audit-poll`, `disk-watch`, `yt-stats`,
`intervention-chart` — as NOT installed (L2/L3). `launchd` is
macOS-only, and the roadmaps own 2026-05-25 Done entry records the
operator decision to migrate primary production to a Windows machine
("Mac becomes secondary monitor / backup"); no Windows equivalent
(Task Scheduler, cron via WSL, etc.) was ever stood up for the auditor
or queue jobs. In the roughly 140 commits that landed since 2026-05-25,
none of the drift documented below (stale subagent tables, undocumented
cost surfaces, hardcoded machine paths, missing §5 markers, roadmap Done
gaps) was caught — because nothing was watching. That is exactly the
failure mode this subagent exists to prevent, and it went unnoticed
until this on-demand invocation surfaced it.

A second concrete, verifiable break: three committed scripts
(`scripts/batch-recover.sh`, `scripts/build-full-pollinations.sh`,
`scripts/build-full-pollinations-monday.sh`) contain a bare
`cd /Users/melons/ai` with no env-var indirection and no `§8 exception`
marker. That path does not exist on this machine — running any of the
three fails immediately under `set -euo pipefail`. This directly
violates operator-contract §8 principle 4 ("no hardcoded machine-specific
values... all paths env-resolved") and is demonstrable right now, not
hypothetical.

Everything else below is real but lower-impact: documentation (subagent
table, architecture mission map, cost model) has not kept pace with two
skill launches (game-dev-agents 12 role subagents, 2026-05-27; the
4-team content-shorts pipeline, 2026-07-01); `docs/roadmap.md` "Now"
is on its Nth stale cycle (already self-flagged in an unactioned
suggest block); three landed commits have no Done entry;
and the §5 audit-trail marker (`Requested-by: user`) is absent from
every `.claude/agents/*.md` commit since the convention began, though
the contract itself pre-classifies that as low severity.

## Critical / High findings

- **[critical]** Audit automation has been fully inactive for 39 days — `docs/audit/CURRENT-ALERT.md`, `.git/hooks/post-commit`, `scripts/install-scheduler.sh`
- **[high]** Hardcoded, currently-broken machine path in 3 committed scripts — `scripts/batch-recover.sh:11`, `scripts/build-full-pollinations.sh:4`, `scripts/build-full-pollinations-monday.sh:4`

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
