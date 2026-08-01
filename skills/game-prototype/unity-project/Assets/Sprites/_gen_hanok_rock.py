# -*- coding: utf-8 -*-
"""_gen_hanok_rock.py — 바위·광맥.  **정본.**

계기 (2026-08-01 운영자): "바위도 한국형으로 가능하려나 일단 돌이랑 바위 아트 변경 시급."

실측 진단: 기존 `prop64_vein_*.png` 는 **매끄러운 구 3개**였다.  그라데이션으로
둥글린 공이라 돌이 아니라 풍선/달걀로 읽힌다.  각진 면이 하나도 없다.

작법은 조사해서 따랐다 (SLYNYRD Pixelblog 13 — Rocks):
  · **"Angular lines lead to a sharp, hard look"** — 돌의 단단함은 각진 선에서 온다.
  · **"start with solid blobs of color, then chisel down the form"** — 덩어리로
    시작해 **깎아 내려간다**.  기존 에셋은 이 '깎기' 단계가 통째로 빠져 있었다.
  · 환경에 맞는 색을 쓴다 — 일반적인 회색 말고.
출처: https://www.slynyrd.com/blog/2019/1/22/pixelblog-13-rocks

한국형: 한국 산은 **화강암**이다.  밝은 회색 바탕에 검은 알갱이(흑운모)가 박힌
결정질 무늬가 화강암의 식별 신호다.  그래서 화강암을 기본 톤으로 잡고, 나머지
암종은 같은 문법에 색만 달리한다.

구현: 바위를 **다각형 면(facet) 여러 개**로 쌓는다.
  · 윗면(하늘을 보는 면) = 가장 밝다        · 좌상 면 = 중간 밝기
  · 우/아래 면 = 어둡다                      · 면 경계 = 1px 균열선
평면 채색(그라데이션 금지)이라 면과 면이 각으로 만나고, 그게 '깎인 돌' 을 만든다.

usage: python _gen_hanok_rock.py [--stage]
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


def shade(c, dv=0.0, s=None):
    r, g, b, a = c
    h, s0, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    if s is not None:
        s0 = s
    v = max(0.0, min(1.0, v * (1.0 + dv)))
    r2, g2, b2 = colorsys.hsv_to_rgb(h, s0, v)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


# 암종별 기준색 — 전부 팔레트 ROCK/DIRT 램프에서 파생 (톤 이탈 방지).
STONES = {
    "granite":   shade(P.ROCK_MD, +0.06, s=0.06),   # 화강암 — 밝은 회색, 거의 무채
    "sandstone": shade(P.DIRT_LT, +0.02, s=0.26),   # 사암 — 누런 기
    "limestone": shade(P.ROCK_LT, +0.10, s=0.10),   # 석회암 — 밝고 뿌옇다
    "marble":    shade(P.ROCK_LT, +0.20, s=0.04),   # 대리석 — 가장 밝다
}
SPECK = {                                            # 결정 알갱이 색
    "granite":   shade(P.ROCK_DK, -0.35, s=0.05),   # 흑운모 (검은 점) — 화강암의 신호
    "sandstone": shade(P.DIRT_DK, -0.10, s=0.30),
    "limestone": shade(P.ROCK_DK, +0.10, s=0.06),
    "marble":    shade(P.ROCK_MD, -0.05, s=0.05),
}


def outline(im, px, col=None):
    """실루엣 **바깥으로** px 만큼 외곽선을 두른다.

    레퍼런스 콜로니심 아트 가이드의 intensity hierarchy 규칙:
      · 플레이어가 손댈 수 있는 것(아이템·건물·광맥) = 2~3px 검은 외곽선으로 눈에 띈다
      · 식물·지형 = 외곽선 없음.  일부러 배경으로 물러난다
    출처: https://spdskatr.github.io/RWModdingResources/artstyle.html

    알파를 MaxFilter 로 부풀린 뒤 **원본이 없는 곳만** 칠하므로 안쪽 디테일이 살고
    실루엣이 정확히 px 만큼만 두꺼워진다.  (지형 타일에는 쓰지 않는다.)"""
    if px <= 0:
        return im
    from PIL import ImageFilter
    ring = Image.new("RGBA", im.size, col or P.OUTLINE_OBJ)
    ring.putalpha(im.split()[3].filter(ImageFilter.MaxFilter(px * 2 + 1)))
    return Image.alpha_composite(ring, im)


def _facets(rnd, cx, cy, r, n):
    """각진 덩어리 하나의 꼭짓점 — 원을 불규칙하게 찌그러뜨린 다각형.

    반지름을 꼭짓점마다 흔들어야 각이 생긴다.  균일하면 다시 원이 된다."""
    pts = []
    for i in range(n):
        a = (i / n) * math.tau + rnd.uniform(-0.12, 0.12)
        rr = r * rnd.uniform(0.72, 1.06)
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr * 0.86))
    return pts


# 광원 방향 — 좌상단.  이 레포의 절차 아트 전부가 쓰는 방향(돌만 다른 쪽에서 빛을
#  받으면 그 물체 하나가 붙여넣은 것처럼 뜬다).
LIGHT = (-0.46, -0.89)


def _clip(poly, nx, ny, d0):
    """반평면 nx*x + ny*y + d0 >= 0 으로 다각형을 자른다 (Sutherland–Hodgman).

    면을 '밝은 쪽 / 어두운 쪽' 으로 **실제로 갈라야** 각이 선다.  이전 구현은 꼭짓점을
    y 좌표로 걸러 부분집합을 칠했는데, 잘린 면이 얇은 조각이 되거나 비어 버려 결국
    밝은 색 하나로 덮인 각설탕처럼 읽혔다."""
    if not poly:
        return []
    out = []
    n = len(poly)
    for i in range(n):
        ax, ay = poly[i]
        bx, by = poly[(i + 1) % n]
        da = nx * ax + ny * ay + d0
        db = nx * bx + ny * by + d0
        if da >= 0:
            out.append((ax, ay))
        if (da >= 0) != (db >= 0):
            t = da / (da - db)
            out.append((ax + (bx - ax) * t, ay + (by - ay) * t))
    return out


def _chunk(d, rnd, cx, cy, r, base, speck, seed_i=0, tilt=0.0):
    """돌덩이 하나 — **명면과 암면을 각으로 맞대어** 깎인 돌을 만든다.

    광맥(_gen_hanok_rock)과 석재 아이템(_gen_hanok_stone_item)이 **같은 이 함수**를
    쓴다.  돌 문법을 두 곳에 두면 반드시 갈라진다 — 실제로 갈라져서, 아이템 쪽만
    면 대비가 살고 광맥은 창백한 종이 조각처럼 남아 있었다."""
    body = _facets(rnd, cx, cy, r, rnd.choice((5, 6, 7)))
    if tilt:
        body = [(cx + (x - cx) * math.cos(tilt) - (y - cy) * math.sin(tilt) * 0.6,
                 cy + (x - cx) * math.sin(tilt) * 0.6 + (y - cy) * math.cos(tilt))
                for (x, y) in body]

    # 능선(稜線) — 명면과 암면이 만나는 선.  광원에 수직이고 파편마다 조금씩 꺾인다.
    #  이 선 하나가 '깎였다' 를 만든다.
    nx, ny = LIGHT
    a = rnd.uniform(-0.34, 0.34)
    nx, ny = (nx * math.cos(a) - ny * math.sin(a), nx * math.sin(a) + ny * math.cos(a))
    d0 = -(nx * cx + ny * cy) - rnd.uniform(-0.16, 0.14) * r

    d.polygon(body, fill=shade(base, -0.10))            # 바탕(퇴화 방지)
    dark = _clip(body, -nx, -ny, -d0)
    if len(dark) >= 3:
        d.polygon(dark, fill=shade(base, -0.34))        # 암면
    lit = _clip(body, nx, ny, d0)
    if len(lit) >= 3:
        d.polygon(lit, fill=base)                       # 명면
        top = _clip(lit, nx, ny, d0 - r * 0.42)
        if len(top) >= 3:
            d.polygon(top, fill=shade(base, +0.11))     # 꼭대기 면 (3단 램프)

    # 파편끼리 겹칠 때 서로 안 녹도록 몸통 테두리를 한 톤 어둡게 (검은 인라인 대신
    #  가이드가 권하는 '음영 차이' 로 경계를 만든다).
    d.polygon(body, outline=shade(base, -0.52))

    # 균열 — **짧게**.  1차에서 덩어리 중심까지 긋는 긴 선을 썼더니 돌을 가로지르는
    #  막대기(나뭇가지)처럼 보였다.  실제 균열은 모서리에서 시작해 조금 들어가다 만다.
    for _ in range(rnd.randrange(1, 3)):
        i0 = rnd.randrange(len(body))
        x0, y0 = body[i0]
        ux, uy = (cx - x0), (cy - y0)
        L = math.hypot(ux, uy) or 1.0
        f = rnd.uniform(0.18, 0.34)           # 반지름의 1/5~1/3 만
        d.line((x0, y0, x0 + ux / L * r * f, y0 + uy / L * r * f),
               fill=shade(base, -0.40), width=max(1, int(r * 0.06)))

    # 결정 알갱이 — 화강암의 식별 신호
    for _ in range(int(r * 0.9)):
        a = rnd.uniform(0, math.tau)
        rr = rnd.uniform(0, r * 0.82)
        px_ = cx + math.cos(a) * rr
        py_ = cy + math.sin(a) * rr * 0.86
        d.point((px_, py_), fill=speck)


def _at_value(c, v_target):
    """색상은 두고 **명도만** 목표값으로 맞춘다.

    2026-08-02 실측: ROCK 램프(밝기 0.28/0.39/0.52)가 GRASS 램프(0.32/0.41/0.49)
    위에 그대로 포개져 있었다.  밝기 차이가 2%면 색이 달라도 형태가 분리되지 않는다
    — 운영자가 "돌이 안 보인다" 고 한 것의 측정된 원인.  광맥은 캐는 대상이므로
    잔디 위로 확실히 떠올라야 한다 (지형 타일은 반대로 물러나야 하므로 안 올린다)."""
    r, g, b, a = c
    h, s, _ = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    r2, g2, b2 = colorsys.hsv_to_rgb(h, s, v_target)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


def vein(kind: str, size=128, seed=5):
    """광맥 — 돌덩이 3~4개가 뭉친 노두(露頭).  **캐는 대상 = 눈에 띄어야 한다.**"""
    rnd = random.Random(seed + hash(kind) % 1000)
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    base, speck = _at_value(STONES[kind], 0.74), SPECK[kind]
    u = size / 128.0
    # 뒤(위)에 있는 덩이부터 그려 앞 덩이가 덮게 한다
    chunks = [(64, 58, 30), (38, 78, 26), (90, 80, 25), (64, 92, 22)]
    for i, (cx, cy, r) in enumerate(chunks):
        _chunk(d, rnd, cx * u, cy * u, r * u, base, speck, i)
    # 외곽선 — 타일 64px 당 2px (가이드 규칙).  접지 그늘보다 **먼저** 둘러야
    #  그늘이 외곽선 밖으로 새지 않는다.
    im = outline(im, max(1, round(size / 64.0 * 2)))
    sh = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((18 * u, 100 * u, 110 * u, 118 * u), fill=(20, 16, 14, 62))
    return Image.alpha_composite(sh, im)


def rock_tile(size=64, seed=9):
    """암반 지대 타일 — 갈라진 화강암 바닥.  이음매 없이 반복된다."""
    rnd = random.Random(seed)
    base = STONES["granite"]
    im = Image.new("RGBA", (size, size), shade(base, -0.10))
    d = ImageDraw.Draw(im)
    # 판 — 각진 다각형으로 바닥을 채운다 (원 금지)
    for _ in range(9):
        cx, cy = rnd.randrange(size), rnd.randrange(size)
        r = rnd.uniform(9, 16)
        pts = _facets(rnd, cx, cy, r, rnd.choice((5, 6)))
        d.polygon(pts, fill=shade(base, rnd.uniform(-0.09, +0.13)))
        d.line(pts + [pts[0]], fill=shade(base, -0.26), width=1)
    for _ in range(size * 2):                       # 알갱이
        d.point((rnd.randrange(size), rnd.randrange(size)), fill=SPECK["granite"])
    return im


TARGETS = [
    ("prop64_vein_granite", 128, lambda: vein("granite", 128, 11)),
    ("prop64_vein_sandstone", 128, lambda: vein("sandstone", 128, 23)),
    ("prop64_vein_limestone", 128, lambda: vein("limestone", 128, 37)),
    ("prop64_vein_marble", 128, lambda: vein("marble", 128, 51)),
    ("tile64_rock_a", 64, lambda: rock_tile(64, 9)),
]


def main() -> int:
    stage = "--stage" in sys.argv
    out = r"G:/ai/_rock_stage" if stage else HERE
    os.makedirs(out, exist_ok=True)
    for name, _sz, fn in TARGETS:
        img = fn()
        img.save(os.path.join(out, name + ".png"))
        print(f"[ok] {name}.png ({img.width}x{img.height})")
    print(f"{'(검수용) ' if stage else ''}{len(TARGETS)}종")
    return 0


if __name__ == "__main__":
    sys.exit(main())
