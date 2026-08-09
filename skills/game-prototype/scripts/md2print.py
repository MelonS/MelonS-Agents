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

/* ── 그림 ─────────────────────────────────────────────────────────
   운영자 2026-08-09: "이해를 도울수 있는 스샷이나 이미지 타이포 등등".
   글로만 설명한 화면은 심사자가 머릿속에서 다시 그려야 한다 — 한 장이면
   끝날 것을.  그림은 본문 폭에 맞추고, 캡션으로 **무엇을 보라는지** 짚는다.
   픽셀아트라 확대·축소 시 뭉개지지 않게 image-rendering 을 고정한다. */
figure { margin: 4mm 0 5mm; break-inside: avoid; text-align: center; }
figure img { max-width: 100%; height: auto; display: block; margin: 0 auto;
             border: 1px solid #d6d3d1; border-radius: 3px;
             image-rendering: -webkit-optimize-contrast; }
figure figcaption { font-size: 8.8pt; color: #57534e; margin-top: 1.8mm;
                    line-height: 1.5; text-align: center; }
figure figcaption strong { color: #1c1917; }
p > img { max-width: 100%; height: auto; }

/* ── 타이포 ───────────────────────────────────────────────────── */
h1 { border-bottom: 2.5px solid #1c1917; padding-bottom: 3mm; }
h2 { color: #0c0a09; }
h2::before { content: ""; }
/* 표 안 숫자는 자리를 맞춰야 비교가 된다 */
td { font-variant-numeric: tabular-nums; }
/* 첫 문단(리드)은 조금 크게 — 심사자가 3초 안에 무엇인지 알게 */
h1 + p { font-size: 11.5pt; color: #292524; }
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


def embed_images(html: str, base: Path) -> str:
    """`<img src="상대경로">` 를 base64 data URI 로 바꾼다.

    PDF 는 Playwright 가 `file://` 로 열어 굽는데, 상대 경로가 한 단계라도
    어긋나면 **그림 없이 조용히 인쇄된다** — 그림이 빠진 PDF 는 빠졌다는 사실조차
    눈에 잘 안 띈다.  HTML 안에 실어 두면 경로 문제 자체가 사라진다."""
    import base64
    import mimetypes
    import re as _re

    def sub(m):
        src = m.group(1)
        if src.startswith(("data:", "http://", "https://")):
            return m.group(0)
        path = (base / src).resolve()
        if not path.exists():
            print(f"[md2print] ⚠ 그림 없음: {src}", file=sys.stderr)
            return m.group(0)
        mime = mimetypes.guess_type(path.name)[0] or "image/png"
        b64 = base64.b64encode(path.read_bytes()).decode("ascii")
        return f'src="data:{mime};base64,{b64}"'

    return _re.sub(r'src="([^"]+)"', sub, html)


def figurize(html: str) -> str:
    """`<p><img alt="캡션" ...></p>` 를 `<figure>` + `<figcaption>` 으로.

    마크다운의 `![캡션](경로)` 는 그냥 문단 속 이미지가 된다.  캡션이 alt 에만
    들어가 **화면에는 보이지 않는다** — 그림이 무엇을 보여주는지 심사자가 알 수
    없다.  alt 를 실제 캡션으로 끌어올린다."""
    import re as _re

    def sub(m):
        alt, tag = m.group(1), m.group(0)
        img = _re.search(r"<img[^>]*>", tag).group(0)
        # alt 는 평문이라 `**강조**` 가 별표째 인쇄된다 — 캡션에서만 최소 변환.
        cap_txt = _re.sub(r"\*\*(.+?)\*\*", r"<strong></strong>", alt)
        cap = f"<figcaption>{cap_txt}</figcaption>" if alt.strip() else ""
        return f"<figure>{img}{cap}</figure>"

    return _re.sub(r'<p><img alt="([^"]*)"[^>]*></p>', sub, html)


def convert(md_path: Path, title: str) -> Path:
    text = strip_internal(md_path.read_text(encoding="utf-8"))
    body = markdown.markdown(
        text,
        extensions=["tables", "fenced_code", "toc", "sane_lists", "attr_list"],
    )
    body = figurize(body)
    body = embed_images(body, md_path.parent)
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
