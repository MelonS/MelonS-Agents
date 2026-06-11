# -*- coding: utf-8 -*-
"""_gen_tiles32.py — Art v2 terrain tiles 32px (staging).

Spec: G:/ai/_artv2_staging/artv2-style-spec.md  §5 지형 타일 32×32
Palette: import from Assets/Sprites/palette.py (single source of truth).

Rules implemented:
- interior detail density ~10% (30x30 interior, ~75-90 detail px)
- variant mean luminance within +-2 of pure-MD tile (placement variation, not value variation)
- 1px border ring = pure base MD (seamless), detail never touches border
- no dithering, no single-dot noise — organic 2-3px clusters only
- water: 2 frames, LT horizontal ripple segments (phase-shifted), not dither
"""
import os
import sys
import random

sys.stdout.reconfigure(encoding="utf-8")

PALETTE_DIR = r"G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Sprites"
sys.path.insert(0, PALETTE_DIR)

from PIL import Image  # noqa: E402
import palette as P    # noqa: E402

OUT = r"G:/ai/_artv2_staging/out"
os.makedirs(OUT, exist_ok=True)

SEED = 20260611
T = 32          # tile size
LO, HI = 1, 30  # interior pixel range (border = 0 and 31, pure MD)
SCALE = 8


# ── helpers ─────────────────────────────────────────────────────────

def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def mean_lum(img):
    px = img.load()
    s = 0.0
    for y in range(img.height):
        for x in range(img.width):
            s += lum(px[x, y])
    return s / (img.width * img.height)


class Tile:
    """32x32 tile: MD-filled, detail only inside [1..30], spacing-aware."""

    def __init__(self, base_md):
        self.img = Image.new("RGBA", (T, T), base_md)
        self.px = self.img.load()
        self.detail = set()

    def can_place(self, cells, gap=1):
        """All cells inside interior and no existing detail within `gap`
        (Chebyshev) — keeps clusters separated so the tile stays calm."""
        for (x, y) in cells:
            if not (LO <= x <= HI and LO <= y <= HI):
                return False
        for (x, y) in cells:
            for dy in range(-gap, gap + 1):
                for dx in range(-gap, gap + 1):
                    if (x + dx, y + dy) in self.detail:
                        return False
        return True

    def place(self, cells_colors):
        for (x, y, c) in cells_colors:
            self.px[x, y] = c
            self.detail.add((x, y))

    def n_detail(self):
        return len(self.detail)


def zone_point(rng, zones):
    """zones: list of (cx, cy, sigma, weight) — asymmetric cluster sampling."""
    ws = [z[3] for z in zones]
    cx, cy, sg, _ = rng.choices(zones, weights=ws, k=1)[0]
    x = int(round(rng.gauss(cx, sg)))
    y = int(round(rng.gauss(cy, sg)))
    return x, y


def scatter(tile, rng, zones, shapes, budget_lo, budget_hi):
    """Place organic 2-3px cluster shapes from `shapes`=[(cells, weight)]
    sampled inside asymmetric zones until detail budget reached."""
    ws = [s[1] for s in shapes]
    attempts = 0
    while tile.n_detail() < budget_lo and attempts < 6000:
        attempts += 1
        cells, _ = rng.choices(shapes, weights=ws, k=1)[0]
        ax, ay = zone_point(rng, zones)
        abs_cells = [(ax + dx, ay + dy) for (dx, dy, _c) in cells]
        if tile.n_detail() + len(abs_cells) > budget_hi:
            continue
        if not tile.can_place(abs_cells):
            continue
        tile.place([(ax + dx, ay + dy, c) for (dx, dy, c) in cells])


# ── grass a/b/c ─────────────────────────────────────────────────────

def grass_shapes(rng_unused):
    DK, LT = P.GRASS_DK, P.GRASS_LT
    blade2 = [(0, 0, DK), (0, -1, DK)]                       # short blade
    blade3r = [(0, 0, DK), (0, -1, DK), (1, -2, DK)]          # leaning right
    blade3l = [(0, 0, DK), (0, -1, DK), (-1, -2, DK)]         # leaning left
    clump = [(0, 0, DK), (1, 0, DK), (1, -1, DK)]             # low tuft
    tuft_lit = [(0, 0, DK), (1, 0, DK), (1, -1, LT)]          # tip catches LT
    dapple = [(0, 0, LT), (1, 0, LT)]                         # sun dapple 2px
    return blade2, blade3r, blade3l, clump, tuft_lit, dapple


