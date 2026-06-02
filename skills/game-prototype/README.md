# game-prototype — PawnSim: a lightweight colony-sim vertical slice (Skill #3-A)

A top-down colony-sim **vertical slice** built **with** the
[`game-dev-agent`](../game-dev-agent/) AI assistant — every sprite, script,
scene, and verification harness is agent-scaffolded from the CLI, with no
manual Unity Editor work in the build chain.

Originally a 7-day shippable prototype; it has since grown — through many
autonomous multi-agent sessions verified against vanilla colony-sim
references — into a far deeper slice with
grid A* pathfinding, body-part health + combat, research, build/deconstruct
designations, farming, hauling, director modes, sound, day/night, and
save/load. The bar is no longer "7-day shippable" but **"바닐라 콜로니심급
prototype + 정직한 작동 검증"** (vanilla colony-sim-grade prototype with honest,
automated proof-of-function) — see [`docs/goal.md`](docs/goal.md).

| | |
|---|---|
| Engine | Unity 6000.0.75f1 LTS |
| Build target | Windows x64 (Standalone) |
| Scope | lightweight colony-sim vertical slice — **NOT a clone** |
| Core coverage | ~85% of vanilla colony-sim core systems (estimate; verified slice growing) |
| Verification | `refactor_check.py` 6-stage gate. **Full suite re-run green 2026-06-03** after the #32–#44 batch: isolated PlayMode **76/76**, integration **43/43**, Build Click QA **9/9**, no runtime errors, pawn-action 7/7 · feature-audit 13/13. (Visual-diff step is intentionally noisy for a live sim — pawns/AI move, so two captures of the same build differ a few %; treated as advisory, not a hard gate.) |
| Latest build | date-stamped `builds/day-PLAY-<date>/PawnSim.exe` — **always resolve dynamically** (`ls -dt builds/day-*/ \| head -1`); the per-day folder rolls over at midnight, so a hardcoded date silently runs a stale build. |

> **PM source-of-truth**: [`docs/MILESTONES.md`](docs/MILESTONES.md) (shipped /
> in-flight / queued). **Outcome layer**: [`docs/goal.md`](docs/goal.md).
> **Session narrative**: [`docs/AUTONOMOUS_SESSION_SUMMARY.md`](docs/AUTONOMOUS_SESSION_SUMMARY.md).

---

## What it is

A small top-down colony sim. Colonists (pawns) autonomously chop wood, mine
stone, gather berries, farm crops, cook, haul, build, research, and defend the
colony — driven by a utility AI. An in-game AI Director
generates threats and events. The player drafts pawns for combat, paints build
and designation orders, and watches needs/health/mood play out, with full
save/load.

