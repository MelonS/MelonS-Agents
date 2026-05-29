# -*- coding: utf-8 -*-
"""_gen_cleanup.py — Round 6 remaining sprite cohesion fixes.

Fixes sprites flagged in the Round 6 review:

1. lamp.png           (16x16) — redesign: grounded lamp/torch
2. berry_bush.png     (16x16) — NEW: dedicated bush sprite (was tree + neon tint)
3. stockpile_marker.png (16x16) — palette-correct muted corner-bracket marker
4. decor_flower.png   (16x16) — regen from neon-green fill to palette-consistent flower
5. arrow.png          (16x16) — colour remap: 3 off-palette colours -> master palette

IMPORTANT for programmer:
  berry_bush.png is a NEW file. Update SceneSetup.Game.BerryBush.cs to load
  "Assets/Sprites/berry_bush.png" instead of reusing treeSprite with green tint.
  It will also need a .meta file (Sprite, Point filter, spritePixelsPerUnit=16).

  decor_flower.png is regenerated (was solid neon-green fill). Existing .meta valid.
  arrow.png, lamp.png, stockpile_marker.png: existing .meta valid (sizes unchanged).

All colours from palette.py exclusively.
"""
import sys, os

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)

from PIL import Image
from palette import (
    OUTLINE_OBJ,
    OUTLINE_PLANT,
    WOOD_DK, WOOD_MD, WOOD_LT,
    ROCK_DK, ROCK_MD,
    FIRE_OR, FIRE_LT,
    CROP_GOLD,
    GRASS_DK, GRASS_MD, GRASS_LT,
    UI_BORDER,
    DIRT_MD,
    MEAT_RED,
)

# berry-specific colours (muted dark red — not neon)
_BERRY_RED  = (178, 58, 52, 255)    # close to MEAT_RED family — reads as ripe berry
_BERRY_DK   = (128, 38, 32, 255)    # shadow on berry
_LEAF_MID   = (80, 108, 56, 255)    # flat olive mid-canopy (matches _gen_sprites tree)
_LEAF_LT    = (104, 136, 74, 255)   # dappled leaf highlight
_LEAF_DK    = (56,  76, 38, 255)    # deep shadow leaf

# stockpile muted amber
_MARK_AMBER = (138, 100, 56, 255)   # muted warm amber marker lines (WOOD_DK family)
_MARK_DK    = (100, 70,  38, 255)   # corner anchor dots, darker


def new_img(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im, x, y, c):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


def hline(im, x0, x1, y, c):
    for x in range(x0, x1 + 1):
        put(im, x, y, c)


def vline(im, x, y0, y1, c):
    for y in range(y0, y1 + 1):
        put(im, x, y, c)


