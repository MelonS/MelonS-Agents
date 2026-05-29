# -*- coding: utf-8 -*-
"""B11 terrain-edge decals + rock variety — NEW generator.

Produces 4 new low-saturation decal PNGs:
  rock_variant_a.png   12x9   wide flat slab  — broader, more angular than stone_chunk_small
  rock_variant_b.png    8x10  tall jagged spire — taller silhouette for shape contrast
  rock_variant_c.png   14x7   fractured plate   — wide, fractured multi-piece look
  water_edge_band.png  16x4   water/grass transition band — blends WATER_*/GRASS_* at edge

DESIGN RULES:
  - Colors STRICTLY from palette.py — no ad-hoc values.
  - ALL sprites must recede: saturation clearly below pawn cloth and skin.
      ROCK_DK sat=0.058, ROCK_MD sat=0.084, WATER_MD sat=0.270 — vs CLOTH_BLUE sat=0.440.
  - Rock variants: ROCK_DK/MD/LT body, 1px OUTLINE_OBJ outline (rock convention);
    not OUTLINE_PLANT because rocks are mineral objects not foliage.
  - water_edge_band: WATER_DK/MD/LT blend into GRASS_DK/MD at the boundary;
    no outline — it smears into the background.
  - Transparent background (RGBA), point-filter intent.
  - Three rock silhouettes must be visibly distinct from each other AND from the
    existing stone_chunk_small (10x8 angular) and decor_rock (smooth oval pebble).

NOT EDITED: _gen_fix_audit.py, _gen_sprites.py, palette.py (read-only import),
            _gen_scatter_variety.py, _gen_fence.py, _gen_barricade.py,
            _gen_table_chair.py, _gen_lamp.py.

Lane contract (W-M6-03 Lane B11):
  This file is the ONLY new generator owned by B11.  ScatterVarietyDriver.cs
  is extended (add-only) and the 4 PNGs saved to Assets/Resources/scatter/.
  No SceneSetup, entity, BuildManager, AudioBank, or test file is edited.
"""
from __future__ import annotations
from pathlib import Path
from PIL import Image

# palette.py imported READ-ONLY — no modifications.
from palette import (
    OUTLINE_OBJ,
    ROCK_DK, ROCK_MD, ROCK_LT,
    WATER_DK, WATER_MD, WATER_LT,
    GRASS_DK, GRASS_MD,
)

HERE = Path(__file__).resolve().parent
RESOURCES_SCATTER = HERE.parent / "Resources" / "scatter"


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im: Image.Image, x: int, y: int, c: tuple) -> None:
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


def apply_outline(im: Image.Image, color: tuple, candidates: list[tuple[int, int]]) -> None:
    """Place outline color on transparent pixels that are adjacent to an opaque body."""
    for x, y in candidates:
        if 0 <= x < im.width and 0 <= y < im.height and im.getpixel((x, y))[3] == 0:
            # only place on transparent pixels — body pixels keep their fill
            put(im, x, y, color)


# ─────────────────────────────────────────────────────────────────────────────
# rock_variant_a.png  12x9
#
# WIDE FLAT SLAB — broad, low-profile. Distinctly wider-than-tall, with a
# flat top face and a shadowed underside.  Different from stone_chunk_small
# (more angular, not as wide) and decor_rock (smooth oval).
#
# Silhouette: wide trapezoid, left edge slightly raised, right side heavier.
# Colors: ROCK_LT face (top), ROCK_MD mid, ROCK_DK deep shadow (bottom-right).
# Outline: 1px OUTLINE_OBJ on all transparent-adjacent pixels.
# ─────────────────────────────────────────────────────────────────────────────
def gen_rock_variant_a() -> Image.Image:
    im = new_canvas(12, 9)

    # Top face — lightest
    face_lt = [
        (2, 1), (3, 1), (4, 1), (5, 1),
        (1, 2), (2, 2), (3, 2), (4, 2), (5, 2), (6, 2),
        (1, 3), (2, 3), (3, 3),
    ]
    # Mid body
    face_md = [
        (6, 1), (7, 1),
        (7, 2), (8, 2),
        (4, 3), (5, 3), (6, 3), (7, 3), (8, 3),
        (2, 4), (3, 4), (4, 4), (5, 4), (6, 4), (7, 4), (8, 4), (9, 4),
        (3, 5), (4, 5), (5, 5), (6, 5), (7, 5), (8, 5),
        (4, 6), (5, 6), (6, 6), (7, 6),
    ]
    # Shadow underside — darkest
    face_dk = [
        (9, 3), (10, 3),
        (9, 4), (10, 4),
        (8, 5), (9, 5), (10, 5),
        (7, 6), (8, 6), (9, 6),
        (5, 7), (6, 7), (7, 7), (8, 7),
    ]

    for x, y in face_md: put(im, x, y, ROCK_MD)
    for x, y in face_lt: put(im, x, y, ROCK_LT)
    for x, y in face_dk: put(im, x, y, ROCK_DK)

    # 1px OUTLINE_OBJ border — transparent-adjacent only
    outline_candidates = [
        # top edge
        (1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 0),
        # left edge
        (0, 1), (0, 2), (0, 3), (0, 4), (1, 5),
        # right edge
        (8, 1), (9, 2), (10, 2), (11, 3), (11, 4), (11, 5), (10, 6), (9, 7),
        # bottom edge
        (3, 7), (4, 8), (5, 8), (6, 8), (7, 8), (8, 8),
        # left-bottom corner
        (2, 5), (1, 4),
    ]
    apply_outline(im, OUTLINE_OBJ, outline_candidates)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# rock_variant_b.png  8x10
