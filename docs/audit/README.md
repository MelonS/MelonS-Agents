# `docs/audit/` — repository audit trail

This directory holds the output of the **auditor subagent**
(`.claude/agents/auditor.md`).  The auditor reads the repo end-to-end
and writes a structured findings report.  It changes nothing.

Reports are committed (this directory lives under `docs/`, not
`records/`) so the audit trail survives a machine swap — that's the
whole point of having it.

## File types

### Dated reports — `YYYY-MM-DD-<focus>.md`

One file per audit invocation.  `<focus>` is `all` by default, or one
of `architecture` / `roadmap` / `contract` / `cost` / `security`
when the caller wants a narrower pass.

Each report follows a fixed structure (defined in
`.claude/agents/auditor.md`):

```
# Audit report — <ISO-date> (<focus>)

**Verdict**: CLEAN | DRIFT_DETECTED | CRITICAL

## Summary
## Findings
## Drift between docs and code
## Stale items
## Compliance with operator-contract.md
## Next audit hint
```

The `**Verdict**:` line at the top is contractual — the wrapper script
`scripts/audit-run.sh` parses it to decide whether to write the alert
file (below).

### Active alert — `CURRENT-ALERT.md`

Exists **iff** the most recent audit verdict is non-CLEAN
(`DRIFT_DETECTED` or `CRITICAL`).  Self-clearing — when the next audit
returns to `CLEAN`, the file is deleted.

Written by `scripts/audit-run.sh`, not by the auditor itself.  The
auditor stays read-only; the wrapper does the surface work.

If `CURRENT-ALERT.md` exists at the start of a session, treat it as
higher priority than `docs/roadmap.md` "Now" — drift unaddressed
becomes worse drift.

### Directory marker — `.gitkeep`

Holds the empty state of the directory under version control so the
audit path is stable before the first audit runs.  Safe to leave
alone.

## Triggering audits

Three layers of triggers, each with its own latency/scope trade-off.

### L1 — Reactive: post-commit hook

`scripts/hooks/post-commit.sh` (installed via
`scripts/install-hooks.sh install`) fires `audit-run.sh contract` in
the background whenever a commit touches drift-risk paths:
`.claude/agents/`, `agents/`, `config/`, `CLAUDE.md`,
`docs/operator-contract.md`, `scripts/audit-run.sh`,
`.claude/settings.json`.

- Latency: ~30 s from commit landing to audit-report write.
- Cost: ~1 Sonnet call per drift-risk commit (no fire on pure
  docs/roadmap commits).
- Skip with `AUDIT_HOOK_DISABLED=1 git commit ...`.
- Override focus with `AUDIT_HOOK_FOCUS=all git commit ...`.

Not auto-installed by `bootstrap.sh` — opt-in to avoid surprise
Anthropic spend on a fresh cloner's first commit.

### L2 — Reactive: 15-min mission-anomaly poll

`scripts/audit-poll.sh` (via `com.melons.agents.audit-poll.plist`,
loaded by `scripts/install-scheduler.sh install audit-poll`) runs
every 15 minutes.  Fires `audit-run.sh` only when an anomaly
pattern matches:

- New blocker file in `records/blockers/<date>/` → fires
  `audit-run.sh all`.
- ≥2 mission `qa-report.md` files with `Verdict: FAIL` whose mtime
  falls within the same 60-minute window → fires `audit-run.sh
  contract`.

State at `records/audit/poll-state.json`; trigger log at
`records/audit/poll-trigger.log`.  First-run mode seeds the
seen-blockers list with whatever's already on disk and does NOT
fire — stops false-positive on existing pre-install blockers.

### L3 — Scheduled baseline

`scripts/com.melons.agents.auditor.plist` is loaded into launchd via
`scripts/install-scheduler.sh install auditor`.  Fires
`audit-run.sh all` daily at **03:00 local time** — the always-on
baseline that catches anything L1 + L2 missed.

`RunAtLoad=false` — installing the scheduler does NOT trigger an
immediate audit (avoid surprise token spend at install time).

### On demand

```bash
./scripts/audit-run.sh             # full audit, focus=all
./scripts/audit-run.sh roadmap     # roadmap freshness only
./scripts/audit-run.sh contract    # operator-contract compliance only
./scripts/audit-run.sh security    # secret leakage + .gitignore only
./scripts/audit-run.sh cost        # cost-model accuracy only
./scripts/audit-run.sh architecture
```

Each invocation costs ~$0.05 in Sonnet tokens.  L3 (daily) gives
~$1.50/month baseline; L1 + L2 add ~$1–3/month depending on commit
cadence + mission anomaly rate.

### Regression test for the alert wrapper

```bash
./scripts/test-audit-parser.sh
```

Exercises the verdict-parsing block in `audit-run.sh` against
synthetic CLEAN / DRIFT_DETECTED / CRITICAL reports and verifies the
`CURRENT-ALERT.md` state transitions.  Does NOT call the auditor
itself; runs in a `/tmp` sandbox.  Use after any edit to the parser.

## Retention

All dated reports are kept indefinitely.  They're small (a few KB
each), and the trail itself is the value — drift over time is easier
to read against a long history.

If the directory ever gets unwieldy, the rule will be: keep all
CRITICAL / DRIFT_DETECTED reports forever; the last 30 days of CLEAN
reports; one CLEAN per quarter beyond that.  Not yet implemented; the
auditor only just went autonomous on 2026-05-15, so there is no
volume to manage.

## When an alert exists — playbook

1. Read `CURRENT-ALERT.md`.  Note the verdict and the linked full
   report path.
2. Open the full report — the wrapper only surfaces critical/high
   findings, but medium/low can matter when they cluster.
3. For each finding, apply the **Suggested fix** line if you agree
   with the auditor's judgement, or push back in a commit message
   explaining why the finding is acceptable.
4. After fixes land, re-run `./scripts/audit-run.sh` to confirm the
   verdict returns to CLEAN.  The wrapper will auto-delete the alert
   on a clean run.

The auditor is not the user.  It surfaces options; the human
(or this agent under direction) decides.

## See also

- `.claude/agents/auditor.md` — agent definition and six audit
  dimensions.
- `scripts/audit-run.sh` — invocation wrapper + verdict parser.
- `scripts/test-audit-parser.sh` — regression test for the parser.
- `scripts/com.melons.agents.auditor.plist` — launchd job for the
  daily 03:00 audit.
- `docs/proposals/2026-05-15-auditor-active.md` — the active-surface
  design that produced this directory's current shape.
