# -*- coding: utf-8 -*-
"""A2 rev3 — top-down colonist sprite (the reference sim-grounded, 사람처럼 보이게 개선).

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
    CLOTH_LINEN, CLOTH_LINEN_DK,
    TROUSER_BLUE, TROUSER_RUST, TROUSER_LINEN,
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


def _scale(c: tuple, f: float) -> tuple:
    """RGB 를 f 배 (alpha 유지).  f<1 어둡게, f>1 밝게 — 채널 클램프."""
    r, g, b, a = c
    return (max(0, min(255, int(r * f))),
            max(0, min(255, int(g * f))),
            max(0, min(255, int(b * f))), a)


def gen_pawn(cloth: tuple, cloth_dk: tuple, trouser: tuple,
             hair: tuple = HAIR_DK, skin_md: tuple = SKIN_MD,
             skin_sh: tuple = SKIN_SH) -> Image.Image:
    """16x16 flat-style 콜로니스트 생성. cloth/hair/skin 색은 외부에서 주입.

    #UI-A 음영: 실루엣/스타일은 유지하되 top-down 조명으로 입체감.
      - 머리: 윗줄 하이라이트 / 얼굴 턱줄 그림자
      - 상의: 상단(어깨/가슴) 하이라이트, 배 아래 그림자 → 옷이 둥글게 읽힘
      - 바지: 정강이 그림자, 신발 윗면 하이라이트
    #54 외형 다양화: hair/skin 도 파라미터화 → 콜로니스트마다 머리색·피부톤 구분.

    Args:
        cloth/cloth_dk/trouser: 상의/팔음영/하의 색
        hair:    머리색 (기본 HAIR_DK)
        skin_md/skin_sh: 피부 기본/그림자 톤 (기본 SKIN_MD/SH)

    Returns:
        RGBA Image 16x16
    """
    im = Image.new("RGBA", (16, 16), T)

    cloth_lt   = _scale(cloth, 1.20)     # 상의 하이라이트(어깨/가슴)
    cloth_lo   = _scale(cloth, 0.82)     # 상의 하단 그림자
    hair_lt    = _scale(hair, 1.45)      # 머리 하이라이트
    trouser_lo = _scale(trouser, 0.82)   # 바지 하단 그림자
    boot_lt    = _scale(WOOD_DK, 1.28)   # 신발 윗면 하이라이트
    # TOP-4 (visual-polish-backlog 2026-06-11): 16px 톱다운에서 표정은 눈 2px 가
    #  전부 — SKIN_SH 힌트는 안 보였다.  진짜 눈동자(근흑) + 벨트 1px 절개선.
    eye        = (44, 34, 30, 255)
    belt       = (62, 46, 32, 255)

    def shade(z: int, y: int) -> tuple:
        """zone+행 위치로 음영 색 결정 (top-down 라이팅)."""
        if z == 1:                                   # hair
            return hair_lt if y <= 1 else hair
        if z == 2:                                   # skin
            return skin_sh if y >= 5 else skin_md    # 턱줄 그림자
        if z == 3:                                   # torso cloth
            if y <= 7:  return cloth_lt              # 어깨/가슴 하이라이트
            if y == 11: return belt                  # TOP-4 벨트 절개선
            if y >= 10: return cloth_lo              # 배 아래 그림자
            return cloth
        if z == 4:  return cloth_dk                  # arm (이미 그림자색)
        if z == 5:                                   # trouser
            return trouser_lo if y >= 14 else trouser
        if z == 6:                                   # boot
            return boot_lt if y == 15 else WOOD_DK
        if z == 7:  return eye                       # TOP-4 눈동자 (근흑 2px)
        return skin_md

    mask    = _build_mask()
    outline = _outline_ring(mask)

    # 패스 1: zone 픽셀에 음영 적용 색 채우기
    for y, row in enumerate(ZONE_MAP):
        for x, z in enumerate(row):
            if z != 0:
                im.putpixel((x, y), shade(z, y))

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


# #54 콜로니스트 외형 다양화 — 머리색/피부톤 팔레트.
HAIR_BLACK  = (32, 28, 30, 255)
HAIR_BROWN  = HAIR_DK                  # (58,38,22)
HAIR_AUBURN = (120, 58, 34, 255)
HAIR_BLONDE = (176, 138, 78, 255)
HAIR_GREY   = (158, 156, 150, 255)
# 피부 (md, sh) 쌍 — 라이트/미디엄/탠/다크
SKIN_LIGHT  = ((238, 202, 170, 255), (200, 162, 126, 255))
SKIN_MED    = (SKIN_MD, SKIN_SH)               # 기존
SKIN_TAN    = ((196, 150, 108, 255), (154, 114, 80, 255))
SKIN_DARK   = ((150, 108, 76, 255),  (112, 80, 54, 255))


def main():
    # 기존 슬롯(back-compat) — cloth 변형만.
    variants = [
        ("pawn_colonist", CLOTH_BLUE,  CLOTH_BLUE_DK,  TROUSER_BLUE),
        ("pawn_blue",     CLOTH_BLUE,  CLOTH_BLUE_DK,  TROUSER_BLUE),
        ("pawn_rust",     CLOTH_RUST,  CLOTH_RUST_DK,  TROUSER_RUST),
        ("pawn_olive",    CLOTH_LINEN, CLOTH_LINEN_DK, TROUSER_LINEN),
    ]

    sprites = []
    for slug, cloth, cloth_dk, trouser in variants:
        im  = gen_pawn(cloth, cloth_dk, trouser)
        out = HERE / f"{slug}.png"
        im.save(out)
        print(f"[gen_pawn] {slug}.png  {im.size[0]}x{im.size[1]}")
        sprites.append((slug, im))

    # #54 개인별 구분되는 콜로니스트 변형 8종 (cloth × hair × skin 조합).
    #  GameManager.colonistVariantSprites 가 v0..v7 을 림 0..7 에 배정.
    cloths = [
        (CLOTH_BLUE,  CLOTH_BLUE_DK,  TROUSER_BLUE),
        (CLOTH_RUST,  CLOTH_RUST_DK,  TROUSER_RUST),
        (CLOTH_LINEN, CLOTH_LINEN_DK, TROUSER_LINEN),
    ]
    combos = [
        (0, HAIR_BROWN,  SKIN_MED),
        (1, HAIR_BLACK,  SKIN_LIGHT),
        (2, HAIR_BLONDE, SKIN_TAN),
        (0, HAIR_AUBURN, SKIN_DARK),
        (1, HAIR_GREY,   SKIN_MED),
        (2, HAIR_BLACK,  SKIN_TAN),
        (0, HAIR_BLONDE, SKIN_LIGHT),
        (1, HAIR_BROWN,  SKIN_DARK),
    ]
    vsprites = []
    for idx, (ci, hair, (smd, ssh)) in enumerate(combos):
        cloth, cloth_dk, trouser = cloths[ci]
        im = gen_pawn(cloth, cloth_dk, trouser, hair=hair, skin_md=smd, skin_sh=ssh)
        out = HERE / f"pawn_v{idx}.png"
        im.save(out)
        print(f"[gen_pawn] pawn_v{idx}.png  {im.size[0]}x{im.size[1]}")
        vsprites.append((f"v{idx}", im))
    # 다양화 변형 프리뷰
    gen_preview(vsprites, scale=4).save(HERE / "_preview_variants.png")

    prev      = gen_preview(sprites, scale=4)
    prev_path = HERE / "_preview_colonist.png"
    prev.save(prev_path)
    print(f"[gen_pawn] _preview_colonist.png  {prev.size[0]}x{prev.size[1]}")


if __name__ == "__main__":
    main()
