# PawnSim — genre fidelity Audit (2026-05-29)

Scope: read-only audit of `unity-project/Assets/Scripts/` gameplay constants vs the reference sim
vanilla 1.5/1.6 values. **No code was changed.** This report is the prioritized,
actionable discrepancy list the operator asked for.

A core sim-clock fact governs every time-based number below:
**1 in-game day = 240 real seconds** (`GameClock.realSecondsPerInGameDay = 240`,
`GameClock.cs:17`). So when the reference sim states "per day," the prototype-equivalent
real-second rate at 1× speed is `the reference simPerDay / 240`. Decay rates in
`PawnNeeds.cs` are expressed in **units (0–100) per real second**, so to be
the reference sim-faithful a need that should empty over N in-game days must decay at
`100 / (N × 240)` units/sec.

---

## Severity rubric

- **HIGH** — breaks the colony-sim game feel or is wildly off (order-of-magnitude error;
  e.g. a need empties in seconds, a wolf dies in one hit, skills max out instantly).
- **MED** — noticeably off but still playable; tuning would clearly improve fidelity.
- **LOW** — cosmetic / minor tuning; safe to ignore for a prototype.

Prototype-appropriate simplification (5 techs instead of ~80, 4 skills instead of 12,
flat per-part HP instead of nested organs) is **NOT** flagged as a bug — only values
that are wrong in a way that hurts the colony-sim feel.

---

## 1. Discrepancy table

