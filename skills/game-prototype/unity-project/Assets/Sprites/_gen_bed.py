# -*- coding: utf-8 -*-
"""#193 / #198 - bed sprites for PawnSim (colony-sim-style colony sim).
#A1  - palette unified: frame colours now imported from palette.py.

bed_wood.png  16x32 - utilitarian wood bed (the reference sim Wood Bed)
bed_fine.png  16x32 - quality wood bed (the reference sim Fine Bed)

Both follow Kenney Tiny Town style:
  - 16x32 px canvas = world 1x2 cells at PPU 16.
  - Flat colour blocks + 1px OUTLINE_OBJ (from palette).
  - Top ~1/3 = pillow, bottom ~2/3 = blanket.
  - The two must be distinguishable at a glance when placed side by side.

#198 D4-1: added gen_bed_fine() + _preview_beds.png composite.
"""
from PIL import Image
from pathlib import Path

from palette import OUTLINE_OBJ, WOOD_DK, WOOD_MD, WOOD_LT

HERE = Path(__file__).resolve().parent

W, H = 16, 32

# ---------------------------------------------------------------------------
# bed_wood palette  (utilitarian dark-brown frame, plain red blanket)
# Frame uses unified WOOD_DK / WOOD_LT from palette.py.
# ---------------------------------------------------------------------------
OUTLINE     = OUTLINE_OBJ
WOOD_DARK   = WOOD_DK                # outer frame / outline
WOOD_LIGHT  = WOOD_LT                # inner frame ring
PILLOW      = (240, 232, 210, 255)   # cream pillow face
PILLOW_SH   = (212, 198, 168, 255)   # pillow shadow / seam edge
BLANKET     = (158, 58,  58, 255)    # muted red blanket (was 180,60,60 — closer to MEAT_RED family)
BLANKET_SH  = (118, 38,  38, 255)    # blanket fold shadow
BLANKET_LT  = (190, 88,  72, 255)    # blanket top-edge highlight
SHEET       = (200, 186, 162, 255)   # exposed mattress — warmer, less white

# ---------------------------------------------------------------------------
# bed_fine palette  (polished golden-oak frame, royal-blue blanket)
# ---------------------------------------------------------------------------
FINE_OUTLINE  = OUTLINE_OBJ
FINE_FRAME    = WOOD_LT               # polished oak — unified to palette WOOD_LT
FINE_FRAME_LT = (214, 168, 104, 255)  # slightly lighter face of polished frame
FINE_GOLD     = (196, 162, 56, 255)   # gold accent trim — matches CROP_GOLD family
FINE_PILLOW   = (245, 238, 218, 255)
FINE_PILLO_SH = (215, 202, 172, 255)
FINE_PILLO_HL = (255, 248, 235, 255)
FINE_BLANKET  = (48,  82, 162, 255)   # deep blue blanket (muted, not neon)
FINE_BLANK_SH = (32,  56, 112, 255)
FINE_BLANK_LT = (80, 118, 200, 255)
FINE_SHEET    = (210, 200, 180, 255)


def gen_bed_wood(out_path: Path):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = im.load()

    for y in range(H):
        for x in range(W):
            if x == 0 or x == W - 1 or y == 0 or y == H - 1:
                px[x, y] = OUTLINE
            elif x == 1 or x == W - 2 or y == 1 or y == H - 2:
                px[x, y] = WOOD_LIGHT
            else:
                px[x, y] = SHEET

    # Pillow (top ~1/3): x=3..12, y=3..9
    for y in range(3, 10):
        for x in range(3, 13):
            px[x, y] = PILLOW
    for x in range(3, 13): px[x, 9] = PILLOW_SH
    for x in range(3, 13): px[x, 3] = PILLOW_SH

    # Mid-rail
    for x in range(2, 14): px[x, 10] = WOOD_DARK
    for x in range(2, 14): px[x, 11] = SHEET

    # Blanket (lower ~2/3): x=3..12, y=12..29
    for y in range(12, 30):
        for x in range(3, 13):
            px[x, y] = BLANKET
    for x in range(3, 13): px[x, 12] = BLANKET_LT
    for x in range(3, 13): px[x, 20] = BLANKET_SH
    for x in range(3, 13): px[x, 29] = BLANKET_SH

    # Corner posts
    px[1, 1] = WOOD_DARK;  px[W - 2, 1] = WOOD_DARK
    px[1, H - 2] = WOOD_DARK;  px[W - 2, H - 2] = WOOD_DARK

    im.save(out_path)
    print(f"[gen_bed] wrote {out_path} ({W}x{H})")


