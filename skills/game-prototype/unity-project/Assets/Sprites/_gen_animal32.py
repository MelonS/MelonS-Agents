# -*- coding: utf-8 -*-
"""_gen_animal32.py — PawnSim Art v2: 32x32 animal sprite sheets (side view, facing left).

Spec: G:/ai/_artv2_staging/artv2-style-spec.md
Palette: imported from Assets/Sprites/palette.py (single source of truth).
Species colors: HSV value-derivation from palette anchors only (max +-12%).

Sheets: animal32_{species}.png  64x32, col0=idle col1=walk (W = runtime flipX of nothing
needed here — art faces left = W-ish; E is flipX at runtime if engine faces right).
Quadruped template: body ellipse + head (front-left) + ears + 4 legs + tail + 1px eye.
Exception: chicken is a biped (2x 1px legs, gold) — same idle/walk crossing rule applies.
Walk frame: legs cross fore/aft +-2px, body identical to idle (same silhouette bbox —
no limp; lesson from pawn32 E-walk).
1px OUTLINE_STORY outline, flat fill + 2-step ramp shading (light top-left), no dither.
Deterministic (seed 42 — only used for preview grass tiles).
"""
import sys, os, colorsys, random

sys.stdout.reconfigure(encoding='utf-8')

PAL_DIR = r"G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Sprites"
sys.path.insert(0, PAL_DIR)
from palette import (OUTLINE_STORY,
                     GRASS_DK, GRASS_MD, GRASS_LT,
                     DIRT_DK, DIRT_MD, DIRT_LT,
                     ROCK_DK, ROCK_MD, ROCK_LT,
                     CLOTH_RUST, CLOTH_RUST_DK,
                     WOOD_LT, CROP_GOLD, MEAT_RED,
                     DANGER_RED, UI_CREAM)
from PIL import Image

OUT = r"G:/ai/_artv2_staging/out"
os.makedirs(OUT, exist_ok=True)
SCALE = 8


# ── palette-rule-compliant derivation (HSV value scale, +-12% max) ─────
def shade(c, vmul):
    assert 0.88 <= vmul <= 1.12, "palette rule: value derivation max +-12%"
    r, g, b, a = c
    h, s, v = colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)
    v = max(0.0, min(1.0, v * vmul))
    r2, g2, b2 = colorsys.hsv_to_rgb(h, s, v)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


# ── species ramps (MD base / DK shadow / LT highlight) ─────────────────
SPECIES_COLORS = {
    #          MD                      DK                      LT
    'wolf':   (ROCK_MD,                ROCK_DK,                ROCK_LT),
    'boar':   (shade(DIRT_DK, 1.12),   shade(DIRT_DK, 0.90),   DIRT_MD),
    'rabbit': (DIRT_LT,                DIRT_MD,                shade(DIRT_LT, 1.12)),
    'fox':    (CLOTH_RUST,             CLOTH_RUST_DK,          shade(CLOTH_RUST, 1.12)),
    'deer':   (WOOD_LT,                DIRT_LT,                shade(WOOD_LT, 1.12)),
    'chicken': (shade(UI_CREAM, 0.96), shade(UI_CREAM, 0.88),  UI_CREAM),
}
WOLF_OUTLINE = shade(OUTLINE_STORY, 0.88)   # hostile: darker outline
TUSK_CREAM = UI_CREAM                       # boar tusk / fox chest+tail tip
RABBIT_TAIL = shade(DIRT_LT, 1.12)          # cotton puff = body LT
CHICKEN_BEAK = shade(CROP_GOLD, 0.95)       # beak + legs (derived gold)
CHICKEN_COMB = shade(MEAT_RED, 1.10)        # comb red


# ── low-level paint helpers ────────────────────────────────────────────
def rect(im, x0, y0, x1, y1, c):
    p = im.load()
    W, H = im.size
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if 0 <= x < W and 0 <= y < H:
                p[x, y] = c


def pset(im, pts, c):
    p = im.load()
    W, H = im.size
    for x, y in pts:
        if 0 <= x < W and 0 <= y < H:
            p[x, y] = c


