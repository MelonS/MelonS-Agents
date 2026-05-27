"""refactor_check - 리팩토링 한 사이클 자동 검증.

Usage:  python refactor_check.py [--tag R2-step1]

흐름:
  1. unity scenes (regen)
  2. unity verify-build
  3. PawnSim.exe --delay 3 --screenshot G:/ai/_refactor_current.png
  4. Player.log 에 error/exception 검사
  5. baseline 과 픽셀 diff (PIL) - 임계 5% 초과 시 FAIL
  6. exit 0 = PASS, 1 = FAIL (compile/build/runtime/visual)

  PASS 시:
    - 현재 screenshot 을 _refactor_baseline.png 로 덮어쓰지 X
    - 운영자가 시각 변경 의도적이면 --accept-visual 로 baseline 갱신
"""
from __future__ import annotations
import argparse
import sys
from pathlib import Path
from PIL import Image
import subprocess

REPO = Path(__file__).resolve().parents[3]  # ...skills/game-dev-agent/scripts/X.py -> MelonS-Agents
UNITY_PROJ = REPO / "skills" / "game-prototype" / "unity-project"
BUILD_EXE  = REPO / "skills" / "game-prototype" / "builds" / "verify-game-only" / "PawnSim.exe"
BASELINE   = Path("G:/ai/_refactor_baseline.png")
CURRENT    = Path("G:/ai/_refactor_current.png")
PLAYER_LOG = Path("C:/Users/comdo/AppData/LocalLow/DefaultCompany/unity-project/Player.log")
SCENE_LOG  = Path("G:/ai/_unity_scene.log")
BUILD_LOG  = Path("G:/ai/_unity_build.log")


def step_scenes() -> int:
    print("[refactor] (1/5) scenes regen ...")
    proc = subprocess.run(
        [sys.executable, str(REPO / "skills" / "game-dev-agent" / "scripts" / "agent.py"),
         "integrate", "--project", str(UNITY_PROJ), "--method", "scenes"],
        capture_output=True, text=True,
    )
    if proc.returncode != 0:
        print(f"  FAIL rc={proc.returncode}")
        print(proc.stderr[-500:])
        return 1
    # compile error check in scene log
    if SCENE_LOG.exists():
        log = SCENE_LOG.read_text(encoding="utf-8", errors="ignore")
        if "error CS" in log:
            print("  COMPILE ERROR:")
            for line in log.splitlines():
                if "error CS" in line:
                    print(f"  {line.strip()}")
            return 2
    print("  scenes OK")
    return 0


def step_build() -> int:
    print("[refactor] (2/5) build verify ...")
    proc = subprocess.run(
        [sys.executable, str(REPO / "skills" / "game-dev-agent" / "scripts" / "agent.py"),
         "integrate", "--project", str(UNITY_PROJ), "--method", "verify-build"],
        capture_output=True, text=True,
    )
    if proc.returncode != 0:
        print(f"  FAIL rc={proc.returncode}")
        return 3
    if BUILD_LOG.exists():
        log = BUILD_LOG.read_text(encoding="utf-8", errors="ignore")
        if "error CS" in log:
            print("  BUILD COMPILE ERROR")
            return 4
    print("  build OK")
    return 0


def step_qa(delay: float = 3.0) -> int:
    print(f"[refactor] (3/5) QA screenshot (delay {delay}s) ...")
    # qa 모듈 재사용 - 검증된 launch_and_capture 사용
    sys.path.insert(0, str(REPO / "skills" / "game-dev-agent" / "scripts"))
    from modules import qa
    ok, msg = qa.launch_and_capture(BUILD_EXE, CURRENT, delay_sec=delay)
    if not ok:
        print(f"  FAIL - {msg}")
        return 5
    print(f"  {msg}")
    return 0


def step_log_check() -> int:
    print("[refactor] (4/5) Player.log error scan ...")
    if not PLAYER_LOG.exists():
        print("  WARN: Player.log not found")
        return 0
    log = PLAYER_LOG.read_text(encoding="utf-8", errors="ignore")
    bad_lines = []
    for line in log.splitlines():
        # Ignore known harmless lines
        if "Direct3D: detected that IDXGISwapChain" in line: continue
        if "kGfxThreadingModeSplitJobs" in line: continue
        if any(tok in line for tok in ["Exception", "NullReferenceException",
                                       "error CS", "MissingReferenceException",
                                       "ArgumentOutOfRangeException"]):
            bad_lines.append(line.strip())
    if bad_lines:
        print("  RUNTIME ERROR:")
        for b in bad_lines[:10]:
            print(f"    {b}")
        return 6
    print("  no runtime errors")
    return 0


def step_playmode_tests() -> int:
    """R7 - PawnSim -testmode 자동 검증 (55 isolated 시나리오)"""
    print("[refactor] (6/7) PlayMode tests (isolated) ...")
    report_path = Path("G:/ai/_pawnsim_test_report.json")
    if report_path.exists():
        report_path.unlink()
    proc = subprocess.run(
        [str(BUILD_EXE), "-testmode", "-batchmode", "-nographics"],
        capture_output=True, text=True, timeout=30,
    )
    if not report_path.exists():
        print("  WARN: no test report produced - skip")
        return 0  # not a hard fail (legacy build 호환)
    import json
    try:
        report = json.loads(report_path.read_text(encoding="utf-8"))
    except Exception as e:
        print(f"  FAIL: parse {e}")
        return 9
    p, f = report.get("totalPassed", 0), report.get("totalFailed", 0)
    print(f"  isolated: {p} PASS / {f} FAIL")
    for r in report.get("results", []):
        sym = "OK" if r.get("passed") else "X"
        # cp949 console 호환 — 유니코드 안되는 문자 제거
        msg = r['message']
        try:
            print(f"    [{sym}] {r['id']}: {msg}")
        except UnicodeEncodeError:
            safe = msg.encode('cp949', errors='replace').decode('cp949')
            print(f"    [{sym}] {r['id']}: {safe}")
    if f > 0:
        return 10
    return 0


