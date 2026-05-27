# -*- coding: utf-8 -*-
"""운영자 fb #1 + #115 — sprite 디테일 향상.

이전 sprite 가 "초등학생 게임" 수준이라 RimWorld 스타일로
픽셀 아트 디테일 추가.  PIL procedural.

생성 sprite:
  tree.png          32x32  multi-tone canopy + trunk + dappled shadow
  wall_wood.png     16x16  3 plank rows + grain + knots + 위 그림자
  pawn_colonist.png 32x32  head + face + torso + arms + legs + boots
  deer.png          24x24  silhouette + legs + antlers + body fur tones
  trader.png        24x24  robed humanoid + backpack
  bandit.png        24x24  (있으면) hostile humanoid

실행:
  cd <unity-project>/Assets/Sprites
  python _gen_sprites.py
"""
from __future__ import annotations
from PIL import Image, ImageDraw, ImageFilter
import random
import os
from pathlib import Path

HERE = Path(__file__).resolve().parent


# ─────────────────────────────────────────────────────────────────────────
# 공통 헬퍼
# ─────────────────────────────────────────────────────────────────────────

def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def shade(rgb: tuple, factor: float) -> tuple:
    """RGB tuple 을 factor(0~2) 만큼 곱해서 어둡게/밝게."""
    r, g, b = rgb[:3]
    a = rgb[3] if len(rgb) == 4 else 255
    return (
        max(0, min(255, int(r * factor))),
        max(0, min(255, int(g * factor))),
        max(0, min(255, int(b * factor))),
        a,
    )


def put_px(im, x, y, color):
    w, h = im.size
    if 0 <= x < w and 0 <= y < h:
        im.putpixel((x, y), color)


# ─────────────────────────────────────────────────────────────────────────
# tree.png — 32x32 디테일 트리
# ─────────────────────────────────────────────────────────────────────────

def gen_tree(seed: int = 7):
    """#141 - 64x64 high-res. 5 색조 canopy + 트렁크 + 가지 + 그림자.
    PPU 32 로 import → world 크기 32x32@PPU16 와 동일 (2 unit)."""
    random.seed(seed)
    W = H = 64
    im = new_canvas(W, H)
    dr = ImageDraw.Draw(im)

    # 지면 그림자 (트리 밑 타원)
    for x in range(16, 48):
        for y in range(58, 64):
            dx = (x - 32) / 16.0
            dy = (y - 61) / 3.0
            if dx*dx + dy*dy < 1.0:
                cur = im.getpixel((x, y))
                if cur[3] == 0:
                    put_px(im, x, y, (0, 0, 0, 100))

    # 트렁크 폭 10, 높이 16 (32~57)
    trunk_dark = (52, 30, 12, 255)
    trunk_mid  = (88, 55, 25, 255)
    trunk_lit  = (130, 88, 45, 255)
    trunk_hi   = (170, 125, 75, 255)
    dr.rectangle([26, 44, 37, 58], fill=trunk_mid)
    # 좌 그림자 (2 px)
    for y in range(44, 59):
        put_px(im, 26, y, trunk_dark)
        put_px(im, 27, y, shade(trunk_dark, 1.25))
    # 우 그림자
    for y in range(44, 59):
        put_px(im, 37, y, shade(trunk_mid, 0.65))
        put_px(im, 36, y, shade(trunk_mid, 0.80))
    # 가운데 highlight column
    for y in range(45, 58):
        put_px(im, 31, y, trunk_lit)
        put_px(im, 32, y, trunk_hi)
    # 나무껍질 줄무늬
    for y in [46, 49, 52, 55]:
        for x in range(27, 37):
            if random.random() < 0.55:
                put_px(im, x, y, trunk_dark)
    # 옹이 (3x3)
    for ox in range(2):
        for oy in range(2):
            put_px(im, 32 + ox, 50 + oy, trunk_dark)
    put_px(im, 33, 51, shade(trunk_dark, 1.5))

    # foliage 5 색조
    foliage_darkest = (18, 45, 20, 255)
    foliage_dark    = (32, 75, 28, 255)
    foliage_mid     = (52, 115, 45, 255)
    foliage_lit     = (95, 170, 72, 255)
    foliage_hi      = (175, 220, 120, 255)

    # 큰 canopy center (32, 22), radius ~22
    cx, cy = 32, 22
    for y in range(0, 46):
        for x in range(0, W):
            dx, dy = x - cx, y - cy
            d = (dx * dx + dy * dy) ** 0.5
            r_max = 22.0 + random.uniform(-2.5, 1.0)
            if d > r_max:
                continue
            if d > r_max - 1.5:
                color = foliage_darkest
            elif d > r_max - 4.0:
                color = foliage_dark
            elif d > r_max - 8.0:
                color = foliage_mid if random.random() < 0.75 else foliage_dark
            elif d > r_max - 13.0:
                color = foliage_lit if random.random() < 0.65 else foliage_mid
            else:
                color = foliage_lit if random.random() < 0.55 else foliage_hi
            put_px(im, x, y, color)

    # 좌상단 sun highlight cluster
    for _ in range(60):
        hx = random.randint(20, 38)
        hy = random.randint(6, 22)
        if random.random() < 0.35:
            put_px(im, hx, hy, foliage_hi)
        else:
            put_px(im, hx, hy, foliage_lit)

    # 잎 그림자 점 (전체 canopy)
    for _ in range(35):
        hx = random.randint(16, 48)
        hy = random.randint(20, 42)
        cur = im.getpixel((hx, hy))
        if cur[3] > 0 and random.random() < 0.45:
            put_px(im, hx, hy, foliage_dark)

    # 가지 (트렁크 옆)
    for x, y in [(25, 42), (24, 40), (23, 39), (38, 42), (39, 40), (40, 39)]:
        put_px(im, x, y, trunk_dark)
        put_px(im, x, y + 1, shade(trunk_dark, 0.85))

    return im


