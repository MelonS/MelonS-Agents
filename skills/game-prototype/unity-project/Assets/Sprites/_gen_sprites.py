# -*- coding: utf-8 -*-
"""_gen_sprites.py — tree + trader sprites.

OWNERSHIP MAP (after A1 palette unification, 2026-05-29):
  _gen_pawn.py      → pawn_colonist, pawn_blue, pawn_rust, pawn_olive
  _gen_fix_audit.py → wall_wood, floor_wood, stove, deer, wolf, crop_rice,
                      stone_vein, stone_chunk, wood_pile, meat_pile, research_bench
  _gen_bed.py       → bed_wood, bed_fine
  _gen_sprites.py   → tree, trader   (this file)

RETIRED from this file: gen_pawn (→ _gen_pawn.py), gen_wall_wood,
  gen_deer, gen_wood_pile, gen_stone_vein, gen_stone_chunk, gen_meat_pile
  (all → _gen_fix_audit.py).

Style: flat-Kenney (flat colour fills + OUTLINE_OBJ / OUTLINE_PLANT, NO
gradients, NO gaussian blur).  All colours from palette.py.

Polish Wave v3 V4/V5 (2026-05-30):
  gen_tree rewritten with 3-cluster directional canopy (GRASS_DK core +
  GRASS_MD mid-ring + GRASS_LT rim-light offset up-left) and a trunk-shadow
  pixel under the canopy.  Light direction: upper-left.  Still flat (no
  gradients), still recedes behind pawns (lower saturation than cloth).
"""
from __future__ import annotations
from PIL import Image, ImageDraw
import random
from pathlib import Path

from palette import (
    OUTLINE_OBJ   as OUTLINE,
    OUTLINE_PLANT,
    WOOD_DK, WOOD_MD, WOOD_LT,
    GRASS_DK, GRASS_MD, GRASS_LT,
    SKIN_MD, SKIN_SH, HAIR_DK,
)

# Additional colours needed for trader not in main palette export
_HOOD      = (78, 58, 96, 255)     # muted dusty purple hood (trader)
_HOOD_DK   = (52, 38, 66, 255)
_ROBE      = (100, 80, 116, 255)   # robe body
_ROBE_DK   = (64, 48, 80, 255)
_GOLD_ACC  = (190, 155, 62, 255)   # belt accent — matches CROP_GOLD family
_BOOT_DK   = (40, 28, 18, 255)     # dark boot

HERE = Path(__file__).resolve().parent


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put_px(im, x, y, color):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), color)


def hline(im, x0, x1, y, c):
    for x in range(x0, x1 + 1): put_px(im, x, y, c)


def vline(im, x, y0, y1, c):
    for y in range(y0, y1 + 1): put_px(im, x, y, c)


