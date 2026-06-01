# PawnSim — Milestones (PM source-of-truth)

Owner: **game-pm**. This is the single production-management view of the
PawnSim (colony-sim-style colony sim) prototype: what's **shipped**, what's
**in-flight this session**, and what's **queued** — grouped into named,
daily-shippable milestones.

- **Outcome layer**: [`goal.md`](goal.md) — "바닐라 콜로니심급 prototype + 정직한 작동 검증" (R/V/P series Done-when).
- **Work queue (legacy step list)**: [`ROADMAP_40H.md`](ROADMAP_40H.md) — Step 38→78 sprint, now mostly historical.
- **Design/polish backlog**: [`design-improvement-backlog.md`](design-improvement-backlog.md) — A1–A10, U1–U9 (DONE) + Polish Wave v3 (V1–V8, partial).
- **Session history**: [`AUTONOMOUS_SESSION_SUMMARY.md`](AUTONOMOUS_SESSION_SUMMARY.md) — narrative log through #209.

**Last updated**: 2026-05-30 (PM, parallel session).

---

## Operating constraints (every milestone respects these)

- **Daily-shippable**: every day ends with a launch-able `PawnSim.exe` — menu
  opens, world renders — even if the day's feature is incomplete. Reject any
  day plan where the end-of-day build wouldn't open.
- **No "QA at the end"**: each increment ends with `refactor_check.py` PASS
  (isolated + integration + Build Click QA + REAL QA + visual diff). A failing
  gate = no advance (fix-in-place or roll back to last-good).
- **기능추가 보수적, 게임이 되는게 먼저** (operator): breadth features (power /
  trading / taming / bills) stay deferred until the core *plays well* and is
  *verified*. Depth/polish/verification before new vanilla systems.
- **Logic-change rule (§5)**: behavior-logic changes (mood free-fall, mental-break
  tiers, work-priority semantics, needs/mood rebalance) require explicit operator
  OK before landing. These are flagged below as **[OP-OK]**.

---

## 1. Current state snapshot — SHIPPED

Synthesized from the session summary. Coverage estimate **~85%** of the reference sim
vanilla core systems; the active goal is to push verified coverage to 90%+ and
to raise visual polish from ~7/10.