# ─────────────────────────────────────────────────────────────────────────
# wall_wood.png — 16x16 plank texture
# ─────────────────────────────────────────────────────────────────────────

def gen_wall_wood(seed: int = 11):
    """#139 - 통나무 벽: 4 plank rows + 굵은 grain + 옹이 + 그림자."""
    random.seed(seed)
    W = H = 16
    im = new_canvas(W, H)
    dr = ImageDraw.Draw(im)

    # 4 plank rows (전보다 더 많은 줄로 디테일)
    plank_rows = [(0, 3), (4, 7), (8, 11), (12, 15)]
    plank_colors_mid = [
        (148, 100, 55, 255),
        (128, 85, 45, 255),
        (138, 92, 50, 255),
        (118, 78, 42, 255),
    ]

    for idx, (y0, y1) in enumerate(plank_rows):
        base = plank_colors_mid[idx]
        dark = shade(base, 0.55)
        verylit = shade(base, 1.25)
        lit  = shade(base, 1.10)
        dr.rectangle([0, y0, W - 1, y1], fill=base)
        # 상단 seam (이전 plank 의 그림자)
        for x in range(W):
            put_px(im, x, y0, dark)
        # 하단 highlight (광원 위)
        for x in range(W):
            put_px(im, x, y1, shade(base, 0.80))
        # grain (수평 줄무늬)
        for gy in range(y0 + 1, y1):
            for x in range(W):
                r = random.random()
                if r < 0.22: put_px(im, x, gy, dark)
                elif r < 0.36: put_px(im, x, gy, verylit)
                elif r < 0.42: put_px(im, x, gy, lit)
        # 옹이 (50% 확률)
        if random.random() < 0.55:
            kx = random.randint(2, W - 4)
            ky = random.randint(y0 + 1, y1 - 1)
            # 더 큰 옹이 (3x2)
            for ox in range(2):
                for oy in range(2):
                    put_px(im, kx + ox, ky + oy, dark)
            put_px(im, kx + 1, ky, shade(dark, 1.4))  # highlight 중심

    # 왼/오 측면 그림자
    for y in range(H):
        cur = im.getpixel((0, y))
        if cur[3] > 0: im.putpixel((0, y), shade(cur, 0.65))
        cur = im.getpixel((W-1, y))
        if cur[3] > 0: im.putpixel((W-1, y), shade(cur, 0.80))

    return im


