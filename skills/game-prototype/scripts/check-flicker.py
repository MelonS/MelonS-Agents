# -*- coding: utf-8 -*-
"""check-flicker.py — 연속 프레임에서 **떨림**을 수치로 잡는다.

계기 (2026-08-09): 조명에 불꽃 흔들림을 넣었는데 운영자가 영상을 보고
*"식사만드는 도구? 밤되면 왜케 덜덜 떨리는거 멀까"* 라고 지적했다.  나는 정지
프레임만 보고 "조명이 자연스러워졌다"고 판단했다 — **정지 프레임에는 떨림이
찍히지 않는다.**  움직임은 프레임 *사이*에 있기 때문이다.

원인은 반경을 흔든 것이었다.  라이트맵은 해상도가 낮은 텍스처라 반경이 몇 %만
변해도 가장자리 픽셀 한 줄이 통째로 켜졌다 꺼진다.  같은 진폭이라도 밝기를
흔들면 픽셀 경계를 건드리지 않아 부드럽다.

이 스크립트는 **카메라가 멈춘 구간**을 찾아 그 안에서 인접 프레임의 픽셀 변화를
잰다.  카메라가 움직이면 화면 전체가 바뀌므로 떨림과 구분되지 않는다 — 그래서
'조용한 구간'을 먼저 고른다.

usage:
  python check-flicker.py <프레임_디렉터리> [--top 8]
"""
from __future__ import annotations
import argparse
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

try:
    from PIL import Image, ImageChops, ImageStat
except ImportError:
    print("[flicker] Pillow 필요: pip install pillow", file=sys.stderr)
    raise SystemExit(1)

# 판정 방법 — **조명이 켜진 구간과 꺼진 구간을 비교**한다.
#
#  처음엔 "카메라가 멈춘 구간의 절대 변화량"으로 재려 했는데 전부 경고가 떴다.
#  게임 화면은 낮에도 늘 무언가 움직인다(주민·동물·작업 이펙트) — 절대값에는
#  그 정상 움직임이 섞여 있어서 떨림과 구분되지 않는다.
#
#  조명 떨림이라면 **밤이 낮보다 시끄러워야** 한다.  실측(2026-08-09, 수정 후):
#  낮 중앙 5.65 / 밤 중앙 0.41 — 밤이 낮의 1/14 로 조용하다.  반대로 밤이 낮보다
#  시끄러우면 조명·이펙트가 픽셀 경계를 건드리고 있다는 뜻이다.
RATIO_WARN = 1.0     # 밤/낮 중앙값 비가 이 값을 넘으면 의심


def diff(a: Path, b: Path) -> float:
    """두 프레임의 평균 밝기 차이 (0~255)."""
    ia = Image.open(a).convert("L")
    ib = Image.open(b).convert("L")
    return ImageStat.Stat(ImageChops.difference(ia, ib)).mean[0]


def series(fs, lo, hi, step=3):
    return sorted(diff(fs[i], fs[i + 1]) for i in range(lo, min(hi, len(fs) - 1), step))


def median(v):
    return v[len(v) // 2] if v else 0.0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("frames")
    ap.add_argument("--day", default="120:700", help="조명 꺼진 구간 lo:hi (프레임)")
    ap.add_argument("--night", default="1000:1380", help="조명 켜진 구간 lo:hi")
    args = ap.parse_args()

    fs = sorted(Path(args.frames).glob("f*.png"))
    if len(fs) < 100:
        print(f"[flicker] 프레임이 부족하다: {len(fs)}장", file=sys.stderr)
        return 1

    dlo, dhi = (int(x) for x in args.day.split(":"))
    nlo, nhi = (int(x) for x in args.night.split(":"))
    day = series(fs, dlo, dhi)
    night = series(fs, nlo, nhi)
    md, mn = median(day), median(night)

    print(f"[flicker] 프레임 {len(fs)}장")
    print(f"  낮 {dlo}-{dhi}   중앙 {md:.2f}  상위10% {day[int(len(day)*0.9)]:.2f}")
    print(f"  밤 {nlo}-{nhi}  중앙 {mn:.2f}  상위10% {night[int(len(night)*0.9)]:.2f}")

    ratio = mn / md if md > 0.01 else float("inf")
    print(f"  밤/낮 비 {ratio:.2f}  (경고 {RATIO_WARN} 초과)")
    if ratio > RATIO_WARN:
        print()
        print("[flicker] ✗ 밤이 낮보다 시끄럽다 — 조명·이펙트가 매 프레임 "
              "픽셀을 흔들고 있을 가능성이 크다.")
        print("  라이트맵처럼 해상도가 낮은 텍스처에서는 **반경**을 흔들면 "
              "가장자리 픽셀 한 줄이")
        print("  통째로 켜졌다 꺼진다.  **밝기**를 흔들 것.")
        return 1
    print()
    print("[flicker] ✓ 조명이 켜진 구간이 꺼진 구간보다 조용하다.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
