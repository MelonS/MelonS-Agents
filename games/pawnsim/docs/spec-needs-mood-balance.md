# Spec — Needs / Mood / Mental-Break Balance (deferred #200 items)

Status: PROPOSAL — operator approval required before any code change.
Author: systems-designer subagent. Date: 2026-05-30.
Source audit: `skills/game-prototype/docs/audit-genre-fidelity-2026-05-29.md`
(rows "Mood decay" and "Mood-break threshold", §4 "Uncertain values").

This document covers ONLY the two behavior-logic items the #200 audit deferred
because they change *behavior*, not just numbers:

1. Mood "free-fall" — the mood model is conceptually wrong.
2. Mental break — collapsed to 1 tier; the reference sim has 3.

The numeric-only #200 fixes (food decay 0.5→0.14, move speed, skill XP, body-part
HP) are already landed and are explicitly OUT of scope here. Nothing in this spec
should touch those values.

---

## 1. As-is — how mood + mental break work NOW

Two scripts both write to the single field `PawnNeeds.mood` (0–100), and they
disagree. This is the root of the "free-fall" flag.

### 1a. `PawnNeeds.cs` — the decay path

- `mood = 80` initial. `moodDecay = 0.2`/sec (`:24`).
- Every frame mood is pushed *down*: `mood = max(0, mood - moodDecay*dt - weatherPenalty)`
  (`:104`). It has no floor other than 0 and no baseline it settles toward — it is a
  monotonic timer that drains to 0 unless something adds to it.
- Additions come only from discrete events: eating a meal (`+10/+20`, `:144,154`),
  sleeping on a good bed (`+MoodBonus/sec`, `:82`).
- Net effect at idle: a pawn doing nothing loses ~0.2/sec = ~48 mood per in-game day
  (240s) and reaches a mental break in well under one day regardless of circumstances.
  THIS is "free-fall."

### 1b. `PawnThoughts.cs` — the (correct-shaped but conflicting) thought path

- Already implements the colony-sim-shaped model: `CurrentMood = baseMood(50) + Σ thought.offset`,
  clamped 0–100 (`:81-89`).
- Has a thought `Catalog` (`:28-42`) with both positive ("최고의 식사" +12, "푹 잠" +4)
  and negative ("배고픔" -4, "수면 부족" -3, "동료 사망" -15) offsets and expiry timers.
- **Conflict:** `PawnThoughts.Update()` (`:103-107`) does
  `if (active.Count > 0) needs.mood = CurrentMood;` — it *hard-overrides* the field
  PawnNeeds is simultaneously decaying. So mood is whichever script wrote last this
  frame, and when no thoughts are active the decay timer wins and free-falls.
- Thoughts are only added on a few events; the persistent need states (hungry, tired)
  do NOT continuously emit thoughts, so the offset model is starved of inputs and the
  decay model fills the vacuum.

### 1c. Mental break (`PawnNeeds.cs:40-46, 110-118`)

- Single threshold: `moodBreakThreshold = 20`, recover at `moodBreakRecoverAt = 35`,
  `moodBreakDuration = 30s`.
- One behavior. `IsBreaking` becomes true when mood < 20; it stays true until both
  `Time.time > breakUntil` AND mood > 35.
- Consumed in `PawnUtilityAI.cs:123-139`: a breaking pawn clears all work tasks and
  wanders randomly within `idleWanderRadius`. That is the *only* break behavior — a
  harmless "sad wander" for every break, no tiers, no aggression.

### 1d. Why the audit flagged them

- **Mood decay (MED):** "Mood is a sum of thought offsets around a baseline (~50–60%),
  not a monotonic timer; it does not auto-drain to 0." The decay path is conceptually
  wrong AND it actively fights the already-correct PawnThoughts path.
- **Mood-break threshold (MED):** "Three tiers: minor 35% / major 20% / extreme 5%;
  break is a mean-time-to-event below each, not instant." The prototype's single 20
  threshold ≈ the reference sim "major"; minor and extreme tiers are missing, and break is
  instant-on-cross rather than a probabilistic mean-time-to-break.

---

## 2. the reference sim reference

Sources: Mental break Mental Break Threshold Mood ### 2a. Mood model

- Mood is the **sum of all active thought mood-offsets**, evaluated continuously and
  clamped 0–100%. There is no decay timer; mood *settles* wherever the thought sum lands.
- Thoughts come from two families:
  - **Need-driven** (continuous while the need state holds): hungry, tired, low
    recreation, uncomfortable environment (cold/hot/dark/ugly). These flip on/off with
    the need and provide a *negative floor* when the colony is failing.
  - **Event-driven** (timed, decaying): ate a fine meal, slept in a good bed, witnessed
    a death, got injured, etc.
- Net result: a well-supplied colonist hovers comfortably above baseline; a failing one
  is dragged down by stacked negative need-thoughts — never by a blind timer.

