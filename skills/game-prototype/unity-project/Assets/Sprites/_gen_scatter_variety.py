# -*- coding: utf-8 -*-
"""W-M4-03 Lane B — new scatter variety decal sprites.

Produces 3 new low-saturation decal PNGs that complement the V2 scatter
set (decor_rock / grass_tuft / wildflower1 / wildflower2).  These new
sprites are used ONLY by ScatterVarietyDriver.cs at runtime — they are
NOT added to the existing SceneSetup.Game.Scatter.cs pool (that file is
untouched per lane contract).

New sprites:
  stone_chunk_small.png   10x8   angular stone fragment — ROCK_* palette,
                                  OUTLINE_PLANT border; placed near StoneVeins.
  dead_leaves.png          8x8   irregular leaf-litter cluster — DIRT_DK/MD/LT;
                                  no outline; placed on GRASS_DK/DIRT tiles.
  pebble_scatter.png      10x6   flat multi-pebble cluster — ROCK_DK/MD, no body
                                  outline; second rock variant for variety.

DESIGN RULES (same north-star as V2 spec):
  - Colors STRICTLY from palette.py — no ad-hoc values.
  - Every sprite must RECEDE: saturation below ALL pawn cloth and skin.
      ROCK_DK sat 0.058, ROCK_MD sat 0.084, DIRT_DK sat 0.304 — all well below
      CLOTH_BLUE sat 0.440, SKIN_MD sat 0.411, CLOTH_OLIVE sat 0.375.
  - Outline: OUTLINE_PLANT for stone_chunk_small (1px, rock convention);
    no outline for dead_leaves or pebble_scatter (recede further).
  - Transparent background (RGBA), point-filter intent.
  - Designed to read as incidental ground noise from normal play-view distance,
    never as story-relevant objects.

NOT EDITED: _gen_fix_audit.py, _gen_sprites.py, palette.py (read-only import).

Lane contract (W-M4-03 Lane B):
  This file and ScatterVarietyDriver.cs are the ONLY files owned by Lane B.
  No SceneSetup, entity, SaveLoadManager, or test file is edited.
"""
from __future__ import annotations
from pathlib import Path
from PIL import Image

# palette.py imported READ-ONLY — no modifications.
from palette import (
    OUTLINE_PLANT,
    ROCK_DK, ROCK_MD, ROCK_LT,
    DIRT_DK, DIRT_MD, DIRT_LT,
    GRASS_DK, GRASS_MD,
)

HERE = Path(__file__).resolve().parent


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im: Image.Image, x: int, y: int, c: tuple) -> None:
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


# ─────────────────────────────────────────────────────────────────────────────
# stone_chunk_small.png   10x8
#
# An angular stone fragment — larger and more angular than decor_rock.png
# (which is a smooth oval pebble).  This one reads as a broken-off rock chunk,
# placed near StoneVein entities to suggest mining activity / natural fracture.
#
# Shape: irregular hexagon-ish, wider than tall.
# Colors: ROCK_LT face, ROCK_MD mid, ROCK_DK shadow underside.
# Outline: 1px OUTLINE_PLANT on transparent border pixels only.
# ─────────────────────────────────────────────────────────────────────────────
def gen_stone_chunk_small() -> Image.Image:
    im = new_canvas(10, 8)

    # Body pixels — irregular angular shape
    body_lt = [
        (2, 1), (3, 1), (4, 1),
        (1, 2), (2, 2), (3, 2),
        (1, 3), (2, 3),
    ]
    body_md = [
        (5, 1), (6, 1),
        (4, 2), (5, 2), (6, 2), (7, 2),
        (3, 3), (4, 3), (5, 3), (6, 3), (7, 3),
        (2, 4), (3, 4), (4, 4), (5, 4), (6, 4),
        (3, 5), (4, 5), (5, 5),
    ]
    body_dk = [
        (4, 4), (5, 4), (6, 4), (7, 3),
        (3, 5), (4, 5), (5, 5), (6, 5),
        (4, 6), (5, 6),
    ]

    for x, y in body_md: put(im, x, y, ROCK_MD)
    for x, y in body_lt: put(im, x, y, ROCK_LT)
    for x, y in body_dk: put(im, x, y, ROCK_DK)

    # 1px OUTLINE_PLANT only on transparent-adjacent pixels
    outline_candidates = [
        (1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 0),
        (0, 1), (7, 1), (8, 1),
        (0, 2), (8, 2),
        (0, 3), (8, 3),
        (1, 4), (7, 4), (8, 4),
        (2, 5), (7, 5),
        (3, 6), (6, 6),
        (4, 7), (5, 7),
    ]
    for x, y in outline_candidates:
        if 0 <= x < im.width and 0 <= y < im.height and im.getpixel((x, y))[3] == 0:
            put(im, x, y, OUTLINE_PLANT)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# dead_leaves.png   8x8
