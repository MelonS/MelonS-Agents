# -*- coding: utf-8 -*-
"""W-M6-03 B4 — barricade / sandbag buildable sprite (wiki row B4).

Produces ONE new buildable sprite PNG for the M4 build catalogue:

  barricade.png   16x16   a LOW sandbag barricade: two staggered rows of piled
                          sandbags, drawn in the LOWER HALF of the tile so it
                          reads as a waist-height defensive line (never a wall),
                          on muted DIRT / WOOD palette tones (sandbag canvas =
                          earthy dirt, with a wood-toned tie/strap accent).

UNLIKE the fence (which is passable), the placed BarricadeEntity BLOCKS PATHING
like a low wall — a pawn paths AROUND it (B4 acceptance: "pawns path around it
like a low wall").  That impassability is a RUNTIME property of the prefab /
PathGrid registration (BarricadeEntity reuses the wall pathing-block API),
NOT of this sprite; this file only authors the object's pixels.

DESIGN RULES (same north-star as the rest of the catalogue — fence/lamp/table/
stone-floor):
  - Colors STRICTLY from palette.py — no ad-hoc values.
  - flat-Kenney language: solid fills, 1px OUTLINE_OBJ on the solid object body.
  - LOW read: all sandbags sit at y >= 7 (lower half of the 16x16 tile) leaving
    the top half transparent, so the eye reads "low barrier" not "wall".
  - Muted DIRT tones for the sandbag canvas (sand/earth), with a WOOD-toned tie
    strap accent so it reads as lashed sandbags, not bare soil.
  - Transparent background (RGBA), point-filter intent (PPU 16, FilterMode Point).
  - 16x16 = one grid cell, matching wall_wood / stove / lamp / table / fence.

The procedural fallback in BuildManager.BuildProceduralBarricadeSprite mirrors
this authored content pixel-for-pixel so a player build (where the PNG is absent
/ outside Resources/) shows the SAME barricade — never null/magenta.

NOT EDITED: _gen_sprites.py, _gen_fix_audit.py, _gen_fence.py, _gen_table_chair.py,
            _gen_lamp.py, _gen_stone_floor.py, _gen_scatter_variety.py,
            palette.py (all read-only).

Lane contract (W-M6-03 B4):
  This file + barricade.png + BuildManager.cs + BarricadeEntity.cs are the files
  owned by this lane.  No SceneSetup, no other generator edit, no cover-math.
"""
from __future__ import annotations
from pathlib import Path
from PIL import Image

# palette.py imported READ-ONLY — no modifications.
from palette import (
    OUTLINE_OBJ,
    DIRT_DK, DIRT_MD, DIRT_LT,
    WOOD_DK,
)

HERE = Path(__file__).resolve().parent

# Solid object colours for the 4-adjacent outline pass (sandbag canvas + tie).
SOLID_COLS = {DIRT_DK, DIRT_MD, DIRT_LT, WOOD_DK}


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im: Image.Image, x: int, y: int, c: tuple) -> None:
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


def add_outline(im: Image.Image) -> None:
    """1px OUTLINE_OBJ on transparent pixels 4-adjacent to a solid (collect first)."""
    pts = []
    for y in range(im.height):
        for x in range(im.width):
            if im.getpixel((x, y))[3] != 0:
                continue
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < im.width and 0 <= ny < im.height:
                    if im.getpixel((nx, ny)) in SOLID_COLS:
                        pts.append((x, y))
                        break
    for x, y in pts:
        put(im, x, y, OUTLINE_OBJ)


# ─────────────────────────────────────────────────────────────────────────────
# barricade.png   16x16
#
# PIL origin is top-left (y=0 = TOP).  We keep ALL sandbags at y >= 7 (lower
# half) so the barricade reads LOW.  Two staggered rows of "sandbags":
#
#   back row  (rows 7..9)  : bags centred on x = 3, 7, 11  (offset right)
#   front row (rows 10..13): bags centred on x = 1, 5, 9, 13 (offset left, taller)
#
# Each sandbag = a 3-wide rounded lump: DIRT_LT top highlight, DIRT_MD body,
# DIRT_DK shadow underside.  A 1px WOOD_DK tie strap crosses the front-row bags
# at row 11 so they read as lashed sandbags, not bare mounds.
# ─────────────────────────────────────────────────────────────────────────────
def sandbag(im: Image.Image, cx: int, y_top: int, h: int) -> None:
    """Draw a 3-wide sandbag lump centred at column cx, top at y_top, height h."""
    for row in range(h):
        y = y_top + row
        if row == 0:
            cols = (DIRT_LT, DIRT_LT, DIRT_LT)        # rounded top highlight
        elif row == h - 1:
            cols = (DIRT_DK, DIRT_DK, DIRT_DK)        # shadow underside
        else:
            cols = (DIRT_MD, DIRT_LT, DIRT_MD)        # body with a centre catch-light
        for dx, c in zip((-1, 0, 1), cols):
            put(im, cx + dx, y, c)


def gen_barricade() -> Image.Image:
    im = new_canvas(16, 16)

    # Back row — shorter bags (height 3), staggered to the right.
    for cx in (3, 7, 11):
        sandbag(im, cx, 7, 3)

    # Front row — taller bags (height 4), staggered to the left, overlapping the
    #  back row so the pile reads as a stacked low barrier.
    for cx in (1, 5, 9, 13):
        sandbag(im, cx, 10, 4)

    # WOOD_DK tie strap across the front row (row 11) — lashed-sandbag read.
    for x in range(0, 16):
        if im.getpixel((x, 11))[3] != 0:   # only over an existing bag pixel
            put(im, x, 11, WOOD_DK)

    add_outline(im)
    return im


# ─────────────────────────────────────────────────────────────────────────────
# Preview composite (QA self-check) — 8x scale on a grass-ish panel.
# ─────────────────────────────────────────────────────────────────────────────
def gen_preview(spr: Image.Image) -> Image.Image:
    BG = (96, 116, 70, 255)  # GRASS_MD-ish so the low barricade reads in context
    pad = 3
    W = spr.width + pad * 2
    H = spr.height + pad * 2
    canvas = Image.new("RGBA", (W, H), BG)
    canvas.paste(spr, (pad, pad), spr)
    scale = 8
    return canvas.resize((W * scale, H * scale), Image.NEAREST)


def main():
    bar = gen_barricade()
    bar.save(HERE / "barricade.png")
    print(f"[gen_barricade] barricade.png  {bar.width}x{bar.height}")

    prev = gen_preview(bar)
    prev.save(HERE / "_preview_barricade.png")
    print(f"[gen_barricade] _preview_barricade.png  {prev.width}x{prev.height}  (8x scale QA)")


if __name__ == "__main__":
    main()
