# -*- coding: utf-8 -*-
"""A2 rev3 — top-down colonist sprite (RimWorld-grounded, 사람처럼 보이게 개선).

v1 문제: outline-first 방식이 좁은 zone(leg 3px, arm 3px)을 거의 검은색으로 덮어
         "검은 헬멧 + 파란 사각형"처럼 읽혔음.
v3 수정:
  - 16x16 유지, PPU 16
  - 머리: 10px 폭, hair interior 2행, 얼굴(skin) 3행
  - 눈 힌트: y4 에 SKIN_SH 2px
  - 목/숄더: y6 (9px)
  - 몸통+팔: y7-y9 (14px, 팔 3px). y8에서 각 팔 2px interior 확보
  - 몸통 하단: y10-y11 (10px) — y9 팔 하단 outline 자연스럽게 끊김
  - 다리: y12 가랑이(cloth), y13-14 분리, y15 발
  - 중립 베이스: 코드가 cloth tint 입힘

출력:
  pawn_colonist.png  16x16 (기본 파란 천)
  pawn_blue.png      16x16
  pawn_rust.png      16x16
  pawn_olive.png     16x16
  _preview_colonist.png  4x scale 4종 나란히
"""
from __future__ import annotations
from PIL import Image
from pathlib import Path

from palette import (
    OUTLINE_STORY,
    SKIN_MD, SKIN_SH,
    HAIR_DK,
    CLOTH_BLUE, CLOTH_BLUE_DK,
    CLOTH_RUST, CLOTH_RUST_DK,
    CLOTH_OLIVE, CLOTH_OLIVE_DK,
    TROUSER_BLUE, TROUSER_RUST, TROUSER_OLIVE,
    WOOD_DK,
)

HERE = Path(__file__).resolve().parent

T  = (0, 0, 0, 0)    # transparent
OL = OUTLINE_STORY   # near-black warm outline

# ---------------------------------------------------------------------------
# 존 맵 — 16x16
# 0 = transparent
# 1 = hair     (HAIR_DK)
# 2 = skin     (SKIN_MD)
# 3 = torso    (cloth)
# 4 = arm      (cloth_dk)
# 5 = leg      (trouser)
# 6 = boot     (WOOD_DK)
# 7 = skin_sh  (SKIN_SH — 눈 힌트)
#
# 레이아웃 (col 0-15):
#   y0      머리 꼭대기 10px
#   y1-2    머리카락 interior (10px interior)
#   y3-5    얼굴: 측면 2px 머리카락, 중앙 8px 피부
#   y6      목+숄더 전환 (9px)
#   y7-9    몸통(8px)+팔(3px each), 총14px
#   y10-11  몸통 하단 10px (팔 없음)
#   y12     가랑이 (torso 색, 다리와 연결)
#   y13-14  다리 (각 4px)
#   y15     발 (각 4px)
# ---------------------------------------------------------------------------
#         col: 0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15
ZONE_MAP = [
    # y=0  머리 꼭대기
    [0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0],
    # y=1
    [0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0],
    # y=2  머리카락 interior
    [0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0],
    # y=3  얼굴 상단 (hair 측면 2px, skin 8px)
    [0, 0, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 0, 0],
    # y=4  얼굴 중단 (눈 힌트)
    [0, 0, 1, 1, 2, 2, 7, 2, 2, 7, 2, 2, 1, 1, 0, 0],
    # y=5  얼굴 하단
    [0, 0, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 0, 0],
    # y=6  목+숄더 (9px: cloth 양옆, skin 중앙)
    [0, 0, 0, 3, 3, 2, 2, 2, 2, 2, 3, 3, 0, 0, 0, 0],
    # y=7  몸통+팔 (14px: 팔 3px each)
    [0, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 0],
    # y=8
    [0, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 0],
    # y=9
    [0, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 0],
    # y=10 몸통 하단 (10px, 팔 없음 → 자연스럽게 팔 끝 표현)
    [0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0],
    # y=11 몸통 최하단 (10px 유지 → y12 가랑이와 동폭)
    [0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0],
    # y=12 가랑이 (cloth 색, 다리 시작 열과 동폭 → outline 없음)
    [0, 0, 0, 5, 5, 5, 5, 3, 3, 5, 5, 5, 5, 0, 0, 0],
    # y=13 다리 (각 4px)
    [0, 0, 0, 5, 5, 5, 5, 0, 0, 5, 5, 5, 5, 0, 0, 0],
    # y=14
    [0, 0, 0, 5, 5, 5, 5, 0, 0, 5, 5, 5, 5, 0, 0, 0],
    # y=15 발
    [0, 0, 0, 6, 6, 6, 6, 0, 0, 6, 6, 6, 6, 0, 0, 0],
]

