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


PS_FOCUS = r"""
$ErrorActionPreference = 'Stop'
Add-Type @"
using System;
using System.Runtime.InteropServices;
public struct R { public int L, T, Rt, B; }
public class W {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after,
      int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref P p);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
public struct P { public int X, Y; }
"@
[void][W]::SetProcessDPIAware()
$p = Get-Process -Name 'PawnSim' -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowTitle -eq '__TITLE__' } | Select-Object -First 1
if ($null -eq $p) { Write-Output 'NOWINDOW'; exit 1 }
$h = $p.MainWindowHandle
[void][W]::ShowWindow($h, 9)          # SW_RESTORE
[void][W]::SetForegroundWindow($h)
# HWND_TOPMOST(-1), SWP_NOMOVE|SWP_NOSIZE(0x0002|0x0001)
[void][W]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 0x0003)
Start-Sleep -Milliseconds 400
# 클라이언트 영역(테두리·타이틀바 제외)의 화면 좌표 — 여기만 잘라 찍는다.
$rc = New-Object R
[void][W]::GetClientRect($h, [ref]$rc)
$pt = New-Object P
[void][W]::ClientToScreen($h, [ref]$pt)
Write-Output ("RECT {0} {1} {2} {3}" -f $pt.X, $pt.Y, ($rc.Rt - $rc.L), ($rc.B - $rc.T))
"""


def focus_window(title: str):
    """게임 창을 전경 + 최상위로 올리고 **클라이언트 영역 화면 좌표**를 돌려준다.

    좌표가 필요한 이유: gdigrab 의 창 단위 캡처(`-i title=`)는 Unity 처럼 GPU
    스왑체인으로 그리는 창에서 정지 화면을 돌려준다.  데스크톱 DC 를 잘라 찍어야
    실제 화면이 잡히고, 그러려면 창이 화면 어디에 있는지 알아야 한다.
    """
    try:
        r = subprocess.run(
            ["powershell", "-NoProfile", "-NonInteractive", "-Command",
             PS_FOCUS.replace("__TITLE__", title)],
            capture_output=True, text=True, timeout=30)
        for line in (r.stdout or "").splitlines():
            if line.startswith("RECT "):
                x, y, w, h = (int(v) for v in line.split()[1:5])
                if w > 0 and h > 0:
                    # 짝수 폭/높이 (yuv420p 요구)
                    return (x, y, w - (w % 2), h - (h % 2))
        print(f"[record-demo] focus 실패: {(r.stdout or '').strip()} "
              f"{(r.stderr or '').strip()[:200]}")
        return None
    except Exception as e:                                  # noqa: BLE001
        print(f"[record-demo] focus 예외: {e}")
        return None


