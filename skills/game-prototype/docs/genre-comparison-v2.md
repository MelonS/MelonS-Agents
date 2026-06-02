# PawnSim vs the reference sim — SECOND-PASS Gap Re-Audit (v2)

Date: 2026-05-30. Author: research agent (read-only on `skills/game-prototype/unity-project/`).
Scope: the operator's **6 gameplay dimensions** (디자인/비주얼 · 사운드 · 림 이동 · 건축 · 게임플레이 루프 · 메뉴 UX/UI).
Predecessor: `skills/game-prototype/docs/genre-comparison.md` (v1) drove cx waves 1-6 + cy waves 1-4. This v2 **verifies what shipped, then finds the next layer.**

> **Source caveat:** 장르 위키 still blocks direct fetch (403); canonical values below come from WebSearch snippets of the wiki + the reference sim domain knowledge. URLs at the end.

---

## What shipped since v1 — VERIFIED in code (do NOT re-list as gaps)

I confirmed each of these is **real and wired**, not just stubbed:

| v1 backlog # | Item | Verified artifact |
|---|---|---|
| 1-9 (SOUND) | sfxBuild / sfxAlert(tier) / wolf-howl / ambient bed / sfxMine / UI-click / danger-music crossfade / rain-loop | `AudioBank.cs` has all 11 slots + tier-scaled `AlertBurstCoroutine` + `DangerCrossfadeCoroutine`; callers verified in BlueprintEntity, AIDirector, StoneVeinEntity, ArchitectMenu, ResearchUI, GuiControlBar, MusicDirector, RainSoundDriver |
| 10-14 (MOVE/VIS) | walk-bob, facing(flipX), sleep-pose, fire-flicker/tree-sway, weather particles | `PawnSpriteBob.cs`, `PawnFacing.cs` (child-flipX, root untouched), `SleepPoseDriver.cs`, `FlickerLight.cs`, `WeatherController.cs`, `NightLightPoolDriver.cs`, `ScatterVarietyDriver.cs` |
| 15-21 (BUILD) | deconstruct, mine-designation, grow-zone, drag-rect, lamp/torch, table+chair, stone-floor | `DeconstructDesignation.cs`, `MineDesignation.cs`, `GrowZoneDesignation.cs`, `BlueprintDragDesignation.cs`, `LampEntity.cs`, `TableEntity.cs`+`ChairEntity.cs`, `StoneFloorEntity.cs`; all surfaced in `ArchitectMenu` (6 categories) |
| 22-23 (UI) | top-right alert/letter stack, inspector tabs | `AlertStackUI.cs` (self-bootstrapping, click-to-pan), `PawnInfoPanel.cs` tabs |
| 25-29 (VERIFY) | V1-V9 + V15 + V59b scenario tests, save-load round-trip, moveSpeed reconcile | `Tests/` holds V1,V2,V3,V4,V5,V6,V7,V8(×2),V9,V15,V59b — 12 scenario test files |

**Verdict: the v1 backlog is ~90% executed.** The cheapest, highest-felt wins (sound coverage, facing/motion, build-ops, alert stack) are all done. This was a high-yield two rounds.

---

## Updated closeness per dimension

| # | Dimension | v1 → **v2** | Meaningful NON-gated layer left? |
|---|-----------|:---:|---|
| 1 | 디자인 / 비주얼 | 70 → **80%** | **Thin.** Style + motion + grounding done. Remaining: pose variety (carry/attack), terrain edge/decal richness. Mostly polish-of-polish. |
| 2 | 사운드 | 30 → **80%** | **Mostly closed.** 11 slots wired. Remaining genuine: footstep + day/night ambient variation + door/cook/shoot SFX. Small. |
| 3 | 림 이동 / 움직임 | 75 → **82%** | **Thin.** Facing shipped. Terrain move-cost is the only real one, and it's **[OP-OK] gated** (logic). |
| 4 | 건축 | 65 → **80%** | **YES — the richest remaining seam.** deconstruct/mine/grow/drag done; catalogue still thin vs vanilla Structure+Security tabs (no fence, no door variety, no barricade/sandbag, no copy/cancel-area). |
| 5 | 게임플레이 루프 | 80 → **85%** | **Partly.** V-slate exists. Remaining non-gated: a few verify edge-cases + carry-cap wiring + save-load entity sub-state. The big lever (mood thought-sum, work-priority) is **[OP-OK] gated**. |
| 6 | 메뉴 UX / UI | 70 → **82%** | **YES — a real seam.** Alert stack + tabs done. Remaining: **multi-select + gizmo command buttons** (genuine vanilla UX, non-gated), work-done/floating feedback text, build-category drag-cancel. |