# ─────────────────────────────────────────────────────────────────────────
# pawn_colonist.png — 32x32 detailed humanoid (top-down 3/4 view)
# ─────────────────────────────────────────────────────────────────────────

def gen_pawn(seed: int = 3):
    """#141 - 64x64 high-res colonist (PPU 32 → world 2 unit, 동일).
    얼굴 표정 + 외투 디테일 + 단추 + 손/발 정밀화."""
    random.seed(seed)
    W = H = 64
    im = new_canvas(W, H)
    dr = ImageDraw.Draw(im)

    # palette
    skin       = (228, 184, 138, 255)
    skin_lit   = (245, 205, 162, 255)
    skin_shade = (175, 132, 92, 255)
    hair       = (55, 35, 18, 255)
    hair_lit   = (90, 60, 30, 255)
    hair_hi    = (130, 92, 50, 255)
    coat       = (115, 70, 38, 255)
    coat_dark  = (75, 42, 18, 255)
    coat_lit   = (155, 105, 58, 255)
    coat_seam  = (60, 35, 14, 255)
    pants      = (58, 70, 92, 255)
    pants_dk   = (35, 45, 62, 255)
    pants_hi   = (88, 102, 128, 255)
    boots      = (35, 24, 16, 255)
    boots_hi   = (62, 44, 30, 255)
    button     = (235, 220, 140, 255)
    eye        = (22, 12, 8, 255)
    mouth      = (95, 50, 40, 255)
    cheek      = (200, 130, 110, 200)
    shadow     = (0, 0, 0, 120)

    # ── 지면 그림자 (y 60-62)
    for x in range(20, 44):
        dx = (x - 32) / 12.0
        if dx*dx < 1.0:
            put_px(im, x, 60, shadow)
            put_px(im, x, 61, shade(shadow, 0.7))

    # ── 머리 (y 8~26, center x=32, radius 10)
    dr.ellipse([20, 8, 44, 28], fill=hair)            # 머리카락 뒤
    dr.ellipse([22, 12, 42, 28], fill=skin)           # 얼굴 oval
    # 이마 위 머리카락 sweep
    for x in range(22, 43):
        for y in range(12, 16):
            if random.random() < 0.85:
                put_px(im, x, y, hair)
    # 머리카락 highlight (좌상)
    for x in range(26, 36):
        put_px(im, x, 10, hair_lit)
        put_px(im, x, 11, hair_lit if random.random() < 0.5 else hair)
    put_px(im, 28, 9, hair_hi)
    put_px(im, 30, 9, hair_hi)
    # 얼굴 살색 highlight (좌상)
    for x in range(24, 30):
        cur = im.getpixel((x, 17))
        if cur[:3] == skin[:3]:
            put_px(im, x, 17, skin_lit)
    # 턱 그림자
    for x in range(24, 41):
        for y in [25, 26]:
            cur = im.getpixel((x, y))
            if cur[:3] == skin[:3]:
                put_px(im, x, y, skin_shade)
    # 눈 (2 px each)
    dr.rectangle([26, 18, 28, 19], fill=eye)
    dr.rectangle([36, 18, 38, 19], fill=eye)
    # 눈썹
    for x in range(25, 30): put_px(im, x, 16, hair)
    for x in range(35, 40): put_px(im, x, 16, hair)
    # 코 (3 px shadow)
    put_px(im, 31, 20, skin_shade)
    put_px(im, 32, 20, skin_shade)
    put_px(im, 32, 21, skin_shade)
    # 입
    for x in range(30, 35): put_px(im, x, 23, mouth)
    put_px(im, 31, 24, mouth)
    put_px(im, 33, 24, mouth)
    # 볼 (귀여움)
    put_px(im, 24, 22, cheek)
    put_px(im, 25, 22, cheek)
    put_px(im, 39, 22, cheek)
    put_px(im, 40, 22, cheek)
    # 귀
    put_px(im, 21, 20, skin_shade)
    put_px(im, 21, 21, skin_shade)
    put_px(im, 43, 20, skin_shade)
    put_px(im, 43, 21, skin_shade)

    # ── 목 (y 28-30)
    for x in range(30, 35):
        put_px(im, x, 28, skin_shade)
        put_px(im, x, 29, skin_shade)

    # ── 몸통 - 외투 (y 30~46, 폭 22)
    dr.rectangle([22, 30, 42, 46], fill=coat)
    # 어깨 정점
    for x in range(22, 43):
        put_px(im, x, 30, coat_dark)
    # 측면 그림자
    for y in range(30, 47):
        put_px(im, 22, y, coat_dark)
        put_px(im, 23, y, shade(coat, 0.85))
        put_px(im, 42, y, coat_dark)
        put_px(im, 41, y, shade(coat, 0.85))
    # 좌상 highlight
    for y in range(31, 36):
        put_px(im, 24, y, coat_lit)
        put_px(im, 25, y, coat_lit if random.random() < 0.6 else coat)
    # 가운데 단추 라인 + seam
    for y in range(31, 46):
        put_px(im, 32, y, coat_seam)
    # 단추 5개 (y 33, 36, 39, 42, 45)
    for by in [33, 36, 39, 42, 45]:
        put_px(im, 31, by, button)
        put_px(im, 32, by, button)
        put_px(im, 31, by + 1, shade(button, 0.7))
    # 옷깃 (collar 좌우)
    for x in range(26, 32):
        put_px(im, x, 30, coat_lit)
    for x in range(33, 39):
        put_px(im, x, 30, coat_lit)
    # 외투 밑단 디테일
    for x in range(22, 43):
        put_px(im, x, 46, coat_dark)

    # ── 팔 + 어깨 (좌우 폭 4)
    for y in range(30, 42):
        for x in range(18, 22):
            put_px(im, x, y, coat)
        for x in range(43, 47):
            put_px(im, x, y, coat)
        # 외측 어두운 outline
        put_px(im, 18, y, coat_dark)
        put_px(im, 46, y, coat_dark)
    # 좌상 어깨 highlight
    for y in range(31, 35):
        put_px(im, 19, y, coat_lit)
    # 손 (살색) 양 옆
    dr.rectangle([18, 42, 22, 45], fill=skin)
    dr.rectangle([43, 42, 47, 45], fill=skin)
    for x in range(18, 23):
        put_px(im, x, 42, skin_shade)  # 손목 그림자
        put_px(im, x, 45, skin_shade)
    for x in range(43, 48):
        put_px(im, x, 42, skin_shade)
        put_px(im, x, 45, skin_shade)
    # 손가락 라인
    put_px(im, 20, 44, skin_shade)
    put_px(im, 45, 44, skin_shade)

    # ── 다리 - 바지 (y 47~56, 폭 5 each)
    dr.rectangle([24, 47, 30, 56], fill=pants)
    dr.rectangle([33, 47, 39, 56], fill=pants)
    # 가운데 갭 (다리 분리)
    # 측면 highlight
    for y in range(47, 56):
        put_px(im, 24, y, pants_hi)
        put_px(im, 33, y, pants_hi)
    # 내측 그림자
    for y in range(47, 56):
        put_px(im, 30, y, pants_dk)
        put_px(im, 39, y, pants_dk)
    # 무릎 라인
    for x in range(24, 31):
        put_px(im, x, 51, pants_dk)
    for x in range(33, 40):
        put_px(im, x, 51, pants_dk)

    # ── 부츠 (y 56~59)
    dr.rectangle([23, 56, 31, 59], fill=boots)
    dr.rectangle([33, 56, 41, 59], fill=boots)
    # 부츠 highlight (상단)
    for x in range(23, 32):
        put_px(im, x, 56, boots_hi)
    for x in range(33, 42):
        put_px(im, x, 56, boots_hi)
    # 부츠 끝 굽
    for x in range(23, 32):
        put_px(im, x, 59, shade(boots, 0.6))
    for x in range(33, 42):
        put_px(im, x, 59, shade(boots, 0.6))

    return im


