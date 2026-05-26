---
name: game-prototype-vs-lite
description: Vampire-Survivors-lite. WASD survivor with auto-aim, escalating-rate enemy waves, projectile damage, XP gem pickup. Built as the THIRD framework-test, validating the "each new prototype is faster than the last" hypothesis.
license: MIT
compatibility:
  - claude-code
  - agentskills.io
metadata:
  genre: action-survivor
  engine: Unity-6000.0.75f1
  framework_used: skills/game-dev-agent
  framework_elapsed_day_1_min: 4.6
  baseline_elapsed_day_1_min: ~120
  speedup_vs_prefframework: ~26x
---

# Vampire-Survivors-lite — Day 1

The third framework validation prototype.  Built end-to-end in
**4 minutes 37 seconds** wall-clock using `agent.py` exclusively.

## Empirical speedup curve

| Prototype | Day-1 wall-clock | Notes |
|---|---|---|
| Skill #3-A (RimWorld-lite) | ~2 hours | Pre-framework, includes SDXL→Kenney pivot |
| Skill #3-B (Suika lite) | ~8 min | 1st framework use, ~15× speedup |
| Skill #3-C (VS lite — this) | **~4.6 min** | 2nd framework use, ~26× speedup |

**The speedup is compounding.**  Each new prototype is faster than
the last because:
1. Genre YAML already defines `scripts:` list (`agent.py plan`)
2. C# template catalog covers 80% of patterns
3. `agent.py gen-sprite-proc` + `gen-sfx` generate assets in seconds
4. `agent.py gen-editor-scaffold` produces SceneSetup + BuildScript
   + AutoScreenshotter (with lesson #9 runInBackground fix baked in)
5. Bug patterns (#4-9) absorbed into templates — re-discovery cost = 0

## Day 1 — what ships

11 C# scripts:
- PlayerMovement (WASD, Rigidbody2D kinematic)
- PlayerHealth (HP 100, iframes 0.6s, hit flash)
- AutoShooter (find nearest enemy, fire projectile 0.45s interval)
- Projectile (move, lifetime 2s, contact damage 3, destroys self)
- EnemyEntity (HP, contact damage, hit flash, drops XP gem on death)
- EnemyAI (move toward player at speed 2)
- WaveSpawner (escalating 0.6→ rate, ring-spawn radius 9 around player)
- XPGem (magnet within 2.5, pickup adds XP)
- XPManager (singleton XP store)
- HUD (HP / XP display, poll-via-Update)
- AudioBank (shoot / hit / pickup / levelup SFX with throttling)
- CameraFollowPlayer (smooth follow with SmoothDamp)

Visual verified: HP:100 top-left, XP:0 top-right, blue circle player
in middle, yellow projectile, 3 cyan XP gems already dropped by killed
enemies, control hint at bottom.

## Day 2 cuts

- LevelUpUI (offered weapon choices after N XP)
- WeaponUpgrade (player picks one of 3 upgrades)
- Multiple weapon types (current = single projectile)
- Visual death anim
- BGM track

## Build + run

```
python skills/game-dev-agent/scripts/agent.py integrate \
  --project skills/game-prototype-vs-lite/unity-project \
  --method MelonS.GameProto.EditorTools.SceneSetup.GenerateAll

python skills/game-dev-agent/scripts/agent.py integrate \
  --project skills/game-prototype-vs-lite/unity-project \
  --method MelonS.GameProto.EditorTools.BuildScript.BuildGameOnlyVerify

python skills/game-dev-agent/scripts/agent.py qa \
  --exe G:/ai/MelonS-Agents/skills/game-prototype-vs-lite/builds/verify/VSLite.exe \
  --screenshot G:/ai/_vs_verify.png --delay 8
```

## License

MIT.  All assets procedural (sprite_proc + sfx_proc), public-domain.
