# -*- coding: utf-8 -*-
"""나무 v3 — 캐노피 3톤 입체 + 잎 덩어리(로브) 구분 + 트렁크 음영.
artv2 스펙 준수(팔레트 import, 디더링 금지, 좌상 조명).  32x48, PPU 32.
flora32_tree_a/b/c 교체.  TreeEntity 가 species baseTint 곱하므로 중립 녹색 유지."""
import sys, os, random, math
sys.stdout.reconfigure(encoding="utf-8")
PAL = r"G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Sprites"
sys.path.insert(0, PAL)
import palette as P
from PIL import Image
OUT = r"G:/ai/_artv2_staging/out"
T = (0,0,0,0)
G_DK, G_MD, G_LT = P.GRASS_DK, P.GRASS_MD, P.GRASS_LT
# 잎 그림자(가장 어두운 4번째 톤) — G_DK 에서 명도 -12% 파생 (스펙 허용)
G_SH = (int(G_DK[0]*0.78), int(G_DK[1]*0.80), int(G_DK[2]*0.78), 255)
W_LT, W_MD, W_DK = P.WOOD_LT, P.WOOD_MD, P.WOOD_DK
OUTLINE = getattr(P, 'OUTLINE_PLANT', (40, 52, 32, 255))

def px(im,x,y,c):
    if 0<=x<im.width and 0<=y<im.height: im.putpixel((x,y),c)

def ellipse(cx,cy,rx,ry):
    s=set()
    for y in range(int(cy-ry-1), int(cy+ry+2)):
        for x in range(int(cx-rx-1), int(cx+rx+2)):
            if ((x-cx)/rx)**2 + ((y-cy)/ry)**2 <= 1.0: s.add((x,y))
    return s

# 로브: (cx,cy,rx,ry).  첫 로브 = 메인, 나머지 = 주변 클럼프
SHAPES = {
 "a": [(16,15,11,10),(23,11,6,6),(9,18,7,6),(13,8,6,6),(20,19,5,5)],
 "b": [(15,16,11,10),(8,12,6,6),(22,18,7,6),(19,8,6,6),(11,20,5,4)],
 "c": [(16,14,10,11),(22,18,6,6),(11,8,6,6),(10,18,5,5),(21,9,5,5)],
}

def make_tree(variant):
    im = Image.new("RGBA",(32,48),T)
    # 트렁크 (4px 폭, 좌상광 3톤) + 밑동 플레어
    for y in range(23,46):
        for x in range(14,18):
            px(im,x,y, W_LT if x==14 else (W_DK if x==17 else W_MD))
    for y in (44,45):
        px(im,12,y,W_MD); px(im,13,y,W_MD); px(im,18,y,W_DK); px(im,19,y,W_DK)
    # 옹이
    px(im,15,34,W_DK); px(im,16,34,W_DK); px(im,15,35,W_DK); px(im,16,35,W_DK); px(im,15,34,W_LT)

    lobes = SHAPES[variant]
    canopy = set()
    lobe_sets = []
    for (cx,cy,rx,ry) in lobes:
        e = ellipse(cx,cy,rx,ry); lobe_sets.append((cx,cy,e)); canopy |= e
    # 베이스 = 잎 그림자
    for (x,y) in canopy: px(im,x,y,G_SH)
    # 각 로브별 음영: 로브 중심 기준 좌상은 밝게, 우하는 어둡게 (덩어리 볼륨)
    glob_lit = (12, 9)   # 전역 광원(좌상)
    for (cx,cy,e) in lobe_sets:
        for (x,y) in e:
            # 로브 내부 깊이(가장자리에서 멀수록 밝힘 후보)
            edge = any((x+dx,y+dy) not in canopy for dx,dy in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,-1),(1,-1),(-1,1)))
            d_lobe = math.hypot((x-cx), (y-cy))
            # 전역 광 방향 성분
            lit_dir = (glob_lit[0]-x)*0.0 + (x < cx) + (y < cy)  # 로브의 좌상 1/4
            if edge:
                continue  # 가장자리는 그림자 유지 → 로브 경계가 보임
            if (x <= cx) and (y <= cy):
                px(im,x,y,G_MD)      # 로브 좌상 = 중간톤
            else:
                px(im,x,y,G_DK)      # 로브 우하 = 어두운 베이스
    # 전역 하이라이트: 캐노피 좌상 코어에 LT (메인 로브 좌상)
    mcx,mcy,_ = lobe_sets[0]
    hi = ellipse(mcx-3, mcy-3, lobes[0][2]*0.45, lobes[0][3]*0.45)
    for (x,y) in hi:
        if (x,y) in canopy and not any((x+dx,y+dy) not in canopy for dx,dy in ((1,0),(-1,0),(0,1),(0,-1))):
            px(im,x,y,G_LT)
    # 1px 외곽선
    src = im.copy()
    for y in range(48):
        for x in range(32):
            if src.getpixel((x,y))[3]==0:
                if any(0<=x+dx<32 and 0<=y+dy<48 and src.getpixel((x+dx,y+dy))[3]>0
                       for dx,dy in ((1,0),(-1,0),(0,1),(0,-1))):
                    px(im,x,y,OUTLINE)
    return im

os.makedirs(OUT, exist_ok=True)
outs = {f"flora32_tree_{v}": make_tree(v) for v in ("a","b","c")}
for n,im in outs.items(): im.save(os.path.join(OUT,n+".png"))
# 프리뷰 (잔디 배경)
cv = Image.new("RGBA",(32*6*len(outs)+10*len(outs), 48*6+20), G_MD)
x=5
for n,im in outs.items():
    big=im.resize((32*6,48*6),Image.NEAREST); cv.paste(big,(x,10),big); x+=32*6+10
cv.save(os.path.join(OUT,"_preview_tree_v3.png"))
print("trees v3 generated")
