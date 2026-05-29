# Spec: RimWorld-Style Work-Priority System (PawnSim)

Status: DESIGN / not yet implemented
Author: AI Designer subagent
Date: 2026-05-30
Scope: docs only — no code in this change.

> Source files read for this spec:
> `Assets/Scripts/AI/PawnActions.cs`, `IPawnAction.cs`, `PawnContext.cs`.
> The decision driver (referenced in comments as `PawnUtilityAI`) and the
> `WorkKind` enum + `WorkSettings` component could not be opened by name during
> this pass — the programmer should locate the concrete files (grep
> `WorkKind`, `WorkSettings`, `Decide(`); their *behaviour* is fully described
> below from the call sites in `PawnActions.cs` / `IPawnAction.cs`.

---

## 1. Current state — how jobs are assigned NOW

### Mechanism
Job selection is a **fixed-order priority chain over `IPawnAction` objects**, not
a per-pawn priority grid.

- Every action implements `IPawnAction` with three members:
  `bool TryStart(PawnContext)`, `string DisplayName`, `WorkKind Kind`.
- The driver (`PawnUtilityAI`, per the doc comment in `IPawnAction.cs`) holds a
  **single hard-coded ordered list** of actions and calls `TryStart` on each in
  turn. The **first** action that returns `true` wins; the rest are skipped that
  decision tick. (`IPawnAction.cs` lines 9-11.)
- `TryStart` does *both* eligibility (conditions met?) and execution (find target
  + set it on the worker component). Returns `false` ⇒ "not my turn, try next".
- The documented order (`PawnActions.cs` lines 8-14):
  1. `EatBerryAction`   — self food < 40
  2. `HuntAnimalAction` — global food < 5
  3. `CookMealAction`   — global food > 5 and a stove exists
  4. `ChopTreeAction`   — default labor
  5. `WanderAction`     — fallback
  - plus `BuildBlueprintAction`, `MineStoneAction`, `TendPatientAction`,
    `HaulWoodAction`, `HaulStoneAction`, `HaulMeatAction` slotted into the chain.

### Work-type taxonomy that EXISTS today (`WorkKind`)
Confirmed enum members referenced in `PawnActions.cs`:
`Gather`, `Hunt`, `Cook`, `Chop`, `Research`.

The taxonomy is **collapsed**. Several distinct jobs are forced onto one slot:

| Real job          | Action class            | `WorkKind` it reports | Note |
|-------------------|-------------------------|-----------------------|------|
| Chop wood         | `ChopTreeAction`        | `Chop`                | true Chop |
| Build blueprint   | `BuildBlueprintAction`  | **`Chop`**            | "1차로 Chop 슬롯" (PawnActions L180) |
| Mine stone        | `MineStoneAction`       | **`Chop`**            | "1차로 Chop 슬롯" (L263) |
| Haul wood         | `HaulWoodAction`        | **`Chop`**            | "1차로 Chop 슬롯" (L368) |
| Haul stone        | `HaulStoneAction`       | **`Chop`**            | (L330) |
| Haul meat         | `HaulMeatAction`        | **`Chop`**            | (L298) |
| Tend patient      | `TendPatientAction`     | **`Research`**        | "Research 슬롯 재활용" (L220) |
| Gather berries    | `EatBerryAction`        | `Gather`              | |
| Hunt              | `HuntAnimalAction`      | `Hunt`                | |
| Cook              | `CookMealAction`        | `Cook`                | |
| Wander (fallback) | `WanderAction`          | `Chop`                | not real work (L413) |

