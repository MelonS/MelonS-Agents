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
import numpy as np
from PIL import Image

PACK = os.environ.get("TS_PACK_DIR", r"G:/ai/_artpacks/TinySwords")
OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                    "..", "unity-project", "Assets", "Sprites"))


# ── 지형 밸류 그레이드 (2026-07-29) ────────────────────────────────────
# 팩 원본 지형은 콜로니심에 쓰기엔 너무 밝다.  실측(상대휘도 L):
#   모래 0.737 · 잔디 0.400  vs  콜로니스트 0.095~0.220 · 나무 0.108~0.167
# = 화면에서 제일 밝은 게 게임플레이상 의미 없는 지면이고, 캐릭터가 제일
# 어둡다.  폰(L 0.105)과 나무(L 0.108)가 같은 밴드라 "내 캐릭터를 못 찾는"
# 상태였다 (페르소나 QA 다수가 독립적으로 같은 지적).
#
# 잔디 L=0.400 위에서 캐릭터가 3:1 로 뜨려면 L>=1.30 이 필요 — 물리적으로
# 불가능하다.  그러므로 "폰을 밝게"가 아니라 **지면을 낮춰** 캐릭터가 올라설
# 밸류 헤드룸을 만드는 것이 유일한 해법.  목표: 잔디 0.28 / 모래 0.38.
#
# 선형광에서 gain 을 곱하고(감마 보존) 채도만 살짝 죽인다 — 색상각(hue)은
# 건드리지 않아 팩의 아이덴티티는 유지된다.  ts_*.png 는 gitignore 라
# 파일을 직접 고치면 다음 추출에 덮인다 → 반드시 여기(추출 시점)에서 적용.
def _srgb2lin(x):
    return np.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4)


def _lin2srgb(x):
    return np.where(x <= 0.0031308, x * 12.92,
                    1.055 * np.clip(x, 0, 1) ** (1 / 2.4) - 0.055)


def grade(im, gain, sat=0.92):
    """선형광 gain + 채도 조정.  알파는 불변."""
    a = np.array(im.convert("RGBA")).astype(float)
    rgb = _srgb2lin(a[:, :, :3] / 255.0) * gain
    lum = (rgb * [0.2126, 0.7152, 0.0722]).sum(-1, keepdims=True)
    rgb = lum + (rgb - lum) * sat
    a[:, :, :3] = _lin2srgb(np.clip(rgb, 0, 1)) * 255.0
    return Image.fromarray(a.astype("uint8"))


# 실측 캘리브레이션값 (이 두 크롭 전용 — 다른 타일에 그대로 쓰면 안 됨)
GRADE_GRASS = 0.7023   # L 0.400 -> 0.279
GRADE_SAND = 0.5199    # L 0.737 -> 0.380


def crop_save(src, box, name, gain=None):
    im = Image.open(os.path.join(PACK, src)).convert("RGBA").crop(box)
    if gain is not None:
        im = grade(im, gain)
    im.save(os.path.join(OUT, name))
    print(name)


def save(src, name):
    Image.open(os.path.join(PACK, src)).convert("RGBA").save(os.path.join(OUT, name))
    print(name)


# 지형 (Tilemap_Flat: 10x4 @64px — (1,1)=잔디 중심, (6,1)=모래 중심)
crop_save("Terrain/Ground/Tilemap_Flat.png", (64, 64, 128, 128), "ts_tile_grass.png",
          gain=GRADE_GRASS)
crop_save("Terrain/Ground/Tilemap_Flat.png", (384, 64, 448, 128), "ts_tile_sand.png",
          gain=GRADE_SAND)

# ── 지형 전환 엣지 세트 (2026-07-29) ──────────────────────────────────
#  잔디↔모래 경계가 직각 계단으로 보이던 것(G-3)의 해결.  팩 Tilemap_Flat 은
#  이미 **4×4 엣지 세트**를 갖고 있다 — 그리지 않고 꺼내 쓰면 된다.
#
#  실측으로 확인한 배치 (알파 분석):
#    잔디 c0~c3 / 모래 c5~c8, 각각 r0~r3
#    열: 0=왼쪽잘림 1=가운데 2=오른쪽잘림 3=좌우모두잘림
#    행: 0=위잘림   1=가운데 2=아래잘림   3=상하모두잘림
#  즉 (열,행) = (좌우 이웃 유무, 상하 이웃 유무) 의 표준 4×4 매핑.
#  프린지가 얇아(불투명 85~100%) 큰 여백이 아니라 **너덜너덜한 가장자리**를 만든다 —
#  직각 계단을 없애는 데 정확히 맞는 형태다.
#
#  ⚠ 이 타일들은 가장자리가 투명하므로 **아래에 잔디 베이스가 깔려 있어야** 한다.
#   SceneSetup 이 베이스 타일맵(잔디) + 오버레이 타일맵(모래 엣지) 2층으로 그린다.
EDGE_COLS = {"grass": 0, "sand": 5}