def make_grass(variant):
    rng = random.Random(SEED + {"a": 1, "b": 2, "c": 3, "d": 4}[variant])
    t = Tile(P.GRASS_MD)
    b2, b3r, b3l, cl, tl, dp = grass_shapes(rng)
    if variant == "a":   # blade-heavy, two diagonal zones
        zones = [(9, 11, 4.5, 0.55), (23, 22, 5.0, 0.45)]
        shapes = [(b2, 30), (b3r, 18), (b3l, 14), (cl, 14), (tl, 10), (dp, 9)]
    elif variant == "b":  # clumpier, three small pockets
        zones = [(21, 7, 4.0, 0.40), (8, 24, 4.5, 0.40), (27, 17, 3.0, 0.20)]
        shapes = [(b2, 20), (b3r, 12), (b3l, 12), (cl, 26), (tl, 12), (dp, 8)]
    elif variant == "c":  # airier, more dapple light
        zones = [(13, 18, 5.5, 0.60), (25, 8, 3.5, 0.40)]
        shapes = [(b2, 24), (b3r, 12), (b3l, 12), (cl, 12), (tl, 12), (dp, 16)]
    else:                 # d: edge-leaning bands, lean-left bias (spec: grass x4)
        zones = [(6, 17, 4.0, 0.35), (26, 12, 4.0, 0.35), (17, 26, 4.0, 0.30)]
        shapes = [(b2, 26), (b3r, 10), (b3l, 18), (cl, 16), (tl, 10), (dp, 10)]
    scatter(t, rng, zones, shapes, budget_lo=76, budget_hi=90)
    return t.img


# ── dirt ────────────────────────────────────────────────────────────

def make_dirt(variant="a"):
    rng = random.Random(SEED + {"a": 10, "b": 11}[variant])
    t = Tile(P.DIRT_MD)
    DK, LT = P.DIRT_DK, P.DIRT_LT
    clod2 = [(0, 0, DK), (1, 0, DK)]                          # small clod
    clod3 = [(0, 0, DK), (1, 0, DK), (1, 1, DK)]              # bigger clod
    clod_lit = [(0, 0, LT), (1, 0, DK), (1, 1, DK)]           # lit upper-left
    crack = [(0, 0, DK), (1, 1, DK), (2, 1, DK)]              # short crack
    streak = [(0, 0, LT), (1, 0, LT)]                         # dry light streak
    if variant == "a":
        zones = [(10, 9, 4.5, 0.40), (23, 20, 5.0, 0.40), (7, 25, 3.5, 0.20)]
        shapes = [(clod2, 26), (clod3, 18), (clod_lit, 14), (crack, 14), (streak, 10)]
    else:  # b: crackier, pockets shifted to the other diagonal (spec: dirt x2)
        zones = [(22, 8, 4.5, 0.40), (9, 21, 5.0, 0.40), (25, 26, 3.5, 0.20)]
        shapes = [(clod2, 20), (clod3, 14), (clod_lit, 12), (crack, 24), (streak, 12)]
    scatter(t, rng, zones, shapes, budget_lo=74, budget_hi=88)
    return t.img


# ── rock (warm grey) ────────────────────────────────────────────────

