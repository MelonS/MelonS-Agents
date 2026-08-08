# -*- coding: utf-8 -*-
"""trim-trailer.py — 소개 영상 녹화본에서 **본편만** 잘라낸다.

왜 잘라야 하는가: `TrailerDirector` 는 촬영 전에 게임을 20배속으로 하루 돌린다
(워밍업).  그렇게 하지 않으면 자원 0·벽 없음·주민 전원 취침인 **빈 터**에서
영상이 시작한다 — 실제로 첫 촬영이 그랬다.  워밍업 구간은 화면을 검게 덮어
두므로, 남는 일은 그 검은 구간을 잘라내는 것뿐이다.

어디서 자르는지를 **영상 스스로 알려준다**: 녹화 시작 시각과 게임 시작 시각의
오프셋은 실행마다 다르고 밖에서 알 방법이 없다.  초 단위로 손대면 매번 한두
프레임씩 어긋난다.  그래서 `ffmpeg blackdetect` 로 검은 구간의 끝을 찾는다.

요강: 제출 영상은 **30~60초**.  잘라낸 길이가 이 범위를 벗어나면 실패로 본다 —
조용히 61초짜리를 내보내는 것이 이 스크립트가 막아야 할 유일한 사고다.

usage:
  python trim-trailer.py <녹화본.mp4> [-o 출력.mp4]
"""
from __future__ import annotations
import argparse
import os
import re
import subprocess
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

FFMPEG = os.environ.get("FFMPEG_BIN", "ffmpeg")
MIN_SEC, MAX_SEC = 30.0, 60.0


def run(cmd: list[str]) -> str:
    p = subprocess.run(cmd, capture_output=True, text=True,
                       encoding="utf-8", errors="replace")
    return (p.stdout or "") + (p.stderr or "")


def duration(path: Path) -> float:
    out = run([FFMPEG, "-i", str(path)])
    m = re.search(r"Duration:\s*(\d+):(\d+):(\d+\.\d+)", out)
    if not m:
        return -1.0
    h, mi, s = int(m.group(1)), int(m.group(2)), float(m.group(3))
    return h * 3600 + mi * 60 + s


def black_span(path: Path):
    """본편의 (시작, 끝) 시각.  끝이 -1 이면 영상 끝까지.

    임계는 실측으로 잡았다 — `pix_th` 를 0.05 로 조이면 인코딩 노이즈 때문에
    순수 검정으로 인정되지 않아 구간 자체를 못 찾는다.  중간의 밤 장면은 자원
    패널·시계 UI가 항상 켜져 있어 `pic_th=0.90` 을 넘지 못하므로 오검출되지 않는다."""
    out = run([FFMPEG, "-i", str(path),
               "-vf", "blackdetect=d=0.5:pic_th=0.90:pix_th=0.10",
               "-an", "-f", "null", "-"])
    # **영상 맨 처음부터 시작하는** 검은 구간만 본다.  이것이 워밍업 암전의 정확한
    #  정의다.  처음엔 "앞쪽 N초 안에 끝나는 구간"으로 잡았는데, 워밍업 길이는
    #  환경에 따라 48~190초로 달라져서 N을 40 → 95 로 올려도 또 넘겼다.
    #  시작 지점으로 판별하면 길이가 얼마든 상관이 없다.
    spans = [(float(m.group(1)), float(m.group(2)))
             for m in re.finditer(r"black_start:([\d.]+)\s+black_end:([\d.]+)", out)]
    if not spans or spans[0][0] >= 0.5:
        return -1.0, -1.0
    start = spans[0][1]
    # 끝점 = **두 번째** 암전의 시작(연출 마무리 페이드아웃).  없으면 영상 끝까지.
    end = next((a for a, _ in spans[1:] if a > start + 5.0), -1.0)
    return start, end


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("-o", "--out")
    args = ap.parse_args()

    src = Path(args.src)
    if not src.exists():
        print(f"[trim] 없음: {src}", file=sys.stderr)
        return 1

    total = duration(src)
    start, end = black_span(src)
    if start < 0:
        print("[trim] 암전 구간을 찾지 못했다 — TrailerDirector 없이 녹화했거나 "
              "페이드가 어긋났다.  자르지 않는다.", file=sys.stderr)
        return 2

    stop = end if end > 0 else total
    length = stop - start
    print(f"[trim] 원본 {total:.1f}s · 본편 {start:.2f}~{stop:.2f}s ({length:.1f}s)")

    if not (MIN_SEC <= length <= MAX_SEC):
        print(f"[trim] ✗ 본편 {length:.1f}s 가 요강 범위({MIN_SEC:.0f}~{MAX_SEC:.0f}s) 밖이다.",
              file=sys.stderr)
        return 3

    out = Path(args.out) if args.out else src.with_name(src.stem + "_trimmed.mp4")
    # 재인코딩한다 — `-c copy` 는 키프레임 단위로만 잘려서 앞에 검은 프레임이
    #  몇 초씩 남는다(잘랐다고 착각하기 딱 좋다).
    rc = subprocess.run(
        [FFMPEG, "-y", "-ss", f"{start:.3f}", "-t", f"{length:.3f}", "-i", str(src),
         "-c:v", "libx264", "-preset", "slow", "-crf", "18",
         "-pix_fmt", "yuv420p", "-movflags", "+faststart",
         "-an", str(out)],
        capture_output=True, text=True, encoding="utf-8", errors="replace")
    if rc.returncode != 0:
        print(rc.stderr[-2000:], file=sys.stderr)
        return rc.returncode

    got = duration(out)
    size_mb = out.stat().st_size / (1024 * 1024)
    print(f"[trim] ✓ {out}  {got:.1f}s · {size_mb:.1f}MB")
    if not (MIN_SEC <= got <= MAX_SEC):
        print(f"[trim] ✗ 결과가 {got:.1f}s — 범위 밖", file=sys.stderr)
        return 3
    return 0


if __name__ == "__main__":
    sys.exit(main())
