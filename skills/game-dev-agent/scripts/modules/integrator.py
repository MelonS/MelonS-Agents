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
    # encoding 을 명시한다 — 없으면 Windows 가 **cp949** 로 디코드해서, Unity 로그에
    #  한글이 한 줄이라도 들어가는 순간 UnicodeDecodeError 로 죽는다.  빌드 자체는
    #  성공했는데 로그를 읽다 실패해 `FAIL rc=1` 이 뜬다 — 원인이 정반대로 보인다.
    #  (2026-08-09: 새 Debug.Log 한글 한 줄에 WebGL 배포 경로가 통째로 막혔다.)
    result = subprocess.run(cmd, capture_output=True, text=True,
                            encoding="utf-8", errors="replace")

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


def record_gameplay(
    project_path: Path,
    duration_sec: int = 120,
    out_path: str = "G:/ai/pawnsim_gameplay.mp4",
    fps: int = 30,
    unity_exe: Optional[str] = None,
) -> int:
    """Record gameplay video via Unity Recorder (H.264 mp4, Game View).

    IMPORTANT: does NOT pass -nographics — Unity Recorder requires rendering.
    Runs Unity with -batchmode only (windowed editor on a machine with display).

    Args:
        project_path: Unity project root
        duration_sec: recording length in seconds
        out_path:     absolute path for the output mp4
        fps:          capture frame rate (30 recommended)
        unity_exe:    override Unity.exe path (uses DEFAULT_UNITY if None)

    Returns Unity exit code (0 = success).
    """
    import os as _os

    unity = unity_exe or DEFAULT_UNITY
    if not Path(unity).exists():
        raise FileNotFoundError(f"Unity not found at {unity} (set UNITY_EXE env)")

    log_file = Path("G:/ai/_unity_recorder.log")

    # Env vars for GameplayRecorderTool.cs
    env = _os.environ.copy()
    env["MELONS_REC_SECONDS"] = str(duration_sec)
    env["MELONS_REC_OUT"]     = out_path
    env["MELONS_REC_FPS"]     = str(fps)

    # NOTE: -nographics intentionally omitted — Recorder needs the render pipeline.
    # NOTE: -quit intentionally omitted — GameplayRecorderTool calls EditorApplication.Exit()
    #       itself; -quit would kill Unity before the EditorApplication callbacks fire.
    cmd = [
        unity,
        "-batchmode",
        "-projectPath", str(project_path),
        "-executeMethod", "MelonS.GameProto.EditorTools.GameplayRecorderTool.Record",
        "-logFile", str(log_file),
    ]

    print(f"[integrator] record_gameplay: {duration_sec}s -> {out_path}")
    print(f"[integrator] cmd: {' '.join(cmd)}")

    result = subprocess.run(cmd, env=env)

    log_tail = ""
    if log_file.exists():
        with open(log_file, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.readlines()
            log_tail = "".join(lines[-30:])

    if result.returncode != 0:
        print(f"[integrator] record_gameplay FAIL rc={result.returncode}")
        if log_tail:
            print("--- log tail ---")
            print(log_tail)
    else:
        found = Path(out_path).exists() or Path(out_path.replace(".mp4", "_0001.mp4")).exists()
        if found:
            p = Path(out_path) if Path(out_path).exists() else Path(out_path.replace(".mp4", "_0001.mp4"))
            print(f"[integrator] record_gameplay OK: {p} ({p.stat().st_size // 1024} KB)")
        else:
            print(f"[integrator] record_gameplay: Unity ok but output file missing at {out_path}")

    return result.returncode
