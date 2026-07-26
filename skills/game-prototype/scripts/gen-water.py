#!/usr/bin/env python3
"""gen-water.py — 물 타일 + 물가 포말 밴드 생성 (TS 팔레트 정합).

계기 (2026-07-27): 가상 유저 평가에서 **4명이 독립적으로** 좌하단 호수를
"정체불명의 미완성 UI 패널"로 읽었다.  아트 디렉터는 "완전한 직사각형, 가로 밴딩
스트라이프, 해안선·셰이딩 전무 — 차라리 맵에서 빼는 게 낫다"고 판정했다.

실물 확인 결과:
  - `ts_tile_water.png` = 64x64 **완전 단색**(#44A0A0 계열).  TS 팩은 평평한 물 +
    별도 Foam 스프라이트로 물가를 그리는 설계라, 물 타일만 뽑아 쓰면 색면이 된다.
  - `scatter/water_edge_band.png` = 16x4 **남색 막대**.  v2(32px) 시절 자산이라
    64px 타일 위에서 검은 띠로 보이고, 배치 코드가 경계 방향과 무관하게 항상
    수평으로 놓아 "물 위에 떠 있는 검은 바"가 된다.

이 스크립트는 둘 다 다시 그린다.  타일은 **이음매 없는(seamless)** 것이 필수 —
가로/세로 모두 wrap 되는 노이즈만 쓴다.

usage: python skills/game-prototype/scripts/gen-water.py
"""
from __future__ import annotations
import math
import random
import sys
from pathlib import Path

from PIL import Image

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1] / "unity-project" / "Assets"
TILE_OUT = ROOT / "Sprites" / "ts_tile_water.png"
BAND_OUT = ROOT / "Resources" / "scatter" / "water_edge_band.png"

N = 64            # 타일 한 변 (TS 지형 규격)
SEED = 20260727

# TS 물 관찰 팔레트 — 원본 단색에서 명도만 위아래로 벌린다 (색조 유지).
DEEP  = (0x35, 0x7F, 0x82)
BASE  = (0x44, 0x93, 0x94)
LIGHT = (0x55, 0xA6, 0xA4)
FOAM  = (0xC9, 0xE6, 0xDF)


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def water_tile() -> Image.Image:
    """이음매 없는 물 타일.

    사인 합성만 쓰되 **주기가 타일 폭을 정수분할**하게 잡아 wrap 을 보장한다
    (랜덤 노이즈를 쓰면 가장자리가 안 맞아 격자 시임이 생긴다).
    진폭은 작게 — 지형은 밸류 위계상 가장 낮아야 하고, 물이 튀면 캐릭터가 묻힌다.
    """
    im = Image.new("RGBA", (N, N))
    px = im.load()
    for y in range(N):
        for x in range(N):
            u = x / N * 2 * math.pi
            v = y / N * 2 * math.pi
            # **저주파 + 저진폭**.  1차 시도는 주기 2·3·5 를 큰 진폭으로 겹쳐서 니트/
            #  하운드투스 무늬가 돼 버렸다 — 예전 지형 타일이 "스웨터 같다"고 반려된 것과
            #  같은 함정(art-style-guide-v3 큐레이션: 1x 배율에서 잔디테일은 노이즈가 된다).
            #  물은 지형이라 밸류 위계상 가장 낮아야 하므로, 모티프가 읽히면 안 된다.
            #  주기 1~2 만 쓰고 진폭을 1/4 로 줄여 "아주 옅은 일렁임"만 남긴다.
            w = (0.60 * math.sin(u + 0.35 * math.sin(v))
                 + 0.40 * math.sin(v * 2 + 1.3))
            t = (w + 1.0) * 0.5            # 0..1
            # 중앙 40% 구간에 몰아넣어 대비를 더 눌러준다 (BASE 근처만 사용).
            t = 0.30 + t * 0.40
            if t < 0.5:
                c = lerp(DEEP, BASE, t / 0.5)
            else:
                c = lerp(BASE, LIGHT, (t - 0.5) / 0.5)
            px[x, y] = (c[0], c[1], c[2], 255)
    return im


def foam_band() -> Image.Image:
    """물가 포말 밴드 — 64x16.

    기존 16x4 남색 막대를 대체.  배치 코드가 방향을 안 가리므로(경계와 무관하게
    수평 배치), **어느 방향에 놓여도 '물거품'으로 읽히도록** 좌우로 부드럽게
    사라지는 밝은 띠로 만든다.  어두운 막대는 어떤 방향에서도 이물질로 보인다.
    """
    W, H = 64, 16
    im = Image.new("RGBA", (W, H))
    px = im.load()
    rnd = random.Random(SEED)
    for x in range(W):
        # 좌우 끝으로 갈수록 사라짐 (인접 타일과 부드럽게 이어지게)
        edge = math.sin(math.pi * (x + 0.5) / W) ** 0.8
        crest = 0.5 + 0.5 * math.sin(x / W * 4 * math.pi + rnd.random() * 0.3)
        for y in range(H):
            # 위쪽이 물, 아래쪽이 뭍 — 가운데에 포말이 몰리게
            band = math.sin(math.pi * (y + 0.5) / H) ** 1.6
            a = edge * band * (0.35 + 0.45 * crest)
            if a <= 0.02:
                px[x, y] = (0, 0, 0, 0)
                continue
            c = lerp(LIGHT, FOAM, min(1.0, a * 1.3))
            px[x, y] = (c[0], c[1], c[2], int(min(255, a * 210)))
    return im


def main() -> int:
    if not TILE_OUT.parent.is_dir():
        print(f"✗ 경로 없음: {TILE_OUT.parent}")
        return 2
    water_tile().save(TILE_OUT)
    print(f"✔ {TILE_OUT.name}  ({N}x{N}, 이음매 없음)")
    BAND_OUT.parent.mkdir(parents=True, exist_ok=True)
    foam_band().save(BAND_OUT)
    print(f"✔ {BAND_OUT.name}  (64x16 포말)")
    print()
    print("주의: ts_tile_water.png 는 gitignore 대상(팩 재배포 금지)이라 커밋되지 않는다.")
    print("      extract-tinyswords.py 를 다시 돌리면 단색 원본으로 덮이므로,")
    print("      추출 후에는 이 스크립트를 이어서 실행할 것.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
