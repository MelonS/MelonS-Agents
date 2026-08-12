#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""gen-crops.py — 작물 3단계 + 수확물 아이템 스프라이트 생성.

계기 (2026-07-31 운영자): "작물은 왜 이렇게 해상도가 낮아?" /
"농작물 아이템이 오브젝트 이미지가 이상한데?"

실측한 문제
-----------
1. 기존 `crops32/crop_rice*.png` 는 32x32 캔버스에 **색이 7~9개뿐**이었다.
   음영이 없어 성긴 선 몇 개로 보인다 — 나무(_gen_tree_v3)는 전용 생성기로
   음영까지 넣어 만들었는데 작물만 그 파이프라인을 타지 않았다.
   특히 새싹 단계는 내용이 28x**7px** 이라 '풀 몇 가닥'으로 읽힌다.
2. 수확물이 **밭에 서 있던 벼 그림 그대로** 바닥에 떨어졌다.
   `CropEntity` 가 `MeatPileEntity.Spawn(..., spriteRipe, "농작물")` 로
   익은 작물 스프라이트를 재사용한다.  2026-06-12 에 "작물 채집하면 고기?" 를
   고치면서 고기 스프라이트 대신 작물 스프라이트를 넣었는데, **수확물 전용
   아이콘을 만들지 않고 재사용**한 것이 지금까지 남아 있었다.
   결과: 논에서 자라던 벼가 땅에 누워 있는 모양 — 곡식 다발이 아니다.

무엇을 만드나
-------------
  crop_rice_seedling.png  새싹   — 흙 위로 갓 올라온 어린 싹
  crop_rice_growing.png   생장   — 키가 오르고 잎이 벌어짐
  crop_rice.png           수확기 — 이삭이 고개를 숙임 (황금색)
  item_crop_v2.png        수확물 — **묶인 곡식 다발** (3프레임 시트, 다른 아이템과 동형)

톤은 `ts_palette` 하나에서만 가져온다 (값이 하나면 정의도 하나 — 그 파일 주석 참조).
아웃라인 OUT_C 는 팩 실측값이라 `check-art-tone.py` 가 이 값으로 판정한다.

usage:
  python games/pawnsim/scripts/gen-crops.py
