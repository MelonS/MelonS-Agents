# Autonomous decisions log

> Append-only log of decisions the agent made **without operator input**
> during autonomous (overnight / between-session) work.  Goal: when the
> operator wakes up they can scan a single page to see what was decided,
> instead of typing "어디까지 했어?" / "what's the state?" prompts and
> driving up the
> [intervention chart](metrics/intervention.png) Panel B count.

## What goes here

- Decisions where the agent picked one option from several plausible
  paths (per [[minimize-intervention]] "default to recommended option")
- Lever / hypothesis dismissals — when a planned approach turned out
  wrong on closer look
- Architecture / scope nudges the agent made unilaterally because of
  [[infra-maintenance]] / [[no-premature-done]]
- Cross-session decisions affecting future agent runs

## What does NOT go here

- Atomic implementation moves (those are in `git log`)
- Status / progress reporting (use `docs/daily/<date>.md` for narrative)
- Goal changes (those need operator OK — go in `docs/goal.md`)
- Anything in `agents/*.md` / `.claude/agents/*.md` (logic-changes-need-OK)

## Helper

```bash
scripts/log-decision.sh "Lever 1 classifier tightening dropped — spot-check showed no false positives"
```

Appends one bullet under today's section.  Auto-creates the date
header if today's section doesn't exist.

---

## 2026-05-22 (overnight autonomous)

