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
    """#139 - 더 디테일한 나무: 5 색조 canopy + 가지 + 그림자."""
    random.seed(seed)
    W = H = 32
    im = new_canvas(W, H)
    dr = ImageDraw.Draw(im)

    # 그림자 (지면) - 트리 밑 회색 타원
    for x in range(8, 24):
        for y in range(29, 32):
            dx = (x - 16) / 8.0
            dy = (y - 30.5) / 1.5
            if dx*dx + dy*dy < 1.0:
                put_px(im, x, y, (0, 0, 0, 90))

    # 트렁크 - 폭 5, 높이 8.  나무껍질 무늬 추가.
    trunk_dark = (52, 30, 12, 255)
    trunk_mid  = (88, 55, 25, 255)
    trunk_lit  = (130, 88, 45, 255)
    trunk_hi   = (165, 120, 70, 255)
    dr.rectangle([13, 22, 18, 29], fill=trunk_mid)
    # 왼 가장자리 = 그림자
    for y in range(22, 30):
        put_px(im, 13, y, trunk_dark)
    # 오른 가장자리 = 미드 그림자
    for y in range(22, 30):
        put_px(im, 18, y, shade(trunk_mid, 0.7))
    # highlight 가운데
    for y in range(22, 29):
        put_px(im, 15, y, trunk_lit)
    # 나무껍질 줄무늬 4개 (수평 어두운 라인)
    for y in [23, 25, 27]:
        for x in range(14, 18):
            if random.random() < 0.7:
                put_px(im, x, y, trunk_dark)
    # 옹이
    put_px(im, 16, 25, trunk_dark)
    put_px(im, 16, 26, shade(trunk_dark, 1.2))

    # foliage - 5 색조
    foliage_darkest = (22, 50, 22, 255)
    foliage_dark    = (35, 80, 30, 255)
    foliage_mid     = (55, 120, 48, 255)
    foliage_lit     = (95, 170, 72, 255)
    foliage_hi      = (170, 215, 115, 255)

    # 큰 canopy - center (16, 11), radius ~11 (전보다 큼)
    cx, cy = 16, 11
    for y in range(0, 23):
        for x in range(0, W):
            dx, dy = x - cx, y - cy
            d = (dx * dx + dy * dy) ** 0.5
            r_max = 11.0 + random.uniform(-1.2, 0.5)
            if d > r_max:
                continue
            # layer 결정 (5 등급)
            if d > r_max - 0.8:
                color = foliage_darkest
            elif d > r_max - 2.0:
                color = foliage_dark
            elif d > r_max - 4.0:
                color = foliage_mid if random.random() < 0.75 else foliage_dark
            elif d > r_max - 6.5:
                color = foliage_lit if random.random() < 0.65 else foliage_mid
            else:
                color = foliage_lit if random.random() < 0.6 else foliage_hi
            put_px(im, x, y, color)

    # highlight cluster - 좌상단 sun (더 자연스럽게)
    for _ in range(20):
        hx = random.randint(10, 18)
        hy = random.randint(3, 11)
        if random.random() < 0.4:
            put_px(im, hx, hy, foliage_hi)
        else:
            put_px(im, hx, hy, foliage_lit)

    # 작은 어두운 그림자 점 (잎 그림자)
    for _ in range(15):
        hx = random.randint(8, 24)
        hy = random.randint(10, 21)
        cur = im.getpixel((hx, hy))
        if cur[3] > 0 and random.random() < 0.5:
            put_px(im, hx, hy, foliage_dark)

    # 가지 (canopy 아래 트렁크 옆 살짝)
    put_px(im, 12, 21, trunk_dark)
    put_px(im, 11, 20, trunk_dark)
    put_px(im, 19, 21, trunk_dark)
    put_px(im, 20, 20, trunk_dark)

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
    random.seed(seed)
    W = H = 32
    im = new_canvas(W, H)

    # palette
    skin       = (224, 178, 132, 255)
    skin_shade = (180, 138, 96, 255)
    hair       = (62, 40, 22, 255)
    hair_lit   = (95, 62, 32, 255)
    coat       = (110, 68, 38, 255)   # 갈색 외투
    coat_dark  = (78, 46, 22, 255)
    coat_lit   = (148, 100, 55, 255)
    pants      = (60, 70, 90, 255)
    pants_dk   = (38, 48, 64, 255)
    boots      = (38, 26, 18, 255)
    boots_hi   = (62, 44, 30, 255)
    eye        = (28, 18, 12, 255)
    mouth      = (90, 50, 38, 255)
    outline    = (24, 16, 10, 255)

    dr = ImageDraw.Draw(im)

    # ── 머리 (y 5~13, 8x9 정도, center x=16)
    # hair back/sides
    dr.ellipse([10, 4, 22, 14], fill=hair)
    # face oval (살구색)
    dr.ellipse([11, 6, 21, 14], fill=skin)
    # 턱 그림자
    for x in range(12, 21):
        cur = im.getpixel((x, 13))
        if cur == skin:
            im.putpixel((x, 13), skin_shade)
    # hair sweep over forehead
    for x in range(11, 21):
        put_px(im, x, 6, hair)
    for x in range(11, 13):
        put_px(im, x, 7, hair)
    for x in range(19, 21):
        put_px(im, x, 7, hair)
    # hair highlight
    put_px(im, 14, 5, hair_lit)
    put_px(im, 17, 5, hair_lit)

    # eyes
    put_px(im, 13, 9, eye)
    put_px(im, 18, 9, eye)
    # mouth
    put_px(im, 15, 11, mouth)
    put_px(im, 16, 11, mouth)

    # ── 목 (살색 짧게)
    put_px(im, 15, 14, skin_shade)
    put_px(im, 16, 14, skin_shade)

    # ── 몸통 - 외투 (y 15~23, 폭 10)
    dr.rectangle([11, 15, 20, 23], fill=coat)
    # 외투 가장자리 어두운 outline (좌/우/아래)
    for y in range(15, 24):
        put_px(im, 11, y, coat_dark)
        put_px(im, 20, y, coat_dark)
    for x in range(11, 21):
        put_px(im, x, 23, coat_dark)
    # 가운데 단추 라인 (vertical seam)
    for y in range(16, 23):
        put_px(im, 16, y, coat_dark)
    # 외투 highlight 좌상
    for y in range(16, 19):
        put_px(im, 12, y, coat_lit)
    # 단추 3개
    put_px(im, 16, 17, (235, 220, 140, 255))
    put_px(im, 16, 19, (235, 220, 140, 255))
    put_px(im, 16, 21, (235, 220, 140, 255))

    # ── 팔 (옷 짧은 소매 - 양 옆 살짝)
    for y in range(15, 21):
        put_px(im, 10, y, coat)
        put_px(im, 21, y, coat)
    # 손 (살색)
    put_px(im, 10, 21, skin)
    put_px(im, 10, 22, skin_shade)
    put_px(im, 21, 21, skin)
    put_px(im, 21, 22, skin_shade)

    # ── 다리 - 바지 (y 24~28)
    dr.rectangle([12, 24, 15, 28], fill=pants)
    dr.rectangle([16, 24, 19, 28], fill=pants)
    # 바지 그림자 사이 seam
    for y in range(24, 29):
        put_px(im, 15, y, pants_dk)
        put_px(im, 16, y, pants_dk)
    # 바지 외측 highlight
    for y in range(24, 28):
        put_px(im, 12, y, shade(pants, 1.15))
        put_px(im, 19, y, shade(pants, 1.15))

    # ── 부츠 (y 28~30)
    dr.rectangle([12, 28, 15, 30], fill=boots)
    dr.rectangle([16, 28, 19, 30], fill=boots)
    # 부츠 highlight
    for x in range(12, 16):
        put_px(im, x, 28, boots_hi)
    for x in range(16, 20):
        put_px(im, x, 28, boots_hi)

    # ── 발 그림자 (캐릭터 밑에 살짝)
    for x in range(13, 19):
        put_px(im, x, 31, (0, 0, 0, 120))

    # ── 가벼운 outline 만: 머리 좌우 가장자리 + 외투 양쪽 가장자리
    #  (전체 outline 은 너무 무거워서 silhouette 가독성 떨어트림 - 부분만)
    # 머리 좌우 boundary 살짝
    for y in range(6, 13):
        cur = im.getpixel((10, y))
        if cur[3] == 0:
            # 오른쪽이 비 0 픽셀인지 확인
            r = im.getpixel((11, y))
            if r[3] > 200:
                put_px(im, 10, y, outline)
        cur = im.getpixel((22, y))
        if cur[3] == 0:
            l = im.getpixel((21, y))
            if l[3] > 200:
                put_px(im, 22, y, outline)

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
