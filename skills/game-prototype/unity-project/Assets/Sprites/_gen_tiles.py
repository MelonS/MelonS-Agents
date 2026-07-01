# -*- coding: utf-8 -*-
"""A3 + Polish-Grass — muted cohesive terrain tiles + 잔디 variant 3종.

Generates 16x16 RGBA PNGs for the 4 terrain tiles used by
SceneSetup.Game.Terrain.cs via LoadOrCreateTile.  All colours
come exclusively from palette.py (the master palette established
in A1).

Design rules (from design-improvement-backlog.md A3 + north-star):
- Terrain is LOW-saturation -- must recede behind pawns/buildings.
- No outline on terrain tiles (background, not story-relevant objects).
- Subtle per-pixel speckle within each tile's ramp for visual interest
  without creating readable noise that fights the map read.
- Seamless tiling: edges stay base colour so repeated tiles blend.
- Grass uses GRASS_DK/MD/LT ramp (muted olive-khaki), NOT neon green.
- Dirt uses DIRT_DK/MD/LT ramp, Water WATER_DK/MD/LT, Rock ROCK_DK/MD/LT.

폴리싱 추가 (2026-06-01): 잔디 반복감 개선.
  tile_grass.png   — 원본 자리, asymmetric micro-zone 패턴으로 교체.
                      SceneSetup 이 이미 4방향 랜덤 rotation 을 적용하므로,
                      내부에 비대칭 구조가 있으면 rotation 4가지가 서로 다르게 보임.
  tile_grass_b.png — 밝은 풀 variant (GRASS_LT 중심).
  tile_grass_c.png — 어두운/습한 풀 + 흙점 variant (GRASS_DK + DIRT_DK 반점).

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


# -- grass tile generators ------------------------------------------------
#
# 핵심 원칙: 타일은 16x16 이고 SceneSetup 이 각 셀마다 0/90/180/270 도 랜덤 rotation 을
# 적용한다(ApplyRandomTileTransform).  따라서 내부 패턴이 비대칭(asymmetric)일수록
# rotation 4가지가 서로 다른 면을 보여주어 반복감이 줄어든다.
#
# tile_grass (A variant): GRASS_MD 기반, 비대칭 zone 3개
#   - 좌상단 quadrant: GRASS_LT 덩어리 (밝은 풀 뭉치)
#   - 우하단 quadrant: GRASS_DK 덩어리 (그늘진 풀)
#   - 중앙~기타: GRASS_MD + 개별 speckle
#   테두리 1px 는 GRASS_MD 고정 (seamless).
#
# tile_grass_b (B variant): GRASS_LT 기반 — 밝고 열린 풀밭.
#   밝은 잔디 위주, 드문 GRASS_MD 점, 아주 드문 DIRT_LT 흙점 (햇빛 받은 건조 지면).
#
# tile_grass_c (C variant): GRASS_DK 기반 — 어둡고 촉촉한 풀밭.
#   어두운 잔디 위주, DIRT_DK 흙점 다수 (습한 흙/그늘 지면), GRASS_MD 하이라이트 점.

def gen_grass(size=16):
    """
    tile_grass.png (variant A) — GRASS_MD 기반 비대칭 micro-zone 패턴.

    내부를 4개 zone 으로 나누어 각기 다른 색 bias 를 준 뒤 edge 픽셀을 GRASS_MD 로
    덮어써서 seamless tiling 을 보장한다.  4방향 rotation 에서 각각 다른 zone 이
    코너에 오므로 반복감이 최소화된다.
    """
    rng = random.Random(7001)
    img = Image.new("RGBA", (size, size), GRASS_MD)
    pixels = img.load()

    # -- zone 정의: 각 내부 픽셀의 좌표로 zone 결정 (1-indexed quadrant 방식)
    # zone A (좌상): x < size//2, y < size//2  → GRASS_LT bias
    # zone B (우상): x >= size//2, y < size//2 → GRASS_MD (baseline)
    # zone C (좌하): x < size//2, y >= size//2 → GRASS_MD + DIRT_DK 미세 흙점
    # zone D (우하): x >= size//2, y >= size//2 → GRASS_DK bias
    half = size // 2

    for y in range(1, size - 1):      # 테두리 제외
        for x in range(1, size - 1):
            r = rng.random()
            in_top = y < half
            in_left = x < half

            # TOP-1 (visual-polish-backlog 2026-06-11): 디테일 픽셀 밀도를 절반 이하로
            #  — per-pixel 노이즈가 줌아웃에서 "정전기 화면" 으로 읽히던 것을, MD 가
            #  지배하는 면 + 드문 액센트로 (the reference sim 지면이 조용한 이유).
            if in_left and in_top:
                # zone A: 살짝 밝은 풀 (LT 액센트)
                if r < 0.22:
                    pixels[x, y] = GRASS_LT
                elif r < 0.90:
                    pixels[x, y] = GRASS_MD
                else:
                    pixels[x, y] = GRASS_DK
            elif not in_left and not in_top:
                # zone D: 살짝 그늘진 풀 (DK 액센트)
                if r < 0.22:
                    pixels[x, y] = GRASS_DK
                elif r < 0.90:
                    pixels[x, y] = GRASS_MD
                else:
                    pixels[x, y] = GRASS_LT
            elif in_left and not in_top:
                # zone C: GRASS_MD + 드문 흙점
                if r < 0.03:
                    pixels[x, y] = DIRT_DK    # 흙점: 작은 맨땅
                elif r < 0.13:
                    pixels[x, y] = GRASS_DK
                elif r < 0.24:
                    pixels[x, y] = GRASS_LT
                else:
                    pixels[x, y] = GRASS_MD
            else:
                # zone B (우상): 중성 baseline
                if r < 0.07:
                    pixels[x, y] = GRASS_DK
                elif r < 0.13:
                    pixels[x, y] = GRASS_LT
                else:
                    pixels[x, y] = GRASS_MD

    # seamless: 테두리 1px 를 GRASS_MD 로 덮어씀
    for x in range(size):
        pixels[x, 0] = GRASS_MD
        pixels[x, size - 1] = GRASS_MD
    for y in range(size):
        pixels[0, y] = GRASS_MD
        pixels[size - 1, y] = GRASS_MD

    return img


def gen_grass_b(size=16):
    """
    tile_grass_b.png (variant B) — GRASS_LT 중심 밝은 풀밭.

    햇빛을 많이 받아 밝고 건조한 지면.  드문드문 DIRT_LT 흙점이 섞여 건조감을 준다.
    내부 zone 분할: 좌상 = 가장 밝음(GRASS_LT 뭉치), 우하 = GRASS_MD 로 어두움.
    seamless edge: GRASS_MD.
    """
    # TOP-1: 변형 간 평균 명도차 압축 — 기존 LT 통짜 베이스는 A/C 와 체커보드를
    #  만들었다.  베이스를 MD 로 통일하고 밝음은 zone 액센트로만.
    rng = random.Random(7002)
    img = Image.new("RGBA", (size, size), GRASS_MD)
    pixels = img.load()

    half = size // 2
    for y in range(1, size - 1):
        for x in range(1, size - 1):
            r = rng.random()
            in_bright = (x < half + 2) and (y < half + 2)  # 살짝 비대칭

            if in_bright:
                if r < 0.02:
                    pixels[x, y] = DIRT_LT    # 건조 흙점 (밝은 색)
                elif r < 0.40:
                    pixels[x, y] = GRASS_LT
                else:
                    pixels[x, y] = GRASS_MD
            else:
                if r < 0.02:
                    pixels[x, y] = DIRT_MD    # 좀 더 어두운 흙점
                elif r < 0.08:
                    pixels[x, y] = GRASS_DK
                elif r < 0.20:
                    pixels[x, y] = GRASS_LT
                else:
                    pixels[x, y] = GRASS_MD

    # seamless edge
    for x in range(size):
        pixels[x, 0] = GRASS_MD
        pixels[x, size - 1] = GRASS_MD
    for y in range(size):
        pixels[0, y] = GRASS_MD
        pixels[size - 1, y] = GRASS_MD

    return img


def gen_grass_c(size=16):
    """
    tile_grass_c.png (variant C) — GRASS_DK 중심 어둡고 습한 풀밭.

    그늘지거나 수분이 많은 지면.  DIRT_DK 흙점이 잦아 젖은 흙 느낌.
    우상 quadrant 에 GRASS_MD 하이라이트 cluster — 비대칭 보장.
    seamless edge: GRASS_MD.
    """
    # TOP-1: 변형 간 평균 명도차 압축 — DK 통짜 베이스 → MD 베이스 + DK zone 액센트.
    rng = random.Random(7003)
    img = Image.new("RGBA", (size, size), GRASS_MD)
    pixels = img.load()

    half = size // 2
    for y in range(1, size - 1):
        for x in range(1, size - 1):
            r = rng.random()
            in_highlight = (x >= half - 1) and (y < half + 1)  # 우상 하이라이트 zone

            if in_highlight:
                if r < 0.12:
                    pixels[x, y] = GRASS_LT
                elif r < 0.88:
                    pixels[x, y] = GRASS_MD
                else:
                    pixels[x, y] = GRASS_DK
            else:
                if r < 0.05:
                    pixels[x, y] = DIRT_DK    # 젖은 흙 반점 (어두운 갈색)
                elif r < 0.40:
                    pixels[x, y] = GRASS_DK
                else:
                    pixels[x, y] = GRASS_MD

    # seamless edge
    for x in range(size):
        pixels[x, 0] = GRASS_MD
        pixels[x, size - 1] = GRASS_MD
    for y in range(size):
        pixels[0, y] = GRASS_MD
        pixels[size - 1, y] = GRASS_MD

    return img


def gen_dirt(size=16):
    """Warm muted brown dirt / tilled soil patches.  (TOP-1: 노이즈 밀도 절반)"""
    return _speckle_tile(
        size, DIRT_MD, DIRT_DK, DIRT_LT,
        dark_prob=0.05, light_prob=0.03, seed=2002,
    )


def gen_water(size=16):
    """Desaturated blue-grey water, calm depth-band pattern."""
    return _water_tile(size, seed=3003)


def gen_rock(size=16):
    """Warm grey rock - subtle texture, recedes from pawns.  (TOP-1: 웜그레이 + 밀도 절반)"""
    return _speckle_tile(
        size, ROCK_MD, ROCK_DK, ROCK_LT,
        dark_prob=0.05, light_prob=0.03, seed=4004,
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
      Row 0: 5 tiles (grass_a / grass_b / grass_c / dirt / water / rock)
      Row 1-3: 3x4 grass_a 연속 배치 (seamless tiling 확인)
      Row 4-6: grass_a 4가지 rotation 시뮬레이션 (0/90/180/270 도)
    Scaled up 4x (nearest-neighbour) for readability.
    """
    scale = 4

    grass_a = gen_grass(tile_size)
    grass_b = gen_grass_b(tile_size)
    grass_c = gen_grass_c(tile_size)
    dirt    = gen_dirt(tile_size)
    water   = gen_water(tile_size)
    rock    = gen_rock(tile_size)
    top_row = [grass_a, grass_b, grass_c, dirt, water, rock]

    col_count = len(top_row)
    canvas_w = tile_size * col_count
    canvas_h = tile_size * 7  # 1 header + 3 seamless + 3 rotation rows
    canvas = Image.new("RGBA", (canvas_w, canvas_h), (30, 25, 20, 255))

    # Row 0: 각 tile 종류
    for i, t in enumerate(top_row):
        canvas.paste(t, (i * tile_size, 0))

    # Rows 1-3: 4x3 grass_a 반복 (seamless 확인)
    for gy in range(3):
        for gx in range(4):
            canvas.paste(grass_a, (gx * tile_size, (gy + 1) * tile_size))

    # Rows 4-6: 4가지 rotation 시뮬레이션
    #  (SceneSetup.ApplyRandomTileTransform 가 하는 것을 여기서 프리뷰)
    rotations = [0, 90, 180, 270]
    for ri, angle in enumerate(rotations):
        rotated = grass_a.rotate(angle)
        canvas.paste(rotated, (ri * tile_size, 4 * tile_size))

    # Row 5: grass_b rotation 4종
    for ri, angle in enumerate(rotations):
        rotated = grass_b.rotate(angle)
        canvas.paste(rotated, (ri * tile_size, 5 * tile_size))

    # Row 6: grass_c rotation 4종
    for ri, angle in enumerate(rotations):
        rotated = grass_c.rotate(angle)
        canvas.paste(rotated, (ri * tile_size, 6 * tile_size))

    big = canvas.resize((canvas_w * scale, canvas_h * scale), Image.NEAREST)
    preview_path = os.path.join(OUT_DIR, "_preview_tiles.png")
    big.save(preview_path)
    print("  preview saved {}  ({}x{})".format(preview_path, big.size[0], big.size[1]))


