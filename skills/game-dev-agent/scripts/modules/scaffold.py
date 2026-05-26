"""scaffold — Editor + harness scaffolding generator.

Generates the 4-file boilerplate set every Unity prototype starts with:

    Assets/Editor/SceneSetup.cs        — programmatic scene generation
    Assets/Editor/BuildScript.cs       — Windows + verify build entry points
    Assets/Scripts/AutoScreenshotter.cs — qa.py self-verify harness

Templates live at `skills/game-dev-agent/templates/editor/*.cs.tmpl`
and encode hard-won lessons (sprite import race, skybox leak,
auto-quit guard, audio throttle, GetInstanceID tiebreaker, Stay
handler, singleton-subscription race) — see `lessons.md` next to
templates.

This is the Build-Engineer / TA agent of the game-dev pipeline:
"set up the project skeleton so the gameplay programmer never has
to write build infrastructure from scratch."
"""
from __future__ import annotations

from pathlib import Path
from typing import Optional


TEMPLATES_DIR = Path(__file__).resolve().parent.parent.parent / "templates" / "editor"

# (template_name, destination relative to project Assets/)
TEMPLATE_TARGETS = [
    ("AutoScreenshotter.cs.tmpl", "Scripts/AutoScreenshotter.cs"),
    ("SceneSetup.cs.tmpl",        "Editor/SceneSetup.cs"),
    ("BuildScript.cs.tmpl",       "Editor/BuildScript.cs"),
]


def render(template: str, variables: dict[str, str]) -> str:
    out = template
    for k, v in variables.items():
        out = out.replace("{{" + k + "}}", v)
    return out


def generate_editor_scaffold(
    assets_dir: Path,
    project_name: str,
    namespace: Optional[str] = None,
    exe_name: Optional[str] = None,
    overwrite: bool = False,
) -> list[Path]:
    """Materialize the 3-file editor scaffold under <assets_dir>/.

    Intended usage: a NEW prototype directory.  Running against an
    existing prototype that already has custom-named editor scripts
    (e.g. SuikaSceneSetup.cs in skills/game-prototype-suika/) will
    create duplicate default-named files alongside — overwrite=False
    only guards against same-name collision, not different-name
    duplicates.  Delete unused defaults manually if so.

    Args:
        assets_dir: typically <unity-project>/Assets/
        project_name: human-friendly name (used in MenuItem labels)
        namespace: C# namespace (default MelonS.GameProto for legacy compat)
        exe_name: Windows .exe stem (default = project_name without spaces)
        overwrite: if False, skip files that already exist (safe re-runs)
    """
    namespace = namespace or "MelonS.GameProto"
    exe_name = exe_name or project_name.replace(" ", "")

    variables = {
        "PROJECT_NAME": project_name,
        "NAMESPACE":    namespace,
        "EXE_NAME":     exe_name,
    }

    written: list[Path] = []
    for tmpl_name, rel in TEMPLATE_TARGETS:
        src = TEMPLATES_DIR / tmpl_name
        if not src.exists():
            raise FileNotFoundError(f"missing template: {src}")
        dest = assets_dir / rel
        if dest.exists() and not overwrite:
            print(f"[scaffold] SKIP exists: {dest}")
            continue
        dest.parent.mkdir(parents=True, exist_ok=True)
        rendered = render(src.read_text(encoding="utf-8"), variables)
        dest.write_text(rendered, encoding="utf-8")
        print(f"[scaffold] wrote {dest}")
        written.append(dest)
    return written


if __name__ == "__main__":
    import argparse
    p = argparse.ArgumentParser()
    p.add_argument("assets_dir", type=Path)
    p.add_argument("--project-name", required=True)
    p.add_argument("--namespace", default="MelonS.GameProto")
    p.add_argument("--exe-name")
    p.add_argument("--overwrite", action="store_true")
    args = p.parse_args()
    generate_editor_scaffold(
        args.assets_dir, args.project_name, args.namespace,
        args.exe_name, args.overwrite,
    )
