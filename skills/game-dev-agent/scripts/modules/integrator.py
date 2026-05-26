"""integrator — Unity batchmode invocation wrapper.

Wraps Unity Editor CLI calls (scene generation, build pipeline) so the
orchestrator can invoke them without rewriting the bash incantation
every time.
"""
from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path
from typing import Optional


# Default to known install (operator's machine).  Override via env.
DEFAULT_UNITY = os.environ.get(
    "UNITY_EXE",
    "G:/tools/UnityEditors/6000.0.75f1/Editor/Unity.exe",
)


def run_unity_method(
    project_path: Path,
    method_name: str,
    log_file: Optional[Path] = None,
    unity_exe: Optional[str] = None,
    extra_args: Optional[list[str]] = None,
) -> tuple[int, str]:
    """Run a Unity Editor static method in batchmode.

    Args:
        project_path: Unity project root (contains Assets/ + Packages/)
        method_name: fully-qualified, e.g.
            "MelonS.GameProto.EditorTools.SceneSetup.GenerateAll"
        log_file: where Unity writes its log (Unity overwrites this each run)
        unity_exe: absolute path to Unity.exe (or use env UNITY_EXE)
        extra_args: passed to Unity after standard batchmode flags

    Returns (exit_code, log_tail_lines).
    """
    unity = unity_exe or DEFAULT_UNITY
    if not Path(unity).exists():
        raise FileNotFoundError(f"Unity not found at {unity} (set UNITY_EXE env)")

    cmd = [
        unity,
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath", str(project_path),
        "-executeMethod", method_name,
    ]
    if log_file:
        cmd += ["-logFile", str(log_file)]
    if extra_args:
        cmd += list(extra_args)

    print(f"[integrator] {method_name} @ {project_path.name}")
    result = subprocess.run(cmd, capture_output=True, text=True)

    log_tail = ""
    if log_file and Path(log_file).exists():
        with open(log_file, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.readlines()
            log_tail = "".join(lines[-30:])

    if result.returncode != 0:
        print(f"[integrator] FAIL rc={result.returncode}")
        if log_tail:
            print("--- log tail ---")
            print(log_tail)
    else:
        print(f"[integrator] OK")

    return result.returncode, log_tail


def gen_scenes(project_path: Path) -> int:
    """Convenience: invoke SceneSetup.GenerateAll."""
    rc, _ = run_unity_method(
        project_path,
        "MelonS.GameProto.EditorTools.SceneSetup.GenerateAll",
        log_file=Path("G:/ai/_unity_scene.log"),
    )
    return rc


def build_windows(project_path: Path, day: str = "X") -> int:
    """Convenience: invoke BuildScript.BuildWindows."""
    os.environ["MELONS_BUILD_DAY"] = day
    rc, _ = run_unity_method(
        project_path,
        "MelonS.GameProto.EditorTools.BuildScript.BuildWindows",
        log_file=Path("G:/ai/_unity_build.log"),
    )
    return rc


def build_verify(project_path: Path) -> int:
    """Convenience: invoke BuildScript.BuildGameOnlyVerify (skips menu)."""
    rc, _ = run_unity_method(
        project_path,
        "MelonS.GameProto.EditorTools.BuildScript.BuildGameOnlyVerify",
        log_file=Path("G:/ai/_unity_build.log"),
    )
    return rc
