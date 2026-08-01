# -*- coding: utf-8 -*-
"""_gen_hanok.py — 한국풍(한옥) 정착지 에셋 생성기.  **건축·가구 정본.**

계기 (2026-08-01 운영자): "이제 먼가 특색을 주고 싶은데 머가 있을까?" →
한국풍 정착지 방향 선택 → 시안 승인.

왜 리스킨인가:
  시스템(일감·니즈·연구·습격·건설)은 레퍼런스를 충실히 따라가 완성도가 나왔지만,
  **이 게임만 하는 것이 하나도 없어서** 4초 봤을 때 '축소판'으로 읽혔다.
  세계관을 바꾸는 것이 가장 빠르고 확실한 식별 신호다.

규약 (art-style-guide-v3 준수):
  · 색은 palette.py 의 기준 램프에서만 파생 — 하드코딩 금지.
    황토는 DIRT, 목재는 WOOD, 기와는 ROCK 램프에서 나온다.  그래서 기존 에셋
    (잔디·나무·주민·바위)과 톤이 갈리지 않는다.
  · 광원 좌상단 고정.  외곽선 1px OUTLINE_OBJ.
  · 1x(실제 게임 배율)에서 종류가 즉시 식별될 것 — 낱개 확대로만 판단하지 않는다.

교체 대상은 **파일명을 그대로 덮어쓴다.**  SceneSetup/BuildManager 가 이 경로를
직접 참조하므로 이름을 바꾸면 코드 여러 곳을 손봐야 하고, 한 곳이라도 놓치면
스프라이트가 조용히 사라진다(이 레포에서 반복된 실패 유형).  '나무 벽' 이라는
이름은 심벽(기둥+흙)에도 여전히 맞는다.

usage:
  python _gen_hanok.py            # Assets/Sprites 에 직접 반입
  python _gen_hanok.py --stage    # G:/ai/_hanok_stage 에만 출력 (검수용)
"""
from __future__ import annotations
import sys
import os
import colorsys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import palette as P  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402

CELL = 32


def shade(c, dv=0.0, s=None):
    r, g, b, a = c
    h, s0, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    if s is not None:
        s0 = s
    v = max(0.0, min(1.0, v * (1.0 + dv)))
    r2, g2, b2 = colorsys.hsv_to_rgb(h, s0, v)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


# ── 한옥 파생색 (전부 기준 램프에서 파생) ──────────────────────────────
LOAM_MD = shade(P.DIRT_LT, +0.06, s=0.30)     # 황토벽 면
LOAM_LT = shade(LOAM_MD, +0.10)
LOAM_DK = shade(LOAM_MD, -0.14)
STRAW = shade(P.CROP_GOLD, -0.18, s=0.42)     # 지푸라기 / 새끼줄
POST_MD = P.WOOD_DK                           # 기둥
POST_LT = shade(P.WOOD_DK, +0.18)
TILE_MD = shade(P.ROCK_MD, -0.08, s=0.16)     # 기와 (푸른기 회색)
TILE_DK = shade(TILE_MD, -0.18)
TILE_LT = shade(TILE_MD, +0.16)
HANJI = shade(P.UI_CREAM, -0.06, s=0.10)      # 한지 (창호)
IRON_DK = shade(P.ROCK_DK, -0.25, s=0.10)     # 무쇠 (가마솥)
IRON_LT = shade(IRON_DK, +0.35)
JAR_MD = shade(P.WOOD_DK, -0.10, s=0.45)      # 옹기
JAR_LT = shade(JAR_MD, +0.22)
INDIGO = shade(P.CLOTH_BLUE, -0.30, s=0.55)   # 남색 무명 이불
INDIGO_DK = shade(INDIGO, -0.22)
MUSLIN = shade(P.CLOTH_LINEN, +0.02, s=0.12)  # 무명 요
SILK_RED = shade(P.CLOTH_RUST, -0.16, s=0.58)  # 비단 (고급 이불)
SILK_DK = shade(SILK_RED, -0.22)
BRASS = shade(P.CROP_GOLD, -0.22, s=0.30)     # 놋그릇


def hline(d, x0, x1, y, c):
    d.line((x0, y, x1, y), fill=c)


def vline(d, x, y0, y1, c):
    d.line((x, y0, x, y1), fill=c)


