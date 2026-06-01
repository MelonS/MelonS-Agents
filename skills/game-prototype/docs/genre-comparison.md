# PawnSim vs the reference sim — Comprehensive Comparative Gap Analysis

Date: 2026-05-30. Author: research agent (read-only on `skills/game-prototype/unity-project/`).
Scope: the operator's **6 gameplay dimensions** (디자인/비주얼 · 사운드 · 림 이동 · 건축 · 게임플레이 루프 · 메뉴 UX/UI), as a gap matrix feeding an autonomous improvement backlog.

This report is **complementary** to `docs/audit-genre-fidelity-2026-05-29.md`. That audit owns the **numeric/balance** layer (decay rates, HP, dmg, XP curve, move-speed value 3.0→4.6). This report deliberately does **not** re-derive those constants; it covers the **structural/experiential** gaps the balance audit explicitly left out (art, sound coverage, movement feel, build-flow completeness, loop verification, UI conventions). Where they touch (e.g. move-speed 4.6), this report references the audit instead of restating.

Verification status note: the move-speed value fix (3.0→4.6) is recorded as landed in MILESTONES `#200`, but the audit file still reads 3.0 — there is a goal-vs-log reconciliation gap (see M5).

> **Source caveat:** 장르 위키 blocks direct fetch (403). Canonical values below come from WebSearch snippets of the wiki + the colony-sim art guide, supplemented by the reference sim domain knowledge. Items resting on knowledge the search didn't confirm are tagged **[knowledge, unverified]**. Source URLs are listed at the end.

---

## Executive summary — closeness per dimension

| # | Dimension | Closeness to vanilla | One-line verdict |
|---|-----------|:---:|------------------|
| 1 | 디자인 / 비주얼 | **~70%** | Coherent flat-Kenney + outline language is solid; the gap is now **motion/life** (frozen world) and directional/animated pawns, not style. |
| 2 | 사운드 | **~30%** | 6 SFX with good throttle discipline, but **no UI/build/mine/cook/combat-melee/ambient/weather/alert sounds and no dynamic music**. Quietest dimension. |
| 3 | 림 이동 / 움직임 | **~75%** | A* full-grid cutover is genuinely good (octile, no corner-cut, reservations, eject). Gaps: **no pawn facing/rotation**, **no terrain move-cost variety** (only floor bonus + door slow), no smoothing/crowding nuance. |
| 4 | 건축 | **~65%** | blueprint→haul→frame→build + stockpile + multi-cell + red-ghost validation is real and correct. Gaps: **no deconstruct, no roofs, no zone-paint (floors/mine/grow placed per-click only), no quality on build, limited build catalogue**. |
| 5 | 기본 게임플레이 루프 | **~80%** | All core loops present (food/sleep/mood/threat/grow/build/research/defend). Gap is **depth + honest end-to-end verification** (V1–V9), plus mood is a free-fall timer not a thought-sum (logic, OP-gated). |
| 6 | 메뉴 UX / UI 위치 | **~70%** | Restyled bordered-panel system is cohesive; hotkeys + architect categories match. Gap is **panel POSITIONS** — inspector is bottom-**LEFT** (the reference sim = bottom-left info is correct-ish, but the *selected-thing inspector* sits bottom-left while **alerts/letters top-right are entirely missing**), no alert stack, no work/schedule polish. |

### Top-3 overall gaps (by player-felt impact)
1. **Sound coverage (Dim 2).** The game is ~70% silent during play. No build/mine/cook/melee/UI-click/ambient/weather/alert sounds and no dynamic music. This is the single biggest "feels unfinished" lever and most of it is cheap (wire existing AudioBank slots + add ~6 clips). Highest impact-per-effort in the whole backlog.
2. **Frozen world / no pawn motion & facing (Dim 1 + 3).** Pawns stand rigid, never face their movement direction, never lie down to sleep. the reference sim implies motion in every still. Walk-bob (V1) is queued but unshipped; facing is not even queued. A still screenshot still reads "diorama."
3. **Missing alert / letter stack (Dim 6).** the reference sim's top-right letter stack ("Raid!", "Mental break") is how the player learns what's happening. PawnSim fires events only into a scrolling EventLog with no persistent, clickable, jump-to-source alert — so threats are easy to miss. This is a loop-legibility gap as much as a UI gap.

