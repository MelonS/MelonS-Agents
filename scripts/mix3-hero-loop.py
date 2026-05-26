#!/usr/bin/env python3
"""mix3-hero-loop.py - single hero clip × infinite loop architecture.

Generates N text-free atmospheric hero candidates (SDXL-Turbo still →
LTX-Video img2vid), then loops a chosen hero clip with a target audio
file via ffmpeg -stream_loop.

Background: Mix #2 used 598-clip diversity strategy.  Operator feedback
2026-05-26 = visual cuts distracting + AI text artifacts grotesque.
Mix #3 pivots to "one beautiful clip, infinite loop, audio variation".

Usage:
  # Stage 1 - generate candidates (~10-20 min for 5 hero candidates)
  python scripts/mix3-hero-loop.py generate --count 5 \\
      --output-dir outputs/publish/mix-3-hero-candidates

  # Stage 2 - build final from chosen hero
  python scripts/mix3-hero-loop.py build \\
      --hero outputs/publish/mix-3-hero-candidates/hero-00.mp4 \\
      --audio path/to/audio.mp3 \\
      --output outputs/publish/mix-3/yt-mix-3-<theme>-<date>.mp4

  # All-in-one (auto-pick first candidate)
  python scripts/mix3-hero-loop.py auto \\
      --audio path/to/audio.mp3 \\
      --output-dir outputs/publish/mix-3
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

FFMPEG = os.environ.get("FFMPEG_BIN", "ffmpeg")
COMFYUI_URL = os.environ.get("COMFYUI_URL", "http://127.0.0.1:8188")
SCRIPT_DIR = Path(__file__).resolve().parent
LTX_IMG2VID = SCRIPT_DIR / "ltx-img2vid.py"

# Hero pool - atmospheric, text-free, hand-curated.  Each prompt is
# explicitly text-avoid + composition that minimizes AI-artifact risk.
HERO_PROMPTS = [
    {
        "name": "rooftop-heavy-rain",
        "prompt": (
            "Static fixed camera view of an empty Seoul rooftop at deep night. "
            "Heavy continuous rain falls vertically with thousands of visible "
            "raindrops streaming across the entire frame from top to bottom. "
            "Wet rooftop surface reflects warm distant city glow. "
            "Rain splashes are visible on the puddles. "
            "Lofi cinematic color grade, 35mm film grain. "
            "Camera does not move - only the rain moves. "
            "No people, no animals, no signs, no posters, no shop fronts, "
            "no neon text, no billboards, no letters, no logos, no readable text anywhere."
        ),
    },
    {
        "name": "rain-on-window-warm",
        "prompt": (
            "Static fixed camera close-up shot looking at a dark window glass. "
            "Heavy continuous rain runs down the glass in many flowing rivulets. "
            "Visible raindrops constantly impact the glass and trickle downward. "
            "Behind the glass, warm orange and pink window bokeh is heavily blurred. "
            "Camera is completely still - only the rain water moves. "
            "Intimate cinematic lofi atmosphere. "
            "No people, no objects behind glass, no signs, no text, no letters, "
            "no logos, no readable surfaces."
        ),
    },
    {
        "name": "steaming-coffee-static",
        "prompt": (
            "Static fixed camera close-up of a dark ceramic coffee cup on a wooden table. "
            "Continuous wisps of warm white steam rise gently from the coffee surface "
            "and curl upward through the warm lamp light beam. "
            "Steam motion is constant and natural. "
            "Background is warm blurred bokeh of cafe lights. "
            "Camera does not move - only the steam moves. "
            "No hands, no people, no faces, no text, no logos on the cup, "
            "no signs, no menu boards, no readable surfaces."
        ),
    },
    {
        "name": "snow-falling-window",
        "prompt": (
            "Static fixed camera view looking out a dark window at heavy snow falling. "
            "Thousands of snowflakes drift downward across the entire frame. "
            "Snowflakes vary in size and speed for parallax depth. "
            "Soft warm interior light reflects faintly on the window. "
            "Outside, distant blurred warm street lamps form bokeh. "
            "Camera is completely still - only the snow moves. "
            "Cinematic lofi mood, 35mm grain. "
            "No people, no buildings with visible signs, no text, no letters, "
            "no readable surfaces."
        ),
    },
    {
        "name": "fireplace-flames-static",
        "prompt": (
            "Static fixed camera close-up of a small fireplace with continuously "
            "flickering warm orange flames dancing over dark logs. "
            "Flames move naturally and constantly with realistic motion. "
            "Glowing embers occasionally pop. "
            "Background is dim warm cabin interior with soft bokeh. "
            "Camera does not move - only the flames and embers move. "
            "Cozy lofi cinematic mood. "
            "No people, no animals, no text, no signs, no books with readable covers, "
            "no logos, no readable surfaces anywhere."
        ),
    },
]

# Common negative prompt - explicit text + sign + people exclusions
NEGATIVE = (
    "low quality, blurry, watermark, signature, frame border, "
    "people, person, face, faces, hand, hands, fingers, "
    "text, letters, signs, neon signs, shop signs, shop fronts, "
    "billboards, posters, logos, brands, writing, characters, "
    "phone screens, TV screens, book covers, newspapers, menus, "
    "graffiti, store front, advertising, deformed, distorted, ugly"
)


def fetch_audio_duration(audio_path: Path, ffmpeg_bin: str) -> float:
    """Get audio duration in seconds via ffprobe.

    Resolves ffprobe sibling of ffmpeg when FFMPEG_BIN is a path; else
    falls back to "ffprobe" on PATH.
    """
    env_probe = os.environ.get("FFPROBE_BIN", "")
    if env_probe:
        ffprobe = env_probe
    elif "/" in ffmpeg_bin or "\\" in ffmpeg_bin:
        # path-style: probe sibling
        ffmpeg_p = Path(ffmpeg_bin)
        ext = ffmpeg_p.suffix  # .exe or empty
        ffprobe = str(ffmpeg_p.parent / f"ffprobe{ext}")
    else:
        ffprobe = "ffprobe"
    out = subprocess.run(
        [ffprobe, "-v", "error", "-show_entries", "format=duration",
         "-of", "default=nw=1:nk=1", str(audio_path)],
        capture_output=True, text=True,
    )
    if out.returncode != 0:
        raise RuntimeError(f"ffprobe failed: {out.stderr}")
    return float(out.stdout.strip())


def generate_hero_via_sdxl(prompt: str, name: str, out_jpg: Path,
                            server: str, width: int = 1024, height: int = 576,
                            seed: int = 42) -> bool:
    """Generate a still via SDXL-Turbo + ComfyUI."""
    # Round to 32-multiples
    w = (width // 32) * 32 + (32 if width % 32 else 0) if width % 32 else width
    h = (height // 32) * 32 + (32 if height % 32 else 0) if height % 32 else height
    workflow = {
        "1": {"class_type": "CheckpointLoaderSimple",
              "inputs": {"ckpt_name": "sd_xl_turbo_1.0_fp16.safetensors"}},
        "2": {"class_type": "CLIPTextEncode",
              "inputs": {"clip": ["1", 1], "text": prompt}},
        "3": {"class_type": "CLIPTextEncode",
              "inputs": {"clip": ["1", 1], "text": NEGATIVE}},
        "4": {"class_type": "EmptyLatentImage",
              "inputs": {"width": w, "height": h, "batch_size": 1}},
        "5": {"class_type": "KSampler",
              "inputs": {"model": ["1", 0], "seed": seed, "steps": 6,
                         "cfg": 1.5, "sampler_name": "euler_ancestral",
                         "scheduler": "normal", "denoise": 1.0,
                         "positive": ["2", 0], "negative": ["3", 0],
                         "latent_image": ["4", 0]}},
        "6": {"class_type": "VAEDecode",
              "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
        "7": {"class_type": "SaveImage",
              "inputs": {"images": ["6", 0], "filename_prefix": f"mix3_{name}"}},
    }
    data = json.dumps({"prompt": workflow, "client_id": f"mix3_{name}_{seed}"}).encode("utf-8")
    try:
        req = urllib.request.Request(
            f"{server}/prompt", data=data,
            headers={"Content-Type": "application/json"}, method="POST")
        with urllib.request.urlopen(req, timeout=30) as resp:
            result = json.loads(resp.read())
    except urllib.error.HTTPError as e:
        print(f"[sdxl] ERROR {e.code}: {e.read().decode('utf-8',errors='ignore')[:400]}", file=sys.stderr)
        return False
    prompt_id = result.get("prompt_id")
    if not prompt_id:
        return False
    t0 = time.time()
    while True:
        time.sleep(2)
        if time.time() - t0 > 90:
            print(f"[sdxl] timeout for {name}", file=sys.stderr)
            return False
        try:
            with urllib.request.urlopen(f"{server}/history/{prompt_id}", timeout=10) as resp:
                hist = json.loads(resp.read())
        except urllib.error.HTTPError as e:
            if e.code == 404:
                continue
            raise
        if prompt_id in hist:
            entry = hist[prompt_id]
            if entry.get("status", {}).get("completed"):
                comfy_out = Path("G:/ai/ComfyUI_windows_portable/ComfyUI/output")
                for _, node_out in entry.get("outputs", {}).items():
                    for img in node_out.get("images", []):
                        comfy_file = comfy_out / img.get("subfolder", "") / img["filename"]
                        if comfy_file.exists():
                            out_jpg.parent.mkdir(parents=True, exist_ok=True)
                            shutil.move(str(comfy_file), str(out_jpg))
                            return True
                return False


def run_ltx_img2vid(image: Path, prompt: str, output: Path, *,
                     width: int = 1024, height: int = 576,
                     length: int = 193, steps: int = 40,
                     server: str = COMFYUI_URL, seed: int = -1) -> int:
    cmd = [
        sys.executable, str(LTX_IMG2VID),
        "--image", str(image),
        "--prompt", prompt,
        "--output", str(output),
        "--width", str(width),
        "--height", str(height),
        "--length", str(length),
        "--fps", "24",
        "--steps", str(steps),
        "--negative-extra", NEGATIVE,
        "--server", server,
        "--timeout", "300",
        "--skip-existing",
    ]
    if seed >= 0:
        cmd += ["--seed", str(seed)]
    return subprocess.run(cmd).returncode


def cmd_generate(args):
    out_dir = Path(args.output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    prompts = HERO_PROMPTS[:args.count]
    if not prompts:
        print("no prompts to generate (count=0)", file=sys.stderr)
        return 1
    print(f"[mix3] generating {len(prompts)} hero candidates → {out_dir}")
    for i, spec in enumerate(prompts):
        name = spec["name"]
        prompt = spec["prompt"]
        jpg = out_dir / f"hero-{i:02d}-{name}.jpg"
        mp4 = out_dir / f"hero-{i:02d}-{name}.mp4"
        seed = 1000 + i * 17
        if not jpg.exists():
            print(f"[mix3] still {i+1}/{len(prompts)} - {name}")
            ok = generate_hero_via_sdxl(prompt=prompt, name=name, out_jpg=jpg,
                                         server=args.server, seed=seed)
            if not ok:
                print(f"[mix3] FAIL still {name}", file=sys.stderr)
                continue
        if not mp4.exists():
            print(f"[mix3] clip {i+1}/{len(prompts)} - {name} (LTX 40 steps, ~80-120s)")
            rc = run_ltx_img2vid(image=jpg, prompt=prompt, output=mp4,
                                  width=1024, height=576, length=193,
                                  steps=40, server=args.server, seed=seed)
            if rc != 0:
                print(f"[mix3] FAIL clip {name} (rc={rc})", file=sys.stderr)
                continue
    print(f"[mix3] candidate list:")
    for f in sorted(out_dir.glob("hero-*.mp4")):
        sz = f.stat().st_size / 1024
        print(f"  {sz:>7.0f} KB  {f}")
    return 0


def cmd_build(args):
    hero = Path(args.hero)
    audio = Path(args.audio)
    output = Path(args.output)
    if not hero.exists():
        print(f"hero not found: {hero}", file=sys.stderr)
        return 2
    if not audio.exists():
        print(f"audio not found: {audio}", file=sys.stderr)
        return 2
    output.parent.mkdir(parents=True, exist_ok=True)
    duration = fetch_audio_duration(audio, FFMPEG)
    print(f"[mix3] looping {hero.name} → {output.name} (audio {duration:.1f}s)")
    cmd = [
        FFMPEG, "-y", "-hide_banner", "-loglevel", "warning",
        "-stream_loop", "-1", "-i", str(hero),
        "-i", str(audio),
        "-map", "0:v", "-map", "1:a",
        "-c:v", "copy",  # no re-encode, instant
        "-c:a", "aac", "-b:a", "192k",
        "-shortest",
        "-t", f"{duration:.3f}",
        str(output),
    ]
    rc = subprocess.run(cmd).returncode
    if rc == 0:
        sz = output.stat().st_size / 1024 / 1024
        print(f"[mix3] DONE: {output} ({sz:.1f} MB)")
    return rc


def cmd_auto(args):
    """All-in-one: generate candidates → pick first → build final."""
    cand_dir = Path(args.output_dir) / "hero-candidates"
    gen_args = argparse.Namespace(
        output_dir=str(cand_dir),
        count=args.count,
        server=args.server,
    )
    rc = cmd_generate(gen_args)
    if rc != 0:
        return rc
    # pick first candidate
    candidates = sorted(cand_dir.glob("hero-*.mp4"))
    if not candidates:
        print("no candidates generated", file=sys.stderr)
        return 3
    hero = candidates[args.pick_idx if args.pick_idx < len(candidates) else 0]
    out = Path(args.output_dir) / f"yt-mix-3-{hero.stem}-{time.strftime('%Y-%m-%d')}.mp4"
    build_args = argparse.Namespace(
        hero=str(hero),
        audio=args.audio,
        output=str(out),
    )
    return cmd_build(build_args)


def main():
    p = argparse.ArgumentParser(description="Mix #3 hero-loop orchestrator")
    sub = p.add_subparsers(dest="cmd", required=True)

    pg = sub.add_parser("generate", help="generate N hero candidates")
    pg.add_argument("--output-dir", required=True)
    pg.add_argument("--count", type=int, default=5)
    pg.add_argument("--server", default=COMFYUI_URL)
    pg.set_defaults(func=cmd_generate)

    pb = sub.add_parser("build", help="loop hero with audio")
    pb.add_argument("--hero", required=True)
    pb.add_argument("--audio", required=True)
    pb.add_argument("--output", required=True)
    pb.set_defaults(func=cmd_build)

    pa = sub.add_parser("auto", help="generate + auto-pick + build")
    pa.add_argument("--output-dir", required=True)
    pa.add_argument("--audio", required=True)
    pa.add_argument("--count", type=int, default=5)
    pa.add_argument("--pick-idx", type=int, default=0,
                    help="which candidate to use (default 0 = first)")
    pa.add_argument("--server", default=COMFYUI_URL)
    pa.set_defaults(func=cmd_auto)

    args = p.parse_args()
    sys.exit(args.func(args) or 0)


if __name__ == "__main__":
    main()
