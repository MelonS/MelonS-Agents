# -*- coding: utf-8 -*-
"""_gen_icons.py — 4 resource icons for the top-bar UI (U4 completion).

Generates 24x24 RGBA PNGs for the four ResIcon slots in TopBar:
  icon_stone.png  ResIcon_stone  — grey rock chunk (ROCK palette)
  icon_wood.png   ResIcon_wood   — wood log cross-section (WOOD palette)
  icon_meal.png   ResIcon_meal   — cooked meal on plate (warm tones)
  icon_food.png   ResIcon_food   — raw food / grain head (CROP_GOLD + plant)

Design rules:
- These are UI icons rendered at 24x24 screen pixels — NOT world sprites.
  They sit inside flat Image components in the top bar; no PPU concern.
- Style: flat color + 1px OUTLINE_OBJ (items/UI tier, same as world items).
- Palette: exclusively from palette.py. No ad-hoc colors.
- Silhouette must read instantly at 24px: bold, simple shape, strong value
  contrast (light fill against dark outline, not pastel-on-pastel).
- Icons are UI elements so they sit on the warm dark panel background
  (UI_PANEL_HDR ~rgb 73,50,35). Ensure each icon reads against that bg.

Wiring note (programmer):
  Four new PNGs → need .meta files (Sprite, Point filter, spritePixelsPerUnit=16).
  Each ResIcon_<key> Image in TopBar currently has alpha=0 and sprite=null.
  Programmer must:
    1. ForceImport these 4 PNGs (or let SceneSetup.ForceImportAllAssets handle it).
    2. Assign each sprite to its Image.sprite.
    3. Set Image.color = Color.white (alpha 1).
  Slot names in TopBar hierarchy:
    ResIcon_stone, ResIcon_wood, ResIcon_meal, ResIcon_food
"""
import sys, os

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)

from PIL import Image
from palette import (
    OUTLINE_OBJ,
    ROCK_DK, ROCK_MD, ROCK_LT,
    WOOD_DK, WOOD_MD, WOOD_LT,
    CROP_GOLD,
    GRASS_DK,
    FIRE_OR, FIRE_LT,
    OUTLINE_PLANT,
    DIRT_MD,
)

# ── helpers ─────────────────────────────────────────────────────────────────

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


# ── icon_stone.png ───────────────────────────────────────────────────────────
# A chunky rock silhouette: irregular hexagon shape, 3-tone ROCK ramp.
# Reads immediately as "stone / mineral" — solid grey mass + lighter top face.

