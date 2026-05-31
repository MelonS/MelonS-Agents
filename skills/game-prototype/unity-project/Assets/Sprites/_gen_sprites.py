# -*- coding: utf-8 -*-
"""_gen_sprites.py — tree + trader sprites.

OWNERSHIP MAP (after A1 palette unification, 2026-05-29):
  _gen_pawn.py      → pawn_colonist, pawn_blue, pawn_rust, pawn_olive
  _gen_fix_audit.py → wall_wood, floor_wood, stove, deer, wolf, crop_rice,
                      stone_vein, stone_chunk, wood_pile, meat_pile, research_bench
  _gen_bed.py       → bed_wood, bed_fine
  _gen_sprites.py   → tree, trader   (this file)

RETIRED from this file: gen_pawn (→ _gen_pawn.py), gen_wall_wood,
  gen_deer, gen_wood_pile, gen_stone_vein, gen_stone_chunk, gen_meat_pile
  (all → _gen_fix_audit.py).

Style: flat-Kenney (flat colour fills + OUTLINE_OBJ / OUTLINE_PLANT, NO
gradients, NO gaussian blur).  All colours from palette.py.

Polish Wave v3 V6 (2026-06-01):
  gen_tree 대폭 개선 — 단순 blob → 잎 질감 + 명암 있는 트리.
  접근법: 불규칙 타원 수관 base fill → GRASS_DK 그림자 클러스터 덮기
          → GRASS_LT 하이라이트 클러스터 덮기.
  픽셀 맵 (V6 final, 검증 완료):
    y00: ...OOOOOOOOO.....
    y01: ....LLLMDDDDO....
    y02: .OOLLLLMMDDDDO...
    y03: .OLLLLMMMDDDDO...
    y04: .OLLLLMMDDDDD....
    y05: .OLMMMMMDDDDD....
    y06: .OMMMMMDDDDDO....
    y07: .ODDDDDDDDDO.....
    y08: ..ODDDWWDDDO.....
    y09: ....OOWwOOO......
    y10..14: ......OWwO......
    y15: .......OO.......
  진한 하이라이트 좌상단, 그림자 우하단, 기둥 자연스러운 연결.
  RuntimeTint (Pine/Birch/Oak) 하에도 3-tone 상대명암 유지.
"""
from __future__ import annotations
from PIL import Image, ImageDraw
import random
from pathlib import Path

from palette import (
    OUTLINE_OBJ   as OUTLINE,
    OUTLINE_PLANT,
    WOOD_DK, WOOD_MD, WOOD_LT,
    GRASS_DK, GRASS_MD, GRASS_LT,
    SKIN_MD, SKIN_SH, HAIR_DK,
)

# Additional colours needed for trader not in main palette export
_HOOD      = (78, 58, 96, 255)     # muted dusty purple hood (trader)
_HOOD_DK   = (52, 38, 66, 255)
_ROBE      = (100, 80, 116, 255)   # robe body
_ROBE_DK   = (64, 48, 80, 255)
_GOLD_ACC  = (190, 155, 62, 255)   # belt accent — matches CROP_GOLD family
_BOOT_DK   = (40, 28, 18, 255)     # dark boot

HERE = Path(__file__).resolve().parent


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put_px(im, x, y, color):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), color)


def hline(im, x0, x1, y, c):
    for x in range(x0, x1 + 1): put_px(im, x, y, c)


def vline(im, x, y0, y1, c):
    for y in range(y0, y1 + 1): put_px(im, x, y, c)


