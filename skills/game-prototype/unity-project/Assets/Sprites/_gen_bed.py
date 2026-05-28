# -*- coding: utf-8 -*-
"""#193 / #198 - bed sprites for PawnSim (RimWorld-style colony sim).

bed_wood.png  16x32 - utilitarian wood bed (RimWorld Wood Bed)
bed_fine.png  16x32 - quality wood bed (RimWorld Fine Bed)

Both follow Kenney Tiny Town style:
  - 16x32 px canvas = world 1x2 cells at PPU 16.
  - Flat color blocks + 1px dark outline.
  - Top ~1/3 = pillow, bottom ~2/3 = blanket.
  - The two must be distinguishable at a glance when placed side by side.

#198 D4-1: added gen_bed_fine() + _preview_beds.png composite.
"""
from PIL import Image
from pathlib import Path

HERE = Path(__file__).resolve().parent

# ---------------------------------------------------------------------------
# Shared constants
# ---------------------------------------------------------------------------
W, H = 16, 32

# ---------------------------------------------------------------------------
# bed_wood palette  (utilitarian dark-brown frame, plain red blanket)
# ---------------------------------------------------------------------------
WOOD_DARK   = (90,  58,  36, 255)   # outer frame / outline
WOOD_LIGHT  = (158, 102, 64, 255)   # inner frame ring
PILLOW      = (240, 232, 210, 255)  # cream pillow face
PILLOW_SH   = (212, 198, 168, 255)  # pillow shadow / seam edge
BLANKET     = (180,  60,  60, 255)  # red blanket (RimWorld utilitarian red)
BLANKET_SH  = (140,  40,  40, 255)  # blanket fold shadow
BLANKET_LT  = (212,  95,  80, 255)  # blanket top-edge highlight
SHEET       = (220, 220, 215, 255)  # exposed mattress base

# ---------------------------------------------------------------------------
# bed_fine palette  (polished golden-oak frame, royal-blue blanket)
#   Differentiation strategy:
#     1. Frame: lighter honey-oak ("polished wood") vs dark brown
#     2. Headboard: 2-row raised headboard accent with decorative dots
#     3. Blanket: deep royal blue (not red) — unambiguous at a glance
#     4. Pillow: same cream but with a visible centre seam highlight
#     5. Inner frame: warm gold trim row at headboard end
# ---------------------------------------------------------------------------
FINE_OUTLINE  = (72,  48,  20, 255)  # outer 1px outline (darker, sharper)
FINE_FRAME    = (200, 155,  80, 255)  # polished oak / honey-gold frame
FINE_FRAME_LT = (225, 185, 110, 255)  # lighter face of polished frame
FINE_GOLD     = (210, 175,  60, 255)  # gold accent trim on headboard
FINE_PILLOW   = (245, 238, 218, 255)  # slightly brighter cream pillow
FINE_PILLO_SH = (215, 202, 172, 255)  # pillow shadow
FINE_PILLO_HL = (255, 248, 235, 255)  # pillow seam highlight (centre)
FINE_BLANKET  = ( 45,  80, 170, 255)  # deep royal blue blanket
FINE_BLANK_SH = ( 30,  55, 120, 255)  # blanket fold shadow
FINE_BLANK_LT = ( 80, 120, 210, 255)  # blanket top-edge highlight
FINE_SHEET    = (230, 225, 210, 255)  # mattress base (slightly warmer)


def gen_bed_wood(out_path: Path):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = im.load()

    # 1) Full frame: 1px outline (WOOD_DARK), 1px inner ring (WOOD_LIGHT), fill SHEET
    for y in range(H):
        for x in range(W):
            if x == 0 or x == W - 1 or y == 0 or y == H - 1:
                px[x, y] = WOOD_DARK
            elif x == 1 or x == W - 2 or y == 1 or y == H - 2:
                px[x, y] = WOOD_LIGHT
            else:
                px[x, y] = SHEET

    # 2) Pillow (top ~1/3): x=3..12, y=3..9
    for y in range(3, 10):
        for x in range(3, 13):
            px[x, y] = PILLOW
    # Pillow bottom shadow row
    for x in range(3, 13):
        px[x, 9] = PILLOW_SH
    # Pillow top edge (subtle inset)
    for x in range(3, 13):
        px[x, 3] = PILLOW_SH

    # 3) Mid-rail: separates pillow from blanket (y=10 dark bar, y=11 mattress)
    for x in range(2, 14):
        px[x, 10] = WOOD_DARK
        px[x, 11] = SHEET

    # 4) Blanket (lower ~2/3): x=3..12, y=12..29
    for y in range(12, 30):
        for x in range(3, 13):
            px[x, y] = BLANKET
    # Blanket top highlight
    for x in range(3, 13):
        px[x, 12] = BLANKET_LT
    # Single centre fold stripe
    for x in range(3, 13):
        px[x, 20] = BLANKET_SH
    # Blanket bottom shadow
    for x in range(3, 13):
        px[x, 29] = BLANKET_SH

    # 5) Corner posts (4 dark dots at inner-ring corners)
    px[1, 1] = WOOD_DARK
    px[W - 2, 1] = WOOD_DARK
    px[1, H - 2] = WOOD_DARK
    px[W - 2, H - 2] = WOOD_DARK

    im.save(out_path)
    print(f"[gen_bed] wrote {out_path} ({W}x{H})")


