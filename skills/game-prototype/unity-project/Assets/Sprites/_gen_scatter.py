# -*- coding: utf-8 -*-
"""Polish Wave v3 V2 — world scatter decoration sprites.

Produces four tiny transparent-bg sprites for sparse ground scatter
(~1 per 6-10 grass tiles, deterministic seed in the scene builder).

  decor_rock.png       8x8   small stone — ROCK_DK/MD/LT, OUTLINE_PLANT edge
  grass_tuft.png       8x8   grass blades — GRASS_DK/MD/LT, no outline
  wildflower1.png      8x8   muted grey-lavender flower — DIRT_MD stem, ROCK_LT/ROCK_MD head
  wildflower2.png      8x8   muted grey-cool flower — GRASS_MD stem, ROCK_MD/ROCK_DK head

DESIGN RULES (V2 spec):
  - Colours STRICTLY from palette.py — no ad-hoc values anywhere.
  - All sprites must RECEDE: nothing brighter than SKIN_MD (224,176,132).
    Rock uses ROCK_* (grey-blue, sat 0.06-0.09).  Tufts use GRASS_DK/MD/LT
    (muted khaki, sat 0.37-0.40).  Flowers use ROCK_* head (sat <0.10),
    clearly below ALL pawn cloth (CLOTH_OLIVE min sat 0.375).
  - 1px OUTLINE_PLANT edge (rock only) or no outline (grass, flowers).
  - 8x8 canvas, transparent background.
  - No sprite should be mistaken for a pawn or a building.
  - Flowers differ by silhouette + stem, not by hue (both recede to grey):
      wf1: straight stem (DIRT_MD, warm brown), 4-petal cross head (ROCK_LT centre)
      wf2: bent stem (GRASS_MD, muted khaki), drooping 2-lobe head (ROCK_DK shadow)

_preview_scatter.png  6x-scaled composite QA image on dark panel bg.

WIRING FLAG (for Build Engineer / Programmer):
  Each PNG needs .meta + ForceImportAllAssets entry in SceneSetup.
  PPU = 16 (8x8 sprite at PPU16 = 0.5 world units = half a tile, correct).
  TextureImporter: filterMode = Point, compression = None.

  Scatter spawn location:
    Assets/Editor/SceneSetup.Game.Entities.cs  SetupWorldEntities()
    After the existing grass/dirt tile loop, add a deterministic scatter pass:
      - Seed: System.Random rng = new System.Random(42);
      - For each grass cell (x, y), roll rng.NextDouble():
          < 0.06  -> place decor_rock    (6% density)
          < 0.10  -> place grass_tuft    (4% density, cumulative)
          < 0.13  -> place wildflower1   (3% density)
          < 0.16  -> place wildflower2   (3% density)
      - SpriteRenderer.sortingOrder = ground_order + 1 (above ground, below pawns)
      - Do NOT place on any occupied cell (check EntityGrid.IsOccupied).
      - GameObject tag = "Decor" so the cell occupancy system ignores them.
"""
from __future__ import annotations
from pathlib import Path
from PIL import Image

from palette import (
    OUTLINE_PLANT,
    ROCK_DK, ROCK_MD, ROCK_LT,
    GRASS_DK, GRASS_MD, GRASS_LT,
    DIRT_MD,
)

HERE = Path(__file__).resolve().parent


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im: Image.Image, x: int, y: int, c: tuple) -> None:
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


# ─────────────────────────────────────────────────────────────────────────────
# decor_rock.png  8x8
# Rounded pebble, 5w x 4h oval.  ROCK_LT highlight top-left, ROCK_DK shadow
# bottom-right, ROCK_MD body.  OUTLINE_PLANT border (1px, transparent pixels only).
# ─────────────────────────────────────────────────────────────────────────────
def gen_decor_rock() -> Image.Image:
    im = new_canvas(8, 8)

    # Base body (ROCK_MD)
    body = [
        (3, 3), (4, 3),
        (2, 4), (3, 4), (4, 4), (5, 4),
        (2, 5), (3, 5), (4, 5), (5, 5),
        (3, 6), (4, 6),
    ]
    # Top-left highlight (ROCK_LT)
    highlight = [(3, 3), (4, 3), (2, 4)]
    # Bottom-right shadow (ROCK_DK)
    shadow = [(5, 5), (3, 6), (4, 6), (5, 4)]

    for x, y in body:      put(im, x, y, ROCK_MD)
    for x, y in highlight: put(im, x, y, ROCK_LT)
    for x, y in shadow:    put(im, x, y, ROCK_DK)

    # 1px OUTLINE_PLANT — only transparent neighbours
    outline = [
        (2, 2), (3, 2), (4, 2), (5, 2),
        (1, 3), (6, 3),
        (1, 4), (6, 4),
        (1, 5), (6, 5),
        (2, 6), (5, 6),
        (2, 7), (3, 7), (4, 7), (5, 7),
    ]
    for x, y in outline:
        if im.getpixel((x, y))[3] == 0:
            put(im, x, y, OUTLINE_PLANT)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# grass_tuft.png  8x8
