#!/usr/bin/env python3
"""game-dev-agent — CLI orchestrator for Unity game development assistance.

Subcommands map to module/<name>.py. Pattern matches Skill #1 (music-video)
+ Skill #2 (job-hunt) — same agentic decomposition.

Cross-platform Python.  Reuses existing ComfyUI at COMFYUI_URL.

Usage:
  agent.py gen-sprite "evil knight enemy, top-down" --output Assets/Sprites/enemy.png
  agent.py code "PlayerMovement: top-down 2D WASD" --output Assets/Scripts/PlayerMovement.cs
  agent.py balance --config Assets/Configs/enemies.json --goal "tune wave 5"
  agent.py audio --style "lofi dungeon" --output Assets/Audio/bgm.mp3
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))

from modules import asset_gen  # noqa: E402


def cmd_gen_sprite(args):
    return asset_gen.generate_sprite(
        prompt=args.prompt,
        output=Path(args.output),
        width=args.width,
        height=args.height,
        seed=args.seed,
        style=args.style,
        server=args.server,
    )


def cmd_code(args):
    print("[code] not yet implemented (Day 2)", file=sys.stderr)
    return 99


def cmd_balance(args):
    print("[balance] not yet implemented (Day 3)", file=sys.stderr)
    return 99


def cmd_audio(args):
    print("[audio] not yet implemented (Day 6)", file=sys.stderr)
    return 99


def main():
    p = argparse.ArgumentParser(description="game-dev-agent CLI")
    sub = p.add_subparsers(dest="cmd", required=True)

    # gen-sprite
    g = sub.add_parser("gen-sprite", help="generate a 2D sprite via SDXL")
    g.add_argument("prompt", help="sprite description (e.g. 'evil knight, top-down')")
    g.add_argument("--output", required=True, help="output .png path")
    g.add_argument("--width", type=int, default=512)
    g.add_argument("--height", type=int, default=512)
    g.add_argument("--seed", type=int, default=-1)
    g.add_argument("--style", default="pixel-art",
                   choices=["pixel-art", "stylized-2d", "icon", "raw"],
                   help="prompt style preset")
    g.add_argument("--server", default="http://127.0.0.1:8188")
    g.set_defaults(func=cmd_gen_sprite)

    # code
    c = sub.add_parser("code", help="scaffold Unity C# script")
    c.add_argument("description", help="what the script should do")
    c.add_argument("--output", required=True, help=".cs output path")
    c.set_defaults(func=cmd_code)

    # balance
    b = sub.add_parser("balance", help="analyze + tune game balance config")
    b.add_argument("--config", required=True, help="JSON/ScriptableObject path")
    b.add_argument("--goal", required=True, help="balance goal in natural language")
    b.set_defaults(func=cmd_balance)

    # audio
    a = sub.add_parser("audio", help="generate BGM/SFX")
    a.add_argument("--style", required=True, help="audio style description")
    a.add_argument("--output", required=True, help=".mp3/.wav path")
    a.set_defaults(func=cmd_audio)

    args = p.parse_args()
    sys.exit(args.func(args) or 0)


if __name__ == "__main__":
    main()