def gen_icon_stone():
    im = new_img(24, 24)
    # Rock body — irregular chunk shape (wider in middle, flat top-left, angled corners)
    body = [
        # y=5: top edge of chunk (slightly indented)
        (8,5),(9,5),(10,5),(11,5),(12,5),(13,5),(14,5),(15,5),
        # y=6
        (6,6),(7,6),(8,6),(9,6),(10,6),(11,6),(12,6),(13,6),(14,6),(15,6),(16,6),
        # y=7
        (5,7),(6,7),(7,7),(8,7),(9,7),(10,7),(11,7),(12,7),(13,7),(14,7),(15,7),(16,7),(17,7),
        # y=8
        (4,8),(5,8),(6,8),(7,8),(8,8),(9,8),(10,8),(11,8),(12,8),(13,8),(14,8),(15,8),(16,8),(17,8),(18,8),
        # y=9
        (4,9),(5,9),(6,9),(7,9),(8,9),(9,9),(10,9),(11,9),(12,9),(13,9),(14,9),(15,9),(16,9),(17,9),(18,9),
        # y=10
        (4,10),(5,10),(6,10),(7,10),(8,10),(9,10),(10,10),(11,10),(12,10),(13,10),(14,10),(15,10),(16,10),(17,10),(18,10),
        # y=11
        (4,11),(5,11),(6,11),(7,11),(8,11),(9,11),(10,11),(11,11),(12,11),(13,11),(14,11),(15,11),(16,11),(17,11),(18,11),
        # y=12
        (5,12),(6,12),(7,12),(8,12),(9,12),(10,12),(11,12),(12,12),(13,12),(14,12),(15,12),(16,12),(17,12),(18,12),
        # y=13
        (5,13),(6,13),(7,13),(8,13),(9,13),(10,13),(11,13),(12,13),(13,13),(14,13),(15,13),(16,13),(17,13),
        # y=14
        (6,14),(7,14),(8,14),(9,14),(10,14),(11,14),(12,14),(13,14),(14,14),(15,14),(16,14),
        # y=15
        (7,15),(8,15),(9,15),(10,15),(11,15),(12,15),(13,15),(14,15),(15,15),
        # y=16
        (8,16),(9,16),(10,16),(11,16),(12,16),(13,16),(14,16),
    ]
    for x, y in body:
        put(im, x, y, ROCK_MD)

    # Top-face highlight band (upper ~1/3 of body) — lighter tone
    highlight = [
        (8,5),(9,5),(10,5),(11,5),(12,5),(13,5),(14,5),(15,5),
        (6,6),(7,6),(8,6),(9,6),(10,6),(11,6),(12,6),(13,6),(14,6),(15,6),(16,6),
        (5,7),(6,7),(7,7),(8,7),(9,7),(10,7),(11,7),(12,7),(13,7),(14,7),(15,7),(16,7),(17,7),
        (4,8),(5,8),(6,8),(7,8),(8,8),(9,8),(10,8),(11,8),(12,8),
    ]
    for x, y in highlight:
        put(im, x, y, ROCK_LT)

    # Shadow band (lower portion)
    shadow = [
        (5,13),(6,13),(7,13),(8,13),(9,13),
        (6,14),(7,14),(8,14),(9,14),
        (8,15),(9,15),(10,15),
        (8,16),(9,16),(10,16),(11,16),(12,16),(13,16),(14,16),
        (5,12),(6,12),(7,12),(8,12),
    ]
    for x, y in shadow:
        put(im, x, y, ROCK_DK)

    # Outline — 1px OUTLINE_OBJ around the silhouette edge
    outline = [
        # top edge
        (7,4),(8,4),(9,4),(10,4),(11,4),(12,4),(13,4),(14,4),(15,4),(16,4),
        # upper-left slope
        (5,5),(6,5),
        (4,6),(5,6),
        (3,7),(4,7),
        # left edge
        (3,8),(3,9),(3,10),(3,11),
        (4,12),(4,13),
        (5,14),
        (6,15),
        (7,16),
        # bottom edge
        (8,17),(9,17),(10,17),(11,17),(12,17),(13,17),(14,17),
        # right-bottom slope
        (15,16),
        (16,15),(17,15),
        (17,14),(18,14),
        # right edge
        (19,8),(19,9),(19,10),(19,11),(19,12),(19,13),
        (18,7),(19,7),
        (17,6),(18,6),
        (16,5),(17,5),
    ]
    for x, y in outline:
        put(im, x, y, OUTLINE_OBJ)

    return im


# ── icon_wood.png ────────────────────────────────────────────────────────────
# A log cross-section (top-down view): circular ring with annual ring lines.
# Warm brown WOOD ramp — instantly reads as "wood / lumber."

