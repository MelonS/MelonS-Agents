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