def gen_bed_fine(out_path: Path):
    """Fine-quality bed — same 16x32 canvas, Kenney style.

    Visual differentiators vs bed_wood:
      - Polished honey-oak frame (lighter, golden) vs utilitarian dark brown
      - 2-row raised headboard at top with gold trim accent + decorative dots
      - Royal-blue blanket (deep, saturated) vs plain red
      - Wider pillow with visible centre-seam highlight stripe
      - Warmer mattress base
    """
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = im.load()

    # 1) Full frame: 1px outer outline (darker), 1px inner ring (polished oak)
    for y in range(H):
        for x in range(W):
            if x == 0 or x == W - 1 or y == 0 or y == H - 1:
                px[x, y] = FINE_OUTLINE
            elif x == 1 or x == W - 2 or y == 1 or y == H - 2:
                px[x, y] = FINE_FRAME
            else:
                px[x, y] = FINE_SHEET

    # 2) Headboard accent: rows y=2..3 get a polished oak + gold trim treatment.
    #    The headboard reads as a raised panel above the pillow.
    #    Row y=2: full-width gold trim (inside the outer frame ring)
    for x in range(2, W - 2):
        px[x, 2] = FINE_GOLD
    #    Row y=3: polished frame colour (slightly lighter face)
    for x in range(2, W - 2):
        px[x, 3] = FINE_FRAME_LT
    #    Decorative dot accents on headboard (symmetric, 3 dots)
    for dot_x in (3, 8, 12):
        px[dot_x, 2] = FINE_OUTLINE   # dark dot punched into the gold trim

    # 3) Pillow: x=2..13, y=4..10  (wider than wood bed — uses full inner width)
    for y in range(4, 11):
        for x in range(2, 14):
            px[x, y] = FINE_PILLOW
    # Pillow bottom shadow
    for x in range(2, 14):
        px[x, 10] = FINE_PILLO_SH
    # Pillow top inset shadow
    for x in range(2, 14):
        px[x, 4] = FINE_PILLO_SH
    # Pillow centre seam highlight (y=7, horizontal stripe — distinguishes fine)
    for x in range(3, 13):
        px[x, 7] = FINE_PILLO_HL
    # Pillow left/right side inset (1px each side)
    for y in range(5, 10):
        px[2,  y] = FINE_PILLO_SH
        px[13, y] = FINE_PILLO_SH

    # 4) Mid-rail: y=11 dark bar, y=12 mattress sheet
    for x in range(1, W - 1):
        px[x, 11] = FINE_OUTLINE
    for x in range(2, W - 2):
        px[x, 12] = FINE_SHEET

    # 5) Blanket (royal blue): x=2..13, y=13..30
    for y in range(13, 31):
        for x in range(2, 14):
            px[x, y] = FINE_BLANKET
    # Blanket top highlight strip
    for x in range(2, 14):
        px[x, 13] = FINE_BLANK_LT
    # Two fold stripes (finer detail = more visible texture vs wood's one stripe)
    for x in range(2, 14):
        px[x, 19] = FINE_BLANK_SH
    for x in range(2, 14):
        px[x, 25] = FINE_BLANK_SH
    # Blanket bottom shadow
    for x in range(2, 14):
        px[x, 30] = FINE_BLANK_SH

    # 6) Corner post accents: gold dots at top-rail corners (headboard posts)
    px[1, 1] = FINE_GOLD
    px[W - 2, 1] = FINE_GOLD
    # Foot-rail corners: polished frame
    px[1, H - 2] = FINE_FRAME_LT
    px[W - 2, H - 2] = FINE_FRAME_LT

    im.save(out_path)
    print(f"[gen_bed] wrote {out_path} ({W}x{H})")


def gen_preview(out_path: Path, bed_wood_path: Path, bed_fine_path: Path, scale: int = 4):
    """Side-by-side 4x preview composite for operator review."""
    wood = Image.open(bed_wood_path).convert("RGBA")
    fine = Image.open(bed_fine_path).convert("RGBA")

    gap = 4  # px gap between the two beds at 1x
    pw = W * 2 + gap
    ph = H
    composite = Image.new("RGBA", (pw, ph), (60, 60, 60, 255))  # dark grey bg
    composite.paste(wood, (0, 0))
    composite.paste(fine, (W + gap, 0))

    # Scale up
    preview = composite.resize((pw * scale, ph * scale), Image.NEAREST)
    preview.save(out_path)
    print(f"[gen_bed] preview written {out_path} ({pw * scale}x{ph * scale})")


if __name__ == "__main__":
    wood_out = HERE / "bed_wood.png"
    fine_out = HERE / "bed_fine.png"
    gen_bed_wood(wood_out)
    gen_bed_fine(fine_out)
    gen_preview(HERE / "_preview_beds.png", wood_out, fine_out, scale=4)