assert len(ZONE_MAP) == 16, f"행 수 오류: {len(ZONE_MAP)}"
for i, r in enumerate(ZONE_MAP):
    assert len(r) == 16, f"y={i} 열 수 오류: {len(r)}"


def _build_mask() -> set:
    """zone != 0 인 모든 (x, y) 픽셀 집합."""
    return {(x, y) for y, row in enumerate(ZONE_MAP) for x, z in enumerate(row) if z != 0}


def _outline_ring(mask: set) -> set:
    """실루엣 외곽 경계 픽셀: 투명 4-이웃이 하나라도 있는 불투명 픽셀."""
    ring = set()
    for (x, y) in mask:
        for dx, dy in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
            if (x + dx, y + dy) not in mask:
                ring.add((x, y))
                break
    return ring


def gen_pawn(cloth: tuple, cloth_dk: tuple, trouser: tuple) -> Image.Image:
    """16x16 flat-style 콜로니스트 생성. cloth 색은 외부에서 주입 (중립 베이스).

    Args:
        cloth:    상의/몸통 색 (CLOTH_*)
        cloth_dk: 팔/어깨 음영 (CLOTH_*_DK)
        trouser:  하의 색 (TROUSER_*)

    Returns:
        RGBA Image 16x16
    """
    im = Image.new("RGBA", (16, 16), T)

    zone_color: dict[int, tuple] = {
        1: HAIR_DK,
        2: SKIN_MD,
        3: cloth,
        4: cloth_dk,
        5: trouser,
        6: WOOD_DK,
        7: SKIN_SH,
    }

    mask    = _build_mask()
    outline = _outline_ring(mask)

    # 패스 1: 모든 zone 픽셀에 기본 색 채우기
    for y, row in enumerate(ZONE_MAP):
        for x, z in enumerate(row):
            if z != 0:
                im.putpixel((x, y), zone_color[z])

    # 패스 2: 외곽 아웃라인 덮어쓰기 (실루엣 경계만)
    for (x, y) in outline:
        im.putpixel((x, y), OL)

    return im


def gen_preview(sprites: list, scale: int = 4) -> Image.Image:
    """4x scale 나란히 합성, 어두운 패널 배경."""
    gap = 4
    n   = len(sprites)
    pw  = n * 16 + (n - 1) * gap
    ph  = 16
    bg  = (42, 31, 24, 255)
    composite = Image.new("RGBA", (pw, ph), bg)
    for i, (_, spr) in enumerate(sprites):
        composite.paste(spr, (i * (16 + gap), 0), spr)
    return composite.resize((pw * scale, ph * scale), Image.NEAREST)


def main():
    variants = [
        ("pawn_colonist", CLOTH_BLUE,  CLOTH_BLUE_DK,  TROUSER_BLUE),
        ("pawn_blue",     CLOTH_BLUE,  CLOTH_BLUE_DK,  TROUSER_BLUE),
        ("pawn_rust",     CLOTH_RUST,  CLOTH_RUST_DK,  TROUSER_RUST),
        ("pawn_olive",    CLOTH_OLIVE, CLOTH_OLIVE_DK, TROUSER_OLIVE),
    ]

    sprites = []
    for slug, cloth, cloth_dk, trouser in variants:
        im  = gen_pawn(cloth, cloth_dk, trouser)
        out = HERE / f"{slug}.png"
        im.save(out)
        print(f"[gen_pawn] {slug}.png  {im.size[0]}x{im.size[1]}")
        sprites.append((slug, im))

    prev      = gen_preview(sprites, scale=4)
    prev_path = HERE / "_preview_colonist.png"
    prev.save(prev_path)
    print(f"[gen_pawn] _preview_colonist.png  {prev.size[0]}x{prev.size[1]}")


if __name__ == "__main__":
    main()
