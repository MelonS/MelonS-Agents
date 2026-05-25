#!/usr/bin/env python3
"""mix2-design-matrix.py — generate the segment matrix for Mix #2.

Input:  tracks.json (list of source music tracks with duration + metadata)
Output: segments.json (list of ~600 segment entries, ~4-5s each)

Each segment carries: ts_start, duration, track_idx, sub_mood,
time_of_day, shader_ratio, prompt_template, anchor_keywords, seed.

The matrix enforces visual diversity per [[ai-hands-avoid]] +
[mix-2-design.md] §"Sub-mood × time-of-day":
  - 7 sub-moods cycled (neon-alley / subway / cafe / rooftop /
    han-river / convenience / studio-window)
  - 3 time-of-day phases progressing through the mix
    (evening → night → dawn)
  - per-track style shift across the 44 min
  - anti-repetition guarantees: same (sub_mood, time_of_day) cell
    never adjacent

Cross-platform: pure Python stdlib, no OS branches.

Usage:
  python scripts/mix2-design-matrix.py \\
      --tracks-json tracks.json \\
      --clip-duration 4.0 \\
      --output segments.json
"""
from __future__ import annotations

import argparse
import json
import random
from pathlib import Path

SUB_MOODS = [
    "neon-alley",
    "subway",
    "cafe",
    "rooftop",
    "han-river",
    "convenience",
    "studio-window",
]

TIME_OF_DAY = ["evening", "night", "dawn"]

SUB_MOOD_PROMPTS = {
    "neon-alley": {
        "evening": "narrow Seoul alley at dusk, warm orange and pink neon signs starting to glow, wet pavement reflections, light rain mist, empty street, cinematic lofi atmosphere, 35mm film grain, no people",
        "night":   "narrow Seoul alley deep night, magenta and cyan neon signs blazing, wet pavement reflecting saturated colors, heavy drizzle, empty street, cinematic noir lofi mood, 35mm film grain, no people",
        "dawn":    "narrow Seoul alley pre-dawn pale blue light, neon signs barely glowing, wet cobblestones, light fog drifting, empty street, quiet melancholic mood, 35mm film grain, no people",
    },
    "subway": {
        "evening": "Seoul subway platform at evening rush hour ending, warm fluorescent overhead lights, empty platform with departing train motion blur, golden hour glow through windows, melancholic lofi mood, no faces, no hands",
        "night":   "Seoul subway empty platform at deep night, cold fluorescent lights, station signs reflecting on polished floor, single empty train car visible, melancholic lofi atmosphere, no people in frame",
        "dawn":    "Seoul subway platform first morning train arriving dawn, pale blue station lights, empty platform with shafts of light through skylights, quiet contemplative mood, no faces, no hands",
    },
    "cafe": {
        "evening": "cozy Seoul cafe window seat at dusk, warm amber lamp light, steam rising from coffee cup on wooden table, condensation on window with raindrops, blurred street lights outside, intimate lofi mood, no people, no hands",
        "night":   "Seoul cafe window deep night, single warm lamp glowing, rain streaking down window, empty wooden table with closed book and cooling teacup, vinyl record nearby, intimate melancholic mood, no people visible",
        "dawn":    "Seoul cafe window at dawn pale light, cool blue tones with warm lamp accent, empty wooden table with morning newspaper and ceramic mug, soft fog outside, contemplative quiet mood, no people, no hands",
    },
    "rooftop": {
        "evening": "Seoul rooftop golden hour, low sun warming concrete edge, city skyline in soft haze, distant Namsan tower silhouette, empty rooftop with single empty chair, melancholic lofi mood, no people",
        "night":   "Seoul rooftop deep night, city lights twinkling like stars below, dark sky with single airplane light streak, empty concrete rooftop with edge railing, melancholic lofi atmosphere, no people in frame",
        "dawn":    "Seoul rooftop pre-dawn, mist drifting over city, building silhouettes barely visible, pale orange sliver on horizon, empty rooftop with morning dew, quiet contemplative mood, no people",
    },
    "han-river": {
        "evening": "Han River bridge at sunset, warm orange sky reflected on water surface, distant boat lights, empty riverside path, mountains silhouette, melancholic lofi mood, no people, no hands",
        "night":   "Han River bridge deep night, bridge lights reflecting on dark water, city skyline glowing in distance, empty walking path along riverside, ambient lofi atmosphere, no people in frame",
        "dawn":    "Han River bridge at dawn fog, pale blue water mirror smooth, bridge silhouette in mist, single distant runner barely visible (far silhouette, no face), quiet contemplative mood, no hands",
    },
    "convenience": {
        "evening": "Seoul convenience store window at dusk, warm interior fluorescent light spilling out, condensation on glass, rain droplets, empty street outside, melancholic urban lofi mood, no people, no hands",
        "night":   "Seoul 24h convenience store deep night, cold fluorescent interior visible through window, empty street outside with single streetlamp, rain falling, lonely lofi atmosphere, no people in frame",
        "dawn":    "Seoul convenience store at dawn, warm fluorescent light vs cold blue exterior, empty street with first morning bus stop visible, quiet contemplative mood, no people, no hands",
    },
    "studio-window": {
        "evening": "home studio window at dusk, vintage vinyl record spinning on turntable, warm desk lamp glow, books and coffee cup, blurred city lights through window, intimate creative mood, lofi 35mm grain, no people, no hands",
        "night":   "home studio at night, vinyl record on turntable mid-rotation, single warm desk lamp, headphones resting on closed book, rain on window, intimate melancholic mood, no people, no hands visible",
        "dawn":    "home studio at dawn pale light, turntable arm rested back home position, empty mug on desk, dust motes in pale window light, quiet aftermath mood, no people, no hands",
    },
}

