# -*- coding: utf-8 -*-
"""작물·바위 v2 — artv2 스펙 준수 (팔레트 import, 디더링 금지, 플랫+램프, 좌상 조명).
crop_rice 3단계 32px (PPU 32 — meta 갱신 동반) / struct32_stone_vein / tile32_rock_a·b."""
import sys, os, random
sys.stdout.reconfigure(encoding="utf-8")
PAL_DIR = r"G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Sprites"
sys.path.insert(0, PAL_DIR)
import palette as P
from PIL import Image

OUT = r"G:/ai/_artv2_staging/out"
rng = random.Random(20260613)
T = (0, 0, 0, 0)
GRASS_DK, GRASS_MD, GRASS_LT = P.GRASS_DK, P.GRASS_MD, P.GRASS_LT
DIRT_DK, DIRT_MD, DIRT_LT = P.DIRT_DK, P.DIRT_MD, P.DIRT_LT
ROCK_DK, ROCK_MD, ROCK_LT = P.ROCK_DK, P.ROCK_MD, P.ROCK_LT
GOLD = P.CROP_GOLD
GOLD_LT = (min(255, GOLD[0]+24), min(255, GOLD[1]+22), GOLD[2]+30 if GOLD[2]+30<255 else 255, 255)  # 명도 +12% 이내 파생
GOLD_DK = (int(GOLD[0]*0.82), int(GOLD[1]*0.80), int(GOLD[2]*0.72), 255)
OUTLINE_PLANT = getattr(P, 'OUTLINE_PLANT', (40, 52, 32, 255))
OUTLINE_OBJ = getattr(P, 'OUTLINE_OBJ', (38, 34, 28, 255))
STEM_DK = (int(GRASS_DK[0]*0.9), int(GRASS_DK[1]*0.95), int(GRASS_DK[2]*0.9), 255)

def px(im, x, y, c):
    if 0 <= x < im.width and 0 <= y < im.height: im.putpixel((x, y), c)

def blob(im, cx, cy, pts, c):
    for dx, dy in pts: px(im, cx+dx, cy+dy, c)

LEAF = [(0,0),(1,0),(-1,0)]

def crop_stage(stage):
    im = Image.new("RGBA", (32, 32), T)
    # 이랑(밭고랑) — 하단 1/4, 흙 마운드 2덩이 + 골
    for x in range(2, 30):
        px(im, x, 28, DIRT_MD); px(im, x, 29, DIRT_MD); px(im, x, 30, DIRT_DK)
    for x in range(4, 28, 6):
        px(im, x, 27, DIRT_LT); px(im, x+1, 27, DIRT_LT)   # 마운드 하이라이트(좌상광)
    # 포기 3개 (x 7/16/25), 단계별 형태
    for i, bx in enumerate((7, 16, 25)):
        jit = rng.randint(-1, 1)
        cx = bx + jit
        if stage == 0:   # 새싹 — 쌍떡잎
            px(im, cx, 26, STEM_DK); px(im, cx, 25, GRASS_MD)
            px(im, cx-1, 24, GRASS_LT); px(im, cx+1, 24, GRASS_LT)
            px(im, cx-1, 25, GRASS_MD); px(im, cx+1, 25, GRASS_MD)
        elif stage == 1: # 성장 — 줄기 + 갈라진 잎 4장 (폭 있는 풀포기)
            h = 10 + rng.randint(0, 2)
            for y in range(28-h, 27): px(im, cx, y, GRASS_MD)
            px(im, cx, 28-h, GRASS_LT)
            # 좌우 잎 — 길이 2~3px 사선, 위로 갈수록 짧게
            for li, ly in enumerate(range(28-h+2, 26, 3)):
                ln = 3 if li == 0 else 2
                for k in range(1, ln+1):
                    px(im, cx-k, ly+ (1 if k==ln else 0), GRASS_MD if k < ln else GRASS_DK)
                    px(im, cx+k, ly+1+(1 if k==ln else 0), GRASS_MD if k < ln else GRASS_DK)
            px(im, cx-1, 28-h+1, GRASS_LT)  # 좌상 하이라이트 잎
        else:            # 익음 — 황금 이삭 고개숙임 + 잎 황변
            h = 12 + rng.randint(0, 1)
            olive = (130, 128, 62, 255)  # GRASS_MD→황변 파생 (밴드 내)
            for y in range(28-h, 27): px(im, cx, y, olive)
            for li, ly in enumerate(range(28-h+3, 26, 3)):
                for k in (1, 2):
                    px(im, cx-k, ly, olive); px(im, cx+k, ly+1, olive)
            # 이삭: 줄기 끝에서 우하로 휘어진 알갱이 5~6점
            tipx, tipy = cx, 28-h
            arc = [(0,0),(1,0),(1,1),(2,1),(2,2),(3,2)]
            for j, (dx, dy) in enumerate(arc):
                c = GOLD_LT if j < 2 else (GOLD if j < 4 else GOLD_DK)
                px(im, tipx+dx, tipy+dy, c)
            px(im, tipx+1, tipy-1, GOLD_LT)  # 반짝
    return im