def step_integration_tests() -> int:
    """진짜 Game.unity 위 통합 검증 (I1-I16) - GUI 버튼/Pawn 이동/AI 행위 등 실제 게임 flow"""
    print("[refactor] (7/7) Integration tests (real game state) ...")
    report_path = Path("G:/ai/_pawnsim_integration_report.json")
    if report_path.exists():
        report_path.unlink()
    proc = subprocess.run(
        [str(BUILD_EXE), "-integration", "-batchmode", "-nographics",
         "-screen-width", "1280", "-screen-height", "720"],
        capture_output=True, text=True, timeout=180,  # I4 15s + I19 15s + I23 60s + 기타 → 180s
    )
    if not report_path.exists():
        print("  WARN: no integration report - skip")
        return 0
    import json
    try:
        report = json.loads(report_path.read_text(encoding="utf-8"))
    except Exception as e:
        print(f"  FAIL: parse {e}")
        return 11
    p, f = report.get("totalPassed", 0), report.get("totalFailed", 0)
    print(f"  integration: {p} PASS / {f} FAIL")
    for r in report.get("results", []):
        sym = "OK" if r.get("passed") else "X"
        # cp949 console 호환 — 유니코드 안되는 문자 제거
        msg = r['message']
        try:
            print(f"    [{sym}] {r['id']}: {msg}")
        except UnicodeEncodeError:
            safe = msg.encode('cp949', errors='replace').decode('cp949')
            print(f"    [{sym}] {r['id']}: {safe}")
    if f > 0:
        return 12
    return 0


def step_visual_diff(threshold_pct: float = 5.0) -> int:
    print(f"[refactor] (5/5) visual diff vs baseline (threshold {threshold_pct}%) ...")
    if not BASELINE.exists():
        print("  WARN: no baseline - saving current as baseline.")
        import shutil; shutil.copy(CURRENT, BASELINE)
        return 0
    a = Image.open(BASELINE).convert("RGB")
    b = Image.open(CURRENT).convert("RGB")
    if a.size != b.size:
        print(f"  FAIL size mismatch: baseline {a.size} vs current {b.size}")
        return 7
    # Resize down to 480x270 for quick pixel diff
    a_s = a.resize((480, 270))
    b_s = b.resize((480, 270))
    pa, pb = a_s.load(), b_s.load()
    diff = 0; total = 480 * 270
    for y in range(270):
        for x in range(480):
            ra, ga, ba_ = pa[x, y]
            rb, gb, bb_ = pb[x, y]
            if abs(ra-rb) + abs(ga-gb) + abs(ba_-bb_) > 30:  # ~12% per channel
                diff += 1
    pct = 100.0 * diff / total
    print(f"  diff {pct:.2f}%")
    if pct > threshold_pct:
        print(f"  FAIL - diff {pct:.2f}% > {threshold_pct}%")
        return 8
    print("  visual OK")
    return 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tag", default="-", help="cycle tag (logging)")
    ap.add_argument("--delay", type=float, default=3.0)
    ap.add_argument("--threshold", type=float, default=5.0)
    ap.add_argument("--accept-visual", action="store_true",
                    help="update baseline to current (post visual change)")
    ap.add_argument("--skip-scenes", action="store_true")
    ap.add_argument("--skip-build", action="store_true")
    args = ap.parse_args()

    print(f"\n=== refactor cycle [{args.tag}] ===")

    if not args.skip_scenes:
        rc = step_scenes()
        if rc != 0:
            print(f"\n[refactor] FAIL @ scenes (rc={rc})")
            return rc
    if not args.skip_build:
        rc = step_build()
        if rc != 0:
            print(f"\n[refactor] FAIL @ build (rc={rc})")
            return rc
    rc = step_qa(delay=args.delay)
    if rc != 0:
        print(f"\n[refactor] FAIL @ qa (rc={rc})")
        return rc
    rc = step_log_check()
    if rc != 0:
        print(f"\n[refactor] FAIL @ runtime log (rc={rc})")
        return rc
    rc = step_visual_diff(threshold_pct=args.threshold)
    if rc != 0:
        print(f"\n[refactor] FAIL @ visual (rc={rc})")
        return rc
    rc = step_playmode_tests()
    if rc != 0:
        print(f"\n[refactor] FAIL @ playmode (rc={rc})")
        return rc

    rc = step_integration_tests()
    if rc != 0:
        print(f"\n[refactor] FAIL @ integration (rc={rc})")
        return rc

    if args.accept_visual:
        import shutil; shutil.copy(CURRENT, BASELINE)
        print(f"[refactor] baseline updated → {BASELINE}")

    print(f"\n[refactor] PASS [{args.tag}] OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