### Operator control today
- There is a `WorkSettings` concept (`IPawnAction.cs` L26: "WorkSettings 가 disable
  했으면 skip") — a **colony-wide / per-pawn on-off per `WorkKind`**, used by the
  driver to skip an action whose `Kind` is disabled.
- That is the *only* operator lever. It is **binary** (enabled / disabled), it is
  keyed on the **collapsed** `WorkKind`, and there is **no relative ordering**:
  the chain order is hard-coded in source.

### Limitations (the gap the operator is pointing at)
1. **Hauling is not prioritizable.** All three haul actions report `WorkKind.Chop`.
   The operator cannot say "this pawn hauls first" or "that pawn never hauls" —
   toggling `Chop` also kills chopping, building, mining.
2. **No per-pawn job preference.** Every pawn runs the same fixed chain. You can't
   make a dedicated hauler, a dedicated doctor, etc. Specialisation is impossible.
3. **No relative priority.** Chain order is global and compile-time. The operator
   can't promote "Doctor before everything" for one pawn and "Doctor last" for
   another.
4. **Collapsed taxonomy hides intent.** Build / Mine / Haul / Tend share a slot
   with unrelated work, so even the existing on/off lever is too coarse.

---

## 2. Target — RimWorld-style priority model

RimWorld's grid: each pawn has a column per work type, cell value 0 (disabled) or
1–4 (1 = highest). Each pawn, when free, scans work types **in ascending priority
number** (1 first), and within the chosen type picks the nearest reachable job.
We adapt this to the existing `IPawnAction` chain + `WorkKind` taxonomy.

### 2.1 Work types (de-collapse `WorkKind`)
Promote the collapsed slots to first-class work types so each is independently
prioritizable. Target enum:

| `WorkKind`   | Korean label | Actions mapped                              |
|--------------|--------------|---------------------------------------------|
| `Firefight`  | 소화         | (future) — reserve slot, highest by default |
| `Doctor`     | 치료         | `TendPatientAction` (move off `Research`)   |
| `Cook`       | 요리         | `CookMealAction`                            |
| `Hunt`       | 사냥         | `HuntAnimalAction`                          |
| `Construct`  | 건설         | `BuildBlueprintAction` (move off `Chop`)    |
| `Grow`       | 재배         | (future) — reserve slot                     |
| `Mine`       | 채광         | `MineStoneAction` (move off `Chop`)         |
| `Chop`       | 벌목         | `ChopTreeAction`                            |
| `Gather`     | 채집         | `EatBerryAction`                            |
| `Haul`       | 운반         | `HaulWoodAction` / `HaulStoneAction` / `HaulMeatAction` |
| `Clean`      | 청소         | (future) — reserve slot                     |
| `Research`   | 연구         | (future real research action)               |

`WanderAction` stays a non-work fallback (no `WorkKind`-gating; always runs last).
Slots marked "future" can be enum entries with no action yet — they keep the grid
shape RimWorld-shaped and avoid a second refactor later.

> Migration note: today `Construct`, `Mine`, `Haul*` all report `Chop`, and
> `Doctor` reports `Research`. De-collapsing means changing each action's `Kind`
> getter — a one-line edit per action class. This is the load-bearing change; the
> reservation/targeting logic inside each `TryStart` is untouched.

### 2.2 Per-pawn priority value
Per pawn, per `WorkKind`, store a byte priority:

```
0 = disabled (pawn never does this work)
1 = highest  (do this before any 2/3/4 work)
2, 3 = normal tiers
4 = lowest   (only when nothing higher is available)
```

Storage: a `PawnWorkPriorities` component on the pawn — `byte[WorkKind count]`,
serialized so it survives save/load and is operator-editable. Default values are
`[SerializeField]` on a `WorkPriorityDefaults` ScriptableObject / config so the
Systems Designer can rebalance starting priorities without code (avoids the
hardcoded-weights pitfall).

Suggested defaults (every pawn, until operator overrides):
`Firefight=1, Doctor=2, Cook=3, Hunt=3, Construct=3, Mine=3, Chop=3, Gather=3,
Haul=4, Clean=4, Research=4, Grow=3`. (Haul/Clean default to lowest so they're
"chore" work — matches RimWorld feel and the operator's "haul by priority" ask.)

### 2.3 Selection rule (replaces the fixed chain)
Per pawn, each decision tick:

1. **Survival overrides stay hard-wired** (not on the grid): a starving pawn
   (`needs.food < threshold`) still runs `EatBerryAction` first regardless of the
   `Gather` priority — RimWorld does the same (eating/sleeping aren't on the work
   tab). Keep `EatBerryAction` as a pre-grid forced check.
2. **Group eligible actions by `WorkKind`.** An action is eligible if its
   `TryStart` *would* succeed (see note below) **and** its `WorkKind` priority for
   this pawn is `> 0`.
3. **Sort by priority number ascending** (1 before 4). Iterate priority tiers; the
   first tier that has ≥1 eligible action with a reachable target wins.
4. **Tie-break within a tier by proximity / existing utility.** Multiple work
   types at the same priority number → pick the action whose target is nearest
   (the `FindNearestX` distance the actions already compute), falling back to the
   documented chain order as a stable secondary tie-break.
5. **Run that action's `TryStart`** (which reserves + sets the target). On
   reservation race failure (`TryStart` returns false), drop that action and
   continue down the sorted list — exactly the current "try next" semantics.

> Eligibility-without-side-effects note: today `TryStart` *commits* (reserves) on
> success, so you can't cheaply "ask" every action. Two implementable options:
> (a) Add `bool CanStart(ctx)` to `IPawnAction` (pure check, no reserve) used for
> grouping/sorting, and keep `TryStart` for commit. Cleanest, small interface
> change. (b) Keep `TryStart` but iterate the **priority-sorted** action list and
> let the first success win — no separate `CanStart`, at the cost of losing the
> within-tier nearest-target tie-break (chain order decides ties instead). Phase 1
> uses (b); phase 2 adds `CanStart` for true nearest-target tie-breaking.

### 2.4 Decision interval
Keep the existing decision cadence (driver already re-decides on a timer, per the
"#114/#199" tuning history). Target **0.3–0.5 s** between decisions, NOT per-frame
— sorting the grid every frame for N pawns is wasteful and causes job jitter.
Re-decide early only on a discrete event (target depleted, task finished, pawn
downed). This preserves the anti-jitter guarantee from the current design.

---

## 3. Stockpile priority × haul priority interaction

Two **orthogonal** priorities must not be conflated:

- **Haul work-priority (this spec, per *pawn*)** — answers *"should this pawn do
  hauling at all, and how eagerly vs its other work?"* Lives on
  `PawnWorkPriorities[Haul]`.
- **Stockpile priority (#155, 5-tier, per *stockpile zone*)** — answers *"once a
  pawn IS hauling, which destination gets the item?"* Lives on the stockpile zone.

Combined flow when a pawn decides to haul:

1. Grid selection (§2.3) decides the pawn does `Haul` this tick (because `Haul`
   priority > 0 and it out-ranks / ties-and-wins the pawn's other eligible work).
2. The chosen haul action finds a haulable item (the existing `FindNearestPile` /
   `FindNearestChunk` / `FindNearestMeat`, which already respect reservations,
   `InStockpile`, and "blueprint needs material" pull logic).
3. **Destination selection uses #155 stockpile priority**, not work-priority:
   among all valid stockpiles that accept the item type, pick the **highest-tier**
   stockpile (tier 5 > tier 1); ties by nearest. (This is the existing/already-
   planned #155 behaviour — the work-priority layer feeds *into* it, it does not
   replace it.)
4. **Re-haul rule preserved.** Item already in an *equal-or-higher* tier
   stockpile ⇒ not re-hauled (prevents loops; mirrors current `InStockpile` skip
   in `HaulWoodAction` L399-400). Item in a *lower*-tier stockpile and a free
   higher-tier slot exists ⇒ eligible to upgrade (RimWorld behaviour).

Net: **work-priority gates the *worker*, stockpile-priority routes the *item*.**
They compose; neither needs to know the other's numbers.

---

## 4. UI sketch (text) — the priority grid panel

A toggle-able panel matching the existing `UITheme` colony-panel style (same
header bar, row striping, hover highlight as the current zone/stockpile panels).

```
┌─ 작업 우선순위 (Work) ──────────────────────────────────┐
│           소화 치료 요리 사냥 건설 채광 벌목 채집 운반 청소 연구 │
│  Alice     1    2    3    -    3    3    3    3    4    4    -  │
│  Bjorn     1    -    -    3    1    1    3    3    3    4    -  │   ← miner/builder
│  Cara      1    1    3    3    3    3    3    3    4    4    2  │   ← doctor
│  Dmitri    1    -    -    -    4    -    -    -    1    1    -  │   ← dedicated hauler
│  [+ 모두 3] [모두 끄기] [기본값]                                │
└──────────────────────────────────────────────────────────┘
```

Interaction:
- Rows = pawns, columns = work types (RimWorld layout exactly).
- **Left-click a cell** cycles `disabled(-) → 1 → 2 → 3 → 4 → disabled`.
- **Right-click** cycles backwards (quality-of-life; optional phase 2).
- `-` renders dimmed (disabled). Priority `1` rendered in a warm/accent UITheme
  color so high-priority columns pop.
- Column header click: open all-pawns helper (set whole column, optional p2).
- Footer buttons: bulk "all 3", "all off", "reset to defaults".
- Panel is read+write directly to each pawn's `PawnWorkPriorities` component.
- Hover a cell ⇒ tooltip with work-type description + which action(s) it maps to.

Visual ownership note: the *grid widget* (layout, colors, fonts) is the Artist /
UITheme owner's call; this spec only fixes the data binding and click semantics.

---

## 5. Implementation phases (incremental, daily-shippable)

### Phase 1 — MINIMAL: haul on/off + 1-byte priority per pawn  (EFFORT: S, ~1 day)
Goal: directly satisfy the operator's "hauling by priority" ask with the smallest
change that ships and demos.

1. **De-collapse `Haul` only.** Change `HaulWoodAction` / `HaulStoneAction` /
   `HaulMeatAction` `Kind` getter from `Chop` → new `WorkKind.Haul`. Add `Haul`
   to the enum. (Leave Build/Mine/Doctor collapsed for now.)
2. **Add `PawnWorkPriorities` component** storing a `byte` per `WorkKind`,
   `[SerializeField]` defaults, save/load wired.
3. **Driver change (option (b) from §2.3):** before running the fixed chain, sort
   the eligible actions by their pawn's per-`WorkKind` priority (asc), disabled
   (0) removed; keep the existing chain order as the tie-break. First `TryStart`
   success wins. Survival (`EatBerryAction` low-food) stays a forced pre-check.
4. **Minimal UI:** for Phase 1, a per-pawn **inspector control** is enough — when
   a pawn is selected, show its work-priority row (the single grid row from §4)
   so the operator can set `Haul` (and any other type) 0–4 on that pawn. Full
   multi-pawn grid is Phase 2.

Ships: operator can pick one pawn, set Haul=1 (eager hauler) or Haul=0 (never
hauls) — and the same lever already works for every other work type for free.

### Phase 2 — FULL grid  (EFFORT: M, ~2–3 days)
1. **De-collapse the rest:** `Construct` (off `Chop`), `Mine` (off `Chop`),
   `Doctor` (off `Research`); add `Firefight/Grow/Clean/Research` placeholder
   slots.
2. **Add `CanStart(ctx)`** to `IPawnAction` (pure eligibility, no reserve) and
   switch the driver to option (a): true within-tier **nearest-target** tie-break.
3. **Full multi-pawn grid panel** (§4) with click-to-cycle, bulk buttons,
   defaults, tooltips, UITheme styling.
4. **#155 stockpile-tier integration** in haul destination selection (§3) if not
   already landed.
5. **Defaults config** (ScriptableObject) so Systems Designer rebalances starting
   priorities without code.

### Phase 3 — polish (EFFORT: S, optional)
- Right-click reverse-cycle, column-header bulk set, "copy row to all".
- Manual-priority hint (RimWorld's checkbox between numbers/checkboxes modes).
- Per-pawn skill-gating: grey out a work type the pawn is incapable of
  (needs `PawnSkills` "incapable" flags — not confirmed present; verify file).

---

## 6. Risks + what NOT to break

**Do NOT break (regression-critical, from #199–#201):**
- **Central reservation (#199 C2).** Every `TryStart` calls
  `ReservationManager.TryReserve(...)` and bails on a same-frame race
  (`PawnActions.cs` L34, L75, L113, L150, L189-227, L270). The new selection layer
  must call the **unchanged** `TryStart` for commit — never reserve in the
  sort/group step. (This is exactly why Phase 1 uses option (b) and the optional
  `CanStart` must stay side-effect-free.)
- **Reserved-by-other skipping in `FindNearestX`.** All finders skip targets
  reserved by other pawns (e.g. L49, L90, L167). The priority layer sits *above*
  target-finding; do not move reservation checks out of the finders.
- **Give-up / retry-next-tick semantics.** `TryStart` returning `false` must keep
  meaning "yield this tick, try next/other action" — the sorted-list iteration
  must continue past a false, not abort the whole decision.
- **Adjacency / stand-cell logic** (#200–#201, inside the worker components, not
  shown here) — work-priority never touches movement or adjacency; it only
  chooses *which* action commits.
- **Blueprint-material gating (#196/#197).** `BuildBlueprintAction` only targets
  blueprints with `HasAllMaterials` (L206); haulers pull stockpile material only
  when a blueprint needs it (L385-400, L342-355). De-collapsing `Construct`/`Haul`
  must keep these `TryStart` bodies byte-for-byte.

**Risks specific to this change:**
1. **Starvation soft-lock if survival goes on the grid.** If `Gather`/eating is
   priority-gated and operator sets it 0, a pawn could starve. Mitigation:
   keep self-feeding (`EatBerryAction` low-food branch) as a forced pre-grid
   check, NOT a grid-gated work type (§2.3 step 1).
2. **All-disabled deadlock.** If every work type for a pawn is 0, it only Wanders.
   That's intended (RimWorld allows it) but warn in UI (dim the row / "no work").
3. **Tie-break instability → jitter.** Equal-priority work with shifting nearest
   targets could thrash. Mitigation: 0.3–0.5 s decision interval + stable
   secondary tie-break (chain order) so equal distances resolve deterministically.
4. **Save migration.** Existing saves have no `PawnWorkPriorities`. On load,
   absent component ⇒ apply defaults (don't crash). Old `WorkSettings` on/off, if
   present, maps to priority `3` (on) / `0` (off).
5. **O(N×W) per decision.** N pawns × W work types each tick. With the 0.3–0.5 s
   interval and W ≈ 12 this is trivial; do NOT regress to per-frame. Cache the
   sorted action list per pawn; invalidate only when priorities change.
6. **WorkSettings overlap.** If the old global `WorkSettings` (from #114) still
   gates actions, decide ownership: either fold it into per-pawn priorities
   (recommended) or have it act as a colony-wide hard mask AND-ed with the grid.
   Pick one to avoid two sources of truth.

---

## Appendix — open items for the programmer to confirm
- Exact filename/contents of the decision driver (`PawnUtilityAI`?) and its
  current decision-interval value.
- `WorkKind` enum's full current member list and `WorkSettings` shape.
- `PawnSkills` contents — whether per-work "incapable" flags exist (affects
  Phase 3 cell-greying).
- #155 stockpile 5-tier API surface (tier field name, accepts-type query) for §3.