#
# A small cluster of fallen leaf-litter.  Placed on grass and dirt tiles.
# Reads as organic ground noise — warm-brown irregular shapes, no clean edges.
# Silhouette is deliberately irregular (not a rectangle).
#
# Colors: DIRT_DK base (darkest, warm brown), DIRT_MD mid, DIRT_LT highlight.
# No outline — leaves smear into the background per design spec.
# ─────────────────────────────────────────────────────────────────────────────
def gen_dead_leaves() -> Image.Image:
    im = new_canvas(8, 8)

    # Three overlapping leaf-shapes scattered asymmetrically
    # Leaf A — upper-left, DIRT_MD base
    leaf_a_md = [(1, 2), (2, 2), (1, 3), (2, 3)]
    leaf_a_lt = [(2, 1), (3, 1)]
    leaf_a_dk = [(1, 4), (2, 4)]

    # Leaf B — centre, DIRT_DK base (darkest for depth variety)
    leaf_b_dk = [(3, 3), (4, 3), (3, 4), (4, 4)]
    leaf_b_md = [(4, 2), (5, 2), (5, 3)]
    leaf_b_lt = [(5, 4), (4, 5)]

    # Leaf C — lower-right, DIRT_MD
    leaf_c_md = [(5, 5), (6, 5), (5, 6), (6, 6)]
    leaf_c_dk = [(6, 6), (7, 6), (6, 7)]
    leaf_c_lt = [(5, 4), (6, 4)]

    for x, y in leaf_a_dk: put(im, x, y, DIRT_DK)
    for x, y in leaf_b_dk: put(im, x, y, DIRT_DK)
    for x, y in leaf_c_dk: put(im, x, y, DIRT_DK)

    for x, y in leaf_a_md: put(im, x, y, DIRT_MD)
    for x, y in leaf_b_md: put(im, x, y, DIRT_MD)
    for x, y in leaf_c_md: put(im, x, y, DIRT_MD)

    for x, y in leaf_a_lt: put(im, x, y, DIRT_LT)
    for x, y in leaf_b_lt: put(im, x, y, DIRT_LT)
    for x, y in leaf_c_lt: put(im, x, y, DIRT_LT)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# pebble_scatter.png   10x6
#
# A loose cluster of 3 small pebbles — a second rock variant that differs
# from decor_rock.png (single oval pebble) by being a multi-pebble group.
# Gives more rock-reading variety without introducing new hues.
#
# Each pebble is 2-3 px wide.  Colors: ROCK_DK / ROCK_MD only (slightly
# darker mood than decor_rock which uses ROCK_LT highlight).
# No outline — pebbles are too small; an outline would make them read chunky.
# ─────────────────────────────────────────────────────────────────────────────
def gen_pebble_scatter() -> Image.Image:
    im = new_canvas(10, 6)

    # Pebble A — left (3px wide), dark
    put(im, 1, 2, ROCK_MD)
    put(im, 2, 2, ROCK_MD)
    put(im, 1, 3, ROCK_DK)
    put(im, 2, 3, ROCK_DK)
    put(im, 2, 4, ROCK_DK)

    # Pebble B — centre-top (2px wide), lighter
    put(im, 4, 1, ROCK_MD)
    put(im, 5, 1, ROCK_MD)
    put(im, 4, 2, ROCK_DK)
    put(im, 5, 2, ROCK_MD)

    # Pebble C — right (3px wide), mixed
    put(im, 7, 2, ROCK_MD)
    put(im, 8, 2, ROCK_MD)
    put(im, 7, 3, ROCK_DK)
    put(im, 8, 3, ROCK_MD)
    put(im, 8, 4, ROCK_DK)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# Preview composite  (QA self-check)
# 6x scale on dark panel bg, all three sprites side by side with gaps.
# ─────────────────────────────────────────────────────────────────────────────
def gen_preview(sprites: list[tuple[str, Image.Image]]) -> Image.Image:
    SLOT = 14     # each sprite gets 14px slot
    GAP  = 4
    BG   = (42, 31, 24, 255)
    n    = len(sprites)
    W    = n * SLOT + (n - 1) * GAP
    H    = SLOT
    canvas = Image.new("RGBA", (W, H), BG)
    for i, (_, spr) in enumerate(sprites):
        ox = i * (SLOT + GAP) + (SLOT - spr.width) // 2
        oy = (SLOT - spr.height) // 2
        canvas.paste(spr, (ox, oy), spr)
    scale = 6
    return canvas.resize((W * scale, H * scale), Image.NEAREST)


def main():
    targets = [
        ("stone_chunk_small.png", gen_stone_chunk_small),
        ("dead_leaves.png",       gen_dead_leaves),
        ("pebble_scatter.png",    gen_pebble_scatter),
    ]
    sprites = []
    for name, fn in targets:
        im = fn()
        out = HERE / name
        im.save(out)
        print(f"[gen_scatter_variety] {name}  {im.width}x{im.height}")
        sprites.append((name, im))

    prev = gen_preview(sprites)
    prev_path = HERE / "_preview_scatter_variety.png"
    prev.save(prev_path)
    print(f"[gen_scatter_variety] _preview_scatter_variety.png  {prev.width}x{prev.height}  (6x scale QA)")


if __name__ == "__main__":
    main()
