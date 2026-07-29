#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""record-demo.py — 제출물 ② 플레이 영상(30~60초)을 스크립트 조작으로 녹화한다.

왜 이 스크립트가 있는가
----------------------
NAN 2026 제출물 ②는 "실제 플레이 화면 30~60초"이고, AI 합성 영상과 타 게임 영상은
금지다.  그래서 **진짜 빌드를 진짜로 조작해** 화면을 그대로 캡처해야 한다.

운영자는 터미널을 만지지 않는다(운영자 계약).  그러므로 "사람이 플레이하며 녹화"는
선택지가 아니고, 조작 자체가 재현 가능한 산출물이어야 한다 →
`repro-scenarios/_demo-submission.json` 이 그 조작이고, 이 스크립트가 그 실행·캡처다.

왜 Unity Recorder 가 아니라 ffmpeg 화면 캡처인가
-----------------------------------------------
`record-gameplay.py`(Unity Recorder)는 **에디터 Play 모드**를 찍는다.  그런데 조작
스크립트(ReproHarness)는 스탠드얼론 CLI 인자(`-repro`)로만 구동된다.  둘을 합치려면
하네스에 에디터 진입점을 새로 뚫어야 하는데, 그건 제출용 산출물을 위해 검증된
실행 경로를 새로 만드는 셈이라 더 위험하다.  **심사자가 받는 것과 같은 빌드를**
그대로 띄우고 그 창을 찍는 편이 산출물의 진실성에도 맞는다.

usage:
  python skills/game-prototype/scripts/record-demo.py
  python skills/game-prototype/scripts/record-demo.py --scenario _demo-submission --fps 30
"""
from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import time
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]          # skills/game-prototype
SCEN_DIR = ROOT / "repro-scenarios"
BUILDS = ROOT / "builds"
OUT_DIR = ROOT / "art-out" / "demo"

# Unity 스탠드얼론 창 제목 = ProductName.
WINDOW_TITLE = "PawnSim"


def newest_exe() -> Path:
    """빌드 폴더는 날짜 스탬프(day-X-YYYY-MM-DD)라 하드코딩하면 자정 넘어 stale 을
    찍는다.  항상 mtime 최신을 고른다."""
    cands = sorted(BUILDS.glob("*/PawnSim.exe"), key=lambda p: p.stat().st_mtime)
    if not cands:
        sys.exit(f"[record-demo] 빌드 없음: {BUILDS}/*/PawnSim.exe")
    return cands[-1]


def ffmpeg_bin() -> str:
    exe = shutil.which("ffmpeg")
    if not exe:
        sys.exit("[record-demo] ffmpeg 을 찾을 수 없다 (PATH 확인)")
    return exe


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--scenario", default="_demo-submission")
    ap.add_argument("--fps", type=int, default=30)
    ap.add_argument("--width", type=int, default=1280)
    ap.add_argument("--height", type=int, default=720)
    ap.add_argument("--warmup", type=float, default=6.0,
                    help="창이 뜨고 게임 씬이 안정될 때까지 캡처를 미루는 초")
    ap.add_argument("--timeout", type=float, default=240.0)
    ap.add_argument("--out", default=None)
    args = ap.parse_args()

    scenario = SCEN_DIR / f"{args.scenario}.json"
    if not scenario.exists():
        sys.exit(f"[record-demo] 시나리오 없음: {scenario}")

    exe = newest_exe()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    raw = OUT_DIR / "demo_raw.mp4"
    out = Path(args.out) if args.out else OUT_DIR / "pawnsim_demo.mp4"

    print(f"[record-demo] build   : {exe.parent.name}")
    print(f"[record-demo] scenario: {scenario.name}")

    # ── 1. 게임 실행 (심사자가 받는 것과 같은 빌드, 창 모드) ──────────────
    game = subprocess.Popen([
        str(exe), "-autostart",
        "-repro", str(scenario),
        "-repro-report", str(OUT_DIR / "demo_report.json"),
        "-repro-shotdir", str(OUT_DIR / "shots"),
        "-repro-seed", "20260729",
        "-screen-width", str(args.width),
        "-screen-height", str(args.height),
        "-screen-fullscreen", "0",
    ])

    # 창이 뜨고 메뉴 → 게임 씬 전환이 끝날 때까지 기다린다.  이 구간을 찍으면
    # 영상 앞머리가 검은 로딩 화면이 되어 30초 예산을 낭비한다.
    time.sleep(args.warmup)
    if game.poll() is not None:
        return fail(game, "게임이 워밍업 중 종료됐다")

    # ── 2. 창 캡처 시작 ────────────────────────────────────────────────
    if raw.exists():
        raw.unlink()
    rec = subprocess.Popen([
        ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
        "-f", "gdigrab", "-framerate", str(args.fps),
        "-i", f"title={WINDOW_TITLE}",
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "20",
        "-pix_fmt", "yuv420p",
        str(raw),
    ], stdin=subprocess.PIPE)
    print(f"[record-demo] 캡처 시작 → {raw.name}")

    # ── 3. 시나리오가 끝날 때까지 (게임이 스스로 종료한다) ────────────────
    deadline = time.time() + args.timeout
    while time.time() < deadline and game.poll() is None:
        time.sleep(0.5)
    if game.poll() is None:
        game.terminate()
        print("[record-demo] ⚠ 타임아웃 — 게임 강제 종료")

    # ffmpeg 은 'q' 로 정상 종료시켜야 moov atom 이 기록된다.  kill 하면 재생
    # 불가한 파일이 남는다.
    try:
        rec.communicate(input=b"q", timeout=15)
    except Exception:
        rec.terminate()

    if not raw.exists() or raw.stat().st_size < 50_000:
        print(f"[record-demo] ✗ 캡처 실패 ({raw})")
        return 1

    # ── 4. 마무리 인코딩 (웹 재생 대비 faststart) ─────────────────────────
    subprocess.run([
        ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
        "-i", str(raw),
        "-c:v", "libx264", "-preset", "slow", "-crf", "19",
        "-pix_fmt", "yuv420p", "-movflags", "+faststart",
        "-an", str(out),
    ], check=True)

    dur = probe_duration(out)
    print(f"[record-demo] ✓ {out}  ({out.stat().st_size/1e6:.1f} MB, {dur:.1f}s)")
    if dur < 30 or dur > 60:
        print(f"[record-demo] ⚠ 요강은 30~60초다 — 현재 {dur:.1f}s. "
              f"시나리오 wait 값을 조정하거나 --warmup 으로 앞머리를 잘라라.")
    return 0


def probe_duration(p: Path) -> float:
    try:
        r = subprocess.run(
            [shutil.which("ffprobe") or "ffprobe", "-v", "error",
             "-show_entries", "format=duration", "-of", "csv=p=0", str(p)],
            capture_output=True, text=True, check=True)
        return float(r.stdout.strip())
    except Exception:
        return -1.0


def fail(proc, msg: str) -> int:
    print(f"[record-demo] ✗ {msg}")
    if proc.poll() is None:
        proc.terminate()
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
