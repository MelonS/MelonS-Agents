#!/usr/bin/env python3
"""check-asset-refs.py — 씬·에셋의 스프라이트/스크립트 참조가 실제로 해석되는지 검사.

왜 필요한가: Unity 는 참조가 끊긴 스프라이트를 **조용히 안 그린다**. 예외도 콘솔
에러도 없다.  그래서 `repro_all.py`(행동)·`webgl_smoke.py`(부팅·콘솔) 게이트를 전부
통과하면서 화면에서 지형이 통째로 사라질 수 있다 — 2026-07-25 실제 사고
(`docs/incident-2026-07-26-ts-guid-clobber.md`).  이 스크립트가 그 사각지대를 덮는다.

usage:
  python games/pawnsim/scripts/check-asset-refs.py            # 작업트리
  python games/pawnsim/scripts/check-asset-refs.py --rev HEAD~5
  python games/pawnsim/scripts/check-asset-refs.py --all      # 씬·에셋 전수

exit 0 = 미해석 참조 0건.
"""
from __future__ import annotations
import argparse
import re
import subprocess
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

REPO = Path(__file__).resolve().parents[3]
UP = REPO / "games" / "pawnsim" / "unity-project"
GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")

# Unity 내장 리소스는 .meta 가 없다 — guid 접미사로 식별한다.
BUILTIN_SUFFIX = ("f000000000000000", "e000000000000000", "d000000000000000")

DEFAULT_TARGETS = [
    "Assets/Scenes/Game.unity",
    "Assets/Scenes/MainMenu.unity",
    "Assets/Tiles/Grass.asset",
    "Assets/Tiles/Dirt.asset",
    "Assets/Tiles/Water.asset",
]


def known_guids() -> dict[str, str]:
    """프로젝트가 해석할 수 있는 모든 guid.

    Assets/·Packages/ 의 .meta 뿐 아니라 **Library/PackageCache** 도 반드시 포함해야
    한다.  uGUI 의 Image/Text 처럼 패키지에 사는 스크립트 참조가 여기에만 있어서,
    빼먹으면 정상 참조를 깨진 것으로 오검출한다 (첫 구현에서 10건 오검출).
    """
    out: dict[str, str] = {}
    roots = [UP / "Assets", UP / "Packages", UP / "Library" / "PackageCache"]
    for root in roots:
        if not root.is_dir():
            continue
        for m in root.rglob("*.meta"):
            try:
                txt = m.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            g = GUID_RE.search(txt)
            if g and g.group(1) not in out:
                out[g.group(1)] = str(m.relative_to(UP))
    return out


def read_target(rel: str, rev: str | None) -> str | None:
    gitpath = f"games/pawnsim/unity-project/{rel}"
    if rev:
        r = subprocess.run(["git", "-C", str(REPO), "show", f"{rev}:{gitpath}"],
                           capture_output=True)
        if r.returncode != 0:
            return None
        return r.stdout.decode("utf-8", "replace")
    p = UP / rel
    return p.read_text(encoding="utf-8", errors="replace") if p.is_file() else None


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--rev", help="git rev (생략 시 작업트리)")
    ap.add_argument("--all", action="store_true",
                    help="Assets 하위 .unity/.asset/.prefab 전수 검사")
    args = ap.parse_args()

    if not UP.is_dir():
        print(f"✗ unity-project 없음: {UP}")
        return 2

    known = known_guids()
    if len(known) < 100:
        print(f"⚠ 알려진 guid 가 {len(known)}개뿐 — Library/PackageCache 미생성 상태일 수 있다.")
        print("  (프로젝트를 한 번도 임포트하지 않은 체크아웃이면 패키지 스크립트 참조가")
        print("   오검출된다.  Unity 를 한 번 열거나 빌드한 뒤 다시 실행할 것.)")

    if args.all and not args.rev:
        targets = sorted(
            str(p.relative_to(UP)).replace("\\", "/")
            for ext in ("*.unity", "*.asset", "*.prefab")
            for p in (UP / "Assets").rglob(ext))
    else:
        targets = DEFAULT_TARGETS

    label = args.rev or "작업트리"
    print(f"=== 참조 건전성: {label} (알려진 guid {len(known)}개, 대상 {len(targets)}개) ===")

    total_bad = 0
    for rel in targets:
        txt = read_target(rel, args.rev)
        if txt is None:
            continue
        guids = set(GUID_RE.findall(txt))
        bad = sorted(g for g in guids
                     if g not in known and not g.endswith(BUILTIN_SUFFIX))
        if bad:
            total_bad += len(bad)
            print(f"  ✗ {rel}: 참조 {len(guids)}종 / **미해석 {len(bad)}종**")
            for g in bad[:15]:
                print(f"        {g}")
            if len(bad) > 15:
                print(f"        … 외 {len(bad) - 15}종")
        else:
            print(f"  ✔ {rel}: 참조 {len(guids)}종 / 미해석 0")

    print()
    if total_bad:
        print(f"FAIL — 미해석 참조 {total_bad}종.")
        print("끊긴 스프라이트는 조용히 안 그려진다.  ts_*.png.meta 가 커밋돼 있는지,")
        print("병합이 GUID 를 덮지 않았는지 확인할 것 (docs/incident-2026-07-26-*.md).")
        return 1
    print("PASS — 미해석 참조 0종.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
