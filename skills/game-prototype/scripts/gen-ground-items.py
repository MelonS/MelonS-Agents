#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""gen-ground-items.py — 바닥 아이템(목재·석재·고기·베리) 스프라이트 재작업.

계기 (2026-07-31 운영자): "아이템들 크기 너무 작음.  이거 레퍼런스랑 사이즈 맞춘거 맞음??"
그때는 `PileScale` 만 올렸는데(0.5~0.78 → 0.95~1.35), 실측해 보니 **스프라이트 원본이
문제였다**:

    item_wood_v2   프레임 32x32  내용 20x8   색 5
    item_stone_v2  프레임 32x32  내용 16x12  색 5
    item_meat_v2   프레임 32x32  내용 16x10  색 5
    item_berry_v2  프레임 32x32  내용 13x9   색 6

콜로니스트는 같은 32px 프레임에서 내용이 14x29 다.  즉 바닥 아이템이 **사람보다 3~4배
낮다**.  균일 스케일로 높이를 올리면 폭이 한 칸을 넘어 버리므로 스케일로는 못 고친다 —
원본을 다시 그려야 한다.

여기서 만드는 것: 같은 32px 프레임 안에서 **위로 쌓인 더미**.  낱개가 아니라 여러 개가
포개진 형태라야 '자원 더미'로 읽히고, 세로가 살아난다.
목표 치수는 내용 ~22x16 (타일의 0.69 x 0.50) — 사람(0.44 x 0.91)의 절반 남짓.

톤은 ts_palette 한 곳에서만 (값이 하나면 정의도 하나 — 그 파일 주석).
프레임 3개 = 더미 크기 3단계 (양이 많을수록 큰 프레임을 쓴다).

usage:
  python skills/game-prototype/scripts/gen-ground-items.py
"""
from __future__ import annotations
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.path.insert(0, str(Path(__file__).resolve().parent))

from PIL import Image, ImageDraw            # noqa: E402
from ts_palette import (                     # noqa: E402
    OUT_C, WOOD_L, WOOD_M, WOOD_D, RED_L, RED_M, RED_D,
    CREAM, SOIL_L, SOIL_M, SOIL_D, LEAF_D,
)

REPO = Path(__file__).resolve().parents[3]
ITEMS = (REPO / "skills" / "game-prototype" / "unity-project"
         / "Assets" / "Resources" / "items32")
S = 32

STONE_L, STONE_M, STONE_D = (196, 196, 200), (156, 156, 164), (112, 112, 122)


def _log(d, cx, cy, half_w, r, light, mid, dark):
    """통나무 하나 — 옆으로 누운 원통 (외곽 → 몸통 → 하이라이트 → 마구리)."""
    d.rounded_rectangle([cx - half_w - 1, cy - r - 1, cx + half_w + 1, cy + r + 1],
                        radius=r + 1, fill=OUT_C)
    d.rounded_rectangle([cx - half_w, cy - r, cx + half_w, cy + r],
                        radius=r, fill=dark)
    d.rounded_rectangle([cx - half_w, cy - r, cx + half_w, cy + r - 2],
                        radius=r - 1 if r > 1 else 1, fill=mid)
    d.line([(cx - half_w + 1, cy - r + 1), (cx + half_w - 1, cy - r + 1)], fill=light)
    d.ellipse([cx + half_w - 3, cy - r, cx + half_w, cy + r], fill=light)   # 마구리(나이테)


def wood_frame(step: int) -> Image.Image:
    """통나무 더미 — 아래 2~3개, 위로 1~2개 포개진다.  '쌓였다'가 핵심."""
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    base = 23
    rows = [(base, 3, 9), (base - 6, 2, 6), (base - 11, 2, 4)][:1 + step]
    for i, (y, r, hw) in enumerate(rows):
        n = 2 if i == 0 else 1
        for k in range(n):
            _log(d, 16 + (k * 7 - 3 if n > 1 else 0), y, hw, r, WOOD_L, WOOD_M, WOOD_D)
    return im


def _rock(d, cx, cy, w, h, light, mid, dark):
    d.ellipse([cx - w - 1, cy - h - 1, cx + w + 1, cy + h + 1], fill=OUT_C)
    d.ellipse([cx - w, cy - h, cx + w, cy + h], fill=dark)
    d.ellipse([cx - w, cy - h, cx + w - 2, cy + h - 2], fill=mid)
    d.ellipse([cx - w + 2, cy - h + 1, cx - 1, cy - 1], fill=light)


def stone_frame(step: int) -> Image.Image:
    """돌 더미 — 아래 넓게, 위로 좁게 쌓는다."""
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    base = 22
    rows = [[(11, 5, 4), (21, 5, 4)], [(16, 5, 4)], [(16, 4, 3)]][:1 + step]
    for i, row in enumerate(rows):
        for (x, w, h) in row:
            _rock(d, x, base - i * 7, w, h, STONE_L, STONE_M, STONE_D)
    return im


def meat_frame(step: int) -> Image.Image:
    """고기 — 붉은 살덩이 여러 점이 포개진다."""
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    base = 22
    rows = [[(11, 5, 4), (21, 5, 4)], [(16, 5, 4)], [(16, 4, 3)]][:1 + step]
    for i, row in enumerate(rows):
        for (x, w, h) in row:
            _rock(d, x, base - i * 6, w, h, RED_L, RED_M, RED_D)
            d.line([(x - w + 2, base - i * 6), (x + w - 3, base - i * 6)], fill=CREAM)  # 비계 결
    return im


def berry_frame(step: int) -> Image.Image:
    """베리 — 잎사귀 받침 위 열매 무더기."""
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    base = 22
    d.ellipse([16 - 10, base - 2, 16 + 10, base + 4], fill=LEAF_D)   # 잎 받침
    rows = [[(11, 4), (17, 4), (23, 4)], [(14, 4), (20, 4)], [(17, 4)]][:1 + step]
    for i, row in enumerate(rows):
        for (x, r) in row:
            cy = base - 2 - i * 6
            d.ellipse([x - r - 1, cy - r - 1, x + r + 1, cy + r + 1], fill=OUT_C)
            d.ellipse([x - r, cy - r, x + r, cy + r], fill=RED_D)
            d.ellipse([x - r, cy - r, x + r - 2, cy + r - 2], fill=RED_M)
            d.point((x - r + 1, cy - r + 1), fill=RED_L)
    return im


def build(name: str, fn) -> None:
    sheet = Image.new("RGBA", (S * 3, S), (0, 0, 0, 0))
    for i in range(3):
        sheet.paste(fn(i), (S * i, 0))
    sheet.save(ITEMS / name)
    f0 = fn(2); bb = f0.getbbox()
    print(f"  {name:20s} 최대프레임 내용 {bb[2]-bb[0]}x{bb[3]-bb[1]} "
          f"(타일 {(bb[2]-bb[0])/S:.2f} x {(bb[3]-bb[1])/S:.2f})  색 {len(sheet.getcolors(99999) or [])}")


def main() -> int:
    ITEMS.mkdir(parents=True, exist_ok=True)
    build("item_wood_v2.png", wood_frame)
    build("item_stone_v2.png", stone_frame)
    build("item_meat_v2.png", meat_frame)
    build("item_berry_v2.png", berry_frame)
    print("생성 완료 — PileScale 은 WoodPileEntity 참조 (원본이 커졌으므로 재조정 필요할 수 있음)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
