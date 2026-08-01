# -*- coding: utf-8 -*-
"""_gen_hanok_rock.py — 바위·광맥.  **정본.**

계기 (2026-08-01 운영자): "바위도 한국형으로 가능하려나 일단 돌이랑 바위 아트 변경 시급."

실측 진단: 기존 `prop64_vein_*.png` 는 **매끄러운 구 3개**였다.  그라데이션으로
둥글린 공이라 돌이 아니라 풍선/달걀로 읽힌다.  각진 면이 하나도 없다.

작법은 조사해서 따랐다 (SLYNYRD Pixelblog 13 — Rocks):
  · **"Angular lines lead to a sharp, hard look"** — 돌의 단단함은 각진 선에서 온다.
  · **"start with solid blobs of color, then chisel down the form"** — 덩어리로
    시작해 **깎아 내려간다**.  기존 에셋은 이 '깎기' 단계가 통째로 빠져 있었다.
  · 환경에 맞는 색을 쓴다 — 일반적인 회색 말고.
출처: https://www.slynyrd.com/blog/2019/1/22/pixelblog-13-rocks

한국형: 한국 산은 **화강암**이다.  밝은 회색 바탕에 검은 알갱이(흑운모)가 박힌
결정질 무늬가 화강암의 식별 신호다.  그래서 화강암을 기본 톤으로 잡고, 나머지
암종은 같은 문법에 색만 달리한다.

구현: 바위를 **다각형 면(facet) 여러 개**로 쌓는다.
  · 윗면(하늘을 보는 면) = 가장 밝다        · 좌상 면 = 중간 밝기
  · 우/아래 면 = 어둡다                      · 면 경계 = 1px 균열선
평면 채색(그라데이션 금지)이라 면과 면이 각으로 만나고, 그게 '깎인 돌' 을 만든다.

usage: python _gen_hanok_rock.py [--stage]
"""
from __future__ import annotations
import sys
import os
import math
import random
import colorsys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import palette as P  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402


def shade(c, dv=0.0, s=None):
    r, g, b, a = c
    h, s0, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    if s is not None:
        s0 = s
    v = max(0.0, min(1.0, v * (1.0 + dv)))
    r2, g2, b2 = colorsys.hsv_to_rgb(h, s0, v)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


# 암종별 기준색 — 전부 팔레트 ROCK/DIRT 램프에서 파생 (톤 이탈 방지).
STONES = {
    "granite":   shade(P.ROCK_MD, +0.06, s=0.06),   # 화강암 — 밝은 회색, 거의 무채
    "sandstone": shade(P.DIRT_LT, +0.02, s=0.26),   # 사암 — 누런 기
    "limestone": shade(P.ROCK_LT, +0.10, s=0.10),   # 석회암 — 밝고 뿌옇다
    "marble":    shade(P.ROCK_LT, +0.20, s=0.04),   # 대리석 — 가장 밝다
}
SPECK = {                                            # 결정 알갱이 색
    "granite":   shade(P.ROCK_DK, -0.35, s=0.05),   # 흑운모 (검은 점) — 화강암의 신호
    "sandstone": shade(P.DIRT_DK, -0.10, s=0.30),
    "limestone": shade(P.ROCK_DK, +0.10, s=0.06),
    "marble":    shade(P.ROCK_MD, -0.05, s=0.05),
}


def _facets(rnd, cx, cy, r, n):
    """각진 덩어리 하나의 꼭짓점 — 원을 불규칙하게 찌그러뜨린 다각형.

    반지름을 꼭짓점마다 흔들어야 각이 생긴다.  균일하면 다시 원이 된다."""
    pts = []
    for i in range(n):
        a = (i / n) * math.tau + rnd.uniform(-0.12, 0.12)
        rr = r * rnd.uniform(0.72, 1.06)
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr * 0.86))
    return pts