def tile(w=CELL, h=CELL):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


# ══ 벽 ═══════════════════════════════════════════════════════════════
def wall_wood():
    """심벽 — 기둥 사이에 황토를 채운 벽 + 기와 처마.

    타일링 규약: 기둥 주기 16, 기왓골 주기 4 → 인접 타일과 무늬가 이어진다.
    외곽선은 상하만 (좌우는 벽체색으로 연속)."""
    img = tile()
    d = ImageDraw.Draw(img)
    d.rectangle((0, 1, 31, 4), fill=TILE_MD)                 # 기와 처마
    for x in range(0, 32, 4):
        d.rectangle((x, 1, x + 1, 4), fill=TILE_DK)          # 골
        vline(d, x + 2, 1, 3, TILE_LT)                       # 기왓마루 미광
        d.point((x + 2, 4), fill=TILE_MD)
    hline(d, 0, 31, 5, shade(TILE_DK, -0.35))                # 처마 밑 그늘
    d.rectangle((0, 6, 31, 30), fill=LOAM_MD)                # 황토 면
    d.rectangle((0, 29, 31, 30), fill=LOAM_DK)               # 접지 그늘
    for sx, sy, ln in ((9, 10, 4), (14, 18, 3), (25, 13, 3), (11, 24, 3), (22, 26, 4)):
        hline(d, sx, sx + ln, sy, STRAW)                     # 지푸라기 결
    for px in (2, 18):                                       # 기둥
        d.rectangle((px, 6, px + 3, 30), fill=POST_MD)
        vline(d, px, 6, 30, POST_LT)
        vline(d, px + 3, 6, 30, shade(POST_MD, -0.30))
        for ky in (11, 20, 27):
            d.point((px + 1, ky), fill=shade(POST_LT, -0.10))
    hline(d, 0, 31, 0, P.OUTLINE_OBJ)
    hline(d, 0, 31, 31, P.OUTLINE_OBJ)
    return img


def wall_stone():
    """돌담 — 막돌을 **황토로 물린** 담장 + 기와 처마.

    검수 1차 문제: 줄눈을 회색으로 두었더니 현대식 조적벽으로 읽혔고
    기와 처마가 회색 바탕에 묻혔다.  한국 돌담의 식별 신호는 ‘둥글고
    제각각인 막돌 + 누런 황토 줄눈’ 이다 — 줄눈을 흥색으로 바꾸면
    나무벽과 같은 마을의 건축으로 묶이면서 재질만 구분된다."""
    img = tile()
    d = ImageDraw.Draw(img)
    d.rectangle((0, 1, 31, 4), fill=TILE_MD)
    for x in range(0, 32, 4):
        d.rectangle((x, 1, x + 1, 4), fill=TILE_DK)
        vline(d, x + 2, 1, 3, TILE_LT)
        d.point((x + 2, 4), fill=TILE_MD)
    hline(d, 0, 31, 5, shade(TILE_DK, -0.35))
    d.rectangle((0, 6, 31, 30), fill=LOAM_DK)                # 황토 줄눈 (회색 → 흥색)
    # 막돌 — 크기·위치 불규칙, 주기 16 으로 타일링 연속
    stones = [(4, 10, 6, 4), (14, 9, 5, 4), (24, 11, 6, 4),
              (7, 18, 6, 4), (18, 17, 6, 5), (28, 19, 4, 4),
              (3, 26, 5, 3), (13, 25, 6, 4), (24, 26, 6, 3)]
    for cx, cy, rw, rh in stones:
        d.ellipse((cx - rw, cy - rh, cx + rw, cy + rh), fill=P.ROCK_MD)
        d.arc((cx - rw + 1, cy - rh + 1, cx + rw - 1, cy + rh - 1), 170, 300, fill=P.ROCK_LT)
        d.arc((cx - rw, cy - rh, cx + rw, cy + rh), 20, 150, fill=P.ROCK_DK)
    hline(d, 0, 31, 0, P.OUTLINE_OBJ)
    hline(d, 0, 31, 31, P.OUTLINE_OBJ)
    return img