# ─────────────────────────────────────────────────────────────────────────
# deer.png — 24x24 side view, 4 legs + antlers
# ─────────────────────────────────────────────────────────────────────────

def gen_deer(seed: int = 5):
    random.seed(seed)
    W = H = 24
    im = new_canvas(W, H)
    dr = ImageDraw.Draw(im)

    body_mid  = (158, 105, 60, 255)
    body_dark = (108, 68, 36, 255)
    body_lit  = (200, 152, 95, 255)
    belly     = (228, 200, 160, 255)
    antler    = (215, 190, 145, 255)
    antler_dk = (150, 120, 80, 255)
    eye       = (20, 14, 8, 255)
    hoof      = (28, 16, 10, 255)

    # 몸통 (가로 타원 - y 9~14, x 6~17)
    dr.ellipse([6, 9, 17, 13], fill=body_mid)
    # 등 highlight (위 한 줄)
    for x in range(7, 17):
        put_px(im, x, 9, body_lit)
    # 등 가운데 밝은 톤 2 줄
    for x in range(8, 16):
        put_px(im, x, 10, body_lit)
    # 배 밝게 (밑)
    for x in range(8, 16):
        put_px(im, x, 13, belly)

    # 목 (사선 위로) — 머리로 이어짐
    put_px(im, 17, 8, body_mid)
    put_px(im, 18, 8, body_mid)
    put_px(im, 18, 7, body_mid)
    put_px(im, 19, 7, body_mid)

    # 머리 (작은 직사각형 우측 상단)
    dr.rectangle([19, 5, 21, 7], fill=body_mid)
    put_px(im, 19, 5, body_lit)
    # 코 (튀어나옴)
    put_px(im, 22, 6, body_dark)
    put_px(im, 22, 7, (45, 28, 18, 255))
    # 눈
    put_px(im, 21, 6, eye)
    # 귀 (위로 작은 두 점)
    put_px(im, 19, 4, body_dark)
    put_px(im, 20, 4, body_mid)

    # 뿔 - V 모양 양쪽 가지
    # 왼쪽 가지
    put_px(im, 18, 3, antler)
    put_px(im, 18, 2, antler)
    put_px(im, 17, 2, antler_dk)
    # 오른쪽 가지
    put_px(im, 20, 3, antler)
    put_px(im, 20, 2, antler)
    put_px(im, 21, 2, antler_dk)

    # 다리 - 명확한 4 다리, 사이에 빈 픽셀 (silhouette 인식 위해)
    # 앞다리 (오른쪽) - x=15, 16 (간격 1)
    for y in range(14, 20):
        put_px(im, 15, y, body_dark)
    put_px(im, 15, 20, hoof)
    # 뒷다리 (왼쪽) - x=7, 8
    for y in range(14, 20):
        put_px(im, 8, y, body_dark)
    put_px(im, 8, 20, hoof)
    # 추가 다리 silhouette 보조 (조금 안쪽 두 번째 다리)
    for y in range(14, 19):
        put_px(im, 12, y, shade(body_dark, 0.85))
    put_px(im, 12, 19, hoof)
    for y in range(14, 19):
        put_px(im, 11, y, shade(body_dark, 0.85))
    put_px(im, 11, 19, hoof)

    # 꼬리 (좌측 위 작은 점)
    put_px(im, 5, 10, body_mid)
    put_px(im, 5, 9, body_lit)

    # 그림자 (지면 - 배 밑)
    for x in range(7, 17):
        put_px(im, x, 21, (0, 0, 0, 70))

    # 가벼운 outline: 몸통 좌측 + 등 좌상만 살짝 darken (silhouette 가독성)
    for y in range(9, 14):
        cur = im.getpixel((6, y))
        if cur[3] > 0:
            put_px(im, 6, y, shade(cur, 0.75))

    return im


