#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check-art-tone.py — 아트 톤 일관성 게이트.

운영자 지침 (2026-07-29):
  "아트의 톤유지가 중요하다.  특정 오브젝트의 퀄리티가 좋더라도 톤이 다르다면
   게임 전체의 완성도가 낮아 보인다."

그래서 톤 판정을 **눈이 아니라 수치**로 만든다.  사람 눈은 스프라이트를 하나씩
확대해 보면 "잘 나왔다"고 판정하는데, 정작 인게임에서 무너지는 건 개별 품질이
아니라 팩과의 **통계적 서명 불일치**다.  이 게이트는 그 서명을 잰다.

기준선 — Tiny Swords 팩 실측 (ts_tree/ts_sheep/ts_wood_pile/ts_gold_pile/ts_deco_01~08)

| 축 | 팩 범위 | 무엇을 잡나 |
|---|---|---|
| 외곽 휘도 (하위 5% L) | 0.009 ~ 0.012 | 어두운 웜 아웃라인.  12종이 사실상 동일 — 팩의 가장 강한 서명 |
| 색 수 (8단계 양자화) | 4 ~ 9 | 플랫 셀셰이딩.  생성물은 여기서 100배 튄다 |
| 평균 채도 | 0.33 ~ 0.60 | |
| 평균 상대휘도 | 0.10 ~ 0.41 | 밸류 대역 — 캐릭터 대역 침범 방지 |

임계는 팩 실측 범위에 여유를 준 값이다(아래 LIMITS).  팩 스프라이트 자신이
전부 PASS 하는지로 임계를 검증한다 — `--self-test`.

사용:
  python skills/game-prototype/scripts/check-art-tone.py               # 게임 반입 아트 전수
  python skills/game-prototype/scripts/check-art-tone.py PATH...       # 지정 파일만
  python skills/game-prototype/scripts/check-art-tone.py --self-test   # 팩 기준선 검증
