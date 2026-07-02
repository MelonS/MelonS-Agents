#!/usr/bin/env python3
"""wan-i2v.py — image → motion clip (or text → video) via Wan 2.2 TI2V-5B on ComfyUI.

Successor to ltx-img2vid.py for hero/B-roll generation.  Field-validated
(2026-07) against LTX-Video 0.9.5 on the same shots: Wan 2.2 5B produces
markedly better fluid/texture motion and cleaner product shots, and its
CFG-controlled sampling actually honors negative prompts (hands/person
suppression works, unlike distilled cfg=1 pipelines).

Key production learnings baked into the defaults:
  - I2V anchor grounding: feeding a real photo as --image keeps anatomy,
    labels and layout stable while the prompt adds motion.  Prefer this
    over pure T2V whenever a usable still exists.
  - Hybrid principle: pump nozzles, product labels, close-up faces and
    finger-heavy shots are still safer as real footage; use generation for
    fluid/texture/ambience motion.
  - 97 frames @ 24 fps ≈ 4 s; ~6 min on an RTX 4070 Ti SUPER (16 GB) at
    704x1280 with 30 steps.

Cross-platform (mac / linux / windows).  Calls a ComfyUI server's REST API.
Default server: http://127.0.0.1:8188 (override with COMFYUI_URL or --server).

Usage:
  # image → video (recommended)
  python scripts/wan-i2v.py --image stills/01.jpg --prompt "..." --output clips/01.mp4
  # text → video (no anchor still available)
  python scripts/wan-i2v.py --prompt "..." --output clips/02.mp4

Requires:
  - ComfyUI >= 0.22 running locally (core nodes only, no custom nodes)
  - Models installed in ComfyUI/models/:
      diffusion_models/wan2.2_ti2v_5B_fp16.safetensors
      text_encoders/umt5_xxl_fp8_e4m3fn_scaled.safetensors
      vae/wan2.2_vae.safetensors

Note: the heavier Wan 2.2 A14B (GGUF + Lightning LoRA) gives stronger
human/action motion but ignores negatives (cfg=1) and needs the ComfyUI-GGUF
custom node — intentionally out of scope here.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

DEFAULT_SERVER = os.environ.get("COMFYUI_URL", "http://127.0.0.1:8188")

# Negative prompt baked in — applies the [[ai-hands-avoid]] rule globally,
# same policy as ltx-img2vid.py.
AI_HANDS_AVOID_NEGATIVE = (
    "hand, hands, fingers, reaching, grasping, holding object, "
    "playing instrument, writing, typing, "
    "deformed hand, weird hand, fused fingers, six fingers, "
    "extra fingers, twisted joint, mangled limb"
)

# Wan 2.2 responds well to its official Chinese negative set; keep it plus
# the usual English quality terms.
DEFAULT_NEGATIVE_BASE = (
    "色调艳丽, 过曝, 静态, 细节模糊不清, 字幕, 风格, 作品, 画作, 画面, 静止, "
    "整体发灰, 最差质量, 低质量, 抖动, 变形, 扭曲, "
    "watermark, text, signature, blurry, distorted, deformed, jitter, flickering"
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
    """Copy the input image into ComfyUI's input/ dir so LoadImage finds it."""
    server_input_dir.mkdir(parents=True, exist_ok=True)
    dest = server_input_dir / image_path.name
    if dest.exists() and dest.stat().st_size == image_path.stat().st_size:
        return image_path.name
    shutil.copy2(image_path, dest)
    return image_path.name