"""
from __future__ import annotations
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.path.insert(0, str(Path(__file__).resolve().parent))

from PIL import Image, ImageDraw           # noqa: E402
from ts_palette import (                    # noqa: E402
    OUT_C, LEAF_L, LEAF_M, LEAF_D, GOLD_L, GOLD_M, GOLD_D, SOIL_D,
)

REPO = Path(__file__).resolve().parents[3]
RES = REPO / "games" / "pawnsim" / "unity-project" / "Assets" / "Resources"
CROPS = RES / "crops32"
ITEMS = RES / "items32"

S = 32          # 셀 = 32px (다른 스프라이트와 동일 PPU)


def _blade(d: ImageDraw.ImageDraw, x: int, base_y: int, h: int,
           lean: int, light, mid, dark):
    """한 포기 — 아웃라인 → 어두운 면 → 밝은 면 순으로 3톤을 얹는다.

    기존 작물이 성겨 보인 이유가 **음영 없음**이었으므로, 한 포기마다
    최소 3톤(외곽/그늘/빛)을 준다.  이게 나무·가구와 같은 규칙이다.
    """
    top = base_y - h
    tip_x = x + lean
    # 외곽 (한 픽셀 굵게 — 잔디 위에서 형태가 닫히게)
    d.line([(x - 1, base_y), (tip_x - 1, top)], fill=OUT_C)
    d.line([(x + 1, base_y), (tip_x + 1, top)], fill=OUT_C)
    d.line([(x, base_y + 1), (tip_x, top - 1)], fill=OUT_C)
    # 몸통 (그늘 → 빛)
    d.line([(x, base_y), (tip_x, top)], fill=dark)
    d.line([(x, base_y - 1), (tip_x, top + 1)], fill=mid)
    d.point((tip_x, top + 1), fill=light)


def seedling() -> Image.Image:
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    base = 24
    # 흙 자국 — 싹이 '심겨 있다'는 것을 보이게 (기존엔 공중에 뜬 선이었다)
    d.ellipse([8, base - 1, 23, base + 3], fill=SOIL_D)
    for x, h, lean in ((12, 7, -1), (16, 9, 0), (20, 7, 1)):
        _blade(d, x, base, h, lean, LEAF_L, LEAF_M, LEAF_D)
    return im


def growing() -> Image.Image:
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    base = 26
    d.ellipse([6, base - 1, 25, base + 3], fill=SOIL_D)
    for x, h, lean in ((10, 12, -2), (14, 16, -1), (18, 16, 1), (22, 12, 2)):
        _blade(d, x, base, h, lean, LEAF_L, LEAF_M, LEAF_D)
    return im


def ripe() -> Image.Image:
    """수확기 — 이삭이 고개를 숙인다.  줄기는 초록, 이삭은 금색."""
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    base = 27
    d.ellipse([5, base - 1, 26, base + 3], fill=SOIL_D)
    for x, h, lean in ((9, 15, -2), (13, 19, -1), (18, 19, 1), (22, 15, 2)):
        _blade(d, x, base, h, lean, LEAF_L, LEAF_M, LEAF_D)
        # 이삭 — 줄기 끝에서 바깥으로 처진 낟알 덩어리
        tx, ty = x + lean, base - h
        d.ellipse([tx - 3, ty - 3, tx + 2, ty + 2], fill=OUT_C)
        d.ellipse([tx - 2, ty - 2, tx + 1, ty + 1], fill=GOLD_D)
        d.ellipse([tx - 2, ty - 2, tx, ty], fill=GOLD_M)
        d.point((tx - 1, ty - 2), fill=GOLD_L)
    return im


def crop_item_frame(size: int) -> Image.Image:
    """수확물 = **묶인 곡식 다발**.  서 있는 작물이 아니라 '거둬서 묶어 놓은 것'.

    다른 바닥 아이템(목재/석재)과 같은 어법: 낮고 옆으로 퍼진 덩어리 +
    팩 아웃라인.  size 로 더미 크기(1/2/3단계)를 만든다.
    """
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx, base = 16, 22
    w = 5 + size * 3          # 다발 반폭
    h = 4 + size              # 높이
    # 눕혀 놓은 다발 (타원 몸통 + 외곽)
    d.ellipse([cx - w - 1, base - h - 1, cx + w + 1, base + 2], fill=OUT_C)
    d.ellipse([cx - w, base - h, cx + w, base + 1], fill=GOLD_D)
    d.ellipse([cx - w, base - h, cx + w - 2, base - 1], fill=GOLD_M)
    d.ellipse([cx - w + 2, base - h + 1, cx - 1, base - 3], fill=GOLD_L)
    # 묶은 끈 — '수확물'이라는 것을 한 픽셀로 말해 준다
    d.line([(cx - 1, base - h), (cx - 1, base + 1)], fill=LEAF_D)
    d.line([(cx, base - h), (cx, base + 1)], fill=LEAF_M)
    # 삐져나온 낟알 몇 알
    d.point((cx - w + 1, base - h - 1), fill=GOLD_L)
    d.point((cx + w - 2, base - h), fill=GOLD_L)
    return im


def main() -> int:
    CROPS.mkdir(parents=True, exist_ok=True)
    ITEMS.mkdir(parents=True, exist_ok=True)

    for name, im in (("crop_rice_seedling.png", seedling()),
                     ("crop_rice_growing.png", growing()),
                     ("crop_rice.png", ripe())):
        p = CROPS / name
        im.save(p)
        cols = len(im.getcolors(99999) or [])
        bb = im.getbbox()
        print(f"  {name:26s} 색수 {cols:3d}  내용 {bb[2]-bb[0]}x{bb[3]-bb[1]}")

    # 아이템 시트 — 3프레임 가로 배치 (item_wood_v2 와 동형: 96x32)
    sheet = Image.new("RGBA", (S * 3, S), (0, 0, 0, 0))
    for i in range(3):
        sheet.paste(crop_item_frame(i), (S * i, 0))
    p = ITEMS / "item_crop_v2.png"
    sheet.save(p)
    print(f"  {p.name:26s} 색수 {len(sheet.getcolors(99999) or []):3d}  {sheet.size}")
    print("생성 완료 — Unity 가 임포트하면 .meta 의 PPU 32 / sliceMode 를 확인할 것")
    return 0


if __name__ == "__main__":
    sys.exit(main())
