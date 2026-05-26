# game-prototype — RimWorld-lite vertical slice (Skill #3-A)

7-day Unity 6 LTS vertical slice built **using** the
[`game-dev-agent`](../game-dev-agent/) AI assistant.  Portfolio piece
demonstrating game developer + AI agent engineer T-shape skill profile.

## What it is

A small top-down colony sim where 3 colonists autonomously chop trees,
guided by a utility AI, while an in-game AI Director generates random
narrative events.  Save/Load + procedural audio supported.

| | |
|---|---|
| Engine | Unity 6000.0.75f1 LTS |
| Build target | Windows x64 (Standalone, IL2CPP capable) |
| Scope | locked at start of Day 1 (RimWorld-lite vertical slice — NOT a clone) |
| Build size | 83 MB |
| Source LoC | ~900 lines C# |
| Asset count | 3 sprites (agent-generated), 2 SFX (procedural Python WAV), 2 tile/prefab |
| End-state | working .exe, playable, save/load, audio, AI events |

## How it was built

**Every system was generated or scaffolded by `game-dev-agent`**:
- Sprites: `python skills/game-dev-agent/scripts/agent.py gen-sprite "..."` →
  SDXL-Turbo on local ComfyUI → drop into Assets/Sprites/
- Scenes + prefabs: programmatically constructed via
  `Assets/Editor/SceneSetup.cs` (Unity batchmode CLI invocation)
- Build: `Unity.exe -batchmode -executeMethod ...BuildScript.BuildWindows`

No manual Unity Editor work was required to produce the .exe — entire
chain is reproducible from CLI.

## End-of-day deliverables

| Day | What the .exe shows | New systems |
|---|---|---|
| 1 | Title → Start → game scene with grass tilemap + 1 selectable pawn | scenes, selection, build chain |
| 2 | Pawn moves on right-click, needs UI bars tick down | PawnMovement, PawnNeeds, PawnInfoPanel |
| 3 | 8 trees, right-click tree → pawn walks + chops, wood counter rises | TreeEntity, PawnChopper, ResourceManager, ResourceCounterUI |
| 4 | 3 pawns auto-chop trees (utility AI), counter rises without player input | PawnUtilityAI, multi-pawn spawn |
| 5 | AI Director fires events every 15-30s, event log displays last 4 | AIDirector + 8-event pool, EventLogUI |
| 6 | F5/F9 save/load, restore pawn positions + needs + trees + resources | SaveLoadManager (JSON), GameSaveButtons, AudioBank skeleton, procedural SFX |
| 7 | Audio wired (chop + select SFX), portfolio docs, demo recording | AudioClip injection, portfolio narrative |

Each day's build snapshot at `builds/day-X-2026-05-26/PawnSim.exe`.

## Running it

Pre-built `.exe`:
```
builds/day-7-2026-05-26/PawnSim.exe
```

Or rebuild from source:
```bash
# 1. Generate scenes + prefabs (idempotent, safe to re-run)
"G:/tools/UnityEditors/6000.0.75f1/Editor/Unity.exe" -batchmode -nographics -quit \
  -projectPath unity-project \
  -executeMethod MelonS.GameProto.EditorTools.SceneSetup.GenerateAll

# 2. Build Windows .exe
MELONS_BUILD_DAY=7 "G:/tools/UnityEditors/6000.0.75f1/Editor/Unity.exe" -batchmode -nographics -quit \
  -projectPath unity-project \
  -executeMethod MelonS.GameProto.EditorTools.BuildScript.BuildWindows
```

Output: `builds/day-7-<date>/PawnSim.exe`.

## Controls

- **Left-click pawn**: select (yellow tint)
- **Right-click ground**: move selected pawn
- **Right-click tree**: pawn walks + chops tree (wood +5 on destroy)
- **F5**: save
- **F9**: load
- (idle pawns auto-pick nearest tree to chop, override with right-click)

## Architecture

```
Assets/
├── Scripts/
│   ├── MainMenuController.cs    (Day 1)
│   ├── GameManager.cs           (Day 1 → multi-spawn Day 4)
│   ├── PawnEntity.cs            (Day 1)
│   ├── ClickSelector.cs         (Day 1 → right-click chop Day 3)
│   ├── PawnMovement.cs          (Day 2)
│   ├── PawnNeeds.cs             (Day 2)
│   ├── PawnInfoPanel.cs         (Day 2)
│   ├── TreeEntity.cs            (Day 3)
│   ├── PawnChopper.cs           (Day 3)
│   ├── ResourceManager.cs       (Day 3)
│   ├── ResourceCounterUI.cs     (Day 3)
│   ├── PawnUtilityAI.cs         (Day 4)
│   ├── AIDirector.cs            (Day 5)
│   ├── EventLogUI.cs            (Day 5)
│   ├── SaveLoadManager.cs       (Day 6)
│   ├── GameSaveButtons.cs       (Day 6)
│   └── AudioBank.cs             (Day 6 / wired Day 7)
├── Editor/
│   ├── SceneSetup.cs            (programmatic scene generation)
│   └── BuildScript.cs           (headless build)
├── Sprites/   pawn_colonist.png · tile_grass.png · tree.png
├── Tiles/     Grass.asset
├── Audio/     chop.wav · select.wav  (+ _gen_sfx.py to regenerate)
├── Prefabs/   Pawn.prefab
└── Scenes/    MainMenu.unity · Game.unity
```

## Out of scope (deliberately deferred)

What this prototype does **not** do (to honor 7-day timeline):
- Combat (raids, weapons, health damage)
- Crafting / production chains
- Multi-room building / floor / walls
- Research tree
- Mood / relationships / personality simulation
- Pathfinding (uses straight-line MoveTowards, no obstacle avoidance)
- Multiple biomes / seasons / weather effects
- LLM-driven runtime AI Director (currently static pool; runtime LLM = Day 8+)
- Sound: BGM (only SFX shipped; BGM hook present but no clip)
- Optimized rendering / batched draws (3 pawns is trivial; scales pending)

These are not bugs — they're explicit scope cuts to preserve the
"7-day shippable" promise.  Each could be added as a follow-on
sprint if portfolio narrative demands.

## Portfolio narrative

Target audience: game company hiring managers + general-tech hiring
managers for AI agent / LLM-application engineer roles.

Key talking points:
1. **7-day end-to-end delivery** — playable .exe on Day 1, refined daily
2. **AI agent built the game** — every asset generated by SDXL-Turbo,
   every C# script scaffolded by the dev-time agent, scenes programmatically
   constructed (zero Unity Editor manual work for the build chain)
3. **AI inside the game** — utility AI for colonist behavior + AI
   Director for emergent narrative
4. **Reproducible** — 2 CLI commands rebuild the entire project from
   scratch on any Windows machine with Unity 6 LTS installed
5. **T-shape proof** — operator's 10-year game dev expertise + new
   AI agent engineering skill, demonstrated in same artifact

See [`../../docs/mix-3-design.md`](../../docs/mix-3-design.md) and
[`../../docs/daily/2026-05-26-windows-overnight.md`](../../docs/daily/2026-05-26-windows-overnight.md)
for the strategic context of why this skill was prioritized over
continued music-channel content work.
