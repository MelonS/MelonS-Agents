#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""md2print.py — 제출용 마크다운을 **인쇄용 HTML** 로 변환한다 (PDF 직전 단계).

제출물 ③(게임 소개서)·④(AI 활용 기술 문서)는 PDF 로 내야 한다.  이 머신엔
pandoc/wkhtmltopdf 가 없고, 있다 해도 한글 폰트 임베딩이 매번 말썽이다.
그래서 경로를 둘로 쪼갠다:

    1. (이 스크립트) md → 인쇄용 HTML   — 레이아웃·타이포·페이지 규칙을 여기서 확정
    2. Playwright 의 page.pdf()        — 실제 PDF 화 (Chromium 인쇄 엔진)

이렇게 나누면 HTML 을 브라우저로 열어 **PDF 를 만들기 전에** 눈으로 검수할 수 있고,
표·코드블록이 페이지 경계에서 잘리는 것도 CSS 로 고칠 수 있다.

폰트는 시스템 한글 폰트(맑은 고딕)를 쓴다 — 심사자 PDF 는 글자가 벡터로 박히므로
로컬 폰트로 렌더해도 결과물엔 문제가 없다.

usage:
  python skills/game-prototype/scripts/md2print.py docs/submission-game-intro-2026.md
  python skills/game-prototype/scripts/md2print.py --all
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import markdown

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]          # skills/game-prototype
OUT_DIR = ROOT / "art-out" / "submission"

# 제출물 목록 — 요강의 ③/④ 에 대응.
SUBMISSION_DOCS = [
    ("docs/submission-game-intro-2026.md", "PawnSim — 게임 소개 및 설명"),
    ("docs/submission-ai-tech-2026.md", "PawnSim — AI 활용 기술 문서"),
]

CSS = """
@page { size: A4; margin: 18mm 16mm; }
* { box-sizing: border-box; }
body {
  font-family: "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", sans-serif;
  font-size: 10.5pt; line-height: 1.72; color: #1c1917;
  max-width: 176mm; margin: 0 auto; word-break: keep-all;
}
h1 { font-size: 20pt; margin: 0 0 4mm; letter-spacing: -0.01em; }
h2 { font-size: 14pt; margin: 9mm 0 3mm; padding-bottom: 1.5mm;
     border-bottom: 1.5px solid #d6d3d1; break-after: avoid; }
h3 { font-size: 11.5pt; margin: 6mm 0 2mm; break-after: avoid; }
p, ul, ol { margin: 0 0 3.5mm; }
li { margin-bottom: 1mm; }
strong { font-weight: 700; }
code { font-family: Consolas, "D2Coding", monospace; font-size: 9pt;
       background: #f5f5f4; padding: 0.5mm 1.2mm; border-radius: 2px; }
pre { background: #f5f5f4; padding: 3mm; border-radius: 3px; overflow-x: auto;
      break-inside: avoid; border-left: 3px solid #a8a29e; }
pre code { background: none; padding: 0; font-size: 8.5pt; line-height: 1.5; }
table { border-collapse: collapse; width: 100%; margin: 0 0 4mm;
        font-size: 9.5pt; break-inside: avoid; }
th, td { border: 1px solid #d6d3d1; padding: 1.8mm 2.5mm; text-align: left;
         vertical-align: top; }
th { background: #f5f5f4; font-weight: 700; }
blockquote { margin: 0 0 4mm; padding: 2mm 0 2mm 4mm; border-left: 3px solid #78716c;
             color: #44403c; background: #fafaf9; break-inside: avoid; }
blockquote p:last-child { margin-bottom: 0; }
hr { border: none; border-top: 1px solid #e7e5e4; margin: 7mm 0; }
a { color: #1c1917; text-decoration: underline; }
/* 제출 문서에서 가장 흔한 사고: 표/인용이 페이지 경계에서 두 동강 난다. */
h1, h2, h3 { break-inside: avoid; }
"""


def strip_internal(text: str) -> str:
    """`<!--internal-->` ~ `<!--/internal-->` 사이를 제거한다.

    제출 문서는 두 독자를 갖는다: 운영자(판단이 필요한 항목)와 심사자(완성된 설명).
    같은 파일에 둘을 섞으면 "초안", "PDF 변환 전 검토 필요", "운영자 판단" 같은
    **내부 메모가 심사자에게 그대로 나간다** — 실제로 1차 PDF 에서 그렇게 나왔다.
    마크다운은 작업본 그대로 두고, 인쇄 단계에서만 내부 블록을 걷어낸다.
    """
    out, skip = [], False
    for line in text.splitlines():
        s = line.strip()
        if s.startswith("<!--internal-->"):
            skip = True
            continue
        if s.startswith("<!--/internal-->"):
            skip = False
            continue
        if not skip:
            out.append(line)
    return "\n".join(out)


def convert(md_path: Path, title: str) -> Path:
    text = strip_internal(md_path.read_text(encoding="utf-8"))
    body = markdown.markdown(
        text,
        extensions=["tables", "fenced_code", "toc", "sane_lists", "attr_list"],
    )
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    out = OUT_DIR / (md_path.stem + ".html")
    out.write_text(
        f"<!doctype html><html lang=\"ko\"><head><meta charset=\"utf-8\">"
        f"<title>{title}</title><style>{CSS}</style></head>"
        f"<body>{body}</body></html>",
        encoding="utf-8",
    )
    print(f"[md2print] {md_path.name} → {out}")
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("md", nargs="?")
    ap.add_argument("--all", action="store_true", help="제출물 ③/④ 를 한 번에")
    ap.add_argument("--title", default=None)
    args = ap.parse_args()

    if args.all:
        for rel, title in SUBMISSION_DOCS:
            p = ROOT / rel
            if not p.exists():
                print(f"[md2print] ✗ 없음: {p}")
                return 1
            convert(p, title)
        return 0

    if not args.md:
        ap.error("md 경로 또는 --all 이 필요하다")
    p = Path(args.md)
    if not p.is_absolute():
        p = (ROOT / args.md) if (ROOT / args.md).exists() else Path.cwd() / args.md
    if not p.exists():
        print(f"[md2print] ✗ 없음: {p}")
        return 1
    convert(p, args.title or p.stem)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
