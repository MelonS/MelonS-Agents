# -*- coding: utf-8 -*-
"""#193 - 운영자 fb "침대 이미지 안 어울림" + RimWorld 1x2 spec.

bed_wood.png 16x32 (Kenney Tiny Town style).
- 가로 16px / 세로 32px = world 1x2 (PPU 16 가정).
- Flat color + 1px dark outline (Kenney 스타일).
- 위 1/3 = 베개 흰색, 아래 2/3 = 이불 색 (담요).
- 프레임 = 짙은 wood.
"""
from PIL import Image
from pathlib import Path

HERE = Path(__file__).resolve().parent

# Kenney palette (Tiny Town inspired warm tones)
WOOD_DARK  = (90, 58, 36, 255)    # outline / frame
WOOD_LIGHT = (158, 102, 64, 255)  # inner frame
PILLOW     = (240, 232, 210, 255) # cream pillow
PILLOW_SH  = (212, 198, 168, 255) # pillow shadow
BLANKET    = (180, 60, 60, 255)   # red blanket (RimWorld 기본 빨강)
BLANKET_SH = (140, 40, 40, 255)
BLANKET_LT = (212, 95, 80, 255)
SHEET      = (220, 220, 215, 255) # mattress edge


def gen_bed_wood(out_path: Path):
    W, H = 16, 32
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = im.load()

    # 1) 전체 frame (outline + wood interior)
    for y in range(H):
        for x in range(W):
            # 1px 가장자리 = wood dark
            if x == 0 or x == W - 1 or y == 0 or y == H - 1:
                px[x, y] = WOOD_DARK
            # 2px inner frame = wood light
            elif x == 1 or x == W - 2 or y == 1 or y == H - 1 - 1:
                px[x, y] = WOOD_LIGHT
            else:
                px[x, y] = SHEET  # mattress base 색 (덮일 영역)

    # 2) 베개 (위 1/3) - y=2..10
    # pillow box: x=3..12, y=3..9 (가운데, frame 안)
    for y in range(3, 10):
        for x in range(3, 13):
            px[x, y] = PILLOW
    # pillow shadow 하단 1px
    for x in range(3, 13):
        px[x, 9] = PILLOW_SH
    # pillow inner outline (subtle)
    for x in range(3, 13):
        px[x, 3] = PILLOW_SH

    # 3) 가운데 mattress edge (베개 ↔ 이불 사이 띠) - y=10..11
    for x in range(2, 14):
        px[x, 10] = WOOD_DARK  # 가구 frame mid bar
        px[x, 11] = SHEET

    # 4) 이불 (아래 2/3) - y=12..29
    # blanket fills interior
    for y in range(12, 30):
        for x in range(3, 13):
            px[x, y] = BLANKET
    # 이불 top edge highlight
    for x in range(3, 13):
        px[x, 12] = BLANKET_LT
    # 이불 fold stripe (가로) - 2칸 간격 stripe 안 함, 너무 noisy.  단순 가운데 stripe 1줄.
    for x in range(3, 13):
        px[x, 20] = BLANKET_SH
    # 이불 bottom shadow
    for x in range(3, 13):
        px[x, 29] = BLANKET_SH

    # 5) frame inner 표시 - 침대 corner posts (4 모서리에 dark dot)
    px[1, 1] = WOOD_DARK
    px[W - 2, 1] = WOOD_DARK
    px[1, H - 2] = WOOD_DARK
    px[W - 2, H - 2] = WOOD_DARK

    im.save(out_path)
    print(f"[gen_bed] wrote {out_path} ({W}x{H})")


if __name__ == "__main__":
    gen_bed_wood(HERE / "bed_wood.png")
