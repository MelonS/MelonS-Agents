# -*- coding: utf-8 -*-
"""Polish Round v2 — grounding shadows + tilled crop soil.

Produces:
  shadow_small.png  16x8   soft semi-transparent ellipse for pawn / small object grounding
  shadow_tree.png   20x6   wider soft ellipse for tree trunk base grounding
  crop_rice.png     16x16  regenerated with full tilled-soil background (replaces _gen_fix_audit version)

_preview_polish_v2.png  composite QA image (4x scale)

CONTRACT: dimensions unchanged for existing sprites.  crop_rice stays 16x16.
shadow_small + shadow_tree are NEW files (no dimension contract to break).

Pawn shadow wiring (flag for programmer):
  shadow_small.png goes on a second SpriteRenderer child of the pawn prefab,
  sortingOrder = pawn.sortingOrder - 1 (render behind pawn),
  localPosition = (0, -0.25, 0)  (offset down by ~4px at PPU16)
  color = white (alpha comes from the sprite itself)
  See: Assets/Prefabs/Pawn.prefab + SceneSetup.Game.Prefabs.cs GeneratePawnPrefab()

Tree shadow wiring (flag for programmer):
  shadow_tree.png goes on a second SpriteRenderer child of the tree prefab,
  sortingOrder = tree.sortingOrder - 1,
  localPosition = (0, -0.44, 0)  (offset down to trunk base)
  See: Assets/Editor/SceneSetup.Game.Entities.cs SetupWorldEntities() tree spawn block
"""
from __future__ import annotations
import math
from pathlib import Path
from PIL import Image

from palette import (
    OUTLINE_OBJ,
    GRASS_DK,
    DIRT_DK, DIRT_MD, DIRT_LT,
    GRASS_MD,
    CROP_GOLD,
)

HERE = Path(__file__).resolve().parent

# Shadow colour: very dark warm-brown, semi-transparent
# Keep it warmer than a cold grey so it fits the earth palette
SHADOW_CORE  = (22, 15, 10, 110)   # dark warm brown, ~alpha 43% — soft core
SHADOW_EDGE  = (22, 15, 10, 48)    # same hue, ~alpha 19% — outer feather ring
SHADOW_OUTER = (22, 15, 10, 18)    # outermost wisp, barely visible

WHEAT    = CROP_GOLD
WHEAT_DK = (168, 136, 52, 255)    # derived, matches _gen_fix_audit

# ──────────────────────────────────────────────────────────────────────────────
# shadow_small.png  16x8
# Soft ellipse centred at (8, 4), semi-axes a=6 b=2.5
# Three-ring fade: core (a<0.5) → edge (0.5-0.8) → outer wisp (0.8-1.0)
# ──────────────────────────────────────────────────────────────────────────────
def gen_shadow_small() -> Image.Image:
    W, H = 16, 8
    cx, cy = 8.0, 4.0
    a, b = 6.0, 2.5   # semi-axes
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for y in range(H):
        for x in range(W):
            # distance from centre in ellipse coords
            dx = (x + 0.5 - cx) / a
            dy = (y + 0.5 - cy) / b
            r = math.sqrt(dx*dx + dy*dy)
            if r <= 0.50:
                im.putpixel((x, y), SHADOW_CORE)
            elif r <= 0.78:
                im.putpixel((x, y), SHADOW_EDGE)
            elif r <= 1.0:
                im.putpixel((x, y), SHADOW_OUTER)
    return im


# ──────────────────────────────────────────────────────────────────────────────
# shadow_tree.png  20x6
# Slightly wider and flatter (tree has a wider trunk silhouette than a pawn)
# Semi-axes a=8 b=2.2
# ──────────────────────────────────────────────────────────────────────────────
def gen_shadow_tree() -> Image.Image:
    W, H = 20, 6
    cx, cy = 10.0, 3.0
    a, b = 8.0, 2.2
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for y in range(H):
        for x in range(W):
            dx = (x + 0.5 - cx) / a
            dy = (y + 0.5 - cy) / b
            r = math.sqrt(dx*dx + dy*dy)
            if r <= 0.50:
                im.putpixel((x, y), SHADOW_CORE)
            elif r <= 0.78:
                im.putpixel((x, y), SHADOW_EDGE)
            elif r <= 1.0:
                im.putpixel((x, y), SHADOW_OUTER)
    return im