def edge_set(kind, gain):
    base = EDGE_COLS[kind]
    for r in range(4):
        for c in range(4):
            x = (base + c) * 64
            y = r * 64
            crop_save("Terrain/Ground/Tilemap_Flat.png",
                      (x, y, x + 64, y + 64),
                      f"ts_tile_{kind}_e{r}{c}.png", gain=gain)


edge_set("sand", GRADE_SAND)
edge_set("grass", GRADE_GRASS)
# ts_tile_water.png 는 여기서 만들지 않는다 (2026-07-29).
#  팩 원본 물은 완전 단색이라 인게임에서 밋밋했고, 2026-07-27 에 gen-water.py 로
#  일렁임+물가 포말을 절차 생성해 대체했다.  그런데 두 스크립트가 같은 파일을 쓰고
#  있어서, extract 를 나중에 돌리면 그 재작성이 **조용히** 되돌려졌다(빌드도 게이트도
#  통과하므로 아무도 모른다 — 7/25 GUID 사고와 같은 계열).  소유권을 gen-water.py
#  단독으로 확정한다.  팩 원본이 필요하면 gen-water.py 안에서 참조할 것.
# 나무 (애니 시트 첫 프레임 192px)
crop_save("Resources/Trees/Tree.png", (0, 0, 192, 192), "ts_tree.png")
# 자원 더미 (128px, 그림자 베이크판) — 런타임 ItemArt32 가 Resources 에서 로드
save("Resources/Resources/W_Idle.png", "ts_wood_pile.png")
save("Resources/Resources/M_Idle.png", "ts_meat_pile.png")
save("Resources/Resources/G_Idle.png", "ts_gold_pile.png")
_res = os.path.normpath(os.path.join(OUT, "..", "Resources", "Sprites"))
os.makedirs(_res, exist_ok=True)
Image.open(os.path.join(PACK, "Resources/Resources/W_Idle.png")).convert("RGBA").save(os.path.join(_res, "ts_wood_pile.png"))
Image.open(os.path.join(PACK, "Resources/Resources/M_Idle.png")).convert("RGBA").save(os.path.join(_res, "ts_meat_pile.png"))
# 양 (첫 프레임)
crop_save("Resources/Sheep/HappySheep_Idle.png", (0, 0, 128, 128), "ts_sheep.png")
# 데코 (풀·덤불·버섯·잔돌 등 18종)
for i in range(1, 19):
    p = os.path.join(PACK, "Deco", f"{i:02d}.png")
    if os.path.exists(p):
        Image.open(p).convert("RGBA").save(os.path.join(OUT, f"ts_deco_{i:02d}.png"))


def unit_frame(src, name, canvas=96):
    """유닛 시트 첫 프레임(192px) → 타이트 크롭 → canvas 캔버스 하단중앙 정착.
    PPU=canvas 임포트 시 정확히 1×1 유닛, 발이 캔버스 바닥에 닿음."""
    im = Image.open(os.path.join(PACK, src)).convert("RGBA")
    fr = im.crop((0, 0, 192, 192))
    bbox = fr.getbbox()
    fr = fr.crop(bbox)
    sc = min((canvas - 8) / fr.width, (canvas - 4) / fr.height)
    fr = fr.resize((max(1, int(fr.width * sc)), max(1, int(fr.height * sc))), Image.LANCZOS)
    cv = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    cv.paste(fr, ((canvas - fr.width) // 2, canvas - fr.height - 2), fr)
    cv.save(name)
    print(os.path.basename(name))


# 림 4색 (아트 B2: 유닛 세대교체 — Pawn 시트 idle 첫 프레임)
for color in ["Blue", "Purple", "Red", "Yellow"]:
    unit_frame(f"Factions/Knights/Troops/Pawn/{color}/Pawn_{color}.png",
               os.path.join(OUT, f"ts_pawn_{color.lower()}.png"))
# 밴딧 = 고블린 횃불병 (런타임 Resources 로드용)
res_dir = os.path.normpath(os.path.join(OUT, "..", "Resources", "Sprites"))
os.makedirs(res_dir, exist_ok=True)
unit_frame("Factions/Goblins/Troops/Torch/Red/Torch_Red.png",
           os.path.join(res_dir, "ts_bandit.png"))
print("추출 완료 →", OUT)
