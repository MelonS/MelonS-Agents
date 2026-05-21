---
name: goal-lock
description: A discipline helper for autonomous and long sessions — given the active goal in `docs/goal.md`, list the unchecked deliverable subgoals (the actual `- [ ]` items), report how many remain, and let the operator (or an autonomous worker) decide whether the next iteration should keep advancing the goal or whether the goal's `Done when` criteria are already satisfied.  Does *not* alter the agent loop or override the harness — this is a transparency / discipline tool, not a runtime hook.
license: MIT
compatibility: Requires `bash` and `awk`.  macOS or Linux.  Reads `docs/goal.md` from the repo root.
metadata:
  authors: MelonS-Agents
  version: "0.1.0"
  pipeline-source: scripts/check-done.sh
  spec: agentskills.io
  added: "2026-05-22"
  status: minimal scaffold — parses goal.md active-goal subgoals, reports unchecked count.
allowed-tools: Bash(bash:*) Bash(awk:*) Read
---

# goal-lock

A small discipline helper.  When an autonomous worker or a long
interactive session is advancing the active goal in `docs/goal.md`,
it's useful to know *at a glance* which deliverable subgoals are
still unchecked.  That's the entire purpose of this skill.

## What this does

Reads the first `### YYYY-MM-DD | ...` subsection under `## Active
goal` in `docs/goal.md` and lists every `- [ ]` (unchecked)
deliverable subgoal.  Also reports the count and the
"Done when" line if present.

## What this does NOT do

- Does not alter the Claude Code harness's turn loop.  The agent
  still runs whatever the operator or scheduler invoked.
- Does not auto-tick subgoals.  Operator (or the
  per-commit hook that ticks subgoals on relevant commits) does
  that.
- Does not enforce any policy.  This is read-only reporting.  If
  the operator wants autonomous mode to halt when all subgoals
  are checked, they wire `scripts/check-done.sh`'s exit code
  into their `AUTONOMY_MODE` driver.

## Why this exists

`docs/goal.md` "Done when" criteria are prose plus a list of
deliverable subgoals.  Without a one-liner check, a long
autonomous session has to either (a) re-read the entire goal file
between iterations or (b) drift past the goal because no one
re-checked completion.  This skill makes the check a single
`bash` call.

## Invocation

```bash
# Default — list unchecked subgoals + counts
bash skills/goal-lock/scripts/check-done.sh

# One-line summary
bash skills/goal-lock/scripts/check-done.sh --quiet

# Machine-readable JSON
bash skills/goal-lock/scripts/check-done.sh --json
```

Exit codes:

- `0` — at least one subgoal remains unchecked (work is still to do)
- `1` — all subgoals are checked (goal is *probably* done; the
  `Done when` prose may add further conditions the operator must
  read manually)
- `2` — `docs/goal.md` missing, malformed, or active goal section
  empty

## How autonomous mode can use this

The recommended pattern for an autonomous loop is to call
`check-done.sh` after each iteration:

```bash
while bash skills/goal-lock/scripts/check-done.sh --quiet >/dev/null; do
  # advance the goal — fetch next roadmap Now item, do one chunk,
  # commit + push
  ./scripts/autonomous-iterate.sh || break
done
```

The loop exits naturally when `check-done.sh` returns 1 (no more
unchecked subgoals), which is the closest we can get to Hermes'
runtime `/goal` lock-in without modifying the Claude Code harness.

## Scope explicitly out

- **Parsing the "Done when" prose** for arbitrary conditions
  (e.g., "tag v0.x.0 landed").  Those conditions are operator-
  defined and not generally parseable; the operator writes a
  per-goal check script if needed.
- **Auto-ticking subgoals** based on git activity.  The existing
  `docs/operator-contract.md` §9 already says Claude appends to
  roadmap "Done" and ticks subgoals when the relevant commit
  lands — that flow is unchanged by this skill.
- **Multi-goal locking.**  Only the *active* goal is reported.
  Candidate / past / abandoned sections are ignored.

## How it parses goal.md

- Skip everything until the line that starts with `## Active goal`.
- Read the first `### ` subsection below it.
- Within that subsection, find lines starting with `- [ ]` (six
  characters exactly).  Each is an unchecked subgoal.
- Stop at the next `## ` heading (e.g., `## Next goal`).
- The "Done when" line is whatever line in the active goal
  subsection begins with `**Done when**:` or `Done when:` — verbatim
  pass-through to the report.