def ffmpeg_bin() -> str:
    exe = shutil.which("ffmpeg")
    if not exe:
        sys.exit("[record-demo] ffmpeg 을 찾을 수 없다 (PATH 확인)")
    return exe


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--scenario", default="_demo-submission")
    ap.add_argument("--fps", type=int, default=30)
    # 1080p 고정.  카메라 ortho 크기는 해상도와 무관하게 세로 30 유닛을 잡으므로,
    #  720p 로 찍으면 타일이 24px(1080p 36px)로 줄어 **화면이 통째로 축소**돼 보인다.
    #  심사용 영상에서 콜로니스트가 점으로 보이면 안 된다.
    ap.add_argument("--width", type=int, default=1920)
    ap.add_argument("--height", type=int, default=1080)
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

    # ⚠ 반드시 필요하다.  gdigrab 의 `title=` 캡처는 BitBlt 라 창이 **가려져 있으면
    #  위에 덮인 창의 내용이 그대로 찍힌다**.  2026-07-29 1차 녹화에서 실제로 게임이
    #  아니라 뒤에 있던 다른 창이 51초간 녹화됐다 (에러도 없이).  전경 + 최상위로
    #  올려 가림 자체를 없앤다.
    rect = focus_window(WINDOW_TITLE)
    if rect is None:
        game.terminate()
        print("[record-demo] ✗ 게임 창을 전경으로 올리지 못했다 — 가려진 화면이 찍힐 수 있어 중단")
        return 1
    print(f"[record-demo] window : x={rect[0]} y={rect[1]} {rect[2]}x{rect[3]}")
    time.sleep(1.0)

    # ── 2. 창 캡처 시작 ────────────────────────────────────────────────
    if raw.exists():
        raw.unlink()
    # ⚠ `-i title=...`(창 단위 BitBlt)를 쓰면 안 된다.  Unity 는 GPU 스왑체인으로
    #  그리므로 GDI 창 캡처가 **정지된 첫 프레임을 계속 돌려준다** — 2026-07-29 2차
    #  녹화가 50초 내내 6:05 AM·1x 로 얼어붙은 화면을 찍었다(인게임 샷은 1x→3x→6x 로
    #  정상 변했으므로 게임이 아니라 캡처가 틀린 것이 확정).
    #  데스크톱 DC 는 합성 결과를 담으므로 GPU 창도 제대로 읽힌다.  창을 전경·최상위로
    #  올려 둔 상태에서 창 사각형만 잘라 찍는다.
    rec = subprocess.Popen([
        ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
        "-f", "gdigrab", "-framerate", str(args.fps),
        "-offset_x", str(rect[0]), "-offset_y", str(rect[1]),
        "-video_size", f"{rect[2]}x{rect[3]}",
        "-i", "desktop",
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

    # ── 5. 무엇을 찍었는지 검사 ────────────────────────────────────────
    #  1차 녹화는 게임이 아니라 뒤에 있던 다른 창을 51초간 찍고도 "✓" 를 냈다.
    #  길이·용량만 보면 성공처럼 보인다 — 내용을 봐야 한다.
    ok, why = looks_like_game(out)
    if not ok:
        print(f"[record-demo] ✗ 찍힌 내용이 게임이 아니다: {why}")
        print("[record-demo]   창이 가려졌거나 다른 창이 잡혔다. 파일을 삭제한다.")
        out.unlink(missing_ok=True)
        raw.unlink(missing_ok=True)
        return 1

    dur = probe_duration(out)
    print(f"[record-demo] ✓ {out}  ({out.stat().st_size/1e6:.1f} MB, {dur:.1f}s)  [{why}]")
    if dur < 30 or dur > 60:
        print(f"[record-demo] ⚠ 요강은 30~60초다 — 현재 {dur:.1f}s. "
              f"시나리오 wait 값을 조정하거나 --warmup 으로 앞머리를 잘라라.")
    return 0


def looks_like_game(video: Path) -> tuple[bool, str]:
    """찍힌 것이 게임 화면인지 색 분포로 판정한다.

    PawnSim 은 따뜻한 초원(녹/갈)이 화면을 지배한다: 채널 평균이 R>B, G>B.
    반면 잘못 잡히는 대상(에디터·터미널)은 어두운 남색 계열이라 B >= G 이고
    전체가 어둡다.  완벽한 판별이 아니라 **명백한 오촬영을 잡는 가드**다.
    """
    try:
        from PIL import Image  # noqa: PLC0415
    except ImportError:
        return True, "PIL 없음 — 내용 검사 건너뜀"

    import tempfile
    votes, thumbs = [], []
    with tempfile.TemporaryDirectory() as td:
        for t in (5, 20, 35, 45):
            f = Path(td) / f"probe_{t}.png"
            subprocess.run([ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
                            "-ss", str(t), "-i", str(video), "-frames:v", "1", str(f)],
                           check=False)
            if not f.exists():
                continue
            im = Image.open(f).convert("RGB").resize((96, 54))
            px = list(im.getdata())
            n = len(px)
            r = sum(p[0] for p in px) / n
            g = sum(p[1] for p in px) / n
            b = sum(p[2] for p in px) / n
            votes.append((g > b + 6 and r > b, (r, g, b)))
            thumbs.append(px)
    if not votes:
        return False, "프레임을 하나도 못 뽑았다"

    good = sum(1 for v, _ in votes if v)
    means = ", ".join(f"({r:.0f},{g:.0f},{b:.0f})" for _, (r, g, b) in votes)
    if good < 2:
        return False, f"게임 색분포 {good}/{len(votes)} — 평균 RGB {means}"

    # 정지 화면 검사.  2026-07-29 2차 녹화는 색분포는 게임인데 **50초 내내 같은
    #  프레임**이었다 (GDI 창 캡처가 스왑체인을 못 읽음).  색만 보면 통과한다.
    if len(thumbs) >= 2:
        diffs = []
        for a, b_ in zip(thumbs, thumbs[1:]):
            diffs.append(sum(abs(p[0] - q[0]) + abs(p[1] - q[1]) + abs(p[2] - q[2])
                             for p, q in zip(a, b_)) / (len(a) * 3))
        motion = max(diffs)
        if motion < 2.0:
            return False, (f"정지 화면 — 프레임 간 평균 차 {motion:.2f}/255 "
                           f"(게임은 시간·림 이동으로 훨씬 크다)")
        return True, f"색분포 {good}/{len(votes)} · 움직임 {motion:.1f} — 평균 RGB {means}"
    return True, f"게임 색분포 {good}/{len(votes)} — 평균 RGB {means}"


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
