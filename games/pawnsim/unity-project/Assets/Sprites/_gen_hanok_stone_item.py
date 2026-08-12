# -*- coding: utf-8 -*-
"""_gen_hanok_stone_item.py — 석재(자원) 아이템 + 광맥.  **정본.**

계기 (2026-08-02 운영자): "석재 어떤식으로 표현하는지 보고와 우리껀 너무 안보여."

── 실측 진단 ────────────────────────────────────────────────────────────
안 보이는 이유는 취향이 아니라 **밝기**였다.  팔레트를 재보면:

    ROCK_MD  밝기 0.39   ↔   GRASS_MD 밝기 0.41
    ROCK_LT  밝기 0.52   ↔   GRASS_LT 밝기 0.49

돌 램프가 잔디 램프 **위에 그대로 포개져 있다**.  밝기 차이가 2%면 색상이
달라도 눈은 형태를 분리하지 못한다 — 회색 돌을 초록 잔디에 놓았는데도 얼룩처럼
읽히는 게 이 때문이다.  거기에 기존 `prop64_stone_chunk.png` 는 외곽선 없는
**매끈한 회색 달걀**(64px 중 채움 86%, 각진 면 0개)이라 실루엣 신호도 없었다.

── 조사한 작법 ──────────────────────────────────────────────────────────
레퍼런스 콜로니심의 아트 가이드가 명시하는 규칙 (spdskatr, RWModdingResources
"Officially unofficial guide to the artstyle"):
  · **intensity hierarchy** — 플레이어가 손댈 수 있는 물건(story-relevant)은
    눈에 띄어야 하고, 식물·지형은 일부러 물러나야 한다.  석재는 **주워서 나르는
    물건**이므로 물러나면 안 되는 쪽인데, 우리는 지형처럼 그려 놓았다.
  · **아이템·건물은 2~3px 검은 외곽선**, 식물은 외곽선 없음/어두운 초록.
    (타일 64px 기준 2px — 128px 캔버스면 4px)
  · **"3px 미만 디테일은 낭비"** — 큰 색 대비가 없으면 안 보인다.
출처: https://spdskatr.github.io/RWModdingResources/artstyle.html

돌 형태 작법은 이미 `_gen_hanok_rock.py` 가 따르는 SLYNYRD Pixelblog 13 규칙
(각진 면 / 덩어리에서 깎아 내려가기) 을 그대로 재사용한다 — 같은 돌 언어를
두 번 정의하지 않기 위해 `_facets` / `_chunk` 를 import 한다.

── 그래서 무엇을 바꾸는가 ───────────────────────────────────────────────
  ① **밝기를 잔디 위로 올린다** — 화강암은 실제로 밝은 돌이다.  한국 석조물
     (성곽·석탑·석굴)이 전부 화강암이고 멀리서도 하얗게 보인다.  기준색 밝기를
     0.66 로 올려 GRASS_LT(0.49) 와 확실히 갈라 놓는다.  한국풍 방향과 가시성
     문제의 해답이 같은 곳에 있다.
  ② **2px 외곽선을 두른다** — 지형(타일)에는 안 두르고 아이템/광맥에만.
     그게 위 가이드가 말하는 intensity hierarchy 를 색이 아니라 규칙으로 지키는 법.
  ③ **각진 파편 여러 개로 쌓는다** — 달걀 하나 → 파편 3~4개.
  ④ **양이 실루엣으로 읽히게** 3단계 시트를 만든다 (기존엔 몇 개를 캐도 그림이
     똑같았다 — ItemArt32 가 석재만 단일 스프라이트로 고정돼 있었다).

usage: python _gen_hanok_stone_item.py [--stage]
"""
from __future__ import annotations
import sys
import os
import math
import random
import colorsys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import palette as P  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402
from _gen_hanok_rock import _chunk, shade, outline  # noqa: E402  (돌 언어 단일 출처)
from _assetpaths import save_everywhere, rel  # noqa: E402  (경로 규칙 단일 출처)


