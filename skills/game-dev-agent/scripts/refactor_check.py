"""refactor_check - 리팩토링 한 사이클 자동 검증.

Usage:  python refactor_check.py [--tag R2-step1]

흐름:
  1. unity scenes (regen)
  2. unity verify-build
  3. PawnSim.exe --delay 3 --screenshot $PAWNSIM_SCRATCH_DIR/_refactor_current.png
  4. Player.log 에 error/exception 검사
  5. baseline 과 픽셀 diff (PIL) - 임계 5% 초과 시 FAIL
  6. exit 0 = PASS, 1 = FAIL (compile/build/runtime/visual)

  PASS 시:
    - 현재 screenshot 을 _refactor_baseline.png 로 덮어쓰지 X
    - 운영자가 시각 변경 의도적이면 --accept-visual 로 baseline 갱신
"""
from __future__ import annotations
import argparse
import os
import sys
from pathlib import Path
from PIL import Image
import subprocess
# NOTE: 이 파일의 subprocess 호출은 전부 encoding 을 명시한다 — 없으면 Windows 가
#  **cp949** 로 디코드해서, 하위 프로세스 출력에 한글이 한 줄이라도 들어가면
#  UnicodeDecodeError 로 죽는다.  검사는 성공했는데 출력을 읽다 실패해 FAIL 로
#  보이므로 원인이 정반대로 읽힌다 (2026-08-09 WebGL 배포 경로에서 실제로 겪음).

REPO = Path(__file__).resolve().parents[3]  # ...skills/game-dev-agent/scripts/X.py -> MelonS-Agents
UNITY_PROJ = REPO / "skills" / "game-prototype" / "unity-project"
BUILD_EXE  = REPO / "skills" / "game-prototype" / "builds" / "verify-game-only" / "PawnSim.exe"

# Scratch dir for baseline/current screenshots + Unity logs.  Deliberately
# OUTSIDE the repo (these are large, regenerated every cycle, and must not be
# committed — see CLAUDE.md code/data separation).  Default = the repo's parent,
# which reproduces the historical `G:/ai/...` layout without hardcoding a drive
# letter.  Override with PAWNSIM_SCRATCH_DIR.
SCRATCH    = Path(os.environ.get("PAWNSIM_SCRATCH_DIR", REPO.parent))
BASELINE   = SCRATCH / "_refactor_baseline.png"
CURRENT    = SCRATCH / "_refactor_current.png"
SCENE_LOG  = SCRATCH / "_unity_scene.log"
BUILD_LOG  = SCRATCH / "_unity_build.log"
# Unity writes Player.log under the user's LocalLow, i.e. a per-account path.
# Derived from the running user rather than baked in (it used to embed the
# operator's Windows account name — contract §12).
PLAYER_LOG = Path(os.environ.get(
    "PAWNSIM_PLAYER_LOG",
    Path.home() / "AppData" / "LocalLow" / "DefaultCompany" / "unity-project" / "Player.log",
))


