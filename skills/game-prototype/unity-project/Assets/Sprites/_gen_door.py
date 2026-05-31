# -*- coding: utf-8 -*-
"""_gen_door.py — door_wood.png 재생성 (운영자 fb: 문처럼 안 생겼음).

기존 door_wood.png 문제점:
  - 불투명 WOOD_MD 배경으로 꽉 채워진 사각형 → 벽과 구별 안 됨
  - 중앙 회색 사각형(ROCK_LT/MD) → 창문처럼 보임, 문의 특징 없음
  - 세로 판자 없음, 경첩/손잡이 없음, 문 실루엣 없음

새 door_wood.png 디자인 (RimWorld 바닐라 스타일):
  - 투명 배경 (RGBA)
  - 문 패널: cols 2-13, rows 1-14 (1px 투명 마진 사방)
    - 1px OUTLINE_OBJ 프레임
    - 좌측 하이라이트 cols 2-4 → WOOD_LT (빛: 위-좌)
    - 우측 그림자 cols 11-13 → WOOD_DK
    - 본체 → WOOD_MD
  - 세로 판자 구분선 3개 (col 5, col 8, col 11) → WOOD_DK
    세로 판자 = 문의 핵심 시각 언어 (wall_wood는 가로 판자 → 대비)
  - 중앙 분합 seam col 7-8 경계 → OUTLINE_OBJ 1줄
    (두 짝으로 열리는 문 = 쪽문 한 쪽, 중앙에 틈)
  - 손잡이 knob: cols 9-10, rows 7-8 → WOOD_LT + OUTLINE_OBJ 테두리
    (우측 패널에 작은 사각형 손잡이)
  - 상단 경첩 hinge: col 3, rows 3-4 → OUTLINE_OBJ (좌측 상단)
  - 하단 경첩 hinge: col 3, rows 11-12 → OUTLINE_OBJ

스타일: flat-Kenney (solid fill + 1px outline_obj), 투명 배경,
        palette.py 색상만 사용. PPU=16 (1 grid cell).

NOT EDITED: 다른 _gen_*.py, palette.py, .cs 파일.
"""
from __future__ import annotations
from pathlib import Path
import sys

# palette.py 상대 경로 탐색
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from PIL import Image
from palette import (
    OUTLINE_OBJ,
    WOOD_DK, WOOD_MD, WOOD_LT,
)

SIZE = 16


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im: Image.Image, x: int, y: int, c: tuple) -> None:
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


def hline(im: Image.Image, x0: int, x1: int, y: int, c: tuple) -> None:
    for x in range(x0, x1 + 1):
        put(im, x, y, c)


def vline(im: Image.Image, x: int, y0: int, y1: int, c: tuple) -> None:
    for y in range(y0, y1 + 1):
        put(im, x, y, c)


def rect_fill(im: Image.Image, x0: int, y0: int, x1: int, y1: int, c: tuple) -> None:
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(im, x, y, c)


