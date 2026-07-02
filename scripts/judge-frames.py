#!/usr/bin/env python3
"""judge-frames.py — extract evenly-spaced frames from a clip for cut judging.

The cut-judge agent (.claude/agents/cut-judge.md) scores generated cuts by
actually LOOKING at frames — never from priors.  This helper turns an mp4
into a small set of jpgs it can Read.

Usage:
  python scripts/judge-frames.py --video clips/01.mp4 --out-dir judge/01
  python scripts/judge-frames.py --video clips/01.mp4 --out-dir judge/01 --count 5
  python scripts/judge-frames.py --video clips/01.mp4 --out-dir judge/01 --times 0.2,2.0,3.8

Prints one extracted-frame path per line (stdout), ready to paste into a
judging prompt.
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

FFMPEG = os.environ.get("FFMPEG_BIN", "ffmpeg")
FFPROBE = os.environ.get("FFPROBE_BIN", "ffprobe")


def duration_of(video: Path) -> float:
    out = subprocess.run(
        [FFPROBE, "-v", "error", "-show_entries", "format=duration",
         "-of", "json", str(video)],
        capture_output=True, text=True, check=True,
    )
    return float(json.loads(out.stdout)["format"]["duration"])


def extract(video: Path, t: float, dest: Path, width: int) -> bool:
    r = subprocess.run(
        [FFMPEG, "-loglevel", "error", "-y", "-ss", f"{t:.3f}", "-i", str(video),
         "-frames:v", "1", "-vf", f"scale={width}:-1", str(dest)],
        capture_output=True, text=True,
    )
    return r.returncode == 0 and dest.exists()


def main():
    p = argparse.ArgumentParser(description="Extract frames from a clip for judging")
    p.add_argument("--video", required=True, type=Path)
    p.add_argument("--out-dir", required=True, type=Path)
    p.add_argument("--count", type=int, default=3,
                   help="evenly spaced frames incl. near-start/near-end (default 3)")
    p.add_argument("--times", default="",
                   help="comma-separated explicit timestamps (overrides --count)")
    p.add_argument("--width", type=int, default=480, help="jpg width (default 480)")
    args = p.parse_args()

    if not args.video.exists():
        print(f"video not found: {args.video}", file=sys.stderr)
        sys.exit(1)
    args.out_dir.mkdir(parents=True, exist_ok=True)

    if args.times:
        times = [float(t) for t in args.times.split(",")]
    else:
        dur = duration_of(args.video)
        # near-start .. near-end, avoiding first/last 5% (fade/garbage frames)
        lo = dur * 0.05
        hi = max(lo, min(dur * 0.95, dur - 0.15))  # clamp: avoid past-end on very short clips
        n = max(2, args.count)
        times = [lo + (hi - lo) * i / (n - 1) for i in range(n)]

    ok = 0
    for i, t in enumerate(times, 1):
        dest = args.out_dir / f"{args.video.stem}_f{i}_{t:.1f}s.jpg"
        if extract(args.video, t, dest, args.width):
            print(dest)
            ok += 1
        else:
            print(f"WARN: extract failed at {t:.1f}s", file=sys.stderr)

    sys.exit(0 if ok else 2)


if __name__ == "__main__":
    main()
