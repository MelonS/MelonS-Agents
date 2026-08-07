# -*- coding: utf-8 -*-
"""latest_build.py — **가장 최근 빌드**의 실행 파일 경로를 돌려준다.

계기 (2026-08-07, 하루에 두 번째로 같은 함정): 재미 점수의 '긴장' 축이 0/15 라
습격 타이밍을 **네 번** 고쳤다.  코드 기본값 → 씬 직렬화 값 → 시각 지터 →
진단 로그.  네 번 다 재측정했고 네 번 다 0건이었다.

진짜 원인은 다섯 번째였다:

```
빌드 폴더 = builds/day-X-<오늘날짜>/
측정에 쓴 경로 = builds/day-X-2026-08-02/PawnSim.exe   ← 5일 전 exe
```

`BuildScript` 는 폴더 이름에 **날짜를 스탬프**한다.  날짜가 바뀌면 새 빌드는 새
폴더로 나가는데, 하드코딩한 경로는 옛 폴더를 계속 가리킨다.  빌드는 성공하고
(errors: 0) 측정도 정상 종료하고, 그냥 **어제 게임을 재고 있다.**

이건 전에도 한 번 밟은 함정이라 메모까지 남아 있었는데 같은 실수를 했다.
그래서 메모가 아니라 **도구**로 만든다 — 경로를 손으로 적을 수 없게 한다.

usage:
  python latest_build.py                 # 최신 Windows 빌드 exe 경로
  python latest_build.py --webgl         # 최신 WebGL 빌드 폴더
  python latest_build.py --check         # 소스보다 낡았으면 exit 1

  # 셸에서
  EXE="$(python skills/game-dev-agent/scripts/latest_build.py)"
"""
from __future__ import annotations
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent.parent
BUILDS = REPO / "skills" / "game-prototype" / "builds"
SCRIPTS = REPO / "skills" / "game-prototype" / "unity-project" / "Assets" / "Scripts"


def latest_exe():
    cands = sorted(BUILDS.glob("day-*/PawnSim.exe"),
                   key=lambda p: p.stat().st_mtime, reverse=True)
    return cands[0] if cands else None


def latest_webgl():
    cands = [d for d in BUILDS.glob("day-*-webgl") if (d / "index.html").exists()]
    cands.sort(key=lambda p: p.stat().st_mtime, reverse=True)
    return cands[0] if cands else None


def is_stale(exe: Path):
    """빌드가 소스보다 낡았는가.

    실행 파일이 아니라 `Assembly-CSharp.dll` 을 본다 — PawnSim.exe 는 런처
    스텁이라 코드가 바뀌어도 내용이 같으면 Unity 가 다시 쓰지 않는다.
    (이 구분을 몰라 게이트가 며칠간 STALE 로 아무것도 검증하지 않은 전례가 있다.)"""
    dll = exe.parent / "PawnSim_Data" / "Managed" / "Assembly-CSharp.dll"
    if not dll.exists():
        return True, "Assembly-CSharp.dll 없음"
    newest = max((p.stat().st_mtime for p in SCRIPTS.rglob("*.cs")), default=0)
    if newest > dll.stat().st_mtime:
        import datetime
        f = lambda t: datetime.datetime.fromtimestamp(t).strftime("%m-%d %H:%M")
        return True, f"소스 {f(newest)} > 빌드 {f(dll.stat().st_mtime)}"
    return False, ""


def main() -> int:
    if "--webgl" in sys.argv:
        d = latest_webgl()
        if d is None:
            print("[latest_build] WebGL 빌드 없음", file=sys.stderr)
            return 1
        print(d)
        return 0

    exe = latest_exe()
    if exe is None:
        print("[latest_build] 빌드 없음", file=sys.stderr)
        return 1

    if "--check" in sys.argv:
        stale, why = is_stale(exe)
        if stale:
            print(f"[latest_build] STALE — {exe.parent.name}: {why}", file=sys.stderr)
            return 1
        print(f"[latest_build] OK — {exe.parent.name}")
        return 0

    print(exe)
    return 0


if __name__ == "__main__":
    sys.exit(main())