# ─────────────────────────────────────────────────────────────────────────
# trader.png — 24x24 robed humanoid with backpack
# ─────────────────────────────────────────────────────────────────────────

def gen_trader(seed: int = 9):
    random.seed(seed)
    W = H = 24
    im = new_canvas(W, H)
    dr = ImageDraw.Draw(im)

    skin       = (224, 178, 132, 255)
    skin_shade = (180, 138, 96, 255)
    hood       = (60, 45, 80, 255)       # 보라 hood
    hood_lit   = (105, 80, 140, 255)
    robe       = (100, 80, 130, 255)
    robe_dark  = (62, 48, 88, 255)
    robe_lit   = (148, 125, 180, 255)
    pack       = (88, 60, 38, 255)
    pack_lit   = (130, 90, 55, 255)
    pack_dark  = (52, 36, 22, 255)
    gold       = (235, 200, 90, 255)
    boots      = (40, 28, 18, 255)
    outline    = (24, 16, 10, 255)
    eye        = (28, 18, 12, 255)

    # hood (큰 둥근 모자)
    dr.ellipse([7, 2, 17, 11], fill=hood)
    # face peek (안쪽 살색)
    dr.ellipse([9, 5, 15, 11], fill=skin)
    # face shade
    for x in range(10, 15):
        cur = im.getpixel((x, 10))
        if cur == skin:
            im.putpixel((x, 10), skin_shade)
    # eyes
    put_px(im, 10, 8, eye)
    put_px(im, 13, 8, eye)
    # hood highlight 위쪽
    put_px(im, 11, 2, hood_lit)
    put_px(im, 12, 2, hood_lit)

    # robe (사다리꼴 가운)
    for y in range(11, 19):
        # 점점 넓어지는 robe
        w = 4 + (y - 11) // 2
        x0 = 12 - w
        x1 = 11 + w
        for x in range(x0, x1 + 1):
            put_px(im, x, y, robe)
        # 가장자리 outline darken
        put_px(im, x0, y, robe_dark)
        put_px(im, x1, y, robe_dark)
        # 좌측 highlight
        if y < 17:
            put_px(im, x0 + 1, y, robe_lit)

    # 가운 단추/벨트 (가운데 노란 띠)
    for x in range(10, 14):
        put_px(im, x, 15, gold)

    # 등 뒤 backpack (오른쪽 어깨 너머)
    dr.rectangle([16, 11, 19, 17], fill=pack)
    # pack outline
    for y in range(11, 18):
        put_px(im, 16, y, pack_dark)
        put_px(im, 19, y, pack_dark)
    for x in range(16, 20):
        put_px(im, x, 17, pack_dark)
    # pack highlight
    for y in range(12, 16):
        put_px(im, 17, y, pack_lit)
    # 끈 (어깨로 가는 선)
    put_px(im, 16, 11, pack_dark)
    put_px(im, 15, 11, pack_dark)

    # 다리/부츠 (robe 밑)
    for y in range(19, 22):
        put_px(im, 10, y, boots)
        put_px(im, 11, y, boots)
        put_px(im, 13, y, boots)
        put_px(im, 14, y, boots)
    # 부츠 highlight
    put_px(im, 10, 19, (62, 44, 30, 255))
    put_px(im, 13, 19, (62, 44, 30, 255))

    # 그림자
    for x in range(8, 17):
        put_px(im, x, 22, (0, 0, 0, 90))

    # 가벼운 outline: hood 양 옆 한 줄만
    for y in range(3, 11):
        cur = im.getpixel((7, y))
        if cur[3] > 0:
            put_px(im, 7, y, shade(cur, 0.7))
        cur = im.getpixel((17, y))
        if cur[3] > 0:
            put_px(im, 17, y, shade(cur, 0.7))

    return im