# ══ 바닥 ═════════════════════════════════════════════════════════════
def floor_wood():
    """대청마루 — 긴 널 3장.  세로 이음을 두지 않아 타일 격자가 안 보인다."""
    img = Image.new("RGBA", (CELL, CELL), P.WOOD_MD)
    d = ImageDraw.Draw(img)
    for y in (10, 21):
        hline(d, 0, 31, y, shade(P.WOOD_MD, -0.22))
        hline(d, 0, 31, y + 1, shade(P.WOOD_MD, +0.07))
    for gy, x0, x1 in ((4, 2, 14), (6, 19, 29), (15, 5, 21), (17, 24, 31),
                       (26, 1, 12), (28, 16, 27)):
        hline(d, x0, x1, gy, shade(P.WOOD_MD, -0.09))
    return img


def floor_stone():
    """전돌 마당 — 황토를 구운 네모 벙돌을 엇갈려 깔았다.

    검수 1차 문제: 순회색이라 현대 보도블록처럼 보였다.
    전돌은 흙을 구운 것이라 붉은빛 도는 황토색이 맞고,
    그래야 마루·황토벽과 같은 마을로 읽힌다."""
    base = shade(LOAM_MD, -0.22)
    img = Image.new("RGBA", (CELL, CELL), shade(base, -0.18))   # 줄눈
    d = ImageDraw.Draw(img)
    for row, y in enumerate((0, 11, 22)):
        off = 0 if row % 2 == 0 else 8
        for x in range(-8 + off, 32, 16):
            d.rectangle((x + 1, y + 1, x + 14, y + 9), fill=base)
            hline(d, x + 2, x + 13, y + 1, shade(base, +0.12))
            hline(d, x + 2, x + 13, y + 9, shade(base, -0.14))
            d.point((x + 4, y + 5), fill=shade(base, +0.06))
    return img


# ══ 문 ═══════════════════════════════════════════════════════════════
def door_wood():
    """창호문 — 벽 스텁 승계 + 한지 문짝 + 격자 살.

    1x 에서 이 게임이 무엇인지 가장 빨리 말해 주는 조각이라 대비를 세게 준다."""
    img = wall_wood()
    d = ImageDraw.Draw(img)
    d.rectangle((4, 6, 27, 30), fill=P.WOOD_DK)              # 문틀
    d.rectangle((6, 8, 25, 28), fill=HANJI)                  # 한지
    for gx in (11, 16, 21):
        vline(d, gx, 8, 28, shade(P.WOOD_MD, -0.06))
    for gy in (13, 18, 23):
        hline(d, 6, 25, gy, shade(P.WOOD_MD, -0.06))
    d.rectangle((14, 19, 17, 21), fill=shade(P.ROCK_LT, -0.15))   # 문고리
    return img


# ══ 살림 ═════════════════════════════════════════════════════════════
def stove():
    """부뚜막 + 가마솥 — 아궁이 불이 보인다."""
    img = tile()
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((3, 8, 28, 29), radius=2, fill=LOAM_MD, outline=P.OUTLINE_OBJ)
    hline(d, 5, 26, 9, LOAM_LT)
    d.rectangle((4, 26, 27, 28), fill=LOAM_DK)
    d.rounded_rectangle((10, 19, 21, 27), radius=3, fill=shade(P.ROCK_DK, -0.3))
    d.ellipse((12, 22, 19, 27), fill=P.FIRE_OR)
    d.ellipse((14, 24, 17, 27), fill=P.FIRE_LT)
    d.ellipse((6, 4, 25, 17), fill=IRON_DK, outline=P.OUTLINE_OBJ)     # 가마솥
    d.ellipse((8, 5, 23, 13), fill=shade(IRON_DK, +0.18))
    d.arc((9, 5, 22, 12), 200, 340, fill=IRON_LT)
    d.rectangle((14, 2, 17, 5), fill=P.WOOD_MD)
    d.point((14, 2), fill=P.WOOD_LT)
    return img