# ─────────────────────────────────────────────────────────────────────────────
# door_wood.png  16x16  (투명 배경, RimWorld 바닐라 문 스타일)
#
# 레이아웃 (PIL origin 좌상단, y=0=TOP):
#
#   col:  0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15
#         .  .  [========== 문 패널 ===========]  .  .
#   row 0: 투명
#   row 1: . . [OBJ 상단 프레임 ─────────────────] . .
#   row 2: . [OBJ] [WOOD_LT][M][M][S][M][M][M][M][WOOD_DK][OBJ] . .
#   ...
#   row14: . . [OBJ 하단 프레임 ─────────────────] . .
#   row15: 투명
#
#   세로 판자 구분선: col 5, col 8 (WOOD_DK) — 왼판/중앙/오른판 3구획
#   중앙 분합 seam: col 7 (OUTLINE_OBJ) — 문이 열리는 중심선
#   손잡이: cols 9-10, rows 7-8 (WOOD_LT + OUTLINE_OBJ 테두리) — 우측
#   경첩: col 3, rows 3-4 / rows 11-12 (OUTLINE_OBJ) — 좌측
# ─────────────────────────────────────────────────────────────────────────────
def gen_door_wood() -> Image.Image:
    """16x16 RGBA 나무 문 sprite.
    세로 판자 3구획 + 중앙 분합 seam + 손잡이 + 경첩.
    """
    im = new_canvas(SIZE, SIZE)

    # ── 1단계: 문 패널 본체 채우기 (cols 2..13, rows 1..14) ──────────────
    # 기본 색 WOOD_MD 채우기
    rect_fill(im, 2, 1, 13, 14, WOOD_MD)

    # 좌측 하이라이트 strip (cols 2-3) — 빛 방향: 상좌
    rect_fill(im, 2, 1, 3, 14, WOOD_LT)

    # 우측 그림자 strip (cols 12-13)
    rect_fill(im, 12, 1, 13, 14, WOOD_DK)

    # ── 2단계: 세로 판자 구분선 ───────────────────────────────────────────
    # col 5: 왼쪽 판자↔중앙 판자 경계
    vline(im, 5, 2, 13, WOOD_DK)
    # col 9: 중앙 판자↔오른쪽 판자 경계 (오른 그림자 영역 앞)
    vline(im, 9, 2, 13, WOOD_DK)

    # 각 판자 내부 grain 하이라이트 (1픽셀 dot — 나무 질감)
    # 왼쪽 판자 (cols 3-4): 상/중/하 3개 dot
    put(im, 4, 3,  WOOD_LT)
    put(im, 3, 8,  WOOD_LT)
    put(im, 4, 12, WOOD_LT)
    # 중앙 판자 (cols 6-8)
    put(im, 7, 4,  WOOD_LT)
    put(im, 6, 9,  WOOD_LT)
    put(im, 8, 13, WOOD_DK)
    # 오른쪽 판자 (cols 10-11)
    put(im, 11, 3, WOOD_DK)
    put(im, 10, 8, WOOD_DK)

    # ── 3단계: 중앙 분합 seam (col 7) — 문이 열리는 중심선 ───────────────
    # OUTLINE_OBJ 1px 세로선: 문짝 두 쪽 경계 표현
    vline(im, 7, 2, 13, OUTLINE_OBJ)

    # ── 4단계: OUTLINE_OBJ 1px 프레임 ────────────────────────────────────
    hline(im, 2, 13, 1,  OUTLINE_OBJ)   # 상단
    hline(im, 2, 13, 14, OUTLINE_OBJ)   # 하단
    vline(im, 2, 1, 14,  OUTLINE_OBJ)   # 좌측
    vline(im, 13, 1, 14, OUTLINE_OBJ)   # 우측

    # ── 5단계: 손잡이 knob (우측 패널, rows 7-8, cols 10-11) ─────────────
    # 작은 2x2 사각형 손잡이 — WOOD_LT 채우기 + OUTLINE_OBJ 테두리
    put(im, 10, 7, WOOD_LT)
    put(im, 11, 7, WOOD_LT)
    put(im, 10, 8, WOOD_LT)
    put(im, 11, 8, WOOD_LT)
    # 손잡이 테두리 (1px OUTLINE_OBJ)
    put(im,  9, 7, OUTLINE_OBJ)
    put(im,  9, 8, OUTLINE_OBJ)
    put(im, 10, 6, OUTLINE_OBJ)
    put(im, 11, 6, OUTLINE_OBJ)
    put(im, 12, 7, OUTLINE_OBJ)
    put(im, 12, 8, OUTLINE_OBJ)
    put(im, 10, 9, OUTLINE_OBJ)
    put(im, 11, 9, OUTLINE_OBJ)

    # ── 6단계: 경첩 hinge (좌측, col 3) ──────────────────────────────────
    # 상단 경첩: rows 3-4
    put(im, 3, 3, OUTLINE_OBJ)
    put(im, 3, 4, OUTLINE_OBJ)
    put(im, 4, 3, OUTLINE_OBJ)
    put(im, 4, 4, OUTLINE_OBJ)
    # 하단 경첩: rows 11-12
    put(im, 3, 11, OUTLINE_OBJ)
    put(im, 3, 12, OUTLINE_OBJ)
    put(im, 4, 11, OUTLINE_OBJ)
    put(im, 4, 12, OUTLINE_OBJ)

    return im


# ─────────────────────────────────────────────────────────────────────────────
# QA 프리뷰 (8x 확대, GRASS 배경) — _preview_door_wood.png
# ─────────────────────────────────────────────────────────────────────────────
def gen_preview(spr: Image.Image) -> Image.Image:
    BG = (96, 116, 70, 255)   # GRASS_MD — 잔디 위에서의 문 가독성 확인
    pad = 3
    W = spr.width + pad * 2
    H = spr.height + pad * 2
    canvas = Image.new("RGBA", (W, H), BG)
    canvas.paste(spr, (pad, pad), spr)
    scale = 8
    return canvas.resize((W * scale, H * scale), Image.NEAREST)


def main():
    spr = gen_door_wood()
    out_path = HERE / "door_wood.png"
    spr.save(out_path)
    print(f"[gen_door] door_wood.png  {spr.width}x{spr.height}  saved {out_path}")

    prev = gen_preview(spr)
    prev_path = HERE / "_preview_door_wood.png"
    prev.save(prev_path)
    print(f"[gen_door] _preview_door_wood.png  {prev.width}x{prev.height}  (8x QA)")


if __name__ == "__main__":
    main()