def gen_icon_wood():
    im = new_img(24, 24)
    # Log circle — roughly 14px diameter, centered at (12, 12)
    # Fill with WOOD_MD (main wood body)
    log_body = [
        (8,5),(9,5),(10,5),(11,5),(12,5),(13,5),(14,5),(15,5),
        (6,6),(7,6),(8,6),(9,6),(10,6),(11,6),(12,6),(13,6),(14,6),(15,6),(16,6),(17,6),
        (5,7),(6,7),(7,7),(8,7),(9,7),(10,7),(11,7),(12,7),(13,7),(14,7),(15,7),(16,7),(17,7),(18,7),
        (5,8),(6,8),(7,8),(8,8),(9,8),(10,8),(11,8),(12,8),(13,8),(14,8),(15,8),(16,8),(17,8),(18,8),
        (4,9),(5,9),(6,9),(7,9),(8,9),(9,9),(10,9),(11,9),(12,9),(13,9),(14,9),(15,9),(16,9),(17,9),(18,9),(19,9),
        (4,10),(5,10),(6,10),(7,10),(8,10),(9,10),(10,10),(11,10),(12,10),(13,10),(14,10),(15,10),(16,10),(17,10),(18,10),(19,10),
        (4,11),(5,11),(6,11),(7,11),(8,11),(9,11),(10,11),(11,11),(12,11),(13,11),(14,11),(15,11),(16,11),(17,11),(18,11),(19,11),
        (4,12),(5,12),(6,12),(7,12),(8,12),(9,12),(10,12),(11,12),(12,12),(13,12),(14,12),(15,12),(16,12),(17,12),(18,12),(19,12),
        (4,13),(5,13),(6,13),(7,13),(8,13),(9,13),(10,13),(11,13),(12,13),(13,13),(14,13),(15,13),(16,13),(17,13),(18,13),(19,13),
        (4,14),(5,14),(6,14),(7,14),(8,14),(9,14),(10,14),(11,14),(12,14),(13,14),(14,14),(15,14),(16,14),(17,14),(18,14),(19,14),
        (5,15),(6,15),(7,15),(8,15),(9,15),(10,15),(11,15),(12,15),(13,15),(14,15),(15,15),(16,15),(17,15),(18,15),
        (5,16),(6,16),(7,16),(8,16),(9,16),(10,16),(11,16),(12,16),(13,16),(14,16),(15,16),(16,16),(17,16),(18,16),
        (6,17),(7,17),(8,17),(9,17),(10,17),(11,17),(12,17),(13,17),(14,17),(15,17),(16,17),(17,17),
        (8,18),(9,18),(10,18),(11,18),(12,18),(13,18),(14,18),(15,18),
    ]
    for x, y in log_body:
        put(im, x, y, WOOD_MD)

    # Top-face highlight (upper arc of the circle)
    top_highlight = [
        (8,5),(9,5),(10,5),(11,5),(12,5),(13,5),(14,5),(15,5),
        (6,6),(7,6),(8,6),(9,6),(10,6),(11,6),(12,6),(13,6),(14,6),(15,6),(16,6),(17,6),
        (5,7),(6,7),(7,7),(8,7),(9,7),(10,7),(11,7),(12,7),
        (4,9),(5,9),(6,9),(7,9),(8,9),
        (4,10),(5,10),(6,10),(7,10),
        (4,11),(5,11),(6,11),
    ]
    for x, y in top_highlight:
        put(im, x, y, WOOD_LT)

    # Bottom shadow (lower arc)
    bottom_shadow = [
        (15,15),(16,15),(17,15),(18,15),
        (14,16),(15,16),(16,16),(17,16),(18,16),
        (13,17),(14,17),(15,17),(16,17),(17,17),
        (12,18),(13,18),(14,18),(15,18),
    ]
    for x, y in bottom_shadow:
        put(im, x, y, WOOD_DK)

    # Annual ring — darker ring inside to show it's a log cross-section
    ring_inner = [
        # inner ring circle ~ radius 4 centered at (11,11)
        (9,8),(10,8),(11,8),(12,8),(13,8),
        (8,9),(14,9),
        (8,10),(14,10),
        (8,11),(14,11),
        (8,12),(14,12),
        (8,13),(14,13),
        (9,14),(10,14),(11,14),(12,14),(13,14),
    ]
    for x, y in ring_inner:
        put(im, x, y, WOOD_DK)

    # Pith (center dot)
    put(im, 11, 11, WOOD_DK)
    put(im, 12, 11, WOOD_DK)
    put(im, 11, 12, WOOD_DK)
    put(im, 12, 12, WOOD_DK)

    # Outer silhouette outline
    outline = [
        (7,4),(8,4),(9,4),(10,4),(11,4),(12,4),(13,4),(14,4),(15,4),(16,4),
        (5,5),(6,5),(17,5),(18,5),
        (4,6),(5,6),(18,6),(19,6),
        (3,7),(4,7),(19,7),(20,7),
        (3,8),(20,8),
        (3,9),(20,9),
        (3,10),(20,10),
        (3,11),(20,11),
        (3,12),(20,12),
        (3,13),(20,13),
        (3,14),(20,14),
        (4,15),(19,15),
        (4,16),(5,16),(18,16),(19,16),
        (5,17),(6,17),(17,17),(18,17),
        (7,18),(8,18),(9,18),(10,18),(11,18),(12,18),(13,18),(14,18),(15,18),(16,18),
        (9,19),(10,19),(11,19),(12,19),(13,19),(14,19),
    ]
    for x, y in outline:
        put(im, x, y, OUTLINE_OBJ)

    return im


