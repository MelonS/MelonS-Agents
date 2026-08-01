# -*- coding: utf-8 -*-
"""_gen_hanok_flora.py — 한국 수종 생성기 (소나무·대나무·진달래).

계기 (2026-08-01 운영자): "동,식물 전부 다 한국풍으로 바꿔야 느낌이 날거 같음".

무엇을 바꾸고 무엇을 그대로 두나 — **실측으로 판단했다**:
  · 동물(노루·멧돼지·닭·토끼·여우)은 전부 한국에 사는 종이라 그대로 둔다.
    바꿔야 할 이유가 없는 것을 바꾸면 검증된 에셋만 흔들린다.
  · 작물은 이미 **벼**다.  한국 농촌 그 자체라 손대지 않는다.
  · 문제는 **수종**이었다.  현재 소나무 자리에 북유럽식 삼각 침엽수가 서 있고
    가문비(Spruce)까지 있다 — 한국 산에 없는 나무다.  한국 풍경에서 가장
    먼저 눈에 들어오는 것은 **굽은 줄기의 적송**이므로, 여기를 바꾸면
    같은 맵이 통째로 다른 나라가 된다.

교체:
  flora64_pine   → 소나무(적송): 굽은 붉은 줄기 + 층층이 나뉜 수관
  ts_tree        → 큰 소나무 (같은 문법, 192px)
  flora64_spruce → 대나무: 마디 있는 곧은 대 여러 대 + 성긴 잎
  flora32_bush_berry / _picked → 진달래 (봄 한국 산의 분홍)

규약: palette.py 파생만 사용 / 좌상 광원 / 1x 에서 수종 식별.
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
from PIL import Image, ImageDraw
from _assetpaths import save_everywhere, rel  # noqa: E402  (경로 규칙 단일 출처)  # noqa: E402

ASSETS = os.path.normpath(os.path.join(HERE, ".."))


def shade(c, dv=0.0, s=None):
    r, g, b, a = c
    h, s0, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    if s is not None:
        s0 = s
    v = max(0.0, min(1.0, v * (1.0 + dv)))
    r2, g2, b2 = colorsys.hsv_to_rgb(h, s0, v)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


# ── 색 (기준 램프 파생) ────────────────────────────────────────────────
# 적송 줄기는 위로 갈수록 붉다 — 그 대비가 소나무의 식별 신호다.
BARK_DK = shade(P.WOOD_DK, -0.10, s=0.55)
BARK_MD = shade(P.WOOD_MD, -0.12, s=0.60)
BARK_RED = shade(P.CLOTH_RUST, -0.34, s=0.52)      # 윗줄기 적갈색
BARK_LT = shade(BARK_RED, +0.22)
NEEDLE_DK = shade(P.GRASS_DK, -0.30, s=0.52)
NEEDLE_MD = shade(P.GRASS_DK, -0.05, s=0.50)
NEEDLE_LT = shade(P.GRASS_MD, +0.06, s=0.44)
BAMBOO_MD = shade(P.GRASS_LT, +0.02, s=0.46)
BAMBOO_DK = shade(BAMBOO_MD, -0.24)
BAMBOO_LT = shade(BAMBOO_MD, +0.18)
LEAF_DK = shade(P.GRASS_DK, -0.06, s=0.48)
def _hue_shift(c, deg):
    """색상만 회전.  기준 램프에 분홍이 없어서 러스트를 자홍 쪽으로 돌려 만든다.
    스타일가이드 §2 는 포인트 컬러를 화면당 소량 허용한다 — 진달래는 봄 한 철,
    관목 몇 그루에만 쓰이므로 그 예외에 해당한다."""
    r, g, b, a = c
    h0, s0, v0 = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    r2, g2, b2 = colorsys.hsv_to_rgb((h0 + deg / 360.0) % 1.0, s0, v0)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


# 진달래 분홍 — 러스트(주황)를 자홍 쪽으로 -35° 돌린다.  채도만 올리면 주황으로
#  남아 '진달래' 가 아니라 '살구꽃' 으로 읽혔다(1차 실측).
AZALEA = _hue_shift(shade(P.CLOTH_RUST, +0.02, s=0.58), -32)
AZALEA_DK = shade(AZALEA, -0.20)
AZALEA_LT = shade(AZALEA, +0.16)


def blank(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def fit_inside(im, margin=1):
    """캔버스 밖으로 삐져나간 그림을 **가로로** 밀어 넣는다.  아래는 건드리지 않는다.

    2026-08-02 실측: ts_tree 오른쪽 가장자리에 불투명 픽셀 25개 — 수관이 잘려
    나가고 있었다.  줄기가 오른쪽으로 휘는 만큼 층 중심도 밀리는데 캔버스는
    그대로여서, 폭을 조절할 때마다 조용히 잘렸다 (화면에서는 '한쪽이 각지게 잘린
    나무' 로 보인다).  파라미터를 손으로 맞추는 대신 **결과를 재서** 밀어 넣는다.

    세로는 그대로 둔다 — 바닥이 접지선이라 위아래로 움직이면 나무가 땅에서 뜬다."""
    bb = im.getbbox()
    if bb is None:
        return im
    x0, _y0, x1, _y1 = bb
    w = im.size[0]
    dx = 0
    if x1 > w - margin:
        dx = (w - margin) - x1
    if x0 + dx < margin:
        dx = margin - x0
    if dx == 0:
        return im
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    out.alpha_composite(im, (dx, 0))
    return out


def outline(im, color=P.OUTLINE_PLANT, thickness=2):
    """불투명 픽셀 바깥 외곽선.

    2026-08-01 운영자 "한국풍 나무가 퀄리티가 좀 떨어짐. 외곽선이 없어서 그런가?"
    실측으로 답을 냈다 — **외곽선은 있었다**(소나무 292px).  색 수도 팩 나무와
    같다(8 vs 8~9).  진짜 차이는 두 가지였다:
      · **부피**: 소나무 불투명 1,598px(채움률 29%) vs 단풍 6,189px(63%)
      · **외곽선 두께**: 팩 나무는 가장 많은 색이 외곽선이다(상수리 912px) —
        1px 이 아니라 2~3px 라 무게가 실린다.
    그래서 기본 두께를 2px 로 올리고, 수관 부피를 함께 키운다."""
    w, h = im.size
    px = im.load()
    edge = []
    for y in range(h):
        for x in range(w):
            if px[x, y][3] == 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3] == 0:
                    edge.append((nx, ny))
    for x, y in edge:
        px[x, y] = color
    if thickness > 1:
        # 한 겹 더 — 재귀 대신 같은 절차를 반복해 두께를 만든다.
        return outline(im, color, thickness - 1)
    return im


def _pine(w, h, seed=7):
    """소나무(적송).

    1차 시안 실패: 수관 층을 4단으로 촘촘히 겹쳤더니 **하나의 덩어리**가 되어
    분재/관목처럼 보였고 줄기가 완전히 가려졌다.  적송의 형태 신호는 층 개수가
    아니라 **비율**이다 — 아래 60%가 맨줄기(굽은 붉은 기둥)이고 위 40%에만
    수관이 층져 얹힌다.  줄기가 보여야 소나무이고, 안 보이면 그냥 덤불이다.

    형태 신호 셋:
      ① 줄기가 굽는다  ② 위쪽 줄기가 붉다  ③ 수관이 위쪽에만, 옆으로 납작하게
    """
    rnd = random.Random(seed)
    im = blank(w, h)
    d = ImageDraw.Draw(im)
    cx = w // 2
    base_y = h - 2
    crown_bottom = int(h * 0.58)          # 여기까지는 맨줄기
    top_y = int(h * 0.08)

    # ① 굽은 줄기 — 밑에서 위까지 하나의 곡선.  수관보다 먼저 그려 뒤에 둔다.
    trunk_w = max(3, int(w * 0.10))
    pts = []
    for t in range(25):
        f = t / 24.0
        y = base_y - (base_y - top_y) * f
        bend = math.sin(f * math.pi * 0.75) * (w * 0.16)   # 한 번 크게 휜다
        pts.append((cx + bend, y))
    for i in range(len(pts) - 1):
        f = i / (len(pts) - 1)
        col = BARK_MD if f < 0.40 else BARK_RED            # ② 위로 갈수록 붉게
        tw = max(2, int(trunk_w * (1.0 - f * 0.45)))       # 위로 갈수록 가늘게
        d.line((pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1]), fill=col, width=tw)
        if i % 3 == 0:
            d.point((pts[i][0] - tw * 0.4, pts[i][1]), fill=BARK_LT)   # 좌측 미광
    # 밑동 뿌리
    for rx in (-trunk_w - 2, -1, trunk_w + 2):
        d.line((cx, base_y - 3, cx + rx, base_y), fill=BARK_DK, width=2)

    # 가지 — 수관 층으로 뻗는 짧은 붉은 가지 (층이 공중에 뜨지 않게)
    def trunk_at(f):
        y = base_y - (base_y - top_y) * f
        return cx + math.sin(f * math.pi * 0.75) * (w * 0.16), y

    # 2026-08-02 운영자: "방금 들어간 나무 좀 이상하게 생겼고 너무 큰데?
    #  그전 나무 모양이 더 좋긴했어."  → 수관을 **이전 모양(층진 우산)으로 되돌린다.**
    #
    #  경위: 8/01 "퀄리티가 좀 떨어짐" 지적에 부피와 질감으로 답했다 — 층을 넓히고
    #  덩이(blob) 여러 개로 잎을 쌓았다.  채움률은 29%→목표치로 올랐지만 실루엣이
    #  뭉개져 브로콜리가 됐다.  **부피가 문제가 아니라 층이 정체성이었다** — 적송은
    #  가지가 층층이 갈라져 우산처럼 퍼지는 게 신호고, 덩이로 채우면 그 층이 사라진다.
    #  두께 2px 외곽선은 그 라운드의 다른 절반이었고 옳았으므로 **유지**한다.
    # ③ 수관 — 위 40% 에만, 3층, 좌우로 넓게 (원뿔이 아니라 우산)
    # 층 수는 **캔버스 크기에 따라** 정한다.  작은 캔버스(54px)에 3층을 넣으면
    #  층당 높이가 3px 밖에 안 돼 서로 붙어 다시 덩어리가 된다 — 1차 실패의 재발.
    #  작을수록 적고 넓게: 큰 나무 3층 / 작은 나무 2층.
    # 층 폭 (2026-08-02 "너무 큰데") — 큰 캔버스(ts_tree 192px)에서 fw 0.46 이면
    #  수관이 좌우로 2.4칸 퍼져 참나무(1.5칸)를 압도했다.  월드 크기를 줄이는 것과
    #  **함께** 폭도 좁혀야 한다 — PPU 만 올리면 나무 전체가 작아져 잔가지가 뭉갠다.
    layers = ([(0.60, 0.36, 1), (0.75, 0.30, -1), (0.90, 0.19, 1)] if h >= 120
              else [(0.66, 0.52, 1), (0.88, 0.34, -1)])
    for li, (f, fw, side) in enumerate(layers):
        tx, ty = trunk_at(f)
        rw, rh = w * fw, max(3.0, h * 0.055)
        xo = side * w * 0.07
        d.line((tx, ty, tx + xo, ty - rh * 0.3), fill=BARK_RED, width=2)   # 가지
        d.ellipse((tx - rw + xo, ty - rh * 1.5, tx + rw + xo, ty + rh * 0.9), fill=NEEDLE_DK)
        d.ellipse((tx - rw * 0.80 + xo, ty - rh * 1.7,
                   tx + rw * 0.70 + xo, ty + rh * 0.15), fill=NEEDLE_MD)
        d.ellipse((tx - rw * 0.52 + xo, ty - rh * 1.7,
                   tx + rw * 0.12 + xo, ty - rh * 0.45), fill=NEEDLE_LT)
        for _ in range(int(rw * 1.2)):                    # 솔잎 결
            px_ = rnd.uniform(tx - rw + xo, tx + rw + xo)
            py_ = rnd.uniform(ty - rh * 1.6, ty + rh * 0.7)
            d.line((px_, py_, px_ + rnd.choice((-2, 2)), py_ + 2), fill=NEEDLE_DK)
    return fit_inside(outline(im))

def _bamboo(w, h, seed=11):
    """대나무 — 마디 있는 곧은 대 여러 대 + 성긴 잎.

    침엽수/활엽수와 실루엣이 완전히 갈라져 한 화면에 섞이면 즉시 동아시아로 읽힌다."""
    rnd = random.Random(seed)
    im = blank(w, h)
    d = ImageDraw.Draw(im)
    culms = [(int(w * 0.26), 0.92), (int(w * 0.50), 1.00), (int(w * 0.74), 0.86)]
    cw = max(3, w // 10)
    for cx, fh in culms:
        top = int(h * (1.0 - fh)) + 2
        d.rectangle((cx - cw // 2, top, cx + cw // 2, h - 2), fill=BAMBOO_MD)
        d.line((cx - cw // 2, top, cx - cw // 2, h - 2), fill=BAMBOO_LT)   # 좌측 미광
        d.line((cx + cw // 2, top, cx + cw // 2, h - 2), fill=BAMBOO_DK)
        node = top
        while node < h - 4:                                # 마디
            node += max(9, h // 9)
            d.line((cx - cw // 2 - 1, node, cx + cw // 2 + 1, node), fill=BAMBOO_DK)
            d.point((cx - cw // 2, node - 1), fill=BAMBOO_LT)
        # 잎 — 마디에서 비스듬히, 성기게
        for k in range(3):
            ly = top + int((h - top) * (0.10 + 0.22 * k))
            side = -1 if (k + cx) % 2 == 0 else 1
            for j in range(2):
                ex = cx + side * (cw + 4 + j * 5)
                ey = ly - 3 - j * 3
                d.line((cx, ly, ex, ey), fill=LEAF_DK, width=2)
                d.line((cx + side * 2, ly, ex, ey), fill=BAMBOO_DK)
    return fit_inside(outline(im))


def _azalea(w, h, flowering=True, seed=5):
    """진달래 — 봄 한국 산을 분홍으로 덮는 관목.  열매 덤불 자리를 대신한다."""
    rnd = random.Random(seed)
    im = blank(w, h)
    d = ImageDraw.Draw(im)
    cx, cy = w // 2, int(h * 0.60)
    # 잎 덩이 3~4
    for fx, fy, r in ((-0.20, 0.10, 0.30), (0.22, 0.06, 0.28),
                      (0.00, -0.14, 0.30), (0.02, 0.24, 0.26)):
        x, y, rr = cx + w * fx, cy + h * fy, w * r
        d.ellipse((x - rr, y - rr * 0.85, x + rr, y + rr * 0.85), fill=LEAF_DK)
        d.ellipse((x - rr * 0.78, y - rr * 0.92, x + rr * 0.55, y + rr * 0.30),
                  fill=shade(LEAF_DK, +0.20))
        d.ellipse((x - rr * 0.48, y - rr * 0.86, x + rr * 0.10, y - rr * 0.16),
                  fill=shade(LEAF_DK, +0.38))
    if flowering:
        for _ in range(max(6, w // 12)):                 # 진달래 꽃
            fx = rnd.uniform(cx - w * 0.36, cx + w * 0.36)
            fy = rnd.uniform(cy - h * 0.34, cy + h * 0.26)
            rr = max(2, w // 26)
            d.ellipse((fx - rr, fy - rr, fx + rr, fy + rr), fill=AZALEA)
            d.ellipse((fx - rr * 0.5, fy - rr, fx + rr * 0.3, fy - rr * 0.1), fill=AZALEA_LT)
            d.point((fx, fy + rr * 0.4), fill=AZALEA_DK)
    return fit_inside(outline(im))


def _sapling(w, h, seed=21):
    """소나무 묘목 — **작은 소나무**로 그린다.

    2026-08-01 운영자 이미지: 정체불명의 짙은 초록 덩어리.  실측하니
    `RegrowthScheduler.SpawnSapling` 이 **옛 나무 프리팹의 스프라이트를 빌려**
    작게 줄이고 초록으로 물들이고 있었다.  그 프리팹 스프라이트가 아트 v2 이전
    세대의 동그란 덩어리라, 축소되면 그냥 초록 얼룩이 된다.
    묘목은 '작은 나무' 로 읽혀야 심은 것임을 알 수 있다 — 가는 줄기 + 어린 솔가지."""
    im = blank(w, h)
    d = ImageDraw.Draw(im)
    cx, base = w // 2, h - 2
    top = int(h * 0.30)
    d.line((cx, base, cx + w * 0.06, top), fill=BARK_MD, width=max(2, w // 12))
    for f, r in ((0.42, 0.30), (0.62, 0.24), (0.82, 0.15)):
        y = base - (base - top) * f
        rw = w * r
        d.ellipse((cx - rw, y - rw * 0.55, cx + rw, y + rw * 0.45), fill=NEEDLE_MD)
        d.ellipse((cx - rw * 0.6, y - rw * 0.6, cx + rw * 0.2, y - rw * 0.05), fill=NEEDLE_LT)
    return fit_inside(outline(im))


# ── 출력 표: (경로, 크기, 그리기) ─────────────────────────────────────
def targets():
    S = os.path.join(ASSETS, "Sprites")
    R = os.path.join(ASSETS, "Resources", "flora32")
    return [
        # (경로, 픽셀 크기, 생성함수, **월드 폭[칸]**)
        #  월드 폭을 여기 적어야 '그림만 바꿨는데 크기가 변했다' 가 안 생긴다.
        #  2026-08-02 실측으로 잡은 두 건: ts_tree 가 2.0칸(다른 수종 1.5)이라
        #  소나무만 화면을 눌렀고, 진달래는 Sprites=PPU32 / Resources=PPU128 로
        #  갈라져 있었다.
        (os.path.join(S, "flora64_pine.png"), (54, 103), lambda: _pine(54, 103, 7), 0.72),
        (os.path.join(S, "ts_tree.png"), (192, 192), lambda: _pine(192, 192, 13), 1.23),
        (os.path.join(S, "flora64_spruce.png"), (42, 104), lambda: _bamboo(42, 104), 0.64),
        (os.path.join(R, "flora32_bush_berry.png"), (128, 128),
         lambda: _azalea(128, 128, True), 0.90),
        (os.path.join(R, "flora32_bush_picked.png"), (128, 128),
         lambda: _azalea(128, 128, False), 0.90),
        (os.path.join(S, "flora32_sapling.png"), (32, 48), lambda: _sapling(32, 48), 0.62),
    ]


def main() -> int:
    stage = "--stage" in sys.argv
    for path, size, fn, world in targets():
        img = fn()
        if img.size != size:
            img = img.resize(size, Image.NEAREST)
        name = os.path.splitext(os.path.basename(path))[0]
        if stage:
            out = os.path.join(r"G:/ai/_hanok_flora", name + ".png")
            os.makedirs(os.path.dirname(out), exist_ok=True)
            img.save(out)
            print(f"[ok] {name}.png ({img.width}x{img.height}) → _hanok_flora")
            continue
        # 목적지는 적지 않고 **찾는다** — 2026-08-02 검사에서 묘목이
        #  `Sprites/` 에만 갱신되고 `Resources/flora32/`(런타임이 읽는 곳)에는
        #  하루 전 그림이 남아 있는 것이 잡혔다.  targets() 의 경로는 '어디에
        #  하나는 있어야 한다' 는 뜻이지 '거기에만 있다' 는 뜻이 아니다.
        paths = save_everywhere(img, name, world, create_in=os.path.dirname(path))
        print(f"[ok] {name}.png ({img.width}x{img.height}) → {rel(paths)}")
    print(f"{'(검수용) ' if stage else ''}{len(targets())}종")
    return 0


if __name__ == "__main__":
    sys.exit(main())