# ── 화강암 기준색 ────────────────────────────────────────────────────────
# 잔디(GRASS_LT 밝기 0.49) 위로 확실히 떠오르는 밝기.  약간의 분홍기는 장석(長石)
#  — 한국 화강암의 실제 색이고, 무채색 회색보다 초록 배경에서 더 분리된다.
GRANITE = (196, 188, 182, 255)          # 밝기 0.74 — 잔디와 0.25 차이
GRANITE_MD = shade(GRANITE, -0.16)
GRANITE_DK = shade(GRANITE, -0.36)
FELDSPAR = (206, 186, 180, 255)         # 장석 — 살짝 분홍
BIOTITE = (58, 54, 52, 255)             # 흑운모 — 화강암의 식별 신호(검은 알갱이)
FELDSPAR_UNUSED = FELDSPAR              # 알갱이 색은 _chunk 가 speck 인자로 받는다


def _shard(d, rnd, cx, cy, r, tilt=0.0):
    """석재 파편 하나 — 광맥과 **같은** `_chunk` 를 화강암 색으로 호출한다.

    돌 문법(능선 분할·균열·알갱이)은 `_gen_hanok_rock._chunk` 한 곳에만 있다.
    여기서 다시 구현하면 두 벌이 갈라진다 — 실제로 갈라져서 아이템만 면 대비가
    살고 광맥은 창백하게 남아 있었다."""
    _chunk(d, rnd, cx, cy, r, GRANITE, BIOTITE, tilt=tilt)


def _ground_shadow(im, cx, cy, w, h):
    """접지 그늘 — 없으면 돌이 공중에 뜬다.  외곽선보다 **아래** 층에 깔린다."""
    sh = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2),
                               fill=(20, 16, 14, 78))
    return Image.alpha_composite(sh, im)


def chunk(size=128, seed=7, count=3, ol=None):
    """석재 덩이 — 파편 `count` 개가 쌓인 더미.  개수로 양이 읽힌다."""
    rnd = random.Random(seed)
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    u = size / 128.0

    # 배치 — 뒤(위)부터 그려 앞 파편이 덮게 한다.  한 칸의 ~0.88 을 채운다.
    layouts = {
        1: [(64, 78, 30)],
        3: [(64, 62, 27), (42, 84, 26), (86, 86, 25)],
        5: [(64, 52, 24), (38, 70, 24), (90, 72, 23), (52, 92, 25), (84, 96, 22)],
    }
    for i, (cx, cy, r) in enumerate(layouts[count]):
        _shard(d, rnd, cx * u, cy * u, r * u, tilt=rnd.uniform(-0.3, 0.3))

    px = ol if ol is not None else max(1, round(size / 64.0 * 2))   # 타일 64px당 2px
    im = outline(im, px)
    im = _ground_shadow(im, 64 * u, 104 * u, 82 * u, 20 * u)
    return im


def stage_sheet(size=32):
    """ItemArt32 용 3단계 시트 (96×32) — 소/중/만재.

    기존엔 석재만 단일 스프라이트로 고정돼 1개를 캐든 40개를 캐든 그림이 같았다.
    양이 실루엣으로 읽혀야 '저기 많이 쌓였다' 가 한눈에 보인다."""
    sheet = Image.new("RGBA", (size * 3, size), (0, 0, 0, 0))
    for i, cnt in enumerate((1, 3, 5)):
        # 32px 에선 2px 외곽선이 실루엣을 다 먹는다 — 1px 로 (가이드의 '타일당 2px'
        #  은 64px 기준, 32px 타일이면 1px 이 같은 비율).
        sheet.alpha_composite(chunk(size, 7 + i * 13, cnt, ol=1), (i * size, 0))
    return sheet