# ── icon_meal.png ────────────────────────────────────────────────────────────
# A cooked meal: round plate (warm cream) + food mound (brown/amber warm tones).
# Warm palette — the "prepared food" semantic. Plate rim + food center.

def gen_icon_meal():
    im = new_img(24, 24)

    # Plate rim circle (cream/warm white)
    PLATE     = (230, 210, 180, 255)   # warm cream plate  — in DIRT_LT family
    PLATE_SH  = (190, 170, 140, 255)   # plate shadow rim
    FOOD_BASE = (172, 110, 60, 255)    # cooked stew brown — warm, close to WOOD_MD family
    FOOD_LT   = (210, 150, 80, 255)    # highlight on food mound
    FOOD_ACC  = CROP_GOLD              # golden highlight accent (a carrot/grain)
    STEAM     = (200, 185, 165, 120)   # faint steam wisps (semi-transparent)

    # Plate fill (full circle ~16px dia)
    plate_pixels = [
        (8,6),(9,6),(10,6),(11,6),(12,6),(13,6),(14,6),(15,6),
        (6,7),(7,7),(8,7),(9,7),(10,7),(11,7),(12,7),(13,7),(14,7),(15,7),(16,7),(17,7),
        (5,8),(6,8),(7,8),(8,8),(9,8),(10,8),(11,8),(12,8),(13,8),(14,8),(15,8),(16,8),(17,8),(18,8),
        (5,9),(6,9),(7,9),(8,9),(9,9),(10,9),(11,9),(12,9),(13,9),(14,9),(15,9),(16,9),(17,9),(18,9),
        (4,10),(5,10),(6,10),(7,10),(8,10),(9,10),(10,10),(11,10),(12,10),(13,10),(14,10),(15,10),(16,10),(17,10),(18,10),(19,10),
        (4,11),(5,11),(6,11),(7,11),(8,11),(9,11),(10,11),(11,11),(12,11),(13,11),(14,11),(15,11),(16,11),(17,11),(18,11),(19,11),
        (4,12),(5,12),(6,12),(7,12),(8,12),(9,12),(10,12),(11,12),(12,12),(13,12),(14,12),(15,12),(16,12),(17,12),(18,12),(19,12),
        (4,13),(5,13),(6,13),(7,13),(8,13),(9,13),(10,13),(11,13),(12,13),(13,13),(14,13),(15,13),(16,13),(17,13),(18,13),(19,13),
        (4,14),(5,14),(6,14),(7,14),(8,14),(9,14),(10,14),(11,14),(12,14),(13,14),(14,14),(15,14),(16,14),(17,14),(18,14),(19,14),
        (5,15),(6,15),(7,15),(8,15),(9,15),(10,15),(11,15),(12,15),(13,15),(14,15),(15,15),(16,15),(17,15),(18,15),
        (5,16),(6,16),(7,16),(8,16),(9,16),(10,16),(11,16),(12,16),(13,16),(14,16),(15,16),(16,16),(17,16),(18,16),
        (6,17),(7,17),(8,17),(9,17),(10,17),(11,17),(12,17),(13,17),(14,17),(15,17),(16,17),(17,17),
        (8,18),(9,18),(10,18),(11,18),(12,18),(13,18),(14,18),(15,18),
    ]
    for x, y in plate_pixels:
        put(im, x, y, PLATE)

    # Plate rim shadow (bottom arc)
    rim_shadow = [
        (14,16),(15,16),(16,16),(17,16),
        (12,17),(13,17),(14,17),(15,17),(16,17),
        (10,18),(11,18),(12,18),(13,18),(14,18),
    ]
    for x, y in rim_shadow:
        put(im, x, y, PLATE_SH)

    # Food mound (center dome — smaller circle inside the plate)
    food_px = [
        (9,9),(10,9),(11,9),(12,9),(13,9),(14,9),
        (8,10),(9,10),(10,10),(11,10),(12,10),(13,10),(14,10),(15,10),
        (7,11),(8,11),(9,11),(10,11),(11,11),(12,11),(13,11),(14,11),(15,11),(16,11),
        (7,12),(8,12),(9,12),(10,12),(11,12),(12,12),(13,12),(14,12),(15,12),(16,12),
        (7,13),(8,13),(9,13),(10,13),(11,13),(12,13),(13,13),(14,13),(15,13),(16,13),
        (8,14),(9,14),(10,14),(11,14),(12,14),(13,14),(14,14),(15,14),
        (9,15),(10,15),(11,15),(12,15),(13,15),(14,15),
    ]
    for x, y in food_px:
        put(im, x, y, FOOD_BASE)

    # Food highlight (upper-left of mound)
    food_hl = [
        (9,9),(10,9),(11,9),(12,9),
        (8,10),(9,10),(10,10),(11,10),
        (7,11),(8,11),(9,11),(10,11),
        (7,12),(8,12),(9,12),
    ]
    for x, y in food_hl:
        put(im, x, y, FOOD_LT)

    # Golden accent (a grain / carrot dot on top of food)
    put(im, 13, 10, FOOD_ACC)
    put(im, 14, 10, FOOD_ACC)
    put(im, 13, 11, FOOD_ACC)

    # Steam wisps (above plate, faint semi-transparent pixels)
    put(im, 10, 4, STEAM)
    put(im, 11, 3, STEAM)
    put(im, 12, 4, STEAM)
    put(im, 14, 4, STEAM)
    put(im, 13, 3, STEAM)

    # Plate outline
    plate_outline = [
        (7,5),(8,5),(9,5),(10,5),(11,5),(12,5),(13,5),(14,5),(15,5),(16,5),
        (5,6),(6,6),(17,6),(18,6),
        (4,7),(5,7),(18,7),(19,7),
        (3,8),(4,8),(19,8),(20,8),
        (3,9),(20,9),
        (3,10),(20,10),
        (3,11),(20,11),
        (3,12),(20,12),
        (3,13),(20,13),
        (3,14),(20,14),
        (4,15),(19,15),
        (4,16),(5,16),(18,16),(19,16),
        (5,17),(6,17),(17,17),(18,17),
        (7,18),(8,18),(9,18),(10,18),(11,18),(12,18),(13,18),(14,18),(15,18),(16,18),
        (9,19),(10,19),(11,19),(12,19),(13,19),(14,19),
    ]
    for x, y in plate_outline:
        put(im, x, y, OUTLINE_OBJ)

    return im


