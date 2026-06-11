# -*- coding: utf-8 -*-
"""_gen_struct32.py — Art v2 건축 구조물 생성기 (톱다운 콜로니심, PPU32).

스펙: G:/ai/_artv2_staging/artv2-style-spec.md
  외곽선 1px OUTLINE_OBJ / 플랫 + 램프 2단 / 광원 좌상 / 디더링 금지.
팔레트: Assets/Sprites/palette.py import 만 — 하드코딩 금지.
  파생은 HSV V ±12% (채도는 그룹 밴드 유지).

구조물 13종 — 캔버스 = 월드 풋프린트 x 32, 풋프린트 꽉 채움(타일형):
  wall_wood / wall_stone   32x32  통벽 — 윗면 2px 캡 + 정면, 외곽선 상하만,
                                  좌우 가장자리 1px 벽체 램프색(인접 연속)
  floor_wood / floor_stone 32x32  플랫 바닥, 테두리 1px 베이스 링(심리스)
  door_wood                32x32  벽 프레임 + 중앙 문짝(가로 널 + 손잡이)
  fence / fence_gate       32x32  기둥 2 + 레일 2 (+X 보강), 투명 비율 높음
  barricade                32x32  모래주머니 + 각목 낮은 장벽 (좌우 이음 매칭)
  stove                    32x32  석축 + 전면 화구(FIRE_OR 글로우 2px) + 조리면
  lamp                     32x32  가는 기둥 + 랜턴(CROP_GOLD 유리 + 글로우)
  research_bench           64x32  2x1 작업대 + 두루마리/종이/잉크 소품
  bed_wood / bed_fine      32x64  1x2 침대 — 베개 + 담요(+골드 띠)

출력: out/struct32_{name}.png + 8배 프리뷰 _preview_struct32.png
  (잔디 GRASS_MD 배경, 벽 3연속 행 포함 — 이음새 검증용)
"""
import sys
import os
import colorsys
import random

sys.stdout.reconfigure(encoding="utf-8")

PAL_DIR = r"G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Sprites"
sys.path.insert(0, PAL_DIR)
import palette as P  # noqa: E402

from PIL import Image, ImageDraw  # noqa: E402

SEED = 20260611
OUT = r"G:/ai/_artv2_staging/out"
os.makedirs(OUT, exist_ok=True)

CELL = 32
SCALE = 8


# ── 팔레트 파생 (스펙 §1: V ±12%, 채도는 그룹 밴드 내) ──────────────────
def shade(c, dv=0.0, s=None):
    r, g, b, a = c
    h, s0, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    if s is not None:
        s0 = s
    v = max(0.0, min(1.0, v * (1.0 + dv)))
    r2, g2, b2 = colorsys.hsv_to_rgb(h, s0, v)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


WOOD_GRAIN = shade(P.WOOD_MD, -0.10)     # 널결 (MD 위 은은한 결)
WOOD_HI = shade(P.WOOD_MD, +0.10)        # 널 상단 미광
DOOR_FACE = shade(P.WOOD_MD, +0.08)      # 문짝 (벽보다 살짝 밝게 — 포컬)
UNDER_DK = shade(P.WOOD_DK, -0.12)       # 가구 하부 그늘
STONE_FACE = shade(P.ROCK_MD, -0.12)     # 화덕 전면(그늘면)
PLATE_HOT = shade(P.ROCK_DK, +0.10)      # 화덕 조리면 판
SAND_HI = shade(P.DIRT_LT, +0.10)        # 모래주머니 상단광
CREAM_SH = shade(P.UI_CREAM, -0.10)      # 직물/종이 그늘
GOLD_DK = shade(P.CROP_GOLD, -0.10)
BLUE_HEM = shade(P.CLOTH_BLUE, +0.12)    # 담요 접단
# 로열블루: CLOTH_BLUE 휴 유지, 채도만 의상 밴드 상한(러스트 0.63) 내로
ROYAL = shade(P.CLOTH_BLUE, -0.10, s=0.62)
ROYAL_DK = shade(P.CLOTH_BLUE_DK, -0.08, s=0.62)
ROYAL_HEM = shade(P.CLOTH_BLUE, +0.08, s=0.58)