# ─────────────────────────────────────────────────────────────────────────
# 메인
# ─────────────────────────────────────────────────────────────────────────

def gen_wood_pile(seed: int = 13):
    """16x16 stacked logs - 운영자 fb #116."""
    random.seed(seed)
    W = H = 16
    im = new_canvas(W, H)
    dr = ImageDraw.Draw(im)

    log_mid  = (140, 92, 50, 255)
    log_dark = (95, 60, 30, 255)
    log_lit  = (185, 132, 78, 255)
    ring     = (60, 38, 20, 255)
    shadow   = (0, 0, 0, 100)

    # 그림자 (밑)
    for x in range(2, 14):
        put_px(im, x, 13, shadow)

    # 하단 통나무 2개 (가로 배치 - 길이 6, 높이 3)
    # log1: x 1~7, y 9~11
    dr.rectangle([1, 9, 7, 11], fill=log_mid)
    put_px(im, 4, 9, log_lit)
    put_px(im, 5, 9, log_lit)
    for x in range(1, 8):
        put_px(im, x, 11, log_dark)
    # 단면 (왼쪽) - 동심원
    put_px(im, 1, 10, ring)
    put_px(im, 2, 10, log_lit)

    # log2: x 9~15, y 9~11
    dr.rectangle([9, 9, 15, 11], fill=log_mid)
    put_px(im, 12, 9, log_lit)
    for x in range(9, 16):
        put_px(im, x, 11, log_dark)
    put_px(im, 15, 10, ring)
    put_px(im, 14, 10, log_lit)

    # 상단 통나무 1개 (가운데 위)
    dr.rectangle([4, 5, 12, 8], fill=log_mid)
    put_px(im, 7, 5, log_lit)
    put_px(im, 8, 5, log_lit)
    for x in range(4, 13):
        put_px(im, x, 8, log_dark)
    # 단면
    put_px(im, 4, 6, ring)
    put_px(im, 5, 6, ring)
    put_px(im, 5, 7, log_lit)
    put_px(im, 12, 6, ring)
    put_px(im, 11, 6, log_lit)

    # 작은 highlight
    put_px(im, 8, 6, log_lit)
    put_px(im, 7, 7, log_lit)

    return im


