#!/usr/bin/env python3
"""gen-ts-furniture.py — TS(Tiny Swords) 스타일 절차 드로잉 가구 생성.

운영자 승인 PoC: 아트비교 47 (2026-07-24 "이건 좋았어").
원리: 팩에 없는 가구를 생성형 없이 코드가 직접 그린다 — 팩 관찰로 뽑은
팔레트·두꺼운 웜브라운 아웃라인·3톤 셀 셰이딩·접지 그림자 규칙. 드리프트 0%.
이 산출물은 자작 오리지널(팩 에셋 미포함)이므로 커밋 대상.

캔버스 규격: 1×1칸=128px, 1×2칸=128×256 (PPU128 임포트 전제 — 줌인 내구).
"""
import os
from PIL import Image, ImageDraw

OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                    "..", "unity-project", "Assets", "Sprites"))
RES = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                    "..", "unity-project", "Assets", "Resources", "Sprites"))

# TS 관찰 팔레트
# 아웃라인 색 정정 (2026-07-29) — 팩 실측값으로 교체.
#  기존 (62,42,34) 은 "팩 관찰로 뽑은 웜브라운"이었는데, 팩 스프라이트 12종의
#  최암부를 실제로 재보니 최빈값이 쿨 네이비 (22,28,46) 이었다 (ts_tree·ts_deco_07
#  이 정확히 이 값, ts_sheep 은 (22,23,38)).  웜브라운은 휘도 L 0.0280 으로 팩
#  대역(0.009~0.012)을 벗어나, 절차 가구가 팩 옆에 놓이면 아웃라인만 옅게 떠 보였다.
#  check-art-tone.py 로 확인: struct64 침대·테이블이 전부 outline_L 0.028 로 FAIL.
#  한 상수가 절차 아트 전체의 톤을 결정하므로 여기만 고치면 일괄 정합된다.
OUT_C = (22, 28, 46)
WOOD_L, WOOD_M, WOOD_D = (222, 172, 112), (196, 140, 88), (160, 106, 62)
RED_L, RED_M, RED_D = (232, 106, 94), (204, 76, 66), (166, 56, 50)
GOLD_L, GOLD_M, GOLD_D = (244, 200, 96), (218, 166, 64), (178, 128, 44)
CREAM, CREAM_D = (244, 228, 190), (226, 214, 184)
SOIL_L, SOIL_M, SOIL_D = (206, 160, 104), (178, 130, 80), (140, 98, 58)
PIT_D, PIT_DD = (92, 66, 44), (70, 50, 34)
SHADOW = (40, 60, 40, 110)


def rr(d, box, r, fill, outline=None, ow=0):
    d.rounded_rectangle(box, radius=r, fill=fill, outline=outline, width=ow)


def bed(blanket_l, blanket_m, blanket_d, fancy=False):
    """1×2칸 침대 (128×256)."""
    im = Image.new("RGBA", (128, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([10, 214, 120, 250], fill=SHADOW)
    rr(d, [8, 8, 120, 232], 20, WOOD_M, OUT_C, 6)
    rr(d, [15, 15, 113, 225], 15, WOOD_L)
    rr(d, [21, 21, 107, 219], 13, CREAM, OUT_C, 4)
    # 베개
    rr(d, [30, 30, 98, 66], 12, (252, 244, 220), OUT_C, 4)
    d.rounded_rectangle([30, 56, 98, 62], radius=3, fill=CREAM_D)
    # 담요
    rr(d, [21, 80, 107, 219], 13, blanket_m, OUT_C, 4)
    d.rectangle([26, 85, 102, 102], fill=blanket_l)
    d.rounded_rectangle([26, 186, 102, 214], radius=11, fill=blanket_d)
    d.line([27, 108, 101, 108], fill=blanket_d, width=4)
    if fancy:
        # 고급: 금장 모서리 + 다이아 스티치
        for (cx, cy) in [(34, 118), (64, 132), (94, 118), (34, 158), (64, 172), (94, 158)]:
            d.polygon([(cx, cy-6), (cx+6, cy), (cx, cy+6), (cx-6, cy)], outline=GOLD_L, width=2)
        rr(d, [8, 8, 120, 30], 12, GOLD_M, OUT_C, 5)   # 헤드보드 금장
    return im


def table_chair():
    """1×1칸 식탁+의자 (128×128)."""
    im = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([10, 92, 122, 124], fill=SHADOW)
    # 의자 (좌측 뒤)
    rr(d, [6, 44, 38, 104], 8, WOOD_D, OUT_C, 4)
    rr(d, [10, 48, 34, 72], 6, WOOD_M)
    # 테이블 다리
    rr(d, [46, 66, 62, 108], 6, WOOD_D, OUT_C, 4)
    rr(d, [102, 66, 118, 108], 6, WOOD_D, OUT_C, 4)
    # 상판
    rr(d, [38, 26, 126, 84], 14, WOOD_M, OUT_C, 5)
    rr(d, [44, 31, 120, 66], 10, WOOD_L)
    d.line([52, 48, 112, 48], fill=WOOD_M, width=3)
    return im


def grave(mound=True):
    """1×2칸 무덤 (128×256)."""
    im = Image.new("RGBA", (128, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([10, 210, 118, 248], fill=SHADOW)
    if mound:
        rr(d, [14, 52, 114, 234], 30, SOIL_M, OUT_C, 6)
        rr(d, [24, 62, 104, 150], 22, SOIL_L)
        rr(d, [24, 186, 104, 224], 18, SOIL_D)
        # 나무 십자 표지 (크게 — PoC 피드백: 축소 시 뭉개짐 방지)
        rr(d, [52, 4, 76, 84], 8, WOOD_M, OUT_C, 5)
        rr(d, [28, 22, 100, 46], 8, WOOD_L, OUT_C, 5)
        d.line([56, 12, 56, 76], fill=WOOD_D, width=3)
    else:
        rr(d, [14, 40, 114, 236], 24, SOIL_D, OUT_C, 6)
        rr(d, [26, 52, 102, 224], 16, PIT_D)
        d.ellipse([40, 110, 88, 176], fill=PIT_DD)
        # 파낸 흙 무더기 (우상단)
        d.ellipse([88, 28, 122, 56], fill=SOIL_M, outline=OUT_C, width=4)
    return im


bed(RED_L, RED_M, RED_D).save(os.path.join(OUT, "struct64_bed_wood.png"))
bed(GOLD_L, GOLD_M, GOLD_D, fancy=True).save(os.path.join(OUT, "struct64_bed_fine.png"))
os.makedirs(RES, exist_ok=True)
# 식탁은 BuildManager 가 런타임 Resources.Load — Resources 에 배치 (플레이어 빌드 포함)
table_chair().save(os.path.join(RES, "struct64_table_chair.png"))
grave(False).save(os.path.join(RES, "grave64_empty.png"))
grave(True).save(os.path.join(RES, "grave64_mound.png"))
print("생성 완료: struct64_bed_wood/bed_fine (Sprites) + table_chair/grave64_empty/mound (Resources)")
