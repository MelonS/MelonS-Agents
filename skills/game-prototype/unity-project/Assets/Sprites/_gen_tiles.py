# -*- coding: utf-8 -*-
"""A3 - Muted cohesive terrain tiles + bonus door_wood fix.

Generates 16x16 RGBA PNGs for the 4 terrain tiles used by
SceneSetup.Game.Terrain.cs via LoadOrCreateTile.  All colours
come exclusively from palette.py (the master palette established
in A1).

Design rules (from design-improvement-backlog.md A3 + north-star):
- Terrain is LOW-saturation -- must recede behind pawns/buildings.
- No outline on terrain tiles (background, not story-relevant objects).
- Subtle per-pixel speckle within each tile's ramp for visual interest
  without creating readable noise that fights the map read.
- Seamless tiling: the generator does NOT paint a hard border;
  speckle is interior-only so repeated tiles blend at edges.
- Grass uses GRASS_DK/MD/LT ramp (muted olive-khaki), NOT neon green.
- Dirt uses DIRT_DK/MD/LT ramp, Water WATER_DK/MD/LT, Rock ROCK_DK/MD/LT.

Bonus (A4 pre-work): door_wood.png remapped to palette.py colours
if the old off-palette colours are detected.

Run:
    cd Assets/Sprites && python _gen_tiles.py
"""

import os
import random
import sys

# Windows cp949 console fix: force UTF-8 output so em-dashes in palette
# comments don't raise UnicodeEncodeError on plain python _gen_tiles.py calls.
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

from PIL import Image

# -- palette import -------------------------------------------------------
# Resolve palette.py relative to this file so the script works whether
# called from the Sprites dir or the repo root.
_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
from palette import (
    GRASS_DK, GRASS_MD, GRASS_LT,
    DIRT_DK,  DIRT_MD,  DIRT_LT,
    ROCK_DK,  ROCK_MD,  ROCK_LT,
    WATER_DK, WATER_MD, WATER_LT,
    WOOD_DK,  WOOD_MD,  WOOD_LT,
    OUTLINE_OBJ,
)

# -- output paths ---------------------------------------------------------
OUT_DIR = _HERE  # same folder as the generators


def _save(img, name):
    path = os.path.join(OUT_DIR, name)
    img.save(path)
    print("  saved {}  ({}x{})".format(path, img.size[0], img.size[1]))


# -- speckle helpers ------------------------------------------------------
# Deterministic seed per tile: reproducible output; preview looks right
# on every run without relying on random state from earlier generators.

def _speckle_tile(size, base, dark, light,
                  dark_prob=0.07, light_prob=0.05, seed=0):
    """
    Paint a size x size tile with `base` colour, then scatter a few
    `dark` and `light` pixels for subtle texture variation.

    Edges (outermost pixel ring) are kept strictly `base` so that
    when tiles repeat the boundary is invisible (seamless tiling).
    """
    rng = random.Random(seed)
    img = Image.new("RGBA", (size, size), base)
    pixels = img.load()
    inner = range(1, size - 1)  # skip the border row/col
    for y in inner:
        for x in inner:
            r = rng.random()
            if r < dark_prob:
                pixels[x, y] = dark
            elif r < dark_prob + light_prob:
                pixels[x, y] = light
    return img