def research_bench():
    """서안 — 좌식 책상 + 한지 두루마리 + 벼루·붓 (64x32, 2x1)."""
    img = tile(64, 32)
    d = ImageDraw.Draw(img)
    d.rectangle((2, 10, 61, 24), fill=P.WOOD_MD, outline=P.OUTLINE_OBJ)
    hline(d, 3, 60, 11, P.WOOD_LT)
    hline(d, 3, 60, 23, P.WOOD_DK)
    for lx in (5, 56):
        d.rectangle((lx, 24, lx + 3, 29), fill=P.WOOD_DK)
    d.rounded_rectangle((8, 13, 24, 20), radius=2, fill=HANJI, outline=shade(HANJI, -0.18))
    hline(d, 10, 22, 16, shade(HANJI, -0.12))
    d.rounded_rectangle((27, 14, 38, 19), radius=2, fill=shade(HANJI, -0.04))
    d.rounded_rectangle((43, 14, 53, 20), radius=1, fill=shade(P.ROCK_DK, -0.1))
    d.ellipse((45, 15, 51, 19), fill=shade(P.ROCK_DK, -0.35))
    d.line((55, 20, 59, 12), fill=P.WOOD_LT, width=1)
    d.point((59, 12), fill=shade(P.HAIR_DK, +0.1))
    return img


def lamp():
    """등잔 — 나무 등경 + 기름접시 + 불꽃."""
    img = tile()
    d = ImageDraw.Draw(img)
    d.rectangle((14, 14, 17, 29), fill=P.WOOD_DK)
    vline(d, 14, 14, 29, shade(P.WOOD_DK, +0.20))
    d.ellipse((9, 25, 22, 30), fill=P.WOOD_MD, outline=P.OUTLINE_OBJ)
    d.ellipse((9, 10, 22, 16), fill=shade(P.ROCK_LT, -0.05), outline=P.OUTLINE_OBJ)
    d.ellipse((12, 4, 19, 13), fill=P.FIRE_OR)
    d.ellipse((14, 6, 17, 12), fill=P.FIRE_LT)
    return img


def table_chair():
    """소반 — 낮은 원형 밥상 + 놋그릇."""
    img = tile()
    d = ImageDraw.Draw(img)
    d.ellipse((3, 8, 28, 26), fill=P.WOOD_MD, outline=P.OUTLINE_OBJ)
    d.arc((4, 9, 27, 25), 170, 320, fill=P.WOOD_LT)
    d.ellipse((6, 11, 24, 23), fill=shade(P.WOOD_MD, -0.08))
    for cx, cy in ((12, 15), (20, 18)):
        d.ellipse((cx - 4, cy - 3, cx + 4, cy + 3), fill=BRASS)
        d.ellipse((cx - 3, cy - 2, cx + 3, cy + 1), fill=shade(BRASS, +0.16))
    for lx in (7, 23):
        d.rectangle((lx, 25, lx + 2, 29), fill=P.WOOD_DK)
    return img


# ══ 울타리 · 방어 ═════════════════════════════════════════════════════
def fence():
    """싸리울 — 가는 싸리가지를 가로로 엮은 울."""
    img = tile()
    d = ImageDraw.Draw(img)
    for px in (5, 25):
        d.rectangle((px, 6, px + 2, 29), fill=P.WOOD_DK)
        vline(d, px, 6, 29, shade(P.WOOD_DK, +0.18))
    for ry in (11, 18, 25):
        hline(d, 0, 31, ry, shade(P.WOOD_MD, -0.06))
        hline(d, 0, 31, ry + 1, P.WOOD_DK)
    for sx in range(2, 32, 6):
        vline(d, sx, 8, 28, shade(P.WOOD_LT, -0.22))
    return img


def fence_gate():
    """사립문 — 싸리울에 여닫는 문짝 (기둥 사이가 비어 통로로 읽힌다)."""
    img = tile()
    d = ImageDraw.Draw(img)
    for px in (3, 27):
        d.rectangle((px, 4, px + 2, 29), fill=P.WOOD_DK)
        vline(d, px, 4, 29, shade(P.WOOD_DK, +0.18))
    d.rectangle((8, 10, 23, 27), fill=shade(P.WOOD_MD, -0.10),
                outline=P.OUTLINE_OBJ)                        # 문짝
    for sx in range(10, 23, 4):
        vline(d, sx, 11, 26, shade(P.WOOD_LT, -0.25))
    hline(d, 9, 22, 14, P.WOOD_DK)
    hline(d, 9, 22, 23, P.WOOD_DK)
    d.rectangle((21, 18, 23, 20), fill=STRAW)                 # 새끼 고리
    return img


