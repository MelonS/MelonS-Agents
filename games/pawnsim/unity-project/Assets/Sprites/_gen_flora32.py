# -*- coding: utf-8 -*-
"""_gen_flora32.py — PawnSim Art v2 flora / natural deco generator (32px scale).

Spec : G:/ai/_artv2_staging/artv2-style-spec.md  (§1 palette rules, §2 common, §6 flora)
Palette: imported from Assets/Sprites/palette.py — single source of truth, no ad-hoc colors.
Deterministic: fixed SEED, no wall-clock / no os entropy.

Contents
  - tree_a / tree_b / tree_c : 32x48, asymmetric canopy, trunk knot, 2-step leaf shading,
                               OUTLINE_PLANT 1px
  - stump                    : 32x16, cut-face rings, OUTLINE_OBJ
  - sapling                  : 16x16, OUTLINE_PLANT
  - bush_berry               : 24x20, MEAT_RED 2x2 dots x4 (DANGER_RED forbidden), OUTLINE_PLANT
  - rock_a / rock_b / rock_c : warm-grey ROCK ramp facets, OUTLINE_OBJ
  - flower_a / flower_b      : 8x8, no outline
  - tuft_a / tuft_b          : grass tufts, no outline

Output: G:/ai/_artv2_staging/out/flora32_<name>.png
        + _preview_flora32_grid.png   (8x, neutral dark bg)
        + _preview_flora32_grass.png  (8x, composited on GRASS_MD terrain — silhouette check)
"""
import sys
import os
import random

sys.stdout.reconfigure(encoding="utf-8")

PAL_DIR = r"G:/ai/MelonS-Agents/games/pawnsim/unity-project/Assets/Sprites"
sys.path.insert(0, PAL_DIR)
import palette as P  # noqa: E402

from PIL import Image, ImageDraw  # noqa: E402

OUT = r"G:/ai/_artv2_staging/out"
SEED = 20260611
SCALE = 8

rng = random.Random(SEED)


# ── helpers ──────────────────────────────────────────────────────────
def new(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def in_ellipse(x, y, cx, cy, rx, ry):
    if rx <= 0 or ry <= 0:
        return False
    dx = (x - cx) / rx
    dy = (y - cy) / ry
    return dx * dx + dy * dy <= 1.0


def ellipse_set(cx, cy, rx, ry):
    pts = set()
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            if in_ellipse(x, y, cx, cy, rx, ry):
                pts.add((x, y))
    return pts


def put(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), c)


def add_outline(img, color):
    """1px outer outline around every opaque region (collect-then-apply, no cascade)."""
    w, h = img.size
    p = img.load()
    edge = []
    for y in range(h):
        for x in range(w):
            if p[x, y][3] == 0:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and p[nx, ny][3] != 0:
                        edge.append((x, y))
                        break
    for x, y in edge:
        p[x, y] = color


def scale8(img):
    return img.resize((img.width * SCALE, img.height * SCALE), Image.NEAREST)


