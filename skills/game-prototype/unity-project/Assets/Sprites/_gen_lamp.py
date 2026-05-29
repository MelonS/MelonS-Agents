# -*- coding: utf-8 -*-
"""W-M4-04 Lane A — standing-lamp / torch buildable sprite (wiki #19).

Produces ONE new buildable sprite PNG for the M4 build catalogue:

  lamp.png   16x16   a wooden standing lamp / torch:
                     - a slim wood post (WOOD_DK/MD) rising from a small base,
                     - a warm flame head at the top (FIRE_OR base, FIRE_LT
                       highlight) that reads as the light source,
                     - 1px OUTLINE_OBJ border on the post + base (building/item
                       convention), the flame head left un-outlined so it glows.

The placed lamp emits a warm night light POOL — that pool is drawn at runtime
by LampGlowDriver.cs (a self-attached glow driver mirroring NightLightPoolDriver),
NOT baked into this sprite.  This sprite is only the object itself.

DESIGN RULES (same north-star as the rest of the catalogue):
  - Colors STRICTLY from palette.py — no ad-hoc values.
  - flat-Kenney language: solid fills, 1px OUTLINE_OBJ on the solid object body.
  - The flame is the ONE saturated accent (FIRE_OR/FIRE_LT) — it must read as the
    lit head; everything below it is muted wood so the eye lands on the flame.
  - Transparent background (RGBA), point-filter intent (PPU 16, FilterMode Point).
  - 16x16 = one grid cell, matching wall_wood / stove / door footprint sprites.

NOT EDITED: _gen_sprites.py, _gen_fix_audit.py, _gen_scatter_variety.py,
            palette.py (read-only import).

Lane contract (W-M4-04 Lane A):
  This file + lamp.png + BuildManager.cs (+ LampEntity.cs + LampGlowDriver.cs)
  are the files owned by Lane A.  No SceneSetup, no other generator, no
  NightLightPoolDriver edit.
"""
from __future__ import annotations
from pathlib import Path
from PIL import Image

# palette.py imported READ-ONLY — no modifications.
from palette import (
    OUTLINE_OBJ,
    WOOD_DK, WOOD_MD, WOOD_LT,
    FIRE_OR, FIRE_LT,
)

HERE = Path(__file__).resolve().parent


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im: Image.Image, x: int, y: int, c: tuple) -> None:
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


# ─────────────────────────────────────────────────────────────────────────────
# lamp.png   16x16
#
# A standing lamp / torch.  PIL origin is top-left, so y=0 is the TOP of the
# image (the flame head) and y=15 is the BOTTOM (the base on the ground).
#
# Layout (top -> bottom):
#   rows  1-4  : flame head  (FIRE_LT core, FIRE_OR shoulders) — un-outlined
#   rows  5-12 : wood post   (WOOD_MD body, WOOD_LT left highlight, WOOD_DK
#                             right shadow) — 1px OUTLINE_OBJ on the post edges
#   rows 12-14 : base/foot   (wider WOOD_MD/DK pad) — OUTLINE_OBJ skirt
#
# The post is centred on x=7/8.  Keeps a 1px transparent margin all round.
# ─────────────────────────────────────────────────────────────────────────────
def gen_lamp() -> Image.Image:
    im = new_canvas(16, 16)

    cx_l, cx_r = 7, 8  # the two centre columns of the post

    # ── Flame head (rows 1-4) ──────────────────────────────────────────
    # Teardrop flame: narrow top, wider shoulders, FIRE_LT hot core.
    flame_lt = [
        (7, 1),
        (7, 2), (8, 2),
        (7, 3), (8, 3),
    ]
    flame_or = [
        (8, 1),
        (6, 2), (9, 2),
        (6, 3), (9, 3),
        (6, 4), (7, 4), (8, 4), (9, 4),
    ]
    for x, y in flame_or:
        put(im, x, y, FIRE_OR)
    for x, y in flame_lt:
        put(im, x, y, FIRE_LT)

    # ── Wood post (rows 5-11) ──────────────────────────────────────────
    # Two-column post with a light-left / shadow-right read.
    for y in range(5, 12):
        put(im, cx_l, y, WOOD_LT if y % 2 == 0 else WOOD_MD)
        put(im, cx_r, y, WOOD_MD if y % 2 == 0 else WOOD_DK)

    # A small collar where flame meets post (rows 5) for grounding.
    put(im, 6, 5, WOOD_MD)
    put(im, 9, 5, WOOD_DK)

    # ── Base / foot (rows 12-14) ───────────────────────────────────────
    base_md = [
        (6, 12), (7, 12), (8, 12), (9, 12),
        (6, 13), (7, 13), (8, 13), (9, 13),
    ]
    base_dk = [
        (5, 13), (10, 13),
        (5, 14), (6, 14), (7, 14), (8, 14), (9, 14), (10, 14),
    ]
    for x, y in base_md:
        put(im, x, y, WOOD_MD)
    for x, y in base_dk:
        put(im, x, y, WOOD_DK)

    # ── 1px OUTLINE_OBJ on the solid wood body only (flame stays open) ──
    # Outline any transparent pixel 4-adjacent to a wood pixel that is at or
    # below the collar row (y >= 5), so the flame head is never boxed in.
    wood_cols = {WOOD_DK, WOOD_MD, WOOD_LT}
    outline_pts = []
    for y in range(im.height):
        for x in range(im.width):
            if im.getpixel((x, y))[3] != 0:
                continue
            if y < 5:
                continue  # leave the flame head un-outlined
            touches_wood = False
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < im.width and 0 <= ny < im.height:
                    if im.getpixel((nx, ny)) in wood_cols:
                        touches_wood = True
                        break
            if touches_wood:
                outline_pts.append((x, y))
    for x, y in outline_pts:
        put(im, x, y, OUTLINE_OBJ)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# Preview composite (QA self-check) — 8x scale on a dark night-ish panel.
# ─────────────────────────────────────────────────────────────────────────────
def gen_preview(spr: Image.Image) -> Image.Image:
    BG = (28, 26, 34, 255)  # dark night bg so the flame reads
    pad = 3
    W = spr.width + pad * 2
    H = spr.height + pad * 2
    canvas = Image.new("RGBA", (W, H), BG)
    canvas.paste(spr, (pad, pad), spr)
    scale = 8
    return canvas.resize((W * scale, H * scale), Image.NEAREST)


def main():
    im = gen_lamp()
    out = HERE / "lamp.png"
    im.save(out)
    print(f"[gen_lamp] lamp.png  {im.width}x{im.height}")

    prev = gen_preview(im)
    prev_path = HERE / "_preview_lamp.png"
    prev.save(prev_path)
    print(f"[gen_lamp] _preview_lamp.png  {prev.width}x{prev.height}  (8x scale QA)")


if __name__ == "__main__":
    main()
