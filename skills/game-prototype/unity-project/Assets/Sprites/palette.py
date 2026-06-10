# -*- coding: utf-8 -*-
"""PawnSim master palette v1 — warm muted earth, the reference sim-grounded.

Single source of truth for ALL sprite generators.  Import here; never
define ad-hoc colors in individual generators.

Usage:
    from palette import GRASS_MD, OUTLINE_STORY, SKIN_MD, ...

Groupings follow the visual hierarchy in the backlog north-star spec
(docs/design-improvement-backlog.md, "GLOBAL PALETTE" section).
"""

# ── Outlines (hierarchy) ────────────────────────────────────────────
# Use OUTLINE_STORY for pawns/animals (2px), OUTLINE_OBJ for buildings/
# items (1px), OUTLINE_PLANT for foliage edge or no outline.
OUTLINE_STORY = (26, 20, 16, 255)    # #1A1410  pawns + animals — 2px
OUTLINE_OBJ   = (40, 30, 22, 255)    # #281E16  buildings / items — 1px
OUTLINE_PLANT = (34, 56, 32, 255)    # #223820  plant dark-green edge

# ── Terrain — LOW saturation; must recede behind pawns ──────────────
GRASS_DK      = (74,  92,  58, 255)  # #4A5C3A  olive shadow
GRASS_MD      = (96, 116,  70, 255)  # #607446  base grass (muted khaki-green)
GRASS_LT      = (118, 138, 86, 255)  # #768A56  dappled highlight

DIRT_DK       = (86,  66,  46, 255)  # #56422E
DIRT_MD       = (112, 88,  62, 255)  # #70583E  tilled soil / paths
DIRT_LT       = (138, 112, 80, 255)  # #8A7050

# TOP-1 (visual-polish-backlog 2026-06-11): 한색 블루그레이가 웜톤 잔디/흙 옆에서
#  보라색으로 읽혀 "QR코드 지면" 인상의 주범 — 웜그레이로 이동 (hue ~40°, 저채도).
ROCK_DK       = (76,  72,  64, 255)  # #4C4840  warm dark stone
ROCK_MD       = (106, 100, 88, 255)  # #6A6458  warm grey
ROCK_LT       = (140, 132, 118, 255) # #8C8476  warm light

WATER_DK      = (52,  78,  96, 255)  # #344E60
WATER_MD      = (74, 108, 128, 255)  # #4A6C80
WATER_LT      = (104, 140, 160, 255) # #688CA0

# ── Wood — ONE ramp; walls/floors/beds/benches all use this ─────────
# Retire the three separate wood ramps that previously existed.
WOOD_DK       = (92,  60,  36, 255)  # #5C3C24
WOOD_MD       = (140, 92,  54, 255)  # #8C5C36
WOOD_LT       = (188, 138, 88, 255)  # #BC8A58

# ── Pawn — the ONLY high-saturation focal subjects on screen ────────
SKIN_MD       = (224, 176, 132, 255) # #E0B084
SKIN_SH       = (176, 132, 96, 255)  # #B08460  shadow on skin

HAIR_DK       = (58,  38,  22, 255)  # #3A2616

# Cloth tints — three colonist variants.  Keep muted; no neon.
CLOTH_BLUE    = (74,  96, 132, 255)  # #4A6084  muted denim (colonist default)
CLOTH_RUST    = (158, 86,  58, 255)  # #9E563A  rust / reddish-brown
CLOTH_OLIVE   = (108, 112, 70, 255)  # #6C7046  olive / field-green

# ── Semantic accents — saturated; reserve for story/meaning ─────────
CROP_GOLD     = (218, 178, 70, 255)  # #DAB246  ripe crop / wheat heads
FIRE_OR       = (232, 120, 44, 255)  # #E8782C  fire base
FIRE_LT       = (250, 196, 96, 255)  # #FAC460  fire highlight
MEAT_RED      = (172, 74,  60, 255)  # #AC4A3C  raw meat
DANGER_RED    = (210, 72,  60, 255)  # #D2483C  enemies / HP critical

# ── UI — mirrors UITheme.cs; keep in sync manually ──────────────────
UI_PANEL_BG   = (42, 31, 24, 240)    # #2A1F18 a0.94
UI_PANEL_HDR  = (73, 50, 35, 242)    # #493223
UI_BORDER     = (90, 65, 46, 255)    # #5A412E  every panel gets 1–2px border
UI_GOLD       = (244, 215, 138, 255) # #F4D78A  titles
UI_ORANGE     = (232, 181, 96, 255)  # #E8B560  active / selected state
UI_CREAM      = (242, 228, 208, 255) # #F2E4D0  body text
UI_MUTED      = (187, 170, 148, 255) # #BBAA94  hints / secondary labels

# ── Additional derived shades (computed once here) ──────────────────
# Cloth tint for dark shadow-side detail (50% darkened from each cloth)
CLOTH_BLUE_DK  = (50, 66, 94, 255)   # dark denim shadow
CLOTH_RUST_DK  = (110, 58, 36, 255)  # dark rust shadow
CLOTH_OLIVE_DK = (74, 78, 44, 255)   # dark olive shadow

# Leg / trouser — slightly darker than cloth so they read separately
TROUSER_BLUE   = (58, 76, 108, 255)
TROUSER_RUST   = (130, 68, 44, 255)
TROUSER_OLIVE  = (86, 90, 52, 255)