def build_workflow(
    image_name: str | None,
    positive: str,
    negative: str,
    width: int,
    height: int,
    length: int,
    fps: int,
    steps: int,
    cfg: float,
    shift: float,
    seed: int,
    filename_prefix: str,
    unet: str,
    clip: str,
    vae: str,
) -> dict:
    """API-format workflow for Wan 2.2 TI2V-5B (core nodes only).

    Validated against ComfyUI v0.22.0.  start_image on
    Wan22ImageToVideoLatent is optional: present = I2V, absent = T2V.
    """
    wf: dict = {
        "1": {"class_type": "UNETLoader",
              "inputs": {"unet_name": unet, "weight_dtype": "default"}},
        "2": {"class_type": "CLIPLoader",
              "inputs": {"clip_name": clip, "type": "wan"}},
        "3": {"class_type": "VAELoader", "inputs": {"vae_name": vae}},
        "4": {"class_type": "CLIPTextEncode",
              "inputs": {"text": positive, "clip": ["2", 0]}},
        "5": {"class_type": "CLIPTextEncode",
              "inputs": {"text": negative, "clip": ["2", 0]}},
        "7": {"class_type": "Wan22ImageToVideoLatent",
              "inputs": {"vae": ["3", 0], "width": width, "height": height,
                         "length": length, "batch_size": 1}},
        "8": {"class_type": "ModelSamplingSD3",
              "inputs": {"model": ["1", 0], "shift": shift}},
        "9": {"class_type": "KSampler",
              "inputs": {"model": ["8", 0], "positive": ["4", 0],
                         "negative": ["5", 0], "latent_image": ["7", 0],
                         "seed": seed, "steps": steps, "cfg": cfg,
                         "sampler_name": "euler", "scheduler": "simple",
                         "denoise": 1.0}},
        "10": {"class_type": "VAEDecode",
               "inputs": {"samples": ["9", 0], "vae": ["3", 0]}},
        "11": {"class_type": "CreateVideo",
               "inputs": {"images": ["10", 0], "fps": fps}},
        "12": {"class_type": "SaveVideo",
               "inputs": {"video": ["11", 0], "filename_prefix": filename_prefix,
                          "format": "mp4", "codec": "h264"}},
    }
    if image_name:  # I2V: add LoadImage + wire start_image
        wf["6"] = {"class_type": "LoadImage", "inputs": {"image": image_name}}
        wf["7"]["inputs"]["start_image"] = ["6", 0]
    return wf


def find_comfy_output_dir(server_url: str) -> Path:
    """Locate ComfyUI's output dir (env COMFYUI_OUTPUT_DIR wins)."""
    env = os.environ.get("COMFYUI_OUTPUT_DIR")
    if env:
        p = Path(env)
        if p.exists():
            return p
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
    image_path: Path | None,
    positive: str,
    negative: str,
    output_path: Path,
    width: int,
    height: int,
    length: int,
    fps: int,
    steps: int,
    cfg: float,
    shift: float,
    seed: int,
    skip_existing: bool,
    timeout_seconds: int,
    unet: str,
    clip: str,
    vae: str,
) -> int:
    if skip_existing and output_path.exists() and output_path.stat().st_size > 1024:
        print(f"[wan] SKIP {output_path.name} (exists)")
        return 0

    comfy_out = find_comfy_output_dir(server)
    image_name = None
    if image_path:
        comfy_in = comfy_out.parent / "input"
        image_name = stage_input_image(comfy_in, image_path)

    workflow = build_workflow(
        image_name=image_name, positive=positive, negative=negative,
        width=width, height=height, length=length, fps=fps, steps=steps,
        cfg=cfg, shift=shift, seed=seed, filename_prefix=output_path.stem,
        unet=unet, clip=clip, vae=vae,
    )

    mode = "i2v" if image_name else "t2v"
    client_id = f"wan_i2v_{int(time.time())}"
    src = image_path.name if image_path else "(text only)"
    print(f"[wan] {mode} {src} -> {output_path.name}  (seed={seed}, {width}x{height}x{length}f)")
    try:
        result = post_prompt(server, workflow, client_id)
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="ignore")
        print(f"[wan] ERROR {e.code}: {body[:600]}", file=sys.stderr)
        return 1
    prompt_id = result.get("prompt_id")
    if not prompt_id:
        print(f"[wan] ERROR: no prompt_id in {result}", file=sys.stderr)
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
                if status.get("status_str", "?") != "success":
                    print(f"[wan] ERROR: status={status.get('status_str')}", file=sys.stderr)
                    return 3
                outputs = entry.get("outputs", {})
                for _, node_out in outputs.items():
                    # SaveVideo emits under "images" (animated) or "videos"
                    for item in node_out.get("images", []) + node_out.get("videos", []):
                        fname = item.get("filename", "")
                        if not fname.lower().endswith((".mp4", ".webm", ".mov")):
                            continue
                        comfy_file = comfy_out / item.get("subfolder", "") / fname
                        if comfy_file.exists():
                            output_path.parent.mkdir(parents=True, exist_ok=True)
                            shutil.move(str(comfy_file), str(output_path))
                            sz = output_path.stat().st_size / 1024
                            print(f"[wan] DONE  {elapsed:.0f}s  {output_path.name} ({sz:.0f} KB)")
                            return 0
                print(f"[wan] WARN: no video output found in {outputs}", file=sys.stderr)
                return 4
        if elapsed > timeout_seconds:
            print(f"[wan] TIMEOUT after {elapsed:.0f}s", file=sys.stderr)
            return 5