def stone_vein():
    im = Image.new("RGBA", (32, 32), T)
    # 불규칙 실루엣 — 행별 (x0,x1), 상단 둥글고 우하 무게, 하단도 들쭉
    rows = {6:(11,20),7:(9,23),8:(7,25),9:(6,26),10:(5,27),11:(4,27),12:(4,28),13:(3,28),
            14:(3,28),15:(3,29),16:(2,29),17:(2,29),18:(2,29),19:(2,28),20:(3,28),21:(3,28),
            22:(4,27),23:(5,27),24:(6,26),25:(8,24),26:(11,21)}
    for y, (x0, x1) in rows.items():
        for x in range(x0, x1+1): px(im, x, y, ROCK_MD)
    # 좌상 하이라이트 페이셋(플랫 면 2덩이)
    for x, y in [(9,8),(10,8),(11,8),(8,9),(9,9),(10,9),(11,9),(12,9),(7,10),(8,10),(9,10),(10,10),
                 (6,12),(7,12),(8,12),(6,13),(7,13),(5,14),(6,14)]:
        px(im, x, y, ROCK_LT)
    # 우하 그늘 면
    for y in range(17, 26):
        x0, x1 = rows.get(y, (0,-1))
        for x in range(max(x0, x1-7), x1+1): px(im, x, y, ROCK_DK)
    # 균열 2줄 (DK 1px)
    for x, y in [(14,10),(15,11),(15,12),(16,13),(16,14),(17,15)]: px(im, x, y, ROCK_DK)
    for x, y in [(9,18),(10,19),(10,20),(11,21)]: px(im, x, y, ROCK_DK)
    # 금맥 — 좌상→우하 사선 맥 2갈래 + 반짝
    vein = [(12,14),(13,15),(14,16),(15,17),(16,18),(17,19),(18,20),(20,14),(21,15),(22,16),(22,17)]
    for j, (x, y) in enumerate(vein):
        px(im, x, y, GOLD if j % 3 else GOLD_DK)
        if j % 4 == 1: px(im, x+1, y, GOLD_DK)
    px(im, 13, 14, GOLD_LT); px(im, 21, 14, GOLD_LT)
    # 1px 외곽선 + 접지 그림자
    src = im.copy()
    for y in range(32):
        for x in range(32):
            if src.getpixel((x, y))[3] == 0:
                for dx, dy in ((1,0),(-1,0),(0,1),(0,-1)):
                    nx, ny = x+dx, y+dy
                    if 0 <= nx < 32 and 0 <= ny < 32 and src.getpixel((nx, ny))[3] > 0:
                        px(im, x, y, OUTLINE_OBJ); break
    for x in range(8, 26): px(im, x, 27, (0, 0, 0, 60))
    return im

def rock_tile(variant):
    im = Image.new("RGBA", (32, 32), ROCK_MD)
    r = random.Random(777 + variant)
    # 큰 페이셋 패치 — LT 2개(좌상광), DK 2개(우하), 플랫 채움
    def patch(cx, cy, w, h, c):
        for y in range(cy, cy+h):
            for x in range(cx, cx+w):
                if 0 <= x < 32 and 0 <= y < 32 and r.random() > 0.18: px(im, x, y, c)
    patch(r.randint(2,6), r.randint(2,5), 9, 6, ROCK_LT)
    patch(r.randint(16,20), r.randint(14,18), 8, 7, ROCK_DK)
    patch(r.randint(3,7), r.randint(18,21), 7, 5, ROCK_DK)
    patch(r.randint(18,21), r.randint(3,6), 6, 4, ROCK_LT)
    # 균열 — 연결된 사선 폴리라인 2개
    for _ in range(2):
        x, y = r.randint(4, 26), r.randint(3, 8)
        for _ in range(r.randint(10, 16)):
            px(im, x, y, ROCK_DK)
            x += r.choice((0, 1, 1)); y += r.choice((1, 1, 0))
            if x > 30 or y > 30: break
    # 자갈 점 소량 (LT 1px — 기존 노이즈의 1/4 밀도)
    for _ in range(7):
        px(im, r.randint(1, 30), r.randint(1, 30), ROCK_LT)
    return im

os.makedirs(OUT, exist_ok=True)
outs = {
    'crop_rice_seedling': crop_stage(0), 'crop_rice_growing': crop_stage(1), 'crop_rice': crop_stage(2),
    'struct32_stone_vein': stone_vein(), 'tile32_rock_a': rock_tile(0), 'tile32_rock_b': rock_tile(1),
}
for n, im in outs.items(): im.save(os.path.join(OUT, n + '.png'))
# 프리뷰 — 잔디 배경 8배
W = sum(im.width for im in outs.values()) * 8 + 10 * len(outs)
H = 32 * 8 + 40
cv = Image.new('RGBA', (W, H), GRASS_MD)
x = 5
for n, im in outs.items():
    big = im.resize((im.width*8, im.height*8), Image.NEAREST)
    cv.paste(big, (x, 20), big); x += big.width + 10
cv.save(os.path.join(OUT, '_preview_crop_rock_v2.png'))
print('generated:', list(outs))
