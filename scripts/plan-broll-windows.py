"""Group caption SRT cues into N temporal windows for B-roll planning.

The faceless-short pipeline used to extract 6 search terms from the
entire narration script in a single ollama call, then assign each term
to a fixed N/6-second slot.  That gave equal-length slots with no
guarantee the visual matched what was being said — clip #3 might show
"cuneiform tablets" while the narration was still introducing the
kingdom.

This helper consumes the caption-corrected SRT (which carries whisper
timing) and groups consecutive cues into NUM_WINDOWS temporal windows
of roughly equal duration.  Each window's joint text is what drives a
per-window keyword search downstream — so the B-roll lands where the
caption it matches is on screen.

Usage:
  python3 scripts/plan-broll-windows.py \
    --srt <captions.srt> \
    --total-duration <narration_dur_seconds> \
    --num-windows 8 \
    --out <windows.json>

Output JSON:
  [
    {"index": 0, "start": 0.00, "end": 6.30, "text": "<joint cue text>"},
    ...
  ]
"""

import argparse
import json
import re
import sys


_TS_RE = re.compile(r"^(\d{2}):(\d{2}):(\d{2}),(\d{3})$")


def parse_ts(ts: str) -> float:
    m = _TS_RE.match(ts.strip())
    if not m:
        return 0.0
    h, mn, s, ms = map(int, m.groups())
    return h * 3600 + mn * 60 + s + ms / 1000.0


def parse_srt(text: str):
    """Yield (start_sec, end_sec, text) for each cue."""
    out = []
    blocks = re.split(r"\n\s*\n", text.strip())
    arrow_re = re.compile(r"^([\d:,]+)\s*-->\s*([\d:,]+)")
    for blk in blocks:
        lines = [ln for ln in blk.splitlines() if ln.strip() != ""]
        if len(lines) < 2:
            continue
        ts_line = lines[1] if "-->" in lines[1] else (lines[0] if "-->" in lines[0] else None)
        if ts_line is None:
            continue
        m = arrow_re.match(ts_line.strip())
        if not m:
            continue
        start = parse_ts(m.group(1))
        end = parse_ts(m.group(2))
        body_lines = lines[2:] if "-->" in lines[1] else lines[1:]
        cue_text = " ".join(t for t in body_lines if t)
        out.append((start, end, cue_text))
    return out


def group_into_windows(cues, num_windows: int, total_duration: float):
    """Partition cues into num_windows temporally-contiguous groups.

    Each window's start is the first cue's start; its end is the last
    cue's end (or the next window's start, or total_duration for the
    final window).  Strategy: target equal cue counts per window, then
    snap to cue boundaries.  If there are fewer cues than windows, the
    function returns one window per cue and pads to total_duration."""
    if not cues:
        return []

    n_cues = len(cues)
    if num_windows >= n_cues:
        # Each cue its own window.
        windows = []
        for i, (s, e, t) in enumerate(cues):
            windows.append({"index": i, "start": s, "end": e, "text": t})
        # Stretch the last window's end to total_duration so per-window
        # trim sums match the narration length.
        if windows and total_duration > windows[-1]["end"]:
            windows[-1]["end"] = total_duration
        return windows

    # Standard case: pack roughly n_cues / num_windows cues per window.
    per_window = n_cues / num_windows
    windows = []
    for w in range(num_windows):
        first = int(round(w * per_window))
        last = int(round((w + 1) * per_window)) - 1
        if w == num_windows - 1:
            last = n_cues - 1
        first = max(0, min(first, n_cues - 1))
        last = max(first, min(last, n_cues - 1))
        start = cues[first][0]
        if w == 0:
            start = 0.0  # always anchor first window at 0
        end = cues[last][1] if w < num_windows - 1 else max(cues[last][1], total_duration)
        text = " ".join(c[2] for c in cues[first:last + 1])
        windows.append({
            "index": w,
            "start": round(start, 3),
            "end": round(end, 3),
            "text": text,
        })

    # Stitch boundaries: each window's end becomes the next window's
    # start, so the concat has no gaps.
    for i in range(len(windows) - 1):
        windows[i]["end"] = windows[i + 1]["start"]
    if windows:
        windows[-1]["end"] = round(max(windows[-1]["end"], total_duration), 3)
    return windows


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--srt", required=True)
    p.add_argument("--total-duration", type=float, required=True)
    p.add_argument("--num-windows", type=int, default=8)
    p.add_argument("--out", required=True)
    args = p.parse_args()

    with open(args.srt, encoding="utf-8") as f:
        cues = parse_srt(f.read())

    windows = group_into_windows(cues, args.num_windows, args.total_duration)

    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(windows, f, ensure_ascii=False, indent=2)

    # Quick diagnostic to stderr.
    print(f"planned {len(windows)} windows over {args.total_duration:.2f}s:", file=sys.stderr)
    for w in windows:
        dur = w["end"] - w["start"]
        snippet = w["text"][:60] + ("…" if len(w["text"]) > 60 else "")
        print(f"  [{w['index']}] {w['start']:6.2f}–{w['end']:6.2f}s ({dur:5.2f}s)  {snippet}", file=sys.stderr)

    return 0


if __name__ == "__main__":
    sys.exit(main())
