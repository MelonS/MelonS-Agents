# Proposal: make the auditor actively surface findings

**Date**: 2026-05-15 (written overnight)
**Status**: partially implemented + awaiting user approval on remainder
**Owner**: Claude (proposing) → user (approving the gated parts)

## Why this proposal exists

User question (verbatim, paraphrased): *"감시자가 능동감시되는가? 기본
감시 패턴이 있고 문제 발견 시 처리/보고를 해야 하지 않나?  LLM이 기본
으로 붙어줘야 할 듯한데?"*

Current state (as of 2026-05-15 evening):
- `.claude/agents/auditor.md` defines six audit dimensions and writes a
  structured report to `docs/audit/<ISO-date>-<focus>.md`.
- `scripts/audit-run.sh` invokes the auditor and writes the report.
- launchd job `scripts/com.melons.agents.auditor.plist` triggers
  `audit-run.sh all` daily at 03:00 local.

What's missing for "능동":
- The audit report goes into a dated file that no one notices unless
  they explicitly browse `docs/audit/`.  CRITICAL findings would sit
  there silently.
- There's no signal pathway from "audit found drift" → "next session
  sees it" → "user is asked to resolve."

## Four decisions made + chosen design

### 1. What patterns does the auditor watch?

**Decision**: keep the six dimensions already in `auditor.md` —
architecture-vs-docs drift, roadmap freshness, operator-contract
compliance, cost-model accuracy, stale TODOs, security/secrets.  These
are well-defined and the agent prompt already enforces evidence-based
findings.  **No change to auditor.md from this proposal.**

### 2. What LLM tier does the auditor run on?

**Decision**: tiered, not "Sonnet for everything."

| Stage | What | Tier |
|------|------|------|
| 1 | File presence, mtime, grep patterns, `git log` checks | bash (free) |
| 2 | Severity classification, drift summarization | (currently Sonnet — could be Haiku later) |
| 3 | Verdict synthesis + recommendation language | Sonnet (current) |

For now, the agent stays single-stage Sonnet (which is what
`.claude/agents/auditor.md` frontmatter currently specifies).  **Tier
split is a v2 optimization, not in scope tonight.**  Empirically the
daily audit will cost <$0.05/run, well within budget.

### 3. How are findings surfaced to the next session?

**Decision**: a stable, committed alert file at
`docs/audit/CURRENT-ALERT.md`.

Why this path:
- Stable (no date in filename) so the next session looks at one place,
  not the latest of N dated files.
- Inside `docs/` so it's committed → survives a machine swap and shows
  up in `git status` if it was just created.
- Maintained by `audit-run.sh` (script-level), not by the auditor agent
  itself (would require editing `.claude/agents/auditor.md`).

Lifecycle:
- Non-CLEAN verdict on the latest run → `CURRENT-ALERT.md` written /
  overwritten with summary + critical/high findings + path to full
  report.
- CLEAN verdict → `CURRENT-ALERT.md` deleted.

**This is implemented in `scripts/audit-run.sh` already** (this
session's edit; verdict parsing is post-hoc and wrapped in `|| true`
so it cannot break the audit itself).  Verified parsing on three
synthetic verdicts (CLEAN, DRIFT_DETECTED, CRITICAL).

### 4. Does the auditor act on findings, or only report?

**Decision**: report only.

The operator contract has a hard rule: **logic changes need explicit
OK**.  An auditor that modifies `.claude/agents/`, `agents/`, or core
config files unattended violates that rule.  The active surface stops
at "make the human notice."

This applies to the auditor.  Acting on findings happens in the next
user session, with the user's approval.

## What this session shipped (no user approval needed)

These changes are all script-level / docs-level, inside the autonomy
envelope:

1. `scripts/audit-run.sh` — added `parse_verdict_and_alert()` step:
   - Greps the verdict line from the audit report.
   - On CLEAN: removes `docs/audit/CURRENT-ALERT.md` if present.
   - On DRIFT_DETECTED / CRITICAL: writes `CURRENT-ALERT.md` with
     verdict, summary block, critical/high findings, link to full
     report, and resolution instructions.
   - Failure mode: any parsing failure logs a warning but does not
     fail the audit run.
2. `docs/ideas.md` — separate work item, not part of this proposal.
3. This proposal document.

## What needs user approval (NOT shipped tonight)

### A. Auditor agent self-documents the alert surface

To make the active-surface protocol discoverable from the agent
definition itself, propose adding this paragraph to
`.claude/agents/auditor.md` under "Principles":

> **Alerting**: your report is post-processed by
> `scripts/audit-run.sh`, which extracts the Verdict line and writes
> `docs/audit/CURRENT-ALERT.md` whenever the verdict is non-CLEAN.
> The alert auto-clears when the next run returns CLEAN.  You don't
> write `CURRENT-ALERT.md` — the wrapper does.  Just keep the report
> structure exact, especially the `**Verdict**: …` line, so the parser
> finds it.

Why gated: edits to `.claude/agents/auditor.md` are logic changes per
the operator contract.

### B. Session-start protocol mentions CURRENT-ALERT.md

Propose adding to `CLAUDE.md` under "Session-start protocol":

> If `docs/audit/CURRENT-ALERT.md` exists, read it before picking up
> the roadmap "Now" item.  It means the last audit run flagged drift
> or a critical issue.  Resolving the alert may bump roadmap priority.

Why gated: CLAUDE.md is project-wide instructions; treating its edits
like agent-definition edits is conservative but appropriate.

### C. (Optional, v2) Haiku for stage-2 classification

Defer until daily audit cost actually shows up as noise.  Current
projection: Sonnet daily-only ≈ $0.05/day = $1.5/month.  Below noise
floor relative to mission-generation budget.

## How to apply parts A and B (one user "OK")

Tomorrow morning, after reading the morning briefing, if the user
approves:

```
# Apply A
$EDITOR .claude/agents/auditor.md     # paste the paragraph under Principles
# Apply B
$EDITOR CLAUDE.md                     # paste the line under session-start
git add .claude/agents/auditor.md CLAUDE.md
git commit -m "feat: auditor active surface — agent + session protocol"
git push
```

Or have me do all three lines in one edit pass with explicit OK.

## Verification

Before next 03:00 launchd fire, the post-processing was tested with
three synthetic audit reports (verdicts CLEAN / DRIFT_DETECTED /
CRITICAL).  Verdict extraction worked for all three.  Summary block
extraction tested on DRIFT case; findings filter (high+critical only)
returned the expected line.

If the 03:00 run produces a CLEAN verdict, `CURRENT-ALERT.md` won't
exist tomorrow morning.  If it produces non-CLEAN, the file will be
there, ready for review.

## Rollback

If the parsing causes unexpected behavior, revert
`scripts/audit-run.sh` to commit `7003a53` (or the commit just before
this proposal lands).  No other files need touching.  The auditor
agent itself is untouched.