def main():
    p = argparse.ArgumentParser(description="Wan 2.2 TI2V-5B img2vid / txt2vid CLI (ComfyUI client)")
    p.add_argument("--image", type=Path, default=None,
                   help="input still image (omit for text-to-video)")
    p.add_argument("--prompt", required=True, help="positive prompt (motion-focused, 1-3 sentences)")
    p.add_argument("--negative-extra", default="", help="extra negative terms (ai-hands-avoid auto-applied)")
    p.add_argument("--output", required=True, type=Path, help="output mp4 path")
    p.add_argument("--server", default=DEFAULT_SERVER, help="ComfyUI server URL")
    p.add_argument("--width", type=int, default=704)
    p.add_argument("--height", type=int, default=1280, help="704x1280 = 9:16 shorts default")
    p.add_argument("--length", type=int, default=97, help="frames (97 ~= 4s @ 24fps)")
    p.add_argument("--fps", type=int, default=24)
    p.add_argument("--steps", type=int, default=30)
    p.add_argument("--cfg", type=float, default=5.0, help="5.0 honors negatives; lower = looser")
    p.add_argument("--shift", type=float, default=8.0, help="ModelSamplingSD3 shift")
    p.add_argument("--seed", type=int, default=-1, help="-1 for random")
    p.add_argument("--unet", default="wan2.2_ti2v_5B_fp16.safetensors")
    p.add_argument("--clip", default="umt5_xxl_fp8_e4m3fn_scaled.safetensors")
    p.add_argument("--vae", default="wan2.2_vae.safetensors")
    p.add_argument("--allow-hands", action="store_true",
                   help="drop the ai-hands-avoid negative (e.g., grounded real-hand anchors)")
    p.add_argument("--skip-existing", action="store_true")
    p.add_argument("--timeout", type=int, default=1500, help="seconds (~6min warm at defaults; first run after ComfyUI start adds ~5min model load)")
    args = p.parse_args()

    if args.image and not args.image.exists():
        print(f"input image not found: {args.image}", file=sys.stderr)
        sys.exit(1)

    seed = args.seed
    if seed < 0:
        import secrets
        seed = secrets.randbits(63)

    negative = DEFAULT_NEGATIVE_BASE
    if not args.allow_hands:
        negative += ", " + AI_HANDS_AVOID_NEGATIVE
    if args.negative_extra:
        negative += ", " + args.negative_extra

    rc = run_one(
        server=args.server, image_path=args.image, positive=args.prompt,
        negative=negative, output_path=args.output,
        width=args.width, height=args.height, length=args.length,
        fps=args.fps, steps=args.steps, cfg=args.cfg, shift=args.shift,
        seed=seed, skip_existing=args.skip_existing,
        timeout_seconds=args.timeout,
        unet=args.unet, clip=args.clip, vae=args.vae,
    )
    sys.exit(rc)


if __name__ == "__main__":
    main()