#
# TALL JAGGED SPIRE — vertically elongated, narrow at the top, wider at base.
# Reads as a natural rock outcrop shard.  Distinctly different silhouette
# from the wide flat slab (A) and the existing angular chunk.
#
# Colors: ROCK_LT tip, ROCK_MD body, ROCK_DK base/shadow.
# Outline: 1px OUTLINE_OBJ.
# ─────────────────────────────────────────────────────────────────────────────
def gen_rock_variant_b() -> Image.Image:
    im = new_canvas(8, 10)

    # Tip — lightest (narrow top)
    tip_lt = [
        (3, 0), (4, 0),
        (2, 1), (3, 1), (4, 1), (5, 1),
        (2, 2), (3, 2),
    ]
    # Mid body
    body_md = [
        (4, 2), (5, 2),
        (1, 3), (2, 3), (3, 3), (4, 3), (5, 3), (6, 3),
        (1, 4), (2, 4), (3, 4), (4, 4), (5, 4),
        (2, 5), (3, 5), (4, 5),
        (2, 6), (3, 6),
    ]
    # Base shadow
    base_dk = [
        (5, 4), (6, 4),
        (4, 5), (5, 5), (6, 5),
        (3, 6), (4, 6), (5, 6), (6, 6),
        (2, 7), (3, 7), (4, 7), (5, 7), (6, 7),
        (3, 8), (4, 8), (5, 8),
    ]

    for x, y in body_md: put(im, x, y, ROCK_MD)
    for x, y in tip_lt:  put(im, x, y, ROCK_LT)
    for x, y in base_dk: put(im, x, y, ROCK_DK)

    outline_candidates = [
        # tip
        (2, 0), (3, 0), (4, 0), (5, 0),
        (1, 1), (6, 1),
        (1, 2), (6, 2),
        # left side
        (0, 3), (0, 4), (1, 5), (1, 6), (1, 7),
        # right side
        (7, 3), (7, 4), (7, 5), (7, 6),
        # base
        (2, 8), (6, 8),
        (2, 9), (3, 9), (4, 9), (5, 9), (6, 9),
    ]
    apply_outline(im, OUTLINE_OBJ, outline_candidates)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# rock_variant_c.png  14x7