NEGATIVE_BASE = "low quality, deformed, distorted, motion artifacts, blurry, watermark, text, frame border"


def time_of_day_for_phase(phase_idx: int, total_phases: int) -> str:
    """Map a phase index (0..N-1) to time-of-day progression.

    Mix #2 starts evening, progresses through night, ends dawn.
    Split the mix into 3 equal parts.
    """
    if total_phases <= 0:
        return "night"
    ratio = phase_idx / total_phases
    if ratio < 0.33:
        return "evening"
    elif ratio < 0.67:
        return "night"
    else:
        return "dawn"


def pick_sub_mood(prev_mood: str | None, rng: random.Random) -> str:
    """Pick a sub-mood that's not the previous one (anti-repetition)."""
    pool = [m for m in SUB_MOODS if m != prev_mood]
    return rng.choice(pool)


def generate_segments(
    tracks: list[dict],
    clip_duration: float,
    seed: int,
) -> list[dict]:
    rng = random.Random(seed)
    segments = []
    seg_idx = 0
    total_duration = sum(t["duration"] for t in tracks)
    cumulative = 0.0
    prev_mood = None

    for track_idx, track in enumerate(tracks):
        track_dur = track["duration"]
        track_segments = max(1, int(round(track_dur / clip_duration)))
        # actual per-segment duration (distribute remainder evenly)
        actual_dur = track_dur / track_segments

        for ts in range(track_segments):
            seg_ts_start = cumulative + ts * actual_dur
            # phase for time-of-day progression — based on global position
            phase_idx = int((seg_ts_start / total_duration) * 100)
            time_of_day = time_of_day_for_phase(phase_idx, 100)
            sub_mood = pick_sub_mood(prev_mood, rng)
            prev_mood = sub_mood

            prompt = SUB_MOOD_PROMPTS[sub_mood][time_of_day]

            # shader restraint: ratio varies by phrase position
            # mid-clip (middle 60%) gets higher ratio, ends/starts low
            position_in_track = ts / track_segments
            if 0.2 <= position_in_track <= 0.8:
                shader_ratio = round(0.35 + rng.uniform(-0.05, 0.05), 2)
            else:
                shader_ratio = round(0.15 + rng.uniform(-0.03, 0.03), 2)

            segments.append({
                "idx": seg_idx,
                "ts_start": round(seg_ts_start, 3),
                "duration": round(actual_dur, 3),
                "track_idx": track_idx,
                "track_name": track.get("name", f"track-{track_idx}"),
                "sub_mood": sub_mood,
                "time_of_day": time_of_day,
                "shader_ratio": shader_ratio,
                "prompt": prompt,
                "negative": NEGATIVE_BASE,
                "seed": rng.randint(1, 2**31 - 1),
            })
            seg_idx += 1

        cumulative += track_dur

    return segments


def main():
    p = argparse.ArgumentParser(description="Generate Mix #2 segment matrix")
    p.add_argument("--tracks-json", required=True, type=Path,
                   help='JSON: [{"name": "track1", "duration": 222.5}, ...]')
    p.add_argument("--clip-duration", type=float, default=4.0,
                   help="target seconds per LTX-Video segment (default 4.0)")
    p.add_argument("--seed", type=int, default=2026,
                   help="random seed for reproducible matrix")
    p.add_argument("--output", required=True, type=Path,
                   help="output segments.json")
    args = p.parse_args()

    tracks = json.loads(args.tracks_json.read_text(encoding="utf-8"))
    if not isinstance(tracks, list):
        raise ValueError("tracks-json must be a JSON list of track dicts")

    segments = generate_segments(tracks, args.clip_duration, args.seed)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(segments, indent=2, ensure_ascii=False), encoding="utf-8")

    total = sum(s["duration"] for s in segments)
    print(f"[matrix] wrote {args.output}: {len(segments)} segments, total {total:.1f}s")
    print(f"[matrix] sub_mood distribution:")
    from collections import Counter
    cnt = Counter(s["sub_mood"] for s in segments)
    for mood, n in cnt.most_common():
        print(f"  {mood:18s} {n:4d}  {n*100/len(segments):.1f}%")
    print(f"[matrix] time_of_day distribution:")
    cnt2 = Counter(s["time_of_day"] for s in segments)
    for tod, n in cnt2.most_common():
        print(f"  {tod:10s} {n:4d}  {n*100/len(segments):.1f}%")


if __name__ == "__main__":
    main()
