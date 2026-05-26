"""asset_gen — generate 2D sprites / tiles / icons via ComfyUI + SDXL-Turbo.

Reuses the ComfyUI server already running for Skill #1 (music-video).
Outputs PNG sprites suitable for Unity 2D import.
"""
from __future__ import annotations

import json
import os
import shutil
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path


SDXL_CHECKPOINT = "sd_xl_turbo_1.0_fp16.safetensors"

# Style presets — game-asset specific prompt scaffolds
STYLE_PRESETS = {
    "pixel-art": {
        "prompt_suffix": (
            ", 2D pixel art game sprite, clean silhouette, top-down view, "
            "transparent background, single subject centered, "
            "vibrant flat colors, no shading complexity, retro game style"
        ),
        "negative": (
            "3d, photorealistic, blurry, painterly, complex background, "
            "multiple subjects, text, watermark, signature, frame, ui, "
            "low quality, deformed, hand, hands, fingers, finger"
        ),
        "steps": 6,
        "cfg": 1.5,
    },
    "stylized-2d": {
        "prompt_suffix": (
            ", 2D game art, hand-drawn stylized, clean line work, "
            "vibrant colors, top-down or side view, transparent background, "
            "single subject centered, mobile game art style"
        ),
        "negative": (
            "3d, photorealistic, complex background, multiple subjects, "
            "text, watermark, deformed, low quality, hand, fingers"
        ),
        "steps": 6,
        "cfg": 1.5,
    },
    "icon": {
        "prompt_suffix": (
            ", flat icon design, simple shapes, vibrant solid colors, "
            "centered subject, transparent background, no text, "
            "mobile UI icon style, clean silhouette"
        ),
        "negative": (
            "complex, detailed, photorealistic, text, watermark, blurry, "
            "multiple subjects, frame border, hand, fingers"
        ),
        "steps": 4,
        "cfg": 1.2,
    },
    "raw": {
        "prompt_suffix": "",
        "negative": "low quality, deformed, blurry, text, watermark",
        "steps": 6,
        "cfg": 1.5,
    },
}


def _round_32(n: int) -> int:
    """SDXL latent compat — round up to nearest multiple of 32."""
    if n % 32 == 0:
        return n
    return ((n // 32) + 1) * 32


def _find_comfy_output_dir() -> Path:
    env = os.environ.get("COMFYUI_OUTPUT_DIR")
    if env and Path(env).exists():
        return Path(env)
    candidates = [
        Path("G:/ai/ComfyUI_windows_portable/ComfyUI/output"),
        Path.home() / "ComfyUI" / "output",
    ]
    for c in candidates:
        if c.exists():
            return c
    raise FileNotFoundError("ComfyUI output dir not found; set COMFYUI_OUTPUT_DIR")


def generate_sprite(
    prompt: str,
    output: Path,
    width: int = 512,
    height: int = 512,
    seed: int = -1,
    style: str = "pixel-art",
    server: str = "http://127.0.0.1:8188",
) -> int:
    """Generate a single sprite PNG. Returns 0 on success."""
    if seed < 0:
        import secrets
        seed = secrets.randbits(31)

    preset = STYLE_PRESETS[style]
    full_prompt = prompt + preset["prompt_suffix"]
    negative = preset["negative"]

    w = _round_32(width)
    h = _round_32(height)

    workflow = {
        "1": {"class_type": "CheckpointLoaderSimple",
              "inputs": {"ckpt_name": SDXL_CHECKPOINT}},
        "2": {"class_type": "CLIPTextEncode",
              "inputs": {"clip": ["1", 1], "text": full_prompt}},
        "3": {"class_type": "CLIPTextEncode",
              "inputs": {"clip": ["1", 1], "text": negative}},
        "4": {"class_type": "EmptyLatentImage",
              "inputs": {"width": w, "height": h, "batch_size": 1}},
        "5": {"class_type": "KSampler",
              "inputs": {
                  "model": ["1", 0], "seed": seed,
                  "steps": preset["steps"], "cfg": preset["cfg"],
                  "sampler_name": "euler_ancestral", "scheduler": "normal",
                  "denoise": 1.0, "positive": ["2", 0], "negative": ["3", 0],
                  "latent_image": ["4", 0],
              }},
        "6": {"class_type": "VAEDecode",
              "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
        "7": {"class_type": "SaveImage",
              "inputs": {"images": ["6", 0],
                         "filename_prefix": f"sprite_{output.stem}"}},
    }

    print(f"[asset_gen] prompt: {prompt}")
    print(f"[asset_gen] style={style} {w}x{h} seed={seed}")

    data = json.dumps({"prompt": workflow, "client_id": f"sprite_{seed}"}).encode("utf-8")
    try:
        req = urllib.request.Request(
            f"{server}/prompt", data=data,
            headers={"Content-Type": "application/json"}, method="POST")
        with urllib.request.urlopen(req, timeout=30) as resp:
            result = json.loads(resp.read())
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="ignore")[:600]
        print(f"[asset_gen] ERROR {e.code}: {body}", file=sys.stderr)
        return 1
    except Exception as e:
        print(f"[asset_gen] ERROR: {e}", file=sys.stderr)
        return 2

    prompt_id = result.get("prompt_id")
    if not prompt_id:
        print(f"[asset_gen] no prompt_id in {result}", file=sys.stderr)
        return 3

    t0 = time.time()
    while True:
        time.sleep(2)
        if time.time() - t0 > 60:
            print(f"[asset_gen] TIMEOUT", file=sys.stderr)
            return 4
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
                if entry["status"].get("status_str") != "success":
                    return 5
                comfy_out = _find_comfy_output_dir()
                for _, node_out in entry.get("outputs", {}).items():
                    for img in node_out.get("images", []):
                        fname = img.get("filename", "")
                        if not fname.lower().endswith((".png", ".jpg", ".jpeg")):
                            continue
                        comfy_file = comfy_out / img.get("subfolder", "") / fname
                        if comfy_file.exists():
                            output.parent.mkdir(parents=True, exist_ok=True)
                            shutil.move(str(comfy_file), str(output))
                            sz_kb = output.stat().st_size / 1024
                            elapsed = time.time() - t0
                            print(f"[asset_gen] DONE {elapsed:.1f}s -> {output} ({sz_kb:.0f} KB)")
                            return 0
                return 6
