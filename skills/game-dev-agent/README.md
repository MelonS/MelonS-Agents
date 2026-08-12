# game-dev-agent — Unity 게임 개발 AI agent (Skill #3-C)

Companion to [`pawnsim`](../../games/pawnsim/) (the demo game built
using this agent; moved out of `skills/` on 2026-08-12 to keep its own
Unity ProjectSettings separate from other games' — see
[`genres/naval-sail-prototype.yaml`](genres/naval-sail-prototype.yaml)).  CLI orchestrator that wraps SDXL-Turbo (asset gen),
Unity C# scaffolding patterns, balance tuning, and audio gen into a
single agent surface.

> **What this skill *demonstrates***: AI agent engineering applied to
> the domain operator knows deepest (game dev).  Pattern matches Skill
> #1 (music-video) and Skill #2 (job-hunt) — same agentskills.io
> compliant shape, same module-as-subagent decomposition.

## Modules (Day-by-Day rollout)

| Module | Day shipped | Status |
|---|---|---|
| `asset_gen` (SDXL-Turbo sprite generation) | Day 1 | ✓ live, used by pawnsim |
| `code_assist` (Unity C# scaffolding) | Day 2 | shipped indirectly (scripts written via Claude in-session, codified as templates in `prompts/`) |
| `balance_tune` (ScriptableObject + JSON config tuning) | Day 3 | placeholder |
| `runtime_director` (in-game AI events) | Day 5 | partial — static event pool shipped in game; LLM pool gen pending |
| `audio_gen` (Suno BGM + procedural SFX) | Day 6 | partial — procedural SFX live (`Audio/_gen_sfx.py`); Suno hook pending |

## Usage

CLI is the primary surface:

```bash
# Generate a sprite (ComfyUI must be running at $COMFYUI_URL, default localhost:8188)
python scripts/agent.py gen-sprite \
    "humanoid colonist in simple clothes, top-down view" \
    --style pixel-art \
    --output Assets/Sprites/pawn.png \
    --width 256 --height 256 --seed 1001

# Future:
python scripts/agent.py code "PlayerMovement: top-down 2D WASD" --output Assets/Scripts/PlayerMovement.cs
python scripts/agent.py balance --config Assets/Configs/enemies.json --goal "wave 5 challenging but beatable"
python scripts/agent.py audio --style "lofi dungeon" --output Assets/Audio/bgm.mp3
```

### Style presets for `gen-sprite`

| Preset | Best for | Sampler steps | CFG |
|---|---|---|---|
| `pixel-art` | sprites, tiles, simple icons | 6 | 1.5 |
| `stylized-2d` | hand-drawn looking game art | 6 | 1.5 |
| `icon` | UI icons, flat design | 4 | 1.2 |
| `raw` | bypass all preset prompt scaffolding | 6 | 1.5 |

Each preset bakes in:
- **AI hands avoid**: "hand, fingers" in negative prompt (per
  [[ai-hands-avoid]] operator memory)
- **AI text avoid**: "text, watermark" in negative prompt (per
  [[ai-text-avoid]] operator memory)
- **Game-appropriate**: "transparent background, single subject centered"

## Pattern matches Skills #1 and #2

```
skills/<name>/
├── SKILL.md          (agentskills.io frontmatter)
├── README.md         (this file)
└── scripts/
    ├── agent.py      (CLI entry — argparse subcommands)
    └── modules/      (each module = subagent)
        ├── __init__.py
        ├── asset_gen.py
        ├── code_assist.py    (planned)
        ├── balance_tune.py   (planned)
        ├── audio_gen.py      (planned)
        └── runtime_director.py (planned)
```

The CLI dispatches to subagents the same way Skill #1's `orchestrator`
dispatches to `planner` / `resourcer` / `editor` / `qa`, and Skill #2's
`job-hunt` dispatches to `kr-wanted` / `global-ats` / etc.

## Verified working (2026-05-26)

| Operation | Result | Wall-clock |
|---|---|---|
| Sprite generation (256×256 pixel-art) | ✓ 90 KB PNG | 2-8 sec |
| Sprite generation (128×128 tile/icon) | ✓ 24-28 KB PNG | 2 sec |
| Game prototype Day 1-7 build chain | ✓ 83 MB Windows .exe | scene-gen 90s + build 30s |

## Why this skill exists

Operator pivoted on 2026-05-26 from "music YouTube channel content"
toward AI agent engineer / game-dev career path:

- Game industry hiring 1-4 day AI clone challenges actively
- AI agent engineer roles emerging (rare T-shape niche)
- Operator background: 10 yr game dev + (now) AI agent builder = exact fit
- Steam release possible long-term (operator's passion: a lite Age-of-Sail naval trade sim)

This skill is the agent half; [`pawnsim`](../../games/pawnsim/) is
the demonstration that the agent works.

## Next iterations

Priority order:
1. **`runtime_director`** — LLM-generated event pool (offline gen into
   Resources/events.json, loaded by `AIDirector` at game start).  Day 8.
2. **`code_assist`** — extract C# scaffolding patterns used in Day 1-6
   into reusable prompts/templates.  Day 9-10.
3. **`balance_tune`** — JSON balance config analyzer.  Day 11-12.
4. **`audio_gen`** — Suno API wrapper for BGM.  Day 13.

After these, the agent is portable to **other game projects** (e.g.,
the operator's eventual naval-sail Steam project — see
[`naval-sail-prototype.yaml`](genres/naval-sail-prototype.yaml)).
