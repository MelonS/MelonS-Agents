#!/usr/bin/env python3
"""mix3-rhythm.py — add rhythmic pulse to a hero-loop mp4.

Operator feedback (2026-05-26): hero-loop static visuals need *rhythmic
feel*.  LTX-Video 2B can't precisely control motion strength via prompt,
so we add the rhythm in the ffmpeg compose stage:

- Brightness pulse:   subtle sinewave at target BPM
- Scale pulse:        very small zoom in/out at target BPM (parallax feel)
- Vignette pulse:     edge darkening modulation

Default 80 BPM matches lofi/chillhop typical tempo.  Operator can
override via --bpm.

This is a "loose rhythm" — not beat-detected from audio.  Operator stated
'같은 박자가 아니어도 됨' (doesn't have to match exact beat).  Real
audio-detected beat sync would require aubio + per-beat enable= filter
chain (more complex, future enhancement).

Usage:
  python scripts/mix3-rhythm.py \\
      --hero outputs/publish/mix-3-hero-candidates/hero-03-snow-falling-window.mp4 \\
      --audio G:/ai/mix1-analysis/mix1-full.mp3 \\
      --output outputs/publish/mix-3-rhythm/yt-mix-3-snow-rhythm.mp4 \\
      --bpm 80
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
from pathlib import Path

FFMPEG = os.environ.get("FFMPEG_BIN", "ffmpeg")


def ffprobe_duration(path: Path) -> float:
    ffmpeg_p = Path(FFMPEG)
    ext = ffmpeg_p.suffix
    ffprobe = str(ffmpeg_p.parent / f"ffprobe{ext}") if ("/" in FFMPEG or "\\" in FFMPEG) else "ffprobe"
    out = subprocess.run(
        [ffprobe, "-v", "error", "-show_entries", "format=duration",
         "-of", "default=nw=1:nk=1", str(path)],
        capture_output=True, text=True,
    )
    return float(out.stdout.strip())


def build(hero: Path, audio: Path, output: Path, bpm: float,
          brightness_amp: float, scale_amp: float,
          target_width: int, target_height: int) -> int:
    duration = ffprobe_duration(audio)
    hz = bpm / 60.0  # BPM to Hz (cycles per second)
    print(f"[rhythm] {hero.name} + {audio.name} -> {output.name}")
    print(f"[rhythm] bpm={bpm} hz={hz:.3f} brightness_amp={brightness_amp} scale_amp={scale_amp}")
    print(f"[rhythm] audio duration {duration:.1f}s ({duration/60:.1f}min)")

    output.parent.mkdir(parents=True, exist_ok=True)

    # Filter graph:
    #   1. Loop hero indefinitely (stream_loop input flag).
    #   2. Scale to target with subtle pulse: zoom = 1.0 + scale_amp*(0.5*(1+sin(2PI*hz*t)))
    #      → scale oscillates between 1.0 and 1.0+scale_amp.
    #   3. Apply lanczos for smooth scaling.
    #   4. Crop back to target after zoom so it fits exactly.
    #   5. eq brightness pulse: brightness = brightness_amp * sin(2PI*hz*t)
    #      → subtle exposure breathing.
    #   6. format yuv420p for compat.
    #
    # Notes:
    # - We zoompan with continuous t expression (smooth, not key-framed).
    # - Apply scale pulse via "scale=w='iw*(1+S*0.5*(1+sin(2*PI*H*t)))':h=-1"
    #   then crop.  But ffmpeg scale doesn't allow per-frame dynamic
    #   expressions for w/h.  Use zoompan instead.
    #
    # zoompan z expression: zoom factor (1.0 to ~10).  d=1 for per-frame
    # eval.  s=target output size.  Then we just pulse z.
    #
    # zoompan applies AFTER scale.  Pre-scale to slightly larger than
    # target so zoom can pulse without exceeding bounds.

    pre_scale_w = int(target_width * (1 + scale_amp + 0.02))
    pre_scale_h = int(target_height * (1 + scale_amp + 0.02))

    zoom_expr = f"1.0+{scale_amp}*0.5*(1+sin(2*PI*{hz:.6f}*on/{30:.0f}))"
    # on = output frame number; we use frame-rate-relative time
    # Actually for absolute time use 't'.  Let me use t directly.
    # zoompan z expression supports 't' as time in seconds.
    # But zoompan also has 'in' = input frame number, 'on' = output frame number.
    # Time-based: t = on / fps.  Let me just use 't' which IS supported.
    # zoompan docs: z, x, y, d (duration in frames), fps, s (out size).
    # Variables: in, on, x, y, ix, iy, iw, ih, ow, oh, t (time at end), n (output frame)
    # NOTE: zoompan's 't' might be evaluated once per zoom-cycle, not per frame.
    # Safer: drive zoom by 'on' / fps.
    fps = 24
    zoom_expr = f"1.0+{scale_amp}*0.5*(1+sin(2*PI*{hz:.6f}*on/{fps}))"
    bright_expr = f"{brightness_amp}*sin(2*PI*{hz:.6f}*t)"

    vfilter = (
        f"scale={pre_scale_w}:{pre_scale_h}:flags=lanczos,"
        f"zoompan=z='{zoom_expr}':d=1:fps={fps}:s={target_width}x{target_height},"
        f"eq=brightness='{bright_expr}',"
        f"format=yuv420p"
    )

    cmd = [
        FFMPEG, "-y", "-hide_banner", "-loglevel", "warning",
        "-stream_loop", "-1", "-i", str(hero),
        "-i", str(audio),
        "-filter_complex", f"[0:v]{vfilter}[v]",
        "-map", "[v]", "-map", "1:a",
        "-c:v", "h264_nvenc", "-preset", "p4", "-rc", "vbr", "-cq", "21",
        "-b:v", "6M", "-maxrate", "12M", "-bufsize", "12M",
        "-c:a", "aac", "-b:a", "192k",
        "-shortest", "-t", f"{duration:.3f}",
        str(output),
    ]
    rc = subprocess.run(cmd).returncode
    if rc == 0:
        sz = output.stat().st_size / 1024 / 1024
        print(f"[rhythm] DONE: {output} ({sz:.1f} MB)")
    return rc


def main():
    p = argparse.ArgumentParser(description="Mix #3 rhythm overlay (BPM-pulse on hero-loop)")
    p.add_argument("--hero", required=True, type=Path)
    p.add_argument("--audio", required=True, type=Path)
    p.add_argument("--output", required=True, type=Path)
    p.add_argument("--bpm", type=float, default=80.0, help="rhythm BPM (lofi typical 70-90)")
    p.add_argument("--brightness-amp", type=float, default=0.06,
                   help="brightness pulse amplitude (-1 to 1 range, 0.06 = subtle)")
    p.add_argument("--scale-amp", type=float, default=0.03,
                   help="scale pulse amplitude (0.03 = 3% zoom oscillation)")
    p.add_argument("--width", type=int, default=1920)
    p.add_argument("--height", type=int, default=1080)
    args = p.parse_args()

    if not args.hero.exists():
        print(f"hero not found: {args.hero}", file=sys.stderr); sys.exit(1)
    if not args.audio.exists():
        print(f"audio not found: {args.audio}", file=sys.stderr); sys.exit(1)

    sys.exit(build(args.hero, args.audio, args.output, args.bpm,
                   args.brightness_amp, args.scale_amp,
                   args.width, args.height))


if __name__ == "__main__":
    main()