def gen_bed_fine(out_path: Path):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = im.load()

    for y in range(H):
        for x in range(W):
            if x == 0 or x == W - 1 or y == 0 or y == H - 1:
                px[x, y] = FINE_OUTLINE
            elif x == 1 or x == W - 2 or y == 1 or y == H - 2:
                px[x, y] = FINE_FRAME
            else:
                px[x, y] = FINE_SHEET

    # Headboard accent rows y=2..3
    for x in range(2, W - 2): px[x, 2] = FINE_GOLD
    for x in range(2, W - 2): px[x, 3] = FINE_FRAME_LT
    for dot_x in (3, 8, 12):  px[dot_x, 2] = FINE_OUTLINE

    # Pillow: x=2..13, y=4..10
    for y in range(4, 11):
        for x in range(2, 14):
            px[x, y] = FINE_PILLOW
    for x in range(2, 14): px[x, 10] = FINE_PILLO_SH
    for x in range(2, 14): px[x, 4] = FINE_PILLO_SH
    for x in range(3, 13): px[x, 7] = FINE_PILLO_HL
    for y in range(5, 10): px[2, y] = FINE_PILLO_SH; px[13, y] = FINE_PILLO_SH

    # Mid-rail
    for x in range(1, W - 1): px[x, 11] = FINE_OUTLINE
    for x in range(2, W - 2): px[x, 12] = FINE_SHEET

    # Blanket: x=2..13, y=13..30
    for y in range(13, 31):
        for x in range(2, 14):
            px[x, y] = FINE_BLANKET
    for x in range(2, 14): px[x, 13] = FINE_BLANK_LT
    for x in range(2, 14): px[x, 19] = FINE_BLANK_SH
    for x in range(2, 14): px[x, 25] = FINE_BLANK_SH
    for x in range(2, 14): px[x, 30] = FINE_BLANK_SH

    # Corner posts
    px[1, 1] = FINE_GOLD;  px[W - 2, 1] = FINE_GOLD
    px[1, H - 2] = FINE_FRAME_LT;  px[W - 2, H - 2] = FINE_FRAME_LT

    im.save(out_path)
    print(f"[gen_bed] wrote {out_path} ({W}x{H})")


def gen_preview(out_path: Path, bed_wood_path: Path, bed_fine_path: Path, scale: int = 4):
    wood = Image.open(bed_wood_path).convert("RGBA")
    fine = Image.open(bed_fine_path).convert("RGBA")
    gap = 4
    pw = W * 2 + gap
    ph = H
    composite = Image.new("RGBA", (pw, ph), (42, 31, 24, 255))
    composite.paste(wood, (0, 0))
    composite.paste(fine, (W + gap, 0))
    preview = composite.resize((pw * scale, ph * scale), Image.NEAREST)
    preview.save(out_path)
    print(f"[gen_bed] preview written {out_path} ({pw * scale}x{ph * scale})")


if __name__ == "__main__":
    wood_out = HERE / "bed_wood.png"
    fine_out = HERE / "bed_fine.png"
    gen_bed_wood(wood_out)
    gen_bed_fine(fine_out)
    gen_preview(HERE / "_preview_beds.png", wood_out, fine_out, scale=4)
