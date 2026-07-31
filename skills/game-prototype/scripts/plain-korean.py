#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""plain-korean.py — 화면 문구의 전문용어를 쉬운 말로 바꾼다.

계기 (2026-08-01 운영자): "콜로니, 심 이런 안 쓰는 단어들 이해하기 쉬운 단어로
바꿔야함" / "한국어 표현 어색한것 수정".

'콜로니스트' 는 이 장르를 아는 사람에게만 통하는 말이다.  처음 보는 심사자에게는
**주민**이 즉시 이해된다.  마찬가지로 '콜로니' → '마을'.

무엇을 바꾸고 무엇을 안 바꾸나:
  · 바꾼다  — 화면에 나오는 문자열 리터럴 (툴팁·알림·패널·이벤트 문구)
  · 안 바꾼다 — 주석과 Debug.Log.  주석은 이 레포의 설계 기록이라 용어를 바꾸면
    과거 맥락이 끊긴다.  Debug.Log 는 개발자용이라 화면에 안 나온다.

usage:
  python skills/game-prototype/scripts/plain-korean.py [--dry]
"""
from __future__ import annotations
import io
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SCRIPTS = Path(__file__).resolve().parents[1] / "unity-project" / "Assets" / "Scripts"

# 순서 중요 — 긴 것부터 (조사 결합형이 먼저 잡혀야 한다).
SUBS = [
    ("콜로니스트들", "주민들"), ("콜로니스트", "주민"),
    ("콜로니로", "마을로"), ("콜로니에", "마을에"), ("콜로니가", "마을이"),
    ("콜로니를", "마을을"), ("콜로니의", "마을의"), ("콜로니", "마을"),
    # 장르 은어 — 화면에 남아 있으면 처음 보는 사람이 못 읽는다.
    ("굶는 림", "굶는 주민"), ("idle 림", "노는 주민"),
    ("림 ", "주민 "), (" 림이", " 주민이"), (" 림을", " 주민을"),
    # 조사 오류 (위 치환이 만든 것 포함) — '주민를/이가' 류.
    ("주민를", "주민을"), ("주민가", "주민이"), ("주민는", "주민은"),
    ("마을를", "마을을"), ("마을가", "마을이"),
    # 영어·기호 잔재
    ("세이브", "저장 파일"), ("UI 팔레트", "화면 색상"),
    ("페널티", "불이익"), ("× UI 위 클릭", "화면 UI 위를 눌렀습니다"),
    ("전무", "없음"),
]

STR_RE = re.compile(r'"([^"\\]*(?:\\.[^"\\]*)*)"')


def main() -> int:
    dry = "--dry" in sys.argv
    changed: dict[str, int] = {}
    for f in sorted(SCRIPTS.rglob("*.cs")):
        src = io.open(f, encoding="utf-8").read()
        cnt = [0]

        def rep(m: re.Match) -> str:
            t = o = m.group(1)
            for a, b in SUBS:
                t = t.replace(a, b)
            if t != o:
                cnt[0] += 1
            return '"' + t + '"'

        out = []
        for line in src.split("\n"):
            ls = line.lstrip()
            if ls.startswith("//") or "Debug.Log" in line:
                out.append(line)          # 주석·개발 로그는 보존 (위 주석 참조)
            else:
                out.append(STR_RE.sub(rep, line))
        if cnt[0]:
            changed[str(f.relative_to(SCRIPTS))] = cnt[0]
            if not dry:
                io.open(f, "w", encoding="utf-8").write("\n".join(out))

    for name, n in sorted(changed.items(), key=lambda kv: -kv[1])[:15]:
        print(f"  {n:3d}  {name}")
    print(f"{'(dry) ' if dry else ''}총 {sum(changed.values())}곳 / {len(changed)}파일")
    return 0


if __name__ == "__main__":
    sys.exit(main())
