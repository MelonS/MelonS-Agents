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
#  실패 사유 보존용 — 게이트 요약만으로는 원인을 못 찾는다 (2026-07-30).
LOG_DIR = rc.REPO / "skills" / "game-prototype" / "art-out" / "repro-logs"
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

    # rcode 를 그대로 보관한다.  repro_run 은 이미 2=빌드 문제(주로 STALE)와
    #  1=시나리오 실패를 구분해 돌려주는데, 예전엔 여기서 `rcode == 0` 으로 뭉개
    #  **stale 거부가 진짜 실패와 똑같이 FAIL 로 찍혔다**.  2026-07-29 에 실제로
    #  이것 때문에 "4건 회귀"라는 잘못된 결론을 냈다 — 원인은 스위트가 도는 중에
    #  .cs 를 편집해 뒤늦게 시작한 시나리오들이 stale 판정을 받은 것뿐이었다.
    results: list[tuple[str, int]] = []
    fail_lines: dict[str, list[str]] = {}
    for scen in scenarios:
        try:
            timeout = json.loads(scen.read_text(encoding="utf-8")).get("timeoutSec", 240)
        except Exception:
            timeout = 240
        cmd = [sys.executable, str(RUNNER), str(scen), "--timeout", str(timeout)]
        if args.build:
            cmd += ["--build", args.build]
        print(f"\n[repro_all] ── {scen.name} (timeout {timeout}s) " + "─" * 20)
        # 2026-07-30 — 시나리오별 출력을 파일로도 남긴다.
        #  게이트가 FAIL 을 내도 요약만 남고 **어느 단계에서 왜 실패했는지가 사라졌다**
        #  (콘솔 버퍼가 앞부분을 잃는다).  그래서 매번 개별 재실행으로 사유를 다시
        #  찾아야 했고, 개별로는 PASS 가 나오는 경우엔 원인 추적이 막혔다.
        #  실패는 스스로를 설명해야 한다 — 로그를 남기고, 요약에 실패 줄을 붙인다.
        proc = subprocess.run(cmd, cwd=str(RUNNER.parent),
                              capture_output=True, text=True,
                              encoding="utf-8", errors="replace")
        rcode = proc.returncode
        out = (proc.stdout or "") + (proc.stderr or "")
        print(out, end="")
        LOG_DIR.mkdir(parents=True, exist_ok=True)
        (LOG_DIR / f"{scen.stem}.log").write_text(out, encoding="utf-8")
        if rcode != 0:
            fail_lines[scen.name] = [ln.strip() for ln in out.splitlines()
                                     if "FAIL" in ln or "STALE" in ln][:6]
        results.append((scen.name, rcode))

    def label(code: int) -> str:
        return {0: "PASS", 2: "STALE"}.get(code, "FAIL")

    n_fail = sum(1 for _, c in results if c == 1)
    n_stale = sum(1 for _, c in results if c == 2)
    print("\n[repro_all] ━━━ 요약 ━━━")
    for name, code in results:
        print(f"  {label(code):5s} {name}")
        for ln in fail_lines.get(name, []):
            print(f"        {ln}")
    if n_stale:
        # STALE 은 "검증을 못 했다"이지 "검증에 실패했다"가 아니다.  그래서 실패
        #  건수와 분리해 찍고, 원인(스위트 중 소스 편집)을 그 자리에서 말해 준다.
        print(f"[repro_all] ⚠ STALE {n_stale}건 — 이 시나리오들은 **실행되지 않았다**.")
        print("[repro_all]   원인: 스위트 실행 중 .cs 가 편집되어 빌드가 소스보다 오래됨.")
        print("[repro_all]   조치: 스위트가 도는 동안 소스를 건드리지 말 것. --fresh-build 로 재실행.")
    if n_fail == 0 and n_stale == 0:
        overall = "PASS"
    elif n_fail == 0:
        overall = f"INCOMPLETE (stale {n_stale}/{len(results)})"
    else:
        overall = f"FAIL ({n_fail}/{len(results)})" + (f", stale {n_stale}" if n_stale else "")
    print(f"[repro_all] OVERALL: {overall}")
    return 0 if (n_fail == 0 and n_stale == 0) else 1


if __name__ == "__main__":
    sys.exit(main())
