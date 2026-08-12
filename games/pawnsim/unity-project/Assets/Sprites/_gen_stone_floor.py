# -*- coding: utf-8 -*-
"""W-M4-05 Lane B — stone / paved floor buildable sprite (wiki #21).

Produces ONE new buildable sprite PNG for the M4 build catalogue:

  stone_floor.png   16x16   a cut paved-stone floor tile:
                            - a 2x2 grid of cut paving slabs on ROCK_MD,
                            - a darker mortar groove (ROCK_DK) at the mid
                              cross (x=7/8, y=7/8) so the four pavers read,
                            - a light bevel (ROCK_LT) on the top + left edge of
                              each paver for a hard, cut-stone read,
                            - a 1px OUTLINE_OBJ border around the whole tile
                              (building/item convention) — a HARDER edge than
                              the softer wood floor, so the two floor types are
                              clearly distinguishable at a glance.

Acceptance (binary, wiki Dim4 #21): a stone floor is buildable and gives a
HIGHER move bonus than the wood floor.  The HIGHER bonus is gameplay
(StoneFloorEntity.MoveBonus 1.50x vs wood 1.30x) — this file only authors the
distinguishing VISUAL: a cooler, harder, paved-stone read vs the warm wood floor.

DESIGN RULES (same north-star as the rest of the catalogue):
  - Colors STRICTLY from palette.py — no ad-hoc values (ROCK_DK/MD/LT + OUTLINE_OBJ).
  - flat-Kenney language: solid fills, 1px OUTLINE_OBJ border on the tile.
  - Slightly DARKER / harder read than the wood floor (cool grey-blue stone +
    a full hard border) so a player can tell stone from wood floor at a glance.
  - It must still RECEDE under pawns/structures (it is ground): no saturated
    accent, muted ROCK ramp only.
  - Transparent-free (full-bleed tile), point-filter intent (PPU 16, FilterMode Point).
  - 16x16 = one grid cell, matching floor / wall_wood / stove footprint sprites.

Pixel layout is kept BYTE-FOR-BYTE in sync with BuildManager.cs
BuildProceduralStoneFloorSprite() so the in-Editor PNG and the player-build
procedural fallback are visually interchangeable (the same trap Lane B fixed for
scatter: a PNG outside Resources/ returns null in a player build, so a
procedural fallback must match the authored art).

NOT EDITED: _gen_sprites.py, _gen_fix_audit.py, _gen_lamp.py,
            _gen_scatter_variety.py, palette.py (read-only import).

Lane contract (W-M4-05 Lane B):
  This file + stone_floor.png + BuildManager.cs (+ StoneFloorEntity.cs + a tiny
  FloorEntity bonus accessor + the 2-line PawnMovement bonus read) are owned by
  Lane B.  No SceneSetup, no other generator, no ArchitectMenu edit.
"""
from __future__ import annotations
from pathlib import Path
from PIL import Image

# palette.py imported READ-ONLY — no modifications.
from palette import (
    OUTLINE_OBJ,
    ROCK_DK, ROCK_MD, ROCK_LT,
)

HERE = Path(__file__).resolve().parent

SIZE = 16


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im: Image.Image, x: int, y: int, c: tuple) -> None:
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


# ─────────────────────────────────────────────────────────────────────────────
# stone_floor.png   16x16
#
# PIL origin is top-left (y=0 = TOP).  This MUST match BuildManager.cs
# BuildProceduralStoneFloorSprite() (which converts to bottom-up texture rows).
#
# Build order (each step may overwrite the previous, exactly like the C# code):
#   1. fill whole tile with ROCK_MD            (paved base)
#   2. mortar grooves ROCK_DK at x=7,8 and y=7,8   (the 2x2 slab cross)
#   3. light bevel ROCK_LT on top(y=1 / y=9) and left(x=1 / x=9) of each paver
#   4. 1px OUTLINE_OBJ border around the whole tile  (hard cut-stone edge)
# ─────────────────────────────────────────────────────────────────────────────
def gen_stone_floor() -> Image.Image:
    im = new_canvas(SIZE, SIZE)

    # 1) paved base — fill with mid rock.
    for y in range(SIZE):
        for x in range(SIZE):
            put(im, x, y, ROCK_MD)

    # 2) mortar grooves (darker) — vertical x=7/8, horizontal y=7/8.
    for i in range(SIZE):
        put(im, 7, i, ROCK_DK)
        put(im, 8, i, ROCK_DK)
        put(im, i, 7, ROCK_DK)
        put(im, i, 8, ROCK_DK)

    # 3a) light bevel on the TOP edge of each paver (top tile row y=1, and the
    #     row just below the horizontal groove y=9).
    for oy in (0, 9):
        row = 1 if oy == 0 else 9
        x_start = 1 if oy == 0 else 0
        for x in range(x_start, SIZE):
            if x in (7, 8):
                continue
            put(im, x, row, ROCK_LT)

    # 3b) light bevel on the LEFT edge of each paver (left tile col x=1, and the
    #     col just right of the vertical groove x=9).
    for ox in (0, 9):
        col = 1 if ox == 0 else 9
        for y in range(1, SIZE):
            if y in (7, 8):
                continue
            put(im, col, y, ROCK_LT)

    # 4) 1px OUTLINE_OBJ border around the whole tile (hard edge).
    for i in range(SIZE):
        put(im, i, 0, OUTLINE_OBJ)
        put(im, i, SIZE - 1, OUTLINE_OBJ)
        put(im, 0, i, OUTLINE_OBJ)
        put(im, SIZE - 1, i, OUTLINE_OBJ)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# Preview composite (QA self-check) — 8x scale, tiled 2x2 to verify the slabs
# repeat without an ugly seam, alongside a note.  Saved as _preview only.
# ─────────────────────────────────────────────────────────────────────────────
def gen_preview(spr: Image.Image) -> Image.Image:
    BG = (40, 44, 50, 255)  # neutral grey bg
    pad = 2
    tile = 2  # 2x2 tiling to check seam continuity
    W = spr.width * tile + pad * 2
    H = spr.height * tile + pad * 2
    canvas = Image.new("RGBA", (W, H), BG)
    for ty in range(tile):
        for tx in range(tile):
            canvas.paste(spr, (pad + tx * spr.width, pad + ty * spr.height), spr)
    scale = 8
    return canvas.resize((W * scale, H * scale), Image.NEAREST)


def main():
    im = gen_stone_floor()
    out = HERE / "stone_floor.png"
    im.save(out)
    print(f"[gen_stone_floor] stone_floor.png  {im.width}x{im.height}")

    prev = gen_preview(im)
    prev_path = HERE / "_preview_stone_floor.png"
    prev.save(prev_path)
    print(f"[gen_stone_floor] _preview_stone_floor.png  {prev.width}x{prev.height}  (8x, 2x2 tiled QA)")


if __name__ == "__main__":
    main()