def gen_stone_vein(seed: int = 17):
    """24x24 광맥 - 회색 바위 덩어리 + 광물 줄무늬."""
    random.seed(seed)
    W = H = 24
    im = new_canvas(W, H)
    dr = ImageDraw.Draw(im)

    rock_dark = (52, 50, 55, 255)
    rock_mid  = (88, 84, 90, 255)
    rock_lit  = (140, 134, 138, 255)
    rock_hi   = (180, 175, 178, 255)
    ore       = (165, 145, 80, 255)   # 광물 줄무늬

    # 큰 둥근 바위 (24x24 거의 채움)
    cx, cy = 12, 12
    for y in range(W):
        for x in range(W):
            dx, dy = x - cx, y - cy
            d = (dx*dx + dy*dy) ** 0.5
            r_max = 11.0 + random.uniform(-0.6, 0.3)
            if d > r_max:
                continue
            if d > r_max - 0.8:
                color = rock_dark
            elif d > r_max - 2.5:
                color = rock_mid if random.random() < 0.7 else rock_dark
            elif d > r_max - 5.0:
                color = rock_lit if random.random() < 0.55 else rock_mid
            else:
                color = rock_hi if random.random() < 0.3 else rock_lit
            put_px(im, x, y, color)

    # 갈라진 균열 (선)
    for x in range(7, 17):
        put_px(im, x, 10, rock_dark)
    for y in range(9, 14):
        put_px(im, 14, y, rock_dark)

    # 광물 줄무늬 (3-4개 점)
    for _ in range(5):
        ox = random.randint(5, 18)
        oy = random.randint(5, 18)
        put_px(im, ox, oy, ore)
        if random.random() < 0.5:
            put_px(im, ox + 1, oy, ore)

    return im


