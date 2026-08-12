---
name: game-dev-agent
description: AI agent that assists with Unity game development — sprite generation, code scaffolding, balance tuning, audio generation, in-game AI Director. Built to demonstrate AI agent engineer + game developer T-shape skill profile.
license: MIT
compatibility:
  - agentskills.io
metadata:
  version: 0.1.0
  status: in-development
  pipeline-source: standalone
  domain: game-development
  unity-version: 2022.3.32f1
allowed-tools:
  - Read
  - Write
  - Edit
  - Bash
  - Glob
  - Grep
---

# game-dev-agent

AI agent that orchestrates Unity game development tasks via CLI. Companion
to [`pawnsim`](../../games/pawnsim/) (the actual game built using this
agent).

## What it does

Modules (each = subagent):

- **`asset_gen`** — Generate sprites / tiles / UI icons via ComfyUI + SDXL,
  auto-save to Unity Assets folder with proper import settings
- **`code_assist`** — Unity-aware C# scaffolding: MonoBehaviour / ScriptableObject /
  Editor scripts. Knows Unity 2022.3 API.
- **`balance_tune`** — Read ScriptableObject configs + JSON balance files,
  propose parameter tuning. Game-dev specific (beyond what general AI
  code assistants do).
- **`audio_gen`** — BGM via Suno (operator-provided files), SFX via local
  AudioCraft / freesound API. Auto-conform to Unity AudioClip import.
- **`runtime_director`** (in-game AI) — LLM-driven event generation for
  the game itself, demonstrating dev-time + runtime AI agent patterns.

## Why this skill matters

**Career positioning**: combines deep game-dev domain (operator's
existing strength) with AI agent engineering (rare, emerging hire-niche).
Portfolio = (this agent) + (game built with it) = T-shape proof.

**Operator-specific**:
- Operator has 10+ year game dev background
- AI agent engineer roles emerging in game industry + general tech
- Game industry downturn — pure game-dev roles competitive
- AI agent angle = downturn-resilient niche

## Usage (planned, Day 1)

```bash
# Generate a sprite
python skills/game-dev-agent/scripts/agent.py gen-sprite \
    "evil knight enemy, 2D pixel art, top-down view, simple silhouette" \
    --output Assets/Sprites/enemy_knight.png

# Scaffold Unity MonoBehaviour script
python skills/game-dev-agent/scripts/agent.py code \
    "PlayerMovement: top-down 2D, WASD input, 5 m/s speed" \
    --output Assets/Scripts/PlayerMovement.cs

# Analyze + propose balance changes
python skills/game-dev-agent/scripts/agent.py balance \
    --config Assets/Configs/enemies.json \
    --goal "make wave 5 challenging but beatable"

# Generate BGM via Suno (operator provides Suno prompt template)
python skills/game-dev-agent/scripts/agent.py audio \
    --style "lofi dungeon synth, slow, atmospheric" \
    --output Assets/Audio/BGM_dungeon.mp3
```

## Architecture

Same pattern as Skill #1 (music-video) + Skill #2 (job-hunt):

```
skills/game-dev-agent/
├── SKILL.md (this file)
├── README.md
└── scripts/
    ├── agent.py                  # CLI entry — argparse subcommands
    ├── modules/
    │   ├── __init__.py
    │   ├── asset_gen.py
    │   ├── code_assist.py
    │   ├── balance_tune.py
    │   ├── audio_gen.py
    │   └── runtime_director.py   # Used by game prototype at runtime
    └── prompts/
        ├── unity_csharp_system.md
        ├── unity_monobehaviour_template.md
        ├── sprite_2d_pixel.md
        └── balance_analyze.md
```

## Development phases

Per [`docs/skill-3-design.md`](../../docs/skill-3-design.md):

- **Day 1**: agent.py orchestrator + asset_gen MVP + initial sprite
- **Day 2**: code_assist (Unity C# scaffold)
- **Day 3**: balance_tune
- **Day 4**: utility AI integration (used by game prototype)
- **Day 5**: runtime_director (AI Director — LLM events)
- **Day 6**: audio_gen + polish
- **Day 7**: portfolio docs + demo video + commit

Each end-of-day = build runnable (game prototype side).
