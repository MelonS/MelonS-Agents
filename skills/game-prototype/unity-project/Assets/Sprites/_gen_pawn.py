# -*- coding: utf-8 -*-
"""A2 — flat-style colonist sprite + outline (RimWorld-grounded).

Replaces gen_pawn() in _gen_sprites.py for pawn_colonist.png.

Output:
  pawn_colonist.png   16x16  (unchanged size — 1x1 world unit at PPU 16)

Three tint variants (different cloth colours from palette):
  pawn_blue.png       16x16  CLOTH_BLUE shirt + trouser  (default)
  pawn_rust.png       16x16  CLOTH_RUST shirt + trouser
  pawn_olive.png      16x16  CLOTH_OLIVE shirt + trouser

_preview_colonist.png  4x scale, all four side-by-side on dark bg.

Style rules (from backlog A2):
- Flat colour fills, NO gradients.
- Near-black OUTLINE_STORY as outer silhouette contour (outermost ring
  of the silhouette — at 16x16 scale this reads as a clear 1-pixel
  dark border that "pops" the pawn at game resolution, equivalent to
  RimWorld's 2–3px at their larger canvas size).
- Top-down humanoid: head (SKIN_MD), dark hair on top half of head,
  torso (cloth colour), legs (darker trouser colour), dark boot row.
- Muted colours from palette.py only; no ad-hoc values.

Layout (16x16):
  Row  0–1 : outline ring (top of head)
  Row  2–3 : hair interior
  Row  4–5 : skin face interior
  Row  6   : narrow connector (shoulder) — outline only
  Row  7–10: torso + arms (cloth colour interior, outline rim)
  Row 11   : torso bottom — outline only
  Row 12–14: legs (trouser colour interior, outline rim)
  Row 15   : feet (dark boot)
"""
from __future__ import annotations
from PIL import Image
from pathlib import Path

from palette import (
    OUTLINE_STORY,
    SKIN_MD,
    HAIR_DK,
    CLOTH_BLUE, CLOTH_BLUE_DK,
    CLOTH_RUST, CLOTH_RUST_DK,
    CLOTH_OLIVE, CLOTH_OLIVE_DK,
    TROUSER_BLUE, TROUSER_RUST, TROUSER_OLIVE,
    WOOD_DK,
)

HERE = Path(__file__).resolve().parent

T  = (0, 0, 0, 0)   # transparent
OL = OUTLINE_STORY  # near-black warm outline

# ---------------------------------------------------------------------------
# Zone map — 16x16 grid
# 0 = transparent
# 1 = hair    (HAIR_DK)
# 2 = skin    (SKIN_MD)
# 3 = torso   (cloth)
# 4 = arm     (cloth_dk)
# 5 = leg     (trouser)
# 6 = boot    (WOOD_DK — dark foot)
# ---------------------------------------------------------------------------
#   col: 0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15
ZONE_MAP = [
    # y=0  head top
    [0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0],
    # y=1
    [0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0],
    # y=2  hair interior
    [0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0],
    # y=3
    [0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0],
    # y=4  face
    [0, 0, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 0, 0],
    # y=5
    [0, 0, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 0, 0],
    # y=6  shoulder connector
    [0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0],
    # y=7  torso + wide arms
    [0, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 0],
    # y=8
    [0, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 0],
    # y=9
    [0, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 0],
    # y=10
    [0, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 0],
    # y=11  torso bottom, no arms
    [0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0],
    # y=12  legs
    [0, 0, 0, 5, 5, 5, 0, 0, 0, 0, 5, 5, 5, 0, 0, 0],
    # y=13
    [0, 0, 0, 5, 5, 5, 0, 0, 0, 0, 5, 5, 5, 0, 0, 0],
    # y=14
    [0, 0, 0, 5, 5, 5, 0, 0, 0, 0, 5, 5, 5, 0, 0, 0],
    # y=15  feet (slightly narrower)
    [0, 0, 0, 6, 6, 0, 0, 0, 0, 0, 0, 6, 6, 0, 0, 0],
]

assert len(ZONE_MAP) == 16
for r in ZONE_MAP:
    assert len(r) == 16


def _build_mask() -> set:
    """All (x,y) where zone != 0."""
    return {(x, y) for y, row in enumerate(ZONE_MAP) for x, z in enumerate(row) if z != 0}


def _outline_ring(mask: set) -> set:
    """Outermost opaque ring: opaque pixels with at least one transparent
    4-neighbour.  At 16x16 this is a single-pixel dark border around the
    entire silhouette — reads as a clear outline at game resolution.
    """
    ring = set()
    for (x, y) in mask:
        for dx, dy in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
            if (x + dx, y + dy) not in mask:
                ring.add((x, y))
                break
    return ring


def gen_pawn(cloth: tuple, cloth_dk: tuple, trouser: tuple) -> Image.Image:
    """Generate a 16x16 flat-style colonist with the given cloth colours."""
    im = Image.new("RGBA", (16, 16), T)

    zone_color = {
        1: HAIR_DK,
        2: SKIN_MD,
        3: cloth,
        4: cloth_dk,
        5: trouser,
        6: WOOD_DK,
    }

    mask    = _build_mask()
    outline = _outline_ring(mask)

    # Pass 1: fill all opaque zones with their base colour
    for y, row in enumerate(ZONE_MAP):
        for x, z in enumerate(row):
            if z != 0:
                im.putpixel((x, y), zone_color[z])

    # Pass 2: overwrite the outer ring with near-black outline
    for (x, y) in outline:
        im.putpixel((x, y), OL)

    return im


def gen_preview(sprites: list, scale: int = 4) -> Image.Image:
    """4x scale side-by-side composite on a dark panel background."""
    gap = 4
    n   = len(sprites)
    pw  = n * 16 + (n - 1) * gap
    ph  = 16
    bg  = (42, 31, 24, 255)
    composite = Image.new("RGBA", (pw, ph), bg)
    for i, (_, spr) in enumerate(sprites):
        composite.paste(spr, (i * (16 + gap), 0), spr)
    return composite.resize((pw * scale, ph * scale), Image.NEAREST)


def main():
    variants = [
        ("pawn_colonist", CLOTH_BLUE,  CLOTH_BLUE_DK,  TROUSER_BLUE),
        ("pawn_blue",     CLOTH_BLUE,  CLOTH_BLUE_DK,  TROUSER_BLUE),
        ("pawn_rust",     CLOTH_RUST,  CLOTH_RUST_DK,  TROUSER_RUST),
        ("pawn_olive",    CLOTH_OLIVE, CLOTH_OLIVE_DK, TROUSER_OLIVE),
    ]

    sprites = []
    for slug, cloth, cloth_dk, trouser in variants:
        im   = gen_pawn(cloth, cloth_dk, trouser)
        out  = HERE / f"{slug}.png"
        im.save(out)
        print(f"[gen_pawn] {slug}.png  {im.size[0]}x{im.size[1]}")
        sprites.append((slug, im))

    prev      = gen_preview(sprites, scale=4)
    prev_path = HERE / "_preview_colonist.png"
    prev.save(prev_path)
    print(f"[gen_pawn] _preview_colonist.png  {prev.size[0]}x{prev.size[1]}")


if __name__ == "__main__":
    main()
