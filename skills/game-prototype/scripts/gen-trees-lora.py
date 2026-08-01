#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""gen-trees-lora.py — 수종 스프라이트를 TS 스타일 LoRA 로 정합 생성.

확정 아트 노선(RESUME-2026-07-24 "뒤집지 말 것" §1) = B2 하이브리드:
  TS 팩(지형·나무·자원·유닛) + 가구/구조물=절차 드로잉 + **유기물=TS 스타일 LoRA**
나무는 유기물이므로 이 스크립트가 정본 경로다.

배경
----
`SceneSetup.Game.Trees` 는 5종 슬롯에 ts_tree 를 다섯 번 넣고 있었다(아트 B2).
게임 로직은 5종 분포를 굴리는데 화면엔 전부 같은 나무가 나와 45그루 맵이 단일
수종 모노컬처로 읽혔다.  대안으로 기존 flora64_*(FLUX 소지, LoRA 이전 세대)를
배선해 봤으나 팩과 톤이 어긋났다 — 특히 자작이 L 0.476 로 혼자 튀어
콜로니스트 밸류 대역(리넨 L 0.600)을 침범했다.  그래서 **같은 LoRA 로 다시 뽑아
톤을 팩에 맞춘다.**

Pine 은 팩 원본 ts_tree 를 그대로 유지한다 — 톤의 기준점이자 PPU96(2×2칸)
상층 캐노피로 숲의 높낮이를 만든다.  나머지 4종만 생성한다.

⚠ 2026-08-01 한국풍 전환으로 **이 전제가 깨졌다.**  운영자 "동,식물 전부 다
한국풍으로" 지시에 따라 Pine/Spruce 슬롯을 `_gen_hanok_flora.py` 의 절차
소나무(적송)·대나무로 덮었다.  즉 ts_tree 는 더 이상 팩 톤의 기준점이 아니며,
이 스크립트를 다시 돌려 Pine 을 재생성하면 한국풍 전환이 되돌아간다.
톤 기준점은 이제 잔디·바위 등 지형 팩과 `palette.py` 램프다.
Maple/Oak/Birch 는 그대로 LoRA 산출물을 쓴다 — 단풍·상수리·은행으로 읽히므로
한국 수종과 충돌하지 않는다.

사용
----
  python skills/game-prototype/scripts/gen-trees-lora.py            # 4종×3시드
  python skills/game-prototype/scripts/gen-trees-lora.py --species maple --seeds 5
  python skills/game-prototype/scripts/gen-trees-lora.py --contact-sheet-only

산출:  <OUT>/cand/<species>_s<seed>.png  (키잉·크롭 완료, RGBA)
       <OUT>/contact_<species>.png       (시드 비교용 컨택트시트)
채택본을 Assets/Sprites/flora64_<species>.png 로 복사하는 것은 **수동 픽** 단계다
(design-qa 5단계: 시안 → TA 채점 → 운영자 픽 → 인게임 재채점 → 커밋).

함정 메모
--------
- FLUX 는 알파를 못 낸다.  평면 청보라 배경에 그린 뒤 **모서리 시드 flood fill**
  로 키잉한다.  단순 색거리 임계로 하면 단풍(빨강)이 배경과 hue 가 가까워 같이
  파인다 — 반드시 연결성 기반이어야 한다 (BG_PROMPT 주석의 실측 참조).
- 축소만 하면 팩 톤을 벗어난다.  색 수 405~1022(팩 4~9) · 외곽 휘도 0.065(팩
  0.012).  quantize() + ensure_outline() + pull_saturation() 3단이 그 복구다.
  검증은 `check-art-tone.py` — 이 게이트를 통과해야 반입한다.
- 접지 그림자는 배경과 연결돼 있어 같은 fill 로 함께 제거된다(게임에서는
  BlobShadow 가 런타임으로 붙으므로 스프라이트에 구워 넣으면 이중이 된다).
