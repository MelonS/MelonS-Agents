#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""gen-ui-icons.py — 건축/구역 메뉴 셀 아이콘 절차 드로잉.

배경 (2026-07-29):
  건축 메뉴의 구역 셀이 아이콘 자리에 **한글 첫 글자**를 크게 띄우고 있었다 —
  "경(작) 저(장) 폐(기) 지(붕)".  tofu 사고(폰트에 없는 이모지가 빈칸으로 그려짐)를
  수습하며 이모지를 걷어낼 때 남은 잔재로, 의미가 전달되지 않고 무엇보다
  **미완성으로 읽힌다.**  심사자가 10분 안에 반드시 여는 메뉴라 인상에 직결된다.
  ArchitectMenu.MakeShelfCell 은 이미 `Sprite icon` 인자를 받는데 null 이 들어가고
  있었다 — 자리는 있고 그림만 없었던 것.

규약:
  · 팔레트는 ts_palette.py 단일 출처 (팩 실측 아웃라인 (22,28,46)).
  · 64x64, 1px 아웃라인, 3톤 셀셰이딩 — 팩/절차 가구와 같은 문법.
  · check-art-tone.py 를 통과해야 반입한다.
  · Assets/Sprites 와 Resources/Sprites 양쪽에 저장 — ArchitectMenu.LoadIcon 이
    에디터에선 Assets/Sprites, 런타임 빌드에선 Resources/Sprites 를 본다.

사용: python games/pawnsim/scripts/gen-ui-icons.py
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ts_palette import (OUT_C, WOOD_L, WOOD_M, WOOD_D,
                        SOIL_L, SOIL_M, SOIL_D, LEAF_L, LEAF_M, LEAF_D,
                        GOLD_M, CREAM)

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "unity-project", "Assets", "Sprites"))
RES = os.path.normpath(os.path.join(HERE, "..", "unity-project", "Assets",
                                    "Resources", "Sprites"))
S = 64


def canvas():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def box(d, xy, fill, ow=3):
    d.rectangle(xy, fill=fill, outline=OUT_C, width=ow)


def farm():
    """경작 — 갈아엎은 이랑 + 새싹.  '심는 곳'이 한눈에."""
    im, d = canvas()
    # 이랑 3줄 (원근 없이 정면 — 셀이 작아 단순할수록 읽힌다)
    for i, y in enumerate((30, 42, 54)):
        d.rectangle([6, y - 6, 58, y + 3], fill=SOIL_M, outline=OUT_C, width=3)
        d.rectangle([9, y - 4, 55, y - 1], fill=SOIL_L)
    # 새싹 두 포기
    for cx in (22, 42):
        d.rectangle([cx - 1, 12, cx + 1, 28], fill=LEAF_D)
        d.ellipse([cx - 11, 8, cx - 1, 20], fill=LEAF_M, outline=OUT_C, width=2)
        d.ellipse([cx + 1, 6, cx + 11, 18], fill=LEAF_L, outline=OUT_C, width=2)
    return im


def stockpile():
    """저장 — 나무 상자 두 개를 쌓은 더미."""
    im, d = canvas()
    box(d, [8, 30, 38, 56], WOOD_M)          # 아래 왼쪽
    d.rectangle([12, 34, 34, 40], fill=WOOD_L)
    box(d, [34, 30, 58, 56], WOOD_D)         # 아래 오른쪽
    box(d, [18, 8, 48, 32], WOOD_M)          # 위
    d.rectangle([22, 12, 44, 19], fill=WOOD_L)
    d.line([18, 20, 48, 20], fill=WOOD_D, width=3)   # 뚜껑 띠
    return im


def dump():
    """폐기 — 상자 + 아래로 쏟아지는 화살표.  저장과 실루엣이 갈려야 한다."""
    im, d = canvas()
    box(d, [8, 34, 44, 58], WOOD_D)
    d.rectangle([12, 38, 40, 44], fill=WOOD_M)
    # 아래 방향 화살표
    d.rectangle([46, 8, 54, 34], fill=GOLD_M, outline=OUT_C, width=3)
    d.polygon([(38, 32), (62, 32), (50, 52)], fill=GOLD_M, outline=OUT_C)
    return im


def roof():
    """지붕 영역 — 박공 지붕.  '덮는다'가 읽히게 벽 없이 지붕만."""
    im, d = canvas()
    d.polygon([(6, 40), (32, 12), (58, 40)], fill=WOOD_M, outline=OUT_C)
    d.polygon([(14, 38), (32, 19), (50, 38)], fill=WOOD_L)
    d.rectangle([6, 40, 58, 50], fill=WOOD_D, outline=OUT_C, width=3)   # 처마
    d.line([32, 14, 32, 38], fill=WOOD_D, width=3)                      # 용마루
    return im


def quantize(im, n=10):
    """팩과 같은 플랫 셀셰이딩으로 스냅 — 안티에일리어싱이 만든 중간색 제거.
    (LoRA 나무에서 색 수 405~1022 로 톤이 깨졌던 것과 같은 이유.)"""
    a = im.split()[-1]
    rgb = im.convert("RGB")
    bbox = a.getbbox()
    src = rgb.crop(bbox) if bbox else rgb
    pal = src.quantize(colors=n, method=Image.MEDIANCUT, dither=Image.NONE)
    out = rgb.quantize(palette=pal, dither=Image.NONE).convert("RGB")
    out.putalpha(a)
    return out


ICONS = {
    "icon_zone_farm": farm,
    "icon_zone_stock": stockpile,
    "icon_zone_dump": dump,
    "icon_zone_roof": roof,
}

os.makedirs(RES, exist_ok=True)
for name, fn in ICONS.items():
    im = quantize(fn())
    im.save(os.path.join(OUT, name + ".png"))
    im.save(os.path.join(RES, name + ".png"))
    print(f"{name}.png  {im.size}")
print(f"완료 → {OUT} + {RES}")