def _chunk(d, rnd, cx, cy, r, base, speck, seed_i):
    """돌덩이 하나 — 몸통 + 윗면(밝은 면) + 균열 + 알갱이."""
    body = _facets(rnd, cx, cy, r, rnd.choice((6, 7)))
    d.polygon(body, fill=base, outline=shade(base, -0.42))

    # 윗면 — 하늘을 보는 면.  몸통 위쪽을 잘라 낸 다각형이라 각으로 만난다.
    top = []
    for (x, y) in body:
        if y <= cy + r * 0.10:
            top.append((x, y))
    if len(top) >= 3:
        top.append((cx + r * 0.10, cy + r * 0.16))
        d.polygon(top, fill=shade(base, +0.13))

    # 좌상 하이라이트 면 — 더 작게 한 겹 더 (2단 램프)
    hi = [(x * 0.72 + cx * 0.28, y * 0.72 + cy * 0.28) for (x, y) in top[:-1]]
    if len(hi) >= 3:
        d.polygon(hi, fill=shade(base, +0.26))

    # 우/하단 그늘 면
    dark = [(x, y) for (x, y) in body if y >= cy - r * 0.05 and x >= cx - r * 0.30]
    if len(dark) >= 3:
        d.polygon(dark, fill=shade(base, -0.20))

    # 균열 — **짧게**.  1차에서 덩어리 중심까지 긋는 긴 선을 썼더니 돌을 가로지르는
    #  막대기(나뭇가지)처럼 보였다.  실제 균열은 모서리에서 시작해 조금 들어가다 만다.
    for _ in range(rnd.randrange(1, 3)):
        i0 = rnd.randrange(len(body))
        x0, y0 = body[i0]
        ux, uy = (cx - x0), (cy - y0)
        L = math.hypot(ux, uy) or 1.0
        f = rnd.uniform(0.18, 0.34)           # 반지름의 1/5~1/3 만
        d.line((x0, y0, x0 + ux / L * r * f, y0 + uy / L * r * f),
               fill=shade(base, -0.40), width=max(1, int(r * 0.06)))

    # 결정 알갱이 — 화강암의 식별 신호
    for _ in range(int(r * 0.9)):
        a = rnd.uniform(0, math.tau)
        rr = rnd.uniform(0, r * 0.82)
        px_ = cx + math.cos(a) * rr
        py_ = cy + math.sin(a) * rr * 0.86
        d.point((px_, py_), fill=speck)


def vein(kind: str, size=128, seed=5):
    """광맥 — 돌덩이 3~4개가 뭉친 노두(露頭)."""
    rnd = random.Random(seed + hash(kind) % 1000)
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    base, speck = STONES[kind], SPECK[kind]
    u = size / 128.0
    # 뒤(위)에 있는 덩이부터 그려 앞 덩이가 덮게 한다
    chunks = [(64, 58, 30), (38, 78, 26), (90, 80, 25), (64, 92, 22)]
    for i, (cx, cy, r) in enumerate(chunks):
        _chunk(d, rnd, cx * u, cy * u, r * u, base, speck, i)
    # 접지 그늘 — 땅에 놓인 느낌
    d.ellipse((18 * u, 100 * u, 110 * u, 118 * u), fill=(0, 0, 0, 46))
    return im


def rock_tile(size=64, seed=9):
    """암반 지대 타일 — 갈라진 화강암 바닥.  이음매 없이 반복된다."""
    rnd = random.Random(seed)
    base = STONES["granite"]
    im = Image.new("RGBA", (size, size), shade(base, -0.10))
    d = ImageDraw.Draw(im)
    # 판 — 각진 다각형으로 바닥을 채운다 (원 금지)
    for _ in range(9):
        cx, cy = rnd.randrange(size), rnd.randrange(size)
        r = rnd.uniform(9, 16)
        pts = _facets(rnd, cx, cy, r, rnd.choice((5, 6)))
        d.polygon(pts, fill=shade(base, rnd.uniform(-0.09, +0.13)))
        d.line(pts + [pts[0]], fill=shade(base, -0.26), width=1)
    for _ in range(size * 2):                       # 알갱이
        d.point((rnd.randrange(size), rnd.randrange(size)), fill=SPECK["granite"])
    return im


TARGETS = [
    ("prop64_vein_granite", 128, lambda: vein("granite", 128, 11)),
    ("prop64_vein_sandstone", 128, lambda: vein("sandstone", 128, 23)),
    ("prop64_vein_limestone", 128, lambda: vein("limestone", 128, 37)),
    ("prop64_vein_marble", 128, lambda: vein("marble", 128, 51)),
    ("tile64_rock_a", 64, lambda: rock_tile(64, 9)),
]


def main() -> int:
    stage = "--stage" in sys.argv
    out = r"G:/ai/_rock_stage" if stage else HERE
    os.makedirs(out, exist_ok=True)
    for name, _sz, fn in TARGETS:
        img = fn()
        img.save(os.path.join(out, name + ".png"))
        print(f"[ok] {name}.png ({img.width}x{img.height})")
    print(f"{'(검수용) ' if stage else ''}{len(TARGETS)}종")
    return 0


if __name__ == "__main__":
    sys.exit(main())