exit 0 = 전부 PASS.
"""
from __future__ import annotations
import argparse
import colorsys
import glob
import os
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SPRITES = Path(__file__).resolve().parents[1] / "unity-project" / "Assets" / "Sprites"

# (하한, 상한).  None = 무제한.
LIMITS = {
    "outline_L": (None, 0.020),   # 팩 0.012 → 여유 1.7배
    "ncolors":   (None, 16),      # 팩 9 → 여유.  생성물은 400~1000 이라 확실히 갈린다
    "sat":       (None, 0.68),    # 하한 없음 — 아래 주석 참조
    "lum":       (0.04, 0.52),
}
# 채도 **하한을 없앤 이유** (2026-07-29 정정):
#  처음엔 팩 실측 범위(0.33~0.60)를 그대로 임계로 옮겨 하한 0.28 을 뒀는데, 그러자
#  돌·광맥·무덤이 전부 FAIL 했다.  바위가 회색인 것은 톤 위반이 아니다 — 오히려
#  채도를 올리면 그게 이상해진다.  기준선으로 쓴 팩 12종에 무채색 자산이 없어서
#  자가검증만으로는 이 오류가 안 드러났다.
#  게다가 우리는 콜로니스트 3번 슬롯으로 **리넨(S 0.163)** 을 의도적으로 채택했다.
#  저채도를 한쪽에선 채택하고 다른 쪽에선 위반으로 치는 건 앞뒤가 안 맞는다.
#  톤을 깨는 것은 **과채도**(혼자 튀는 것)이므로 상한만 본다.
#  명도 대역도 같은 이유로 소폭 넓혔다 (무덤 0.067 처럼 어두운 게 정상인 자산이 있다).

# 팩 기준선 검증용 — 이 파일들은 정의상 PASS 여야 한다.
PACK_REFS = ["ts_tree.png", "ts_sheep.png", "ts_wood_pile.png", "ts_gold_pile.png"] + \
            [f"ts_deco_{i:02d}.png" for i in range(1, 9)]

# 기본 검사 대상 — **화면에 실제로 나오는** 자체 제작/생성 아트.
#  (팩 원본 ts_* 는 정의상 기준이므로 대상이 아니라 레퍼런스다.)
DEFAULT_TARGETS = ["flora64_*.png"]

# 의도적 제외 — 왜 제외하는지 남긴다.  게이트가 상시 빨간색이면 아무도 안 보게
#  되고, 그 순간 게이트는 없느니만 못해진다.  제외는 근거와 함께 명시적으로.
#
#  flora32_tree_a~f : 구세대 32x48 롤리팝 나무 (외곽 휘도 0.033 로 FAIL).
#    현재 화면에 나오지 않는다 — TreeEntity.SetSpecies() 가 생성 시점에 항상
#    SpeciesSprites 로 덮어쓰므로, 프리팹 기본값인 flora32_tree_a 는 주입이
#    실패했을 때만 보이는 안전망이다.  마감 12일 전에 프리팹 에셋을 흔드는
#    (GUID/씬 참조 리스크) 대신 현 상태를 기록만 해 둔다.
#    되살릴 일이 생기면 gen-trees-lora.py 로 다시 뽑을 것 — 그게 정본 경로다.
LEGACY_UNUSED = ["flora32_tree_*.png"]


def profile(path: str | Path) -> dict | None:
    a = np.array(Image.open(path).convert("RGBA")).astype(float)
    m = a[:, :, 3] > 40
    if m.sum() < 50:
        return None
    rgb = a[:, :, :3][m] / 255.0
    hsv = np.array([colorsys.rgb_to_hsv(*q) for q in rgb])
    lin = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    L = (lin * [0.2126, 0.7152, 0.0722]).sum(1)
    q = (a[:, :, :3][m] // 8).astype(int)
    return {
        "outline_L": float(np.percentile(L, 5)),
        "ncolors": len(set(map(tuple, q))),
        "sat": float(hsv[:, 1].mean()),
        "lum": float(L.mean()),
        "px": int(m.sum()),
    }


def judge(p: dict) -> list[str]:
    bad = []
    for k, (lo, hi) in LIMITS.items():
        v = p[k]
        if lo is not None and v < lo:
            bad.append(f"{k}={v:.3f}<{lo}")
        if hi is not None and v > hi:
            bad.append(f"{k}={v:.3f}>{hi}" if k != "ncolors" else f"색수 {v}>{hi}")
    return bad


def run(paths: list[str], label: str) -> int:
    print(f"\n== {label} ==")
    print(f"{'파일':30s} {'외곽L':>7s} {'색수':>6s} {'채도':>6s} {'휘도':>6s}  판정")
    print("-" * 74)
    fails = 0
    for p in paths:
        pr = profile(p)
        if pr is None:
            continue
        bad = judge(pr)
        if bad:
            fails += 1
        verdict = "PASS" if not bad else "FAIL " + " ".join(bad)
        print(f"{os.path.basename(p):30s} {pr['outline_L']:7.3f} {pr['ncolors']:6d} "
              f"{pr['sat']*100:6.1f} {pr['lum']:6.3f}  {verdict}")
    return fails


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("paths", nargs="*")
    ap.add_argument("--self-test", action="store_true",
                    help="팩 스프라이트로 임계 검증 (전부 PASS 여야 임계가 옳다)")
    a = ap.parse_args()

    if a.self_test:
        refs = [str(SPRITES / n) for n in PACK_REFS if (SPRITES / n).exists()]
        if not refs:
            print("팩 스프라이트 없음 — extract-tinyswords.py 를 먼저 실행할 것 "
                  "(ts_* 는 gitignore 대상이라 클린 클론엔 없다).")
            return 0
        f = run(refs, "기준선 자가검증 (팩 원본 — 전부 PASS 여야 함)")
        print(f"\n{'PASS — 임계가 팩을 통과시킨다.' if not f else f'FAIL {f}건 — 임계가 너무 빡빡하다.'}")
        return 1 if f else 0

    targets = a.paths
    if not targets:
        for pat in DEFAULT_TARGETS:
            targets += sorted(glob.glob(str(SPRITES / pat)))
    if not targets:
        print("검사 대상 없음.")
        return 0

    fails = run(targets, "톤 게이트")
    print()
    if fails:
        print(f"FAIL — {fails}건이 팩 톤을 벗어남.")
        print("  색수 초과 = 생성물 미양자화 (gen-trees-lora.py 의 quantize() 참조)")
        print("  외곽L 초과 = 축소로 아웃라인이 뭉개짐 — 양자화로 회복된다")
        return 1
    print(f"PASS — {len(targets)}건 전부 팩 톤 범위.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