# ── icon_food.png ────────────────────────────────────────────────────────────
# Raw food: a ripe grain/wheat ear — paired kernels on a stem.
# CROP_GOLD body + GRASS_DK stem.  Silhouette reads as "grain" at 24px.
# Redesigned: organic paired-kernel ear (not boxy rect grid).

def gen_icon_food():
    im = new_img(24, 24)

    GRAIN    = CROP_GOLD              # ripe grain — the reserved semantic accent
    GRAIN_DK = (168, 136, 52, 255)   # shadow bottom row of each kernel pair
    STEM_COL = GRASS_DK              # stem — muted olive, recedes

    # Central stem  x=11..12, full column
    for y in range(6, 21):
        put(im, 11, y, STEM_COL)
        put(im, 12, y, STEM_COL)

    # Top awn spike (points up above ear)
    put(im, 11, 3, STEM_COL); put(im, 12, 3, STEM_COL)
    put(im, 11, 4, STEM_COL); put(im, 12, 4, STEM_COL)
    put(im, 11, 5, STEM_COL)

    # Grain pair 1 — topmost  y=5..7
    for x in range(8, 11): put(im, x, 5, GRAIN)
    for x in range(8, 11): put(im, x, 6, GRAIN)
    put(im, 9, 7, GRAIN)
    for x in range(12, 15): put(im, x, 5, GRAIN)
    for x in range(12, 15): put(im, x, 6, GRAIN)
    put(im, 13, 7, GRAIN)

    # Grain pair 2  y=8..10
    for x in range(7, 11): put(im, x, 8, GRAIN)
    for x in range(7, 11): put(im, x, 9, GRAIN)
    put(im, 8, 10, GRAIN); put(im, 9, 10, GRAIN)
    for x in range(12, 16): put(im, x, 8, GRAIN)
    for x in range(12, 16): put(im, x, 9, GRAIN)
    put(im, 13, 10, GRAIN); put(im, 14, 10, GRAIN)

    # Grain pair 3  y=11..13
    for x in range(7, 11): put(im, x, 11, GRAIN)
    for x in range(7, 11): put(im, x, 12, GRAIN)
    put(im, 8, 13, GRAIN); put(im, 9, 13, GRAIN)
    for x in range(12, 16): put(im, x, 11, GRAIN)
    for x in range(12, 16): put(im, x, 12, GRAIN)
    put(im, 13, 13, GRAIN); put(im, 14, 13, GRAIN)

    # Grain pair 4 — base of ear  y=14..16
    for x in range(8, 12): put(im, x, 14, GRAIN)
    for x in range(8, 12): put(im, x, 15, GRAIN)
    for x in range(12, 16): put(im, x, 14, GRAIN)
    for x in range(12, 16): put(im, x, 15, GRAIN)

    # Shadow (darker bottom pixel row of each kernel pair)
    for x in range(8, 11): put(im, x, 7,  GRAIN_DK)
    for x in range(12,15): put(im, x, 7,  GRAIN_DK)
    for x in range(7, 11): put(im, x, 10, GRAIN_DK)
    for x in range(12,16): put(im, x, 10, GRAIN_DK)
    for x in range(7, 11): put(im, x, 13, GRAIN_DK)
    for x in range(12,16): put(im, x, 13, GRAIN_DK)
    for x in range(8, 16): put(im, x, 16, GRAIN_DK)

    # Leaf sprigs at lower stem
    put(im, 9, 17, STEM_COL); put(im, 8, 17, STEM_COL); put(im, 7, 18, STEM_COL)
    put(im, 13, 17, STEM_COL); put(im, 14, 17, STEM_COL); put(im, 15, 18, STEM_COL)

    # Auto-outline: scan all opaque pixels and place OUTLINE_OBJ on adjacent
    # transparent neighbours — gives clean 1px silhouette edge.
    opaque = [(x, y) for y in range(24) for x in range(24)
              if im.getpixel((x, y))[3] > 0]
    border = set()
    for x, y in opaque:
        for dx, dy in [(-1,0),(1,0),(0,-1),(0,1)]:
            nx, ny = x+dx, y+dy
            if 0<=nx<24 and 0<=ny<24 and im.getpixel((nx, ny))[3] == 0:
                border.add((nx, ny))
    for x, y in border:
        put(im, x, y, OUTLINE_OBJ)

    return im


