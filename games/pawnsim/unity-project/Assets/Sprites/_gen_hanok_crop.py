# -*- coding: utf-8 -*-
"""_gen_hanok_crop.py — 벼 3단계.  **정본.**

계기 (2026-08-01 운영자): "작물 아트 완성도 여전히 다른것에 못미치는 수준."

실측 진단: 기존 `crop_rice*.png` 는 **곧은 막대 3~5개 + 꼭대기의 주황 공**이었다.
검은 외곽선이 두꺼워 줄기가 빨대처럼 보이고, 이삭이 구슬이라 막대사탕에 가깝다.
벼의 형태 신호가 하나도 없다.

벼를 벼로 만드는 것 (실물 관찰):
  ① 잎이 **부채꼴로 벌어진다** — 한 포기에서 여러 잎이 사방으로 휜다
  ② 익으면 **고개를 숙인다** — "벼는 익을수록 고개를 숙인다".  이삭 무게로
     줄기 끝이 아래로 활처럼 휜다.  이게 익은 벼의 가장 강한 식별 신호다.
  ③ 이삭은 구슬이 아니라 **낟알이 줄지어 달린 이삭**이다 — 곡선을 따라 알갱이가 늘어선다

3단계는 실루엣으로 구분되어야 한다 (색만 바꾸면 멀리서 같은 것):
  모 → 낮고 곧다 / 자람 → 크고 벌어진다 / 익음 → 크고 **고개를 숙인다**

usage: python _gen_hanok_crop.py [--stage]
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

N = 32


def shade(c, dv=0.0, s=None):
    r, g, b, a = c
    h, s0, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    if s is not None:
        s0 = s
    v = max(0.0, min(1.0, v * (1.0 + dv)))
    r2, g2, b2 = colorsys.hsv_to_rgb(h, s0, v)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


LEAF_YOUNG = shade(P.GRASS_LT, +0.14, s=0.46)     # 모 — 연둣빛
LEAF_MID = shade(P.GRASS_MD, +0.10, s=0.48)
LEAF_DK = shade(P.GRASS_DK, +0.02, s=0.46)
STRAW_RIPE = shade(P.CROP_GOLD, -0.06, s=0.52)    # 익은 줄기
GRAIN = P.CROP_GOLD
GRAIN_LT = shade(P.CROP_GOLD, +0.16, s=0.40)
GRAIN_DK = shade(P.CROP_GOLD, -0.24, s=0.58)
SOIL = shade(P.DIRT_DK, -0.05)


def _blade(d, x0, y0, h, bend, col, w=1):
    """잎 하나 — 밑에서 위로 가며 옆으로 휜다.  직선이면 빨대가 된다."""
    prev = (x0, y0)
    steps = max(4, int(h))
    for i in range(1, steps + 1):
        t = i / steps
        x = x0 + bend * (t * t)          # 위로 갈수록 더 휜다
        y = y0 - h * t
        d.line((prev[0], prev[1], x, y), fill=col, width=w)
        prev = (x, y)
    return prev


def _clump(d, rnd, cx, base_y, h, spread, col_main, col_dark, count):
    """한 포기 — 잎이 부채꼴로 벌어진다."""
    tips = []
    for i in range(count):
        f = (i / max(1, count - 1)) * 2.0 - 1.0        # -1..1
        bend = f * spread * rnd.uniform(0.8, 1.15)
        hh = h * rnd.uniform(0.82, 1.06)
        col = col_dark if abs(f) > 0.6 else col_main   # 바깥 잎이 어둡다
        tips.append(_blade(d, cx + f * 1.6, base_y, hh, bend, col))
    return tips


def _soil(d):
    d.ellipse((8, 27, 23, 31), fill=SOIL)


def rice_seedling(seed=3):
    """모 — 낮고 곧다.  '막 심었다' 가 읽혀야 한다."""
    rnd = random.Random(seed)
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    _soil(d)
    _clump(d, rnd, 16, 28, 10, 2.6, LEAF_YOUNG, shade(LEAF_YOUNG, -0.18), 7)
    return im


def rice_growing(seed=7):
    """자람 — 키가 크고 부채꼴로 벌어진다.  아직 고개를 안 숙인다."""
    rnd = random.Random(seed)
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    _soil(d)
    _clump(d, rnd, 16, 28, 18, 5.4, LEAF_MID, LEAF_DK, 10)
    return im


def rice_ripe(seed=11):
    """익음 — **고개를 숙인다.**  이삭 무게로 끝이 활처럼 휘고 낟알이 줄지어 달린다.

    이 단계만 실루엣이 아래로 꺾이므로, 색을 못 봐도 '수확할 때' 가 읽힌다."""
    rnd = random.Random(seed)
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    _soil(d)
    # 아래쪽 잎 (아직 초록기가 남는다)
    _clump(d, rnd, 16, 28, 13, 4.8, LEAF_DK, shade(LEAF_DK, -0.12), 7)

    # 이삭 줄기 4개 — 위로 올라가다 끝에서 아래로 꺾인다
    for k, (dx, hh, sway) in enumerate(((-5, 19, -4.5), (-1, 22, -2.0),
                                        (3, 21, 2.4), (6, 17, 4.8))):
        x0, y0 = 16 + dx, 28
        # 줄기 — 위로
        prev = (x0, y0)
        pts = []
        for i in range(1, 13):
            t = i / 12.0
            x = x0 + sway * (t * t) * 0.5
            y = y0 - hh * t
            d.line((prev[0], prev[1], x, y), fill=STRAW_RIPE)
            prev = (x, y)
        # 이삭 — 꼭대기에서 **아래로 휘어 내려온다**
        tipx, tipy = prev
        # 낟알 — 1px 점이면 1배율에서 실처럼 보인다.  2px 덩어리로 달아야
        #  '알이 맺혔다' 가 읽힌다 (운영자 지적의 핵심은 '완성도' = 밀도였다).
        n = 8
        for i in range(n):
            t = (i + 1) / n
            gx = tipx + sway * 0.55 * t + math.sin(t * 2.2) * 1.2
            gy = tipy + t * 8.0                      # 아래로
            d.rectangle((gx - 1, gy - 1, gx, gy), fill=GRAIN)
            d.point((gx - 1, gy - 1), fill=GRAIN_LT)
            d.point((gx, gy), fill=GRAIN_DK)
    return im


TARGETS = [
    ("crop_rice_seedling", rice_seedling),
    ("crop_rice_growing", rice_growing),
    ("crop_rice", rice_ripe),
]


def main() -> int:
    stage = "--stage" in sys.argv
    # 두 곳에 같은 이름이 있다 — Sprites/(에디터 로드) 와 Resources/crops32/(런타임).
    #  한쪽만 바꾸면 어느 쪽이 보이는지 알 수 없게 되므로 **둘 다** 쓴다.
    dests = ([r"G:/ai/_crop_stage"] if stage
             else [HERE, os.path.normpath(os.path.join(HERE, "..", "Resources", "crops32"))])
    for name, fn in TARGETS:
        img = fn()
        for dst in dests:
            os.makedirs(dst, exist_ok=True)
            if stage or os.path.exists(os.path.join(dst, name + ".png")):
                img.save(os.path.join(dst, name + ".png"))
        print(f"[ok] {name}.png ({img.width}x{img.height})")
    print(f"{'(검수용) ' if stage else ''}{len(TARGETS)}종")
    return 0


if __name__ == "__main__":
    sys.exit(main())