| System | Prototype value (file:line) | the reference sim value (source) | Severity | Suggested fix |
|---|---|---|---|---|
| **Food decay** | `foodDecay = 0.5`/sec → 0→100 in 200s = **0.83 in-game days** to starve from full (`PawnNeeds.cs:18`) | Hunger 1.6 nutrition/day; a fed pawn goes from Fed→Starving over **~2.5–3 in-game days** ([Saturation], [Needs]) | **HIGH** | Lower to ~`0.14`/sec (100 / (3×240)). At minimum ≤0.21 (≈2 days). Pawns currently starve ~3× too fast. |
| **Sleep decay** | `sleepDecay = 0.3`/sec → awake-to-exhausted in 333s = **1.4 in-game days** (`PawnNeeds.cs:19`) | Rest falls so a pawn needs sleep after ~**16h awake**, full rest takes 10.5h; cycle ≈1 day ([Rest], [Sleep Fall Rate]) | **MED** | Slightly slow is OK. ~`0.20`/sec (≈0.83 day awake budget) is closer; current value is mild. Low priority vs food. |
| **Sleep regen** | `sleepRegenAtNight = 8`/sec → 0→100 in 12.5s = **0.05 in-game day** (`PawnNeeds.cs:23`) | 0→100 rest takes **10.5 h** (~0.44 in-game day) at 100% effectiveness ([Rest]) | **MED** | Regen is ~9× too fast — pawns "nap" trivially. ~`1.0`/sec is closer (still gamified). Pairs with the sleep-decay note. |
| **Sleep trigger** | sleeps only if `sleep<30 && night` (`PawnNeeds.cs:69`); night = 22:00–06:00 (`:196`) | Pawns sleep on schedule when rest <~28%, day or night ([Rest], [Needs]) | LOW | Acceptable prototype simplification (schedule UI exists separately). |
| **Mood decay** | `moodDecay = 0.2`/sec, always falling toward 0 (`PawnNeeds.cs:20,100`) | Mood is a **sum of thought offsets around a baseline (~50–60%)**, not a monotonic timer; it does not auto-drain to 0 ([Mood]) | **MED** | Conceptually off (mood shouldn't free-fall), but thoughts/meals offset it. Lower to ~`0.05` so mood settles rather than plummets; full rework is out of scope ("기능추가 보수적"). |
| **Mood-break threshold** | single `moodBreakThreshold = 20`, recover at 35 (`PawnNeeds.cs:37-38`) | Three tiers: **minor 35%, major 20%, extreme 5%**; break is a mean-time-to-event below each, not instant ([Mental Break Threshold]) | **MED** | Prototype collapses 3 tiers to 1. The single threshold ≈ the reference sim's "major" (20%). Fidelity win: rename/treat 20 as "major," optionally add a 35 minor. Low effort, but logic-ish — flag for operator. |
| **Skill XP curve** | `XPToLevel(L) = 100·L^1.5` → L0→1 = 100 XP, L1→2 = 283, L2→3 = 520 (`PawnSkills.cs:66`) | the reference sim: **1000 XP per level flat up to ~10**, rising after; ~4000 XP/day soft cap ([Skills]) | **MED** | Early levels come ~10× too cheap (100 vs 1000). Multiply base from 100→~`1000` (and/or flatten the exponent) so early skill gains aren't trivial. |
| **Work speed per level** | `+0.04 (4%) per level`, linear (`PawnChopper.cs:131`, `PawnBuilder.cs:116`, `PawnGatherer.cs:109`, `PawnMiner.cs:98`) | Construction/Mining **+~15%/level** (≈×3 at lvl20 from a low base); cooking ~11%/level ([Skills]) | LOW–MED | 4%/level (×1.8 at lvl20) is flatter than the reference sim's curve but a defensible prototype choice. Bump to ~0.06–0.08 if a stronger skill-progression feel is wanted. |
| **Combat dmg per level** | `+0.03 (3%) per Combat level` (`PawnUtilityAI.cs:249,273`) | Melee skill scales hit chance + dodge, not flat damage; shooting scales accuracy ([Skills]) | LOW | Reasonable simplification; not a "wrong value." |
| **Body part HP** | head **10**, torso **30**, arm 18, leg 20 (`HealthPartsConfig.cs:27-32`) | torso **40**, head ~25 (brain 12 inside), arm(shoulder) ~30, leg ~30 ([Table of Human Body Parts], [Body Parts]) | **MED** | All parts low; head especially (10 vs 25) makes head-death far too easy. Suggest head≈25, torso≈40, arms≈30, legs≈30. |
| **Head death/downed** | head HP=0 → death; head <30% → downed (`PawnHealth.cs:197,212`) | Brain destruction kills; downing is from **pain/blood-loss/consciousness**, not raw head % ([Health]) | LOW | Simplification is fine; the *value* fix is the head HP above. |
| **Bleed model** | `bleed = dmg×0.25`, capped 3.0 HP/s, decays 0.05/s (`PawnHealth.cs:103-107`) | Bleeding is per-wound bleed-rate summed into a blood-loss need over hours; tend stops it ([Health]) | LOW | Gamified but plausible; cap prevents instakills. Keep. |
| **Pawn base HP** | `maxHp = 30` flat pool (`PawnStats.cs:23`); used for bandit-facing combat only | the reference sim has **no single 30 HP bar** — health is per-part (torso 40 etc.) | LOW | This `maxHp` is a parallel legacy combat pool; the per-part system (PawnHealth) is the real one. Not worth changing for a prototype; note the dual system to operator. |
| **Pawn move speed** | `moveSpeed = 4.6` units/sec (`PawnStats.cs:31`) ✓ FIXED (#200, verified #28) | Human base **4.6 c/s** ([Move Speed]) | ~~MED~~ **CLOSED** | Value matches the reference sim canonical. V8MoveSpeedTest.cs asserts PASS. |
| **Melee base dmg** | bandit `attackDamage = 1` (`PawnStats.cs:24`); drafted melee `2 + weapon` vs bandit, `3` vs wolf (`PawnUtilityAI.cs:250,274`) | Fist ~8.2 dmg; weapons 9–25 ([Weapons]) | LOW | Numbers are internally consistent (small HP pools), so combat *feels* right relative to enemy HP. Not the reference sim-scaled but coherent — leave unless rescaling all combat. |
| **Bow / arrow damage** | arrow `damage = 4` default (`ArrowProjectile.cs:16`); drafted ranged 4/5/3 by target (`PawnUtilityAI.cs:210-212`); tech desc claims "12 dmg" (`ResearchManager.cs:67`) | Short bow **18 damage**, 12-tile range, ~moderate accuracy ([Short bow]) | **MED** | Two issues: (a) actual arrow dmg (4) is far below 18 *and* (b) the research description says "12 dmg 화살" — **description lies about the code**. Either bump arrow dmg toward ~12–18 or fix the desc string. Internal-consistency bug worth fixing cheaply. |
| **Bow range** | `RangedAttackRange = 5.0` (`PawnUtilityAI.cs:39`) | Short bow range **12 tiles** ([Short bow]) | **MED** | If 1 unit = 1 tile, bow reaches <½ its the reference sim range. Bump to ~`9–12`. |
| **Bow cooldown** | `RangedAttackInterval = 1.5`s (`PawnUtilityAI.cs:40`) | Short bow warmup ~1.5s + cooldown ~1.8s ≈ **~3.3s/shot** ([Short bow]) | LOW | Prototype fires ~2× faster; acceptable, or raise to ~2.5–3s for fidelity. |
| **Research bench rate** | `pointsPerSecondPerBench = 2`/sec × researcher speed sum (`ResearchManager.cs:43`) | 0.00825 pts/tick × 60 = **~0.5 pts/sec** at baseline; Intellectual +11.5%/level ([Research Speed]) | LOW | Prototype is ~4× faster, but tech costs are also tiny (100–300 vs the reference sim 100–4000), so *time-to-complete* feels fine. Leave; it's a coherent prototype scaling. |
| **Research tech costs** | 100 / 120 / 150 / 250 / 300 pts (`ResearchManager.cs:67-73`) | Real projects 100–4000 ([Research]); early ones ~100–500 | LOW | Intentional prototype compression of the tech tree. Not a bug. |
| **Rice/crop grow time** | `growthPerSecond = 0.011` → ripen in ~90s = **~0.38 in-game day** (`CropEntity.cs:22`) | Rice **3 base days (5.54 real-grow days)** to mature ([Rice plant]) | LOW | Deliberately accelerated for session pacing (comment cites the tradeoff). ~8–14× faster than the reference sim but a justified prototype choice. Leave. |
| **Crop yield** | `harvestFood = 8` per harvest (`CropEntity.cs:24`) | Rice yields **6 units/plant** ([Rice plant]) | LOW | Close enough (8 vs 6); fine. |
| **Wolf HP** | `maxHp = 18` (`WolfEnemy.cs:12`) | Timber wolf body size ~0.85, **~50 HP-equivalent** health pool | **MED** | Wolf dies in ~4 arrows; the reference sim wolves are tankier. But scaled to the prototype's small dmg numbers it's coherent. Bump to ~30 if wolves feel like paper; else low. |
| **Wolf bite dmg** | `attackDamage = 4`, interval 1.2s (`WolfEnemy.cs:13,15`) | Bite **12 dmg, 2s cooldown**, 18% AP ([Timber wolf]) | LOW | Scaled down with everything else; coherent. |
| **Wolf move speed** | chase `2.5`, wander `0.8` (`WolfEnemy.cs:17-18`) | Timber wolf move **~4.6 c/s** ([Timber wolf]) | LOW | Slower than the reference sim; if pawn speed is bumped to 4.6, wolves should also rise (~3.5–4.6) or they can never catch a pawn. **Tie to the pawn-move-speed fix.** |
| **Wolf detection** | `detectionRadius = 5.0` (`WolfEnemy.cs:16`) | Predators hunt across the map; manhunter packs target on sight | LOW | Fine for a prototype. |
| **Deer/animal stats** | deer HP 12 / drop 5; boar 25/8; chicken 4/2; rabbit 3/1 (`AnimalEntity.cs:36-41`) | Deer ~50 HP, much larger meat yields; chicken/rabbit small | LOW | All scaled down together; internally coherent relative to hunter dmg (2/hit). Leave. |
| **Storyteller threat tiers** | Steady day 3/7/14, Calm 6/14/25, raids every 3 days at 06:00 (`AIDirector.cs:59-76,116-119`) | Storytellers use **threat points** scaling with wealth+time, not fixed day gates ([no clean wiki value; knowledge]) | LOW | Day-gated tiers are a sound prototype abstraction of the points system. Not a wrong "value." |
| **Carry capacity** | `carryCapacity = 75kg` (`PawnAbilities.cs:26`) | Human carrying capacity **75 kg** ([Stats]) | — | **Correct.** No change. |
| **Door pass slowdown** | `PassMul = 0.65` (referenced `PawnMovement.cs:482`) | Doors add a pass-through delay (~0.45s open) ([knowledge, unverified]) | LOW | Reasonable. |

---

## 2. Top-5 fixes (impact × low-effort), ranked

These are the cheapest single-constant edits with the biggest fidelity payoff. Each is a
one-line value change (no logic rework), so all fit the operator's "기능추가 보수적" rule.

1. **Food decay — `PawnNeeds.cs:18`  `foodDecay` 0.5 → ~0.14** (≈2.0 fix). Pawns currently
   starve in ~0.83 in-game days; the reference sim is ~3 days. This is the single most game-feel-breaking
   value (HIGH). Use `0.14` for ~3-day fidelity, or `0.21` for a safer ~2-day budget.

2. **Pawn move speed — `PawnStats.cs:31`  `moveSpeed` ~~3.0 → ~4.6~~.** DONE (#200). Value is
   now 4.6, matching the reference sim human base. Confirmed by V8MoveSpeedTest.cs (#28 verify gate).
   (Wolf chaseSpeed 2.5 in `WolfEnemy.cs:17` still lower than the reference sim's ~4.6 — tied note below
   still applies; wolf speed is a separate future fix.)

3. **Head body-part HP — `HealthPartsConfig.cs:27`  head `maxHp` 10 → 25** (and torso 30 → 40,
   `:28`). Head at 10 HP makes decapitation/head-death far too easy vs the reference sim's ~25. One-line
   per part; big survivability-feel fix.

4. **Bow/arrow damage consistency — `ResearchManager.cs:67` description vs `ArrowProjectile.cs:16`.**
   The "원시 활" tech advertises "12 dmg 화살" but arrows deal 4. Either raise arrow `damage`
   toward ~12 (closer to the reference sim's 18) **or** correct the description string. This is a
   truth-in-UI bug, not just tuning.

5. **Skill XP base — `PawnSkills.cs:66`  `100f * L^1.5` → `1000f * ...`** (or flatten toward
   the reference sim's flat ~1000/level). Early levels currently cost 100/283/520 XP — roughly 10× too
   cheap, so pawns blow through low skill levels in seconds. Raising the base restores
   meaningful early progression.

---

## 3. Scope notes — MISSING vs PRESENT-but-wrong

**PRESENT-but-wrong (audit targets — tuning is fair game):** food/sleep/mood decay & regen
rates, mood-break threshold count, skill XP curve, work-speed-per-level, body-part HP values,
pawn & wolf move speed, bow damage/range/cooldown, arrow-vs-description mismatch. These are the
rows above with MED/HIGH severity and concrete from→to fixes.

**MISSING entirely (NOT in scope to add — operator said "기능추가 보수적"):**
- **Temperature system** — no body-temp, no comfortable-range, no hypothermia/heatstroke.
  `WeatherController` exists but only drives a Storm mood penalty (`PawnNeeds.cs:90-96`); there is
  no temperature stat at all. (Do not add.)
- **Recreation/Joy need** — the reference sim's 4th core need is absent; prototype has only Food/Sleep/Mood
  (`NeedType` enum, `PawnNeeds.cs:217`). Mood partly stands in. (Do not add.)
- **Pain / consciousness / blood-loss needs** — downing keys off head-% instead of a pain or
  blood-loss meter (`PawnHealth.cs:212`). (Do not add; the per-part HP *values* are the in-scope fix.)
- **Disease/infection progression, hediffs** — `minor_disease`/`food_blight` are flavor-only
  events (`AIDirector.cs:357,377`); no actual hediff mechanics. (Do not add.)
- **Shooting accuracy by range/cover** — arrows perturb by a flat accuracy roll
  (`ArrowProjectile.cs:24-38`); no per-range accuracy table or cover. (Do not add.)
- **Separate work types** (Hauling/Construction/Mining/Medical are folded into the `Chop`/`Research`
  slots, see `PawnActions.cs:181,220,263,298,369`). Intentional consolidation; not a value bug.

---

## 4. Uncertain the reference sim values (operator should decide)

The wiki blocks direct fetch; these rest partly on training knowledge and should be
double-checked against the live wiki before committing a change:

- **Body-part HP exact figures** — torso 40 is wiki-confirmed ([Table of Human Body Parts]);
  head ~25, arms/legs ~30 are [knowledge, unverified]. Confirm head and limb numbers on the
  Table-of-Human-Body-Parts page before editing `HealthPartsConfig.cs`.
- **Timber wolf health pool** — bite (12 dmg / 2s) and ~4.6 c/s confirmed ([Timber wolf]); the
  ~50-HP figure is derived from body-size and is [knowledge, unverified]. Confirm before bumping
  `WolfEnemy.maxHp`.
- **Short bow exact cooldown/warmup** — 18 dmg and 12-tile range confirmed ([Short bow]); the
  ~1.8s cooldown + ~1.5s warmup split is [knowledge, unverified].
- **Mood baseline** — the reference sim mood is a thought-sum with no single "decay rate," so any
  `moodDecay` value is an approximation; there is no canonical number to match. The fix is
  directional (lower it), not a precise target.
- **Sleep fall rate in absolute units** — the wiki gives multipliers and "10.5 h to full rest,"
  not a flat units/day; the suggested `0.20`/`1.0` values are derived from the 240s-day mapping,
  not lifted verbatim.

---

## Sources

- Move Speed — human base 4.6 c/s
- Mental Break Threshold — minor 35% / major 20% / extreme 5%
- Skills — ~1000 XP/level, 4000/day soft cap, +15%/lvl construction/mining
- Saturation / Needs — hunger 1.6 nutrition/day; work thresholds rec 35% / eat 30% / rest 30%
- Rest / Sleep Fall Rate — 10.5 h to full rest
- Mood — mood is a sum of thought offsets
- Short bow — 18 dmg, 12-tile range
- Research Speed — 0.00825 pts/tick base, +11.5%/Intellectual lvl
- Research — project costs 100–4000
- Rice plant — 3 base / 5.54 grow days, yield 6
- Timber wolf — bite 12 dmg / 2s, 18% AP
- Table of Human Body Parts / Body Parts — torso 40 HP
- Weapons — fist ~8.2, melee 9–25
- [Stats](https://reference-sim.fandom.com/wiki/Stats) — carrying capacity 75 kg
- Health — downing from pain/blood-loss/consciousness