def ell(im, cx, cy, rx, ry, c):
    p = im.load()
    W, H = im.size
    for y in range(H):
        for x in range(W):
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0:
                p[x, y] = c


def outline(im, color):
    """1px outer outline: every transparent pixel 4-adjacent to body."""
    p = im.load()
    W, H = im.size
    edge = []
    for y in range(H):
        for x in range(W):
            if p[x, y][3] == 0:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < W and 0 <= ny < H and p[nx, ny][3] != 0:
                        edge.append((x, y))
                        break
    for x, y in edge:
        p[x, y] = color


def rim_shade(im, md, lt, dk):
    """Flat + 2-step ramp: light source top-left.  Only touches MD pixels."""
    src = im.copy()
    sp, p = src.load(), im.load()
    W, H = im.size
    for y in range(H):
        for x in range(W):
            if sp[x, y] != md:
                continue
            top = y == 0 or sp[x, y - 1][3] == 0
            left = x == 0 or sp[x - 1, y][3] == 0
            bot = y == H - 1 or sp[x, y + 1][3] == 0
            right = x == W - 1 or sp[x + 1, y][3] == 0
            if top or (left and not bot):
                p[x, y] = lt
            elif bot or right:
                p[x, y] = dk


def upscale(im, s=SCALE):
    return im.resize((im.width * s, im.height * s), Image.NEAREST)


def legs(im, specs, f, md, dk, ybot=29):
    """specs: list of (x_idle, dx_walk, w, ytop, far). Walk shifts x only —
    body untouched, so idle/walk silhouettes match (no limp)."""
    for x0, dx, w, ytop, far in specs:
        x = x0 + (dx if f == 1 else 0)
        rect(im, x, ytop, x + w - 1, ybot, dk if far else md)


# ── species drawing (32x32 cell, facing left, feet on y29) ─────────────
def draw_wolf(f):
    md, dk, lt = SPECIES_COLORS['wolf']
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    # far legs first (DK), diagonal gait: FF back / BF fwd when NF fwd / NB back
    legs(im, [(9, +2, 2, 21, True), (23, -2, 2, 21, True)], f, md, dk)
    ell(im, 27.0, 15.0, 1.9, 2.4, md)             # bushy raised tail x26..28 y13..17
    ell(im, 17.0, 19.0, 10.4, 4.4, md)            # body x7..26 y15..23
    rect(im, 4, 17, 7, 21, md)                    # deep chest (connects front legs)
    ell(im, 6.3, 15.0, 3.6, 3.2, md)              # head x3..10 y12..18
    rect(im, 2, 15, 3, 16, md)                    # muzzle
    pset(im, [(4, 10), (4, 11), (4, 12), (5, 11), (5, 12),
              (7, 10), (7, 11), (8, 11)], md)     # 2 pointed ears, notch at x6
    legs(im, [(6, -2, 2, 21, False), (20, +2, 2, 21, False)], f, md, dk)
    rim_shade(im, md, lt, dk)
    pset(im, [(2, 16)], dk)                       # muzzle underside
    outline(im, WOLF_OUTLINE)                     # hostile = darker outline
    pset(im, [(4, 14)], DANGER_RED)               # hostile eye
    return im


def draw_boar(f):
    md, dk, lt = SPECIES_COLORS['boar']
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    legs(im, [(10, +2, 2, 22, True), (24, -2, 2, 22, True)], f, md, dk)
    pset(im, [(27, 15), (28, 16), (27, 17)], dk)  # thin curl tail
    ell(im, 16.0, 19.0, 10.4, 5.4, md)            # stocky body x6..26 y14..24
    ell(im, 6.0, 18.0, 4.2, 4.2, md)              # big head x2..10 y14..22
    rect(im, 5, 12, 7, 13, md)                    # round ear
    rect(im, 1, 18, 2, 20, md)                    # snout
    legs(im, [(7, -2, 2, 22, False), (21, +2, 2, 22, False)], f, md, dk)
    rim_shade(im, md, lt, dk)
    pset(im, [(1, 18), (1, 19), (1, 20)], dk)     # snout disc dark
    pset(im, [(3, 21)], TUSK_CREAM)               # tusk 1px
    pset(im, [(6, 13)], dk)                       # inner ear
    pset(im, [(9, 14), (12, 14), (15, 14)], dk)   # bristle mane dots
    outline(im, OUTLINE_STORY)
    pset(im, [(4, 16)], OUTLINE_STORY)            # eye
    return im