---

## Dimension 1 — 디자인 / 비주얼

| Aspect | the reference sim canonical | PawnSim current | Gap | Sev | Fix-items |
|---|---|---|---|:--:|---|
| Art language | Flat, low-noise, 2–3px black border on story-relevant things; 64×64 textures; tight muted palette ([Artstyle guide]) | Single `palette.py`; flat colonist + 2px outline (3 cloth variants); muted terrain; unified wood ramp; drop shadows (#202–208) | **Style war already resolved** — language matches vanilla intent. Minor: textures are 16/32px not 64. | L | None urgent. Optionally up-res key sprites to 32px for crispness at zoom. |
| Pawn rendering | Pawn **faces 4 directions** (N/S/E/W body+head), head bobs, **lies down** to sleep; subtle sway ([Artstyle]; [knowledge]) | Single static sprite, no flipX/rotation, no sleep pose; walk-bob queued (V1) but **unshipped** | **HIGH** — no facing + no walk motion = the "frozen diorama" complaint #1 | **H** | (a) Ship V1 walk-bob (sprite-child offset only). (b) Add E/W `flipX` toward movement dir (cheapest facing). (c) Sleep = lay sprite flat / dim. |
| Environmental richness | Maps quietly busy: scattered rocks, plants, filth, chunks everywhere | V2 scatter (rocks/tufts/flowers) shipped per backlog; still sparse | M — better than before; density/variety can grow | M | More scatter variety (chunks, dead leaves); cluster scatter near trees/rock. |
| Animation / motion | Fire flicker, weather particles (rain/snow), gentle plant sway; otherwise minimal ([knowledge]) | NightOverlay + DayNight color stops + WeatherController (storm darkening only) + FlickerLight present; tree sway/fire-pulse (V5) queued, weather has **no particles** | **M** — no rain/snow particles; fire flicker partial | M | Add rain/snow particle layer tied to WeatherController; ship V5 fire flicker + tree sway. |
| Day-night | Continuous tint + light glow at night | DayNightCycle 11 color stops + NightOverlay alpha curve + weather mul — **genuinely good** | L — close to vanilla | L | Optional: warm light pools around lit stove/fire at night. |
| Grounding | Objects sit *in* world (shadow/AO) | Contact shadows (V3) shipped per backlog | L | L | Done. |

**Net:** Dim 1's *style* gap is closed; the remaining gap is **motion + facing**, which overlaps Dim 3. The highest-value art-side item is pawn **facing** (not even on the backlog yet).

---

## Dimension 2 — 사운드

the reference sim sound model ([Modding Tutorials/Sounds]; [Category:Sounds]): per-action SFX for nearly every interaction (chop, mine, construct, cook, shoot, melee-hit, door, UI clicks), continuous **ambient beds** (wind, birds, indoor room-tone), **weather** sound (rain/thunder), an **alert siren** system (low danger = 2 sirens, high danger = 4), and **dynamic music** that swaps to tension/combat tracks during threats.

| Action / layer | the reference sim | PawnSim current | Gap | Sev | Fix-items |
|---|---|---|---|:--:|---|
| Chop | woody axe thunk | `PlayChop()` (0.25s throttle) ✔ | none | — | — |
| Harvest | plant snap | `PlayHarvest()` ✔ | none | — | — |
| Mine | pick-on-stone | uses `PlayChop()` via StoneVein (semantically wrong) | **M** | M | Add `sfxMine` slot + `mine.wav`; wire StoneVeinEntity. |
| Construct/build | hammer/clink loop | **none** | **H** | H | Add `sfxBuild`; call from `PawnBuilder`/`BlueprintEntity` complete. |
| Cook | sizzle | **none** | **M** | M | Add `sfxCook`; call from `StoveEntity`/`PawnCook`. |
| Shoot (bow) | bow twang / arrow whoosh | **none** (arrow silent) | **M** | M | Add `sfxShoot`; call from `ArrowProjectile` spawn. |
| Melee hit | thud | `PlayHit()` exists; AnimalEntity wrongly calls `PlayChop()` | **M** | M | Fix AnimalEntity:149 `PlayChop`→`PlayHit`; ensure bandit/wolf combat calls it. |
| UI click | soft blip | `PlaySelect()` on pawn select only; **buttons/architect/research silent** | **M** | M | Route GuiControlBar/Architect/Research buttons through `PlaySelect()`. |
| Ambient bed | wind + birds room-tone | only `bgm_ambient` music loop | **H** | H | Add looping ambient layer (outdoor wind/birds) independent of music. |
| Weather sound | rain/thunder loop | **none** (storm is visual+mood only) | **M** | M | Add rain loop tied to WeatherController.Storm. |
| Alert siren | 2 sirens low / 4 high danger | **none** | **H** | H | Add `sfxAlert`; fire on raid/wolf_pack/mental-break with tier-scaled repeats. |
| Wolf howl | eerie howl | `PlayWolfHowl()` exists but **0 callers** | **M** | M | Wire to AIDirector `wolf_pack` event + WolfEnemy spawn. |
| Dynamic music | calm ↔ tension/combat swap | single static bgm loop | **H** | H | Add a 2nd "danger" track; crossfade when `CurrentThreatTier`≥2 or enemy on map. |

**Net:** the *infrastructure* is good (throttle discipline, graceful no-op, named slots). The *coverage* is ~30%. Roughly 6 new clips + ~10 wiring calls would lift this to ~80%. **Highest impact-per-effort dimension.**

---

## Dimension 3 — 림 이동 / 움직임

| Aspect | the reference sim | PawnSim current | Gap | Sev | Fix-items |
|---|---|---|---|:--:|---|
| Grid + A* | 8-dir grid, octile, no corner-cut | PathGrid + AStar (8-dir octile, no corner-cut, cap 4000), reservations, eject-on-block, adjacent-stand-cell — **strong** (#199) | none | — | Keep. |
| Base speed | 4.6 c/s human | `moveSpeed` 4.6 per MILESTONES #200 (audit file still says 3.0 — reconcile) | L (value) | L | Confirm 4.6 actually in `PawnStats`; tick the audit. |
| Terrain move-cost | path cost per terrain; soil=2 (½ speed), gravel, marsh, mud, snow; speed-cap formula 13/(13+pathcost) ([Move Speed]) | **only** FloorEntity speed *bonus* + DoorEntity slow; **no per-terrain cost** (grass/dirt/rock-edge all same) | **M** | M | Add per-terrain path-cost on PathGrid cells (dirt/marsh slower); apply as speed mul in movement. |
| **Facing / rotation** | pawn rotates to face N/S/E/W of travel/work | **none** — no flipX, no rotation toward target | **H** | **H** | Add E/W `flipX` (cheap) or 4-dir sprite swap toward `_path` next waypoint. (Shared with Dim 1.) |
| Diagonal | allowed, octile cost √2 | yes (octile) ✔ | none | — | — |
| Pass-through | doors slow; pawns can swap | DoorEntity `PassMul` 0.65 ✔; no pawn-swap/crowd push | L–M | L | Optional: minor crowd-avoid when two pawns share a cell target. |
| Smoothing | glide between cells | `MoveTowards` cell-to-cell with arriveDistance; functional, slightly robotic | L | L | Optional path-corner smoothing; low priority. |
| Crowding | pawns yield/repath around each other | reservations prevent double-occupy work cells; no live mid-path avoidance | M | M | Out of scope ("게임이 되는게 먼저"); reservations are sufficient. |

**Net:** pathing core is the project's strongest system. The glaring experiential gap is **no facing** — a pawn moonwalking to its target reads as broken even with perfect pathing. Terrain-cost variety is the secondary gap.

---

## Dimension 4 — 건축

| Aspect | the reference sim | PawnSim current | Gap | Sev | Fix-items |
|---|---|---|---|:--:|---|
| Build flow | blueprint → haul materials → frame → construct | blueprint (no upfront cost) → hauler carries → PawnBuilder builds; multi-cell footprint; secs by type — **correct end-to-end** | none | — | Keep; verify in a V-scenario. |
| Placement validation | red ghost on water/occupied/no-roof-support | red ghost on terrain(water/rock)+occupied, per-cell footprint, toast reasons — **good** | none | — | Keep. |
| Build catalogue | walls, doors, floors, furniture, production, power, security, art, etc. | wall(wood/stone), floor, door, stove, bed×3 quality, research bench (pre-placed) | **M** — small catalogue; no table/chair/torch/sandbag/cooler | M | Add a few high-value items: torch/standing-lamp (light), table+chair (eat/rec), sandbag/barricade (defense). Each independently shippable. |
| **Deconstruct** | designate → pawn removes, refunds ~50% | **none** — cannot remove a built wall | **H** | **H** | Add a Deconstruct designation mode + PawnBuilder deconstruct action; refund 50% material; clears PathGrid cell. |
| **Roofs** | auto-roof under enclosed walls; build/remove roof zones; collapse on support loss | **none** — no roof concept at all | **M** (vanilla-defining but heavy) | M | OP-gated. A *visual* "indoor" overlay when enclosed is a cheap stand-in; full roof+collapse is M-heavy, defer. |
| **Zone painting** | drag-rect to paint floors / mining / growing / stockpile zones | floor & build are **per-click single cell**; stockpile is a zone entity; **no drag-paint, no mining designation, no grow-zone designation** | **H** | **H** | Add drag-rect designation for: floor build, **mine** (designate rock/ore → PawnMiner), **grow zone** (designate → crops auto-plant). Biggest "feels like the reference sim" build-UX win. |
| Quality on build | constructed items roll Awful→Legendary by Construction skill | bed has 3 *fixed* tiers (sleeping-spot/wood/fine) chosen at build; **no rolled quality** | M | M | Optional: roll a quality tier on completion from builder's skill; affects mood/value. OP-gated (logic). |
| Floors | many floor types, speed/beauty | one wood floor (speed bonus) | L | L | Add stone/paved floor variant (faster, from stone). |
| Designation UX | drag-select, copy, cancel-area, mirror | single-click place + ESC/right-click cancel; no drag, no cancel-area | **M** | M | Pair with zone-painting: drag-place walls/floors in a line/rect; area-cancel of blueprints. |

**Net:** the *plumbing* (blueprint→haul→build) is correct and verified-ish. The gaps are **operations the player expects**: deconstruct, drag-paint zones, mining/grow designation. These are the difference between "I placed one wall" and "I designed a base."

---

## Dimension 5 — 기본 게임플레이 루프

| Loop piece | the reference sim | PawnSim current | Gap | Sev | Fix / VERIFY |
|---|---|---|---|:--:|---|
| Food | grow→cook→eat; hunger over ~2–3 days | crops grow+harvest, stove cooks, eat-from-stockpile w/ meal mood; hunger 0.14/s (~3 days) ✔ | depth: no bills/recipes queue | L | **V-VERIFY: crop plant→ripen→harvest→cook→eat→food restored** end-to-end. |
| Sleep | schedule + bed quality | sleep<30 & night → sleep in bed, quality rest/mood mul ✔ | sleep-decay/regen tuning (audit MED) | L | **VERIFY: pawn seeks bed at night, rest rises.** |
| Mood | thought-SUM around baseline, 3 break tiers (35/20/5) | mood **free-falls** to 0 via `moodDecay`; single break threshold 20 | **M (logic)** | M | **OP-gated**: convert to thought-sum + 3 tiers. Audit flagged; held. |
| Threats | points-scaled raids, predators, events | 3 storytellers, day-gated tiers, raids every 3d@06:00, wolf, 15 events | acceptable abstraction | L | **VERIFY: raid spawns day 3, bandits path to colony.** |
| Defend | draft, ranged/melee, cover | draft (R), bow (research-gated), melee, wolf/bandit combat, body-parts dmg/bleed/downed | no cover system | M | **VERIFY: drafted pawn shoots arrow, hits, enemy HP drops, death.** |
| Research | bench, tech tree, intellectual skill | bench + 5-tech tier tree + auto-first + manipulation-sum rate | compressed (intentional) | L | **VERIFY: research accrues, tech unlocks (bow), bow becomes buildable/usable.** |
| Economy/haul | stockpiles, priorities, carry cap | 5-tier stockpile priority, hauler; carryCapacity 75kg defined but **not wired to hauler cap** | M | M | **VERIFY: hauler moves wood to blueprint; carry-cap fix queued (M2).** |
| Persistence | full save/load | save/load present; BedQuality/StockpilePriority/TreeSpecies/WallMaterial **not serialized** | M | M | **VERIFY: save→load round-trips entity sub-state.** |
| Joy/Recreation | 4th core need | **absent** (audit: do-not-add) | — | — | Out of scope per operator. |
| Temperature | hypothermia/heatstroke | **absent** (audit: do-not-add) | — | — | Out of scope. |

**V1–V9 verification slate (promote "code exists"→"binary PASS"):** V1 drafted-tint, V2 wolf-detection→chase, V3 research-progress→unlock, V4 arrow-hit→dmg, V5 crop plant→harvest→cook→eat, V6 body-part dmg+bleed+downed, V7 storyteller tier@day7 + raid@day3, V8 map-obstacle stop / pathfind-around, V9 mood-break triggers below threshold. Each = one PASS line in the gate.

**Net:** the loop is **present and ~80% there**. The gap is **trust** (honest end-to-end V-tests) + two held logic items (mood model). Per "게임이 되는게 먼저," verification > new systems here.

---

## Dimension 6 — 메뉴 UX / UI 위치

the reference sim conventions ([User interface]): **Architect** button bottom-left → opens category column → buildables; **selected-thing inspector** bottom-left; **gizmos** (Draft/actions) bottom-center; **resources** read-out + **date/speed** top; **alerts/letters** stack **top-right** (clickable, jump-to-source); **work/schedule/research** as top tabs; rich hotkeys.

| Element | the reference sim position/behavior | PawnSim current | Gap | Sev | Fix-items |
|---|---|---|---|:--:|---|
| Architect menu | bottom-left button → category column | ArchitectMenu left-mid panel, collapsible categories (F8), bordered — **good match** | L | L | Optionally anchor to bottom-left to mirror exactly. |
| Build/speed/draft controls | gizmos bottom-center | GuiControlBar bottom-center, grouped w/ dividers, active highlight — **good** | L | L | Add per-button SFX (Dim 2). |
| Selected inspector | bottom-left | PawnInfoPanel anchored **bottom-LEFT** (380×200) | **near-match** but cramped + overlaps potential | L–M | Expand to tabbed (상태/건강/기분/장비); already styled. |
| Resources read-out | top | TopBar top, full-width, icons + clock + speed — **good** | L | L | — |
| **Alerts / letters** | **top-right stack, persistent, clickable** | **MISSING** — events go only to a scrolling EventLog; no persistent alert, no jump-to-source | **H** | **H** | Add top-right alert stack: raid/low-food/mental-break/wolf as clickable cards that pan camera to source. Loop-legibility critical. |
| Work tab | top tab, priority grid | WorkTabUI (F1) exists | M | M | VERIFY priorities actually change pawn behavior (OP-gated logic). |
| Schedule tab | per-hour assignment grid | ScheduleUI (F4) exists | L | L | VERIFY schedule drives sleep/work. |
| Research tab | tech tree screen | ResearchUI strip + N picker | M | M | A tree *view* (deps as graph) vs strip; low priority. |
| Hotkeys | extensive (B/G/H/Space/1-3/R...) | Space pause, 1/2/3 speed, R draft, B/F/G/T/Y build, N research, F1/F4/F8 tabs — **good coverage** | L | L | Add ESC=cancel everywhere consistently; document in-game. |
| Tooltips / info hierarchy | hover everything, nested info | HoverTooltip + EntityInspectorPanel + bordered panels | L | L | — |

**Net:** the panel *system* and *hotkeys* are cohesive and conventional. The one **HIGH** gap is the **missing top-right alert/letter stack** — without it the player can't reliably perceive threats, which undercuts the whole loop (overlaps Dim 5).

---

## Consolidated PRIORITIZED BACKLOG

Tags: **code / art / sound / verify**. **[OP-OK]** = behavior-logic change, needs operator sign-off before landing (per §5 / "기능추가 보수적"). Impact-per-effort: ★★★ (do first) → ★. Each item is independently shippable with a **binary** acceptance.

| # | Milestone | Item | Tag | Imp/Eff | [OP-OK] | Binary acceptance |
|---|---|---|---|:--:|:--:|---|
| 1 | M2 sound | Add `sfxBuild` + wire PawnBuilder/Blueprint-complete | sound+code | ★★★ | | A wall finishing plays a construct sound once (throttled). |
| 2 | M2 sound | Add `sfxAlert` + fire on raid/wolf_pack/mental-break (tier-scaled repeats) | sound+code | ★★★ | | A raid event plays an alert siren; tier-3 repeats more than tier-1. |
| 3 | M2 sound | Wire `PlayWolfHowl()` to AIDirector wolf_pack + WolfEnemy spawn | sound+code | ★★★ | | A wolf spawn plays the existing howl clip (currently 0 callers). |
| 4 | M2 sound | Looping outdoor ambient bed (wind/birds), independent of music | sound+code | ★★★ | | With nothing happening, a continuous ambient layer is audible (not the music). |
| 5 | M2 sound | Route GuiControlBar/Architect/Research buttons through `PlaySelect()` | sound+code | ★★ | | Clicking any UI button plays the blip. |
| 6 | M2 sound | Fix AnimalEntity:149 `PlayChop`→`PlayHit` | sound+code | ★★ | | Hitting an animal plays the combat thud, not the chop. |
| 7 | M2 sound | Add `sfxMine` + `sfxCook` + `sfxShoot`; wire StoneVein/Stove/Arrow | sound+code | ★★ | | Mining, cooking, and firing an arrow each play a distinct sound. |
| 8 | M2 sound | Dynamic music: add danger track, crossfade when threat≥2 or enemy on map | sound+code | ★★ | | Music swaps to the tension track during a raid and back when clear. |
| 9 | M2 sound | Rain loop tied to WeatherController.Storm | sound+code | ★ | | A storm plays a rain loop; clear weather is silent of rain. |
| 10 | M1 design | Ship V1 walk-bob (sprite-child offset ONLY) | code | ★★★ | | Two frames 0.1s apart show a walking pawn at different vertical offset; name-plate/bars do NOT bob; no pawn drifts off its cell over 10s. |
| 11 | M3 movement | Pawn **facing**: flipX (E/W) toward `_path` next waypoint | art+code | ★★★ | | A pawn walking left faces left, walking right faces right. |
| 12 | M3 movement | Sleep pose: lay/dim sprite when `IsSleeping` | art+code | ★★ | | A sleeping pawn is visually distinct (rotated/flat/dimmed) from a standing one. |
| 13 | M1 design | Ship V5 fire-flicker + tree-sway (sprite-child) | code | ★★ | | A lit stove's flame pixels differ between two frames 0.25s apart. |
| 14 | M1 design | Weather particles: rain/snow layer tied to WeatherController | art+code | ★ | | During storm a rain particle layer is visible; clear weather has none. |
| 15 | M4 building | **Deconstruct** designation + PawnBuilder remove + 50% refund + clear PathGrid cell | code | ★★★ | | Designating a built wall → a pawn removes it, refunds material, the cell becomes walkable. |
| 16 | M4 building | **Mine designation** drag-rect → PawnMiner targets rock/ore | code | ★★★ | | Drag-selecting rock cells → a miner walks over and mines them, yielding stone. |
| 17 | M4 building | **Grow-zone designation** drag-rect → crops auto-plant | code | ★★ | | Painting a grow zone on dirt → crops appear and grow there. |
| 18 | M4 building | Drag-rect placement for walls/floors (line/area blueprints) | code | ★★ | | Click-drag places a row of wall blueprints, not one cell. |
| 19 | M4 building | Add torch/standing-lamp buildable (night light pool) | art+code | ★★ | | A built lamp emits a light glow at night. |
| 20 | M4 building | Add table+chair buildable (eat/rec spot) | art+code | ★ | | Table+chair can be built and a pawn uses it to eat. |
| 21 | M4 building | Stone/paved floor variant (from stone, faster) | art+code | ★ | | A stone floor is buildable and gives a higher move bonus than wood. |
| 22 | M6 UI | **Top-right alert/letter stack** (raid/low-food/break/wolf), clickable→pan camera | code | ★★★ | | A raid creates a persistent top-right card; clicking it pans the camera to the bandits. |
| 23 | M6 UI | PawnInfoPanel → tabbed (상태/건강/기분/장비) | code | ★ | | Inspector shows tabs; switching tabs changes content. |
| 24 | M3 movement | Per-terrain path-cost (dirt/marsh slower) applied as speed mul | code | ★ | [OP-OK] | A pawn crosses dirt measurably slower than a wood floor. |
| 25 | M5 verify | V5 loop chain: plant→ripen→harvest→cook→eat→food↑ as one PASS | verify | ★★★ | | A single gated test asserts the full food chain raises a pawn's food need. |
| 26 | M5 verify | V4 combat chain: draft→shoot→hit→dmg→death as one PASS | verify | ★★★ | | A gated test asserts a drafted arrow reduces enemy HP to death. |
| 27 | M5 verify | V7 threat: raid@day3 + bandit paths to colony as one PASS | verify | ★★ | | A gated test asserts a bandit spawns day 3 and moves toward the colony. |
| 28 | M5 verify | Reconcile move-speed 4.6 (confirm in PawnStats; tick audit/goal) | verify | ★★ | | `PawnStats.moveSpeed` reads 4.6 and the audit/goal checkbox is updated. |
| 29 | M5 verify | Save→load round-trips BedQuality/StockpilePriority/TreeSpecies/WallMaterial | verify+code | ★★ | | After save→load, a fine bed is still fine (not reverted to default). |
| 30 | M3 gameplay | Mood = thought-sum around baseline + 3 break tiers (35/20/5) | code | ★ | [OP-OK] | Mood settles at a baseline from summed thoughts instead of free-falling to 0; minor/major/extreme breaks fire at 35/20/5. |

---

## What NOT to do (over-scoping guardrails)

Per operator "**기능추가 보수적 — 게임이 되는게 먼저**", explicitly do **not** start these until the core *plays well + is verified*:

- **Full roof + collapse system** (Dim 4). Vanilla-defining but heavy (support graph, collapse damage). A cheap "indoor" overlay when walls enclose a space is acceptable; the real system is M-heavy — defer. **[OP-OK]**
- **Temperature / hypothermia / heatstroke** — audit said do-not-add. Stays out.
- **Joy/Recreation need** (4th core need) — audit do-not-add. Mood stands in.
- **Disease/infection hediff mechanics, pain/consciousness/blood-loss needs** — flavor events only; do not build real hediffs.
- **Cover/accuracy-by-range combat tables** — current flat-roll combat is coherent at the prototype's small HP scale; rescaling all combat is a separate large task.
- **Trading UI / power grid / animal taming / bills queue** (M4 breadth) — all **[OP-OK] gated**, sequenced last. Do not start without operator direction.
- **Detailed 32×32 pawn redraw (goal P5)** — likely *superseded* by the #202 flat-outline direction; the "potato in a pot" problem was caused by detail. Confirm with operator before redrawing; do **not** add detail back.
- **PawnSkills 14-type expansion** — current 4-skill consolidation is an intentional, coherent simplification; expand only if a specific loop needs it.

**Sequencing recommendation for the autonomous chain:** Sound (1–9) and walk-bob/facing (10–13) are the cheapest, highest-felt wins and have **no operator gate** — run them first. Then build-ops (15–18, the "design a base" feel) and the alert stack (22). Verification (25–29) runs continuously as the gate. Logic-gated items (24, 30) and breadth (M4 trading/power) wait for explicit OP-OK.

---

## Sources

Numeric/balance values: see `docs/audit-genre-fidelity-2026-05-29.md` (move speed 4.6, hunger, HP, XP, bow, wolf — not restated here).

- User interface — architect bottom, gizmos, selected-thing info, letter stack
- Modding Tutorials/Sounds — sound def model, parameters
- Category:Sounds — sound coverage categories
- alert siren tiers (2 low / 4 high danger): [Alert Tones — Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2025650566) [knowledge-corroborated]
- Move Speed — path cost 13/(13+pathcost), soil cost 2, terrain speed cap
- Moving / Environment — terrain path costs
- Roof / Build roof area / Remove roof area — roof zones, 65-tick build, 6-tile support, collapse
- [Officially unofficial guide to the reference sim's Artstyle](https://spdskatr.github.io/RWModdingResources/artstyle.html) — 2–3px black border, 64×64, low-noise discipline
- Pawn facing 4-dir / minimal animation / sleep pose / fire flicker / weather particles: **[knowledge, unverified]** (wiki search did not return a directional-rendering page; corroborated by the art guide's "almost no animation" note and general the reference sim knowledge)
