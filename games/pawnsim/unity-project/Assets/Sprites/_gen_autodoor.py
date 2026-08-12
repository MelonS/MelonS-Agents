# -*- coding: utf-8 -*-
"""W-M6-04 B5 — autodoor buildable sprite (wiki row B5).

Produces ONE new buildable sprite PNG for the M4 build catalogue:

  autodoor.png   16x16   a plain wooden DOOR panel reskinned with a teal/cyan
                         AUTOMATION light strip down the centre split, so it
                         reads as a POWERED / auto door — visually distinct from
                         the plain wooden door while sharing the same door
                         silhouette.  WOOD palette tones, 1px OUTLINE_OBJ.

GAMEPLAY (RUNTIME, not this sprite): an autodoor IS the same DoorEntity built
with a HIGHER per-instance passMul (≈0.95 vs the plain door's 0.65), so a pawn
crosses an autodoor FASTER than a plain door (B5 acceptance: "pawns cross it
faster than a plain door").  That faster-cross is a property of the prefab's
DoorEntity.passMul field set in BuildManager.EnsureAutodoorPrefab, NOT of this
sprite; this file only authors the object's pixels.

DESIGN RULES (same north-star as the rest of the catalogue — fence/lamp/table/
stone-floor/barricade):
  - WOOD colours STRICTLY from palette.py — no ad-hoc wood values.
  - flat-Kenney language: solid fills, 1px OUTLINE_OBJ on the solid body.
  - the cyan automation strip (TECH_*) is a DELIBERATE saturated tech accent for
    the "powered door" read; it is NOT a base-palette terrain/wood colour, so it
    is defined locally here (and mirrored in the BuildManager fallback) rather
    than added to palette.py (palette.py stays read-only).
  - Transparent background (RGBA), point-filter intent (PPU 16, FilterMode Point).
  - 16x16 = one grid cell, matching wall_wood / door_wood / stove / lamp / table.

The procedural fallback in BuildManager.BuildProceduralAutodoorSprite mirrors
this authored content pixel-for-pixel so a player build (where the PNG is absent
/ not loaded) shows the SAME autodoor — never null/magenta.

NOT EDITED: _gen_sprites.py, _gen_fix_audit.py, _gen_fence.py, _gen_barricade.py,
            _gen_table_chair.py, _gen_lamp.py, _gen_stone_floor.py,
            _gen_scatter_variety.py, palette.py (all read-only).

Lane contract (W-M6-04 B5):
  This file + autodoor.png + BuildManager.cs + DoorEntity.cs (+ ArchitectMenu.cs
  catalogue row) are the files this lane touches.  No SceneSetup, no other
  generator edit.
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

# Cyan automation accent — DELIBERATE saturated tech tone for the powered-door
#  read.  Local to this generator (not a palette.py terrain/wood colour);
#  mirrored exactly in BuildManager.BuildProceduralAutodoorSprite.
TECH_LT = (120, 224, 220, 255)
TECH_DK = (48, 150, 152, 255)

SIZE = 16


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im: Image.Image, x: int, y: int, c: tuple) -> None:
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


# ─────────────────────────────────────────────────────────────────────────────
# autodoor.png   16x16  (PIL origin top-left, y=0 = TOP — matches BuildManager's
#                        top-down authored coords; the C# fallback flips to the
#                        bottom-up texture once.)
#
# Door panel fills cols 2..13, rows 1..14 (1px transparent margin all round):
#   - rows 1 / 14 and cols 2 / 13 → 1px OUTLINE_OBJ frame
#   - cols 2..4   → WOOD_LT left highlight
#   - cols 11..13 → WOOD_DK right shadow
#   - else        → WOOD_MD body
# Centre automation strip cols 7..8, rows 3..12, alternating TECH_LT/TECH_DK so
# it reads as a powered light seam down the door split.
# ─────────────────────────────────────────────────────────────────────────────
def gen_autodoor() -> Image.Image:
    im = new_canvas(SIZE, SIZE)

    for gy in range(1, 15):          # rows 1..14
        for gx in range(2, 14):      # cols 2..13
            if gy == 1 or gy == 14 or gx == 2 or gx == 13:
                c = OUTLINE_OBJ      # 1px OBJ outline frame
            elif gx <= 4:
                c = WOOD_LT          # left highlight
            elif gx >= 11:
                c = WOOD_DK          # right shadow
            else:
                c = WOOD_MD          # body
            put(im, gx, gy, c)

    for gy in range(3, 13):          # rows 3..12
        put(im, 7, gy, TECH_LT if gy % 2 == 0 else TECH_DK)
        put(im, 8, gy, TECH_DK if gy % 2 == 0 else TECH_LT)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# Preview composite (QA self-check) — 8x scale on a grass-ish panel.
# ─────────────────────────────────────────────────────────────────────────────
def gen_preview(spr: Image.Image) -> Image.Image:
    BG = (96, 116, 70, 255)  # GRASS_MD-ish so the door reads in context
    pad = 3
    W = spr.width + pad * 2
    H = spr.height + pad * 2
    canvas = Image.new("RGBA", (W, H), BG)
    canvas.paste(spr, (pad, pad), spr)
    scale = 8
    return canvas.resize((W * scale, H * scale), Image.NEAREST)


def main():
    spr = gen_autodoor()
    spr.save(HERE / "autodoor.png")
    print(f"[gen_autodoor] autodoor.png  {spr.width}x{spr.height}")

    prev = gen_preview(spr)
    prev.save(HERE / "_preview_autodoor.png")
    print(f"[gen_autodoor] _preview_autodoor.png  {prev.width}x{prev.height}  (8x scale QA)")


if __name__ == "__main__":
    main()
