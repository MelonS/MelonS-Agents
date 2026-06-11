# -*- coding: utf-8 -*-
"""_gen_items32.py — Art v2 드롭 아이템 생성기 (32px 칸, 드로잉 ~20x16).

스펙: G:/ai/_artv2_staging/artv2-style-spec.md §7
팔레트: Assets/Sprites/palette.py (단일 진실 소스 — import만, 하드코딩 금지)

아이템 5종 x 스택 3단계(s1/s2/s3), 시트 96x32:
  item_wood  — 통나무 가로쌓기 1/3/5단, 단면 LT 원 + 나이테 1px
  item_stone — 각진 돌덩이 1/2/4개 클러스터, 윗면 ROCK_LT 하이라이트
  item_meat  — 고기 슬랩 + SKIN_SH 지방선
  item_meal  — 간편식 캔/팩 (UI_CREAM + CROP_GOLD 띠)
  item_berry — 그릇 없이 알알이 무더기 (MEAT_RED — DANGER_RED 금지)

규칙 준수: 외곽 OUTLINE_OBJ 1px(요소별 — 스택 내부 분리), 좌상 조명,
디더 금지(플랫+램프), 스택양은 스프라이트 단계로 표현(스케일 변경 금지),
하단 중앙 정렬.
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


# ── 팔레트 파생 (스펙 §1: 램프 앵커 HSV 파생, 명도 ±12% 이내) ──────────
def shade(c, dv):
    r, g, b, a = c
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    v = max(0.0, min(1.0, v * (1.0 + dv)))
    r2, g2, b2 = colorsys.hsv_to_rgb(h, s, v)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


MEAT_DK = shade(P.MEAT_RED, -0.12)   # 베리/고기 그늘면
MEAT_LT = shade(P.MEAT_RED, +0.12)   # 베리 하이라이트
CREAM_SH = shade(P.UI_CREAM, -0.10)  # 포장 그늘
GOLD_DK = shade(P.CROP_GOLD, -0.10)  # 띠 그늘


# ── 공통 헬퍼 ────────────────────────────────────────────────────────
def add_outline(img, color=P.OUTLINE_OBJ):
    """본체 바깥 4-이웃 1px 외곽선 (픽셀아트 컷코너)."""
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
    """요소를 자체 레이어(w+2,h+2)에 그리고 외곽선 후 반환.

    스택 안 요소끼리 1px 외곽으로 분리 — 덩어리 뭉개짐 방지.
    """
    img = Image.new("RGBA", (w + 2, h + 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    draw_fn(d, 1, 1, *args)
    return add_outline(img)


def put(canvas, elem, x, y):
    """elem 의 본체 좌상이 (x,y)에 오도록 합성 (외곽선 1px 보정)."""
    canvas.alpha_composite(elem, (x - 1, y - 1))


def new_sprite():
    return Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))


def upscale(img, s=SCALE):
    return img.resize((img.width * s, img.height * s), Image.NEAREST)


# ── 목재: 통나무 (몸통 h6, 우측 단면 LT 원 + 나이테 1px) ─────────────
def _log(d, x, y, ln):
    h = 6
    d.rounded_rectangle((x, y, x + ln - 1, y + h - 1), radius=2, fill=P.WOOD_MD)
    d.line((x + 2, y, x + ln - 7, y), fill=P.WOOD_LT)                 # 좌상광
    d.line((x + 1, y + h - 1, x + ln - 7, y + h - 1), fill=P.WOOD_DK)  # 하단 그늘
    d.line((x, y + 2, x, y + 3), fill=P.WOOD_LT)                      # 좌측면 광
    d.line((x + ln - 7, y + 1, x + ln - 7, y + 4), fill=P.WOOD_DK)    # 단면 경계
    # 우측 단면: LT 원 + 1px WOOD_MD 나이테 링
    cx = x + ln - 6
    d.ellipse((cx, y, cx + 5, y + 5), fill=P.WOOD_LT)
    d.ellipse((cx + 1, y + 1, cx + 4, y + 4), outline=P.WOOD_MD, width=1)


def log_elem(ln):
    return element(ln, 6, _log, ln)


def sprite_wood(stage):
    img = new_sprite()
    if stage == 0:        # 1단
        put(img, log_elem(18), 7, 24)
    elif stage == 1:      # 3단: 뒤1 + 위1 + 앞1
        put(img, log_elem(18), 9, 21)
        put(img, log_elem(16), 7, 18)
        put(img, log_elem(18), 6, 24)
    else:                 # 5단: 아래열 3(뒤→앞) + 윗열 2
        put(img, log_elem(16), 11, 18)
        put(img, log_elem(18), 9, 21)
        put(img, log_elem(14), 9, 15)
        put(img, log_elem(16), 7, 18)
        put(img, log_elem(18), 6, 24)
    return img


# ── 석재: 각진 다면체 (윗면 LT / 정면 MD / 우측 DK) ──────────────────
def _poly(d, ox, oy, pts, fill):
    d.polygon([(ox + x, oy + y) for x, y in pts], fill=fill)


def _rock_big(d, x, y):
    """14x10 — 윗면 LT 평면 크게, 직선 모서리로 각진 느낌."""
    _poly(d, x, y, [(0, 6), (3, 1), (9, 0), (13, 4), (13, 8), (10, 9), (2, 9)],
          P.ROCK_MD)
    _poly(d, x, y, [(0, 6), (3, 1), (9, 0), (13, 4), (9, 5), (3, 6)], P.ROCK_LT)
    _poly(d, x, y, [(13, 4), (13, 8), (10, 9), (9, 5)], P.ROCK_DK)
    d.line((x + 5, y + 6, x + 4, y + 9), fill=P.ROCK_DK)  # 각진 균열 1px


def _rock_mid(d, x, y):
    """10x7."""
    _poly(d, x, y, [(0, 4), (3, 0), (7, 0), (9, 3), (9, 6), (2, 6)],
          P.ROCK_MD)
    _poly(d, x, y, [(0, 4), (3, 0), (7, 0), (9, 3), (6, 3), (2, 4)], P.ROCK_LT)
    _poly(d, x, y, [(9, 3), (9, 6), (6, 6), (6, 3)], P.ROCK_DK)


def _rock_small(d, x, y):
    """7x5."""
    _poly(d, x, y, [(0, 3), (2, 0), (6, 1), (6, 4), (1, 4)], P.ROCK_MD)
    _poly(d, x, y, [(0, 3), (2, 0), (6, 1), (3, 2)], P.ROCK_LT)
    d.line((x + 6, y + 2, x + 6, y + 4), fill=P.ROCK_DK)


ROCK_BIG = (14, 10, _rock_big)
ROCK_MID = (10, 7, _rock_mid)
ROCK_SMALL = (7, 5, _rock_small)


def rock_elem(kind):
    w, h, fn = kind
    return element(w, h, fn)


def sprite_stone(stage):
    img = new_sprite()
    if stage == 0:        # 1개
        put(img, rock_elem(ROCK_BIG), 9, 20)
    elif stage == 1:      # 2개
        put(img, rock_elem(ROCK_BIG), 7, 20)
        put(img, rock_elem(ROCK_MID), 18, 23)
    else:                 # 4개 (높이 증가: 위에 1개 얹음)
        put(img, rock_elem(ROCK_MID), 5, 22)
        put(img, rock_elem(ROCK_MID), 20, 23)
        put(img, rock_elem(ROCK_BIG), 10, 20)
        put(img, rock_elem(ROCK_SMALL), 13, 15)
    return img


# ── 고기: 슬랩 + 지방선(SKIN_SH) ─────────────────────────────────────
def _slab(d, x, y, w=14, h=8):
    d.rounded_rectangle((x, y, x + w - 1, y + h - 1), radius=2, fill=P.MEAT_RED)
    d.line((x + 1, y + h - 1, x + w - 3, y + h - 1), fill=MEAT_DK)   # 하단 그늘
    d.line((x + w - 1, y + 2, x + w - 1, y + h - 3), fill=MEAT_DK)   # 우측 그늘
    d.line((x + 2, y, x + w - 3, y), fill=P.SKIN_SH)                 # 상단 지방 캡
    d.point((x + 1, y + 1), fill=P.SKIN_SH)
    d.line((x + 3, y + 3, x + 6, y + 3), fill=P.SKIN_SH)             # 마블링 1
    d.line((x + 8, y + 5, x + 11, y + 5), fill=P.SKIN_SH)            # 마블링 2


def slab_elem():
    return element(14, 8, _slab)


def sprite_meat(stage):
    img = new_sprite()
    if stage == 0:
        put(img, slab_elem(), 9, 22)
    elif stage == 1:
        put(img, slab_elem(), 12, 19)
        put(img, slab_elem(), 7, 22)
    else:
        put(img, slab_elem(), 13, 21)
        put(img, slab_elem(), 6, 23)
        put(img, slab_elem(), 9, 16)
    return img


# ── 간편식: 캔(원통) + 팩(납작 상자) ─────────────────────────────────
def _can(d, x, y):
    """7x9."""
    d.rectangle((x, y + 1, x + 6, y + 8), fill=P.UI_CREAM)
    d.rectangle((x + 1, y, x + 5, y, ), fill=P.ROCK_LT)        # 뚜껑 윗면
    d.rectangle((x, y + 1, x + 6, y + 1), fill=P.ROCK_LT)
    d.line((x + 1, y + 2, x + 5, y + 2), fill=P.ROCK_MD)       # 뚜껑 림
    d.rectangle((x, y + 4, x + 6, y + 5), fill=P.CROP_GOLD)    # 라벨 띠
    d.point((x + 6, y + 4), fill=GOLD_DK)
    d.point((x + 6, y + 5), fill=GOLD_DK)
    d.line((x + 1, y + 8, x + 5, y + 8), fill=CREAM_SH)        # 바닥 그늘
    d.line((x + 6, y + 2, x + 6, y + 7), fill=CREAM_SH)        # 우측 그늘


def _pack(d, x, y):
    """10x6 납작 팩."""
    d.rounded_rectangle((x, y, x + 9, y + 5), radius=1, fill=P.UI_CREAM)
    d.rectangle((x + 4, y, x + 5, y + 5), fill=P.CROP_GOLD)    # 세로 띠
    d.line((x + 5, y + 1, x + 5, y + 5), fill=GOLD_DK)
    d.line((x + 1, y + 5, x + 3, y + 5), fill=CREAM_SH)
    d.line((x + 6, y + 5, x + 8, y + 5), fill=CREAM_SH)
    d.line((x + 9, y + 1, x + 9, y + 4), fill=CREAM_SH)
    d.line((x + 1, y + 1, x + 2, y + 1), fill=CREAM_SH)        # 봉인 접합선


def can_elem():
    return element(7, 9, _can)


def pack_elem():
    return element(10, 6, _pack)


def sprite_meal(stage):
    img = new_sprite()
    if stage == 0:        # 캔 1
        put(img, can_elem(), 13, 21)
    elif stage == 1:      # 캔 1 + 팩 1
        put(img, can_elem(), 17, 21)
        put(img, pack_elem(), 7, 24)
    else:                 # 캔 2 + 팩 2 (위로 쌓기)
        put(img, can_elem(), 7, 21)
        put(img, can_elem(), 19, 21)
        put(img, pack_elem(), 10, 24)
        put(img, pack_elem(), 11, 17)
    return img


# ── 베리: 알알이 무더기 (그릇 X, 알마다 외곽선 → 또렷한 입자감) ─────
def _berry(d, x, y, col, hi):
    d.ellipse((x, y, x + 3, y + 3), fill=col)
    if hi:
        d.point((x + 1, y + 1), fill=MEAT_LT)


def berry_elem(back=False):
    col = MEAT_DK if back else P.MEAT_RED
    return element(4, 4, _berry, col, not back)


BERRY_LAYOUT = {
    # (x, y, back?) — back 알은 MEAT_DK, 뒤→앞 순서로 합성
    0: [(13, 23, True), (16, 23, True),
        (11, 25, False), (15, 25, False), (18, 26, False)],
    1: [(11, 21, True), (15, 20, True), (18, 22, True),
        (8, 24, True), (20, 25, True),
        (10, 24, False), (14, 23, False), (17, 25, False),
        (12, 26, False), (15, 26, False)],
    2: [(13, 16, True), (10, 18, True), (16, 18, True),
        (7, 21, True), (12, 20, False), (15, 21, True), (19, 21, True),
        (6, 24, True), (9, 23, False), (13, 23, False), (17, 23, False),
        (21, 24, True),
        (8, 26, False), (11, 26, False), (15, 26, False), (18, 26, False)],
}


def sprite_berry(stage):
    img = new_sprite()
    pts = BERRY_LAYOUT[stage]
    for x, y, back in pts:
        if back:
            put(img, berry_elem(back=True), x, y)
    for x, y, back in pts:
        if not back:
            put(img, berry_elem(back=False), x, y)
    # 꼭지 잎 (GRASS_DK 2px — 베리 정체성 보강)
    d = ImageDraw.Draw(img)
    leaf = {0: [(14, 22)], 1: [(13, 19), (17, 19)], 2: [(13, 15), (16, 15)]}
    for lx, ly in leaf[stage]:
        d.point((lx, ly), fill=P.GRASS_DK)
        d.point((lx + 1, ly), fill=P.GRASS_DK)
    return img


# ── 잔디 배경 (합성 프리뷰용 — 스펙 §5 저밀도 디테일) ────────────────
def grass_tile(rng):
    t = Image.new("RGBA", (CELL, CELL), P.GRASS_MD)
    d = ImageDraw.Draw(t)
    for _ in range(22):
        x = rng.randint(1, 30)
        y = rng.randint(1, 29)
        if rng.random() < 0.55:
            d.line((x, y, x, y + 1), fill=P.GRASS_DK)   # 풀잎 1x2
        else:
            d.point((x, y), fill=P.GRASS_LT)
    return t


# ── 시트 / 프리뷰 빌드 ───────────────────────────────────────────────
ITEMS = [
    ("wood", sprite_wood),
    ("stone", sprite_stone),
    ("meat", sprite_meat),
    ("meal", sprite_meal),
    ("berry", sprite_berry),
]


def main():
    rng = random.Random(SEED)
    sprites = {}
    for name, fn in ITEMS:
        sheet = Image.new("RGBA", (CELL * 3, CELL), (0, 0, 0, 0))
        cells = []
        for s in range(3):
            spr = fn(s)
            sheet.paste(spr, (s * CELL, 0))
            cells.append(spr)
        sprites[name] = cells
        sheet.save(os.path.join(OUT, f"item_{name}_v2.png"))
        upscale(sheet).save(os.path.join(OUT, f"_preview_item_{name}.png"))
        print(f"[ok] item_{name}_v2.png (96x32) + _preview_item_{name}.png")

    # 전체 시트 프리뷰 (투명 배경, 5행 x 3열)
    grid = Image.new("RGBA", (CELL * 3, CELL * len(ITEMS)), (0, 0, 0, 0))
    for r, (name, _) in enumerate(ITEMS):
        for s in range(3):
            grid.paste(sprites[name][s], (s * CELL, r * CELL))
    upscale(grid).save(os.path.join(OUT, "_preview_items_all.png"))

    # 잔디 합성 프리뷰: GRASS_MD 타일 위 5x3 배치
    bg_w, bg_h = CELL * 4, CELL * 6  # 128x192
    bg = Image.new("RGBA", (bg_w, bg_h))
    for ty in range(0, bg_h, CELL):
        for tx in range(0, bg_w, CELL):
            bg.paste(grass_tile(rng), (tx, ty))
    for r, (name, _) in enumerate(ITEMS):
        for s in range(3):
            bg.alpha_composite(sprites[name][s], (16 + s * CELL, 16 + r * CELL))
    upscale(bg).save(os.path.join(OUT, "_preview_grass_composite.png"))
    print("[ok] _preview_items_all.png / _preview_grass_composite.png")
    print("done.")


if __name__ == "__main__":
    main()
