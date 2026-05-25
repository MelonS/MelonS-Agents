#!/usr/bin/env python3
"""ltx-img2vid.py — image → 5-sec motion clip via LTX-Video on ComfyUI.

Cross-platform (mac / linux / windows).  Calls a ComfyUI server's REST
API.  Default server: http://127.0.0.1:8188.

Usage:
  python scripts/ltx-img2vid.py --image stills/01.jpg --prompt "..." \\
      --output clips/01.mp4

Resumable: if --output exists and --skip-existing, returns 0 without work.

Requires:
  - ComfyUI server running locally (or pass --server URL)
  - Models installed in ComfyUI/models/:
      checkpoints/ltx-video-2b-v0.9.5.safetensors
      text_encoders/t5xxl_fp8_e4m3fn.safetensors
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

DEFAULT_SERVER = os.environ.get("COMFYUI_URL", "http://127.0.0.1:8188")

# Negative prompt baked in — applies the [[ai-hands-avoid]] rule globally.
# Hands are appended to the user-supplied negative prompt automatically.
AI_HANDS_AVOID_NEGATIVE = (
    "hand, hands, fingers, reaching, grasping, holding object, "
    "playing instrument, writing, typing, "
    "deformed hand, weird hand, fused fingers, six fingers, "
    "extra fingers, twisted joint, mangled limb"
)

DEFAULT_NEGATIVE_BASE = (
    "low quality, worst quality, deformed, distorted, disfigured, "
    "motion smear, motion artifacts, bad anatomy, ugly, blurry, "
    "watermark, text, signature, frame border"
)


def post_prompt(server: str, workflow: dict, client_id: str) -> dict:
    data = json.dumps({"prompt": workflow, "client_id": client_id}).encode("utf-8")
    req = urllib.request.Request(
        f"{server}/prompt",
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        return json.loads(resp.read())


def get_history(server: str, prompt_id: str) -> dict | None:
    url = f"{server}/history/{prompt_id}"
    try:
        with urllib.request.urlopen(url, timeout=15) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return None
        raise


def stage_input_image(server_input_dir: Path, image_path: Path) -> str:
    """Copy the input image into ComfyUI's input/ dir so LoadImage finds it.

    Returns the filename (basename) to reference in the workflow.
    Idempotent: if already present and same content, no-op.
    """
    server_input_dir.mkdir(parents=True, exist_ok=True)
    dest_name = image_path.name
    dest = server_input_dir / dest_name
    if dest.exists() and dest.stat().st_size == image_path.stat().st_size:
        return dest_name
    shutil.copy2(image_path, dest)
    return dest_name


def build_workflow(
    image_name: str,
    positive: str,
    negative: str,
    width: int,
    height: int,
    length: int,
    fps: int,
    steps: int,
    seed: int,
    filename_prefix: str,
) -> dict:
    """Build the API-format workflow JSON for LTX-Video img2vid.

    Validated against ComfyUI v0.22.0 with LTX-Video 2B v0.9.5.
    Test passed 2026-05-25: 768x432, 97 frames, 30 steps -> 27s on
    RTX 4070 Ti SUPER 16GB.
    """
    return {
        "1": {
            "class_type": "CheckpointLoaderSimple",
            "inputs": {"ckpt_name": "ltx-video-2b-v0.9.5.safetensors"},
        },
        "2": {
            "class_type": "CLIPLoader",
            "inputs": {
                "clip_name": "t5xxl_fp8_e4m3fn.safetensors",
                "type": "ltxv",
                "device": "default",
            },
        },
        "3": {
            "class_type": "LoadImage",
            "inputs": {"image": image_name, "upload": "image"},
        },
        "4": {
            "class_type": "CLIPTextEncode",
            "inputs": {"clip": ["2", 0], "text": positive},
        },
        "5": {
            "class_type": "CLIPTextEncode",
            "inputs": {"clip": ["2", 0], "text": negative},
        },
        "6": {
            "class_type": "LTXVImgToVideo",
            "inputs": {
                "positive": ["4", 0],
                "negative": ["5", 0],
                "vae": ["1", 2],
                "image": ["3", 0],
                "width": width,
                "height": height,
                "length": length,
                "batch_size": 1,
                "strength": 1.0,
            },
        },
        "7": {
            "class_type": "LTXVConditioning",
            "inputs": {
                "positive": ["6", 0],
                "negative": ["6", 1],
                "frame_rate": 25,
            },
        },
        "8": {
            "class_type": "LTXVScheduler",
            "inputs": {
                "steps": steps,
                "max_shift": 2.05,
                "base_shift": 0.95,
                "stretch": True,
                "terminal": 0.1,
                "latent": ["6", 2],
            },
        },
        "9": {
            "class_type": "KSamplerSelect",
            "inputs": {"sampler_name": "euler"},
        },
        "10": {
            "class_type": "SamplerCustom",
            "inputs": {
                "model": ["1", 0],
                "add_noise": True,
                "noise_seed": seed,
                "cfg": 3.0,
                "positive": ["7", 0],
                "negative": ["7", 1],
                "sampler": ["9", 0],
                "sigmas": ["8", 0],
                "latent_image": ["6", 2],
            },
        },
        "11": {
            "class_type": "VAEDecode",
            "inputs": {"samples": ["10", 0], "vae": ["1", 2]},
        },
        "12": {
            "class_type": "CreateVideo",
            "inputs": {"images": ["11", 0], "fps": fps},
        },
        "13": {
            "class_type": "SaveVideo",
            "inputs": {
                "video": ["12", 0],
                "filename_prefix": filename_prefix,
                "format": "auto",
                "codec": "auto",
            },
        },
    }


def find_comfy_output_dir(server_url: str) -> Path:
    """Locate ComfyUI's output dir.

    Tries a few common locations; falls back to env var COMFYUI_OUTPUT_DIR.
    """
    env = os.environ.get("COMFYUI_OUTPUT_DIR")
    if env:
        p = Path(env)
        if p.exists():
            return p

    # Common portable layouts (windows + linux/mac)
    candidates = [
        Path("G:/ai/ComfyUI_windows_portable/ComfyUI/output"),
        Path.home() / "ComfyUI" / "output",
        Path.home() / "ai" / "ComfyUI" / "output",
        Path("/opt/ComfyUI/output"),
    ]
    for c in candidates:
        if c.exists():
            return c
    raise FileNotFoundError(
        "ComfyUI output dir not found; set COMFYUI_OUTPUT_DIR env var"
    )


def run_one(
    server: str,
    image_path: Path,
    positive: str,
    negative: str,
    output_path: Path,
    width: int,
    height: int,
    length: int,
    fps: int,
    steps: int,
    seed: int,
    skip_existing: bool,
    timeout_seconds: int,
) -> int:
    if skip_existing and output_path.exists() and output_path.stat().st_size > 1024:
        print(f"[ltx] SKIP {output_path.name} (exists, {output_path.stat().st_size} bytes)")
        return 0

    comfy_out = find_comfy_output_dir(server)
    comfy_in = comfy_out.parent / "input"
    image_name = stage_input_image(comfy_in, image_path)
    prefix = output_path.stem  # filename without .mp4

    workflow = build_workflow(
        image_name=image_name,
        positive=positive,
        negative=negative,
        width=width,
        height=height,
        length=length,
        fps=fps,
        steps=steps,
        seed=seed,
        filename_prefix=prefix,
    )

    client_id = f"ltx_img2vid_{int(time.time())}"
    print(f"[ltx] {image_path.name} -> {output_path.name}  (seed={seed})")
    try:
        result = post_prompt(server, workflow, client_id)
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="ignore")
        print(f"[ltx] ERROR {e.code}: {body[:600]}", file=sys.stderr)
        return 1
    prompt_id = result.get("prompt_id")
    if not prompt_id:
        print(f"[ltx] ERROR: no prompt_id in {result}", file=sys.stderr)
        return 2

    t0 = time.time()
    while True:
        time.sleep(3)
        elapsed = time.time() - t0
        hist = get_history(server, prompt_id)
        if hist and prompt_id in hist:
            entry = hist[prompt_id]
            status = entry.get("status", {})
            if status.get("completed", False):
                status_str = status.get("status_str", "?")
                if status_str != "success":
                    print(f"[ltx] ERROR: status={status_str}", file=sys.stderr)
                    return 3
                outputs = entry.get("outputs", {})
                for _, node_out in outputs.items():
                    # ComfyUI SaveVideo emits under "images" key with .mp4 filenames
                    # (animated=True flag distinguishes from still images).
                    for item in node_out.get("images", []) + node_out.get("videos", []):
                        fname = item.get("filename", "")
                        if not fname.lower().endswith((".mp4", ".webm", ".mov", ".gif")):
                            continue
                        comfy_file = comfy_out / item.get("subfolder", "") / fname
                        if comfy_file.exists():
                            output_path.parent.mkdir(parents=True, exist_ok=True)
                            shutil.move(str(comfy_file), str(output_path))
                            sz = output_path.stat().st_size / 1024
                            print(f"[ltx] DONE  {elapsed:.0f}s  {output_path.name} ({sz:.0f} KB)")
                            return 0
                print(f"[ltx] WARN: no video output found in {outputs}", file=sys.stderr)
                return 4
        if elapsed > timeout_seconds:
            print(f"[ltx] TIMEOUT after {elapsed:.0f}s", file=sys.stderr)
            return 5


def main():
    p = argparse.ArgumentParser(description="LTX-Video img2vid CLI (ComfyUI client)")
    p.add_argument("--image", required=True, type=Path, help="input still image")
    p.add_argument("--prompt", required=True, help="positive prompt (descriptive, 1-3 sentences)")
    p.add_argument("--negative-extra", default="", help="extra negative terms (ai-hands-avoid auto-applied)")
    p.add_argument("--output", required=True, type=Path, help="output mp4 path")
    p.add_argument("--server", default=DEFAULT_SERVER, help="ComfyUI server URL")
    p.add_argument("--width", type=int, default=768)
    p.add_argument("--height", type=int, default=432)
    p.add_argument("--length", type=int, default=121, help="frames (97 ~= 4s, 121 ~= 5s @ 24fps)")
    p.add_argument("--fps", type=int, default=24)
    p.add_argument("--steps", type=int, default=30)
    p.add_argument("--seed", type=int, default=-1, help="-1 for random")
    p.add_argument("--skip-existing", action="store_true")
    p.add_argument("--timeout", type=int, default=300, help="seconds")
    args = p.parse_args()

    if not args.image.exists():
        print(f"input image not found: {args.image}", file=sys.stderr)
        sys.exit(1)

    seed = args.seed
    if seed < 0:
        import secrets
        seed = secrets.randbits(63)

    negative = DEFAULT_NEGATIVE_BASE + ", " + AI_HANDS_AVOID_NEGATIVE
    if args.negative_extra:
        negative += ", " + args.negative_extra

    rc = run_one(
        server=args.server,
        image_path=args.image,
        positive=args.prompt,
        negative=negative,
        output_path=args.output,
        width=args.width,
        height=args.height,
        length=args.length,
        fps=args.fps,
        steps=args.steps,
        seed=seed,
        skip_existing=args.skip_existing,
        timeout_seconds=args.timeout,
    )
    sys.exit(rc)


if __name__ == "__main__":
    main()