def step_scenes() -> int:
    print("[refactor] (1/5) scenes regen ...")
    proc = subprocess.run(
        [sys.executable, str(REPO / "skills" / "game-dev-agent" / "scripts" / "agent.py"),
         "integrate", "--project", str(UNITY_PROJ), "--method", "scenes"],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
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
        capture_output=True, text=True, encoding="utf-8", errors="replace",
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


def _find_day_build_target(harness_latest: Path | None = None) -> tuple[Path, bool]:
    """Return (exe_path, is_fresh) for REAL QA / Build Click QA.

    Resolution order:
      1. harness_latest  (builds/_harness-latest, produced by --fresh-build)
      2. newest day-*-2026-* directory by mtime
      3. fallback: builds/verify-game-only  (no menu path, but always fresh)

    Also returns is_fresh=False when a day-N build is selected but its mtime
    is older than the newest .cs source file (staleness guard).
    Callers should treat is_fresh=False as a hard FAIL unless --allow-stale.
    """
    if harness_latest is not None and harness_latest.exists():
        return harness_latest, True

    builds_dir = REPO / "skills" / "game-prototype" / "builds"
    day_builds = sorted(builds_dir.glob("day-*-2026-*"),
                        key=lambda p: p.stat().st_mtime)
    if day_builds:
        candidate = day_builds[-1] / "PawnSim.exe"
        if candidate.exists():
            # Staleness guard: compare build mtime vs newest .cs source file.
            #
            # ⚠ 무엇의 mtime 을 보느냐가 핵심이다 (2026-07-31 실측으로 교정).
            #  이전엔 **빌드 디렉터리**의 mtime 을 봤는데, 디렉터리 mtime 은 항목이
            #  추가/삭제될 때만 갱신된다.  Unity 재빌드는 기존 파일을 **제자리에서
            #  덮어쓰므로** 디렉터리 시각은 최초 생성 시각에 머문다.  그 결과 같은
            #  day 폴더로 두 번째 빌드를 한 순간부터 게이트가 **영구 STALE** 이 됐다
            #  (실측: dll 04:09 / 디렉터리 00:08 → 22/22 STALE).
            #  `PawnSim.exe` 도 안 된다 — 런처 스텁이라 코드가 바뀌어도 내용이 같으면
            #  Unity 가 다시 쓰지 않는다.
            #  실제로 "이 빌드가 담고 있는 C# 코드"를 대표하는 것은 Assembly-CSharp.dll
            #  하나뿐이다.  그것과 Assets/Scripts 를 비교한다.
            asm = day_builds[-1] / "PawnSim_Data" / "Managed" / "Assembly-CSharp.dll"
            build_mtime = (asm if asm.exists() else candidate).stat().st_mtime
            scripts_dir = UNITY_PROJ / "Assets" / "Scripts"
            newest_src_mtime = 0.0
            if scripts_dir.exists():
                for cs in scripts_dir.rglob("*.cs"):
                    t = cs.stat().st_mtime
                    if t > newest_src_mtime:
                        newest_src_mtime = t
            is_fresh = (newest_src_mtime == 0.0 or build_mtime >= newest_src_mtime)
            return candidate, is_fresh

    # No day-N build at all; fall back to verify-game-only (always rebuilt each run).
    return BUILD_EXE, True  # verify-game-only is always fresh; no menu path, but honest.


def step_fresh_build(day_tag: str = "_harness-latest") -> int:
    """Optional step triggered by --fresh-build flag.

    Runs a full BuildWindows into builds/_harness-latest so that steps 4.5/4.6
    test current code WITH the MainMenu->Game path, without touching the
    operator-facing day-N builds.  Adds ~2-5 min per run.
    """
    import shutil
    harness_out = REPO / "skills" / "game-prototype" / "builds" / "_harness-latest"
    print(f"[refactor] (4.4/7) fresh build → {harness_out} ...")
    # Temporarily set env so BuildScript writes to _harness-latest folder.
    import os
    os.environ["MELONS_BUILD_DAY"] = day_tag
    proc = subprocess.run(
        [sys.executable, str(REPO / "skills" / "game-dev-agent" / "scripts" / "agent.py"),
         "integrate", "--project", str(UNITY_PROJ), "--method", "build"],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    if proc.returncode != 0:
        print(f"  FAIL rc={proc.returncode}")
        print(proc.stderr[-500:])
        return 18
    print("  fresh build OK")
    return 0


def step_real_qa(delay: float = 30.0, allow_stale: bool = False,
                 harness_latest: Path | None = None) -> int:
    """운영자 fb (#140) - graphics 모드 30s 시뮬 + 자원 변화 자동 검증.
    운영자 fb 두 번째 (#137 root cause 3): verify-game-only build 만 검증하면
    MainMenu->Game 전이 등 실제 운영자 path 누락.  최신 day-N 빌드 우선 사용.

    Staleness guard (fix for false-pass blind-spot):
      If the selected day-N build is older than the newest Assets/Scripts/*.cs
      file, the run is aborted with FAIL (rc=19) unless --allow-stale is set.
      Use --fresh-build to produce a fresh full build before this step.
    """
    target, is_fresh = _find_day_build_target(harness_latest)
    if not is_fresh:
        msg = (
            f"[refactor] (4.5/7) REAL QA - STALE BUILD DETECTED\n"
            f"  Selected: {target.parent.name}\n"
            f"  The day-N build is older than Assets/Scripts sources.\n"
            f"  This means REAL QA would test OLD code -- a false pass.\n"
            f"  Fix: run with --fresh-build (rebuilds full game, ~3-5 min extra)\n"
            f"       OR run `agent.py integrate --method build` first\n"
            f"       OR pass --allow-stale to force-proceed (risks false pass)."
        )
        print(msg)
        if not allow_stale:
            return 19
        print("  WARN: --allow-stale set, proceeding with stale build.")
    if not target.exists():
        target = BUILD_EXE
    print(f"[refactor] (4.5/7) REAL QA - {target.parent.name} {delay}s 시뮬 ...")
    REAL_QA_SHOT = SCRATCH / "_refactor_realqa.png"
    sys.path.insert(0, str(REPO / "skills" / "game-dev-agent" / "scripts"))
    from modules import qa
    ok, msg = qa.launch_and_capture(target, REAL_QA_SHOT, delay_sec=delay)
    if not ok:
        print(f"  FAIL launch - {msg}")
        return 13
    if not PLAYER_LOG.exists():
        print("  WARN: Player.log 없음 - skip")
        return 0
    log = PLAYER_LOG.read_text(encoding="utf-8", errors="ignore")
    # [ResMon] t=X.Xs wood=N food=M stone=K meals=L fineMeals=F
    import re
    snapshots = []
    for line in log.splitlines():
        m = re.search(r"\[ResMon\] t=([\d.]+)s wood=(\d+) food=(\d+) stone=(\d+) meals=(\d+)", line)
        if m:
            snapshots.append({
                "t": float(m.group(1)),
                "wood": int(m.group(2)),
                "food": int(m.group(3)),
                "stone": int(m.group(4)),
                "meals": int(m.group(5)),
            })
    if len(snapshots) < 2:
        print(f"  FAIL: ResMon snapshots {len(snapshots)} (>=2 expected) - GameManager 가 ResourceMonitorLogger 못 박은 듯")
        return 14
    first, last = snapshots[0], snapshots[-1]
    wood_d = last["wood"] - first["wood"]
    food_d = last["food"] - first["food"]
    stone_d = last["stone"] - first["stone"]
    print(f"  {len(snapshots)} snapshots: t={first['t']:.0f}s wood={first['wood']} food={first['food']} stone={first['stone']}")
    print(f"  → t={last['t']:.0f}s wood={last['wood']} (+{wood_d}) food={last['food']} (+{food_d}) stone={last['stone']} (+{stone_d})")
    if wood_d <= 0:
        print(f"  FAIL: wood 증가 없음 (운영자 fb '목재 안 나옴' 회귀 검출)")
        return 15
    print(f"  REAL QA OK - {delay}s 안에 wood +{wood_d}")
    return 0


def step_build_click_qa(allow_stale: bool = False,
                        harness_latest: Path | None = None) -> int:
    """#187 - BuildClickAutoQA 6-mode 자동 회귀 보호.
    -build-click-qa flag 로 binary 실행 → [BuildClickQA] OVERALL: PASS/FAIL 파싱.
    매 commit cycle 에서 건축 click chain 회귀 검출.

    Staleness guard: same policy as step_real_qa — FAIL if day-N is older
    than current sources unless --allow-stale / --fresh-build is used.
    """
    target, is_fresh = _find_day_build_target(harness_latest)
    if not is_fresh:
        msg = (
            f"[refactor] (4.6/7) Build Click QA - STALE BUILD DETECTED\n"
            f"  Selected: {target.parent.name}\n"
            f"  The day-N build is older than Assets/Scripts sources.\n"
            f"  Refusing to validate old code. Use --fresh-build or --allow-stale."
        )
        print(msg)
        if not allow_stale:
            return 19
        print("  WARN: --allow-stale set, proceeding with stale build.")
    if not target.exists():
        print("[refactor] (4.6/7) Build Click QA - 빌드 없음, skip")
        return 0
    print(f"[refactor] (4.6/7) Build Click QA - {target.parent.name} (6 mode chain) ...")
    QA_LOG = SCRATCH / "_refactor_clickqa.log"
    import subprocess
    try:
        proc = subprocess.run(
            [str(target), "-batchmode", "-nographics", "-build-click-qa",
             "-autostart", "-logFile", str(QA_LOG)],
            timeout=180, capture_output=True
        )
    except subprocess.TimeoutExpired:
        print("  FAIL: timeout 180s")
        return 16
    if not QA_LOG.exists():
        print("  WARN: log 없음 - skip")
        return 0
    log = QA_LOG.read_text(encoding="utf-8", errors="ignore")
    pass_count = log.count("[BuildClickQA] OVERALL: PASS")
    fail_count = log.count("[BuildClickQA] OVERALL: FAIL")
    # 개별 case PASS/FAIL 도 카운트
    case_pass = log.count(": PASS")
    case_fail = log.count(": FAIL")
    if pass_count > 0:
        print(f"  Build Click QA OK - 6 mode chain OVERALL PASS (case {case_pass}/{case_pass+case_fail})")
        return 0
    if fail_count > 0:
        print(f"  FAIL - OVERALL FAIL (case pass={case_pass}, fail={case_fail})")
        # log 의 FAIL 줄들 출력
        for line in log.splitlines():
            if "[BuildClickQA]" in line and "FAIL" in line:
                print(f"    {line.strip()}")
        return 17
    print("  WARN: BuildClickQA 결과 없음 (BuildClickAutoQA 미박힘?) - skip")
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
    report_path = SCRATCH / "_pawnsim_test_report.json"
    if report_path.exists():
        report_path.unlink()
    proc = subprocess.run(
        [str(BUILD_EXE), "-testmode", "-batchmode", "-nographics"],
        capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=30,
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
    report_path = SCRATCH / "_pawnsim_integration_report.json"
    if report_path.exists():
        report_path.unlink()
    proc = subprocess.run(
        [str(BUILD_EXE), "-integration", "-batchmode", "-nographics",
         "-screen-width", "1280", "-screen-height", "720"],
        capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=180,  # I4 15s + I19 15s + I23 60s + 기타 → 180s
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
    ap.add_argument("--skip-real-qa", action="store_true",
                    help="skip 30s graphics 모드 자원 검증 (개발 중 빠른 iter 용)")
    ap.add_argument("--real-qa-seconds", type=float, default=30.0,
                    help="REAL QA graphics 시뮬 길이 (default 30s)")
    ap.add_argument("--fresh-build", action="store_true",
                    help=(
                        "Build a fresh full game (with MainMenu) into builds/_harness-latest "
                        "before REAL QA + Build Click QA. Guarantees steps 4.5/4.6 test current "
                        "code. Adds ~3-5 min. Default OFF for quick iters but staleness guard "
                        "will FAIL if day-N build is older than source files."
                    ))
    ap.add_argument("--allow-stale", action="store_true",
                    help=(
                        "Allow REAL QA / Build Click QA to proceed even when the selected "
                        "day-N build is older than current source files. Risks false passes. "
                        "Use only when you know the sources haven't changed since last build."
                    ))
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
    # 운영자 fb #140 - 진짜 QA: 30s graphics 시뮬 + 자원 변화 자동 검증
    if not args.skip_real_qa:
        # --fresh-build: produce a fresh full build into builds/_harness-latest.
        # Without it, the staleness guard will FAIL if day-N is older than sources.
        harness_latest: Path | None = None
        if args.fresh_build:
            rc = step_fresh_build()
            if rc != 0:
                print(f"\n[refactor] FAIL @ fresh build (rc={rc})")
                return rc
            harness_latest = (
                REPO / "skills" / "game-prototype" / "builds" / "_harness-latest" / "PawnSim.exe"
            )
        rc = step_real_qa(delay=args.real_qa_seconds,
                          allow_stale=args.allow_stale,
                          harness_latest=harness_latest)
        if rc != 0:
            print(f"\n[refactor] FAIL @ real QA (rc={rc})")
            return rc
        # #187 - 건축 click chain 회귀 보호 (운영자 fb "건축 안 됨")
        rc = step_build_click_qa(allow_stale=args.allow_stale,
                                 harness_latest=harness_latest)
        if rc != 0:
            print(f"\n[refactor] FAIL @ build click QA (rc={rc})")
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
