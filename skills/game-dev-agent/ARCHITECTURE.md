# game-dev-agent — multi-agent architecture

Mirrors Skill #1 (music-video) orchestrator + subagent pattern.  Each
module = subagent with a single responsibility.  Agent dispatches to
modules via CLI subcommands.  Pipeline composes them for end-to-end
NL→playable-prototype flow.

## Module roster

```
scripts/
├── agent.py                       # orchestrator — CLI entry, dispatches subcommands
└── modules/
    ├── __init__.py
    ├── planner.py                 # subagent — NL game spec → task list + system breakdown
    ├── resourcer/                 # subagent group — all asset acquisition
    │   ├── __init__.py
    │   ├── asset_gen.py           # SDXL-Turbo sprite generation via ComfyUI (existing)
    │   └── asset_fetch.py         # Kenney CC0 / OpenGameArt licensed pack download
    ├── coder.py                   # subagent — C# code scaffolding (Unity-aware patterns)
    ├── integrator.py              # subagent — Unity batchmode invocations (scene gen, build)
    └── qa.py                      # subagent — build + AutoScreenshotter + screenshot verify
```

## Subagent contracts

### planner

**Input**: natural-language game spec  
**Output**: structured task list (JSON or Markdown checklist)  
**Pattern**: "Build a colony-sim" → enumerated subtasks:
  1. Generate sprites: pawn, 3 resource types, tile set
  2. Code: PawnMovement, WorkScheduler, ResourceSpawner, ColonyManager
  3. Scene: tilemap map, spawn points, UI overlay
  4. Balance: work speed / resource yield per tick
  5. Audio: BGM loop, chop SFX, build SFX

Day 1 implementation: stub returning hard-coded template per known
genre (colony-sim, colony-sim-lite, etc.).  Future: invoke
Claude API to decompose arbitrary specs.

### resourcer

**Input**: asset spec (sprite description, license requirements,
quantity)  
**Output**: files placed in `Assets/Sprites/` (or Audio, etc.)  
**Tools**:
- `asset_gen.py` — SDXL-Turbo generation (operator-built, **last
  resort** since results are poor quality vs. licensed packs)
- `asset_fetch.py` — Kenney + OpenGameArt + similar CC0/CC-BY sources;
  license enforcement (rejects non-commercial-restricted), attribution
  recording in `ATTRIBUTIONS.md`

Per [OPQ-001] resolution: Kenney CC0 is **primary** source.  Asset
gen is fallback only when no licensed pack covers a specific need.

### coder

**Input**: C# script spec (MonoBehaviour class name, fields, behavior
description, Unity 6 API constraints)  
**Output**: `.cs` file at `Assets/Scripts/<Name>.cs`  
**Pattern**: Unity-aware templates with proper:
- `[SerializeField]` + Inspector references
- MonoBehaviour lifecycle hooks
- Persistent button listeners for UI (avoiding runtime-only Awake
  wiring footguns)
- Proper sorting orders for 2D sprites
- Modern API usage (Unity 6: FindObjectsByType not deprecated
  FindObjectsOfType, InputSystemUIInputModule when new input
  enabled, etc.)

Day 1 implementation: template library (look-up by class type).
Future: Claude API wrapping per [OPQ-002].

### integrator

**Input**: project path, method name to invoke  
**Output**: Unity batchmode invocation result  
**Tools**:
- Scene generation (calls `SceneSetup.GenerateAll`)
- Build pipeline (calls `BuildScript.BuildWindows` /
  `BuildScript.BuildGameOnlyVerify`)
- Log parsing (extracts compile errors, scene-gen errors, build
  status from Unity log)

### qa

**Input**: built `.exe` path  
**Output**: pass/fail + screenshot + diagnostic notes  
**Process**:
1. Launch .exe with `-delay N -screenshot path/to.png` flags
2. Wait for self-quit (via AutoScreenshotter)
3. Read screenshot via image-capable tool
4. Verify content (presence of expected sprites, no error states,
   UI rendering, etc.)

Currently: agent uses `Read` tool to view PNG.  Future: vision-API
content checks (count pawns, detect "null sprite" white squares,
detect text-rendering glitches).

## End-to-end flow

```
operator: "build a colony-sim"
  │
  ├─> agent.py plan "build a colony-sim"
  │     └─> planner.py → task list (5 systems, 8 sprites, 3 audio)
  │
  ├─> agent.py resource --from-plan plan.json
  │     └─> resourcer/asset_fetch.py → Kenney/OpenGameArt sprites
  │       OR resourcer/asset_gen.py → SDXL (fallback)
  │
  ├─> agent.py code --from-plan plan.json
  │     └─> coder.py → C# scripts for each system
  │
  ├─> agent.py integrate
  │     └─> integrator.py → Unity batchmode → scene + build
  │
  └─> agent.py qa
        └─> qa.py → launch .exe → screenshot → verify → report
```

## Why this architecture

1. **Reuses Skill #1 / #2 pattern**: same agentskills.io shape, same
   module-as-subagent decomposition.  Operator can grok any skill in
   the repo with one mental model.
2. **Composable**: each subagent stands alone (can be invoked
   directly via `agent.py <subcommand>`) AND composes via plan-driven
   pipeline.
3. **Failure-isolated**: if one subagent fails (e.g., resourcer can't
   reach Kenney), others still function.
4. **Test-friendly**: each subagent has clear input/output contract,
   makes unit testing tractable.
5. **Operator-extensible**: operator can add new subagents (e.g.,
   `balance.py` for ScriptableObject tuning) without touching the
   orchestrator.

## Day-1 implementation scope

Tonight's autonomous build:
- ✓ `asset_gen` (already shipped)
- ✓ `asset_fetch` (Kenney downloader, this commit)
- ✓ `integrator` (Unity batchmode wrapper, this commit)
- ✓ `qa` (build + screenshot + read PNG, this commit)
- ◇ `planner` (stub returning hard-coded colony-sim-lite plan for now)
- ◇ `coder` (template library for known script types; Claude API
  wrapper gated behind ANTHROPIC_API_KEY env var per [OPQ-002])

After this commit, the next prototype should be measurably faster to
scaffold because resourcer + integrator + qa are reusable.