def gen_stone_chunk(seed: int = 19):
    """16x16 돌덩이 (drop item)."""
    random.seed(seed)
    W = H = 16
    im = new_canvas(W, H)

    rock_dark = (50, 48, 52, 255)
    rock_mid  = (95, 90, 98, 255)
    rock_lit  = (150, 145, 150, 255)
    shadow    = (0, 0, 0, 100)

    # 그림자
    for x in range(2, 14):
        put_px(im, x, 13, shadow)

    # 큰 돌 (왼쪽 위)
    dr = ImageDraw.Draw(im)
    dr.ellipse([1, 4, 9, 11], fill=rock_mid)
    # highlight
    put_px(im, 3, 5, rock_lit)
    put_px(im, 4, 5, rock_lit)
    put_px(im, 3, 6, rock_lit)
    # 어두운 가장자리
    for y in range(4, 12):
        put_px(im, 1, y, rock_dark)
    for x in range(1, 10):
        put_px(im, x, 11, rock_dark)

    # 작은 돌 (오른쪽 아래)
    dr.ellipse([8, 7, 14, 12], fill=rock_mid)
    put_px(im, 10, 8, rock_lit)
    put_px(im, 11, 8, rock_lit)
    for y in range(7, 13):
        put_px(im, 14, y, rock_dark)
    for x in range(8, 15):
        put_px(im, x, 12, rock_dark)

    # 작은 부스러기
    put_px(im, 5, 12, rock_mid)
    put_px(im, 12, 5, rock_mid)

    return im


def gen_meat_pile(seed: int = 23):
    """16x16 raw meat chunk - 빨간 살덩이."""
    random.seed(seed)
    W = H = 16
    im = new_canvas(W, H)
    dr = ImageDraw.Draw(im)

    meat_dark = (115, 30, 30, 255)
    meat_mid  = (170, 55, 55, 255)
    meat_lit  = (215, 95, 90, 255)
    bone      = (240, 230, 200, 255)
    shadow    = (0, 0, 0, 110)

    # 그림자
    for x in range(2, 14):
        put_px(im, x, 13, shadow)

    # 큰 살덩이 (사각 둥근)
    dr.ellipse([2, 5, 12, 12], fill=meat_mid)
    # highlight
    put_px(im, 4, 6, meat_lit)
    put_px(im, 5, 6, meat_lit)
    put_px(im, 6, 7, meat_lit)
    # 어두운 가장자리
    for y in range(5, 13):
        put_px(im, 2, y, meat_dark)
    for x in range(2, 13):
        put_px(im, x, 12, meat_dark)

    # 작은 뼈조각 (오른쪽)
    put_px(im, 13, 7, bone)
    put_px(im, 13, 8, bone)
    put_px(im, 14, 8, bone)
    put_px(im, 14, 9, bone)

    # 핏자국
    put_px(im, 8, 4, meat_dark)
    put_px(im, 11, 11, meat_dark)

    return im


def main():
    targets = [
        ("tree.png",          gen_tree),
        ("wall_wood.png",     gen_wall_wood),
        ("pawn_colonist.png", gen_pawn),
        ("deer.png",          gen_deer),
        ("trader.png",        gen_trader),
        ("wood_pile.png",     gen_wood_pile),
        ("stone_vein.png",    gen_stone_vein),
        ("stone_chunk.png",   gen_stone_chunk),
        ("meat_pile.png",     gen_meat_pile),
    ]
    for name, fn in targets:
        out = HERE / name
        im = fn()
        im.save(out)
        print(f"  generated {name} {im.size}")


if __name__ == "__main__":
    main()