def make_rock(variant="a"):
    rng = random.Random(SEED + {"a": 20, "b": 21}[variant])
    t = Tile(P.ROCK_MD)
    DK, LT = P.ROCK_DK, P.ROCK_LT
    # crack polylines: mostly horizontal random walks, 4-7 px
    n_cracks = 5 if variant == "a" else 6  # b: one more, shorter cracks
    placed_cracks = 0
    attempts = 0
    while placed_cracks < n_cracks and attempts < 400:
        attempts += 1
        length = rng.randint(4, 7) if variant == "a" else rng.randint(3, 5)
        x = rng.randint(LO + 1, HI - length - 1)
        y = rng.randint(LO + 2, HI - 2)
        cells = []
        cx, cy = x, y
        for _ in range(length):
            cells.append((cx, cy))
            cx += 1
            cy += rng.choice([0, 0, 0, 1, -1])
        if not t.can_place(cells, gap=2):
            continue
        t.place([(px_, py_, DK) for (px_, py_) in cells])
        # lit facet edge just above the first half of the crack (light = top-left)
        lit = [(px_, py_ - 1) for (px_, py_) in cells[: length // 2]]
        lit = [c for c in lit if LO <= c[0] <= HI and LO <= c[1] <= HI
               and c not in t.detail]
        if len(lit) >= 2:
            t.place([(px_, py_, LT) for (px_, py_) in lit[:3]])
        placed_cracks += 1
    # a few small chip clusters to fill the budget organically
    chip = [(0, 0, DK), (1, 0, DK)]
    fleck = [(0, 0, LT), (0, 1, LT)]
    if variant == "a":
        zones = [(8, 22, 5.0, 0.5), (24, 8, 5.0, 0.5)]
    else:
        zones = [(22, 24, 5.0, 0.5), (10, 8, 5.0, 0.5)]
    scatter(t, rng, zones, [(chip, 3), (fleck, 2)], budget_lo=72, budget_hi=86)
    return t.img


# ── water (2-frame ripple) ──────────────────────────────────────────

def water_ripple_plan():
    """Deterministic ripple segments: (y_base, [(x0, len, dy)...], drift).

    Uneven row spacing + per-segment y jitter so it reads as scattered
    wavelets, not ruled lines."""
    rng = random.Random(SEED + 30)
    rows = []
    for i, y in enumerate([4, 9, 15, 21, 26]):
        segs = []
        x = rng.randint(1, 6)
        n_max = rng.choice([2, 2, 3])  # some rows sparser than others
        while x < 26 and len(segs) < n_max:
            ln = rng.randint(3, 6)
            dy = rng.choice([-1, 0, 0, 1])
            segs.append((x, ln, dy))
            x += ln + rng.randint(5, 9)
        rows.append((y, segs, 1 if i % 2 == 0 else -1))
    return rows


def make_water(frame):
    t = Tile(P.WATER_MD)
    LT, DK = P.WATER_LT, P.WATER_DK
    W = HI - LO + 1  # 30: interior wrap width keeps the loop seamless
    for (yb, segs, ddir) in water_ripple_plan():
        phase = frame * 2 * ddir
        for si, (x0, ln, dy) in enumerate(segs):
            y = max(LO, min(HI - 1, yb + dy))
            # frame 1: alternate segments stretch/shrink by 1px = shimmer
            ln2 = ln + (1 if (frame == 1 and si % 2 == 0) else 0)
            for k in range(ln2):
                x = LO + ((x0 - LO + phase + k) % W)
                t.px[x, y] = LT
                t.detail.add((x, y))
            # short DK underline at segment tail = depth (also drifts)
            if si % 2 == 0:
                for k in range(2):
                    x = LO + ((x0 - LO + phase + ln2 - 1 + k) % W)
                    if (x, y + 1) not in t.detail and y + 1 <= HI:
                        t.px[x, y + 1] = DK
                        t.detail.add((x, y + 1))
    return t.img


# ── previews ────────────────────────────────────────────────────────

def save(img, name):
    path = os.path.join(OUT, name)
    img.save(path)
    return path


def preview(img, name):
    big = img.resize((img.width * SCALE, img.height * SCALE), Image.NEAREST)
    return save(big, name)


def sheet(images):
    s = Image.new("RGBA", (T * len(images), T), (0, 0, 0, 0))
    for i, im in enumerate(images):
        s.paste(im, (i * T, 0))
    return s


GRASS_MIX_W = [55, 20, 15, 10]  # a/b/c/d


def grass_mix_6x6(grass):
    """Weighted a/b/c/d mix — the calmness/tiling acid test."""
    rng = random.Random(SEED + 40)
    g = Image.new("RGBA", (T * 6, T * 6))
    for ty in range(6):
        for tx in range(6):
            v = rng.choices("abcd", weights=GRASS_MIX_W, k=1)[0]
            g.paste(grass[v], (tx * T, ty * T))
    return g


def scene_6x6(grass, dirt, rock, water0):
    """Mixed-terrain composite on the grass field — contrast check."""
    rng = random.Random(SEED + 41)
    layout = [
        "ggggww",
        "gddgww",
        "ggddgg",
        "rggdgg",
        "rrggdg",
        "ggggdg",
    ]
    g = Image.new("RGBA", (T * 6, T * 6))
    for ty, row in enumerate(layout):
        for tx, ch in enumerate(row):
            if ch == "g":
                v = rng.choices("abcd", weights=GRASS_MIX_W, k=1)[0]
                im = grass[v]
            elif ch == "d":
                im = dirt["a" if (tx + ty) % 2 == 0 else "b"]
            elif ch == "r":
                im = rock["a" if (tx + ty) % 2 == 0 else "b"]
            else:
                im = water0
            g.paste(im, (tx * T, ty * T))
    return g


# ── main ────────────────────────────────────────────────────────────

def main():
    grass = {v: make_grass(v) for v in "abcd"}
    dirt = {v: make_dirt(v) for v in "ab"}
    rock = {v: make_rock(v) for v in "ab"}
    water0 = make_water(0)
    water1 = make_water(1)

    checks = ([(f"grass_{v}", grass[v], P.GRASS_MD) for v in "abcd"]
              + [(f"dirt_{v}", dirt[v], P.DIRT_MD) for v in "ab"]
              + [(f"rock_{v}", rock[v], P.ROCK_MD) for v in "ab"]
              + [("water_0", water0, P.WATER_MD),
                 ("water_1", water1, P.WATER_MD)])

    # luminance report + spec assertion (variant mean within +-2 of MD)
    print(f"GRASS_MD luminance baseline: {lum(P.GRASS_MD):.2f}")
    for name, im, md in checks:
        d = mean_lum(im) - lum(md)
        dens = sum(1 for y in range(T) for x in range(T)
                   if im.load()[x, y] != md) / 900 * 100
        print(f"  {name}: delta={d:+.2f} density={dens:.1f}%")
        assert abs(d) <= 2.0, f"{name} luminance delta {d:+.2f} > 2"
    dw = mean_lum(water0) - mean_lum(water1)
    print(f"  water frame delta: {dw:+.2f} (frames should match closely)")

    # no-single-dot-noise check: every detail px must have a detail neighbour
    for name, im, md in checks:
        p = im.load()
        det = {(x, y) for y in range(T) for x in range(T) if p[x, y] != md}
        lone = [c for c in det
                if not any((c[0] + dx, c[1] + dy) in det
                           for dy in (-1, 0, 1) for dx in (-1, 0, 1)
                           if (dx, dy) != (0, 0))]
        assert not lone, f"{name} isolated dot noise at {lone}"
    print("dot-noise check: no isolated pixels — all detail is 2px+ clusters")

    # border purity check (seamless ring)
    for name, im, md in checks:
        p = im.load()
        for i in range(T):
            for (x, y) in ((i, 0), (i, 31), (0, i), (31, i)):
                assert p[x, y] == md, f"{name} border broken at {(x, y)}"
    print("border ring: all pure MD — seamless OK")

    # sheets (spec §5: grass x4, dirt x2, rock x2, water x2)
    save(sheet([grass[v] for v in "abcd"]), "tiles_grass_v2.png")
    save(sheet([dirt["a"], dirt["b"]]), "tiles_dirt_v2.png")
    save(sheet([rock["a"], rock["b"]]), "tiles_rock_v2.png")
    save(sheet([water0, water1]), "tiles_water_v2.png")

    # previews (8x)
    paths = [
        preview(sheet([grass[v] for v in "abcd"]), "_preview_tiles_grass.png"),
        preview(sheet([dirt["a"], dirt["b"], rock["a"], rock["b"]]),
                "_preview_tiles_dirt_rock.png"),
        preview(sheet([water0, water1]), "_preview_tiles_water.png"),
        preview(grass_mix_6x6(grass), "_preview_grass_mix6x6.png"),
        preview(scene_6x6(grass, dirt, rock, water0), "_preview_scene6x6.png"),
    ]
    for p in paths:
        print("preview:", p)
    print("done.")


if __name__ == "__main__":
    main()