def _water_tile(size, seed=0):
    """
    2-tone water: horizontal bands of WATER_MD/WATER_DK with a very
    sparse WATER_LT highlight for depth.  Calm, not bright.
    Interior speckle only (edges stay WATER_MD for seamless tiling).
    """
    rng = random.Random(seed)
    img = Image.new("RGBA", (size, size), WATER_MD)
    pixels = img.load()
    for y in range(size):
        band_dark = (y // 4) % 2 == 0
        for x in range(size):
            if y == 0 or y == size - 1 or x == 0 or x == size - 1:
                pixels[x, y] = WATER_MD  # edge = base for seamless tiling
            else:
                base_px = WATER_DK if band_dark else WATER_MD
                r = rng.random()
                if r < 0.04:
                    pixels[x, y] = WATER_LT  # rare highlight
                else:
                    pixels[x, y] = base_px
    return img


# -- tile generators ------------------------------------------------------

def gen_grass(size=16):
    """Muted olive-khaki grass - desaturated so pawns pop."""
    return _speckle_tile(
        size, GRASS_MD, GRASS_DK, GRASS_LT,
        dark_prob=0.08, light_prob=0.06, seed=1001,
    )


def gen_dirt(size=16):
    """Warm muted brown dirt / tilled soil patches."""
    return _speckle_tile(
        size, DIRT_MD, DIRT_DK, DIRT_LT,
        dark_prob=0.09, light_prob=0.05, seed=2002,
    )


def gen_water(size=16):
    """Desaturated blue-grey water, calm depth-band pattern."""
    return _water_tile(size, seed=3003)


def gen_rock(size=16):
    """Cool grey-blue rock - subtle texture, recedes from pawns."""
    return _speckle_tile(
        size, ROCK_MD, ROCK_DK, ROCK_LT,
        dark_prob=0.10, light_prob=0.06, seed=4004,
    )


# -- bonus: door_wood colour remap ----------------------------------------
# The existing door_wood.png uses off-palette colours:
#   (192, 203, 220) - grey-blue window pane  -> remap to ROCK_LT
#   (139, 155, 180) - mid window pane        -> remap to ROCK_MD
#   (234, 165, 108) - light corner highlight -> remap to WOOD_LT
#   (189, 108,  74) - main wood body         -> remap to WOOD_MD
#   (118,  59,  54) - dark frame strip       -> remap to WOOD_DK
#   ( 63,  38,  49) - old brown-dark outline -> remap to OUTLINE_OBJ
# This brings door_wood into the unified palette family (A4 pre-work).

_DOOR_REMAP = {
    (192, 203, 220, 255): ROCK_LT,      # window pane bright
    (139, 155, 180, 255): ROCK_MD,      # window pane mid
    (234, 165, 108, 255): WOOD_LT,      # corner highlight
    (189, 108,  74, 255): WOOD_MD,      # main wood body
    (118,  59,  54, 255): WOOD_DK,      # dark frame strip
    ( 63,  38,  49, 255): OUTLINE_OBJ,  # outline
}


def remap_door_wood():
    """
    Recolour door_wood.png from the old ad-hoc palette to the master
    palette.  Returns True if the file was updated, False if skipped.
    """
    path = os.path.join(OUT_DIR, "door_wood.png")
    if not os.path.exists(path):
        print("  door_wood.png not found - skipping bonus remap")
        return False

    img = Image.open(path).convert("RGBA")
    pixels = img.load()
    w, h = img.size
    changed = 0
    for y in range(h):
        for x in range(w):
            px = pixels[x, y]
            if px in _DOOR_REMAP:
                pixels[x, y] = _DOOR_REMAP[px]
                changed += 1
    if changed == 0:
        print("  door_wood.png: no off-palette colours found - already clean")
        return False
    img.save(path)
    print("  door_wood.png: remapped {} pixels to master palette  saved {}".format(
        changed, path))
    return True


# -- preview composite ----------------------------------------------------

def gen_preview(tile_size=16):
    """
    Composite preview:
      Row 0: single tile of each (grass / dirt / water / rock)
      Row 1-3: 3x3 grass repeat to verify seamless tiling
    Scaled up 4x (nearest-neighbour) for readability.
    """
    scale = 4

    grass = gen_grass(tile_size)
    dirt  = gen_dirt(tile_size)
    water = gen_water(tile_size)
    rock  = gen_rock(tile_size)
    tiles_row = [grass, dirt, water, rock]

    col_count = 4
    canvas_w = tile_size * col_count
    canvas_h = tile_size * 4  # 1 row singles + 3 rows repeat
    canvas = Image.new("RGBA", (canvas_w, canvas_h), (30, 25, 20, 255))

    for i, t in enumerate(tiles_row):
        canvas.paste(t, (i * tile_size, 0))

    for gy in range(3):
        for gx in range(3):
            canvas.paste(grass, (gx * tile_size, (gy + 1) * tile_size))

    big = canvas.resize((canvas_w * scale, canvas_h * scale), Image.NEAREST)
    preview_path = os.path.join(OUT_DIR, "_preview_tiles.png")
    big.save(preview_path)
    print("  preview saved {}  ({}x{})".format(preview_path, big.size[0], big.size[1]))


# -- main -----------------------------------------------------------------

def main():
    print("=== _gen_tiles.py - A3 terrain tile regen ===")
    size = 16  # must match existing tile size exactly

    print("\n[1/4] Grass tile")
    _save(gen_grass(size), "tile_grass.png")

    print("[2/4] Dirt tile")
    _save(gen_dirt(size), "tile_dirt.png")

    print("[3/4] Water tile")
    _save(gen_water(size), "tile_water.png")

    print("[4/4] Rock tile")
    _save(gen_rock(size), "tile_rock.png")

    print("\n[Bonus] Door wood remap")
    remap_door_wood()

    print("\n[Preview] Composite")
    gen_preview(size)

    print("\nDone.")
    print("Tiles are 16x16 RGBA - same dimensions as before.")
    print("LoadOrCreateTile + ForceImportAllSprites in SceneSetup will")
    print("auto-reimport these when scene-regen runs (no .meta changes needed).")


if __name__ == "__main__":
    main()
