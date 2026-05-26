---
name: game-prototype-suika
description: Suika Game-lite 2D physics merge prototype. Built end-to-end via game-dev-agent CLI as the empirical validation of the multi-agent framework's "faster on the next prototype" claim.
license: MIT
compatibility:
  - claude-code
  - agentskills.io
metadata:
  genre: physics-puzzle
  engine: Unity-6000.0.75f1
  framework_used: skills/game-dev-agent
  framework_elapsed_day_1_min: 8
  baseline_elapsed_day_1_min: ~120
  speedup: ~15x
---

# game-prototype-suika — Suika lite

Click → fruit drops at cursor X → physics carries it down → same-tier
collision merges into next-tier fruit + awards score → game over when
any fruit stays above the red line for 1.5 s.

Built as the **second prototype** in the Skill #3 series.  Whole
purpose is to empirically validate the multi-agent framework
(`skills/game-dev-agent/`) — the operator's hypothesis was *"the
NEXT prototype after RimWorld-lite should be much faster because the
framework now exists."*

## Day 1 — empirical baseline measurement

| Phase | Tool used | Wall-clock |
|---|---|---|
| Sprite gen (5 fruits + wall + line) | `gen_fruits.py` (PIL) | ~30 s |
| C# scaffolding (9 scripts) | `agent.py code` | ~10 s |
| Gameplay logic fill-in | direct Write | ~3 min |
| Editor scene/build scripts | direct Write | ~2 min |
| Scene generation | `agent.py integrate --method ...SuikaSceneSetup.GenerateAll` | ~30 s |
| Build .exe | `agent.py integrate --method ...SuikaBuildScript.BuildVerify` | ~25 s |
| Self-verify screenshot | `agent.py qa --exe ... --screenshot ...` | ~10 s |
| Visual debug iteration (skybox / wall sizing) | direct Edit + 1 re-build | ~2 min |
| **Total Day-1** | | **~8 min** |

Baseline (Skill #3-A RimWorld-lite Day 1, without framework) was
**~2 h** including the SDXL→Kenney asset pivot loop.  Speedup ≈
**15×** for "click-to-screenshot" loop.

The bulk of saved time:
1. `qa.py` removed the "operator must manually launch + screenshot"
   step (saved ~5 min per build iteration).
2. `agent.py code` removed boilerplate-typing-of-MonoBehaviours
   (saved ~20 sec per script × 9 = ~3 min).
3. `integrator.py` removed bespoke Unity batchmode incantation
   per call (saved ~30 sec per call × ~3 calls = ~1.5 min).
4. `gen_fruits.py` removed SDXL→reject→Kenney-pivot loop (saved
   the entire ~30 min asset-quality debug from PawnSim).

## What ships on Day 1

- 1 Game scene (no MainMenu yet; Suika has no real menu)
- 5 fruit tier prefabs (Cherry / Orange / Lemon / Melon / Watermelon)
- Walled-box arena with floor + 2 walls
- Click-to-drop spawn at cursor X (drops tier 1-3 randomly)
- Same-tier merge → next-tier promotion + score
- Score UI (top-right) + hint UI (top-left)
- GameOver detector + GameOverPanel
- AutoScreenshotter for qa.py self-verify

## Roadmap (planner.py estimate: 2 days)

- [x] **Day 1** — click-to-drop + physics + merge + score + game-over
  (8 min via framework)
- [x] **Day 2** — NEXT-tier preview UI + drop cursor indicator + score
  popup + AudioBank (drop/merge/gameover SFX) + 4 merge-related bugs
  caught + fixed via qa.py iteration loop (9 min wall-clock total).
- [ ] **Day 3** (optional) — score juice particles + merge VFX +
  start screen + high-score persistence

## Day 2 — additional bugs caught + framework lessons

1. **OnCollisionEnter2D miss post-grace** — fruits in `justSpawned`
   grace at first contact never get a second Enter event once they're
   settled.  Switched to `OnCollisionEnter2D + OnCollisionStay2D`
   double-handler.
2. **Pre-spawned prefab `justSpawned=true` baked in** — pre-spawned
   editor instances inherit the field's default-true.  Set to false
   via `SerializedObject.FindProperty("justSpawned").boolValue = false`
   on each pre-spawn so they're immediately mergeable.
3. **GetInstanceID() tiebreaker bug** — comparing `GetInstanceID()`
   (component ID) vs `collision.gameObject.GetInstanceID()` (GO ID)
   is meaningless: components are created AFTER GameObjects, so the
   component ID is always larger.  Both sides bail.  Must compare
   `gameObject.GetInstanceID()` on both sides.
4. **ScoreUI Awake/OnEnable race** — UI subscribing to
   `ScoreManager.OnScoreChanged` in OnEnable hit a race where the
   manager's Awake hadn't yet run.  Switched to poll-via-Update.

Each of those 4 issues was surfaced by `agent.py qa` reading the
screenshot back: "Score still 0 after merge" → debug.  Without the
auto-screenshot harness the human-loop would have been 5× slower.

## Build + run

```
python skills/game-dev-agent/scripts/agent.py integrate \
  --project skills/game-prototype-suika/unity-project \
  --method MelonS.GameProto.EditorTools.SuikaSceneSetup.GenerateAll

python skills/game-dev-agent/scripts/agent.py integrate \
  --project skills/game-prototype-suika/unity-project \
  --method MelonS.GameProto.EditorTools.SuikaBuildScript.BuildVerify

python skills/game-dev-agent/scripts/agent.py qa \
  --exe G:/ai/MelonS-Agents/skills/game-prototype-suika/builds/verify/SuikaLite.exe \
  --screenshot G:/ai/_suika_verify.png --delay 4
```

## Attribution

All fruit sprites + wall + drop-line are procedurally generated by
`scripts/gen_fruits.py` (PIL).  Public domain.  No external assets
used.

## License

MIT — see repo root.