# ── 공통 헬퍼 ────────────────────────────────────────────────────────
def add_outline(img, color=P.OUTLINE_OBJ):
    w, h = img.size
    px = img.load()
    solid = [(x, y) for y in range(h) for x in range(w) if px[x, y][3] > 0]
    for (x, y) in solid:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3] == 0:
                px[nx, ny] = color
    return img


def element(w, h, draw_fn, *args):
    img = Image.new("RGBA", (w + 2, h + 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    draw_fn(d, 1, 1, *args)
    return add_outline(img)


def put(canvas, elem, x, y):
    canvas.alpha_composite(elem, (x - 1, y - 1))


def tile(w=CELL, h=CELL):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def upscale(img, s=SCALE):
    return img.resize((img.width * s, img.height * s), Image.NEAREST)


def hline(d, x0, x1, y, c):
    d.line((x0, y, x1, y), fill=c)


def vline(d, x, y0, y1, c):
    d.line((x, y0, x, y1), fill=c)


# ── 벽: 통벽 (외곽선 상하만, 좌우 1px 벽체색 — 인접 연속) ────────────────
def sprite_wall_wood():
    img = tile()
    d = ImageDraw.Draw(img)
    d.rectangle((0, 1, 31, 2), fill=P.WOOD_LT)      # 윗면 캡 2px
    hline(d, 0, 31, 3, P.WOOD_DK)                   # 캡-정면 경계 그늘
    d.rectangle((0, 4, 31, 28), fill=P.WOOD_MD)     # 정면 널판
    d.rectangle((0, 29, 31, 30), fill=P.WOOD_DK)    # 하단 접지 그늘
    for sx in (3, 11, 19, 27):                      # 널 이음 (주기 8 — 타일링)
        vline(d, sx, 4, 30, P.WOOD_DK)
        d.point((sx, 2), fill=P.WOOD_MD)            # 캡 위 이음 힌트
    # 세로 결 (널마다 1줄, 위치 분산)
    for gx, gy0, gy1 in ((6, 8, 13), (14, 17, 22), (22, 6, 11), (29, 15, 20)):
        vline(d, gx, gy0, gy1, WOOD_GRAIN)
    # 널 좌측 미광 (좌상 광원)
    for px_ in ((4, 5, 10), (12, 6, 11), (20, 14, 19), (28, 7, 12)):
        vline(d, px_[0], px_[1], px_[2], WOOD_HI)
    hline(d, 0, 31, 0, P.OUTLINE_OBJ)               # 외곽선 상
    hline(d, 0, 31, 31, P.OUTLINE_OBJ)              # 외곽선 하
    return img


def sprite_wall_stone():
    img = tile()
    d = ImageDraw.Draw(img)
    d.rectangle((0, 1, 31, 2), fill=P.ROCK_LT)      # 윗면 캡 2px
    hline(d, 0, 31, 3, P.ROCK_DK)
    d.rectangle((0, 4, 31, 30), fill=P.ROCK_MD)
    # 가로 줄눈 4단
    for jy in (10, 17, 24):
        hline(d, 0, 31, jy, P.ROCK_DK)
    # 세로 줄눈 — 코스별 반엇갈림 (주기 16: 인접 타일과 패턴 연속)
    courses = [(4, 9, (7, 23)), (11, 16, (15, 31)),
               (18, 23, (7, 23)), (25, 30, (15, 31))]
    for y0, y1, xs in courses:
        for jx in xs:
            vline(d, jx, y0, y1, P.ROCK_DK)
        # 블록 좌상단 미광 2px (좌상 광원)
        for jx in xs:
            bx = (jx + 2) % 32
            hline(d, bx, bx + 1, y0, P.ROCK_LT)
    hline(d, 0, 31, 0, P.OUTLINE_OBJ)
    hline(d, 0, 31, 31, P.OUTLINE_OBJ)
    return img


# ── 바닥: 플랫, 테두리 1px 베이스 링 (심리스) ────────────────────────────
def sprite_floor_wood():
    img = Image.new("RGBA", (CELL, CELL), P.WOOD_MD)
    d = ImageDraw.Draw(img)
    # 널 3장: 가로 이음 y10/21, 장마다 마구리(세로) 이음 1개 엇갈림
    hline(d, 1, 30, 10, P.WOOD_DK)
    hline(d, 1, 30, 21, P.WOOD_DK)
    vline(d, 20, 1, 9, P.WOOD_DK)
    vline(d, 8, 11, 20, P.WOOD_DK)
    vline(d, 24, 22, 30, P.WOOD_DK)
    # 널 상단 미광 (이음 아래 1px 부분 대시)
    hline(d, 2, 7, 11, WOOD_HI)
    hline(d, 22, 27, 22, WOOD_HI)
    hline(d, 4, 9, 1, WOOD_HI)
    # 결 대시
    hline(d, 4, 7, 5, WOOD_GRAIN)
    hline(d, 24, 28, 4, WOOD_GRAIN)
    hline(d, 12, 16, 15, WOOD_GRAIN)
    hline(d, 3, 5, 18, WOOD_GRAIN)
    hline(d, 13, 17, 26, WOOD_GRAIN)
    hline(d, 27, 29, 28, WOOD_GRAIN)
    # 못 1px (마구리 이음 양옆)
    for nx, ny in ((18, 3), (22, 7), (6, 13), (10, 18), (22, 24), (26, 28)):
        d.point((nx, ny), fill=P.ROCK_LT)
    return img


def sprite_floor_stone():
    img = Image.new("RGBA", (CELL, CELL), P.ROCK_MD)
    d = ImageDraw.Draw(img)
    # 석판 4장 — 십자 어긋난 줄눈 (핀휠)
    vline(d, 15, 1, 30, P.ROCK_DK)
    hline(d, 1, 15, 13, P.ROCK_DK)
    hline(d, 15, 30, 19, P.ROCK_DK)
    # 판마다 좌상 미광 2-4px
    hline(d, 2, 5, 2, P.ROCK_LT)
    vline(d, 2, 3, 4, P.ROCK_LT)
    hline(d, 17, 20, 2, P.ROCK_LT)
    hline(d, 2, 4, 15, P.ROCK_LT)
    hline(d, 17, 20, 21, P.ROCK_LT)
    # 패임 1px
    for px_, py in ((9, 8), (25, 7), (7, 24), (23, 27), (12, 26), (28, 14)):
        d.point((px_, py), fill=P.ROCK_DK)
    return img


# ── 문: 벽 프레임 + 중앙 문짝 (가로 널 + 손잡이) ─────────────────────────
def sprite_door_wood():
    img = sprite_wall_wood()                        # 벽 스텁/캡/외곽선 승계
    d = ImageDraw.Draw(img)
    # 프레임 기둥 (안쪽 외곽선)
    vline(d, 5, 3, 30, P.OUTLINE_OBJ)
    vline(d, 26, 3, 30, P.OUTLINE_OBJ)
    # 문짝 (살짝 밝은 면 — 가로 널)
    d.rectangle((6, 3, 25, 28), fill=DOOR_FACE)
    hline(d, 6, 25, 3, P.WOOD_DK)                   # 인셋 상단 그늘
    vline(d, 6, 3, 28, P.WOOD_DK)                   # 인셋 좌측 그늘
    hline(d, 7, 25, 4, WOOD_HI)                     # 상단 널 미광
    for jy in (12, 20):                             # 가로 널 3장 (서랍장 회피)
        hline(d, 7, 25, jy, P.WOOD_DK)
        hline(d, 7, 12, jy + 1, WOOD_HI)            # 이음 아래 미광
    hline(d, 9, 14, 8, WOOD_GRAIN)                  # 결 대시
    hline(d, 12, 17, 16, WOOD_GRAIN)
    hline(d, 10, 15, 25, WOOD_GRAIN)
    vline(d, 22, 14, 15, P.ROCK_LT)                 # 손잡이 (1px 폭 금속)
    d.point((22, 16), fill=P.OUTLINE_OBJ)           # 손잡이 하단 그늘
    d.point((21, 15), fill=WOOD_GRAIN)
    d.rectangle((6, 29, 25, 30), fill=UNDER_DK)     # 문턱 밑 그늘
    return img


# ── 울타리: 기둥 2 + 레일 2 (레일은 가장자리까지 — 인접 연속) ────────────
def _rail(d, y):
    hline(d, 0, 31, y, P.OUTLINE_OBJ)
    hline(d, 0, 31, y + 1, P.WOOD_LT)
    hline(d, 0, 31, y + 2, P.WOOD_MD)
    hline(d, 0, 31, y + 3, P.OUTLINE_OBJ)


def _post(d, x, y):
    """3x24 기둥 — 좌 LT / 중 MD / 우 DK, 캡 LT."""
    vline(d, x, y + 1, y + 23, P.WOOD_LT)
    vline(d, x + 1, y, y + 23, P.WOOD_MD)
    vline(d, x + 2, y + 1, y + 23, P.WOOD_DK)
    d.point((x + 1, y), fill=P.WOOD_LT)
    hline(d, x, x + 2, y + 1, P.WOOD_LT)


def post_elem():
    return element(3, 24, _post)


def sprite_fence():
    img = tile()
    d = ImageDraw.Draw(img)
    _rail(d, 11)
    _rail(d, 19)
    put(img, post_elem(), 4, 4)
    put(img, post_elem(), 25, 4)
    return img


def _xbrace(d, x, y):
    """16x18 X자 보강 (2px 사선)."""
    d.line((x, y, x + 15, y + 17), fill=P.WOOD_MD, width=2)
    d.line((x + 15, y, x, y + 17), fill=P.WOOD_MD, width=2)


def sprite_fence_gate():
    img = tile()
    d = ImageDraw.Draw(img)
    _rail(d, 11)
    _rail(d, 19)
    put(img, element(16, 18, _xbrace), 8, 8)        # 레일 앞, 기둥 뒤
    put(img, post_elem(), 4, 4)
    put(img, post_elem(), 25, 4)
    d2 = ImageDraw.Draw(img)
    d2.point((15, 16), fill=P.ROCK_LT)              # 걸쇠 1px
    d2.point((16, 16), fill=P.ROCK_LT)
    return img


# ── 바리케이드: 모래주머니 + 각목, 좌우 이음 매칭 ────────────────────────
def _bag(d, x, y):
    """12x8 모래주머니."""
    d.rounded_rectangle((x, y, x + 11, y + 7), radius=3, fill=P.DIRT_LT)
    hline(d, x + 2, x + 7, y, SAND_HI)              # 상단광
    hline(d, x + 1, x + 10, y + 7, P.DIRT_MD)       # 하단 그늘
    hline(d, x + 8, x + 10, y + 6, P.DIRT_MD)
    for sx in (x + 3, x + 6, x + 9):                # 박음질 점
        d.point((sx, y + 2), fill=P.DIRT_MD)


def bag_elem():
    return element(12, 8, _bag)


def _cross_planks(d, x, y):
    """26x13 교차 각목 (모래주머니 뒤 X 보강재)."""
    d.line((x, y + 12, x + 25, y), fill=P.WOOD_MD, width=2)
    d.line((x, y, x + 25, y + 12), fill=P.WOOD_MD, width=2)
    d.point((x + 25, y + 1), fill=P.WOOD_LT)        # 끝단 절단면
    d.point((x, y + 1), fill=P.WOOD_LT)


def sprite_barricade():
    M = 14
    work = Image.new("RGBA", (CELL + 2 * M, CELL), (0, 0, 0, 0))
    # 뒷줄(위) 먼저 → 앞줄(아래)이 덮음
    put(work, element(26, 13, _cross_planks), M + 3, 9)   # 교차 각목
    put(work, bag_elem(), M + 3, 16)                      # 윗줄 주머니 2개
    put(work, bag_elem(), M + 17, 16)
    # 아랫줄: x=-6 과 x=26 은 같은 주머니 (mod 32) → 인접 시 한 덩어리
    for bx in (-6, 5, 16, 26):
        put(work, bag_elem(), M + bx, 23)
    return work.crop((M, 0, M + CELL, CELL))


# ── 화덕: 석축 + 윗면 조리면 + 전면 화구 (FIRE_OR 글로우 2px) ───────────
def sprite_stove():
    img = tile()
    d = ImageDraw.Draw(img)
    # 윗면 조리면
    d.rectangle((1, 1, 30, 17), fill=P.ROCK_MD)
    hline(d, 1, 30, 1, P.ROCK_LT)                   # 좌상광
    vline(d, 1, 1, 17, P.ROCK_LT)
    hline(d, 4, 6, 4, P.ROCK_LT)                    # 석재 미광 대시
    hline(d, 25, 27, 3, P.ROCK_DK)                  # 윗면 석재 줄눈 힌트
    d.ellipse((10, 5, 21, 13), outline=P.ROCK_DK)   # 조리판 링 1px
    d.ellipse((11, 6, 20, 12), fill=PLATE_HOT)      # 판 면
    hline(d, 15, 16, 9, P.FIRE_OR)                  # 달궈진 중심
    d.point((15, 9), fill=P.FIRE_LT)
    hline(d, 13, 15, 7, shade(P.ROCK_DK, +0.12))    # 판 좌상 미광
    hline(d, 1, 30, 18, P.ROCK_DK)                  # 윗면-전면 경계
    # 전면 (그늘면) + 줄눈
    d.rectangle((1, 19, 30, 30), fill=STONE_FACE)
    vline(d, 6, 19, 30, P.ROCK_DK)
    vline(d, 25, 19, 30, P.ROCK_DK)
    hline(d, 1, 30, 25, P.ROCK_DK)
    # 화구 (아치 개구부)
    d.rectangle((10, 20, 21, 30), fill=P.OUTLINE_OBJ)
    d.point((10, 20), fill=STONE_FACE)              # 아치 모서리 컷
    d.point((21, 20), fill=STONE_FACE)
    # 잉걸불: FIRE_OR 2px 글로우 + FIRE_LT 심지
    hline(d, 13, 18, 28, P.FIRE_OR)
    hline(d, 12, 19, 29, P.FIRE_OR)
    hline(d, 15, 16, 28, P.FIRE_LT)
    d.point((14, 27), fill=P.FIRE_OR)
    d.point((17, 27), fill=P.FIRE_OR)
    d.rectangle((0, 0, 31, 31), outline=P.OUTLINE_OBJ)
    return img


# ── 램프: 가는 기둥 + 랜턴 머리 (CROP_GOLD 유리 + 글로우) ────────────────
def _lamp_base(d, x, y):
    d.rectangle((x, y, x + 7, y + 2), fill=P.ROCK_MD)
    hline(d, x, x + 7, y, P.ROCK_LT)
    hline(d, x + 1, x + 6, y + 2, P.ROCK_DK)


def _lamp_post(d, x, y):
    vline(d, x, y, y + 15, P.WOOD_MD)
    vline(d, x + 1, y, y + 15, P.WOOD_DK)


def _lantern(d, x, y):
    """8x10 랜턴 — 지붕/받침 WOOD_DK, 유리 CROP_GOLD, 심지 FIRE_LT."""
    hline(d, x + 2, x + 5, y, P.WOOD_MD)            # 지붕 꼭대기
    hline(d, x + 1, x + 6, y + 1, P.WOOD_DK)
    d.rectangle((x, y + 2, x + 7, y + 7), fill=P.CROP_GOLD)
    vline(d, x, y + 2, y + 7, P.WOOD_DK)            # 프레임 기둥
    vline(d, x + 7, y + 2, y + 7, P.WOOD_DK)
    d.rectangle((x + 3, y + 4, x + 4, y + 5), fill=P.FIRE_LT)  # 심지
    d.point((x + 1, y + 2), fill=P.FIRE_LT)         # 유리 좌상 글린트
    hline(d, x + 1, x + 6, y + 8, P.WOOD_DK)        # 받침
    hline(d, x + 2, x + 5, y + 9, P.WOOD_DK)
    return


def sprite_lamp():
    img = tile()
    put(img, element(8, 3, _lamp_base), 12, 26)
    put(img, element(2, 16, _lamp_post), 15, 11)
    put(img, element(8, 10, _lantern), 12, 2)
    d = ImageDraw.Draw(img)
    for gx, gy in ((10, 5), (10, 7), (21, 5), (21, 7)):  # 1px 글로우 광선
        d.point((gx, gy), fill=P.CROP_GOLD)
    return img


# ── 연구 작업대 64x32: 목재 상판 + 소품 (두루마리/종이/잉크+깃펜) ───────
def _scroll(d, x, y):
    """13x6 두루마리."""
    d.rounded_rectangle((x, y, x + 12, y + 5), radius=2, fill=P.UI_CREAM)
    hline(d, x + 1, x + 11, y + 5, CREAM_SH)
    d.ellipse((x, y, x + 3, y + 5), fill=CREAM_SH)  # 좌측 말림 단면
    d.point((x + 1, y + 2), fill=P.UI_CREAM)
    vline(d, x + 7, y, y + 5, P.CROP_GOLD)          # 골드 끈
    d.point((x + 7, y + 5), fill=GOLD_DK)


def _paper(d, x, y):
    """14x9 펼친 종이 + UI_MUTED 글줄."""
    d.rectangle((x, y, x + 13, y + 8), fill=P.UI_CREAM)
    vline(d, x + 6, y, y + 8, CREAM_SH)             # 가운데 접힘
    hline(d, x + 1, x + 12, y + 8, CREAM_SH)
    for ly in (y + 2, y + 4, y + 6):
        hline(d, x + 1, x + 4, ly, P.UI_MUTED)
        hline(d, x + 8, x + 11, ly, P.UI_MUTED)


def _inkpot(d, x, y):
    """5x5 잉크병 + 깃펜."""
    d.rectangle((x, y + 1, x + 4, y + 4), fill=P.ROCK_DK)
    hline(d, x + 1, x + 3, y, P.ROCK_MD)
    d.point((x + 1, y + 2), fill=P.ROCK_LT)         # 유리 글린트
    # 깃펜 (꽂힘, 우상향)
    d.line((x + 3, y, x + 6, y - 4), fill=P.UI_CREAM, width=1)
    d.point((x + 6, y - 5), fill=CREAM_SH)


def sprite_research_bench():
    W, H = 64, 32
    img = tile(W, H)
    d = ImageDraw.Draw(img)
    # 상판
    d.rectangle((1, 1, 62, 24), fill=P.WOOD_MD)
    hline(d, 1, 62, 1, P.WOOD_LT)
    vline(d, 1, 1, 24, P.WOOD_LT)
    hline(d, 2, 61, 9, P.WOOD_DK)                   # 상판 널 이음
    hline(d, 2, 61, 17, P.WOOD_DK)
    vline(d, 62, 2, 24, P.WOOD_DK)                  # 우/하 모서리 그늘
    hline(d, 2, 62, 24, P.WOOD_DK)
    hline(d, 6, 10, 13, WOOD_GRAIN)                 # 결 대시
    hline(d, 52, 57, 5, WOOD_GRAIN)
    hline(d, 24, 28, 21, WOOD_GRAIN)
    # 전면 하부: 그늘 + 다리 3
    hline(d, 1, 62, 25, UNDER_DK)
    d.rectangle((1, 26, 62, 30), fill=UNDER_DK)
    for lx in (3, 30, 57):
        d.rectangle((lx, 26, lx + 3, 30), fill=P.WOOD_MD)
        vline(d, lx + 3, 26, 30, P.WOOD_DK)
    # 소품
    put(img, element(13, 6, _scroll), 9, 4)
    put(img, element(14, 9, _paper), 27, 7)
    pot = Image.new("RGBA", (9, 13), (0, 0, 0, 0))
    _inkpot(ImageDraw.Draw(pot), 1, 7)
    put(img, add_outline(pot), 46, 6)
    d.rectangle((0, 0, W - 1, H - 1), outline=P.OUTLINE_OBJ)
    return img


# ── 침대 32x64: 프레임 + 베개 + 담요 (fine: 로열블루 + 골드 띠) ─────────
def _pillow(d, x, y):
    d.rounded_rectangle((x, y, x + 17, y + 8), radius=3, fill=P.UI_CREAM)
    hline(d, x + 2, x + 15, y + 8, CREAM_SH)        # 하단 그늘
    vline(d, x + 17, y + 2, y + 6, CREAM_SH)
    hline(d, x + 5, x + 12, y + 4, CREAM_SH)        # 눌린 자국
def sprite_bed(fine=False):
    W, H = 32, 64
    blanket = ROYAL if fine else P.CLOTH_BLUE
    blanket_dk = ROYAL_DK if fine else P.CLOTH_BLUE_DK
    hem = ROYAL_HEM if fine else BLUE_HEM
    img = tile(W, H)
    d = ImageDraw.Draw(img)
    # 머리판
    d.rectangle((1, 1, 30, 5), fill=P.WOOD_MD)
    hline(d, 1, 30, 1, P.WOOD_LT)
    hline(d, 1, 30, 5, P.WOOD_DK)
    if fine:
        hline(d, 4, 27, 3, P.WOOD_LT)               # 상감 장식선
    # 옆 레일 (좌측이 광원 쪽 — 더 밝음)
    d.rectangle((1, 6, 2, 58), fill=P.WOOD_MD)
    vline(d, 1, 6, 58, P.WOOD_LT)
    d.rectangle((29, 6, 30, 58), fill=P.WOOD_MD)
    vline(d, 30, 6, 58, P.WOOD_DK)
    # 발판
    d.rectangle((1, 59, 30, 62), fill=P.WOOD_MD)
    hline(d, 1, 30, 59, P.WOOD_LT)
    hline(d, 1, 30, 62, P.WOOD_DK)
    # 매트리스 시트
    d.rectangle((3, 6, 28, 58), fill=CREAM_SH)
    hline(d, 3, 28, 6, P.UI_MUTED)                  # 프레임 안 그늘
    vline(d, 3, 6, 58, P.UI_MUTED)
    # 베개
    put(img, element(18, 9, _pillow), 7, 9)
    # 담요 (상단 외곽선으로 또렷한 가장자리)
    d.rectangle((3, 22, 28, 58), fill=blanket)
    hline(d, 3, 28, 21, P.OUTLINE_OBJ)
    hline(d, 3, 28, 22, hem)                        # 접단 미광
    if fine:
        hline(d, 3, 28, 26, P.CROP_GOLD)            # 골드 띠 1px
        d.point((28, 26), fill=GOLD_DK)
    hline(d, 5, 26, 34, blanket_dk)                 # 주름
    hline(d, 7, 28, 44, blanket_dk)
    hline(d, 4, 24, 52, blanket_dk)
    vline(d, 28, 23, 58, blanket_dk)                # 우측 그늘
    hline(d, 3, 28, 57, blanket_dk)
    hline(d, 3, 28, 58, blanket_dk)
    d.rectangle((0, 0, W - 1, H - 1), outline=P.OUTLINE_OBJ)
    return img


# ── 잔디 배경 (프리뷰 합성용, 스펙 §5 저밀도) ────────────────────────────
def grass_tile(rng):
    t = Image.new("RGBA", (CELL, CELL), P.GRASS_MD)
    d = ImageDraw.Draw(t)
    for _ in range(22):
        x = rng.randint(1, 30)
        y = rng.randint(1, 29)
        if rng.random() < 0.55:
            d.line((x, y, x, y + 1), fill=P.GRASS_DK)
        else:
            d.point((x, y), fill=P.GRASS_LT)
    return t


# ── 빌드 ────────────────────────────────────────────────────────────
STRUCTS = [
    ("wall_wood", sprite_wall_wood),
    ("wall_stone", sprite_wall_stone),
    ("floor_wood", sprite_floor_wood),
    ("floor_stone", sprite_floor_stone),
    ("door_wood", sprite_door_wood),
    ("fence", sprite_fence),
    ("fence_gate", sprite_fence_gate),
    ("barricade", sprite_barricade),
    ("stove", sprite_stove),
    ("lamp", sprite_lamp),
    ("research_bench", sprite_research_bench),
    ("bed_wood", lambda: sprite_bed(fine=False)),
    ("bed_fine", lambda: sprite_bed(fine=True)),
]


def main():
    rng = random.Random(SEED)
    sp = {}
    for name, fn in STRUCTS:
        img = fn()
        sp[name] = img
        img.save(os.path.join(OUT, f"struct32_{name}.png"))
        print(f"[ok] struct32_{name}.png ({img.width}x{img.height})")

    # 프리뷰: 10x8 타일 잔디 위 배치 (벽 3연속 / 문-벽 / 바닥 2x2 / 울타리 행)
    COLS, ROWS = 10, 8
    bg = Image.new("RGBA", (COLS * CELL, ROWS * CELL))
    for ty in range(ROWS):
        for tx in range(COLS):
            bg.paste(grass_tile(rng), (tx * CELL, ty * CELL))

    def place(name, cx, cy):
        bg.alpha_composite(sp[name], (cx * CELL, cy * CELL))

    # r0: 벽 3연속 (이음새 검증) — 목재 / 석재
    for c in (0, 1, 2):
        place("wall_wood", c, 0)
    for c in (4, 5, 6):
        place("wall_stone", c, 0)
    place("stove", 8, 0)
    # r1: 벽-문-벽 / 바리케이드 2연속 / 램프
    place("wall_wood", 0, 1)
    place("door_wood", 1, 1)
    place("wall_wood", 2, 1)
    place("barricade", 4, 1)
    place("barricade", 5, 1)
    place("lamp", 8, 1)
    # r2-3: 바닥 2x2 (심리스 검증) / 작업대 / 침대 2종
    for cx, cy in ((0, 2), (1, 2), (0, 3), (1, 3)):
        place("floor_wood", cx, cy)
    for cx, cy in ((3, 2), (4, 2), (3, 3), (4, 3)):
        place("floor_stone", cx, cy)
    place("research_bench", 6, 2)
    place("bed_wood", 8, 2)
    place("bed_fine", 9, 2)
    # r4: 울타리 행 (레일 연속 검증)
    place("fence", 0, 4)
    place("fence_gate", 1, 4)
    place("fence", 2, 4)
    place("stove", 4, 4)
    place("lamp", 5, 4)
    # r5: 단독 배치 (개별 실루엣 점검)
    singles = ["wall_wood", "wall_stone", "floor_wood", "floor_stone",
               "door_wood", "fence", "fence_gate", "barricade", "stove",
               "lamp"]
    for i, name in enumerate(singles):
        place(name, i, 5)
    # r6-7: 멀티타일 단독
    place("research_bench", 0, 6)
    place("bed_wood", 3, 6)
    place("bed_fine", 5, 6)

    upscale(bg).save(os.path.join(OUT, "_preview_struct32.png"))
    print(f"[ok] _preview_struct32.png ({COLS * CELL * SCALE}x{ROWS * CELL * SCALE})")
    print("done.")


if __name__ == "__main__":
    main()
