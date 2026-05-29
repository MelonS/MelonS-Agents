# -*- coding: utf-8 -*-
"""W-M4-06 Lane A — table + chair buildable sprite (wiki #20).

Produces ONE new buildable sprite PNG for the M4 build catalogue:

  table_chair.png   16x16   a small wooden dining spot:
                            - a low wood table top slab (WOOD_LT highlight on
                              WOOD_MD body) sitting on two short legs,
                            - a single wood stool / chair tucked to one side
                              (WOOD_MD seat + WOOD_DK shadow leg) so the piece
                              reads at a glance as an "eat/rec spot",
                            - 1px OUTLINE_OBJ border on the solid wood body
                              (the building/item convention shared with
                              wall_wood / stove / lamp / bed).

This is a COMBINED table+chair piece (one 1x1 build op) — the wiki row #20 is
"table+chair buildable (eat/rec spot)" and the prototype's other furniture
(stove, sleeping spot) are single-cell, so a combined piece keeps it one click
and one footprint while still reading as a table WITH a chair.

DESIGN RULES (same north-star as the rest of the catalogue):
  - Colors STRICTLY from palette.py — no ad-hoc values.
  - flat-Kenney language: solid fills, 1px OUTLINE_OBJ on the solid object body.
  - Muted WOOD ramp so the piece recedes like other furniture (no saturated
    accent — a table is background furniture, the eye lands on pawns).
  - Transparent background (RGBA), point-filter intent (PPU 16, FilterMode Point).
  - 16x16 = one grid cell, matching wall_wood / stove / lamp / door footprint
    sprites, and consistent with the bed/bench furniture read.

NOT EDITED: _gen_sprites.py, _gen_fix_audit.py, _gen_bed.py, _gen_stone_floor.py,
            _gen_lamp.py, _gen_scatter_variety.py, palette.py (read-only import).

Lane contract (W-M4-06 Lane A):
  This file + table_chair.png + BuildManager.cs + TableEntity.cs + ChairEntity.cs
  are the files owned by Lane A.  No SceneSetup, no other generator, no other
  entity edit.
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


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im: Image.Image, x: int, y: int, c: tuple) -> None:
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


# ─────────────────────────────────────────────────────────────────────────────
# table_chair.png   16x16
#
# A low dining table with a single stool tucked to its left.  PIL origin is
# top-left, so y=0 is the TOP of the image and y=15 is the BOTTOM (the legs /
# feet rest on the ground).
#
# Layout (top -> bottom):
#   rows  4-6   : table top slab (WOOD_LT highlight band over WOOD_MD body)
#                 spanning x=4..13 — the dominant horizontal read of a table.
#   rows  7-12  : two table legs (WOOD_DK) at x=5 and x=12.
#   rows  8-13  : stool to the left — a small WOOD_MD seat (rows 8-9, x=1..3)
#                 on a single WOOD_DK leg (rows 10-13, x=2), reading as a chair
#                 pulled up to the table.
#   1px OUTLINE_OBJ skirts the whole solid wood body.
#
# Keeps a 1px transparent margin where possible.  Centred-ish so the table is
# the dominant mass; the chair sits to its left so the combined silhouette
# clearly reads "a place to sit and eat".
# ─────────────────────────────────────────────────────────────────────────────
def gen_table_chair() -> Image.Image:
    im = new_canvas(16, 16)

    # ── Table top slab (rows 4-6) ──────────────────────────────────────
    # WOOD_MD body, WOOD_LT highlight along the top edge (catching light),
    # WOOD_DK along the bottom edge of the slab (under-shadow).
    for x in range(4, 14):
        put(im, x, 4, WOOD_LT)   # top highlight band
    for x in range(4, 14):
        put(im, x, 5, WOOD_MD)   # body
    for x in range(4, 14):
        put(im, x, 6, WOOD_DK)   # under-shadow band

    # ── Table legs (rows 7-12) ─────────────────────────────────────────
    for y in range(7, 13):
        put(im, 5, y, WOOD_DK)
        put(im, 12, y, WOOD_DK)
    # a touch of mid on the leg fronts so they aren't a flat black bar
    for y in range(7, 12):
        put(im, 5, y, WOOD_MD if y % 2 == 0 else WOOD_DK)
        put(im, 12, y, WOOD_MD if y % 2 == 0 else WOOD_DK)

    # ── Stool / chair, tucked left (rows 8-13) ─────────────────────────
    # Seat: a small 3-wide pad.
    for x in range(1, 4):
        put(im, x, 8, WOOD_LT)   # seat top highlight
        put(im, x, 9, WOOD_MD)   # seat body
    # Single centre leg under the seat.
    for y in range(10, 14):
        put(im, 2, y, WOOD_DK)
    put(im, 2, 10, WOOD_MD)      # one mid pixel so the leg reads round

    # ── 1px OUTLINE_OBJ on the solid wood body ─────────────────────────
    # Outline any transparent pixel 4-adjacent to a wood pixel (building/item
    # convention).  Collect first, then write (no self-feed).
    wood_cols = {WOOD_DK, WOOD_MD, WOOD_LT}
    outline_pts = []
    for y in range(im.height):
        for x in range(im.width):
            if im.getpixel((x, y))[3] != 0:
                continue
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
# Preview composite (QA self-check) — 8x scale on a neutral panel.
# ─────────────────────────────────────────────────────────────────────────────
def gen_preview(spr: Image.Image) -> Image.Image:
    BG = (46, 44, 40, 255)  # neutral muted bg so the wood reads
    pad = 3
    W = spr.width + pad * 2
    H = spr.height + pad * 2
    canvas = Image.new("RGBA", (W, H), BG)
    canvas.paste(spr, (pad, pad), spr)
    scale = 8
    return canvas.resize((W * scale, H * scale), Image.NEAREST)


def main():
    im = gen_table_chair()
    out = HERE / "table_chair.png"
    im.save(out)
    print(f"[gen_table_chair] table_chair.png  {im.width}x{im.height}")

    prev = gen_preview(im)
    prev_path = HERE / "_preview_table_chair.png"
    prev.save(prev_path)
    print(f"[gen_table_chair] _preview_table_chair.png  {prev.width}x{prev.height}  (8x scale QA)")


if __name__ == "__main__":
    main()
