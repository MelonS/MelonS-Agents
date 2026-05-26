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
import io
import sys
from pathlib import Path

# Windows default console is cp949 which can't render em-dash / Korean.
# Force UTF-8 stdout so YAML-loaded Korean prints cleanly.
if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    try:
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
        sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")
    except Exception:
        pass

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))

from modules import asset_gen  # noqa: E402
from modules import asset_fetch  # noqa: E402
from modules import integrator  # noqa: E402
from modules import qa as qa_module  # noqa: E402
from modules import planner  # noqa: E402
from modules import coder  # noqa: E402
from modules import scaffold  # noqa: E402


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


def cmd_fetch_assets(args):
    if args.cmd_sub == "list":
        for name, meta in asset_fetch.KENNEY_CATALOG.items():
            print(f"  {name:18s} {meta['license']:6s}  {meta['use_for']}")
        return 0
    elif args.cmd_sub == "fetch":
        path = asset_fetch.fetch_pack(args.pack, Path(args.out_dir))
        print(f"ready: {path}")
        return 0


def cmd_plan(args):
    p = planner.plan(args.spec)
    planner.print_plan(p)
    return 0


def cmd_scaffold_editor(args):
    written = scaffold.generate_editor_scaffold(
        Path(args.assets_dir),
        project_name=args.project_name,
        namespace=args.namespace,
        exe_name=args.exe_name,
        overwrite=args.overwrite,
    )
    print(f"[scaffold] {len(written)} file(s) materialized")
    return 0


def cmd_code(args):
    content = coder.scaffold(args.class_name, args.template, args.description or "")
    if args.output:
        Path(args.output).parent.mkdir(parents=True, exist_ok=True)
        Path(args.output).write_text(content, encoding="utf-8")
        print(f"wrote {args.output}")
    else:
        print(content)
    return 0


def cmd_integrate(args):
    proj = Path(args.project)
    if args.method == "scenes":
        return integrator.gen_scenes(proj)
    elif args.method == "build":
        return integrator.build_windows(proj, day=args.day)
    elif args.method == "verify-build":
        return integrator.build_verify(proj)
    else:
        rc, _ = integrator.run_unity_method(proj, args.method)
        return rc


def cmd_qa(args):
    ok, msg = qa_module.verify(
        Path(args.exe),
        Path(args.screenshot),
        delay_sec=args.delay,
    )
    print(msg)
    return 0 if ok else 1


def cmd_balance(args):
    print("[balance] not yet implemented (planned Day 3 of next prototype)", file=sys.stderr)
    return 99


def cmd_audio(args):
    print("[audio] not yet implemented (planned Day 6 of next prototype)", file=sys.stderr)
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

    # gen-editor-scaffold (Build Engineer / TA agent)
    ges = sub.add_parser("gen-editor-scaffold",
                         help="materialize SceneSetup + BuildScript + AutoScreenshotter "
                              "in a Unity project (one-shot, idempotent)")
    ges.add_argument("assets_dir", help="path to <unity-project>/Assets/")
    ges.add_argument("--project-name", required=True,
                     help="MenuItem label + namespace prefix (e.g. 'SuikaLite')")
    ges.add_argument("--namespace", default="MelonS.GameProto",
                     help="C# namespace (default MelonS.GameProto for legacy compat)")
    ges.add_argument("--exe-name",
                     help="Windows .exe stem (default = project-name minus spaces)")
    ges.add_argument("--overwrite", action="store_true",
                     help="replace existing files (default: skip)")
    ges.set_defaults(func=cmd_scaffold_editor)

    # code
    c = sub.add_parser("code", help="scaffold Unity C# script from a template")
    c.add_argument("class_name", help="C# class name (e.g. PlayerHealth)")
    c.add_argument("--template", default="minimal-monobehaviour",
                   choices=list(coder.CSHARP_TEMPLATES.keys()))
    c.add_argument("--description", default="")
    c.add_argument("--output", help=".cs output path (omit to stdout)")
    c.set_defaults(func=cmd_code)

    # plan
    pl = sub.add_parser("plan", help="produce task list from natural-language game spec")
    pl.add_argument("spec", help='e.g. "build a vampire survivors lite"')
    pl.set_defaults(func=cmd_plan)

    # fetch-assets
    fa = sub.add_parser("fetch-assets", help="Kenney CC0 asset pack ops")
    fa_sub = fa.add_subparsers(dest="cmd_sub", required=True)
    fa_sub.add_parser("list", help="list known packs")
    fa_fetch = fa_sub.add_parser("fetch", help="download + extract a pack")
    fa_fetch.add_argument("pack")
    fa_fetch.add_argument("--out-dir", default="G:/ai/assets_external")
    fa.set_defaults(func=cmd_fetch_assets)

    # integrate
    intg = sub.add_parser("integrate", help="invoke Unity batchmode method")
    intg.add_argument("--project", required=True, help="Unity project path")
    intg.add_argument("--method", default="scenes",
                      help="scenes | build | verify-build | <fully-qualified static method>")
    intg.add_argument("--day", default="X", help="day index for MELONS_BUILD_DAY env")
    intg.set_defaults(func=cmd_integrate)

    # qa
    qa = sub.add_parser("qa", help="run built .exe + capture screenshot + verify")
    qa.add_argument("--exe", required=True)
    qa.add_argument("--screenshot", required=True)
    qa.add_argument("--delay", type=float, default=4.0)
    qa.set_defaults(func=cmd_qa)

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