def interior(pts):
    """Pixels whose 4-neighbours are all inside the set — keeps light off the outline."""
    return {(x, y) for (x, y) in pts
            if all((x + dx, y + dy) in pts for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}


# ── trees 32x48 ──────────────────────────────────────────────────────
TREE_SHAPES = {
    # lobes: (cx, cy, rx, ry) union → asymmetric canopy.  lit: ellipse of GRASS_MD light.
    "a": dict(
        lobes=[(16, 15, 12, 11), (23, 12, 7, 6), (9, 19, 7, 6), (12, 7, 6, 5)],
        lit=(11, 10, 10, 8),
        knot=(14, 34),
    ),
    "b": dict(
        lobes=[(15, 16, 12, 10), (8, 11, 6, 6), (21, 19, 7, 5), (19, 7, 6, 5)],
        lit=(10, 11, 9, 8),
        knot=(15, 37),
    ),
    "c": dict(
        lobes=[(16, 13, 9, 11), (22, 19, 6, 6), (11, 7, 6, 5), (10, 19, 6, 5)],
        lit=(11, 8, 8, 8),
        knot=(14, 33),
    ),
}


def make_tree(variant):
    img = new(32, 48)
    spec = TREE_SHAPES[variant]

    # trunk first — canopy overdraws its top
    for y in range(24, 46):
        for x in range(14, 18):
            if x == 14:
                c = P.WOOD_LT
            elif x == 17:
                c = P.WOOD_DK
            else:
                c = P.WOOD_MD
            put(img, x, y, c)
    # base flare
    for y in (44, 45):
        put(img, 13, y, P.WOOD_MD)
        put(img, 18, y, P.WOOD_DK)
    # knot (옹이): 3x2 dark oval with 1px light core
    kx, ky = spec["knot"]
    for dx in range(3):
        for dy in range(2):
            put(img, kx + dx, ky + dy, P.WOOD_DK)
    put(img, kx + 1, ky, P.WOOD_LT)

    # canopy: GRASS_DK base + GRASS_MD top-left light (2-step) + few LT sparkles
    canopy = set()
    for cx, cy, rx, ry in spec["lobes"]:
        canopy |= ellipse_set(cx, cy, rx, ry)
    lcx, lcy, lrx, lry = spec["lit"]
    core = interior(interior(canopy))  # 2px DK margin so light never touches the outline
    lit = {(x, y) for (x, y) in core if in_ellipse(x, y, lcx, lcy, lrx * 0.85, lry * 0.85)}
    for x, y in canopy:
        put(img, x, y, P.GRASS_MD if (x, y) in lit else P.GRASS_DK)
    rim = sorted(lit, key=lambda q: (q[0] + q[1], q[0]))[:20]
    for x, y in rng.sample(rim, min(7, len(rim))):
        put(img, x, y, P.GRASS_LT)

    add_outline(img, P.OUTLINE_PLANT)
    return img


# ── stump 32x16 ──────────────────────────────────────────────────────
def make_stump():
    img = new(32, 16)
    cx, topy, rx, ry = 16, 5, 8, 3

    # side (cylinder)
    for y in range(topy, 13):
        for x in range(cx - rx, cx + rx + 1):
            if x <= cx - rx + 1:
                c = P.WOOD_LT
            elif x >= cx + rx - 2:
                c = P.WOOD_DK
            else:
                c = P.WOOD_MD
            put(img, x, y, c)
    # bottom cap + root flare
    for x, y in ellipse_set(cx, 12, rx, 2):
        if img.getpixel((x, y))[3] == 0:
            put(img, x, y, P.WOOD_DK if x > cx else P.WOOD_MD)
    for y in (12, 13):
        put(img, cx - rx - 1, y, P.WOOD_MD)
        put(img, cx + rx + 1, y, P.WOOD_DK)

    # cut face with one thin growth ring + heart dot
    for x, y in ellipse_set(cx, topy, rx, ry):
        put(img, x, y, P.WOOD_LT)
    d = ImageDraw.Draw(img)
    d.ellipse([cx - 5, topy - 1, cx + 5, topy + 1], outline=P.WOOD_MD, width=1)
    put(img, cx, topy, P.WOOD_MD)
    put(img, cx - 1, topy, P.WOOD_MD)
    put(img, cx + 6, topy + 2, P.WOOD_MD)  # bark chip notch

    add_outline(img, P.OUTLINE_OBJ)
    return img


# ── sapling 16x16 ────────────────────────────────────────────────────
def make_sapling():
    img = new(16, 16)
    for y in range(9, 14):
        put(img, 7, y, P.WOOD_MD)
        put(img, 8, y, P.WOOD_DK)
    canopy = ellipse_set(8, 6, 5, 4) | ellipse_set(11, 5, 3, 2)
    lit = {(x, y) for (x, y) in interior(canopy) if in_ellipse(x, y, 6, 4, 4, 3)}
    for x, y in canopy:
        put(img, x, y, P.GRASS_MD if (x, y) in lit else P.GRASS_DK)
    put(img, 6, 4, P.GRASS_LT)  # single sparkle, top-left light side
    add_outline(img, P.OUTLINE_PLANT)
    return img


# ── berry bush 32x32 (spec §6: full + picked, sheet 64x32) ───────────
def make_bush(picked=False):
    img = new(32, 32)
    ox, oy = 4, 10  # art block 24x20 centered-x, bottom margin 2px
    lobes = [(12, 12, 10, 6), (7, 9, 5, 4), (16, 9, 5, 4)]
    body = set()
    for cx, cy, rx, ry in lobes:
        body |= ellipse_set(cx, cy, rx, ry)
    lit = {(x, y) for (x, y) in interior(interior(body)) if in_ellipse(x, y, 8, 8, 7, 5)}
    for x, y in body:
        put(img, ox + x, oy + y, P.GRASS_MD if (x, y) in lit else P.GRASS_DK)
    if not picked:
        # 4 berries, 2x2 MEAT_RED, asymmetric (DANGER_RED reserved — spec §1)
        for bx, by in [(4, 9), (10, 6), (15, 11), (8, 13)]:
            for dx in range(2):
                for dy in range(2):
                    put(img, ox + bx + dx, oy + by + dy, P.MEAT_RED)
    add_outline(img, P.OUTLINE_PLANT)
    return img


# ── deco rocks (warm grey, faceted) ──────────────────────────────────
def make_rock(w, h, pts, cracks):
    img = new(w, h)
    d = ImageDraw.Draw(img)
    d.polygon(pts, fill=P.ROCK_MD)
    p = img.load()
    mask = [(x, y) for y in range(h) for x in range(w) if p[x, y][3] != 0]
    sums = [x + y for x, y in mask]
    lo = min(sums) + 0.30 * (max(sums) - min(sums))
    hi = min(sums) + 0.72 * (max(sums) - min(sums))
    for x, y in mask:
        s = x + y
        if s <= lo:
            p[x, y] = P.ROCK_LT
        elif s >= hi:
            p[x, y] = P.ROCK_DK
    for x, y in cracks:
        put(img, x, y, P.ROCK_DK)
    add_outline(img, P.OUTLINE_OBJ)
    return img


def make_rocks():
    a = make_rock(20, 14, [(3, 12), (1, 7), (5, 2), (13, 1), (18, 5), (18, 11), (13, 13)],
                  [(9, 6), (10, 7), (10, 8)])
    b = make_rock(14, 10, [(2, 8), (1, 4), (6, 1), (11, 2), (12, 7), (8, 9)],
                  [(6, 4), (7, 5)])
    c = make_rock(10, 8, [(1, 6), (2, 2), (6, 1), (8, 4), (7, 6)], [(4, 3)])
    return a, b, c


# ── wildflowers 8x8 (no outline) ─────────────────────────────────────
def make_flower(kind):
    img = new(8, 8)
    if kind == 1:  # white daisy, gold core
        for y in range(4, 7):
            put(img, 4, y, P.OUTLINE_PLANT)
        put(img, 3, 5, P.GRASS_DK)  # leaf
        for x, y in [(3, 2), (5, 2), (4, 1), (4, 3)]:
            put(img, x, y, P.UI_CREAM)
        put(img, 4, 2, P.CROP_GOLD)
    else:  # twin gold buds
        for y in range(3, 7):
            put(img, 2, y, P.OUTLINE_PLANT)
        for y in range(5, 8):
            put(img, 5, y, P.OUTLINE_PLANT)
        for bx, by in [(1, 1), (4, 3)]:
            for dx in range(2):
                for dy in range(2):
                    put(img, bx + dx, by + dy, P.CROP_GOLD)
            put(img, bx, by, P.UI_CREAM)
    return img


# ── grass tufts (no outline) ─────────────────────────────────────────
def make_tuft(kind):
    if kind == 1:
        img = new(12, 8)
        blades = [(1, 4), (3, 6), (5, 7), (7, 5), (9, 6), (10, 3)]
    else:
        img = new(10, 7)
        blades = [(1, 3), (3, 5), (5, 6), (7, 4), (8, 2)]
    h = img.height
    for x, hgt in blades:
        for i in range(hgt):
            # bottom half anchored in OUTLINE_PLANT dark green, upper half GRASS_DK
            put(img, x, h - 1 - i, P.OUTLINE_PLANT if i < max(1, hgt // 2) else P.GRASS_DK)
        if hgt >= 5:  # bend + light tip on tall blades
            put(img, x, h - hgt, (0, 0, 0, 0))
            put(img, x + 1, h - hgt, P.GRASS_LT)
        elif hgt >= 3:
            put(img, x, h - hgt, P.GRASS_LT)
    return img


# ── previews ─────────────────────────────────────────────────────────
def grass_bg(w, h):
    """GRASS_MD terrain per spec §5: ~5-10% detail, 1px pure-MD tile border."""
    img = Image.new("RGBA", (w, h), P.GRASS_MD)
    for ty in range(0, h, 32):
        for tx in range(0, w, 32):
            r = random.Random(SEED + tx * 7 + ty * 13)
            for _ in range(45):
                x, y = r.randrange(1, 31), r.randrange(1, 31)
                c = P.GRASS_DK if r.random() < 0.5 else P.GRASS_LT
                if tx + x < w and ty + y < h:
                    img.putpixel((tx + x, ty + y), c)
    return img


def build_grid_preview(sprites):
    pad = 4
    rows = [
        ["tree_a", "tree_b", "tree_c", "stump", "sapling"],
        ["bush_berry", "bush_picked", "rock_a", "rock_b", "rock_c", "flower_a", "flower_b", "tuft_a", "tuft_b"],
    ]
    row_h = [max(sprites[n].height for n in r) for r in rows]
    row_w = [sum(sprites[n].width + pad for n in r) + pad for r in rows]
    W = max(row_w)
    H = sum(h + pad for h in row_h) + pad
    canvas = Image.new("RGBA", (W, H), (54, 48, 42, 255))
    cy = pad
    for r, rh in zip(rows, row_h):
        cx = pad
        for n in r:
            s = sprites[n]
            canvas.alpha_composite(s, (cx, cy + rh - s.height))  # bottom align
            cx += s.width + pad
        cy += rh + pad
    return scale8(canvas)


def build_grass_preview(sprites):
    scene = grass_bg(208, 112)
    # bottoms sit on imaginary ground lines
    place = [
        ("tree_a", 6, 8), ("tree_b", 44, 8), ("tree_c", 82, 8),
        ("stump", 122, 40), ("sapling", 162, 40),
        ("bush_berry", 4, 70), ("rock_a", 44, 76), ("rock_b", 72, 80),
        ("rock_c", 94, 82), ("flower_a", 114, 80), ("flower_b", 128, 80),
        ("tuft_a", 146, 80), ("tuft_b", 166, 81), ("bush_picked", 174, 70),
    ]
    for n, x, y in place:
        scene.alpha_composite(sprites[n], (x, y))
    return scale8(scene)


# ── main ─────────────────────────────────────────────────────────────
def main():
    os.makedirs(OUT, exist_ok=True)
    rock_a, rock_b, rock_c = make_rocks()
    sprites = {
        "tree_a": make_tree("a"),
        "tree_b": make_tree("b"),
        "tree_c": make_tree("c"),
        "stump": make_stump(),
        "sapling": make_sapling(),
        "bush_berry": make_bush(),
        "bush_picked": make_bush(picked=True),
        "rock_a": rock_a,
        "rock_b": rock_b,
        "rock_c": rock_c,
        "flower_a": make_flower(1),
        "flower_b": make_flower(2),
        "tuft_a": make_tuft(1),
        "tuft_b": make_tuft(2),
    }
    for name, img in sprites.items():
        path = os.path.join(OUT, f"flora32_{name}.png")
        img.save(path)
        print(f"saved {path}  {img.width}x{img.height}")

    # spec §8 sheets ----------------------------------------------------
    # tree_v2.png 160x48: tree_a | tree_b | tree_c | stump | sapling
    tree_sheet = new(160, 48)
    for i, n in enumerate(("tree_a", "tree_b", "tree_c")):
        tree_sheet.alpha_composite(sprites[n], (i * 32, 0))
    s = sprites["stump"]                                   # 32x16 → bottom of cell
    tree_sheet.alpha_composite(s, (3 * 32, 48 - s.height))
    sp = sprites["sapling"]                                # 16x16 → centered-x, grounded
    tree_sheet.alpha_composite(sp, (4 * 32 + 8, 48 - sp.height - 2))
    tree_sheet.save(os.path.join(OUT, "tree_v2.png"))
    print(f"saved tree_v2.png  160x48")

    # bush_berry_v2.png 64x32: full | picked
    bush_sheet = new(64, 32)
    bush_sheet.alpha_composite(sprites["bush_berry"], (0, 0))
    bush_sheet.alpha_composite(sprites["bush_picked"], (32, 0))
    bush_sheet.save(os.path.join(OUT, "bush_berry_v2.png"))
    print(f"saved bush_berry_v2.png  64x32")

    grid = build_grid_preview(sprites)
    grid.save(os.path.join(OUT, "_preview_flora32_grid.png"))
    print(f"saved _preview_flora32_grid.png  {grid.width}x{grid.height}")

    grass = build_grass_preview(sprites)
    grass.save(os.path.join(OUT, "_preview_flora32_grass.png"))
    print(f"saved _preview_flora32_grass.png  {grass.width}x{grass.height}")


if __name__ == "__main__":
    main()