# -- main -----------------------------------------------------------------

def main():
    print("=== _gen_tiles.py - terrain tile regen (잔디 variant 3종) ===")
    size = 16  # must match existing tile size exactly

    print("\n[1/3] Grass tile A (tile_grass.png) — asymmetric micro-zone")
    _save(gen_grass(size), "tile_grass.png")

    print("[2/3] Grass tile B (tile_grass_b.png) — bright/dry variant")
    _save(gen_grass_b(size), "tile_grass_b.png")

    print("[3/3] Grass tile C (tile_grass_c.png) — dark/wet variant")
    _save(gen_grass_c(size), "tile_grass_c.png")

    print("\n[4/4] Dirt tile")
    _save(gen_dirt(size), "tile_dirt.png")

    print("[5/5] Water tile")
    _save(gen_water(size), "tile_water.png")

    print("[6/6] Rock tile")
    _save(gen_rock(size), "tile_rock.png")

    print("\n[Bonus] Door wood remap")
    remap_door_wood()

    print("\n[Preview] Composite (tile variants + rotation 시뮬레이션)")
    gen_preview(size)

    print("\nDone.")
    print("tile_grass.png: asymmetric zone 패턴 - 4방향 rotation 마다 다른 면 노출.")
    print("tile_grass_b.png / tile_grass_c.png: 밝은/어두운 variant (향후 tilemap 확장용).")
    print("LoadOrCreateTile + ForceImportAllSprites in SceneSetup will")
    print("auto-reimport these when scene-regen runs (no .meta changes needed).")


if __name__ == "__main__":
    main()