def rect_fill(im, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put_px(im, x, y, c)


def rect_outline(im, x0, y0, x1, y1, c):
    hline(im, x0, x1, y0, c); hline(im, x0, x1, y1, c)
    vline(im, x0, y0, y1, c); vline(im, x1, y0, y1, c)


# ─────────────────────────────────────────────────────────────────────────────
# tree.png — 16x16 flat-style top-down tree (Polish Wave v3 V4 improved)
#
# CHANGE from prior version:
#   BEFORE: 3 tone zones but roughly concentric rings — reads as a flat blob
#           with no clear light direction; mid and dark rings wrap equally.
#   AFTER:  3 clusters with explicit UP-LEFT light direction:
#             GRASS_LT  → tight cluster offset upper-left (rim light, ~5px)
#             GRASS_MD  → large mid-ring
#             GRASS_DK  → lower-right shadow mass (largest zone)
#           Plus: single-pixel trunk-shadow at canopy base (col 9, y=9)
#           so the trunk reads as going INTO the canopy shadow, not just
#           appearing below it.
#   Both before and after: OUTLINE_PLANT edge, WOOD_MD/DK trunk,
#   16x16, transparent bg, no gradients.
#
# Visual check:
#   - Canopy has THREE visually distinct brightness zones, not a uniform fill.
#   - Upper-left corner is the brightest cluster.
#   - Lower-right is the darkest shadow.
#   - Placed next to a pawn (CLOTH_BLUE = 74,96,132): pawn's outline pops,
#     tree recedes — GRASS_LT (118,138,86) has lower saturation than CLOTH_BLUE.
# ─────────────────────────────────────────────────────────────────────────────

def gen_tree() -> Image.Image:
    """16x16 flat top-down tree.
    3-cluster directional canopy (light from upper-left), trunk, OUTLINE_PLANT.
    """
    W = H = 16
    im = new_canvas(W, H)

    # ── ZONE 1: GRASS_DK — deep shadow (lower-right mass) ────────────────────
    # This is the large base fill — most of the canopy reads as recessed shadow.
    shadow_zone = [
        # Upper rows: only the far edges (shadow rim where light can't reach)
        (8, 1), (9, 1), (10, 1), (11, 1),
        (11, 2), (12, 2),
        (11, 3), (12, 3),
        (10, 4), (11, 4), (12, 4),
        (10, 5), (11, 5),
        (9, 6), (10, 6), (11, 6),
        # Mid canopy right side
        (8, 3), (9, 3), (10, 3),
        (8, 4), (9, 4),
        (8, 5), (9, 5),
        (8, 6), (9, 6),
        # Lower canopy (shadow mass) — the heaviest zone
        (5, 7), (6, 7), (7, 7), (8, 7), (9, 7), (10, 7), (11, 7),
        (5, 8), (6, 8), (7, 8), (8, 8), (9, 8), (10, 8),
    ]
    for x, y in shadow_zone:
        put_px(im, x, y, GRASS_DK)

    # ── ZONE 2: GRASS_MD — mid-tone ring (majority of canopy body) ───────────
    mid_zone = [
        # Upper canopy fill (centre and left-of-centre)
        (4, 2), (5, 2), (6, 2), (7, 2),
        (3, 3), (4, 3), (5, 3), (6, 3), (7, 3),
        (3, 4), (4, 4), (5, 4), (6, 4), (7, 4),
        (3, 5), (4, 5), (5, 5), (6, 5), (7, 5),
        (4, 6), (5, 6), (6, 6), (7, 6),
        # Canopy lower mid
        (3, 7), (4, 7),
        (3, 8), (4, 8),
    ]
    for x, y in mid_zone:
        put_px(im, x, y, GRASS_MD)

    # ── ZONE 3: GRASS_LT — rim-light cluster, upper-left ─────────────────────
    # A compact bright cluster offset toward (3-5, 1-4) — implies light from
    # upper-left.  Small enough to feel like a highlight, not a gradient wash.
    rimlight_zone = [
        (4, 1), (5, 1), (6, 1),
        (3, 2), (4, 2), (5, 2),      # overwrites some GRASS_MD — intentional
        (3, 3), (4, 3), (5, 3),
        (3, 4), (4, 4),
    ]
    for x, y in rimlight_zone:
        put_px(im, x, y, GRASS_LT)

    # ── Trunk — 2px wide, visible below canopy ────────────────────────────────
    # WOOD_LT (lighter) on left, WOOD_DK on right — same light-left convention
    vline(im, 7, 9, 14, WOOD_MD)
    vline(im, 8, 9, 14, WOOD_DK)

    # Trunk-shadow pixel: the pixel where trunk meets canopy base gets darkened
    # so the trunk reads as going INTO shadow under the canopy (depth cue).
    put_px(im, 7, 9, WOOD_DK)
    put_px(im, 8, 9, GRASS_DK)    # canopy-shadow bleeds onto trunk top

    # ── OUTLINE_PLANT border — silhouette edge only ───────────────────────────
    # Drawn on transparent pixels at the canopy edge; does not overwrite fills.
    outline_ring = [
        # Top edge
        (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 0), (9, 0), (10, 0), (11, 0), (12, 0),
        # Left edge
        (2, 1), (2, 2), (2, 3), (2, 4), (2, 5), (2, 6),
        # Right edge
        (13, 2), (13, 3), (13, 4), (13, 5), (13, 6),
        # Bottom of canopy
        (3, 9), (4, 9), (5, 9), (6, 9), (9, 9), (10, 9), (11, 9), (12, 9),
        # Lower-right corners
        (12, 7), (12, 8),
        # Lower-left corners
        (2, 7), (2, 8),
        # Transition into trunk sides
        (6, 9), (9, 9),
    ]
    for x, y in outline_ring:
        if 0 <= x < W and 0 <= y < H:
            if im.getpixel((x, y))[3] == 0:
                put_px(im, x, y, OUTLINE_PLANT)

    # Trunk side outlines (below canopy)
    for y in range(9, 15):
        if im.getpixel((6, y))[3] == 0:
            put_px(im, 6, y, OUTLINE_PLANT)
        if im.getpixel((9, y))[3] == 0:
            put_px(im, 9, y, OUTLINE_PLANT)
    # Trunk base
    hline(im, 7, 8, 15, OUTLINE_PLANT)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# trader.png — 16x16 flat robed humanoid (NPC)
# Flat colour fills, OUTLINE_OBJ (1px — NPC is object tier not pawn tier).
# ─────────────────────────────────────────────────────────────────────────────

def gen_trader() -> Image.Image:
    """16x16 flat-style hooded trader.  Muted purple robe + belt accent."""
    W = H = 16
    im = new_canvas(W, H)

    # Hood (top of head) — y 0..4, roughly oval
    hood_pixels = [
        (5,0),(6,0),(7,0),(8,0),(9,0),(10,0),
        (4,1),(5,1),(6,1),(7,1),(8,1),(9,1),(10,1),(11,1),
        (4,2),(5,2),(6,2),(7,2),(8,2),(9,2),(10,2),(11,2),
        (4,3),(5,3),(6,3),(7,3),(8,3),(9,3),(10,3),(11,3),
    ]
    for x, y in hood_pixels: put_px(im, x, y, _HOOD)

    # Face peek (skin) — small window
    for y in range(2, 5):
        for x in range(6, 10):
            put_px(im, x, y, SKIN_MD)
    # Face shadow bottom
    hline(im, 6, 9, 4, SKIN_SH)

    # Robe body — trapezoidal, widens toward bottom, y 4..12
    for y in range(4, 13):
        w_extra = (y - 4) // 3
        x0 = max(3, 5 - w_extra)
        x1 = min(12, 10 + w_extra)
        for x in range(x0, x1 + 1):
            put_px(im, x, y, _ROBE)
        # Edge darker
        put_px(im, x0, y, _ROBE_DK)
        put_px(im, x1, y, _ROBE_DK)

    # Gold belt accent (y=8)
    hline(im, 5, 10, 8, _GOLD_ACC)

    # Legs / boots (y 13..15, two foot columns)
    for y in range(13, 16):
        put_px(im, 5,  y, _BOOT_DK)
        put_px(im, 6,  y, _BOOT_DK)
        put_px(im, 9,  y, _BOOT_DK)
        put_px(im, 10, y, _BOOT_DK)

    # Outline (OUTLINE_OBJ — NPC treated as object tier for outline weight)
    # Hood rim
    outline = [
        (4,0),(11,0),
        (3,1),(12,1),
        (3,2),(12,2),
        (3,3),(12,3),
        (3,4),(12,4),
        # robe sides (approximately)
        (2,5),(13,5),(2,6),(13,6),(2,7),(13,7),
        (3,8),(12,8),(3,9),(12,9),(3,10),(12,10),
        (4,11),(11,11),(4,12),(11,12),
    ]
    for x, y in outline:
        if im.getpixel((x, y))[3] == 0:
            put_px(im, x, y, OUTLINE)
        else:
            put_px(im, x, y, OUTLINE)  # overwrite edge pixels too

    # Hood top outline
    for x in range(4, 12): put_px(im, x, 0, OUTLINE)
    hline(im, 4, 11, 15, OUTLINE)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# Main
# ─────────────────────────────────────────────────────────────────────────────

def main():
    targets = [
        ("tree.png",    gen_tree),
        ("trader.png",  gen_trader),
    ]
    for name, fn in targets:
        im = fn()
        out = HERE / name
        im.save(out)
        print(f"[gen_sprites] {name}  {im.size[0]}x{im.size[1]}")


if __name__ == "__main__":
    main()
