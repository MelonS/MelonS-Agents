# Plan #199 — Pawn movement: full RimWorld-grid fidelity (A + B + C)

> PM / resource-allocation plan.  Operator decision 2026-05-29: bring pawn
> movement to full grid fidelity — **A** (pawn render 2×2 → 1×1 + camera/labels),
> **B** (point-lerp → grid A\* pathfinding, kill the nudge/axis-slide hacks),
> **C** (cell occupancy + adjacent-work-cell + reservation + build-placement block).
>
> **This increment is movement-core correctness ONLY.**  No power-grid / trading /
> taming / bills.  "기능추가는 보수적" still binding for non-movement scope.
>
> **Daily-shippable is sacred.**  Every step below ends with a launchable
> `PawnSim.exe` + `refactor_check.py` ALL stages PASS.  Operator can STOP after
> any step with a working build.  A step that would leave the build red or a
> guard-test red is rejected.

---

## 0. Baseline to protect (must stay GREEN unless a step explicitly schedules a change)

- Isolated V tests: **65/65**
- Integration I tests: **37/37**
- Build Click QA: **6/6** (now a real gate after #198 prefix fix)
- `refactor_check.py` 6 stages: scenes regen / build verify / QA screenshot /
  runtime-error scan / visual diff / PlayMode tests.

Grid fact baseline (verified in source): `Grid.cellSize = (1,1)`
(`SceneSetup.Game.Terrain.cs:48`).  Pawn currently renders 2×2 world units
(32px sprite @ PPU 16, scale 1; `SceneSetup.Pawn.cs:33-35`, collider 2×2 at
:47-48).  Camera `orthographicSize = 6` (`SceneSetup.Game.Core.cs:21`).

---

## 1. Sequencing decision (why this order)

Operator hint is A→B→C.  I keep that order but **insert a B0 stub-first step**
because the movement rewrite (B) is the single highest regression risk in the
whole prototype — every worker (Chopper/Gatherer/Hunter/Cook/Builder/Hauler/
Miner/Doctor), drafted combat, wolf chase, trader wander, and the I19
unreachable-detection all sit on top of `PawnMovement`.

Key sequencing rules applied:

1. **A first** — pure presentation (sprite scale, camera ortho, label/bar
   offsets).  Zero behavior change, zero test logic change.  De-risks the visual
   layer before we touch movement, and gives the operator an immediate visible
   win (1×1 pawns on a 1×1 grid).
2. **B as feature-flagged parallel system, not in-place rewrite.**  A\* is built
   behind `PawnMovement.UsePathfinding` (default **false** at B1/B2 build, flipped
   to **true** only inside B2's acceptance once green).  The old MoveTowards path
   stays compiled as the fallback through B, so every intermediate build ships.
   The nudge/axis-slide hacks are deleted only in **B2** *after* the flag is on
   and guards re-green — never before.
3. **C last** — adjacent-cell + reservation + build-placement all *depend* on a
   working PathGrid (occupancy queries reuse the same grid B builds).  Doing C
   before B would mean writing occupancy against the lerp system and rewriting it.

Net shape: **A1, A2, B0, B1, B2, B3, C1, C2, C3** — 9 shippable steps.
Each is independently shippable; operator can stop after any.

---

## 2. Step-by-step breakdown

Legend: **Deliverable** = what lands.  **Accept** = binary criterion a QA agent
verifies (build + log/test, no human eyeballing required).  **Guards** = existing
V/I that must stay green.  **New** = V/I the programmer adds this step.

---

### A1 — Pawn render 1×1 + camera zoom-in
**Deliverable**
- Pawn renders 1×1 world unit (one grid cell).  Either re-import
  `pawn_colonist.png` at PPU 32 (so 32px → 1 unit) or set transform scale 0.5;
  programmer picks whichever keeps the sprite crisp (PPU change preferred —
  scale 0.5 with Point filter is also fine).  Collider `BoxCollider2D.size`
  → `(1,1)` (`SceneSetup.Pawn.cs:48`).
- Camera `orthographicSize` **6 → 3.5** (see §6).  `zoomMax` wheel-out
  unchanged so operator can still pull back.
- Pawn spawn positions unchanged (no movement-logic touch).

**Accept**
- Build OK, runtime errors 0.
- QA screenshot: pawn bounding box ≈ 1 grid cell (programmer asserts pawn
  sprite world-height ≈ 1.0 ± 0.1 via a one-line log of `sr.bounds.size.y`;
  QA greps it).
- Visual diff will intentionally change → step ships with `--accept-visual`
  and a new baseline.  Log the chosen ortho.

**Guards**: all 65 V / 37 I stay green (no logic changed).
**New**: **V66** — pawn sprite world size ≈ 1×1 (`bounds.size.y ∈ [0.9,1.1]`)
and collider size == (1,1).

---

### A2 — Reposition floating bars + name labels for 1×1 pawn
**Deliverable**
- `PawnFloatingBars` (HP/mood) and `PawnNameLabel` head-offset recomputed for
  the smaller pawn.  Old offsets assumed a 2-unit-tall sprite (head ≈ +1.0);
  new head ≈ +0.5.  Bar width / label font scaled so they remain legible at
  ortho 3.5 (they were sized for ortho 6 in #198 era).

**Accept**
- Build OK, runtime 0.
- QA screenshot: bars/label sit directly above the pawn head (not overlapping
  body, not floating a cell away).  Programmer logs the world-space top of the
  bar vs pawn-head Y; QA asserts `barY - headY ∈ [0.1, 0.7]`.
- Visual diff intentional → `--accept-visual`, new baseline.

**Guards**: 66 V / 37 I green.
**New**: **V67** — floating-bar/label anchor offset within
`[0.1, 0.7]` of pawn-head for the 1×1 pawn.

> **End of A: operator has 1×1 pawns on a 1×1 grid, correct labels, zoomed-in
> camera. Movement still old-style (point lerp). Fully shippable.**

---

### B0 — PathGrid data structure (no behavior change, flag OFF)
**Deliverable**
- New `Pathfinding/PathGrid.cs`: a static/service grid mirroring the 60×60
  tile map (index `[-29..29]²`).  Per-cell **walkable** flag derived from the
  existing obstacle sources: `PawnMovement.IsBlockedAt` (Water/Rock tiles) +
  `WallEntity` cells (blocking) with doors marked **passable**.  Grid built
  once at scene start and on wall build/destroy (subscribe or rebuild-dirty).
- New `Pathfinding/AStar.cs`: 4- or 8-neighbour A\* over PathGrid returning a
  `List<Vector2Int>` cell path (or empty = unreachable).  8-neighbour with
  corner-cut prevention is preferred (RimWorld is 8-dir) but 4-dir is an
  acceptable shippable first cut — programmer's call.
- **No wiring into PawnMovement yet.**  Pure additive code.
  `PawnMovement.UsePathfinding` flag added, default **false**.

**Accept**
- Build OK, runtime 0.  Zero behavior change (flag off).
- V-test exercises A\* in isolation: a known map (wall row with a gap) →
  path routes through the gap; a fully-walled target → empty path.

**Guards**: 67 V / 37 I green (nothing wired).
**New**:
- **V68** — A\* finds a path around a single wall segment (path length > straight-line cell count, path avoids wall cells).
- **V69** — A\* returns empty/`unreachable` for a target fully enclosed by walls.
- **V70** — PathGrid marks Water/Rock/Wall cells unwalkable, door cells walkable.

> **Shippable: dead code path, build identical to A2 at runtime.**

---

### B1 — PawnMovement follows A\* path (flag ON in tests only), old path kept
**Deliverable**
- `PawnMovement.SetTarget` (when `UsePathfinding`): compute A\* path to the
  target cell, store the cell queue, advance cell-by-cell with the existing
  speed/floor/door/leg-damage multipliers (reuse the whole speed block at
  `PawnMovement.cs:120-141`).  Arrival = final cell reached within
  `arriveDistance`.
- If A\* returns empty → **set an `Unreachable` state** (HasTarget false +
  a queryable `LastPathFailed` bool).  This is the hook C and the give-up
  timers will use (see §4 risk).
- Flag still **default false in the shipped scene** at B1; the new path is
  validated only by V-tests that flip the flag on a test pawn.  Old MoveTowards
  remains the live behavior → integration I-tests unchanged & green.

**Accept**
- Build OK, runtime 0.
- New V-tests (flag on) prove path-following: pawn reaches a target around a
  wall; pawn flags unreachable for enclosed target.
- All existing 37 I (flag-off live behavior) untouched & green.

**Guards**: 70 V / 37 I green.
**New**:
- **V71** — flag-on pawn reaches a target on the far side of a wall via the
  gap (final dist ≤ arriveDistance within N sim seconds; moved distance > 0).
- **V72** — flag-on pawn given an enclosed target sets `LastPathFailed` and
  stops (does not jitter / does not move forever).

---

### B2 — Flip flag ON in scene + delete nudge & axis-slide hacks
**Deliverable**
- `PawnMovement.UsePathfinding` default → **true**; scene generation uses it.
- **Delete** the I19 perpendicular-nudge block (`PawnMovement.cs:85-116`) and
  the x/y axis-slide obstacle dodge (`:151-166`).  Real pathing replaces both.
  `ClampToWorld` and the Water/Rock `IsBlockedAt` guards stay (cheap safety).
- Re-green the integration suite (see §3 — I-tests that asserted straight-line
  arrival timing may need their distance/time tolerances loosened, not their
  pass/fail intent).

**Accept**
- Build OK, runtime 0.
- **Full I suite green again** — specifically I19 (chop end-to-end), I20 (crop),
  I21 (drafted vs wolf), I22/I23 (save / 60s stress, exception 0).
- REAL QA: wood still rises in a fresh 25s build (worker pawns still reach
  trees) — same gate as #198's "wood +X" check, must be > 0.
- Nudge/axis-slide code physically removed (QA greps the file: no
  `lastUnstuckTime`, no `nextX`/`nextY` axis-slide).

**Guards**: 72 V green; I suite green **after** the tolerance edits below.
**New**: none (this step is the cutover; correctness proven by re-greening I).

> **This is the highest-risk step. De-risking detail in §4.**

---

### B3 — Walls block paths end-to-end in the live game
**Deliverable**
- Confirm wall build/destroy dirties PathGrid so a wall the player builds
  mid-game actually re-routes pawns (B0 built the rebuild hook; B3 wires the
  live `WallEntity` create/destroy events to it and adds the integration test).
- Doors confirmed passable through the path (door cells walkable in grid).

**Accept**
- Build OK, runtime 0.
- Integration test: spawn pawn + target, drop a wall across the straight line
  mid-sim → pawn detours and still arrives.  Drop a full enclosure → pawn
  flags unreachable (worker gives up, no infinite spin).

**Guards**: 72 V / I suite green.
**New**:
- **I38** — wall built across path mid-sim → pawn reaches target via detour.
- **I39** — door in a wall line → pawn routes through the door cell (passable).

> **End of B: real grid A\* pathfinding. Pawns route around obstacles, walls
> block, doors pass, hacks gone. Fully shippable.**

---

### C1 — Pawns path to an ADJACENT cell of work targets (not on top)
**Deliverable**
- Work actions (Chop/Build/Harvest/Cook/Gather/Hunt/Mine/Haul) request a
  path to a **walkable cell adjacent to the target's cell**, RimWorld-style,
  instead of the target's exact position.  Add a helper
  `PathGrid.NearestAdjacentWalkableCell(targetCell, fromCell)`.
- The give-up / in-range checks in every worker (`PawnChopper.cs:53-61` pattern
  and its siblings) updated so "in range" is evaluated against the adjacency
  geometry, **not** `dist ≤ chopRange` measured to target center — see §4, this
  is the load-bearing change that keeps give-up timers honest.

**Accept**
- Build OK, runtime 0.
- REAL QA wood still rises (choppers still complete) — pawns now stand beside
  the tree, not on it.
- Full I suite green.

**Guards**: 72 V / I suite green.
**New**:
- **V73** — worker pawn's final standing cell is adjacent to (not equal to) the
  work target's cell.
- **I40** — chop end-to-end with adjacency: pawn stops on a neighbour cell,
  tree still gets destroyed, wood rises.

---

### C2 — Cell occupancy + work-target reservation
**Deliverable**
- A reservation layer: (1) two pawns can't claim the same work target —
  generalize the per-worker `claimed`/`ReservedBy` patterns already scattered
  across `PawnActions.cs` (Chop/Gather/Hunt/Build/Haul/Mine each reimplement it)
  into one reservation registry; (2) two pawns don't target the same adjacent
  standing cell (cell reservation).  Reservation released on task complete /
  give-up / pawn death.

**Accept**
- Build OK, runtime 0.
- Full I + V green.

**Guards**: 73 V / I suite green.
**New**:
- **V74** — two pawns offered one tree → distinct trees claimed (existing
  behavior) AND if only one tree exists, only one pawn reserves it, the other
  picks another action.
- **V75** — two pawns never reserve the same adjacent standing cell for the
  same target (second pawn picks a different free adjacent cell or waits).

---

### C3 — Build placement blocked on occupied / unwalkable cells
**Deliverable**
- Blueprint placement (BuildManager / building placement path) rejects a cell
  that is occupied by a pawn, blocked (Water/Rock/Wall), or already has a
  blueprint/building.  Visual/log feedback on reject (reuse existing build-mode
  feedback; no new UI scope).

**Accept**
- Build OK, runtime 0.
- Build Click QA still 6/6 (placement on a valid empty cell still works).
- Full I + V green.

**Guards**: 75 V / I suite / Build Click QA 6/6.
**New**:
- **I41** — attempt to place a blueprint on a cell occupied by a wall/pawn →
  placement rejected, no blueprint spawned; placement on adjacent free cell →
  succeeds.

> **End of C: full grid occupancy + reservation + placement validation.
> Increment #199 complete.**

---

## 3. Regression guard list

**Must stay GREEN through the entire increment (never allowed to fail at a
step boundary):**

- Build Click QA **6/6** — gated every step (esp. C3 touches placement).
- `refactor_check.py` all 6 stages — every commit.
- Worker give-up integration: **I19** (chop end-to-end), **I20** (crop harvest),
  **I21** (drafted vs wolf), **I22** (save/load), **I23** (60s stress, 0
  exceptions).
- V-suite 65 existing isolated tests.

**Tests that LEGITIMATELY must CHANGE (scheduled, with re-green requirement
before the owning step closes):**

- **Any I/V that asserts straight-line arrival distance or a tight arrival-time
  bound** (movement is now cell-stepped and may detour) — touched in **B2**.
  Allowed change: loosen distance/time *tolerances* and update expected final
  standing position. **Not** allowed: change pass/fail *intent* (pawn must still
  arrive / still chop / still rise wood).  Re-green required before B2 closes.
- **I19 specifically**: its assertion is "pawn moved > 0 and tree destroyed and
  wood rose."  The *moved-distance* and *final-position* expectations change in
  B2 (detour path, and in C1 the pawn stops on an adjacent cell so final dist to
  tree ≈ 1.0 instead of ≈ 0).  Pass/fail intent (tree destroyed, wood up) stays.
- **Give-up timer in-range checks** in PawnChopper/Gatherer/Hunter/Cook/Builder/
  Hauler/Miner — the `dist > range` term changes meaning in **C1** (adjacency).
  These are production code, not tests, but they are the most likely silent
  regression; V73/I40 guard the chop case, and the same edit must be mirrored
  to all workers in C1.

---

## 4. Risk callouts + de-risking

**R-1 (highest): give-up timers assume direct-line failure.**
Every worker uses `Time.time - taskStartTime > GiveUpSec && dist > range`
(`PawnChopper.cs:55`).  Today that works because the pawn stands *on* the target,
so `dist` → ~0 when arrived.  Two things break it:
- With A\* (B2), a temporarily-detouring pawn has large `dist` for longer →
  could trip the give-up timer *before* it arrives via the long way.
- With adjacency (C1), the arrived pawn's `dist` to target center is ≈ 1.0,
  which can exceed small `chopRange`-style values → false "unreachable".
**De-risk:** introduce an explicit `LastPathFailed` / `Unreachable` signal in
**B1** and switch the give-up condition from "timer + far" to
"path actually failed **or** (timer expired **and** not making progress)".
Workers then give up on *real* unreachability (empty A\* path), not on
distance-to-center.  V72 guards the unreachable signal; V73/I40 guard adjacency
in-range.

**R-2: the nudge hack was masking a real bug (I19).**
The nudge existed because out-of-bounds tree targets made pawns permanently
stuck.  `SetTarget` already clamps to world (`PawnMovement.cs:75`) and AI
FindNearest filters to ±28.5/±18.5 — but do **not** delete the nudge in B0/B1.
Delete it only in **B2**, after A\* + clamp + unreachable-detection together
cover every case the nudge compensated for, and only once I19 re-greens.
Fallback: keep `UsePathfinding=false` shippable through B1 so if B2 destabilizes,
the operator can ship A2/B1 (old behavior live) while B2 is fixed.

**R-3: PathGrid staleness on dynamic walls.**
If the grid isn't rebuilt when the player builds/removes a wall mid-game, pawns
path through new walls or refuse old openings.  De-risk: B0 builds the
rebuild-dirty hook; B3 explicitly tests live wall build → reroute (I38) and door
pass (I39).  Cheap full-rebuild on wall change is acceptable for a 60×60 grid
(performance is not the constraint here; correctness is).

**R-4: per-frame A\* cost.**
PawnChopper re-calls `SetTarget` every frame (`:82`).  If `SetTarget` recomputes
A\* every frame for every pawn, that's 60×N A\*/sec.  De-risk: cache the path in
`SetTarget`; only recompute on target change or PathGrid dirty.  Note this in
B1 — the every-frame re-target callers must not re-path every frame.

**R-5: reservation deadlock / leak.**
Reservations not released on give-up/death → cells/targets permanently locked,
pawns idle.  De-risk: C2 releases reservation in the same place tasks clear
(`ClearTask`) and on pawn death; V74/V75 + I23 (60s stress, 0 exceptions, and
implicitly progress) guard against frozen colonies.

**R-6: visual baseline churn.**
A1/A2 intentionally change the baseline (sprite size, camera, labels).  Each
ships with `--accept-visual` + a fresh baseline so the visual-diff stage doesn't
false-red. Don't bundle A1 and A2 into one baseline-accept — separate so a
regression in one is attributable.

---

## 5. Camera / visibility plan for A

- **Pawn 2×2 → 1×1** halves on-screen pawn size.  To keep the same apparent
  pawn size (so the operator's #197-era "디테일 보임" win isn't lost), halve the
  ortho: **6 → 3.5** (recommendation; 3.0–4.0 acceptable).  Rationale: at ortho
  6 a 2-unit pawn filled 2/12 = 16.7% of half-height; at ortho 3.5 a 1-unit
  pawn fills 1/3.5 = 28.6% — actually *more* prominent, which is fine for a
  detail-readability goal.  If 3.5 feels too tight for the settlement, 4.0 gives
  25% (≈ the old feel).  Programmer ships 3.5, operator can nudge.
- **`zoomMax` / wheel zoom-out unchanged** so the operator can still pull back
  to see the whole 60×60 map.  Verify min-zoom still allows close inspection of
  one pawn.
- **Camera follow / focus** (the 0.6s smooth focus, I18) — re-verify the focus
  framing still centers the pawn nicely at the new ortho; no logic change
  expected, but I18 stays a guard.
- **Labels/bars (A2):** anchor offset is the real fix, not just scale.  Head
  moves from ≈ +1.0 (2-unit sprite) to ≈ +0.5 (1-unit sprite); recompute the
  bar/label local Y. Font/bar pixel size: because the camera zoomed IN
  (ortho 6→3.5, ~1.7× more pixels per world unit), world-space label/bar widths
  can be **reduced ~40%** and still read the same on screen — otherwise a label
  that was 2 units wide now spans 2 cells.  V67 guards the anchor; eyeball the
  width via the A2 accepted screenshot.

---

## 6. Recommended FIRST step to dispatch

**Dispatch A1 now.**

- It is pure presentation (sprite scale + collider size + camera ortho), zero
  movement-logic and zero test-logic risk.
- It gives the operator an immediate visible result (1×1 pawn on a 1×1 grid,
  zoomed-in camera) — confirms the grid-fidelity direction before we touch the
  high-risk movement core.
- It unblocks A2 (label reposition depends on the new pawn size) and sets the
  visual baseline that B-steps will inherit.

**Concrete A1 hand-off to the programmer:**
1. `SceneSetup.Pawn.cs:35` scale → keep 1.0 but re-import `pawn_colonist.png`
   at **PPU 32** (32px → 1 world unit); OR set scale 0.5 if PPU change is risky.
   Programmer picks; PPU preferred for crispness.
2. `SceneSetup.Pawn.cs:48` `col.size = new Vector2(1f, 1f)`.
3. `SceneSetup.Game.Core.cs:21` `cam.orthographicSize = 3.5f`.
4. Add **V66** (pawn 1×1 sprite + collider assertion).
5. Ship with `--accept-visual` + new baseline; log chosen ortho + pawn
   `bounds.size.y`.
6. Gate: build OK, runtime 0, 65→66 V green, 37 I green, Build Click QA 6/6.

After A1 greens, dispatch A2, then proceed B0→C3 in order, stopping at any step
boundary the operator chooses.