def draw_rabbit(f):
    md, dk, lt = SPECIES_COLORS['rabbit']
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    legs(im, [(12, +2, 1, 25, True), (19, -2, 1, 25, True)], f, md, dk)
    ell(im, 17.0, 24.0, 6.4, 4.4, md)             # body x11..23 y20..28 (~16x12 incl head)
    ell(im, 18.5, 24.5, 4.6, 4.0, md)             # haunch emphasis
    ell(im, 10.5, 20.0, 3.4, 3.2, md)             # head x7..14 y17..23
    rect(im, 8, 22, 12, 25, md)                   # chest (connects front legs)
    rect(im, 9, 8, 10, 15, md)                    # ear 1 upright
    rect(im, 12, 9, 13, 16, md)                   # ear 2 upright (slightly back)
    legs(im, [(10, -2, 1, 25, False), (16, +2, 2, 26, False)], f, md, dk)
    rim_shade(im, md, lt, dk)
    pset(im, [(10, 9), (10, 10), (10, 11), (13, 10), (13, 11), (13, 12)], dk)  # inner ear
    pset(im, [(23, 22)], RABBIT_TAIL)             # cotton puff 1px LT, on body edge
    outline(im, OUTLINE_STORY)
    pset(im, [(9, 19)], OUTLINE_STORY)            # eye
    return im


def draw_fox(f):
    md, dk, lt = SPECIES_COLORS['fox']
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    legs(im, [(10, +2, 1, 22, True), (23, -2, 1, 22, True)], f, md, dk)
    ell(im, 25.0, 19.0, 3.0, 2.9, md)             # thick tail x22..28 y17..21 (bulges above back)
    ell(im, 15.5, 20.5, 7.9, 3.9, md)             # slender body x8..23 y17..24
    rect(im, 6, 18, 9, 22, md)                    # chest (connects front legs)
    ell(im, 7.0, 16.5, 3.9, 2.9, md)              # head x3..10 y14..19
    rect(im, 2, 16, 3, 17, md)                    # narrow snout
    pset(im, [(5, 11), (5, 12), (5, 13), (6, 12), (6, 13),
              (8, 11), (8, 12), (8, 13), (9, 13)], md)  # tall pointed ears
    legs(im, [(8, -2, 2, 22, False), (20, +2, 2, 22, False)], f, md, dk)
    rim_shade(im, md, lt, dk)
    pset(im, [(22, 18), (22, 19), (22, 20), (22, 21)], dk)  # tail/body separation
    pset(im, [(28, 19)], TUSK_CREAM)              # white tail tip 1px
    pset(im, [(6, 19)], TUSK_CREAM)               # white chest 1px
    outline(im, OUTLINE_STORY)
    pset(im, [(5, 16)], OUTLINE_STORY)            # eye
    return im


def draw_deer(f):
    md, dk, lt = SPECIES_COLORS['deer']
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    legs(im, [(11, +2, 1, 21, True), (24, -2, 1, 21, True)], f, md, dk)
    ell(im, 17.5, 18.5, 9.0, 3.2, md)             # slender body x9..26 y16..21
    pset(im, [(27, 17), (27, 18)], md)            # short tail (attaches body rear)
    rect(im, 8, 15, 11, 20, md)                   # chest (connects front legs)
    rect(im, 7, 10, 9, 16, md)                    # long neck
    ell(im, 5.5, 10.0, 2.6, 1.9, md)              # small head x3..8 y9..11
    rect(im, 2, 10, 3, 11, md)                    # narrow muzzle
    legs(im, [(9, -2, 2, 21, False), (21, +2, 2, 21, False)], f, md, dk)
    rim_shade(im, md, lt, dk)
    pset(im, [(4, 8), (4, 7), (3, 6),
              (6, 8), (7, 7), (8, 6)], dk)        # antlers: front beam + back-swept beam
    pset(im, [(2, 11)], dk)                       # nose tip
    pset(im, [(27, 18)], TUSK_CREAM)              # white tail flash 1px
    outline(im, OUTLINE_STORY)
    pset(im, [(4, 10)], OUTLINE_STORY)            # eye
    return im


