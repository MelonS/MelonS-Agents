# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-07-03-contract.md`](2026-07-03-contract.md)
**Generated**: 2026-07-03 05:39:02    

## Summary (from audit)


Contract-focus audit of `docs/operator-contract.md` compliance, run against
a live HEAD that moved from `9559d1c` to `592bfc7` over the course of this
session (the repo had active concurrent write pressure throughout — see the
`[info]` finding below). Findings are verified against `592bfc7` unless
otherwise noted. No secrets, PII, or credential leaks were found (Section 12
clean); `.env` stays untracked and `.env.example` is schema-only; the
autonomy budget in `config/policies.yaml` (`budget_usd_ceiling: 5.00`)
matches the documentation. The Section 5 audit-trail marker
(`Requested-by: user`) remains absent from every agent-definition commit
since the convention began — a persistent, contract-pre-classified low
finding, now at 9 instances after today's `9559d1c` added it. Section 6
(`records/` never committed) and Section 8 (no output artifacts under
`agents/`/`scripts/`) both pass clean checks.

The highest-severity finding is new: the precomputed skill-drift reports
`job-hunt` "manifest references missing script" row is a real, reproducible
functional break, not just a doc-drift artifact. Root cause is CRLF line
endings on `skills/job-hunt/config/activation.tsv` (a `.gitattributes` gap
— no `*.tsv text eol=lf` rule — combined with `core.autocrlf=true` on this
Windows checkout) plus a blank-line-detection bug shared by two consumers
of that manifest. `scripts/audit-skill-drift.sh` produces the bogus
finding; separately and more seriously, `skills/job-hunt/scripts/status.sh`
— the operator-facing dashboard the manifests own header comment names as
its consumer — crashes with exit code 3 (`[status] malformed manifest
row: ''`) when actually run on this machine, reproduced directly during
this audit. That is a live operator-contract Section 8 portability
violation ("the same repo must run identically on any qualified machine")
on the exact machine currently serving as primary production.

Everything else is documentation drift adjacent to contract rules rather
than a hard-rule violation: `docs/architecture.md`'s "Autonomous flow"
section still describes the L3 audit trigger and the mission-queue job
purely via `launchd` (macOS-only) with no mention of the Windows Task
Scheduler path that a sibling doc (`docs/for-analysts.md`) already
documents, and the `com.melons.agents.queue` autonomous mission-runner has
no Windows equivalent at all; the subagent roster count is internally
contradictory across two docs (22 vs 23) and both are now behind the live
file count of 24 after today's `cut-judge.md` landed; `docs/roadmap.md`'s
Done section (Section 9) has a growing citation gap of at least 5 un-cited
commits; and `docs/audit/CURRENT-ALERT.md` still displays a CRITICAL
verdict whose underlying findings were already fixed by the commit that
produced the report it cites.

## Critical / High findings

- **[high]** `skills/job-hunt/scripts/status.sh` crashes on this machine — CRLF line endings in the manifest it reads — `skills/job-hunt/scripts/status.sh:55-61`, `skills/job-hunt/config/activation.tsv`

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