- `TS 타일 flip 금지` 규약은 지형 타일 대상 — 나무 스프라이트에는 해당 없음.
"""
from __future__ import annotations
import argparse
import os
import subprocess
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

REPO = Path(__file__).resolve().parents[3]
FLUX = REPO / "scripts" / "flux-still.py"
DEFAULT_OUT = Path(os.environ.get("PAWNSIM_ART_OUT")
                   or (REPO / "skills" / "game-prototype" / "art-out" / "trees"))

LORA = "tswords_v1.safetensors"
TRIGGER = "tswords style game sprite"
# 배경색은 **청보라**다.  1차 시도의 마젠타(H340)는 단풍 빨강(H10)과 30° 밖에
#  안 떨어져, 잔상 제거용 색거리 임계를 조금만 올려도 단풍 잎이 같이 파였다
#  (실측: tol60 에서 단풍 8.6%, tol80 에서 27.6% 손실).  청보라(H262)는 이 게임의
#  나무 색 전체 — 초록 H60~145 · 갈색 H30 · 노랑 H50 · 빨강 H10 · 청록 H180 —
#  중 가장 가까운 청록과도 82° 떨어져 안전 여유가 크다.
#  프롬프트에 flat 을 명시해야 FLUX 가 그라데이션을 안 넣는다.
BG_PROMPT = ("centered on plain flat solid violet purple background, "
             "no shadow, no gradient, isolated object")

# 종별 프롬프트 — 실루엣과 색이 서로 확실히 갈리도록 (실루엣이 종 정체성).
#  Pine 은 팩 원본 ts_tree 유지 → 생성 대상 아님.
SPECIES = {
    # Pine 은 인게임에선 팩 원본 ts_tree 를 쓰지만, **공개 레포 클린 클론에는
    #  팩이 없다**(ts_* gitignore).  그때 SceneSetup 이 flora64_pine 으로 폴백하므로
    #  이 슬롯도 같은 LoRA 로 톤을 맞춰 둬야 폴백 화면이 안 깨진다.
    # "dark green" 을 쓰면 3시드 모두 휘도 0.051~0.058 로 팩 최저(0.096)보다
    #  어둡게 나온다(톤 게이트 FAIL).  눈으로는 "괜찮아 보였는데" 수치가 잡았다.
    "pine":  "a tall pine conifer tree with layered sunlit green needle tiers and "
             "bare lower trunk, narrow triangular silhouette",
    # 1차(s1001)는 노란 잔가지로 읽혔다 — 인게임에서 실루엣이 성겨 나무로 안 보였다.
    #  "slender/narrow" 가 원인.  잎을 뭉치게 하고 캐노피를 채우는 쪽으로 바꾼다.
    "birch": "a birch tree with white bark trunk and a full dense rounded canopy of "
             "golden yellow autumn leaves, thick clustered foliage, solid silhouette",
    "oak":   "a sturdy oak tree with broad rounded green canopy and thick trunk "
             "with root flare, wide silhouette",
    "maple":  "a maple tree with bright crimson red autumn foliage, medium rounded "
              "canopy, slender dark trunk",
    "spruce": "a tall narrow spruce conifer tree with layered blue-green needle "
              "tiers, pointed triangular silhouette",
}
# 목표 높이 px — 기존 flora64 대역(96~104)과 맞춤.  PPU64 → 1.5~1.6칸 높이.
TARGET_H = {"pine": 104, "birch": 100, "oak": 96, "maple": 96, "spruce": 104}


def key_background(im: Image.Image, tol: int = 70) -> Image.Image:
    """모서리 시드 flood fill 로 배경 제거.

    단순 임계가 아니라 **연결성**으로 판정한다 — 단풍의 붉은 잎처럼 배경과 색이
    가까운 내부 픽셀을 파먹지 않게 하는 유일한 방법.
    """
    im = im.convert("RGBA")
    w, h = im.size
    px = im.load()
    seeds = [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]
    ref = [px[s][:3] for s in seeds]

    def near(c) -> bool:
        return any(abs(c[0] - r[0]) + abs(c[1] - r[1]) + abs(c[2] - r[2]) <= tol
                   for r in ref)

    seen = bytearray(w * h)
    q = deque()
    for sx, sy in seeds:
        if not seen[sy * w + sx] and near(px[sx, sy][:3]):
            seen[sy * w + sx] = 1
            q.append((sx, sy))
    while q:
        x, y = q.popleft()
        px[x, y] = (0, 0, 0, 0)
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and not seen[ny * w + nx]:
                if near(px[nx, ny][:3]):
                    seen[ny * w + nx] = 1
                    q.append((nx, ny))
    return im


def quantize(im: Image.Image, ncolors: int) -> Image.Image:
    """불투명 픽셀만 median-cut 으로 ncolors 색에 스냅.  알파는 보존.

    **톤 게이트의 핵심 단계다.**  팩 스프라이트는 8단계 양자화 기준 색이 4~9종인
    플랫 셀셰이딩인데, 512px 생성물을 ~96px 로 LANCZOS 축소하면 중간색이 수백 종
    생기고(실측 405~1022) 1px 아웃라인이 평균에 먹혀 흐려진다(외곽 휘도 0.012 →
    0.065).  개별 나무는 잘 나와 보여도 **팩과 다른 종류의 이미지**가 되어,
    운영자 지적대로 "특정 오브젝트 퀄리티가 좋아도 톤이 다르면 전체 완성도가
    낮아 보이는" 상태가 된다.  양자화가 흐려진 아웃라인을 가장 어두운 팔레트
    엔트리로 되돌려 크리스프함까지 함께 회복시킨다.
    """
    a = im.split()[-1]
    rgb = im.convert("RGB")
    # 투명부가 팔레트 한 칸을 먹지 않도록 불투명 영역 색으로만 팔레트를 만든다.
    bbox = a.getbbox()
    src = rgb.crop(bbox) if bbox else rgb
    pal = src.quantize(colors=ncolors, method=Image.MEDIANCUT, dither=Image.NONE)
    out = rgb.quantize(palette=pal, dither=Image.NONE).convert("RGB")
    out.putalpha(a)
    return out


# 팩 아웃라인 실측값 — ts_tree / ts_deco_07 의 최빈 최암부가 정확히 이 색이고
#  ts_sheep 도 (22,23,38) 로 사실상 같다.  어두운 쿨 네이비 1px 테두리가 Tiny
#  Swords 의 가장 식별력 높은 톤 서명이다(팩 12종의 외곽 휘도가 0.009~0.012 로
#  거의 동일).  LoRA 는 이걸 낼 때도 있고 안 낼 때도 있어(자작은 흰 줄기라 늘
#  실패) 결정론적으로 보장한다.
PACK_OUTLINE = (22, 28, 46, 255)
# 팩 평균 채도 0.472, 범위 0.326~0.597.  생성물이 이보다 뜨거우면 끌어내린다.
SAT_CEIL = 0.60


def pull_saturation(im: Image.Image, ceil: float = SAT_CEIL) -> Image.Image:
    """평균 채도가 팩 상한을 넘으면 전체를 비례 축소.  색상각은 불변.

    가문비(청록)·단풍(빨강)이 생성 단계에서 팩보다 뜨겁게 나온다(실측 0.72~0.78
    vs 팩 0.33~0.60).  개별로는 예뻐 보이지만 화면에 섞이면 그 오브젝트만
    도드라져 톤이 깨진다 — 운영자 지적의 정확한 사례.
    """
    a = np.array(im.convert("RGBA")).astype(float)
    m = a[:, :, 3] > 40
    if m.sum() == 0:
        return im
    rgb = a[:, :, :3] / 255.0
    mx = rgb.max(-1, keepdims=True)
    mn = rgb.min(-1, keepdims=True)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    cur = float(sat[m].mean())
    if cur <= ceil:
        return im
    # HSV 의 S 만 k 배와 동치: rgb' = mx - (mx - rgb) * k  (V=mx 불변, H 불변)
    k = ceil / cur
    a[:, :, :3] = np.clip(mx - (mx - rgb) * k, 0, 1) * 255.0
    return Image.fromarray(a.astype("uint8"))


def ensure_outline(im: Image.Image, color=PACK_OUTLINE) -> Image.Image:
    """실루엣 바깥 경계 1px 을 팩 아웃라인 색으로 확정.

    바깥으로 넓히지 않고 **가장자리 픽셀을 덮어쓴다** — 크기/앵커가 안 변해야
    PPU 계산과 셀 점유가 그대로 유지된다.
    """
    a = np.array(im.convert("RGBA"))
    op = a[:, :, 3] > 0
    if not op.any():
        return im
    pad = np.pad(op, 1, constant_values=False)
    # 상하좌우 중 하나라도 투명이면 경계 픽셀
    edge = op & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    a[edge] = color
    return Image.fromarray(a)


def fit(im: Image.Image, target_h: int, ncolors: int = 10) -> Image.Image:
    """타이트 크롭 → 목표 높이 축소 → 알파 이진화 → 팔레트 양자화.

    NEAREST 축소는 격자를 깨므로 LANCZOS 로 줄이고, 그 대가로 생긴 중간색을
    quantize() 가 되돌린다.  순서가 중요하다 — 양자화를 먼저 하면 축소가 다시
    중간색을 만든다.
    """
    bbox = im.getbbox()
    if bbox is None:
        return im
    im = im.crop(bbox)
    scale = target_h / im.height
    im = im.resize((max(1, round(im.width * scale)), target_h), Image.LANCZOS)
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            px[x, y] = (r, g, b, 0 if a < 110 else 255)
    bbox = im.getbbox()
    im = im.crop(bbox) if bbox else im
    # 순서 고정: 채도 정렬 → 양자화 → 아웃라인.
    #  아웃라인을 양자화보다 먼저 넣으면 median-cut 이 팩 아웃라인 색을 이웃
    #  어두운 색과 합쳐 버려 서명이 흐려진다.  마지막에 덮어써야 정확히 남는다.
    im = pull_saturation(im)
    im = quantize(im, ncolors)
    return ensure_outline(im)


def generate(species: str, seed: int, out: Path, size: int = 512) -> Path | None:
    raw = out / "raw" / f"{species}_s{seed}.png"
    raw.parent.mkdir(parents=True, exist_ok=True)
    prompt = f"{TRIGGER}, {SPECIES[species]}, top-down three-quarter view, {BG_PROMPT}"
    cmd = [sys.executable, str(FLUX), "--lora", LORA,
           "--width", str(size), "--height", str(size),
           "--seed", str(seed), "--prompt", prompt, "--output", str(raw)]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0 or not raw.exists():
        print(f"  ✗ {species} s{seed}: {r.stderr[-200:]}")
        return None
    keyed = fit(key_background(Image.open(raw)), TARGET_H[species])
    dst = out / "cand" / f"{species}_s{seed}.png"
    dst.parent.mkdir(parents=True, exist_ok=True)
    keyed.save(dst)
    print(f"  ✔ {species} s{seed} -> {dst.name}  {keyed.size}")
    return dst


def contact_sheet(species: str, out: Path) -> None:
    """시드 비교용 — 새 잔디 타일 위에 올려 실제 대비로 본다."""
    cands = sorted((out / "cand").glob(f"{species}_s*.png"))
    if not cands:
        return
    grass = (REPO / "skills" / "game-prototype" / "unity-project" / "Assets"
             / "Sprites" / "ts_tile_grass.png")
    cell_w = max(Image.open(c).width for c in cands) + 24
    cell_h = max(Image.open(c).height for c in cands) + 24
    sheet = Image.new("RGBA", (cell_w * len(cands), cell_h))
    if grass.exists():
        g = Image.open(grass).convert("RGBA").resize((64, 64), Image.NEAREST)
        for y in range(0, cell_h, 64):
            for x in range(0, sheet.width, 64):
                sheet.paste(g, (x, y))
    for i, c in enumerate(cands):
        im = Image.open(c).convert("RGBA")
        sheet.alpha_composite(im, (i * cell_w + (cell_w - im.width) // 2,
                                   cell_h - im.height - 12))
    p = out / f"contact_{species}.png"
    sheet.convert("RGB").save(p)
    print(f"  → {p.name}  ({len(cands)}시드)")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--species", default=None, choices=sorted(SPECIES),
                    help="생략 시 4종 전부")
    ap.add_argument("--seeds", type=int, default=3)
    ap.add_argument("--seed-base", type=int, default=1000)
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT)
    ap.add_argument("--contact-sheet-only", action="store_true",
                    help="생성 없이 기존 cand/ 로 컨택트시트만 재작성")
    a = ap.parse_args()

    targets = [a.species] if a.species else sorted(SPECIES)
    a.out.mkdir(parents=True, exist_ok=True)
    print(f"[trees] LoRA={LORA}  종={targets}  시드={a.seeds}  out={a.out}")

    for sp in targets:
        if not a.contact_sheet_only:
            for i in range(a.seeds):
                generate(sp, a.seed_base + i, a.out)
        contact_sheet(sp, a.out)

    print("\n다음: 컨택트시트로 시드 픽 → Assets/Sprites/flora64_<종>.png 로 복사 →")
    print("      SceneSetup 씬 재생성 + 빌드 → 인게임 재채점 (design-qa 5단계)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