def barricade():
    """방책 — 끝을 깎은 통나무를 비스듬히 박은 목책."""
    img = tile()
    d = ImageDraw.Draw(img)
    d.rectangle((0, 22, 31, 27), fill=P.WOOD_DK)              # 가로 띠
    hline(d, 0, 31, 22, shade(P.WOOD_MD, +0.05))
    for bx in (3, 11, 19, 27):
        d.polygon([(bx, 29), (bx + 4, 29), (bx + 6, 12), (bx + 2, 10)],
                  fill=P.WOOD_MD, outline=P.OUTLINE_OBJ)
        d.line((bx + 1, 28, bx + 3, 12), fill=P.WOOD_LT)
    return img


def grave():
    """봉분 — 흙 무덤 + 상석 (1x2)."""
    img = tile(32, 64)
    d = ImageDraw.Draw(img)
    d.ellipse((2, 8, 29, 44), fill=shade(P.GRASS_DK, +0.05))  # 잔디 덮인 봉분
    d.arc((4, 10, 27, 42), 170, 320, fill=P.GRASS_MD)
    for tx, ty in ((9, 20), (18, 16), (23, 28), (12, 33)):
        d.point((tx, ty), fill=P.GRASS_LT)
    d.rectangle((10, 46, 21, 56), fill=P.ROCK_MD, outline=P.OUTLINE_OBJ)  # 상석
    hline(d, 11, 20, 47, P.ROCK_LT)
    for ly in (50, 53):
        hline(d, 13, 18, ly, P.ROCK_DK)
    return img


# ══ 잠자리 ═══════════════════════════════════════════════════════════
def bed_spot():
    """짚자리 — 맨바닥 잠자리 (32x64).

    검수 1차 문제: 평면 노란 사각형이라 '노란 상자' 로 보였다.
    거적은 (a) 가로세로 엮음결 (b) 말린 끝단 두 가지로 읽힌다."""
    img = tile(32, 64)
    d = ImageDraw.Draw(img)
    body = shade(STRAW, -0.06)
    d.rounded_rectangle((3, 10, 28, 55), radius=2, fill=body, outline=P.OUTLINE_OBJ)
    # 엮음결 — 가로살 위로 세로살이 번갈아 지나가는 무늬
    for i, y in enumerate(range(13, 55, 4)):
        hline(d, 4, 27, y, shade(body, -0.20))
        for x in range(5 + (i % 2) * 3, 28, 6):
            d.point((x, y), fill=shade(body, +0.16))
            d.point((x + 1, y - 1), fill=shade(body, +0.10))
    # 말린 끝단 (위·아래)
    for ey in (10, 54):
        d.rounded_rectangle((3, ey - 1, 28, ey + 2), radius=1, fill=shade(body, -0.28))
        hline(d, 5, 26, ey, shade(body, +0.10))
    return img


def _bed_hi(fine: bool):
    """요·이불 (128x256, PPU128).

    벽(32px/칸)보다 4배 밀도라 곡선을 부드럽게 낼 수 있다 — 기존 침대가 그랬으므로
    같은 밀도로 그려야 나란히 놓았을 때 이질감이 없다 (스타일가이드 §4-6)."""
    W, H = 128, 256
    img = tile(W, H)
    d = ImageDraw.Draw(img)
    quilt = SILK_RED if fine else INDIGO
    quilt_dk = SILK_DK if fine else INDIGO_DK

    # 요 (무명 매트) — 전체 바탕
    d.rounded_rectangle((8, 14, 119, 242), radius=12, fill=MUSLIN,
                        outline=P.OUTLINE_OBJ, width=3)
    d.rounded_rectangle((14, 20, 113, 236), radius=9, outline=shade(MUSLIN, -0.10))

    # 목침 (나무 베개) — 위쪽
    d.rounded_rectangle((34, 32, 93, 66), radius=7, fill=P.WOOD_MD,
                        outline=P.OUTLINE_OBJ, width=3)
    d.rounded_rectangle((40, 36, 87, 48), radius=5, fill=P.WOOD_LT)
    vline(d, 63, 40, 62, shade(P.WOOD_MD, -0.16))
    vline(d, 64, 40, 62, shade(P.WOOD_MD, -0.16))

    # 이불 — 아래 2/3
    d.rounded_rectangle((12, 88, 115, 238), radius=9, fill=quilt)
    d.rectangle((12, 88, 115, 100), fill=quilt)
    hline(d, 12, 115, 86, P.OUTLINE_OBJ)
    hline(d, 12, 115, 87, P.OUTLINE_OBJ)
    for i in range(3):                                        # 동정 (흰 깃)
        hline(d, 13, 114, 88 + i, MUSLIN)
    for fy in (126, 160, 194, 224):                           # 누비 주름
        hline(d, 20, 107, fy, quilt_dk)
        hline(d, 20, 107, fy + 1, shade(quilt, +0.10))
    d.rectangle((108, 92, 114, 236), fill=quilt_dk)           # 우측 그늘
    d.rectangle((12, 230, 115, 238), fill=quilt_dk)
    if fine:                                                  # 비단: 금박 띠
        for gy in (104, 112):
            hline(d, 16, 111, gy, shade(P.CROP_GOLD, -0.05, s=0.42))
    return img


