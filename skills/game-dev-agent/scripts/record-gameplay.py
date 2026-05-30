#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""record-gameplay.py — Record PawnSim gameplay to mp4 via Unity Recorder.

USAGE:
  python record-gameplay.py
  python record-gameplay.py --seconds 30
  python record-gameplay.py --seconds 120 --out G:/ai/pawnsim_gameplay.mp4

ENV VARS (override any CLI arg):
  MELONS_REC_SECONDS   — duration in seconds (default 120)
  MELONS_REC_OUT       — output mp4 absolute path (default G:/ai/pawnsim_gameplay.mp4)
  MELONS_REC_FPS       — capture frame rate (default 30)
  UNITY_EXE            — override Unity.exe path

CRITICAL design decisions:
  1. NO -nographics: Unity Recorder captures the Game View via the rendering
     pipeline.  -nographics disables rendering entirely = black/empty output.
  2. NO -quit: -quit makes Unity exit immediately after executeMethod returns,
     before any EditorApplication.update callbacks fire.  GameplayRecorderTool
     calls EditorApplication.Exit() itself when done.
  The machine must have a display (Windows 11 + GPU — confirmed here).

The Unity process opens a windowed editor, loads Game.unity, enters Play
mode, records for the requested duration, writes the mp4, then quits.
Progress is streamed from the Unity log file in real-time.
"""
from __future__ import annotations

import argparse
import io
import os
import subprocess
import sys
import time
from pathlib import Path

# Force UTF-8 stdout so Unity log lines with non-ASCII don't crash the tailer
if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    try:
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
        sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")
    except Exception:
        pass

UNITY_EXE   = os.environ.get("UNITY_EXE",
              "G:/tools/UnityEditors/6000.0.75f1/Editor/Unity.exe")
SCRIPT_DIR  = Path(__file__).resolve().parent
# scripts/ -> game-dev-agent/ (1 ..) -> skills/ (2 ..) -> game-prototype/unity-project
PROJECT     = (SCRIPT_DIR / ".." / ".." / "game-prototype" / "unity-project").resolve()
METHOD      = "MelonS.GameProto.EditorTools.GameplayRecorderTool.Record"
LOG_FILE    = Path("G:/ai/_unity_recorder.log")
DEFAULT_OUT = "G:/ai/pawnsim_gameplay.mp4"

# Keywords that matter when tailing the log
INTERESTING = (
    "[GameplayRecorderTool]",
    "[Recorder]",
    "RecorderController",
    "ERROR",
    "Exception",
    "FAILED",
    "Recording",
    "OUTPUT OK",
    "WARNING:",
    "Batchmode quit",
    "EnteredPlayMode",
    "EnteredEditMode",
    "stopped",
)


def stream_log(log_path: Path, proc: subprocess.Popen, poll_interval: float = 2.0):
    """Tail Unity log while Unity runs, printing relevant lines."""
    offset = 0
    while proc.poll() is None:
        time.sleep(poll_interval)
        if log_path.exists():
            try:
                with open(log_path, "r", encoding="utf-8", errors="replace") as f:
                    f.seek(offset)
                    chunk = f.read()
                    if chunk:
                        for line in chunk.splitlines():
                            stripped = line.strip()
                            if not stripped:
                                continue
                            if any(kw in stripped for kw in INTERESTING):
                                print(f"  {stripped}", flush=True)
                    offset = f.tell()
            except OSError:
                pass


def run(seconds: int, out_path: str) -> int:
    if not Path(UNITY_EXE).exists():
        print(f"ERROR: Unity not found: {UNITY_EXE}", file=sys.stderr)
        print("       Set UNITY_EXE env var to override.", file=sys.stderr)
        return 1

    if not PROJECT.exists():
        print(f"ERROR: Unity project not found: {PROJECT}", file=sys.stderr)
        return 1

    # Env vars for GameplayRecorderTool.cs to read
    env = os.environ.copy()
    env["MELONS_REC_SECONDS"] = str(seconds)
    env["MELONS_REC_OUT"]     = out_path
    env["MELONS_REC_FPS"]     = env.get("MELONS_REC_FPS", "30")

    # Ensure output dir exists
    Path(out_path).parent.mkdir(parents=True, exist_ok=True)

    LOG_FILE.parent.mkdir(parents=True, exist_ok=True)
    if LOG_FILE.exists():
        try:
            LOG_FILE.unlink()
        except OSError:
            # Log file may be locked by a previous Unity run; that's fine —
            # Unity will overwrite it when it starts.
            pass

    cmd = [
        UNITY_EXE,
        "-batchmode",
        # NOTE: -nographics intentionally absent (Recorder needs rendering pipeline).
        # NOTE: -quit intentionally absent (GameplayRecorderTool calls Exit() itself
        #       after recording completes; -quit would kill Unity before callbacks fire).
        "-projectPath", str(PROJECT),
        "-executeMethod", METHOD,
        "-logFile",  str(LOG_FILE),
    ]

    print(f"[record-gameplay] Recording {seconds}s of PawnSim gameplay")
    print(f"[record-gameplay] Output : {out_path}")
    print(f"[record-gameplay] Log    : {LOG_FILE}")
    print(f"[record-gameplay] Unity  : {UNITY_EXE}")
    print(f"[record-gameplay] Project: {PROJECT}")
    print()
    print("[record-gameplay] Starting Unity — may take 60-120s to compile + enter play mode...")
    print()

    proc = subprocess.Popen(cmd, env=env)
    try:
        stream_log(LOG_FILE, proc, poll_interval=2.0)
    except KeyboardInterrupt:
        print("\n[record-gameplay] Interrupted — terminating Unity.")
        proc.terminate()
        return 130

    rc = proc.returncode
    print()

    if rc == 0:
        # Confirm output file exists
        out_no_ext = out_path[:-4] if out_path.lower().endswith(".mp4") else out_path
        candidates = [
            out_path,
            out_no_ext + "_0001.mp4",
        ]
        found = None
        for c in candidates:
            if Path(c).exists():
                found = c
                break

        if found:
            size_bytes = Path(found).stat().st_size
            size_mb = size_bytes / (1024 * 1024)
            print(f"[record-gameplay] SUCCESS: {found}")
            print(f"[record-gameplay] Size   : {size_mb:.1f} MB ({size_bytes:,} bytes)")
            return 0
        else:
            print(f"[record-gameplay] WARN: Unity exited 0 but output file not found.")
            print(f"[record-gameplay] Checked: {candidates}")
            _dump_log_tail(LOG_FILE, 40)
            return 2
    else:
        print(f"[record-gameplay] FAIL: Unity exited with code {rc}")
        _dump_log_tail(LOG_FILE, 50)
        return rc


def _dump_log_tail(log_path: Path, n: int):
    if not log_path.exists():
        print("[record-gameplay] (no log file)")
        return
    with open(log_path, "r", encoding="utf-8", errors="replace") as f:
        lines = f.readlines()
    print(f"--- Unity log tail (last {n} lines) ---")
    for line in lines[-n:]:
        print(line, end="")


def main():
    ap = argparse.ArgumentParser(
        description="Record PawnSim gameplay to mp4 via Unity Recorder")
    ap.add_argument(
        "--seconds", type=int,
        default=int(os.environ.get("MELONS_REC_SECONDS", "120")),
        help="Recording duration in seconds (default 120; env: MELONS_REC_SECONDS)")
    ap.add_argument(
        "--out",
        default=os.environ.get("MELONS_REC_OUT", DEFAULT_OUT),
        help=f"Output mp4 path (default {DEFAULT_OUT}; env: MELONS_REC_OUT)")
    args = ap.parse_args()
    sys.exit(run(args.seconds, args.out))


if __name__ == "__main__":
    main()