def vein(size=128, seed=31):
    """광맥(노두) — 캐는 대상.  아이템과 같은 화강암 언어 + 외곽선.

    지형 타일과 달리 **상호작용 대상**이므로 물러나면 안 된다 (intensity hierarchy).
    기존 `struct32_stone_vein.png` 는 노란 빗금이 그어진 갈색 덩어리라 흙더미로
    읽혔고, 화강암 광맥이라는 신호가 전혀 없었다."""
    rnd = random.Random(seed)
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    u = size / 128.0
    for cx, cy, r in ((62, 50, 34), (34, 76, 28), (94, 78, 27), (64, 94, 26)):
        _shard(d, rnd, cx * u, cy * u, r * u)
    im = outline(im, max(1, round(size / 64.0 * 2)))
    im = _ground_shadow(im, 64 * u, 110 * u, 96 * u, 22 * u)
    return im


def icon(size=32):
    """UI 아이콘 — 어두운 패널 배경 위에 놓이므로 접지 그늘 없이, 외곽선 1px."""
    rnd = random.Random(5)
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    u = size / 32.0
    for cx, cy, r in ((16, 13, 8), (10, 21, 7), (22, 22, 7)):
        _shard(d, rnd, cx * u, cy * u, r * u)
    return outline(im, 1)


# 이름은 기존 파일을 그대로 덮어쓴다 — 새 이름을 만들면 어느 쪽이 화면에 나오는지
#  알 수 없게 된다(이 레포에서 반복된 '같은 사실이 두 곳에' 함정).
#
# (이름, 생성함수, 월드 크기[칸]) — 월드 크기는 여기서 정하고 .meta PPU 를 맞춘다.
#  실측된 문제: 런타임이 읽는 Resources/Sprites/prop64_stone_chunk 는 64px @ PPU 128
#  = **0.5칸**이었다.  잔디와 밝기가 같은 반 칸짜리 회색 덩어리 — 안 보이는 게 당연하다.
#  광맥(1.0칸) > 떨어진 석재(0.85칸) 순서를 지켜야 '캘 것' 과 '주울 것' 이 구분된다.
TARGETS = [
    ("prop64_stone_chunk",  lambda: chunk(128, 7, 3), 0.85),   # 바닥에 떨어진 석재 (정본)
    ("stone_chunk",         lambda: chunk(32, 7, 3, ol=1), 0.85),  # 폴백 경로
    ("struct32_stone_vein", lambda: vein(64), 1.00),           # 씬이 쓰는 광맥 기본
    ("stone_vein",          lambda: vein(32), 1.00),           # 구 폴백
    ("icon_stone",          lambda: icon(32), None),           # UI — 월드 크기 무관
]

def main() -> int:
    stage = "--stage" in sys.argv
    if stage:
        out = r"G:/ai/_stone_stage"
        os.makedirs(out, exist_ok=True)
        for name, fn, _w in TARGETS:
            fn().save(os.path.join(out, name + ".png"))
        stage_sheet().save(os.path.join(out, "item_stone_v2.png"))
        print(f"(검수용) {len(TARGETS) + 1}종 → {out}")
        return 0

    n = 0
    for name, fn, world in TARGETS:
        img = fn()
        paths = save_everywhere(img, name, world)
        n += len(paths)
        print(f"[ok] {name}.png ({img.width}x{img.height})"
              + (f" → {world}칸" if world else "") + f"  [{rel(paths)}]")

    # 3단계 시트 — ItemArt32 가 Resources/items32/ 에서 읽는다 (32px 프레임 = 1칸).
    sheet_dir = os.path.normpath(os.path.join(HERE, "..", "Resources", "items32"))
    if os.path.isdir(sheet_dir):
        p = os.path.join(sheet_dir, "item_stone_v2.png")
        stage_sheet().save(p)
        n += 1
        print("[ok] item_stone_v2.png (96x32, 3단계)")
    print(f"{n}개 파일 갱신")
    return 0


if __name__ == "__main__":
    sys.exit(main())