It is deliberately **a slice, not a clone**: it implements the loud, core
colony-sim loops at prototype fidelity, and explicitly defers the long tail
(roofs, temperature, joy, real injury systems, cover tables, 14-skill, power/trading/
taming/bills) — see [Out of scope](#out-of-scope-deliberately-deferred).

## Current feature coverage

Verified in code and gated by the harness (see
[`docs/genre-comparison-v2.md`](docs/genre-comparison-v2.md) for the
verified-vs-stub audit):

- **Grid + pathfinding** — **90×90 map** (#235, enlarged from 40→60→90 per
  operator "기본맵부터 상당히 큼") with **4 terrain types** — grass / dirt-soil /
  water (impassable lakes) / rock (impassable), the soil & rock patches widened
  map-wide for visible variety (#43), and **clustered ore veins** (sandstone /
  limestone / granite / marble) placed in dense RimWorld-style blobs rather than
  scattered (#42). `PathGrid` + A* (8-direction, octile cost
  10/14, no corner-cut, 12000-node cap). Pawn 1×1 collider, multi-cell footprints
  (e.g. bed 1×2, research bench 2×1), wall path-blocking (ref-counted),
  door pass-through (slowed), adjacent-stand-cell work positioning,
  `ReservationManager` (no double-occupy), build-placement validation
  (rejects water/rock/occupied with a toast), eject/push-out + standing
  safety-net so a pawn never gets trapped by a freshly-built wall.
- **Pawns** — needs (food/sleep/mood) with perceptible decay; **mood now reacts
  to state** — low food → 배고픔, low sleep → 수면 부족, injury → 부상 negative
  thoughts feed the mood sum so it actually drops when needs/health degrade
  (#35/#36, a first step toward the gated full thought-sum + mental-break model),
  **schedule-driven sleep** (#269 — the
  the reference sim Sleep time-slot makes a pawn walk to a bed and stay asleep through
  the block, not just collapse when exhausted), 6-body-part health (bleed /
  downed / death), 4 skills + XP/level (gather/chop/build/combat), 8 traits with real
  effects (Lazy 0.75× / Industrious 1.30× …), equipment (armor + damage
  bonus), per-pawn facing (flipX) + walk-bob + idle-breathe + sleep-pose
  (sprite-child only — root untouched for pathfinding).
- **Combat** — drafted state (R key), bow + arrow ranged (research-gated,
  accuracy spread), melee, wolf predator + bandit raiders, body-part damage
  with bleed/downed/death, threat-alert UI.
- **Research** — research bench + 5-tech tier tree (dependencies),
  auto-first-tech, progress = sum of pawns' manipulation.
- **Build / deconstruct / designations** — wall (wood/stone), floor (wood +
  stone-floor variant), door + autodoor variant, fence + gate, barricade,
  standing-lamp / torch, table + chair, stove, bed (3 quality tiers with
  rest/mood multipliers); blueprint → haul → frame → build flow;
  **deconstruct** (50% refund), **mine** designation, **grow-zone**
  designation, drag-rect placement, area-cancel; Architect menu with multiple
  categories.
- **Farming / economy** — crops grow + harvest, cooking at stove, eat-from-
  stockpile with meal-quality mood, 5-tier stockpile priority, hauling with
  priority-aware target selection.
- **AI Director / threats** — 3 director modes (Steady / Calm / Chaos),
  threat tier 0–3 → event frequency scaling, ~15 events, raids day-gated.
- **Sound** — `AudioBank` with 11 wired slots: chop / harvest / mine / build /
  cook / shoot / hit / door / UI-click / footstep / wolf-howl, plus
  tier-scaled alert siren, looping ambient bed (day/night variation),
  dynamic music crossfade on threat, rain/weather loop.
- **Day / night + lighting + weather** — `DayNightCycle` (11 colour stops) +
  `NightOverlay` rebuilt (#267) as a **colony-sim-style dynamic lightmap**: night
  darkness is a per-texel texture that lamps *reveal* (lift the darkness so the
  real floor/walls show) with warm candle colour and **line-of-sight occlusion**
  (light is blocked by walls + closed doors, bilinear-softened edges) following
  the colony-sim torch-lamp glow formula — replacing the old additive light-pools
  (which read as a hazy fog). `WeatherController` (storm darkening + rain).
- **UI** — unified bordered-panel system (`MakeBorderedPanel`): top bar
  (clock / speed), **top-left vertical resource readout** (food / meals / wood /
  stone, genre-standard placement — #41), Architect menu, bottom-center gizmo
  command bar, top-right alert/letter stack (clickable → camera pan), tabbed pawn
  inspector (health tab = body HP only, abilities live on the equipment tab —
  #40), settings panel (audio sliders + save/load row), context/float action
  menus (left-click an entity → 벌목/채광/채집 designation), multi-select marquee,
  hover tooltips (Korean), floating combat/work text, hotkey cheat-sheet overlay.
- **Persistence** — JSON save/load (F5/F9) of pawns / trees / resources /
  world state. (Known gap: some entity sub-state — BedQuality /
  StockpilePriority / TreeSpecies / WallMaterial — not yet serialized; see
  [Known gaps](#known-gaps--next).)

## Visual / UX state

The early "style civil war" (two conflicting generators — neon-detailed vs
flat-Kenney) was resolved in #202–208 into a **single `palette.py` source**:
flat colonists with a 2px outline (3 cloth variants), muted terrain, a unified
wood ramp, drop shadows, and one consistent bordered-panel UI language across
every panel. Pawns now animate (facing / walk-bob / idle-breathe / sleep-pose,
all on the sprite child so pathfinding is unaffected). Operator-assessed visual
polish moved from ~3/10 to ~6.5–7/10, clearing the "프로토타입 수준도 안됨"
(below-prototype) bar.

## genre fidelity comparison

Per-dimension closeness to vanilla, from the second-pass audit
([`docs/genre-comparison-v2.md`](docs/genre-comparison-v2.md); v1 in
[`docs/genre-comparison.md`](docs/genre-comparison.md); numeric/balance
fidelity in [`docs/audit-genre-fidelity-2026-05-29.md`](docs/audit-genre-fidelity-2026-05-29.md)):

| Dimension | v1 → v2 | Note |
|---|:--:|---|
| 디자인 / 비주얼 (design/visual) | 70 → **80%** | Style + motion + grounding done; remaining is polish-of-polish. |
| 사운드 (sound) | 30 → **80%** | 11 slots wired; was the quietest dimension, now mostly closed. |
| 림 이동 (movement) | 75 → **82%** | A* + facing shipped; terrain move-cost remains and is **[OP-gated]**. |
| 건축 (building) | 65 → **80%** | deconstruct / mine / grow / drag done; catalogue still thinner than vanilla. |
| 게임플레이 루프 (gameplay loop) | 80 → **85%** | Loops present + verified; mood-model & work-priority are **[OP-gated]**. |
| 메뉴 UX / UI | 70 → **82%** | Alert stack + tabs + multi-select + gizmo bar done. |

> Source caveat: 장르 위키 blocks direct fetch (403); canonical values
> come from WebSearch snippets + the reference sim domain knowledge. The audit's honest
> headline: **the non-gated improvement surface is nearly exhausted** — the
> next leap in player-felt depth is behind the operator-gated tier below.

## How it was built — agent-driven, reproducible from CLI

**Every system is generated or scaffolded by [`game-dev-agent`](../game-dev-agent/)**
via `python skills/game-dev-agent/scripts/agent.py <cmd>`:

- Sprites — `agent.py gen-sprite` (SDXL-Turbo on local ComfyUI) or
  `agent.py gen-sprite-proc` (procedural flat shapes), plus `palette.py` /
  `_gen_*.py` PIL generators for the Kenney-style flat set.
- SFX — `agent.py gen-sfx` (procedural Python WAV).
- Scenes + prefabs — programmatically built via `Assets/Editor/SceneSetup*.cs`
  (14 partial files, ~310L entry after refactor) run in Unity batchmode.
- Build — `agent.py integrate --method build` →
  `BuildScript.BuildWindows` (headless).

No manual Unity Editor work is required to produce the `.exe` — the entire
chain is reproducible from the CLI on any Windows machine with Unity 6 LTS.

The work was driven through the repo's **multi-agent pipeline** —
`game-pm` (plan / backlog) → `game-director` (design) → `game-programmer`
(code) / `game-artist` (assets) → `game-qa` (gate) — and, in the latest
sessions, an autonomous **chain-reaction workflow**: PM publishes
non-conflicting subtasks → parallel makers (Unity not run) → serial Unity
QA + integrator (`refactor_check --fresh-build` → GREEN merges to `main` &
pushes, RED auto-rolls-back + preserves a wip branch + writes a bug report).

## Running it

Pre-built `.exe` (date-stamped — pick the newest):

```
# always resolve the newest dynamically (the folder is date-stamped):
ls -dt builds/day-*/ | head -1      # → builds/day-PLAY-<latest-date>/PawnSim.exe
```

Per-day snapshots live under `builds/day-<day>-<date>/PawnSim.exe` (the folder
is date-stamped, so after midnight the newest is a new dated folder — always
resolve it dynamically, never a hardcoded date).

Rebuild from source (idempotent):

```bash
cd skills/game-prototype

# 1) regenerate scenes + prefabs
python ../game-dev-agent/scripts/agent.py integrate --project unity-project --method scenes

# 2) build the Windows .exe
python ../game-dev-agent/scripts/agent.py integrate --project unity-project --method build --day final
```

Both steps invoke Unity 6000.0.75f1 in batchmode under the hood. Output:
`builds/day-<day>-<date>/PawnSim.exe`.

Useful runtime flags: `-starthour 22` (night demo), `-delay 3 -screenshot
<path>` (capture), `-testmode -batchmode -nographics` (run the PlayMode
scenario suite and emit the JSON report).

## Controls

- **Left-click** select · **left-drag empty ground** marquee multi-select
- **Right-click** ground: move · tree: chop · target: contextual order
- **R** draft/undraft · **Space** pause · **1/2/3** speed
- **B/F/G/T/Y** build modes · **N** research · **F1/F4/F8** tabs · **H** hotkey cheat-sheet
- **F5** save · **F9** load
- Bottom gizmo bar + Architect menu mirror the keyboard commands; idle pawns
  auto-pick the best work via the utility AI.

## Verification approach — honest, automated, gated

The project's north star is **"코드 있음 ≠ 작동 검증됨"** (code existing is not
the same as verified-working). Every commit must pass
`python skills/game-dev-agent/scripts/refactor_check.py --tag NN`, a 6-stage
gate (one cycle ~110s):

1. **scenes regen** — Unity batchmode `SceneSetup.GenerateAll`
2. **build verify** — compile-error scan
3. **QA screenshot** — launch the `.exe`, capture
4. **runtime-error scan** — grep `Player.log` for Exception/NullRef
5. **visual diff** — downsampled baseline compare (5% threshold)
6. **PlayMode tests** — `-testmode` → JSON-asserted scenarios

Three verification layers feed the gate:

- **V-series** (isolated PlayMode scenarios, in `Assets/Scripts/Tests/`) —
  combat / movement / health / resource / AI / time-mood / system /
  persistence assertions (V1…; isolated **76/76**, green 2026-06-03).
- **I-series** (integration scenarios on the real `Game.unity` with spawned
  pawns/trees) — GUI buttons, click round-trips, end-to-end chop / harvest /
  combat, save→load round-trip, 60s stress (**43/43**, green 2026-06-03).
- **Build Click QA** — drives real on-screen button clicks against the built
  `.exe` (9/9; this harness itself was found silently skipping for months due
  to a log-prefix mismatch — fixed in #198, a "the verifier must be verified
  too" lesson).

A failing gate = **no advance**: fix-in-place or roll back to the last-good
build. The chain-reaction workflow automates the RED → `main` rollback.

## Architecture

```
unity-project/Assets/
├── Editor/
│   ├── SceneSetup.cs (+ 14 partial files)   programmatic scene/prefab generation
│   └── BuildScript.cs                       headless build entry points
├── Scripts/
│   ├── Core/      Services.cs (ServiceLocator)
│   ├── Data/      PawnStats.cs, HealthPartsConfig.cs  (SO-externalized tuning)
│   ├── AI/        IPawnAction + PawnActions (utility-AI Strategy pattern)
│   ├── Tests/     V-series PlayMode scenarios
│   └── (~50+ runtime components — PawnEntity, PawnHealth, PathGrid, AStar,
│        ReservationManager, BuildManager, AIDirector, AudioBank, …)
├── Sprites/   palette.py + PIL generators + SDXL/procedural sprites
├── Audio/     procedural WAV SFX (+ _gen_sfx.py)
├── Prefabs/   Pawn / Wall / Floor / Door / Stove / Bench / …
└── Scenes/    MainMenu.unity · Game.unity
```

Architecture-hardening (R-series): `refactor_check.py` harness, SO-externalized
tuning data, utility-AI Strategy pattern, ServiceLocator (5 singletons →
testable lookup), PlayMode auto-test, and `SceneSetup.cs` split 1057L → 310L
across 14 partials.

## Gated / next

The autonomous chain has nearly drained the **non-gated** surface. The next
leap from "looks and sounds like the reference sim" to "*plays* like the reference sim" is
**operator-gated** (behavior-logic changes need explicit OK per the project's
§5 logic-change rule). Specs are drafted and waiting:

- **Mood = thought-sum + 3-tier mental-break** ([`docs/spec-needs-mood-balance.md`](docs/spec-needs-mood-balance.md)) —
  the negative-thought wiring (배고픔/수면 부족/부상 → mood drop, #36) is now in;
  the remaining gated piece is the full positive/negative catalogue balance +
  the 3-tier mental-break behaviour.
- **Work-priority grid** ([`docs/spec-work-priority.md`](docs/spec-work-priority.md)) —
  per-job 1–4 priority grid; directly addresses the operator's hauling-priority
  interest (currently WorkKind is collapsed, so Haul can't be controlled alone).
- **Terrain move-cost** — per-terrain pathing cost (dirt/marsh slower).

### Known gaps (non-gated, tracked)

- Save/load entity sub-state (BedQuality / StockpilePriority / TreeSpecies /
  WallMaterial) not yet serialized — reverts to default on load.
- `carryCapacity` defined but not wired to a hauler carry cap.
- BanditEnemy body-parts (reuse PawnHealth).

## Out of scope (deliberately deferred)

Per the operator's "**기능추가 보수적 — 게임이 되는게 먼저**" (conservative on new
features; make it *play* first) and the v1/v2 over-scope guardrails — do **not**
start these until the core plays well and is verified, and several need
operator direction:

- Roofs + collapse, temperature, joy/recreation, real injury systems/disease,
  cover/accuracy-by-range tables.
- Power grid, trading caravan UI (entity exists), animal taming, stockpile
  filter logic, bills queue — **[OP-gated]**, sequenced last.
- Detailed 32×32 pawn redraw — likely *superseded* by the #202 flat-outline
  direction (detail caused the "potato in a pot" problem); confirm with
  operator before adding detail back.
- 14-skill expansion — the 4-skill consolidation is an intentional
  simplification.

These are explicit scope cuts, not bugs — each could become a follow-on sprint.

## Portfolio narrative

Target: game-company hiring managers + AI-agent / LLM-application engineer
roles. Key talking points:

1. **Agent built (and keeps building) the game** — every asset and script is
   CLI-scaffolded; scenes are programmatically constructed; the whole `.exe`
   chain is reproducible with zero manual Editor work.
2. **AI inside the game** — utility-AI colonist behavior (Strategy pattern) +
   an AI Director for emergent threats.
3. **Honest verification as a first-class concern** — a 6-stage gate on every
   commit, three test layers, auto-rollback on RED, and the "verify the
   verifier" discipline (#198).
4. **Wiki-grounded fidelity** — improvement backlogs derived from a
   per-dimension the reference sim-wiki comparison, with closeness tracked over time.
5. **Multi-agent production pipeline** — PM → director → programmer/artist →
   QA, run as an autonomous chain reaction with file-based handoff.

See [`../../docs/mix-3-design.md`](../../docs/mix-3-design.md) for the strategic
context of why this skill was prioritized.