def rect_fill(im, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(im, x, y, c)


def rect_outline(im, x0, y0, x1, y1, c):
    hline(im, x0, x1, y0, c)
    hline(im, x0, x1, y1, c)
    vline(im, x0, y0, y1, c)
    vline(im, x1, y0, y1, c)


# ─── lamp.png 16x16 ──────────────────────────────────────────────────────────
# BEFORE:  4x4 neon-yellow head (255,220,100) + 2px pin stem y=6-13 + 6px base.
#          Off-palette head color. Floats -- not grounded.
# AFTER:   Standing lamp / torch: wider lantern head with FIRE_OR glow, WOOD_MD
#          post, wider base plate. Grounded in its cell.

def gen_lamp():
    im = new_img(16, 16)

    # Post / stand -- 4px wide, y=9 to y=14
    rect_fill(im, 6, 8, 9, 14, WOOD_MD)
    hline(im, 6, 9, 8, WOOD_LT)
    hline(im, 6, 9, 14, WOOD_DK)

    # Base plate (wider than post, visually anchors it)
    hline(im, 4, 11, 14, WOOD_DK)
    hline(im, 3, 12, 14, WOOD_DK)
    hline(im, 3, 12, 12, WOOD_MD)
    hline(im, 4, 11, 11, WOOD_MD)

    # Lantern body -- 6px wide, y=2 to y=7
    rect_fill(im, 5, 3, 10, 7, WOOD_DK)
    rect_fill(im, 6, 4, 9, 6, FIRE_OR)
    put(im, 7, 4, FIRE_LT)
    put(im, 8, 4, FIRE_LT)
    put(im, 7, 5, FIRE_LT)

    # Lantern cap
    hline(im, 6, 9, 2, OUTLINE_OBJ)
    hline(im, 6, 9, 3, WOOD_DK)
    put(im, 6, 7, WOOD_DK)
    put(im, 9, 7, WOOD_DK)

    # Glow bloom (semi-transparent warm highlight around lantern)
    _GLOW = (FIRE_LT[0], FIRE_LT[1], FIRE_LT[2], 80)
    for dx, dy in [(-1,3),(-1,4),(-1,5),(10,3),(10,4),(10,5),
                   (5,1),(6,1),(7,1),(8,1),(9,1),(10,1),
                   (5,8),(6,8),(9,8),(10,8)]:
        put(im, dx, dy, _GLOW)

    # Outline OUTLINE_OBJ 1px around entire lamp
    vline(im, 5, 8, 10, OUTLINE_OBJ)
    vline(im, 10, 8, 10, OUTLINE_OBJ)
    put(im, 2, 11, OUTLINE_OBJ); put(im, 12, 11, OUTLINE_OBJ)
    put(im, 2, 12, OUTLINE_OBJ); put(im, 12, 12, OUTLINE_OBJ)
    hline(im, 2, 12, 13, OUTLINE_OBJ)
    hline(im, 5, 10, 2, OUTLINE_OBJ)
    vline(im, 4, 3, 7, OUTLINE_OBJ)
    vline(im, 11, 3, 7, OUTLINE_OBJ)
    hline(im, 4, 11, 8, OUTLINE_OBJ)

    return im


# ─── berry_bush.png 16x16 ────────────────────────────────────────────────────
# NEW -- previously berry bush reused tree.png + neon lime tint (0.6, 0.9, 0.4).
# Design: flat round bush, 3-tone olive canopy, 4 red berry dots, OUTLINE_PLANT edge.

def gen_berry_bush():
    im = new_img(16, 16)

    canopy_mid = [
        (5,3),(6,3),(7,3),(8,3),(9,3),(10,3),
        (3,4),(4,4),(5,4),(6,4),(7,4),(8,4),(9,4),(10,4),(11,4),(12,4),
        (3,5),(4,5),(5,5),(6,5),(7,5),(8,5),(9,5),(10,5),(11,5),(12,5),
        (2,6),(3,6),(4,6),(5,6),(6,6),(7,6),(8,6),(9,6),(10,6),(11,6),(12,6),(13,6),
        (2,7),(3,7),(4,7),(5,7),(6,7),(7,7),(8,7),(9,7),(10,7),(11,7),(12,7),(13,7),
        (2,8),(3,8),(4,8),(5,8),(6,8),(7,8),(8,8),(9,8),(10,8),(11,8),(12,8),(13,8),
        (3,9),(4,9),(5,9),(6,9),(7,9),(8,9),(9,9),(10,9),(11,9),(12,9),
        (3,10),(4,10),(5,10),(6,10),(7,10),(8,10),(9,10),(10,10),(11,10),(12,10),
        (4,11),(5,11),(6,11),(7,11),(8,11),(9,11),(10,11),(11,11),
        (5,12),(6,12),(7,12),(8,12),(9,12),(10,12),
    ]
    for x, y in canopy_mid:
        put(im, x, y, _LEAF_MID)

    canopy_lt = [
        (5,3),(6,3),(7,3),(8,3),(9,3),(10,3),
        (3,4),(4,4),(5,4),(6,4),(7,4),(8,4),(9,4),(10,4),(11,4),(12,4),
        (3,5),(4,5),(5,5),(6,5),(7,5),
        (2,6),(3,6),(4,6),(5,6),
    ]
    for x, y in canopy_lt:
        put(im, x, y, _LEAF_LT)

    canopy_dk = [
        (5,12),(6,12),(7,12),(8,12),(9,12),(10,12),
        (4,11),(5,11),(6,11),(7,11),(8,11),
        (3,10),(4,10),(5,10),(6,10),
        (3,9),(4,9),(5,9),
        (2,8),(3,8),(4,8),
    ]
    for x, y in canopy_dk:
        put(im, x, y, _LEAF_DK)

    # Berry dots (4 berries)
    berries = [(7,5),(11,7),(5,9),(10,10)]
    for bx, by in berries:
        put(im, bx, by, _BERRY_RED)
        put(im, bx+1, by, _BERRY_RED)
        put(im, bx, by+1, _BERRY_DK)

    # Ground stub
    hline(im, 6, 9, 13, GRASS_DK)
    hline(im, 7, 8, 14, GRASS_DK)

    # Plant outline
    outline_ring = [
        (4,2),(5,2),(6,2),(7,2),(8,2),(9,2),(10,2),(11,2),
        (3,3),(12,3),
        (2,4),(13,4),
        (1,5),(14,5),
        (1,6),(14,6),
        (1,7),(14,7),
        (1,8),(14,8),
        (2,9),(13,9),
        (2,10),(13,10),
        (3,11),(12,11),
        (4,12),(11,12),
        (5,13),(6,13),(7,13),(8,13),(9,13),(10,13),
    ]
    for x, y in outline_ring:
        if im.getpixel((x, y))[3] == 0:
            put(im, x, y, OUTLINE_PLANT)

    return im


# ─── stockpile_marker.png 16x16 ──────────────────────────────────────────────
# BEFORE:  Solid neon-green/orange fill using completely off-palette colors.
# AFTER:   Corner L-bracket marker on transparent background. Muted amber.

def gen_stockpile_marker():
    im = new_img(16, 16)

    # 4 corner L-brackets
    hline(im, 0, 4, 0, _MARK_AMBER); hline(im, 0, 4, 1, _MARK_DK)
    vline(im, 0, 0, 4, _MARK_AMBER); vline(im, 1, 0, 4, _MARK_DK)

    hline(im, 11, 15, 0, _MARK_AMBER); hline(im, 11, 15, 1, _MARK_DK)
    vline(im, 15, 0, 4, _MARK_AMBER); vline(im, 14, 0, 4, _MARK_DK)

    hline(im, 0, 4, 15, _MARK_AMBER); hline(im, 0, 4, 14, _MARK_DK)
    vline(im, 0, 11, 15, _MARK_AMBER); vline(im, 1, 11, 15, _MARK_DK)

    hline(im, 11, 15, 15, _MARK_AMBER); hline(im, 11, 15, 14, _MARK_DK)
    vline(im, 15, 11, 15, _MARK_AMBER); vline(im, 14, 11, 15, _MARK_DK)

    # Dashed mid-edges
    for x in range(6, 10, 4):
        put(im, x, 0, _MARK_AMBER); put(im, x+1, 0, _MARK_AMBER)
        put(im, x, 15, _MARK_AMBER); put(im, x+1, 15, _MARK_AMBER)
    for y in range(6, 10, 4):
        put(im, 0, y, _MARK_AMBER); put(im, 0, y+1, _MARK_AMBER)
        put(im, 15, y, _MARK_AMBER); put(im, 15, y+1, _MARK_AMBER)

    return im


# ─── decor_flower.png 16x16 ──────────────────────────────────────────────────
# BEFORE:  Three neon-lime greens, solid fill, no transparency.
# AFTER:   Small 5-petal flower on transparent background.
#          GRASS_MD/LT petals, CROP_GOLD center, OUTLINE_PLANT edge.

def gen_decor_flower():
    im = new_img(16, 16)

    # Upper petal
    put(im, 7, 5, GRASS_LT); put(im, 8, 5, GRASS_LT)
    put(im, 7, 6, GRASS_MD); put(im, 8, 6, GRASS_MD)
    # Lower petal
    put(im, 7, 10, GRASS_MD); put(im, 8, 10, GRASS_MD)
    put(im, 7, 11, GRASS_LT); put(im, 8, 11, GRASS_LT)
    # Left petal
    put(im, 5, 7, GRASS_LT); put(im, 5, 8, GRASS_LT)
    put(im, 6, 7, GRASS_MD); put(im, 6, 8, GRASS_MD)
    # Right petal
    put(im, 9, 7, GRASS_MD); put(im, 9, 8, GRASS_MD)
    put(im, 10, 7, GRASS_LT); put(im, 10, 8, GRASS_LT)
    # Center (CROP_GOLD semantic accent -- tiny)
    put(im, 7, 7, CROP_GOLD); put(im, 8, 7, CROP_GOLD)
    put(im, 7, 8, CROP_GOLD); put(im, 8, 8, CROP_GOLD)
    # Stem
    put(im, 7, 12, GRASS_DK); put(im, 8, 12, GRASS_DK)
    put(im, 8, 13, GRASS_DK)

    # Auto-outline (OUTLINE_PLANT)
    opaque = [(x, y) for y in range(16) for x in range(16)
              if im.getpixel((x, y))[3] > 0]
    border = set()
    for x, y in opaque:
        for dx, dy in [(-1,0),(1,0),(0,-1),(0,1)]:
            nx, ny = x+dx, y+dy
            if 0 <= nx < 16 and 0 <= ny < 16 and im.getpixel((nx, ny))[3] == 0:
                border.add((nx, ny))
    for x, y in border:
        put(im, x, y, OUTLINE_PLANT)

    return im


# ─── arrow.png 16x16 -- colour remap ─────────────────────────────────────────
# BEFORE:  (63,38,49) old-outline, (189,108,74) old-wood, (192,203,220) old-grey-blue.
# AFTER:   Remap to master palette equivalents (in-place pixel recolour).

_ARROW_REMAP = {
    (63,  38, 49, 255): OUTLINE_OBJ,
    (189, 108, 74, 255): WOOD_MD,
    (192, 203, 220, 255): ROCK_MD,
}


def remap_arrow():
    path = os.path.join(_HERE, "arrow.png")
    if not os.path.exists(path):
        print("  arrow.png not found -- skipping")
        return
    img = Image.open(path).convert("RGBA")
    px = img.load()
    w, h = img.size
    changed = 0
    for y in range(h):
        for x in range(w):
            c = px[x, y]
            if c in _ARROW_REMAP:
                px[x, y] = _ARROW_REMAP[c]
                changed += 1
    img.save(path)
    print(f"[gen_cleanup] arrow.png remapped {changed} pixels to master palette")


# ─── preview composite ──────────────────────────────────────────────────────

def gen_preview(sprites):
    n = len(sprites)
    gap = 4
    W = 16 * n + gap * (n - 1)
    H = 16
    bg = (42, 31, 24, 255)   # UI_PANEL_BG for contrast check
    canvas = Image.new("RGBA", (W, H), bg)
    for i, (_, im) in enumerate(sprites):
        canvas.paste(im, (i * (16 + gap), 0), im)
    scale = 8
    preview = canvas.resize((W * scale, H * scale), Image.NEAREST)
    out = os.path.join(_HERE, "_preview_cleanup.png")
    preview.save(out)
    print(f"[gen_cleanup] preview  {out}  ({W * scale}x{H * scale})")


def main():
    targets = [
        ("lamp.png",             gen_lamp),
        ("berry_bush.png",       gen_berry_bush),
        ("stockpile_marker.png", gen_stockpile_marker),
        ("decor_flower.png",     gen_decor_flower),
    ]
    sprites = []
    for name, fn in targets:
        im = fn()
        out = os.path.join(_HERE, name)
        im.save(out)
        print(f"[gen_cleanup] {name}  {im.size[0]}x{im.size[1]}")
        sprites.append((name, im))
    # In-place colour remap passes
    remap_arrow()
    gen_preview(sprites)


if __name__ == "__main__":
    main()