# Three blades: left-lean, centre-straight, right-lean.
# Dense root row (y=7), GRASS_DK bases, GRASS_MD mid, GRASS_LT tips.
# No outline — 1px blades look clunky with border.
# ─────────────────────────────────────────────────────────────────────────────
def gen_grass_tuft() -> Image.Image:
    im = new_canvas(8, 8)

    # Dense root row
    for x in range(2, 7):
        put(im, x, 7, GRASS_DK)

    # Blade 1 — leans left
    put(im, 2, 6, GRASS_DK)
    put(im, 2, 5, GRASS_MD)
    put(im, 1, 4, GRASS_MD)
    put(im, 1, 3, GRASS_LT)

    # Blade 2 — straight centre
    put(im, 4, 6, GRASS_DK)
    put(im, 4, 5, GRASS_MD)
    put(im, 4, 4, GRASS_MD)
    put(im, 4, 3, GRASS_LT)
    put(im, 4, 2, GRASS_LT)

    # Blade 3 — leans right
    put(im, 6, 6, GRASS_DK)
    put(im, 6, 5, GRASS_MD)
    put(im, 7, 4, GRASS_MD)
    put(im, 7, 3, GRASS_LT)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# wildflower1.png  8x8
# Straight-stem flower.  Stem: DIRT_MD (warm brown, sat 0.45 — darker than SKIN).
# Head: 4-petal cross; petals ROCK_MD (sat 0.09), centre ROCK_LT (sat 0.06).
# Both head colours are far below CLOTH_OLIVE (min cloth sat 0.375).
# Leaf stub at mid-stem breaks the straight line.
# ─────────────────────────────────────────────────────────────────────────────
def gen_wildflower1() -> Image.Image:
    im = new_canvas(8, 8)

    # Root anchors
    put(im, 3, 7, GRASS_DK)
    put(im, 4, 7, GRASS_DK)

    # Stem — straight, warm brown
    put(im, 4, 6, DIRT_MD)
    put(im, 4, 5, DIRT_MD)
    put(im, 4, 4, DIRT_MD)

    # Leaf stub
    put(im, 3, 5, GRASS_MD)

    # Head: 4-petal cross
    put(im, 4, 3, ROCK_MD)   # petal bottom
    put(im, 3, 2, ROCK_MD)   # petal left
    put(im, 5, 2, ROCK_MD)   # petal right
    put(im, 4, 1, ROCK_MD)   # petal top
    put(im, 4, 2, ROCK_LT)   # bright centre

    return im


# ─────────────────────────────────────────────────────────────────────────────
# wildflower2.png  8x8
# Bent-stem, drooping 2-lobe head — distinct silhouette from wf1.
# Stem: GRASS_MD (muted khaki, sat 0.40), bends left.
# Head: 2-lobe cluster; lobes ROCK_MD, shadow ROCK_DK — same near-zero sat.
# Right leaf stub and different silhouette (drooping vs cross) distinguish it.
# ─────────────────────────────────────────────────────────────────────────────
def gen_wildflower2() -> Image.Image:
    im = new_canvas(8, 8)

    # Root anchors
    put(im, 4, 7, GRASS_DK)
    put(im, 5, 7, GRASS_DK)

    # Stem — bends left
    put(im, 4, 6, GRASS_DK)
    put(im, 4, 5, GRASS_MD)
    put(im, 3, 4, GRASS_MD)

    # Leaf stub (right side)
    put(im, 5, 5, GRASS_DK)

    # Head — drooping 2-lobe (left lobe higher)
    put(im, 2, 3, ROCK_MD)   # left lobe outer
    put(im, 3, 3, ROCK_MD)   # inner
    put(im, 2, 4, ROCK_DK)   # left lobe shadow/underside
    put(im, 4, 3, ROCK_MD)   # right lobe
    put(im, 4, 4, ROCK_DK)   # right lobe shadow
    put(im, 3, 2, ROCK_DK)   # top junction dark

    return im


# ─────────────────────────────────────────────────────────────────────────────
# Preview composite  (QA self-check)
# 6x scale on dark panel bg, all 8x8 sprites side by side with gaps.
# ─────────────────────────────────────────────────────────────────────────────
def gen_preview(sprites: list[tuple[str, Image.Image]]) -> Image.Image:
    SLOT = 12      # each sprite gets 12x12 slot (8x8 + 2px margin each side)
    GAP  = 4
    BG   = (42, 31, 24, 255)   # UI_PANEL_BG opaque
    n    = len(sprites)
    W    = n * SLOT + (n - 1) * GAP
    H    = SLOT
    canvas = Image.new("RGBA", (W, H), BG)
    for i, (_, spr) in enumerate(sprites):
        ox = i * (SLOT + GAP) + (SLOT - spr.width) // 2
        oy = (SLOT - spr.height) // 2
        canvas.paste(spr, (ox, oy), spr)
    scale = 6   # 6x for easy visual inspection
    return canvas.resize((W * scale, H * scale), Image.NEAREST)


def main():
    targets = [
        ("decor_rock.png",  gen_decor_rock),
        ("grass_tuft.png",  gen_grass_tuft),
        ("wildflower1.png", gen_wildflower1),
        ("wildflower2.png", gen_wildflower2),
    ]
    sprites = []
    for name, fn in targets:
        im = fn()
        out = HERE / name
        im.save(out)
        print(f"[gen_scatter] {name}  {im.width}x{im.height}")
        sprites.append((name, im))

    prev = gen_preview(sprites)
    prev_path = HERE / "_preview_scatter.png"
    prev.save(prev_path)
    print(f"[gen_scatter] _preview_scatter.png  {prev.width}x{prev.height}  (6x scale QA)")


if __name__ == "__main__":
    main()
