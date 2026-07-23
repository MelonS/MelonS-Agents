"""repro_all - WORKFLOW-V2 재현 시나리오 전체 일괄 실행 (커밋 게이트).

Usage:
  python repro_all.py [--fresh-build] [--filter <substr>] [--build <PawnSim.exe>]

흐름:
  1. --fresh-build 시 빌드 1회 (시나리오마다 재빌드 금지 — Unity 빌드 직렬·고비용)
  2. repro-scenarios/*.json 을 이름순 직렬 실행 (리포트 파일이 전역 공유라 병렬 금지)
  3. 시나리오별 timeout 은 JSON 의 "timeoutSec" (없으면 240)
  4. 요약표 출력.  exit 0 = 전체 PASS → 커밋 가능.

PASS 한 시나리오는 영구 보존 = 회귀 테스트 (WORKFLOW-V2 규칙 1).
"""
from __future__ import annotations
import argparse
import json
import subprocess
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")

import refactor_check as rc

SCEN_DIR = rc.REPO / "skills" / "game-prototype" / "repro-scenarios"
RUNNER = Path(__file__).parent / "repro_run.py"

# 2단 게이트 (2026-07-24) — 풀 게이트 30~60분이 "게이트 생략" 사고를 유발해 도입.
#  스모크 = 코어 조작 경로 4종(이동/벌목메뉴/선택림전용/4축기본)만 — 매 커밋 최소선.
#  풀 게이트(무인자)는 세션 말/야간/행동로직 변경 시 의무.
SMOKE_SET = [
    "p0-basics-4axis.json",
    "p0-pawn-move.json",
    "p0-chop-menu.json",
    "p1-chop-selected-only.json",
]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--fresh-build", action="store_true")
    ap.add_argument("--filter", default="")
    ap.add_argument("--smoke", action="store_true",
                    help="스모크 세트만 실행 (매 커밋용 빠른 게이트)")
    ap.add_argument("--build")
    args = ap.parse_args()

    # '_' 접두 = 게이트 비포함 (휴먼라이크 플레이테스트/실험 시나리오 — 길고 판정이
    #  사후 리뷰형이라 커밋 게이트에 안 넣는다.  실행은 repro_run 직접 호출).
    scenarios = sorted(p for p in SCEN_DIR.glob("*.json")
                       if args.filter in p.name and not p.name.startswith("_"))
    if args.smoke:
        scenarios = [p for p in scenarios if p.name in SMOKE_SET]
    if not scenarios:
        print(f"[repro_all] 시나리오 없음: {SCEN_DIR} (filter='{args.filter}')")
        return 2

    if args.fresh_build:
        # 2026-06-11 — 씬 재생성을 fresh build 에 선행: 이전엔 build 만 돌아 SceneSetup
        #  레벨 변경(직렬화 노브/베이크 UI/지형)이 빌드에 조용히 미반영됐다 (스테일 씬으로
        #  습격 grace 9 가 살아남아 day2 습격이 발화 안 한 사건).  씬+빌드가 한 단위.
        if rc.step_scenes() != 0:
            print("[repro_all] FAIL: scenes regen 실패")
            return 2
        if rc.step_fresh_build() != 0:
            print("[repro_all] FAIL: fresh build 실패")
            return 2

    results: list[tuple[str, bool]] = []
    for scen in scenarios:
        try:
            timeout = json.loads(scen.read_text(encoding="utf-8")).get("timeoutSec", 240)
        except Exception:
            timeout = 240
        cmd = [sys.executable, str(RUNNER), str(scen), "--timeout", str(timeout)]
        if args.build:
            cmd += ["--build", args.build]
        print(f"\n[repro_all] ── {scen.name} (timeout {timeout}s) " + "─" * 20)
        rcode = subprocess.run(cmd, cwd=str(RUNNER.parent)).returncode
        results.append((scen.name, rcode == 0))

    n_fail = sum(1 for _, ok in results if not ok)
    print("\n[repro_all] ━━━ 요약 ━━━")
    for name, ok in results:
        print(f"  {'PASS' if ok else 'FAIL'}  {name}")
    print(f"[repro_all] OVERALL: {'PASS' if n_fail == 0 else f'FAIL ({n_fail}/{len(results)})'}")
    return 0 if n_fail == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