### Honest one-paragraph verdict
The v1 round closed the **loud** gaps. What's left splits cleanly into three buckets:
1. **A genuine non-gated layer of ~10-12 items** concentrated in **Building catalogue/UX (Dim4)** and **selection/feedback UX (Dim6)**, plus a short tail of sound/visual polish. These are concrete, Unity-verifiable, independently shippable, and follow the proven self-bootstrapping designation/driver pattern the team already uses.
2. **The big-leap items are OP-gated** (mood thought-sum model, work-priority grid, terrain move-cost) — exactly the tier v1 flagged as held. **No further autonomous progress is possible on player-felt depth without unlocking these.**
3. **The over-scope guardrails from v1 still hold** (roofs/temperature/joy/injury systems/cover/14-skill). Do not start them.

So: **a meaningful non-gated layer DOES remain, but it is the last one of its kind.** After this v2 backlog is drained, the next leap requires the operator to unlock the **mood / work-priority** tier.

---

## v2 BACKLOG — the next layer (prioritized, 14 items)

Tags: **code / art / sound / verify**. **[OP-OK]** = behavior-logic, needs operator sign-off (excluded from the actionable set). Imp/Eff: ★★★ → ★. Each has a **binary** acceptance + a **non-overlapping file hint** (the team's proven "one new self-bootstrapping file per lane" pattern). All non-gated items follow existing patterns (designation managers, drivers, AudioBank slots).

| # | M | Item | Tag | Imp/Eff | OP-gate | Binary acceptance | New-file hint |
|---|---|---|---|:--:|:--:|---|---|
| **B1** | M4 | **Multi-select marquee** — left-drag empty ground selects all pawns in the box; commands apply to all | code | ★★★ | | Drag a box over 3 pawns → all 3 show selection ring; right-click move-order moves all 3 | `MarqueeSelector.cs` (reads ClickSelector.currentSelection → promote to `List`; self-bootstrap) |
| **B2** | M6 | **Gizmo command bar on selection** — bottom-center contextual buttons (Draft / Deconstruct-here / Cancel) for the selected thing, mirroring the reference sim InspectGizmoGrid | code | ★★★ | | Selecting a pawn shows a Draft button; clicking it drafts (same as R-key) | `SelectionGizmoBar.cs` (reads ClickSelector selection + existing PawnEntity.Draft) |
| **B3** | M4 | **Fence + fence-gate buildable** (1 wood, passable, low) — vanilla Structure tab staple, cheap | art+code | ★★ | | A fence is buildable from Architect, blocks nothing but renders a low barrier | `FenceEntity.cs` + BuildManager.Mode.Fence (mirror FloorStone lazy-prefab pattern) |
| **B4** | M4 | **Barricade/sandbag buildable** (defense cover marker) — vanilla Security tab; visual+pathing only (no cover-math, that's gated) | art+code | ★★ | | A sandbag is buildable; pawns path around it like a low wall | `BarricadeEntity.cs` + Mode.Barricade + Architect "Security (방어)" category |
| **B5** | M4 | **Autodoor variant** — a faster door (lower PassMul) at higher wood cost; vanilla Door-tab variety | code | ★★ | | An autodoor is buildable and pawns cross it faster than a plain door | extend `DoorEntity.cs` (PassMul field) + Mode.Autodoor catalogue line |
| **B6** | M6 | **Floating combat/work text** — damage numbers on hit, "+5 wood" on harvest, rising-fade popup | art+code | ★★★ | | An arrow hit spawns a red "-N" that rises and fades over 1s above the target | `FloatingText.cs` (self-spawn from a static `FloatingText.Spawn(pos,str,color)`; called by hooking existing events — read-only on combat) |
| **B7** | M4 | **Area-cancel / deconstruct-area drag** — drag-rect to cancel pending blueprints or designate a region for deconstruct | code | ★★ | | Drag over 3 blueprints in cancel mode → all 3 removed | extend `DeconstructDesignation.cs` drag loop (it already drag-rects) |
| **B8** | M2 | **Footstep SFX + day/night ambient variation** — soft footfall throttled per pawn; ambient bed swaps birds(day)/crickets(night) | sound+code | ★★ | | A walking pawn plays a faint footstep ~every 0.4s; ambient differs at 02:00 vs 12:00 | `FootstepDriver.cs` + AudioBank.sfxFootstep/ambientNight slots + DayNightCycle hook |
| **B9** | M2 | **Door / cook / shoot SFX** — the 3 still-silent actions (v1 #7 partially: mine done, these not) | sound+code | ★★ | | Opening a door, cooking at a stove, and firing an arrow each play a distinct sound | AudioBank.sfxDoor/sfxCook/sfxShoot + wire DoorEntity/StoveEntity/ArrowProjectile |
| **B10** | M1 | **Pawn carry-pose + attack-lunge** — hauler shows a carried-item sprite; melee/draft shows a small lunge offset | art+code | ★★ | | A hauling pawn visibly carries something; a melee hit shows a forward jab frame | `PawnPoseDriver.cs` (child-sprite only, root untouched — same firewall as PawnFacing) |
| **B11** | M1 | **Terrain edge decals + rock variety** — water-edge transition tiles, 2-3 rock sprite variants scattered | art | ★ | | Water tiles show an edge band against grass; scattered rocks vary in shape | extend `ScatterVarietyDriver.cs` + new edge-tile sprites |
| **B12** | M5 | **V10 deconstruct→walkable + V11 grow-zone→crop-appears** as gated PASS lines | verify | ★★ | | A test asserts deconstructing a wall makes its cell walkable; painting a grow zone yields a crop | `Tests/V10..` `Tests/V11..` (mirror V15DeconstructRefundTest) |
| **B13** | M5 | **Save→load entity sub-state round-trip** (BedQuality/StockpilePriority/TreeSpecies/WallMaterial) — v1 #29 partially done; close the named gaps | verify+code | ★★ | | After save→load a Fine bed is still Fine, a tier-1 stockpile is still tier-1 | extend SaveLoadManager serialization + `Tests/V9` assertions |
| **B14** | M6 | **Main-menu polish + in-game hotkey cheat-sheet overlay** (H key) — vanilla shows a controls panel; consolidates the existing scattered hotkeys | code | ★ | | Pressing H toggles a panel listing all build/speed/draft hotkeys | `HotkeyCheatSheet.cs` (self-bootstrap UITheme panel) |

### Excluded — still [OP-OK] gated (do NOT put in the actionable set)
- **Mood = thought-sum + 3 break tiers (35/20/5)** — logic rewrite, held since v1.
- **Work-priority grid semantics** — changes how pawns pick work; spec only until OK.
- **Per-terrain path-cost** (dirt/marsh slower) — movement-logic change.
- **Quality-roll on build** — construction-skill-driven mood/value change.
- **BanditEnemy body-parts, carryCapacity→hauler cap** — combat/economy rebalance (medium logic risk; recommend bundling with the gated tier).

### Excluded — over-scope (v1 guardrails still hold)
Roofs+collapse, temperature, joy/recreation need, real injury systems/disease, cover/accuracy-by-range tables, trading/power-grid/taming/bills, detailed 32×32 pawn redraw, 14-skill expansion. **Unchanged from v1 — do not start.**

---

## Top-10 sequencing recommendation

Run in this order — all non-gated, all independently shippable, ordered by impact-per-effort and the proven one-new-file lane discipline:

1. **B6** Floating combat/work text (★★★ — biggest remaining feel win, pure-additive)
2. **B1** Multi-select marquee (★★★ — core the reference sim UX, currently single-select only)
3. **B2** Gizmo command bar on selection (★★★ — pairs with B1)
4. **B8** Footstep + day/night ambient (★★ — last cheap sound layer)
5. **B9** Door/cook/shoot SFX (★★ — closes action-SFX coverage)
6. **B3** Fence + gate buildable (★★ — cheapest catalogue add)
7. **B4** Barricade/sandbag buildable (★★ — Security category)
8. **B10** Carry-pose + attack-lunge (★★ — last motion gap)
9. **B7** Area-cancel drag (★★ — build-UX completeness)
10. **B12 + B13** Verify edge-cases + save-load sub-state (★★ — keeps the gate honest)

---

## Operator recommendation (the real headline)

**The autonomous chain has nearly exhausted the non-gated surface.** v2's 14 items are genuine and worth running, but they are the *last layer of polish/breadth before the gates*. After they ship, every meaningful next leap in player-felt depth — mood that feels like the reference sim, colonists obeying a work-priority grid, terrain that affects movement — is **behind the [OP-OK] wall** that v1 and MILESTONES.md §5 already flagged.

**Recommendation:** approve the **mood thought-sum + 3-tier mental-break** spec and the **work-priority grid** spec (both drafted per MILESTONES §2/§5) so the next wave can move from "looks and sounds like the reference sim" to "*plays* like the reference sim." Without that unlock, the chain will start producing diminishing busywork.

---

## Sources
- User interface — gizmo grid, letter stack, inspect window
- Controls — multi-select, right-click-drag line orders
- Architect / Structure — Structure tab buildable list (wall, door, autodoor, fence, fence gate, column, bridge)
- Door / Autodoor — manual-vs-auto pass delay
- Fence — 1 Stuff, passable, 70-tick build
- Barricade / Defense structures — Security tab, 55% cover (cover-math itself is gated/over-scope)
- Numeric/balance values: see `skills/game-prototype/docs/audit-genre-fidelity-2026-05-29.md` (not restated)
- v1 structural audit: `skills/game-prototype/docs/genre-comparison.md`