def bed_wood():
    return _bed_hi(False)


def bed_fine():
    return _bed_hi(True)


# ══ 신규: 장독대 / 당산나무 ═══════════════════════════════════════════
def jangdokdae():
    """장독대 — 돌단 위 옹기 항아리.  저장 구역 데코."""
    img = tile()
    d = ImageDraw.Draw(img)
    d.rectangle((1, 22, 30, 29), fill=P.ROCK_MD, outline=P.OUTLINE_OBJ)
    hline(d, 2, 29, 23, P.ROCK_LT)
    hline(d, 2, 29, 28, P.ROCK_DK)
    for cx, cy, rw, rh in ((9, 15, 7, 8), (21, 13, 8, 9), (28, 18, 5, 6)):
        d.ellipse((cx - rw, cy - rh, cx + rw, cy + rh), fill=JAR_MD, outline=P.OUTLINE_OBJ)
        d.arc((cx - rw + 1, cy - rh + 1, cx + rw - 1, cy + rh - 1), 165, 285, fill=JAR_LT)
        d.ellipse((cx - rw + 2, cy - rh - 1, cx + rw - 2, cy - rh + 3),
                  fill=shade(JAR_MD, -0.30), outline=P.OUTLINE_OBJ)
        d.ellipse((cx - rw + 3, cy - rh - 1, cx + rw - 3, cy - rh + 2),
                  fill=shade(P.ROCK_LT, -0.10))
    return img


def dangsan_tree():
    """당산나무 — 마을 중심의 노거수.  금줄(새끼줄 + 한지)로 표시. (64x96)"""
    img = tile(64, 96)
    d = ImageDraw.Draw(img)
    d.polygon([(24, 95), (40, 95), (37, 58), (27, 58)], fill=P.WOOD_DK)
    d.polygon([(27, 95), (31, 95), (30, 60), (27, 60)], fill=shade(P.WOOD_DK, +0.22))
    for rx in (18, 46):
        d.line((32, 92, rx, 95), fill=P.WOOD_DK, width=3)
    for cx, cy, r in ((32, 34, 28), (14, 46, 16), (50, 46, 16), (32, 18, 19)):
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=P.GRASS_DK)
    for cx, cy, r in ((28, 30, 21), (17, 44, 12), (47, 43, 12), (33, 17, 14)):
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=P.GRASS_MD)
    for cx, cy, r in ((23, 23, 12), (15, 40, 7), (43, 35, 8)):
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=P.GRASS_LT)
    d.rectangle((22, 64, 42, 68), fill=STRAW)                 # 금줄
    hline(d, 22, 42, 64, shade(STRAW, +0.14))
    for hx in (25, 31, 37):                                   # 한지 조각
        d.rectangle((hx, 68, hx + 3, 75), fill=HANJI)
    return img


def table_chair_16():
    """소반 — 16x16 저해상도.  기존 table_chair.png 규격을 따른다.
    이 크기에선 디테일이 안 들어가므로 **둥근 상판 + 놓그릇 두 점**으로
    실루에을 단순화한다 (1x 에서 '밥상' 으로 읽히면 충분)."""
    img = tile(16, 16)
    d = ImageDraw.Draw(img)
    d.ellipse((1, 3, 14, 13), fill=P.WOOD_MD, outline=P.OUTLINE_OBJ)
    d.arc((2, 4, 13, 12), 170, 320, fill=P.WOOD_LT)
    for cx, cy in ((6, 7), (10, 9)):
        d.ellipse((cx - 2, cy - 1, cx + 2, cy + 1), fill=BRASS)
    for lx in (3, 11):
        d.rectangle((lx, 12, lx + 1, 15), fill=P.WOOD_DK)
    return img


