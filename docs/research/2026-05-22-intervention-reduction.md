# Operator-intervention reduction — analysis + plan

_Date: 2026-05-22._  Pair with the chart at
[`docs/metrics/intervention.png`](../metrics/intervention.png) and the
JSON at [`docs/metrics/intervention.json`](../metrics/intervention.json).

## Why this exists

Operator framed the question on 2026-05-22 ~01:35 KST: continuously
collect data on operator-intervention **count and time**, and use the
trend to drive intervention down.  "운영자(컨트리뷰터) 의 개입은 줄이고
스스로 작업하는 시간을 늘려야함."  The chart was added 2026-05-17,
silently dropped from the README in `aa10ba0` (2026-05-18 README rewrite
to music-video-first), and the data was 2 days stale.  This pass
re-anchors it.

## Two-panel signal

Panel A — git log:

- `user-initiated` vs `agent-autonomous` commit count per day
- `user-ratio %`
- `leverage ratio` (`agent / max(1,user)`) — autonomous commits per
  operator nudge
- `longest_autonomous_gap_h` — longest gap between two consecutive
  user-initiated commits, within the day

Panel B — Claude Code session JSONLs at
`~/.claude/projects/-Users-melons-ai/`:

- `operator_prompts` — count of real text prompts from operator
  (excludes `tool_result` auto-replies)
- `active_session_minutes` — sum of session durations, capped 60min
  per session so an idle laptop doesn't inflate the signal

Both panels share the date axis.  Reduction = both move down.

## Current state (2026-05-14 → 2026-05-22)

| date  | user | agent | ratio | lev  | gap-h | prompts | mins  |
|-------|------|-------|-------|------|-------|---------|-------|
| 05-14 |  0   | 30    | 0%    | 30   | 0     |  52     | 120   |
| 05-15 |  6   | 17    | 26%   | 2.8  | 0.9   | 110     | 180   |
| 05-16 | 10   | 42    | 19%   | 4.2  | 10.2  |   4     |  30   |
| 05-17 | 40   | 18    | 69%   | 0.5  | 3.3   | 317     | 236   |
| 05-18 | 13   | 13    | 50%   | 1.0  | 4.8   |   7     |  57   |
| 05-19 |  7   | 27    | 21%   | 3.9  | 14.6  |  86     | 280   |
| 05-20 |  6   | 46    | 12%   | 7.7  | 12.2  | 279     | 339   |
| 05-21 | 20   | 36    | 36%   | 1.8  | 14.5  |  70     | 168   |
| 05-22 |  0   | 16    | 0%    | 16   | 0     |   5     |  29 (partial) |

Median user-ratio ≈ 20%.  2026-05-17 spike (69%) was the day shaders +
site + scorecard + chart landed — heavy operator taste calls, all the
artifacts the README now showcases.  2026-05-20 hit the best leverage
(7.7x) on autonomous overnight skill-#2 work.

## Reduction levers (highest-impact first)

### 1. Classifier false-positive: Korean direct quotes

Cause: `USER_PATTERNS` regex `[\"“]\s*[가-힣]` (opening quote followed
by Hangul) was added so the classifier catches operator-quoted
direction in commit bodies.  Side effect: any commit body that *cites*
a prior operator quote as historical context gets tagged "user", even
when the commit work itself was autonomous.

Evidence: 2026-05-21 `cc6a104` (`activate kr-saramin OpenAPI live
path`) is tagged user because the body says
`operator-issued key pending Saramin's approval queue`.  The work
itself was autonomous overnight.

Fix: tighten the regex to require the quote to be in the **first
line of body** OR follow a phrase like `Operator direction:`, not
just any Hangul-after-quote anywhere in the body.  Estimated effect:
drop user-ratio 5-10pp on multi-quote commits.

### 2. Default to recommended option ([[minimize-intervention]])

Already memorized: when there's an obvious recommended option, execute
it with a one-line "doing X; reply stop to override" rather than
opening an A/B/C menu.  Apply more aggressively to:

- Goal-promotion decisions where a single candidate is unambiguous
- README-cadence batches when only one trigger is current
- Mission queue picks when there's a single eligible candidate

### 3. Batch taste reviews instead of per-artifact pings

Today's pattern: each new mp4 / preset / shader effect → operator
review → continue.  Each review is a context switch for the operator
and counts as a session.

Alternative: a weekly **review queue** that the operator drains in one
sitting (Sunday afternoon say).  Items go in `outputs/review-queue/`
with a one-line description; weekly digest renders them as a
single-page contact sheet.  Operator pulls up the page, ticks/crosses,
commits the verdict.  Token count for ten artifacts ≈ same as today,
but session count drops from 10 to 1.

### 4. Statusline / dashboard absorbs "status check" prompts

Many Panel B prompts on quiet days are "where are we?" or "what's
running?" / "did the audit fire?" — the new `scripts/doctor.sh`
already answers this in 2s without a Claude session.  Promote
`doctor.sh` output into the Claude Code statusline so the operator
sees it without typing anything.  Optional follow-on: render a
HTML version into `docs/site/` so the public page also shows current
state.

### 5. Permission-prompt bootstrap (shipped)

The 2026-05-19 friend-test surfaced ~30 user-level Claude Code
permission prompts per session.  `feat/permission-bootstrap`
(`c496a0a`) landed in v0.3.0 with `scripts/install-claude-permissions.sh`
auto-installed by `bootstrap.sh`.  Expected effect: drop prompts/day
median by 50-100% on fresh-clone first session for any new user.

## Signal sources surveyed (for future expansions)

- **git log** — primary, already mined.  Reliable since 2026-05-17
  marker convention.
- **`~/.claude/projects/-Users-melons-ai/*.jsonl`** — local Claude
  Code session logs; one file per session.  Mined for prompt count
  + session duration.  Local-only, not in repo.
- **`docs/daily/*.md`** — human session reports; could be NLP'd for
  decision-density signal but not worth the complexity now.
- **`records/missions/<date>/<id>/`** — mission timestamps; could give
  autonomous-work-time per mission but already implicit in commit
  cadence.
- **`records/audit/hook-trigger.log`** — L1 audit hook fire log; useful
  as autonomous-correction signal (audit fires without operator
  noticing).
- **`records/blockers/`** — autonomous-mode halt logs; non-empty days
  signal operator-intervention-required events.  Currently empty for
  the visible range.

If the chart later wants a third panel, the blockers + audit-hook
combination would show "autonomous self-correction count" — the
agent system catching its own drift without escalating.

## Automation in place after this pass

- `scripts/generate-intervention-chart.py` — extended to two panels,
  session-mining included
- `scripts/intervention-chart-collect.sh` — runner with venv bootstrap
- `scripts/com.melons.agents.intervention-chart.plist.template` —
  launchd job firing daily 02:00 KST
- `scripts/install-scheduler.sh intervention-chart` — install/uninstall
  + status

The data accumulates daily without operator action.  README
re-references the chart so the trend is always one click away.

## Next checkpoint

Re-read this memo on 2026-05-29 (7-day check).  Ask: does the median
user-ratio drop below 15%?  Does Panel B's `operator_prompts/day`
drop below 30 on routine days?  If yes, the levers work; if no,
return to lever 1 (classifier tightening) or lever 3 (batch reviews).
