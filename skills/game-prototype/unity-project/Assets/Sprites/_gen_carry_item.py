# -*- coding: utf-8 -*-
"""W-M6-02 Lane B10 — carry-bundle sprite for PawnPoseDriver.

Produces ONE new sprite PNG:

  carry_bundle.png   8x8   a tiny wrapped resource bundle that a hauling
                            pawn holds above their body sprite.  Reads as a
                            generic item parcel at a glance (PPU16 scale).

DESIGN RULES (same north-star as rest of the catalogue):
  - Colors STRICTLY from palette.py — no ad-hoc values.
  - flat-Kenney language: solid fills, 1px OUTLINE_OBJ border.
  - Warm WOOD ramp body + DIRT highlight so it reads as a wrapped bundle,
    not a specific resource.  Palette stays muted so it never competes
    with the pawn silhouette.
  - Transparent background (RGBA), point-filter intent (PPU16).
  - 8x8 = half a grid cell, intentionally small so it sits above the pawn
    child renderer without obscuring the pawn face.

NOT EDITED: _gen_sprites.py, _gen_fix_audit.py, _gen_bed.py,
            _gen_stone_floor.py, _gen_lamp.py, _gen_scatter_variety.py,
            _gen_table_chair.py, palette.py (read-only import).

Lane contract (W-M6-02 Lane B10):
  This file + carry_bundle.png + PawnPoseDriver.cs are the ONLY files
  owned by Lane B10.  No SceneSetup, no existing-file edits.
"""
from __future__ import annotations
from pathlib import Path
from PIL import Image

# palette.py imported READ-ONLY.
from palette import (
    OUTLINE_OBJ,
    WOOD_DK, WOOD_MD, WOOD_LT,
    DIRT_LT,
)

HERE = Path(__file__).resolve().parent


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im: Image.Image, x: int, y: int, c: tuple) -> None:
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


# ─────────────────────────────────────────────────────────────────────────────
# carry_bundle.png  8x8
#
# A small wrapped parcel.  PIL origin top-left, y=0 top, y=7 bottom.
#
# Layout:
#   rows 0-0  : top outline row (OUTLINE_OBJ)
#   rows 1-1  : WOOD_LT highlight band (top face, catching light)
#   rows 2-5  : WOOD_MD body fill (main bundle mass)
#   row  6-6  : WOOD_DK bottom shadow strip
#   rows 7-7  : bottom outline row (OUTLINE_OBJ)
#   col  0    : left outline column
#   col  7    : right outline column
#   rows 3-3  : DIRT_LT rope/string horizontal band across center (x 1..6)
#   col  3-4  : DIRT_LT rope vertical stripe (y 1..6) — crossing the horizontal
#               so the bundle looks tied
# ─────────────────────────────────────────────────────────────────────────────
def gen_carry_bundle() -> Image.Image:
    im = new_canvas(8, 8)

    # Body fill (rows 1-6, cols 1-6 — inside the outline border)
    for y in range(1, 7):
        for x in range(1, 7):
            put(im, x, y, WOOD_MD)

    # Top highlight band (row 1, cols 1-6)
    for x in range(1, 7):
        put(im, x, 1, WOOD_LT)

    # Bottom shadow strip (row 6, cols 1-6)
    for x in range(1, 7):
        put(im, x, 6, WOOD_DK)

    # Rope/string — horizontal band (row 3, cols 1-6)
    for x in range(1, 7):
        put(im, x, 3, DIRT_LT)

    # Rope/string — vertical stripe (col 3 and col 4, rows 1-6)
    for y in range(1, 7):
        put(im, 3, y, DIRT_LT)
        put(im, 4, y, DIRT_LT)

    # Outline border (1px, all four edges)
    for x in range(8):
        put(im, x, 0, OUTLINE_OBJ)
        put(im, x, 7, OUTLINE_OBJ)
    for y in range(8):
        put(im, 0, y, OUTLINE_OBJ)
        put(im, 7, y, OUTLINE_OBJ)

    return im


def main() -> None:
    bundle = gen_carry_bundle()
    out = HERE / "carry_bundle.png"
    bundle.save(out)
    print(f"[_gen_carry_item] saved {out}  ({bundle.width}x{bundle.height})")


if __name__ == "__main__":
    main()
