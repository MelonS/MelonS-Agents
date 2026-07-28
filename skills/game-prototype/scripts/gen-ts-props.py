#!/usr/bin/env python3
"""gen-ts-props.py — TS 문법 절차 드로잉 자연물: 광맥 4종·돌덩이·베리덤불.

gen-ts-furniture.py 와 동일 규칙 (팩 관찰 팔레트·웜브라운 아웃라인·3톤 셀셰이딩·
접지 그림자).  잉크 세대 광맥('거대 오렌지 열매' 오독)의 TS 정합 대체.
규격: 광맥·덤불 1×1칸=128px 캔버스(PPU128), 돌덩이 64px(PPU128=0.5칸).
"""
import os
import random
from PIL import Image, ImageDraw

OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                    "..", "unity-project", "Assets", "Sprites"))
# 팔레트는 ts_palette.py 단일 출처에서 가져온다 (2026-07-29).
#  이전엔 각 생성기가 같은 상수를 따로 들고 있어, 팩 실측과 어긋난 아웃라인색을
#  고칠 때 두 파일을 각각 고쳐야 했다 — 하나 놓치면 조용히 어긋나는 구조였다.
import sys, os as _os
sys.path.insert(0, _os.path.dirname(_os.path.abspath(__file__)))
from ts_palette import (OUT_C, WOOD_L, WOOD_M, WOOD_D, RED_L, RED_M, RED_D,
                        GOLD_L, GOLD_M, GOLD_D, CREAM, CREAM_D,
                        SOIL_L, SOIL_M, SOIL_D, PIT_D, PIT_DD, SHADOW)


def rocks(base_l, base_m, base_d, fleck=None, seed=7, canvas=128, n_boulders=4):
    """둥근 바위 무더기 + 광물 플렉."""
    rnd = random.Random(seed)
    im = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([8, canvas - 40, canvas - 8, canvas - 6], fill=SHADOW)
    spots = [(canvas // 2, canvas - 52, 46), (30, canvas - 44, 34),
             (canvas - 34, canvas - 46, 36), (canvas // 2 - 8, canvas - 84, 30)][:n_boulders]
    for (cx, cy, r) in spots:
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=base_m, outline=OUT_C, width=5)
        d.ellipse([cx - r + 8, cy - r + 6, cx + int(r * 0.5), cy - 2], fill=base_l)
        d.ellipse([cx - int(r * 0.4), cy + int(r * 0.25), cx + r - 8, cy + r - 6], fill=base_d)
    if fleck:
        for _ in range(10):
            fx = rnd.randrange(24, canvas - 24)
            fy = rnd.randrange(canvas - 96, canvas - 24)
            d.ellipse([fx, fy, fx + 7, fy + 7], fill=fleck, outline=OUT_C, width=2)
    return im


def berry_bush(with_berries=True):
    im = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([14, 96, 114, 124], fill=SHADOW)
    G_L, G_M, G_D = (122, 168, 82), (92, 138, 62), (66, 108, 48)
    for (cx, cy, r) in [(64, 74, 44), (34, 84, 28), (94, 84, 28), (64, 52, 30)]:
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=G_M, outline=OUT_C, width=5)
        d.ellipse([cx - r + 7, cy - r + 5, cx, cy - 4], fill=G_L)
        d.ellipse([cx - 4, cy + 6, cx + r - 7, cy + r - 5], fill=G_D)
    if with_berries:
        for (bx, by) in [(46, 60), (76, 50), (60, 84), (90, 76), (32, 78), (70, 66)]:
            d.ellipse([bx, by, bx + 11, by + 11], fill=(204, 60, 54), outline=OUT_C, width=3)
            d.ellipse([bx + 2, by + 2, bx + 5, by + 5], fill=(240, 130, 120))
    return im


VEINS = {
    # (밝은, 중간, 어두운, 플렉)
    "sandstone": ((228, 190, 138), (198, 156, 104), (160, 120, 76), None),
    "limestone": ((234, 226, 204), (206, 196, 170), (168, 158, 132), None),
    "granite":   ((168, 168, 172), (132, 132, 138), (98, 98, 106), None),
    "marble":    ((240, 238, 234), (212, 210, 208), (176, 174, 174), (250, 250, 250)),
}
for i, (name, (l, m, dk, fl)) in enumerate(VEINS.items()):
    rocks(l, m, dk, fleck=fl, seed=11 + i).save(os.path.join(OUT, f"prop64_vein_{name}.png"))
    print(f"prop64_vein_{name}.png")

# 돌덩이 (채광 드롭) — 작은 회색 무더기 64px.  런타임 ItemArt32 용 Resources 사본.
chunk = rocks((190, 190, 194), (150, 150, 156), (112, 112, 120), seed=99,
              canvas=64, n_boulders=3)
chunk.save(os.path.join(OUT, "prop64_stone_chunk.png"))
RES = os.path.normpath(os.path.join(OUT, "..", "Resources", "Sprites"))
os.makedirs(RES, exist_ok=True)
chunk.save(os.path.join(RES, "prop64_stone_chunk.png"))
print("prop64_stone_chunk.png (+Resources)")

# 베리덤불 2상태 — 베이크(Assets/Sprites) + 런타임 상태 스왑(Resources/flora32 관례 경로)
berry_bush(True).save(os.path.join(OUT, "prop64_berry_bush.png"))
berry_bush(False).save(os.path.join(OUT, "prop64_berry_bush_empty.png"))
FLO = os.path.normpath(os.path.join(OUT, "..", "Resources", "flora32"))
os.makedirs(FLO, exist_ok=True)
berry_bush(True).save(os.path.join(FLO, "flora32_bush_berry.png"))
berry_bush(False).save(os.path.join(FLO, "flora32_bush_picked.png"))
print("prop64_berry_bush(.empty).png (+Resources/flora32 2상태)")
print("완료 →", OUT)
