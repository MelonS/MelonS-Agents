#!/usr/bin/env python3
"""extract-tinyswords.py — Tiny Swords 팩에서 PawnSim 게임용 스프라이트 추출.

팩 라이선스(상업OK·크레딧 불요·재배포 금지) 때문에 추출물은 repo 에 커밋하지
않는다(.gitignore `ts_*.png`).  새 환경 셋업:
  1) https://pixelfrog-assets.itch.io/tiny-swords 무료 다운로드
  2) TS_PACK_DIR 환경변수 또는 기본 경로(G:/ai/_artpacks/TinySwords)에 압축 해제
  3) python skills/game-prototype/scripts/extract-tinyswords.py
출처 표기는 ATTRIBUTIONS.md 참조 (해커톤 규정 ④ 외부 에셋 출처 명시).
"""
import os
from PIL import Image

PACK = os.environ.get("TS_PACK_DIR", r"G:/ai/_artpacks/TinySwords")
OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                    "..", "unity-project", "Assets", "Sprites"))


def crop_save(src, box, name):
    im = Image.open(os.path.join(PACK, src)).convert("RGBA")
    im.crop(box).save(os.path.join(OUT, name))
    print(name)


def save(src, name):
    Image.open(os.path.join(PACK, src)).convert("RGBA").save(os.path.join(OUT, name))
    print(name)


# 지형 (Tilemap_Flat: 10x4 @64px — (1,1)=잔디 중심, (6,1)=모래 중심)
crop_save("Terrain/Ground/Tilemap_Flat.png", (64, 64, 128, 128), "ts_tile_grass.png")
crop_save("Terrain/Ground/Tilemap_Flat.png", (384, 64, 448, 128), "ts_tile_sand.png")
save("Terrain/Water/Water.png", "ts_tile_water.png")
# 나무 (애니 시트 첫 프레임 192px)
crop_save("Resources/Trees/Tree.png", (0, 0, 192, 192), "ts_tree.png")
# 자원 더미 (128px, 그림자 베이크판)
save("Resources/Resources/W_Idle.png", "ts_wood_pile.png")
save("Resources/Resources/M_Idle.png", "ts_meat_pile.png")
save("Resources/Resources/G_Idle.png", "ts_gold_pile.png")
# 양 (첫 프레임)
crop_save("Resources/Sheep/HappySheep_Idle.png", (0, 0, 128, 128), "ts_sheep.png")
# 데코 (풀·덤불·버섯·잔돌 등 18종)
for i in range(1, 19):
    p = os.path.join(PACK, "Deco", f"{i:02d}.png")
    if os.path.exists(p):
        Image.open(p).convert("RGBA").save(os.path.join(OUT, f"ts_deco_{i:02d}.png"))
print("추출 완료 →", OUT)
