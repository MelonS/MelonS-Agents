#!/usr/bin/env python3
"""beat-narration.py — beat-aligned synthetic narration for data-chart shorts.

Each beat in the spec carries its own `vo` line. Instead of generating one long
narration and hoping it lines up with the visuals, this synthesizes ONE clip PER
BEAT and lays each at its beat's exact start time. Alignment is arithmetic, not
ASR — nothing to drift.

If a clip overruns its beat it is re-synthesized at a faster rate (up to a cap)
rather than silently bleeding into the next beat.

Usage:
  python scripts/beat-narration.py <spec.json> <out.wav> [--fps 30] [--voice ko-KR-SunHiNeural]
"""

import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

FPS_DEFAULT = 30
VOICE_DEFAULT = "ko-KR-SunHiNeural"
RATE_STEPS = ["+0%", "+8%", "+16%", "+25%", "+35%"]
HEAD_PAD_S = 0.25          # let a beat's visual land before its line starts
TAIL_SLACK_S = 0.15        # a clip may run this close to the beat edge


def ffbin(name):
    import os
    return os.environ.get(f"{name.upper()}_BIN") or shutil.which(name) or name


def duration_s(path):
    out = subprocess.run(
        [ffbin("ffprobe"), "-v", "error", "-show_entries", "format=duration",
         "-of", "default=nw=1:nk=1", str(path)],
        capture_output=True, text=True)
    try:
        return float(out.stdout.strip())
    except ValueError:
        return 0.0


def synth(text, voice, rate, out_path):
    cmd = [sys.executable, "-m", "edge_tts", "--voice", voice,
           "--text", text, "--write-media", str(out_path)]
    if rate != "+0%":
        cmd += ["--rate", rate]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0 or not Path(out_path).exists():
        sys.exit(f"[beat-narration] edge-tts failed for {text[:24]!r}\n{r.stderr[-500:]}")


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    flags = {a.split("=", 1)[0]: (a.split("=", 1)[1] if "=" in a else True)
             for a in sys.argv[1:] if a.startswith("--")}
    if len(args) < 2:
        sys.exit(__doc__)
    spec_path, out_path = Path(args[0]), Path(args[1])
    fps = int(flags.get("--fps", FPS_DEFAULT))
    voice = flags.get("--voice", VOICE_DEFAULT)

    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    beats = spec["beats"]
    tmp = Path(tempfile.mkdtemp(prefix="beatvo-"))

    clips, start_f, overruns = [], 0, []
    for i, b in enumerate(beats):
        beat_s = b["frames"] / fps
        start_s = start_f / fps + HEAD_PAD_S
        budget = beat_s - HEAD_PAD_S + TAIL_SLACK_S
        vo = (b.get("vo") or "").strip()
        if vo:
            clip = tmp / f"{i:02d}.mp3"
            chosen, dur = None, 0.0
            for rate in RATE_STEPS:
                synth(vo, voice, rate, clip)
                dur = duration_s(clip)
                chosen = rate
                if dur <= budget:
                    break
            flag = "" if dur <= budget else "  ← OVERRUN"
            if dur > budget:
                overruns.append((b["scene"], round(dur, 2), round(budget, 2)))
            print(f"  · {b['scene']:9s} beat {beat_s:5.2f}s  vo {dur:5.2f}s  "
                  f"rate {chosen:>5s}{flag}")
            clips.append((clip, start_s))
        start_f += b["frames"]

    total_s = start_f / fps
    inputs, filters, labels = [], [], []
    for n, (clip, start_s) in enumerate(clips):
        inputs += ["-i", str(clip)]
        filters.append(f"[{n}:a]aresample=48000,adelay={int(start_s * 1000)}|"
                       f"{int(start_s * 1000)}[a{n}]")
        labels.append(f"[a{n}]")
    filter_complex = ";".join(filters) + ";" + "".join(labels) + \
        f"amix=inputs={len(clips)}:normalize=0:dropout_transition=0,"\
        f"alimiter=limit=0.95,apad,atrim=0:{total_s:.3f}[out]"

    out_path.parent.mkdir(parents=True, exist_ok=True)
    cmd = [ffbin("ffmpeg"), "-y", "-loglevel", "error", *inputs,
           "-filter_complex", filter_complex, "-map", "[out]",
           "-ac", "1", "-ar", "48000", str(out_path)]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        sys.exit(f"[beat-narration] mux failed\n{r.stderr[-800:]}")

    print(f"[beat-narration] {len(clips)} clips → {out_path} ({duration_s(out_path):.2f}s)")
    if overruns:
        print("[beat-narration] WARNING — lines that still overrun their beat "
              "(shorten the vo text or lengthen the beat):")
        for scene, d, b in overruns:
            print(f"    {scene}: {d}s vo in a {b}s slot")
    shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    main()