# ──────────────────────────────────────────────────────────────────────────────
# crop_rice.png  16x16  (FULL REPLACEMENT — keeps 16x16)
#
# Tilled soil background: rows 12-15 = DIRT_MD base with furrow stripes.
# Rows 0-11 transparent (crop stalks + heads float above soil).
# The CropEntity tint multiplier applies at runtime — choose soil colours that
# stay muted/readable under all three tints:
#   DIRT_MD (112,88,62) * SPROUT(0.51,0.78,0.31) → (57,69,19)  dark olive — fine
#   DIRT_MD (112,88,62) * RIPE  (0.85,0.75,0.20) → (95,66,12)  warm brown — fine
#   DIRT_LT (138,112,80) similarly — stays muted
# Furrow lines use DIRT_DK — slightly darker so they read through the tint.
# Soil patch covers bottom 4 rows (y=12..15) for clear "worked earth" read.
# Stalks occupy cols 3,6,9,12 (same as old), heads above.
# ──────────────────────────────────────────────────────────────────────────────
def gen_crop_rice() -> Image.Image:
    im = Image.new("RGBA", (16, 16), (0, 0, 0, 0))

    def put(x, y, c):
        if 0 <= x < 16 and 0 <= y < 16:
            im.putpixel((x, y), c)

    def hline(x0, x1, y, c):
        for x in range(x0, x1 + 1):
            put(x, y, c)

    def vline(x, y0, y1, c):
        for y in range(y0, y1 + 1):
            put(x, y, c)

    # ── Tilled soil background (rows 11-15) ──────────────────────────────────
    # Base fill: DIRT_MD on rows 11-15
    for y in range(11, 16):
        hline(0, 15, y, DIRT_MD)

    # Top-of-soil edge row (y=11): slightly lighter so soil "starts" visually
    hline(0, 15, 11, DIRT_LT)

    # Furrow lines: horizontal dark stripes at y=12, y=14 (tilled rows)
    # Use DIRT_DK — subtle but clearly darker than base
    hline(0, 15, 12, DIRT_DK)
    hline(0, 15, 14, DIRT_DK)

    # Vertical furrow dividers at stalk columns to show individual plant rows
    # Light, only in the soil zone (y=11..15)
    for sx in [3, 6, 9, 12]:
        for y in range(11, 16):
            # only overwrite if current pixel is DIRT base (not already a divider)
            px = im.getpixel((sx, y))
            if px == DIRT_MD or px == DIRT_LT:
                put(sx, y, DIRT_DK)

    # ── Crop stalks (y 6..10) ────────────────────────────────────────────────
    # Stalks in cols 3,6,9,12  (same column pattern as before)
    for sx in [3, 6, 9, 12]:
        vline(sx, 6, 10, GRASS_DK)

    # ── Grain heads ──────────────────────────────────────────────────────────
    # Slightly varied heights for natural look (same as old generator)
    for sx, top in [(3, 4), (6, 3), (9, 4), (12, 5)]:
        put(sx,     top,     WHEAT)
        put(sx,     top + 1, WHEAT)
        put(sx - 1, top + 1, WHEAT_DK)
        put(sx + 1, top + 1, WHEAT_DK)
        put(sx,     top - 1, WHEAT)

    return im


# ──────────────────────────────────────────────────────────────────────────────
# Preview composite  (QA self-check)
# Layout (4x scale, all on dark panel bg):
#   shadow_small | shadow_tree | crop_rice
# Each padded to 20x16 canvas for alignment, then 4x scaled.
# ──────────────────────────────────────────────────────────────────────────────
def gen_preview(sprites: list[tuple[str, Image.Image]]) -> Image.Image:
    # Normalise all to a 24x16 slot for side-by-side comparison
    SLOT_W, SLOT_H = 24, 20
    GAP = 4
    BG  = (42, 31, 24, 255)
    n   = len(sprites)
    W   = n * SLOT_W + (n - 1) * GAP
    H   = SLOT_H
    canvas = Image.new("RGBA", (W, H), BG)
    for i, (_, spr) in enumerate(sprites):
        ox = i * (SLOT_W + GAP) + (SLOT_W - spr.width) // 2
        oy = (SLOT_H - spr.height) // 2
        canvas.paste(spr, (ox, oy), spr)
    scale = 4
    return canvas.resize((W * scale, H * scale), Image.NEAREST)


def main():
    shadow_sm   = gen_shadow_small()
    shadow_tree = gen_shadow_tree()
    crop        = gen_crop_rice()

    outputs = [
        ("shadow_small.png",  shadow_sm),
        ("shadow_tree.png",   shadow_tree),
        ("crop_rice.png",     crop),
    ]
    for name, im in outputs:
        p = HERE / name
        im.save(p)
        print(f"[polish_v2] {name}  {im.width}x{im.height}")

    prev = gen_preview(outputs)
    prev_path = HERE / "_preview_polish_v2.png"
    prev.save(prev_path)
    print(f"[polish_v2] _preview_polish_v2.png  {prev.width}x{prev.height}")


if __name__ == "__main__":
    main()
