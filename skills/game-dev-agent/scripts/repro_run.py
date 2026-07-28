"""repro_run - WORKFLOW-V2 운영자 버그 재현 시나리오 러너.

Usage:
  python repro_run.py <scenario.json> [--fresh-build] [--build <PawnSim.exe>]
                      [--timeout 240]

흐름:
  1. 빌드 결정 — --build 명시 > builds/_harness-latest (--fresh-build 시 재생성)
     > 최신 day-N (stale 가드: 소스보다 오래되면 거부)
  2. PawnSim.exe -repro <scenario> 실행 (그래픽 모드 — GraphicRaycaster/스크린샷 필요,
     -nographics 금지.  ReproHarness 가 runInBackground=ON + 자가 종료)
  3. _repro_report.json 파싱 → 스텝별 PASS/FAIL 출력, Player.log exception 스캔
  4. exit 0 = 전체 PASS (재현 시나리오가 '버그 수정됨'을 검증)
     exit 1 = FAIL (버그 재현됨 — 리포트의 FAIL 스텝이 곧 재현 증거)

시나리오 형식은 ReproHarness.cs 헤더 주석 참조.
시나리오 파일은 skills/game-prototype/repro-scenarios/ 에 영구 보존 (= 회귀 테스트).
"""
from __future__ import annotations
import argparse
import json
import subprocess
import sys
from pathlib import Path

# Windows cp949 콘솔에서 — 같은 비 cp949 문자 print 가 UnicodeEncodeError 로 죽는 것 방지
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")

import refactor_check as rc

REPO = rc.REPO
REPORT = Path("G:/ai/_repro_report.json")
SHOTS = Path("G:/ai/_repro_shots")
RUN_LOG = Path("G:/ai/_repro_run.log")   # main() 에서 시나리오별 경로로 재설정


def resolve_build(args) -> Path | None:
    if args.build:
        p = Path(args.build)
        return p if p.exists() else None
    harness = REPO / "skills" / "game-prototype" / "builds" / "_harness-latest" / "PawnSim.exe"
    if args.fresh_build:
        if rc.step_fresh_build() != 0:
            return None
        # MELONS_BUILD_DAY 가 Unity 까지 전파 안 되는 환경에선 _harness-latest 대신
        #  day-X-<오늘> 로 나간다 — 아래 최신 day-* 탐색이 방금 빌드를 집는다.
        if harness.exists():
            return harness
    target, is_fresh = rc._find_day_build_target(harness if harness.exists() else None)
    if not is_fresh:
        print(f"[repro] STALE BUILD: {target.parent.name} 가 소스보다 오래됨 — --fresh-build 필요")
        return None
    return target


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("scenario")
    ap.add_argument("--fresh-build", action="store_true")
    ap.add_argument("--build")
    ap.add_argument("--timeout", type=int, default=None)
    ap.add_argument("--seed", type=int, default=None,
                    help="전역 난수 시드 교체 (기본은 ReproHarness 내장값 20260729)")
    args = ap.parse_args()

    scenario = Path(args.scenario).resolve()
    if not scenario.exists():
        print(f"[repro] scenario 없음: {scenario}")
        return 2
    if args.timeout is None:
        # 시나리오가 자기 실행시간을 안다 — JSON "timeoutSec" (harness 는 무시하는 키)
        try:
            args.timeout = json.loads(scenario.read_text(encoding="utf-8")).get("timeoutSec", 240)
        except Exception:
            args.timeout = 240
    # 시나리오별 로그/샷 분리 — 연속 실행 시 이전 증거 덮어쓰기 방지
    global RUN_LOG, SHOTS
    RUN_LOG = Path(f"G:/ai/_repro_run_{scenario.stem}.log")
    SHOTS = Path(f"G:/ai/_repro_shots/{scenario.stem}")

    exe = resolve_build(args)
    if exe is None or not exe.exists():
        print("[repro] 사용 가능한 빌드 없음")
        return 2

    if REPORT.exists():
        REPORT.unlink()
    print(f"[repro] run {exe.parent.name}/PawnSim.exe -repro {scenario.name} (timeout {args.timeout}s)")
    try:
        subprocess.run(
            [str(exe), "-autostart", "-repro", str(scenario),
             "-repro-report", str(REPORT), "-repro-shotdir", str(SHOTS),
             "-screen-width", "1920", "-screen-height", "1080",
             "-logFile", str(RUN_LOG)]
            + (["-repro-seed", str(args.seed)] if args.seed is not None else []),
            timeout=args.timeout, capture_output=True,
        )
    except subprocess.TimeoutExpired:
        print(f"[repro] FAIL: timeout {args.timeout}s — ReproHarness 가 종료 안 함 (시나리오/씬 멈춤 의심)")
        _tail_log()
        return 1

    if not REPORT.exists():
        print("[repro] FAIL: 리포트 미작성 — harness 미주입(verify-game-only 빌드?) 또는 crash")
        _tail_log()
        return 1

    rep = json.loads(REPORT.read_text(encoding="utf-8"))
    print(f"[repro] scenario '{rep.get('scenario')}' — {len(rep.get('results', []))} steps")
    n_fail = 0
    for r in rep.get("results", []):
        ok = r.get("passed")
        if not ok:
            n_fail += 1
        print(f"  [{r.get('index'):>2}] {'PASS' if ok else 'FAIL'}  {r.get('op'):<24} {r.get('detail')}")

    # Player.log 급 exception 스캔 (step_log_check 와 동일 정책)
    if RUN_LOG.exists():
        bad = [ln.strip() for ln in RUN_LOG.read_text(encoding="utf-8", errors="ignore").splitlines()
               if any(t in ln for t in ("NullReferenceException", "MissingReferenceException",
                                        "ArgumentOutOfRangeException", "error CS"))]
        if bad:
            print(f"[repro] WARN: 런타임 예외 {len(bad)}건:")
            for b in bad[:5]:
                print(f"    {b}")

    overall = rep.get("overallPass") and n_fail == 0
    print(f"[repro] OVERALL: {'PASS' if overall else f'FAIL ({n_fail} step)'}  shots={SHOTS}")
    return 0 if overall else 1


def _tail_log():
    if RUN_LOG.exists():
        lines = RUN_LOG.read_text(encoding="utf-8", errors="ignore").splitlines()
        for ln in lines[-15:]:
            print(f"    {ln}")


if __name__ == "__main__":
    sys.exit(main())