- `14:20 KST` — scripts/test-install-claude-local.sh shipped — 7 asserts validate idempotency + legacy migration + path substitution. test-all.sh auto-discovers via ls scripts/test-*.sh glob. Caught a regression: legacy '└─...┘ -->' was wrongly treated as block-end, leaking body content; fixed.
- `14:00 KST` — install-claude-local.sh idempotency bug fixed — single-line BEGIN/END markers + improved awk pattern + pre-substitution of @@REPO_ROOT@@ before awk feed. Operator's ~/.claude/CLAUDE.md cleaned 271→142 lines, 9 stacked openers→1 single comment, md5 stable across 3 reruns
- `13:45 KST` — Intervention chart split EN/KO + visual polish — bigger figure, legends below panels (no bar overlap), per-bar % labels only (removed totals clutter), CJK font for KO. README EN→intervention-en.png, README.ko→intervention-ko.png
- `05:30 KST` — CLAUDE.md session-start protocol now includes reading docs/autonomous-decisions.md + suggests morning-brief.sh. Future session starts auto-discover the overnight signal stack
- `05:20 KST` — Hardcoded '-Users-melons-ai' in generate-intervention-chart.py replaced with str(ROOT).replace('/','-') derivation. Plus docs/review-digest.md gitignored. Plus 11-commit Done backlog reconciled.
- `05:15 KST` — intervention-chart-collect.sh now auto-mirrors PNG into site/assets/ — no more manual copy step needed. Daily 02:00 KST launchd job keeps site asset in sync
- `05:05 KST` — L1 audit hook trampoline now auto-logs audit verdict transitions (DRIFT→CLEAN or CLEAN→DRIFT) to autonomous-decisions.md. Morning brief sees these without needing alert diffs
- `04:55 KST` — morning-brief.sh surfaced in README EN+KO + site/index.html (Operator tooling card). Discoverability for the canonical 'what happened overnight?' command
- `04:45 KST` — L1 post-commit audit hook gains coalescing lock — sentinel file at records/audit/.hook.inflight tracks in-flight pid; subsequent drift-risk commits within an audit's runtime defer rather than spawn new claude CLI processes. Saves Max-plan tokens during commit bursts
- `04:35 KST` — scripts/morning-brief.sh shipped — single-command digest combining doctor + audit + intervention trend + commit attribution + decisions + review queue + blockers. Operator types one command, reads ~30 lines, knows overnight state
- `04:20 KST` — Phase 16 — intervention.json gains trend_7d field (last7 avg + prev7 avg + delta + direction hints). 7-day comparison populates from 2026-05-29; currently null prev7 since only 9 days of data
- `04:10 KST` — README EN+KO Design notes section refreshed — operator tooling bullet now describes all 5 reduction scripts (doctor, audit-skill-drift, statusline, log-decision, review-queue)
- `04:00 KST` — §8 registry restructured to drop line numbers — anchors by filename + pattern instead, audited via grep. Prevents coordinate-staleness from recurring (39th audit's structural-fix suggestion)
- `03:55 KST` — Site refresh: case-studies count 6→8, operator-tooling card expanded (statusline + log-decision + review-queue), intervention chart copy refreshed to 2-panel, alt text updated
- `03:45 KST` — Daily report written — docs/daily/2026-05-22-overnight-intervention.md — operator can scan this + autonomous-decisions.md in <2min on return
- `03:40 KST` — 38th audit cleared 10 prior findings + flagged 5 new (4 low + 1 medium); all 5 addressed in this batch
- `03:35 KST` — Engineering case study #8 written EN+KO — intervention measurement as the unmeasured axis. Portfolio signal per [[repo-as-credibility-signal]]
- `03:20 KST` — Phase 6 shipped — doctor.sh now reports actionable_warn (excludes opt-in env keys + git-tree). Statusline doctor:⚠N count dropped from 7 to 3 (real items only)
- `03:05 KST` — Lever 10 shipped — statusline now surfaces goal:N/M subgoal progress alongside doctor health
- `03:00 KST` — Autonomous-decision log infrastructure shipped (lever 9)
- `02:55 KST` — Closed 7-cycle §8 audit drift across 7 scripts
  (`ffmpeg-throttled`, 5 music-video helpers, `doctor.sh`) +
  rewrote the §8 exception registry in `docs/operator-contract.md`
  with correct line numbers; updated `docs/architecture.md` Layers
  table to document the `outputs/publish/upload-meta-v2/` v2 batch
  exception (intentional 5/21 a182380) and added a row for
  `outputs/review-queue/`.  Expected next audit verdict: CLEAN.
- `02:50 KST` — **Lever 1 (classifier tightening) INVALIDATED.**
  Spot-checked 5 commits flagged as user-initiated false-positives
  in the reduction memo; all 5 were legitimately user-initiated
  (`cc6a104` has `Requested-by: user` footer; others have explicit
  "Operator strategic shift" / "Per operator '다해봐'" prefixes).
  Conclusion: classifier is tuned correctly; the 36% 5/21 ratio is
  honest signal, not over-counting.  Dropped lever 1; will not
  spend cycles on regex tweaks.
- `02:50 KST` — Shipped **lever 3 (review queue, lever 4 already
  committed earlier)**.  `outputs/review-queue/` + 3 scripts
  + music-video mission post-render hook.  New renders enqueue
  automatically instead of pinging the operator.  Expected effect:
  ~10× drop in per-render review prompts (same total decision
  count, batched).
- `02:35 KST` — Shipped **lever 4 (statusline absorbs doctor signal)**.
  `scripts/statusline.sh` now reads `/tmp/cc-doctor-cache.json`
  (60s TTL, background regen) and renders `doctor:✓/⚠N/✗N` +
  `audit⚠` suffix when `docs/audit/CURRENT-ALERT.md` exists.
  Operator no longer has to type "what's the state?" prompts.
- `02:00 KST` — **Restored intervention chart** to README EN + KO
  under new "Autonomy signal" section.  Chart had been silently
  dropped in 5/18 `aa10ba0` README rewrite; data was 2 days stale.
  Extended the generator to 2-panel signal (commits + Claude Code
  session JSONL mining for prompt count + active minutes).
  Daily 02:00 KST launchd job (`com.melons.agents.intervention-chart`)
  installed for ongoing auto-refresh.

## How to interpret this log

When the operator opens the laptop in the morning, they should be
able to read this section top-to-bottom in <60s and understand:

1. What's now different in the repo (each bullet ties to a commit
   range — see git log if details needed)
2. What was decided NOT to do (lever dismissals are recorded too —
   so the same hypothesis doesn't get re-explored)
3. What's still queued (nothing here means nothing queued; the
   roadmap "Now" / "Next" is authoritative for that)

If a bullet's reasoning needs to be revisited, the corresponding
commit message + git diff is the durable record; this log just
makes it scannable.
