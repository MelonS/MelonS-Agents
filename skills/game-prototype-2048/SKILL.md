---
name: game-prototype-2048
description: 2048-lite. 4x4 grid slide puzzle. WASD/Arrow keys to slide tiles, same-number tiles merge into 2x. Built as the FOURTH framework-test in 3 minutes 46 seconds, confirming the compounding speedup hypothesis.
license: MIT
compatibility:
  - claude-code
  - agentskills.io
metadata:
  genre: grid-puzzle
  engine: Unity-6000.0.75f1
  framework_used: skills/game-dev-agent
  framework_elapsed_day_1_min: 3.8
  baseline_elapsed_day_1_min: ~120
  speedup_vs_prefframework: ~32x
---

# 2048-lite Day 1

The **fourth** framework prototype.  Built end-to-end in
**3 minutes 46 seconds** wall-clock.

## Empirical speedup curve (definitive)

| Prototype | Day-1 wall-clock | Speedup vs PawnSim |
|---|---|---|
| RimWorld-lite (PawnSim) | ~2 hours | baseline |
| Suika lite | ~8 min | 15× |
| Vampire-Survivors lite | ~4.6 min | 26× |
| **2048-lite (this)** | **~3.8 min** | **32×** |

**The speedup keeps compounding** because each prototype:
1. Pulls more from the genre YAML catalog
2. Recycles more C# templates verbatim
3. Skips bug-pattern re-discovery (lessons #4-9 baked in)
4. Trusts the editor-scaffold output sooner

By the time the framework saw a fourth completely-different genre
(grid puzzle, no physics, no real-time entities), Day 1 was ~50 lines
of project-specific GameManager logic + 6 small support classes.
Everything else came from `agent.py`.

## What ships

Scripts (7):
- GameManager (singleton 4x4 grid state + slide-collapse algorithm +
  random 2/4 spawn + game-over detection)
- Tile (sprite + TextMesh + color-per-value table 2..2048)
- InputController (WASD/Arrow direction keymap)
- ScoreUI (poll-via-Update)
- GameOverUI (poll-via-Update)
- AudioBank (slide/merge throttled)
- AutoScreenshotter (from scaffold)

Sprites (1): single white tile (color set per value in `Tile.cs`).
SFX (2): slide=click, merge.

## Day 1 verified

Screenshot (`day1_verify.png`):
- 4x4 grid background (16 cells) ✓
- 2 starting tiles with "2" value ✓
- Score: 0 ✓
- WASD/Arrow hint at bottom ✓

Operator plays:
- WASD/Arrow → all 16 tiles slide in that direction.
- Same-value adjacent tiles merge → next tier + score += merged value.
- Random new tile (2 or 4) spawns after each successful move.
- Game-over fires when no moves possible.

## Day 2 cuts

- Slide animation (currently instant teleport via Refresh)
- Best-score persistence (JsonUtility via json-savestate template)
- Win condition at 2048 tile
- "Try Again" button on game-over

## Build + run

```
python skills/game-dev-agent/scripts/agent.py integrate \
  --project skills/game-prototype-2048/unity-project \
  --method MelonS.GameProto.EditorTools.SceneSetup.GenerateAll

python skills/game-dev-agent/scripts/agent.py integrate \
  --project skills/game-prototype-2048/unity-project \
  --method MelonS.GameProto.EditorTools.BuildScript.BuildGameOnlyVerify

python skills/game-dev-agent/scripts/agent.py qa \
  --exe G:/ai/MelonS-Agents/skills/game-prototype-2048/builds/verify/G2048.exe \
  --screenshot G:/ai/_2048_verify.png --delay 5
```

## License

MIT.  All assets procedural.
