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

### Scheduled (default)

`scripts/com.melons.agents.auditor.plist` is loaded into launchd via
`scripts/install-scheduler.sh install auditor`.  It fires
`audit-run.sh all` daily at **03:00 local time**.

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

Each invocation costs ~$0.05 in Sonnet tokens.  The launchd schedule
gives one daily full audit (~$1.50/month).

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
