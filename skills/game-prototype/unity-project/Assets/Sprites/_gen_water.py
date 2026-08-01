# -*- coding: utf-8 -*-
"""_gen_water.py — 물 타일 (연못).  **정본.**

계기 (2026-08-01 운영자): "연못인지 물인지 먼지 모르겠지만 개선필요".

실측 진단: 기존 `ts_tile_water.png` 는 색이 43종인데 전부 (67~70, 145~150) 범위의
거의 같은 청록이었다 — 즉 **물결 구조가 없는 평면 그라데이션**이다.  그래서
확대해도 물로 안 읽히고 청록색 사각형으로 보였다.

작법은 추측하지 않고 조사해서 따랐다 (SLYNYRD Pixelblog 43 — Top Down Tiles):
  · 물결은 **1px 선으로 이어진 그물망**(wavy interconnected blob shapes)으로 만든다.
    덩이 하나에서 시작해 가지를 뻗어 서로 연결한 뒤, 일부를 끊어 흐름을 남긴다.
  · 밝은 선(반짝임) 아래 **2px 지점에 그림자**를 둔다 — 이 상하 대비가 물의 두께를 만든다.
  · 같은 방법으로 무늬만 다른 타일을 두 장 만들어 번갈아 쓰면 애니메이션이 된다.
출처: https://www.slynyrd.com/blog/2023/3/26/pixelblog-43-top-down-tiles-part-2

타일링: 모든 선을 **모듈로 좌표**로 그려 상하좌우가 이어지게 한다.  경계에서
끊기면 격자가 보이고, 그게 '평면 사각형' 인상의 절반이다.

usage: python _gen_water.py [--stage]
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
from PIL import Image  # noqa: E402

N = 64          # 타일 한 변 (PPU 64 = 1칸)


def shade(c, dv=0.0, s=None):
    r, g, b, a = c
    h, s0, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    if s is not None:
        s0 = s
    v = max(0.0, min(1.0, v * (1.0 + dv)))
    r2, g2, b2 = colorsys.hsv_to_rgb(h, s0, v)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


# 기준색은 **팩 원본 물 타일에서 뽑는다** — palette.py 의 WATER_* 는 UI/소품용
#  파랑이라 지형 팩의 청록과 다르다.  1차에서 그걸 썼다가 연못 색이 통째로
#  회청색으로 바뀌었다.  지형의 톤 기준점은 팩이다 (art-style-guide §2).
def _pack_water_base():
    src = os.path.join(HERE, "ts_tile_water.png")
    if not os.path.exists(src):
        return P.WATER_MD
    from collections import Counter
    im = Image.open(src).convert("RGBA")
    c = Counter(im.getdata())
    return c.most_common(1)[0][0]


BASE = _pack_water_base()
DEEP = shade(BASE, -0.055)   # 깊이 얼룩은 **아주 옅게** — 강하면 얼룩무늬(위장무늬)로 읽힌다
MID = shade(BASE, -0.025)
LINE = shade(BASE, +0.20, s=0.30)        # 반짝이는 물결선 (채도 낮춰 하늘빛)
LINE_HI = shade(BASE, +0.34, s=0.18)     # 가장 밝은 점
SHADOW = shade(BASE, -0.26)              # 물결선 아래 그림자


def _wave_network(rnd, count, jitter):
    """이어진 물결선의 좌표 집합 — 사인 곡선 여러 개를 위상만 다르게 겹친다.

    직선이 아니라 곡선이어야 하고, 곡선끼리 닿았다 떨어져야 '그물망' 이 된다."""
    pts = []
    for k in range(count):
        y0 = (k + 0.5) * (N / count) + rnd.uniform(-jitter, jitter)
        amp = rnd.uniform(2.0, 4.2)
        # 주기를 N 의 약수로 잡아야 좌우가 이어진다
        period = rnd.choice((N / 2, N / 3, N / 4))
        phase = rnd.uniform(0, math.tau)
        run = []
        for x in range(N):
            y = y0 + math.sin(x / period * math.tau + phase) * amp
            run.append((x, y))
        # 일부 구간을 끊어 흐름을 남긴다 (전부 이으면 줄무늬가 된다)
        cut0 = rnd.randrange(N)
        cutlen = rnd.randrange(16, 30)   # 더 많이 끊어 열린 수면을 남긴다
        run = [(x, y) for i, (x, y) in enumerate(run)
               if not (cut0 <= (i - 0) % N < cut0 + cutlen)]
        pts.append(run)
    return pts


def water_tile(seed: int) -> Image.Image:
    rnd = random.Random(seed)
    im = Image.new("RGBA", (N, N), BASE)
    px = im.load()

    # 깊이 얼룩 — 큰 저주파 덩이 (평면 방지).  모듈로 좌표라 이음매 없음.
    for _ in range(5):
        cx, cy = rnd.randrange(N), rnd.randrange(N)
        r = rnd.uniform(13, 22)   # 적고 크게 = 넓은 수면의 완만한 깊이차
        col = DEEP if rnd.random() < 0.55 else MID
        for dy in range(-int(r), int(r) + 1):
            for dx in range(-int(r), int(r) + 1):
                if dx * dx + dy * dy > r * r:
                    continue
                px[(cx + dx) % N, (cy + dy) % N] = col

    # 물결 그물망 — 밝은 1px 선
    for run in _wave_network(rnd, count=3, jitter=3.0):
        for (x, y) in run:
            yi = int(round(y)) % N
            px[x % N, yi] = LINE
            if rnd.random() < 0.10:
                px[x % N, yi] = LINE_HI              # 반짝임 점
            # 밝은 선 아래 2px 에 그림자 — 물의 두께를 만드는 대비
            px[x % N, (yi + 2) % N] = SHADOW

    # 짧은 가지 — 선끼리 이어 붙여 그물처럼 (직선 다발로 보이지 않게)
    for _ in range(7):
        x = rnd.randrange(N)
        y = rnd.randrange(N)
        for k in range(rnd.randrange(2, 5)):
            px[(x + k) % N, (y + k // 2) % N] = LINE
    return im


def main() -> int:
    stage = "--stage" in sys.argv
    out_dir = r"G:/ai/_water_stage" if stage else HERE
    os.makedirs(out_dir, exist_ok=True)
    # 두 장 — 무늬만 다르다.  타일맵이 무작위로 섞어 놓으면 반복감이 사라지고,
    #  나중에 애니메이션으로 쓰려면 그대로 두 프레임이 된다.
    for name, seed in (("ts_tile_water", 41), ("tile64_water_a", 77)):
        img = water_tile(seed)
        img.save(os.path.join(out_dir, name + ".png"))
        print(f"[ok] {name}.png ({img.width}x{img.height})")
    print(f"{'(검수용) ' if stage else ''}물 타일 2종")
    return 0


if __name__ == "__main__":
    sys.exit(main())
