"""qa — built-game self-verification.

Launches a Unity standalone .exe with AutoScreenshotter, waits for
auto-capture + auto-quit, returns the screenshot path so the
orchestrator can inspect content (currently via image-capable Read
tool; future: vision-API content checks).
"""
from __future__ import annotations

import os
import signal
import subprocess
import sys
import time
from pathlib import Path
from typing import Optional


def launch_and_capture(
    exe_path: Path,
    screenshot_path: Path,
    delay_sec: float = 4.0,
    overall_timeout_sec: float = None,
) -> tuple[bool, str]:
    """Launch .exe, wait for AutoScreenshotter to write PNG + auto-quit.

    Returns (success, status_text).

    `overall_timeout_sec` defaults to `delay_sec + 20`, so long-delay
    QA runs (e.g. 90s for Day 12 regen) don't get killed mid-wait.
    Day 11 lesson: default 30s timeout truncated a 90s capture run.
    """
    if not exe_path.exists():
        return False, f"exe not found: {exe_path}"

    # Auto-extend timeout to delay + 20s when caller didn't override
    if overall_timeout_sec is None:
        overall_timeout_sec = max(30.0, delay_sec + 20.0)

    # Clean stale screenshot
    if screenshot_path.exists():
        screenshot_path.unlink()
    screenshot_path.parent.mkdir(parents=True, exist_ok=True)

    print(f"[qa] launching {exe_path.name} (delay {delay_sec}s, timeout {overall_timeout_sec}s)")
    proc = subprocess.Popen(
        [str(exe_path), "-delay", str(delay_sec), "-screenshot", str(screenshot_path)]
    )

    deadline = time.time() + overall_timeout_sec
    captured = False
    while time.time() < deadline:
        if screenshot_path.exists() and screenshot_path.stat().st_size > 1024:
            captured = True
            break
        if proc.poll() is not None:
            # process exited
            break
        time.sleep(0.5)

    # Ensure process is dead
    if proc.poll() is None:
        try:
            proc.terminate()
            time.sleep(1)
            if proc.poll() is None:
                proc.kill()
        except Exception:
            pass

    if captured:
        sz_kb = screenshot_path.stat().st_size / 1024
        return True, f"captured {screenshot_path} ({sz_kb:.0f} KB)"
    else:
        return False, f"no screenshot at {screenshot_path}"


def verify(
    exe_path: Path,
    screenshot_path: Path,
    delay_sec: float = 4.0,
) -> tuple[bool, str]:
    """High-level QA: launch, capture, basic file-level sanity check.

    Content-level checks (sprite presence, UI rendering correctness)
    happen at the agent level via image-aware tool inspection of the
    written PNG.  This function only confirms the build can run +
    produces a non-empty screenshot.
    """
    ok, msg = launch_and_capture(exe_path, screenshot_path, delay_sec)
    if not ok:
        return False, f"FAIL: {msg}"

    sz = screenshot_path.stat().st_size
    if sz < 1024:
        return False, f"FAIL: screenshot too small ({sz} bytes)"

    return True, f"PASS: {msg}"
