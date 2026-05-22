# Operator tooling

Single-page catalog of the scripts that surface system state and
absorb routine status-check prompts, so the operator doesn't have
to type them.

These are the scripts referenced under "Operator tooling" in the
README ([EN](../README.md#design-notes) /
[KO](../README.ko.md#설계-노트)) and the Pages site's
["Operator tooling"](https://melons.github.io/MelonS-Agents/) card.
This file is the single index where each tool has:

- **what it does** in one sentence
- **when to invoke** (the moment the tool exists for)
- **exit / output contract** (what to check after running)

If you're an external reader doing a first-pass on the repo, this
plus [`for-analysts.md`](for-analysts.md) is enough to understand
the daily-operations layer without reading every script.

---

## At a glance

| Tool | When | Output |
|------|------|--------|
| `scripts/morning-brief.sh` | When you start the day or return from a break | ~30-line digest (doctor + audit + intervention trend + commits + decisions + review queue + blockers) |
| `scripts/doctor.sh` | After a fresh clone, machine reboot, or several-day gap | Pass/warn/fail tally + actionable count; `--json` for programmatic use |
| `scripts/audit-skill-drift.sh` | Manually never — embedded into the daily auditor pass | One-line summary or per-skill drift table |
| `scripts/statusline.sh` | Never directly — Claude Code calls it ~300 ms per refresh | One-line statusline (dir · git · model · cost · doctor · goal · audit) |
| `scripts/log-decision.sh` | When the agent makes a unilateral decision during autonomous work | Append to `docs/autonomous-decisions.md` under today's date section |
| `scripts/intervention-log-add.sh` | When the operator surfaces a shaping decision (direction / taste / correction / etc.) | Append synthesized entry to `docs/intervention-log.md` under today's date section |
| `scripts/roadmap-done-sync.sh` | Whenever the audit flags Done-gap, or before a session end | Preview (default) or `--apply` bulk entry to `docs/roadmap.md` |
| `scripts/review-queue-add.sh` | Auto-called by mission post-render — manual rarely | Pending entry under `outputs/review-queue/pending/` |
| `scripts/review-queue-digest.sh` | When you want to drain pending taste decisions in one sitting | Render to `docs/review-digest.md` as a contact sheet |
| `scripts/review-queue-decide.sh` | After reviewing the digest | Move pending → decided with verdict |
| `scripts/intervention-chart-collect.sh` | Auto via launchd daily 02:00 KST — manual after big code change | Regenerates `docs/metrics/intervention-{en,ko}.png` + `.json` |
| `skills/goal-lock/scripts/check-done.sh` | When you want to see active goal subgoal status | Markdown report or `--quiet` / `--json` for statusline consumption |
| `scripts/audit-run.sh` | Auto via L1 post-commit hook + L2 15-min poll + L3 daily 03:00 cron | Audit report at `docs/audit/<date>-<focus>.md` + `CURRENT-ALERT.md` |

---

## Per-tool reference

### `scripts/morning-brief.sh` — the daily wake-up command

```bash
scripts/morning-brief.sh                    # English (default unless $LANG is ko)
scripts/morning-brief.sh --lang ko          # Korean
scripts/morning-brief.sh --since "yesterday"  # custom commit window
```

Combines seven sources into one read:

1. **Health** — `scripts/doctor.sh --quiet` summary
2. **Audit** — verdict + timestamp from `CURRENT-ALERT.md`, or CLEAN
3. **Intervention trend (7-day)** — last-7d avg + Δ vs prior-7d for
   user-ratio / leverage / prompts / minutes
4. **Commits in last window** — count + agent-vs-user attribution +
   most recent 5
5. **Today's autonomous decisions** — entries from
   `docs/autonomous-decisions.md` under today's date section
6. **Review queue** — pending count from `outputs/review-queue/pending/`
7. **Blockers** — count from `records/blockers/<today>/`

Read-only.  Safe to re-run.  Exits 0 in all cases (status is in the
content, not the exit code).

### `scripts/doctor.sh` — fast Claude-free health check

```bash
scripts/doctor.sh           # full report (color when TTY)
scripts/doctor.sh --quiet   # one-line summary
scripts/doctor.sh --json    # machine-readable
```

Twelve checks: CLI tools, ffmpeg+libass, ollama reachable, .env,
required env keys, launchd schedulers, audit alert, git tree+sync,
disk free, per-skill activation, skill manifest drift, intervention
trend (chart-as-alert).

Exit codes: 0 = all PASS, 1 = some WARN, 2 = some FAIL.

JSON output includes both raw `warn` and `actionable_warn` (the
latter excludes opt-in env-key + git-tree noise so the statusline
count reflects items the operator actually wants to see).

### `scripts/log-decision.sh` — autonomous decision recorder

```bash
scripts/log-decision.sh "Lever 1 invalidated after spot-check"
scripts/log-decision.sh --time 14:30 "explicit timestamp"
```

Appends one bullet to `docs/autonomous-decisions.md` under today's
date section (auto-creates the header if today isn't there yet).
Newest-first within each day's section.

Use whenever the agent makes a unilateral decision during autonomous
work — operator wakes up, scans one page in <60 s, understands what
was decided and what was decided *not* to do (lever dismissals
recorded too, so the same hypothesis doesn't get re-explored).

Per `[[idempotency-test-first]]` memory: same-day calls correctly
nest under one date header (validated by
`scripts/test-log-decision.sh`).

### `scripts/roadmap-done-sync.sh` — Done-section auto-reconciliation

```bash
scripts/roadmap-done-sync.sh           # preview to stdout
scripts/roadmap-done-sync.sh --apply   # write bulk entry to roadmap
scripts/roadmap-done-sync.sh --since=<sha>  # explicit base
```

Removes the recurring manual work that audit cycles 39+ kept
flagging (Done section drifting N commits behind HEAD).

Auto-detects the base SHA from the most-recently-mentioned 7-char
commit hash in the Done section.  Lists every commit since then
that doesn't already appear in Done.  In `--apply` mode, prepends
a single bulk entry containing those commits.  Idempotent — a
re-run only adds commits that aren't already covered.

Sanity guard: refuses to overwrite if the new file isn't strictly
larger than the original (caught a regression where the v1 awk
nuked roadmap.md the first try).

Validated by `scripts/test-roadmap-done-sync.sh` (5/5 PASS).

### `outputs/review-queue/` + 3 scripts — batched taste-decision queue

```bash
scripts/review-queue-add.sh <mission_id> <artifact_path> [reason]
scripts/review-queue-digest.sh
scripts/review-queue-decide.sh <mission_id> approve|reject|archive [note]
```

`-add.sh` is auto-called by the music-video mission post-render
(see `agents/missions/music-video/run.sh`).  Operator drains the
batched contact sheet via `-digest.sh` on their cadence (daily,
weekly, etc.), then individually decides via `-decide.sh`.

10× fewer intervention events than per-render pings, same total
decision count.

### `scripts/intervention-chart-collect.sh` — chart regeneration runner

Auto via launchd job `com.melons.agents.intervention-chart` daily
02:00 KST.  Manual invocation only after a large code change worth
visualizing immediately.

Output: `docs/metrics/intervention-en.png` + `intervention-ko.png`
+ `intervention.png` (backward-compat alias = EN) + `.json`.  Also
mirrors all variants into `site/assets/`.

### `skills/goal-lock/scripts/check-done.sh` — active goal subgoal counter

```bash
bash skills/goal-lock/scripts/check-done.sh           # markdown
bash skills/goal-lock/scripts/check-done.sh --quiet   # one-line
bash skills/goal-lock/scripts/check-done.sh --json    # {checked, unchecked, total, all_done}
```

Read by `scripts/statusline.sh` to render `goal:N/M`.  Operator
can also run manually to see which subgoals of the active goal
are still unchecked.

Exit codes: 0 = at least one subgoal remains, 1 = all done, 2 =
malformed `docs/goal.md`.

---

## How the tools compose

The stack is layered:

```
                ┌────────────────────────────────────┐
                │      operator opens the day         │
                └─────────────────┬───────────────────┘
                                  ▼
                     scripts/morning-brief.sh
                            (~30 lines)
                                  │
            ┌─────────────────────┼─────────────────────┐
            ▼                     ▼                     ▼
   scripts/doctor.sh      docs/autonomous-     docs/metrics/
       --quiet            decisions.md         intervention.png
            │             (today's section)            │
            ▼                                          ▼
   skills/goal-lock/                          .json's trend_7d
   scripts/check-done.sh                              │
            │                                          │
            └────────────────────┬─────────────────────┘
                                 ▼
                     scripts/statusline.sh
                  (always-visible in Claude Code)
```

The morning brief is the front door.  Doctor + the chart are the
two slow data sources behind it.  Goal-lock + the decisions log
+ review queue are the operator-action surfaces it points at.
Statusline is the always-visible thin summary.

Together they answer the four most-frequent status-check prompts
without typing:

1. **"what's the state?"** → statusline (doctor:⚠N · goal:N/M · audit⚠)
2. **"what happened overnight?"** → morning-brief
3. **"what's left to do?"** → goal-lock + roadmap "Now"
4. **"any alerts?"** → audit-alert flag in statusline / morning-brief

See [`case-study #8`](engineering-case-studies.md#8-intervention-as-the-unmeasured-axis--autonomy-signal--reduction-levers)
for the longer-form write-up.