| Area | Shipped | Commit range |
|------|---------|--------------|
| **Core sim** | Pawn utility-AI (Strategy pattern), needs (food/sleep/mood), 4 skills + XP, 8 traits, weather, day/night (real NightOverlay darkening) | Step 38–78 |
| **Health & combat** | 6 body-parts health (bleed/downed/death), drafted state (R-key), bow+arrow ranged (research-gated), wolf predator, arrow accuracy spread | Step 45–64 |
| **Research** | ResearchBench (2×1, wiki-aligned) + 5-tech tree (tier deps) + auto-first-tech + manipulation-sum progress | Step 52–54, #169, #195 |
| **Build/economy** | wall/floor/door/stove/bench/bed build, cooking, farming (grow+harvest), hunting, 5-tier stockpile priority, bed quality (Sleeping/Wood/Fine) w/ rest+mood mul | Step 57–68, #153–155 |
| **Meta** | 3 storytellers (Steady/Calm/Chaos), threat tier 0–3 → event frequency, 15 events, tutorial overlay, save/load | Step 73–78, #175 |
| **★ Grid pathfinding (#199)** | the reference sim full-grid cutover: PathGrid + A* (8-dir, octile, no corner-cut, cap 4000), pawn 1×1 collider fix, wall path-block (ref-count), door pass-through, adjacent work-cell (TryGetAdjacentStandCell), ReservationManager (no double-occupy), build-placement validation (reject water/rock/occupied + toast) | `61dd2e7`, `3b2c373`, `c8aa5ca`, `7271079` |
| **★ Balance (#200)** | Fidelity audit (~28 systems vs wiki) + Top-5 tuning: hunger 0.5→0.14 (3-day starve), move 3.0→4.6 + wolf chase 5.0, head HP 10→20 / torso 30→40, arrow desc fix, skill XP base ×10 | `ad6326f` |
| **★ Eject / wall-trap fix (#201)** | EjectPawnsFromCell (the reference sim push-out) + PawnMovement.Update standing safety-net (idle pawn in blocked cell → nearest walkable). I42 reproduces the bug. | `c5e9e22` |
| **★ Design/UI overhaul (#202–208)** | Style civil war resolved → single `palette.py`; flat colonist + 2px outline (3 variants); muted terrain; unified wood; full UI panel system (MakeBorderedPanel: control bar / tooltip / inspector / name-plates / floating bars / top bar / architect / tutorial / S-L); resource icons; neon removed; drop shadows. Polish ~3/10 → ~6.5–7/10. | `e558ef3`–`46ed505` |
| **Architecture** | refactor_check.py 6-stage harness; PawnStats + HealthPartsConfig SO; PawnUtilityAI Strategy (IPawnAction + 6 actions); ServiceLocator; PlayMode TestRunner; SceneSetup 1057L → 310L across 14 partial files | R1–R10m |
| **Recent wiki-fidelity** |림 이동 제어 + 목재 부패 + 2nd wall fix (#197), 건축 실작동 fix (#196), ResearchBench 2×1 (#195), sprite audit + 11종 재생성 (#194), 침대 1×2 multi-cell (#193) | #193–#197 |

**Verification baseline (last green)**: isolated 76/76 · integration 42/42 ·
Build Click QA 9/9 · REAL QA wood +55 · GATE GREEN (post-#208).

---

## 2. In-flight — THIS SESSION (parallel agents)

Running concurrently; PM does not block on these but tracks them for the next
gate. Each must land with a green harness + daily-shippable build.

| Item | Stream | Status | Note |
|------|--------|--------|------|
| **Hauling / work-priority fix** | programmer | in-flight | hauler behavior correctness; pairs with deferred work-priority spec |
| **Action SFX** | programmer + audio | in-flight | chop/build/cook/combat action sound feedback (AudioBank wiring exists) |
| **World scatter + tree art** | art | in-flight | Polish Wave **V2** (scatter decals) + **V4** (richer tree canopy) |
| **Harness blind-spot fix** | qa | in-flight | close the "verifier wasn't being read" class of gap (cf. #198 Build Click QA prefix mismatch — harness self-verification) |
| **Spec: work-priority** | director/pm | drafting | the reference sim work-tab priority semantics; **[OP-OK]** before behavior lands |
| **Spec: needs/mood balance** | director/pm | drafting | mood free-fall + mental-break 3-tier (audit MED, #200 deferred); **[OP-OK]** |
| **This doc (MILESTONES.md)** | pm | this task | PM source-of-truth |

---

## 3. Queued / backlog (priority-ordered)

Pulled from goal.md unchecked series + design backlog v3 remainder + deferred
specs. Ordered by "advances the most-blocked goal subgoal, lowest scope-risk
first."

### A. Verification & architecture (goal.md R/V series) — HIGHEST (goal core)
- [ ] **R5** — PawnUtilityAI Strategy: confirm full IPawnAction + 6 action-class coverage (goal lists open; R-series log shows partial — reconcile).
- [ ] **R6** — ServiceLocator: confirm 5 singletons fully migrated (log shows R6 done — reconcile vs goal checkbox).
- [ ] **R7** — PlayMode auto-verify: drafted/wolf/research/arrow/crop 5 scenarios as a gated suite (TestRunner exists; promote to goal-grade gate).
- [ ] **V1–V9** — promote "code exists" → "verified": drafted tint, wolf detection, research progress, arrow hit, crop harvest, body-parts damage+bleed, storyteller tier@day7, map-obstacle stop, mood-break. (Many have V-tests already; gap is goal-grade binary sign-off.)

### B. Polish Wave v3 remainder (design backlog) — HIGH (operator-visible "alive")
- [ ] **V1 (polish)** — Pawn walk-bob + idle breathe (sprite-child offset only; root untouched for pathfinding). **NOTE: prior walk-bob attempt needs retry — see Risks.**
- [ ] **V3 (polish)** — Contact shadows extended to pawns + trees + buildings (grounding pass).
- [ ] **V2/V4 (polish)** — world scatter + richer tree → IN-FLIGHT this session (§2).
- [ ] **V5 (polish)** — ambient micro-motion (stove/fire flicker, optional tree sway).
- [ ] **V6 (polish)** — crop growth-stage markers (seedling/growing/ripe-gold).
- [ ] **V7 (polish)** — inspector empty-state styling.
- [ ] **V8 (polish)** — optional subtle vignette (reject if it muddies palette).

### C. Visual polish (goal.md P series) — MEDIUM
- [ ] **P5** — pawn 32×32 detailed sprite (face/hair/clothes pixels) — *note: flat-style 2px-outline colonist already shipped (#202); reconcile whether P5 is superseded by the design-overhaul direction.*
- [ ] **P6** — night demo screenshot (22:00 capture).
- [ ] **P7** — combat sequence screenshot set (idle → draft → enemy → arrow → death).

### D. Deferred behavior specs — MEDIUM, **[OP-OK] gated**
- [ ] **Work-priority** system (the reference sim work-tab) — spec in-flight; build pending operator approval.
- [ ] **Needs/mood rebalance** — mood free-fall + mental-break 3-tier (#200 audit MED, explicitly held).

### E. Persistence & known gaps — MEDIUM
- [ ] **SaveLoad fidelity** — BedQuality / StockpilePriority / TreeSpecies / WallMaterial not serialized (reverts to default on load). V-scenario coverage.
- [ ] **carryCapacity → PawnHauler cap** (high efficiency impact, heavy work).
- [ ] **BanditEnemy body parts** (reuse PawnHealth, effort M).
- [ ] **PawnSkills 14-type expansion** (Cook/Mine/Medical/Intellectual).

### F. Stretch / vanilla breadth — LOW, **[OP-OK] gated** (operator: 보수적)
- [ ] Power grid (generator + battery + wire + lamp).
- [ ] Trading caravan + buy/sell UI (Trader entity exists; full UI pending).
- [ ] Animal taming (offer food → success roll).
- [ ] Stockpile filter logic (priority done; filter pending).
- [ ] Bills queue at workbench.

---

## 4. Milestone grouping (named, shippable)

Each milestone is internally split into daily-shippable increments; every day
ends with a launchable build + green harness. Effort is rough and padded ~30%
over raw estimates (team runs slower than estimates).

### M0 — Verification hardening **(in-flight + immediate next)**
*Goal-core: the active goal is "정직한 작동 검증," so this outranks new features.*
- Harness blind-spot fix (in-flight) — close verifier-not-read class of bug.
- Reconcile R5/R6/R7 goal checkboxes against the actual R-series log; promote
  TestRunner to a goal-grade gated suite.
- V1–V9 binary sign-off (drafted/wolf/research/arrow/crop/body-parts/
  storyteller/obstacle/mood) — each as a single PASS line in the gate.
- **Deliverable**: goal.md R/V series fully ticked or explicitly justified.
- **Effort**: ~2–3 increments. **Daily-shippable**: yes (test-only, build unaffected).

### M1 — "Living colony" polish (Polish Wave v3 completion)
*Operator's loudest remaining gap: "everything is frozen / world is empty / pasted on."*
- Walk-bob + idle breathe (V1) **[retry — see Risks]**.
- Contact shadows across pawns/trees/buildings (V3).
- World scatter + richer tree (V2/V4 — in-flight, fold in here).
- Fire flicker / tree sway (V5).
- Crop growth-stage markers (V6) + inspector empty-state (V7) + optional vignette (V8).
- Action SFX (in-flight) folds in as the audio half of "alive."
- **Deliverable**: a still screenshot reads as a paused *living* world (motion
  implied, grounded objects, inhabited map). Target polish 7 → 8.5/10.
- **Effort**: ~3–4 increments. **Daily-shippable**: yes (each item ships alone).

### M2 — Persistence & combat-depth correctness
*Make existing systems trustworthy before adding new ones.*
- SaveLoad fidelity (BedQuality/StockpilePriority/TreeSpecies/WallMaterial) + V-scenario.
- carryCapacity → hauler cap; hauling/work-priority fix (in-flight) lands here.
- BanditEnemy body parts (reuse PawnHealth).
- P6 night + P7 combat-sequence demo screenshots.
- **Deliverable**: save→load round-trips all entity state; combat + hauling verified.
- **Effort**: ~3 increments. **Daily-shippable**: yes.

### M3 — Colony-management depth **[OP-OK gated]**
*Behavior-logic changes — needs explicit operator approval before landing.*
- Work-priority system (the reference sim work-tab) — spec in-flight.
- Needs/mood rebalance: mood free-fall + mental-break 3-tier.
- Stockpile filter logic; PawnSkills 14-type expansion.
- **Deliverable**: colonists obey a player-set work priority; mood/break feels vanilla.
- **Effort**: ~3–4 increments, **blocked on operator OK** for each behavior change.

### M4 — Vanilla-feature breadth **[OP-OK gated, conservative]**
*Explicitly deferred per "기능추가 보수적." Do NOT start without operator direction.*
- Trading caravan UI (entity exists), power grid, animal taming, bills queue.
- **Deliverable**: 90%+ verified vanilla coverage.
- **Effort**: each feature ~2 increments (Day-N stub + Day-N+1 polish to hold
  daily-shippable). **Sequenced last by operator constraint.**

**Sequence**: M0 (now) → M1 (in-flight) → M2 → M3 (on OP-OK) → M4 (on OP-OK).
M0/M1/M2 are pure depth+polish+verification (no operator gate); M3/M4 wait.

---

## 5. Risks / dependencies / operator decisions needed

### Needs operator decision (blocking M3/M4 + one M1 item)
1. **Mood free-fall + mental-break 3-tier** — behavior-logic change (#200 audit
   MED, deliberately held). **[OP-OK]** before it lands.
2. **Work-priority semantics** — spec drafting; the *behavior* needs **[OP-OK]**
   (changes how pawns choose work). Spec doc can be written without OK; wiring cannot.
3. **KlingAI menu art** — 7.4k points available. Unsuitable for in-game pixel
   sprites (breaks cohesion) but **good for ONE main-menu key-art** →
   `Assets/Sprites/menu_bg.png`. Operator decides: want the prompt? If a PNG is
   dropped in, programmer wires it. Pure go/no-go.
4. **head HP = 20** — estimate (wiki head ≈25 unverified). SerializeField-tunable
   if operator wants the real value.
5. **Arrow dmg vs description** — description corrected to "3–5 dmg" to avoid a
   full combat rebalance. Making arrows actually stronger = re-scale all enemy HP
   (separate, larger task). Operator decides scope.

### Technical risks
- **Walk-bob retry (M1/V1)** — a prior walk-bob attempt exists in flight; the
  hard constraint is **offset the SpriteRenderer child transform ONLY**, never
  the root. Root is what `PathGrid.WorldToCell` / movement / clamp / reserved
  cells / floating bars read — bobbing the root desyncs pathfinding. Any retry
  must include the QA check "no pawn drifts off its cell over 10s of walking."
- **Goal-vs-log reconciliation** — goal.md shows R5/R6/R7 unchecked but the
  R-series session log shows R5 (Strategy), R6 (ServiceLocator), R7 (TestRunner)
  as done. M0 must reconcile (tick or justify) so the goal layer is honest — this
  is exactly the "queue empty ≠ goal met" trap CLAUDE.md warns about.
- **P5 vs design-overhaul** — goal.md P5 asks for 32×32 *detailed* pawn, but the
  #202 design overhaul deliberately moved to *flat* 2px-outline colonists (the
  detailed style was the "potato in a pot" problem). P5 may be superseded;
  operator/director should confirm before anyone redraws a detailed pawn.

### Dependencies
- M1 polish items all touch the SceneSetup pawn/ground/building builders +
  `palette.py` / `_gen_fix_audit.py` → serialize art edits to avoid generator
  conflicts (A1's single-palette discipline must hold).
- M0 verification gate is a prerequisite signal-of-health for M2+ (don't build on
  unverified ground).
- Every milestone depends on the harness staying green; the in-flight harness
  blind-spot fix (M0) protects every later gate.