def draw_chicken(f):
    """Biped — quadruped-template exception: 2 legs (1px), crossing fore/aft on walk."""
    md, dk, lt = SPECIES_COLORS['chicken']
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    legs(im, [(14, -2, 1, 24, False), (17, +2, 1, 24, False)], f,
         CHICKEN_BEAK, CHICKEN_BEAK)              # 2 gold legs, tops hidden by body
    ell(im, 16.0, 23.0, 4.4, 3.4, md)             # plump body x12..20 y20..26
    pset(im, [(20, 21), (21, 20)], md)            # tail feathers 2px (up-back)
    ell(im, 12.5, 18.0, 1.9, 1.9, md)             # head x11..14 y17..19
    rect(im, 12, 19, 13, 21, md)                  # neck
    rim_shade(im, md, lt, dk)
    pset(im, [(15, 23), (16, 23), (17, 24)], dk)  # folded wing arc
    pset(im, [(10, 18)], CHICKEN_BEAK)            # beak 1px gold
    pset(im, [(12, 16)], CHICKEN_COMB)            # comb 1px red
    outline(im, OUTLINE_STORY)
    pset(im, [(11, 17)], OUTLINE_STORY)           # eye
    return im


DRAW = {'wolf': draw_wolf, 'boar': draw_boar, 'rabbit': draw_rabbit, 'fox': draw_fox,
        'deer': draw_deer, 'chicken': draw_chicken}
SPECIES = ('wolf', 'boar', 'rabbit', 'fox', 'deer', 'chicken')


# ── grass tile for contrast preview (spec section 5 compliant) ─────────
def grass_tile(rng):
    im = Image.new('RGBA', (32, 32), GRASS_MD)
    p = im.load()
    budget = 88
    while budget > 0:
        x = rng.randint(1, 30)
        y = rng.randint(1, 29)
        if rng.random() < 0.5 and budget >= 2:
            p[x, y] = GRASS_DK
            p[x, y + 1] = GRASS_DK
            budget -= 2
        else:
            p[x, y] = GRASS_LT
            budget -= 1
    return im


def bbox(im):
    b = im.getbbox()
    return b  # (x0, y0, x1, y1) exclusive


def main():
    rng = random.Random(42)
    frames = {}
    for sp in SPECIES:
        idle = DRAW[sp](0)
        walk = DRAW[sp](1)
        frames[sp] = (idle, walk)
        sheet = Image.new('RGBA', (64, 32), (0, 0, 0, 0))
        sheet.paste(idle, (0, 0), idle)
        sheet.paste(walk, (32, 0), walk)
        sheet.save(os.path.join(OUT, f"animal32_{sp}.png"))

        # silhouette sanity: walk bbox == idle bbox (no limp, no width change)
        bi, bw = bbox(idle), bbox(walk)
        assert bi[0] == bw[0] and bi[2] == bw[2], f"{sp}: walk width != idle {bi} {bw}"
        assert bi[3] == bw[3], f"{sp}: walk ground line != idle {bi} {bw}"
        w, h = bi[2] - bi[0], bi[3] - bi[1]
        print(f"animal32_{sp}: bbox {w}x{h}  idle==walk silhouette OK")

    # preview: cols = 6 species, rows = idle / walk, on grass
    pv = Image.new('RGBA', (32 * len(SPECIES), 64), (0, 0, 0, 0))
    tiles = [grass_tile(rng) for _ in range(3)]
    for ty in range(2):
        for tx in range(len(SPECIES)):
            pv.paste(tiles[(tx + ty) % 3], (tx * 32, ty * 32))
    for ci, sp in enumerate(SPECIES):
        for ri in range(2):
            fr = frames[sp][ri]
            pv.paste(fr, (ci * 32, ri * 32), fr)
    upscale(pv).save(os.path.join(OUT, "_preview_animal32.png"))
    print("preview: _preview_animal32.png (cols " + "|".join(SPECIES) + ", rows idle/walk)")
    print("done ->", OUT)


if __name__ == '__main__':
    main()
