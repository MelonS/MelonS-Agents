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

ASSETS = Path(__file__).resolve().parents[1] / "unity-project" / "Assets"
# Scripts/ 만 훑으면 **씬에 구워지는 문구를 놓친다** (2026-08-01).
#  Editor/SceneSetup*.cs 가 만든 Text 는 Game.unity/MainMenu.unity 에 그대로
#  들어가므로 화면에 나오는 것은 똑같다.  실제로 "콜로니스트를 클릭하세요" 가
#  이 사각지대에 남아, 나머지를 전부 '주민' 으로 통일한 뒤에도 정보 패널만
#  옛 용어를 쓰고 있었다 — 한 화면에 두 이름이 다시 생긴 셈.
SCAN_DIRS = [ASSETS / "Scripts", ASSETS / "Editor"]

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
    # 2026-08-01 UX 리뷰 — **한 대상을 네 이름으로** 부르고 있었다.
    #  주민 / 림 / pawn / 콜로니스트 가 같은 화면에 동시에 뜬다.
    ("pawn 없음", "주민 없음"), ("pawn 이", "주민이"), ("pawn 을", "주민을"),
    ("pawn 대기", "주민 대기"), ("pawn 통과", "주민 통과"), ("pawn 자동", "주민 자동"),
    ("pawn 시", "주민이 있으면"), ("/sec/pawn", "/초"), ("림이", "주민이"),
    ("림 ", "주민 "), ("림에게", "주민에게"), ("림은", "주민은"),
    # 징집 / 드래프트 — 버튼은 '징집' 인데 툴팁은 '드래프트' 라 같은 것인 줄 모른다.
    ("드래프트 후", "징집 후"), ("드래프트 필요", "징집 필요"), ("드래프트", "징집"),
    # ("Undraft", "징집 해제") 는 **의도적으로 없다** (2026-08-01).
    #  버튼은 `"징집\nDraft"` 처럼 한글 밑에 영문을 병기하는 규칙이라, Undraft 를
    #  치환하면 병기 줄까지 한글이 되어 규칙이 깨진다.  게다가 이 항목이 실제로
    #  `colorAfterUndraft` 식별자를 `colorAfter징집 해제` 로 만들어 빌드를 깼다.
    #  ASCII 단독 단어는 코드 식별자와 구분이 안 되므로 사전에 넣지 않는다.
    # 적 이름도 세 가지 — 약탈자로 통일 (알림 카드가 쓰는 말).
    ("강도", "약탈자"),
    # 개발 용어가 플레이어 화면에 나온다.
    ("hauler 는", "운반하는 주민은"), ("hauler 운반 중", "운반 중"),
    ("hauler", "운반 담당"), ("collider", "충돌 범위"), ("radius", "반경"),
    ("stage 시각 변화", "단계 성장"),
]

STR_RE = re.compile(r'"([^"\\]*(?:\\.[^"\\]*)*)"')

# 보간 홀 `{...}` — **문자열 안이지만 코드다** (2026-08-01 사고).
#  `$"...(col={Fmt(colorAfterUndraft)})"` 의 중괄호 안은 식별자·메서드 호출이지
#  화면 문구가 아니다.  이걸 구분하지 않은 첫 판이 식별자를 한글로 바꿔
#  `CS1003 Syntax error` 로 빌드를 깼다.  `{{` 는 이스케이프된 리터럴 중괄호라 홀이 아니다.
HOLE_RE = re.compile(r"\{\{|\}\}|\{[^{}]*\}")


def sub_text_only(lit: str) -> str:
    """리터럴에서 **표시 텍스트 구간에만** 치환을 적용한다.

    보간 홀은 원문 그대로 통과시킨다."""
    out, last = [], 0
    for m in HOLE_RE.finditer(lit):
        seg = lit[last:m.start()]
        for a, b in SUBS:
            seg = seg.replace(a, b)
        out.append(seg)
        out.append(m.group(0))      # 홀은 손대지 않는다
        last = m.end()
    seg = lit[last:]
    for a, b in SUBS:
        seg = seg.replace(a, b)
    out.append(seg)
    return "".join(out)


def main() -> int:
    dry = "--dry" in sys.argv
    changed: dict[str, int] = {}
    files = sorted(p for d in SCAN_DIRS for p in d.rglob("*.cs"))
    for f in files:
        # Tests/ 는 화면이 아니라 **개발자 진단 출력**이다.  실패 메시지에
        #  식별자와 내부 상태명이 그대로 박혀 있어야 원인을 읽을 수 있다.
        if "Tests" in f.parts:
            continue
        src = io.open(f, encoding="utf-8").read()
        cnt = [0]

        def rep(m: re.Match) -> str:
            o = m.group(1)
            t = sub_text_only(o)
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
            changed[str(f.relative_to(ASSETS))] = cnt[0]
            if not dry:
                io.open(f, "w", encoding="utf-8").write("\n".join(out))

    for name, n in sorted(changed.items(), key=lambda kv: -kv[1])[:15]:
        print(f"  {n:3d}  {name}")
    print(f"{'(dry) ' if dry else ''}총 {sum(changed.values())}곳 / {len(changed)}파일")
    return 0


if __name__ == "__main__":
    sys.exit(main())
