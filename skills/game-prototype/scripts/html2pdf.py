# -*- coding: utf-8 -*-
"""html2pdf.py — `md2print.py` 가 만든 인쇄용 HTML 을 **PDF 로 굽는다.**

계기 (2026-08-07, 제출 D-3 점검): `md2print.py --all` 을 돌려도 PDF 날짜가
7/29 그대로였다.  md2print 는 설계상 **HTML 까지만** 만들고(눈으로 검수하라는
의도), PDF 화는 "Playwright 의 page.pdf()" 라고 주석에만 적혀 있었다.
즉 마지막 한 걸음이 **문서에만 있고 스크립트로는 없었다** — 제출 마감이 걸린
경로에 사람 손이 끼어 있는 상태였다.  그 손을 없앤다.

md2print 와 나누어 둔 이유는 그대로 유지한다: HTML 단계에서 눈으로 검수할 수
있고, 이 스크립트는 그 결과를 굽기만 한다.

usage:
  python html2pdf.py                 # art-out/submission/*.html 전부
  python html2pdf.py <파일.html> ...  # 지정한 것만
"""
from __future__ import annotations
import sys
import asyncio
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = Path(__file__).resolve().parent
OUT = HERE.parent / "art-out" / "submission"


async def render(paths):
    from playwright.async_api import async_playwright
    made = []
    async with async_playwright() as pw:
        browser = await pw.chromium.launch()
        page = await browser.new_page()
        for html in paths:
            pdf = html.with_suffix(".pdf")
            await page.goto(html.resolve().as_uri())
            # 폰트·이미지가 실제로 자리를 잡은 뒤에 굽는다 — 바로 pdf() 하면
            #  웹폰트가 폴백으로 박혀 한글이 두부(□)로 나오는 사고가 난다.
            await page.wait_for_load_state("networkidle")
            await page.pdf(
                path=str(pdf),
                format="A4",
                print_background=True,
                margin={"top": "14mm", "bottom": "14mm",
                        "left": "12mm", "right": "12mm"},
            )
            made.append(pdf)
            print(f"[html2pdf] {html.name} → {pdf.name}")
        await browser.close()
    return made


def main() -> int:
    args = [Path(a) for a in sys.argv[1:] if not a.startswith("-")]
    paths = args or sorted(OUT.glob("*.html"))
    paths = [p for p in paths if p.exists()]
    if not paths:
        print(f"[html2pdf] HTML 없음: {OUT}  — 먼저 md2print.py --all 을 돌릴 것")
        return 1
    made = asyncio.run(render(paths))

    # 두부(□) 사고를 막는 최소 검증 — PDF 안에 한글 글리프가 실제로 박혔는가.
    #  파일 크기가 비정상적으로 작으면 폰트가 임베드되지 않았을 가능성이 크다.
    bad = [p for p in made if p.stat().st_size < 40_000]
    for p in bad:
        print(f"[html2pdf] ⚠ {p.name} 이 {p.stat().st_size//1024}KB 로 작다 — "
              "한글 폰트 임베딩 확인 필요 (브라우저로 열어 육안 확인)")
    print(f"[html2pdf] {len(made)}개 생성" + (f", 경고 {len(bad)}건" if bad else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
