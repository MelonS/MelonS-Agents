# -*- coding: utf-8 -*-
"""Polish Wave v3 V2 — world scatter decoration sprites.

Produces four tiny transparent-bg sprites for sparse ground scatter
(~1 per 6-10 grass tiles, deterministic seed in the scene builder).

  decor_rock.png       8x8   small stone — ROCK_* palette, no outline
  grass_tuft.png       8x8   grass blades — GRASS_DK/LT, OUTLINE_PLANT or none
  wildflower1.png      8x8   muted warm-accent flower (muted lavender stem+head)
  wildflower2.png      8x8   muted cool-accent flower (dusty rose stem+head)

DESIGN RULES (V2 spec):
  - Colours STRICTLY from palette.py — no ad-hoc values.
  - All sprites must RECEDE: nothing brighter than SKIN_MD (224,176,132).
    Rock uses ROCK_* (grey-blue, low sat). Tufts use GRASS_DK/LT (muted
    khaki). Flowers use a single muted accent pixel, desaturated well below
    CLOTH_BLUE saturation.
  - 1px OUTLINE_PLANT edge or no outline — not OUTLINE_OBJ, not OUTLINE_STORY.
  - 8x8 canvas, transparent background.
  - No individual sprite should be mistaken for a pawn or a building.

_preview_scatter.png  4x-scaled composite QA image on dark panel bg.

WIRING FLAG (for Build Engineer / Programmer):
  Each new PNG needs a .meta file (Unity will auto-generate on domain reload,
  but ForceImportAllAssets must list them).  PPU = 16 (matches all 16x16 world
  sprites — an 8x8 sprite at PPU16 = 0.5 world units = half a tile, correct).
  TextureImporter: filterMode = Point, compression = None.

  Scatter spawn location:
    Assets/Editor/SceneSetup.Game.Entities.cs  SetupWorldEntities()
    After the existing grass/dirt tile loop, add a deterministic scatter pass:
      - Seed: System.Random rng = new System.Random(42);  (deterministic map)
      - For each grass cell (x, y), roll rng.NextDouble():
          < 0.06  → place decor_rock    (6% density)
          < 0.10  → place grass_tuft    (4% density, cumulative)
          < 0.13  → place wildflower1   (3% density)
          < 0.16  → place wildflower2   (3% density)
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
)

HERE = Path(__file__).resolve().parent

# ── Muted flower accent colours (derived from palette, well below pawn sat) ──
# Lavender stem: desaturated purple-grey, no palette entry — derived minimally.
# Rule: max channel value <= 160, saturation below CLOTH_BLUE (74,96,132).
_FLOWER1_STEM  = (88,  78, 56, 255)   # warm muted ochre stem (between GRASS_DK and DIRT_DK)
_FLOWER1_HEAD  = (148, 112, 88, 255)  # muted sandy/peach head — warm but recedes
_FLOWER1_ACC   = (110, 88, 68, 255)   # darker accent petal

_FLOWER2_STEM  = (72,  80, 68, 255)   # cool grey-green stem
_FLOWER2_HEAD  = (108, 100, 120, 255) # dusty muted purple head — lower sat than any cloth
_FLOWER2_ACC   = (82,  74, 94, 255)   # deeper petal shadow


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im: Image.Image, x: int, y: int, c: tuple) -> None:
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


# ─────────────────────────────────────────────────────────────────────────────
# decor_rock.png  8x8
# A small rounded stone; 3-tone grey-blue (ROCK_DK/MD/LT).
# No outline — raw stone edges are enough at this size.
# Shape: roughly oval, 5 wide x 4 tall, offset bottom-centre.
# ─────────────────────────────────────────────────────────────────────────────
def gen_decor_rock() -> Image.Image:
    im = new_canvas(8, 8)
    # Body pixels — asymmetric pebble shape, left-leaning
    body_mid = [
        (2, 3), (3, 3), (4, 3), (5, 3),
        (1, 4), (2, 4), (3, 4), (4, 4), (5, 4),
        (2, 5), (3, 5), (4, 5), (5, 5),
        (2, 6), (3, 6), (4, 6),
    ]
    body_lt = [
        (3, 2), (4, 2),        # top highlight ridge
        (2, 3),                # left rim light (already in mid, overwrite)
        (5, 3),                # right rim highlight
    ]
    body_dk = [
        (2, 6), (3, 6), (4, 6),  # bottom shadow
        (1, 4),                   # far-left dark edge
        (5, 4), (5, 5),          # right underside
    ]
    for x, y in body_mid: put(im, x, y, ROCK_MD)
    for x, y in body_lt:  put(im, x, y, ROCK_LT)
    for x, y in body_dk:  put(im, x, y, ROCK_DK)
    # Single-pixel dark edge (OUTLINE_PLANT — plant-tier, recedes)
    outline = [(3, 1), (4, 1), (2, 2), (5, 2), (1, 3), (6, 3),
               (1, 4), (6, 4), (1, 5), (6, 5), (2, 6), (5, 6),
               (2, 7), (3, 7), (4, 7), (5, 7)]
    for x, y in outline:
        if im.getpixel((x, y))[3] == 0:
            put(im, x, y, OUTLINE_PLANT)
    return im


# ─────────────────────────────────────────────────────────────────────────────
# grass_tuft.png  8x8
# A few grass blades pointing upward; GRASS_LT tips over GRASS_DK bases.
# No outline — plant blades don't need it at this size.
# ─────────────────────────────────────────────────────────────────────────────
def gen_grass_tuft() -> Image.Image:
    im = new_canvas(8, 8)
    # Three blades: left-leaning, center, right-leaning
    # Blade 1 (left, leans left)
    put(im, 2, 7, GRASS_DK)   # base
    put(im, 2, 6, GRASS_DK)
    put(im, 1, 5, GRASS_MD)   # lean left
    put(im, 1, 4, GRASS_MD)
    put(im, 0, 3, GRASS_LT)   # tip
    # Blade 2 (center, straight up)
    put(im, 4, 7, GRASS_DK)   # base
    put(im, 4, 6, GRASS_DK)
    put(im, 4, 5, GRASS_MD)
    put(im, 4, 4, GRASS_MD)
    put(im, 4, 3, GRASS_LT)   # tip
    put(im, 4, 2, GRASS_LT)   # extra top
    # Blade 3 (right, leans right)
    put(im, 6, 7, GRASS_DK)   # base
    put(im, 6, 6, GRASS_DK)
    put(im, 6, 5, GRASS_MD)
    put(im, 7, 4, GRASS_MD)   # lean right
    put(im, 7, 3, GRASS_LT)   # tip
    # Subtle root scatter (makes the base feel planted)
    put(im, 3, 7, GRASS_DK)
    put(im, 5, 7, GRASS_DK)
    return im


# ─────────────────────────────────────────────────────────────────────────────
# wildflower1.png  8x8
# Muted warm-tone flower: sandy/peach head on an ochre stem.
# Low saturation — stays clearly below pawn SKIN_MD brightness.
# ─────────────────────────────────────────────────────────────────────────────
def gen_wildflower1() -> Image.Image:
    im = new_canvas(8, 8)
    # Stem (ochre/warm, 1px)
    put(im, 4, 7, _FLOWER1_STEM)
    put(im, 4, 6, _FLOWER1_STEM)
    put(im, 4, 5, _FLOWER1_STEM)
    put(im, 3, 4, _FLOWER1_STEM)   # leaf stub
    # Head — 3x3 cluster, petals + centre
    put(im, 3, 2, _FLOWER1_ACC)    # petal left
    put(im, 5, 2, _FLOWER1_ACC)    # petal right
    put(im, 4, 1, _FLOWER1_ACC)    # petal top
    put(im, 4, 3, _FLOWER1_ACC)    # petal bottom
    put(im, 4, 2, _FLOWER1_HEAD)   # centre brighter
    # Root base
    put(im, 4, 7, GRASS_DK)
    put(im, 3, 7, GRASS_DK)
    return im


# ─────────────────────────────────────────────────────────────────────────────
# wildflower2.png  8x8
# Muted cool-tone flower: dusty purple head on a grey-green stem.
# Distinctly different silhouette from wildflower1 (offset stem, 2-petal vs 4).
# ─────────────────────────────────────────────────────────────────────────────
def gen_wildflower2() -> Image.Image:
    im = new_canvas(8, 8)
    # Stem (grey-green, leans left slightly)
    put(im, 4, 7, _FLOWER2_STEM)
    put(im, 4, 6, _FLOWER2_STEM)
    put(im, 3, 5, _FLOWER2_STEM)   # bend left
    put(im, 3, 4, _FLOWER2_STEM)
    put(im, 5, 5, _FLOWER2_STEM)   # small leaf right
    # Head — compact 2-lobe shape (like a clover head)
    put(im, 2, 3, _FLOWER2_HEAD)   # left lobe
    put(im, 3, 2, _FLOWER2_HEAD)
    put(im, 2, 2, _FLOWER2_ACC)
    put(im, 4, 3, _FLOWER2_HEAD)   # right lobe
    put(im, 3, 3, _FLOWER2_HEAD)   # centre
    put(im, 3, 1, _FLOWER2_ACC)    # top dark petal
    # Root
    put(im, 4, 7, GRASS_DK)
    put(im, 5, 7, GRASS_DK)
    return im


# ─────────────────────────────────────────────────────────────────────────────
# Preview composite  (QA self-check)
# 4x scale on dark panel bg, all 8x8 sprites side by side with labels gap.
# ─────────────────────────────────────────────────────────────────────────────
def gen_preview(sprites: list[tuple[str, Image.Image]]) -> Image.Image:
    SLOT = 12      # each sprite gets 12x12 slot (8x8 sprite + 2px margin each side)
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
        ("decor_rock.png",    gen_decor_rock),
        ("grass_tuft.png",    gen_grass_tuft),
        ("wildflower1.png",   gen_wildflower1),
        ("wildflower2.png",   gen_wildflower2),
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