# ── preview composite ────────────────────────────────────────────────────────

def gen_preview(icons):
    """4-up composite scaled 4x for visual verification."""
    n = len(icons)
    gap = 4
    W = 24 * n + gap * (n - 1)
    H = 24
    bg = (42, 31, 24, 255)   # UI_PANEL_BG — the actual background these sit on
    canvas = Image.new("RGBA", (W, H), bg)
    for i, (_, im) in enumerate(icons):
        canvas.paste(im, (i * (24 + gap), 0), im)
    scale = 6
    preview = canvas.resize((W * scale, H * scale), Image.NEAREST)
    out = os.path.join(_HERE, "_preview_icons.png")
    preview.save(out)
    print(f"[gen_icons] preview  {out}  ({W * scale}x{H * scale})")


# ── main ─────────────────────────────────────────────────────────────────────

def main():
    targets = [
        ("icon_stone.png", gen_icon_stone),
        ("icon_wood.png",  gen_icon_wood),
        ("icon_meal.png",  gen_icon_meal),
        ("icon_food.png",  gen_icon_food),
    ]
    icons = []
    for name, fn in targets:
        im = fn()
        out = os.path.join(_HERE, name)
        im.save(out)
        print(f"[gen_icons] {name}  {im.size[0]}x{im.size[1]}")
        icons.append((name, im))
    gen_preview(icons)


if __name__ == "__main__":
    main()