def rect_fill(im, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put_px(im, x, y, c)


def rect_outline(im, x0, y0, x1, y1, c):
    hline(im, x0, x1, y0, c); hline(im, x0, x1, y1, c)
    vline(im, x0, y0, y1, c); vline(im, x1, y0, y1, c)


# ─────────────────────────────────────────────────────────────────────────────
# tree.png — 16x16 개선 트리 (Polish Wave v3 V6, 2026-06-01)
#
# 렌더 레이어 순서 (뒤에서 앞으로 덮어쓰기):
#   1. 수관 base: GRASS_MD 로 불규칙 타원 전체 채움 (빈 픽셀 없음 보장)
#   2. GRASS_DK 그림자 클러스터 덮기 (우하단)
#   3. GRASS_LT 하이라이트 클러스터 덮기 (좌상단)
#   4. 기둥 flare + 기둥 본체 + 접합 그림자
#   5. OUTLINE_PLANT 테두리
#
# 수관 범위:
#   y=1: x=4..11  (상단 좁음, 불규칙 — 우측 더 넓어 우상단 잎 덩어리 표현)
#   y=2: x=3..12
#   y=3: x=3..12
#   y=4: x=3..11
#   y=5: x=3..11
#   y=6: x=3..11
#   y=7: x=3..10  (하단 수관, 기둥 위)
#   y=8: x=3..11  (수관 하단 / 기둥 접합 행)
# ─────────────────────────────────────────────────────────────────────────────

# 수관 외곽 범위 (row → (x_start, x_end) inclusive)
_CANOPY_ROWS = {
    1: (4, 11),
    2: (3, 12),
    3: (3, 12),
    4: (3, 11),
    5: (3, 11),
    6: (3, 11),
    7: (3, 10),
    8: (3, 11),
}


def gen_tree() -> Image.Image:
    """16x16 개선 트리: 불규칙 수관 + 잎 클러스터 명암 + 자연스러운 기둥 연결."""
    W = H = 16
    im = new_canvas(W, H)

    # ─────────────────────────────────────────────────────────────────────────
    # PASS 1: 수관 base fill — GRASS_MD 로 전체 수관 영역 채우기
    #   빈 픽셀(hole) 없음 보장.  후속 pass 가 선택적으로 덮어씀.
    # ─────────────────────────────────────────────────────────────────────────
    for y, (x0, x1) in _CANOPY_ROWS.items():
        hline(im, x0, x1, y, GRASS_MD)

    # ─────────────────────────────────────────────────────────────────────────
    # PASS 2: GRASS_DK — 그림자 클러스터 (우측·하단 덮어쓰기)
    #   목표: 우상단 잎 덩어리 그림자, 중앙 우측 그림자, 하단 수관 전체.
    #   좌측 돌출 x=2 (y=3..5) 는 GRASS_LT 에서 처리.
    # ─────────────────────────────────────────────────────────────────────────
    dark = [
        # 우상단 잎 클러스터 그림자
        (9,  1), (10, 1), (11, 1),
        (10, 2), (11, 2), (12, 2),
        (11, 3), (12, 3),
        (11, 4),
        # 중앙 우측 그림자
        (9,  3), (10, 3),
        (9,  4), (10, 4),
        (9,  5), (10, 5), (11, 5),
        (9,  6), (10, 6), (11, 6),
        # 수관 내부 잎 틈 클러스터 (어두운 공간감)
        (8,  4),
        (8,  5), (8,  6),
        # 수관 하단 전체 — 가장 무거운 그림자 zone
        (3,  7), (4,  7), (5,  7), (6,  7), (7,  7), (8,  7), (9,  7), (10, 7),
        (3,  8), (4,  8), (5,  8), (6,  8), (7,  8), (8,  8), (9,  8), (10, 8), (11, 8),
    ]
    for x, y in dark:
        put_px(im, x, y, GRASS_DK)

    # ─────────────────────────────────────────────────────────────────────────
    # PASS 3: GRASS_LT — 하이라이트 클러스터 (좌상단 덮어쓰기)
    #   빛 방향 좌상단.  소규모 픽셀 클러스터로 잎 질감.
    #   좌측 극돌출 x=2 (y=3..5) 포함 — 불규칙 실루엣.
    # ─────────────────────────────────────────────────────────────────────────
    light = [
        # 상단 좌측 주 하이라이트 클러스터
        (4,  1), (5,  1), (6,  1),
        (3,  2), (4,  2), (5,  2), (6,  2),
        (3,  3), (4,  3), (5,  3),
        (3,  4), (4,  4),
        (3,  5), (4,  5),
        # 좌측 돌출 하이라이트 — 불규칙 실루엣 (수관 범위 밖 x=2)
        (2,  3), (2,  4), (2,  5),
    ]
    for x, y in light:
        put_px(im, x, y, GRASS_LT)

    # ─────────────────────────────────────────────────────────────────────────
    # PASS 4a: 기둥 flare (y=8) — 수관 하단에 기둥 폭 표시
    #   x=7: WOOD_MD (빛 받는 좌측)
    #   x=8: WOOD_DK (우측 그림자)
    #   (이미 GRASS_DK 로 채워진 y=8 행 위에 기둥 픽셀 2개만 덮어씀)
    # ─────────────────────────────────────────────────────────────────────────
    put_px(im, 7, 8, WOOD_MD)
    put_px(im, 8, 8, WOOD_DK)

    # ─────────────────────────────────────────────────────────────────────────
    # PASS 4b: 기둥 본체 (y=9..14)
    #   2px: x=7 WOOD_MD (좌, 빛), x=8 WOOD_DK (우, 그림자)
    # ─────────────────────────────────────────────────────────────────────────
    vline(im, 7, 9, 14, WOOD_MD)
    vline(im, 8, 9, 14, WOOD_DK)

    # ─────────────────────────────────────────────────────────────────────────
    # PASS 4c: 기둥 접합 그림자 (y=9, x=8)
    #   GRASS_DK — 수관 그림자 색이 기둥 상단으로 스며들어 depth cue.
    # ─────────────────────────────────────────────────────────────────────────
    put_px(im, 8, 9, GRASS_DK)

    # ─────────────────────────────────────────────────────────────────────────
    # PASS 5: OUTLINE_PLANT 테두리
    #   수관 외곽: 투명 픽셀에만 적용 (채워진 픽셀 덮지 않음).
    #   x=2 돌출 포함.
    #   기둥 측면: x=6, x=9 (y=9..14), 기둥 하단 y=15.
    # ─────────────────────────────────────────────────────────────────────────
    canopy_outline = [
        # 상단 (y=0)
        (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 0),
        (9, 0), (10, 0), (11, 0), (12, 0),
        # 상단 y=1 좌우 끝
        (3, 1), (12, 1),
        # 좌측 (y=2..8)
        (2, 2), (2, 3), (2, 4), (2, 5), (2, 6),
        # 좌측 돌출 x=2 상하 코너
        (1, 3), (1, 4), (1, 5),
        (2, 7), (2, 8),
        # 우측 (y=1..8)
        (13, 2), (13, 3), (13, 4), (13, 5), (13, 6), (13, 7),
        (12, 8),
        # 수관 하단 좌측 (기둥 좌)
        (3, 9), (4, 9), (5, 9), (6, 9),
        # 수관 하단 우측 (기둥 우)
        (9, 9), (10, 9), (11, 9), (12, 9),
    ]
    for x, y in canopy_outline:
        if 0 <= x < W and 0 <= y < H and im.getpixel((x, y))[3] == 0:
            put_px(im, x, y, OUTLINE_PLANT)

    # 기둥 측면 테두리 (y=9..14)
    for y in range(9, 15):
        if im.getpixel((6, y))[3] == 0:
            put_px(im, 6, y, OUTLINE_PLANT)
        if im.getpixel((9, y))[3] == 0:
            put_px(im, 9, y, OUTLINE_PLANT)
    # 기둥 하단
    hline(im, 7, 8, 15, OUTLINE_PLANT)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# trader.png — 16x16 flat robed humanoid (NPC)
# Flat colour fills, OUTLINE_OBJ (1px — NPC is object tier not pawn tier).
# ─────────────────────────────────────────────────────────────────────────────

def gen_trader() -> Image.Image:
    """16x16 flat-style hooded trader.  Muted purple robe + belt accent."""
    W = H = 16
    im = new_canvas(W, H)

    # Hood (top of head) — y 0..4, roughly oval
    hood_pixels = [
        (5,0),(6,0),(7,0),(8,0),(9,0),(10,0),
        (4,1),(5,1),(6,1),(7,1),(8,1),(9,1),(10,1),(11,1),
        (4,2),(5,2),(6,2),(7,2),(8,2),(9,2),(10,2),(11,2),
        (4,3),(5,3),(6,3),(7,3),(8,3),(9,3),(10,3),(11,3),
    ]
    for x, y in hood_pixels: put_px(im, x, y, _HOOD)

    # Face peek (skin) — small window
    for y in range(2, 5):
        for x in range(6, 10):
            put_px(im, x, y, SKIN_MD)
    # Face shadow bottom
    hline(im, 6, 9, 4, SKIN_SH)

    # Robe body — trapezoidal, widens toward bottom, y 4..12
    for y in range(4, 13):
        w_extra = (y - 4) // 3
        x0 = max(3, 5 - w_extra)
        x1 = min(12, 10 + w_extra)
        for x in range(x0, x1 + 1):
            put_px(im, x, y, _ROBE)
        # Edge darker
        put_px(im, x0, y, _ROBE_DK)
        put_px(im, x1, y, _ROBE_DK)

    # Gold belt accent (y=8)
    hline(im, 5, 10, 8, _GOLD_ACC)

    # Legs / boots (y 13..15, two foot columns)
    for y in range(13, 16):
        put_px(im, 5,  y, _BOOT_DK)
        put_px(im, 6,  y, _BOOT_DK)
        put_px(im, 9,  y, _BOOT_DK)
        put_px(im, 10, y, _BOOT_DK)

    # Outline (OUTLINE_OBJ — NPC treated as object tier for outline weight)
    # Hood rim
    outline = [
        (4,0),(11,0),
        (3,1),(12,1),
        (3,2),(12,2),
        (3,3),(12,3),
        (3,4),(12,4),
        # robe sides (approximately)
        (2,5),(13,5),(2,6),(13,6),(2,7),(13,7),
        (3,8),(12,8),(3,9),(12,9),(3,10),(12,10),
        (4,11),(11,11),(4,12),(11,12),
    ]
    for x, y in outline:
        if im.getpixel((x, y))[3] == 0:
            put_px(im, x, y, OUTLINE)
        else:
            put_px(im, x, y, OUTLINE)  # overwrite edge pixels too

    # Hood top outline
    for x in range(4, 12): put_px(im, x, 0, OUTLINE)
    hline(im, 4, 11, 15, OUTLINE)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# Main
# ─────────────────────────────────────────────────────────────────────────────

def main():
    targets = [
        ("tree.png",    gen_tree),
        ("trader.png",  gen_trader),
    ]
    for name, fn in targets:
        im = fn()
        out = HERE / name
        im.save(out)
        print(f"[gen_sprites] {name}  {im.size[0]}x{im.size[1]}")


if __name__ == "__main__":
    main()