### 2b. Break thresholds + behaviors

| Tier | Default mood threshold | Example breaks | Aggression |
|---|---|---|---|
| Minor | **35%** | Sad wander, Hide in room, Food binge, Insulting spree, Crying | Non-aggressive (yellow icon) |
| Major | **20%** | Tantrum (smash structures), Daze/psychotic wander, Sadistic rage, Insane ramblings | Mixed; tantrum is property-aggressive |
| Extreme | **5%** | Berserk (melee any nearby creature), Fire-starting spree, Catatonic breakdown | Aggressive (red icon) |

- Below a tier's threshold the pawn has a **mean-time-to-break** (probabilistic),
  not an instant trigger. Higher tiers are rarer/more severe.
- Berserk: pawn loses control, melee-attacks any living creature nearby, won't
  intentionally kill, retargets when current target downs.
- Tantrum: pawn attacks/destroys colony structures, may leave rubble.

---

## 3. Proposed prototype model (simplified, the reference sim-shaped)

Design intent: make `PawnThoughts` the single source of truth for mood (kill the
free-fall decay), feed it continuous need-driven thoughts, and split the one break
into three tiers reusing systems we already have. This is conservative — it deletes a
conflict and extends an existing-shaped system rather than adding a new one.

### 3a. Mood = thought sum (remove free-fall)

- **`PawnThoughts` becomes authoritative.** `PawnNeeds.mood` becomes a mirror of
  `CurrentMood`, written every frame (not just when `active.Count > 0`).