#
# FRACTURED PLATE — very wide, very low.  Two separate rock pieces with a
# visible fracture gap between them, suggesting a split stone.  Most
# horizontally wide of the three new variants — maximises silhouette contrast.
#
# Colors: ROCK_LT left piece top, ROCK_MD right piece, ROCK_DK shadows.
# Outline: 1px OUTLINE_OBJ.
# ─────────────────────────────────────────────────────────────────────────────
def gen_rock_variant_c() -> Image.Image:
    im = new_canvas(14, 7)

    # Left piece — lighter top face
    left_lt = [
        (1, 1), (2, 1), (3, 1), (4, 1),
        (1, 2), (2, 2), (3, 2),
    ]
    left_md = [
        (4, 2), (5, 2),
        (2, 3), (3, 3), (4, 3), (5, 3),
        (3, 4), (4, 4),
    ]
    left_dk = [
        (4, 3), (5, 3),
        (4, 4), (5, 4),
        (3, 5), (4, 5),
    ]

    # Right piece — slightly darker overall (more shadow face)
    right_md = [
        (7, 0), (8, 0), (9, 0), (10, 0),
        (7, 1), (8, 1), (9, 1), (10, 1), (11, 1),
        (8, 2), (9, 2), (10, 2), (11, 2),
        (9, 3), (10, 3), (11, 3),
    ]
    right_dk = [
        (10, 1), (11, 1),
        (11, 2), (12, 2),
        (10, 3), (11, 3), (12, 3),
        (10, 4), (11, 4),
        (9, 4), (10, 4),
    ]
    right_lt = [
        (7, 0), (8, 0),
        (7, 1), (8, 1),
    ]

    for x, y in left_md:  put(im, x, y, ROCK_MD)
    for x, y in left_lt:  put(im, x, y, ROCK_LT)
    for x, y in left_dk:  put(im, x, y, ROCK_DK)
    for x, y in right_md: put(im, x, y, ROCK_MD)
    for x, y in right_lt: put(im, x, y, ROCK_LT)
    for x, y in right_dk: put(im, x, y, ROCK_DK)

    outline_candidates = [
        # left piece outline
        (0, 1), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0),
        (0, 2), (0, 3), (5, 1), (6, 2), (6, 3),
        (1, 4), (2, 5), (3, 6), (4, 6), (5, 5), (5, 6),
        # right piece outline
        (6, 0), (7, 0), (11, 0), (12, 1),
        (13, 2), (13, 3), (12, 4), (11, 5),
        (8, 5), (9, 5), (10, 5),
        (9, 4), (8, 4),
    ]
    apply_outline(im, OUTLINE_OBJ, outline_candidates)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# water_edge_band.png  16x4
#
# WATER/GRASS TRANSITION BAND — a horizontal strip that sits at the boundary
# where water tiles meet grass tiles.  Blends WATER_DK into GRASS_DK so
# the hard tile boundary is softened into an organic shore edge.
#
# Design: top 1 row = pure WATER_MD dithered with WATER_DK (deepest);
#         row 1 = WATER_MD with GRASS_DK intrusions (shore foam/algae);
#         row 2 = GRASS_DK with WATER_MD intrusions (wet grass);
#         row 3 = GRASS_MD (fully grass — matches terrain tile).
#
# No outline — it smears between tiles, must be nearly invisible against
# both the water and grass tiles it bridges.
# ─────────────────────────────────────────────────────────────────────────────
def gen_water_edge_band() -> Image.Image:
    W, H = 16, 4
    im = new_canvas(W, H)

    # Row 0 — deep water, slight dither with WATER_DK
    # alternating water tones to avoid uniform flat look
    for x in range(W):
        c = WATER_DK if (x % 3 == 0) else WATER_MD
        put(im, x, 0, c)

    # Row 1 — water with grass intrusions (algae/shore)
    # pattern: mostly WATER_MD, every 4th pixel GRASS_DK
    for x in range(W):
        if x % 4 == 1 or x % 7 == 3:
            put(im, x, 1, GRASS_DK)
        else:
            put(im, x, 1, WATER_MD)

    # Row 2 — grass with water intrusions (wet ground)
    # pattern: mostly GRASS_DK, every 5th pixel WATER_DK
    for x in range(W):
        if x % 5 == 2 or x % 6 == 4:
            put(im, x, 2, WATER_DK)
        else:
            put(im, x, 2, GRASS_DK)

    # Row 3 — solid grass base (fully matches terrain tile)
    for x in range(W):
        put(im, x, 3, GRASS_MD)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# Preview composite (QA self-check)
# ─────────────────────────────────────────────────────────────────────────────
def gen_preview(sprites: list[tuple[str, Image.Image]]) -> Image.Image:
    SLOT = 18
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
    # Ensure Resources/scatter exists
    RESOURCES_SCATTER.mkdir(parents=True, exist_ok=True)

    targets = [
        ("rock_variant_a.png", gen_rock_variant_a),
        ("rock_variant_b.png", gen_rock_variant_b),
        ("rock_variant_c.png", gen_rock_variant_c),
        ("water_edge_band.png", gen_water_edge_band),
    ]
    sprites = []
    for name, fn in targets:
        im = fn()
        # Save to Resources/scatter/ for Resources.Load in Unity
        out = RESOURCES_SCATTER / name
        im.save(out)
        print(f"[gen_edge_rocks] {name}  {im.width}x{im.height}  -> {out}")
        sprites.append((name, im))

    prev = gen_preview(sprites)
    prev_path = HERE / "_preview_edge_rocks.png"
    prev.save(prev_path)
    print(f"[gen_edge_rocks] _preview_edge_rocks.png  {prev.width}x{prev.height}  (6x scale QA)")


if __name__ == "__main__":
    main()