def grave_empty():
    """빈 광 — 파 놓은 자리 + 상석 (128x256)."""
    img = tile(128, 256)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((18, 60, 110, 190), radius=10,
                        fill=shade(LOAM_DK, -0.10), outline=P.OUTLINE_OBJ, width=3)
    d.rounded_rectangle((28, 72, 100, 178), radius=8, fill=shade(LOAM_DK, -0.28))
    d.rectangle((40, 196, 88, 232), fill=P.ROCK_MD, outline=P.OUTLINE_OBJ, width=3)
    hline(d, 44, 84, 200, P.ROCK_LT)
    return img


def grave_mound():
    """봉분 — 잔디 덮인 둥근 무덤 + 상석 (128x256)."""
    img = tile(128, 256)
    d = ImageDraw.Draw(img)
    d.ellipse((8, 30, 120, 190), fill=shade(P.GRASS_DK, +0.04), outline=P.OUTLINE_OBJ, width=3)
    d.arc((16, 38, 112, 182), 165, 320, fill=P.GRASS_MD)
    d.ellipse((30, 52, 98, 140), fill=P.GRASS_MD)
    for tx, ty in ((44, 80), (70, 66), (88, 108), (52, 126), (76, 140)):
        d.ellipse((tx, ty, tx + 5, ty + 4), fill=P.GRASS_LT)
    d.rectangle((40, 196, 88, 232), fill=P.ROCK_MD, outline=P.OUTLINE_OBJ, width=3)
    hline(d, 44, 84, 200, P.ROCK_LT)
    for ly in (210, 220):
        hline(d, 50, 78, ly, P.ROCK_DK)
    return img


# ══ 출력 표 ══════════════════════════════════════════════════════════
SPRITES = [
    ("struct32_wall_wood", wall_wood),
    ("struct32_wall_stone", wall_stone),
    ("struct32_floor_wood", floor_wood),
    ("struct32_floor_stone", floor_stone),
    ("struct32_door_wood", door_wood),
    ("struct32_stove", stove),
    ("struct32_research_bench", research_bench),
    ("struct32_lamp", lamp),
    ("struct32_fence", fence),
    ("struct32_fence_gate", fence_gate),
    ("struct32_barricade", barricade),
    ("struct32_bed_spot", bed_spot),
    ("struct64_bed_wood", bed_wood),
    ("struct64_bed_fine", bed_fine),
    # 신규 (기존 파일 없음 — 배선은 후속)
    ("table_chair", table_chair_16),
    ("grave64_empty", grave_empty),
    ("grave64_mound", grave_mound),
    ("hanok_jangdokdae", jangdokdae),
    ("hanok_dangsan_tree", dangsan_tree),
]


# 일부 에셋은 Resources/ 아래에 있다 (런타임 Resources.Load 로 읽는 것들).
#  같은 이름의 파일이 두 곳에 생기면 어느 쪽이 쓰이는지 알 수 없게 되므로
#  **원래 있던 자리에 그대로** 덮어쓴다.
RESOURCES_SPRITES = os.path.normpath(os.path.join(HERE, "..", "Resources", "Sprites"))
DEST_OVERRIDE = {
    "grave64_empty": RESOURCES_SPRITES,
    "grave64_mound": RESOURCES_SPRITES,
}


def main() -> int:
    stage = "--stage" in sys.argv
    for name, fn in SPRITES:
        if stage:
            out = r"G:/ai/_hanok_stage"
        else:
            out = DEST_OVERRIDE.get(name, HERE)
        os.makedirs(out, exist_ok=True)
        img = fn()
        img.save(os.path.join(out, f"{name}.png"))
        print(f"[ok] {name}.png ({img.width}x{img.height}) → {os.path.basename(out)}")
    print(f"{'(검수용) ' if stage else ''}{len(SPRITES)}종 반영")
    return 0


if __name__ == "__main__":
    sys.exit(main())