- **`moodDecay` → 0** (or the decay line removed). Mood no longer drains on a timer.
- **Continuous need-driven thoughts**, added/refreshed by PawnNeeds each tick when the
  need crosses a state boundary, removed when it recovers. Proposed mapping to EXISTING
  needs (no new need types — recreation/temperature stay out per #200 §3):

  | Condition (existing field) | Thought label | Offset | Notes |
  |---|---|---|---|
  | `food < 25` | 배고픔 (exists) | -4 | already in Catalog; emit continuously while hungry |
  | `food < 10` | 굶주림 (new) | -12 | severe; stacks the colony toward major/extreme |
  | `sleep < 20` | 수면 부족 (exists) | -3 | emit continuously while exhausted |
  | outdoor + Storm | 야외 폭풍 (exists) | -6 | already emitted via weather path |
  | any body part injured | 부상 (exists) | -5 | wire from PawnHealth (optional, phase 3) |
  | slept full on good bed | 푹 잠 (exists) | +4 | event, already partly there |
  | ate fine/normal meal | 최고의/맛있는 식사 (exists) | +12/+5 | event, already wired |

  Baseline stays `baseMood = 50`. A fed, rested pawn with no negatives sits at ~50–60
  (the reference sim-shaped), a failing one is pulled to break range by stacked negatives.

### 3b. Three break tiers

Replace the single `moodBreakThreshold` with three, matching the reference sim defaults:

| Tier | Threshold (mood <) | Prototype behavior | Reuses |
|---|---|---|---|
| Minor | 35 | **Sad wander** — clear work, wander aimlessly (current behavior) | existing `PawnUtilityAI` wander path |
| Major | 20 | **Tantrum** — pawn walks to nearest built structure and "attacks" it (damage tick), or if none, sad-wander | melee damage path / structure HP |
| Extreme | 5 | **Berserk** — pawn melee-attacks nearest living pawn/animal | existing drafted-melee `HandleDraftedCombat`-style attack |

- Keep the **mean-time-to-break** simplification optional: phase 1 may keep
  instant-on-cross (current behavior) to limit risk, with probabilistic MTB as a phase-3
  refinement.
- Recovery: break ends when `Time.time > breakUntil` AND mood climbs back above that
  tier's recover offset (e.g. threshold + 15, preserving the current 20→35 gap).
- `IsBreaking` (bool) is preserved for back-compat; add a `BreakTier` enum
  (`{ None, Minor, Major, Extreme }`) so `PawnUtilityAI` can branch behavior. Existing
  `IsBreaking` callers keep working (`IsBreaking => BreakTier != None`).

### 3c. Telemetry hooks (for QA)

- Expose read-only `CurrentMood`, active thought labels+offsets (PawnInfoPanel already
  shows breakdown per `PawnThoughts` docstring), and `BreakTier`.
- QA expectation examples:
  - Fed+rested idle pawn after 60s: mood settles 50–60, never trends to 0.
  - Starve a pawn (food→0): 배고픔+굶주림 stack → mood ≤ 35 → Minor break (sad wander).
  - Drive mood < 20: Major break, structure-attack observable.
  - Drive mood < 5: Extreme break, pawn attacks a nearby pawn/animal.

---

## 4. Implementation phases (daily-shippable)

Each phase is independently shippable and reversible. Effort = rough dev-hours.

### Phase 1 — Kill the free-fall (LOW effort, ~1–2h) — RECOMMENDED FIRST
- `PawnNeeds.cs`: set `moodDecay = 0` (or remove the `mood -=` decay term at `:104`
  and the sleeping-path `mood -=` at `:81`); make `PawnThoughts` authoritative.
- `PawnThoughts.cs`: change `:104` from `if (active.Count > 0)` to always-write
  `needs.mood = CurrentMood;` so the field never free-falls when thoughts are empty.
- Add continuous 배고픔 / 수면 부족 emission from `PawnNeeds.Update()` (add when below
  state threshold, the Catalog labels already exist; PawnThoughts dedups by label).
- Files changed: `PawnNeeds.cs`, `PawnThoughts.cs`.
- Result: mood settles around baseline; the conceptual bug is gone. Single break tier
  unchanged (still works). This alone closes the #200 "Mood decay" row.

### Phase 2 — Three break tiers (MED effort, ~3–4h)
- `PawnNeeds.cs`: replace `moodBreakThreshold` with `minorBreak=35 / majorBreak=20 /
  extremeBreak=5` + matching recover offsets; add `enum BreakTier` and `BreakTier`
  property; keep `IsBreaking => BreakTier != None`.
- `PawnUtilityAI.cs`: branch on `BreakTier` in the existing `IsBreaking` block (`:123`):
  Minor = current wander; Major = walk-to-structure + damage tick; Extreme = attack
  nearest living target (reuse drafted-melee attack code path).
- Files changed: `PawnNeeds.cs`, `PawnUtilityAI.cs` (+ possibly a tiny break-behavior
  helper). No new files strictly required.
- Result: closes the #200 "Mood-break threshold" row.

### Phase 3 — Refinements (LOW–MED, optional, ~2–3h)
- Add 부상 thought from `PawnHealth` (continuous while injured).
- Add 굶주림 (-12) severe-hunger thought.
- Convert instant-on-cross break to probabilistic mean-time-to-break.
- Break icon (yellow/red lightning) in `PawnInfoPanel` for QA visibility.
- Files: `PawnHealth.cs` (emit thought), `PawnNeeds.cs` (MTB), UI panel.

---

## 5. What NOT to break

- **#200 needs decay values** — do NOT touch `foodDecay = 0.14`, `sleepDecay = 0.3`,
  `sleepRegenAtNight`, or any body-part HP. This spec only touches `moodDecay` (the
  free-fall term) and the break thresholds.
- **`IsBreaking` contract** — `PawnUtilityAI.cs:123` and any other reader depends on the
  bool. Keep `public bool IsBreaking` working; layer `BreakTier` alongside it.
- **`IsSleeping` path** — the sleep block (`:73-91`) returns early; mood handling there
  must stay consistent (drop the `mood -=` decay there too in phase 1).
- **Eat/sleep mood additions** — meals (+10/+20) and good-bed bonuses currently write
  `needs.mood` directly. Once PawnThoughts is authoritative these must route through
  `AddThought` (the meal path already calls `AddThought("최고의 식사", ...)` — verify no
  double-counting between the direct `mood +=` and the thought offset).
- **PawnTraits.moodBaselineBonus** (`:55-63`) — applied once to `mood` at start. With a
  thought-sum model this should instead adjust `PawnThoughts.baseMood`; otherwise the
  one-time bump is overwritten on the next frame. Note for whoever implements phase 1.

---

## 6. Open questions for the operator

1. **Scope gate:** approve Phase 1 only (free-fall fix, lowest risk), or Phase 1+2
   (full 3-tier) in one go? Recommendation: Phase 1 today, Phase 2 next session.
2. **Double-count cleanup:** meals currently both `mood += 10` AND `AddThought`. OK to
   remove the direct `mood +=` so meals only act through thoughts? (Cleaner, but changes
   the felt magnitude slightly.)
3. **Trait baseline:** move `moodBaselineBonus` to `PawnThoughts.baseMood`? (Required for
   it to survive the authoritative-thought change — flagging because it touches #164.)
4. **Break determinism:** keep instant-on-cross (deterministic, easy to QA) or move to
   probabilistic mean-time-to-break (the reference sim-faithful, harder to test)? Recommend
   instant for phases 1–2, MTB as optional phase 3.
5. **Major-break target:** tantrum should damage *colony structures* — acceptable for a
   pawn to damage player buildings in a prototype, or prefer a safer "bedroom tantrum"
   that only flails without real damage? (Risk: pawn destroying a wall mid-raid.)
6. **Extreme-break target:** berserk attacks "nearest living" — include other colonists
   (true the reference sim), or restrict to animals/enemies only to avoid colonist-on-colonist
   death in a prototype with small HP pools?

---

## Sources
- Mental break - Mental Break Threshold - Mood - #200 audit: `skills/game-prototype/docs/audit-genre-fidelity-2026-05-29.md`
