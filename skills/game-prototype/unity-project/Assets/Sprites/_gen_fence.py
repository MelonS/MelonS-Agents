# -*- coding: utf-8 -*-
"""W-M6-02 B3 — fence + fence-gate buildable sprites (wiki row B3).

Produces TWO new buildable sprite PNGs for the M4 build catalogue:

  fence.png        16x16   a LOW wooden fence: two horizontal rails on three
                           short posts, drawn in the LOWER HALF of the tile so
                           it reads as a waist-height barrier (never a wall).
  fence_gate.png   16x16   the gate variant: same low rails on TWO jamb posts
                           with a CENTRE OPENING (the rails stop at the gap) so
                           it reads as the door/opening in a fence run.

Both are PASSABLE in-game — the placed FenceEntity carries NO collider, so a
pawn paths straight through (B3 acceptance: "blocks nothing but renders a low
barrier").  Passability is a runtime property of the prefab, NOT of this sprite;
this file only authors the object's pixels.

DESIGN RULES (same north-star as the rest of the catalogue):
  - Colors STRICTLY from palette.py — no ad-hoc values.
  - flat-Kenney language: solid fills, 1px OUTLINE_OBJ on the solid object body.
  - LOW read: all wood sits at y >= 7 (lower half of the 16x16 tile) leaving the
    top half transparent, so the eye reads "low barrier" not "wall".
  - Transparent background (RGBA), point-filter intent (PPU 16, FilterMode Point).
  - 16x16 = one grid cell, matching wall_wood / stove / lamp / table footprints.

The procedural fallback in BuildManager.BuildProceduralFenceSprite mirrors this
authored content pixel-for-pixel so a player build (where the PNG is absent /
outside Resources/) shows the SAME fence — never null/magenta.

NOT EDITED: _gen_sprites.py, _gen_fix_audit.py, _gen_table_chair.py, _gen_lamp.py,
            _gen_stone_floor.py, _gen_scatter_variety.py, palette.py (read-only).

Lane contract (W-M6-02 B3):
  This file + fence.png + fence_gate.png + BuildManager.cs + FenceEntity.cs are
  the files owned by this lane.  No SceneSetup, no other generator edit.
"""
from __future__ import annotations
from pathlib import Path
from PIL import Image

# palette.py imported READ-ONLY — no modifications.
from palette import (
    OUTLINE_OBJ,
    WOOD_DK, WOOD_MD, WOOD_LT,
)

HERE = Path(__file__).resolve().parent

WOOD_COLS = {WOOD_DK, WOOD_MD, WOOD_LT}


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im: Image.Image, x: int, y: int, c: tuple) -> None:
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


def add_outline(im: Image.Image) -> None:
    """1px OUTLINE_OBJ on transparent pixels 4-adjacent to wood (collect first)."""
    pts = []
    for y in range(im.height):
        for x in range(im.width):
            if im.getpixel((x, y))[3] != 0:
                continue
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < im.width and 0 <= ny < im.height:
                    if im.getpixel((nx, ny)) in WOOD_COLS:
                        pts.append((x, y))
                        break
    for x, y in pts:
        put(im, x, y, OUTLINE_OBJ)


# ─────────────────────────────────────────────────────────────────────────────
# fence.png / fence_gate.png   16x16
#
# PIL origin is top-left (y=0 = TOP).  We keep ALL wood at y >= 7 (lower half)
# so the fence reads LOW.  Posts are 2px-wide short uprights (rows 7..13); two
# horizontal rails at rows 8 (top, highlight) and 11 (mid, shadow).
#
# Plain fence : posts at x=2, x=8, x=13; rails span x=2..14.
# Gate variant: posts at x=2, x=13 (jambs only); rails span x=2..14 BUT skip the
#               centre opening x=6..9, so a clear gap reads as the gate.
# ─────────────────────────────────────────────────────────────────────────────
def gen_fence(is_gate: bool) -> Image.Image:
    im = new_canvas(16, 16)

    posts = [2, 13] if is_gate else [2, 8, 13]
    for px0 in posts:
        for y in range(7, 14):
            put(im, px0,     y, WOOD_MD if y % 2 == 0 else WOOD_DK)
            put(im, px0 + 1, y, WOOD_DK if y % 2 == 0 else WOOD_MD)

    for x in range(2, 15):
        if is_gate and 6 <= x <= 9:
            continue  # gate opening — rails stop at the gap
        put(im, x, 8,  WOOD_LT if x % 2 == 0 else WOOD_MD)   # top rail (highlight)
        put(im, x, 11, WOOD_MD if x % 2 == 0 else WOOD_DK)   # mid rail (shadow)

    add_outline(im)
    return im


# ─────────────────────────────────────────────────────────────────────────────
# Preview composite (QA self-check) — 8x scale on a grass-ish panel.
# ─────────────────────────────────────────────────────────────────────────────
def gen_preview(spr: Image.Image) -> Image.Image:
    BG = (96, 116, 70, 255)  # GRASS_MD-ish so the low fence reads in context
    pad = 3
    W = spr.width + pad * 2
    H = spr.height + pad * 2
    canvas = Image.new("RGBA", (W, H), BG)
    canvas.paste(spr, (pad, pad), spr)
    scale = 8
    return canvas.resize((W * scale, H * scale), Image.NEAREST)


def main():
    fence = gen_fence(is_gate=False)
    fence.save(HERE / "fence.png")
    print(f"[gen_fence] fence.png  {fence.width}x{fence.height}")

    gate = gen_fence(is_gate=True)
    gate.save(HERE / "fence_gate.png")
    print(f"[gen_fence] fence_gate.png  {gate.width}x{gate.height}")

    for nm, spr in (("fence", fence), ("fence_gate", gate)):
        prev = gen_preview(spr)
        prev.save(HERE / f"_preview_{nm}.png")
        print(f"[gen_fence] _preview_{nm}.png  {prev.width}x{prev.height}  (8x scale QA)")


if __name__ == "__main__":
    main()
