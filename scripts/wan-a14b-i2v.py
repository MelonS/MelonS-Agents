#!/usr/bin/env python3
"""wan-a14b-i2v.py — image → motion clip via Wan 2.2 I2V A14B (GGUF) + Lightning.

The high-motion sibling of wan-i2v.py (5B): A14B gives far stronger subject/
camera motion — the fix for "slow-motion everything" feedback — at the cost of
negative prompts (cfg=1 distilled: negatives ignored) and weaker label/text
control (docs/wan22-generation-notes.md §1). I2V anchor required (T2V collapses).

Two-stage sampling per the Wan 2.2 A14B recipe: HighNoise expert (steps 0-2)
→ LowNoise expert (steps 2-4), each with its Lightning 4-step LoRA, cfg 1.0.
Uses the GGUF Q4_K_M quants + wan_2.1 VAE already in ComfyUI/models.

Usage:
  python3 scripts/wan-a14b-i2v.py --image anchor.png --prompt "..." --output out.mp4
  [--width 704 --height 1280] [--length 81] [--fps 16] [--seed -1]
  [--strength 1.0]   # denoise; 0.6-0.7 preserves the anchor harder (less drift)
"""
from __future__ import annotations
import argparse, json, random, sys, time, urllib.request, uuid
from pathlib import Path

def upload_image(server: str, path: Path) -> str:
    boundary = uuid.uuid4().hex
    body = (
        f"--{boundary}\r\nContent-Disposition: form-data; name=\"image\"; "
        f"filename=\"{path.name}\"\r\nContent-Type: application/octet-stream\r\n\r\n"
    ).encode() + path.read_bytes() + f"\r\n--{boundary}--\r\n".encode()
    req = urllib.request.Request(f"{server}/upload/image", data=body,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})
    return json.loads(urllib.request.urlopen(req).read())["name"]

def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--image", required=True, type=Path, help="anchor still (required — A14B T2V collapses)")
    p.add_argument("--prompt", required=True)
    p.add_argument("--output", required=True, type=Path)
    p.add_argument("--server", default="http://127.0.0.1:8188")
    p.add_argument("--width", type=int, default=704)
    p.add_argument("--height", type=int, default=1280)
    p.add_argument("--length", type=int, default=81, help="frames (81 ~= 5s @ 16fps)")
    p.add_argument("--fps", type=int, default=16)
    p.add_argument("--seed", type=int, default=-1)
    p.add_argument("--steps", type=int, default=4, help="lightning total steps")
    p.add_argument("--boundary-step", type=int, default=2, help="high->low handoff step")
    p.add_argument("--strength", type=float, default=1.0, help="denoise (<1 = preserve anchor harder)")
    p.add_argument("--timeout", type=int, default=2400)
    a = p.parse_args()
    seed = a.seed if a.seed >= 0 else random.randint(0, 2**31)

    img = upload_image(a.server, a.image)
    g = {
        # experts
        "h1": {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": "Wan2.2-I2V-A14B-HighNoise-Q4_K_M.gguf"}},
        "h2": {"class_type": "LoraLoaderModelOnly",
               "inputs": {"model": ["h1", 0], "lora_name": "wan22_i2v_high_lightning.safetensors", "strength_model": 1.0}},
        "h3": {"class_type": "ModelSamplingSD3", "inputs": {"model": ["h2", 0], "shift": 8.0}},
        "l1": {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": "Wan2.2-I2V-A14B-LowNoise-Q4_K_M.gguf"}},
        "l2": {"class_type": "LoraLoaderModelOnly",
               "inputs": {"model": ["l1", 0], "lora_name": "wan22_i2v_low_lightning.safetensors", "strength_model": 1.0}},
        "l3": {"class_type": "ModelSamplingSD3", "inputs": {"model": ["l2", 0], "shift": 8.0}},
        # shared
        "c":  {"class_type": "CLIPLoader", "inputs": {"clip_name": "umt5_xxl_fp8_e4m3fn_scaled.safetensors", "type": "wan"}},
        "v":  {"class_type": "VAELoader", "inputs": {"vae_name": "wan_2.1_vae.safetensors"}},
        "tp": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["c", 0], "text": a.prompt}},
        "tn": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["c", 0], "text": ""}},
        "im": {"class_type": "LoadImage", "inputs": {"image": img}},
        "lat": {"class_type": "WanImageToVideo",
                "inputs": {"positive": ["tp", 0], "negative": ["tn", 0], "vae": ["v", 0],
                           "width": a.width, "height": a.height, "length": a.length,
                           "batch_size": 1, "start_image": ["im", 0]}},
        # two-stage lightning
        "k1": {"class_type": "KSamplerAdvanced",
               "inputs": {"model": ["h3", 0], "add_noise": "enable", "noise_seed": seed,
                          "steps": a.steps, "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple",
                          "positive": ["lat", 0], "negative": ["lat", 1], "latent_image": ["lat", 2],
                          "start_at_step": 0, "end_at_step": a.boundary_step,
                          "return_with_leftover_noise": "enable"}},
        "k2": {"class_type": "KSamplerAdvanced",
               "inputs": {"model": ["l3", 0], "add_noise": "disable", "noise_seed": seed,
                          "steps": a.steps, "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple",
                          "positive": ["lat", 0], "negative": ["lat", 1], "latent_image": ["k1", 0],
                          "start_at_step": a.boundary_step, "end_at_step": 10000,
                          "return_with_leftover_noise": "disable"}},
        "d":  {"class_type": "VAEDecode", "inputs": {"samples": ["k2", 0], "vae": ["v", 0]}},
        "cv": {"class_type": "CreateVideo", "inputs": {"images": ["d", 0], "fps": a.fps}},
        "sv": {"class_type": "SaveVideo",
               "inputs": {"video": ["cv", 0], "filename_prefix": "a14b", "format": "mp4", "codec": "h264"}},
    }
    req = urllib.request.Request(f"{a.server}/prompt",
        data=json.dumps({"prompt": g}).encode(), headers={"Content-Type": "application/json"})
    pid = json.loads(urllib.request.urlopen(req).read())["prompt_id"]
    t0 = time.time()
    print(f"[a14b] i2v -> {a.output.name} (seed={seed}, {a.width}x{a.height}x{a.length}f)")
    while time.time() - t0 < a.timeout:
        time.sleep(3)
        h = json.loads(urllib.request.urlopen(f"{a.server}/history/{pid}").read())
        if pid in h and h[pid].get("outputs"):
            for out in h[pid]["outputs"].values():
                for key in ("images", "video", "videos", "gifs"):
                    if key in out:
                        f = out[key][0]
                        url = (f"{a.server}/view?filename={f['filename']}"
                               f"&subfolder={f.get('subfolder','')}&type={f.get('type','output')}")
                        a.output.parent.mkdir(parents=True, exist_ok=True)
                        a.output.write_bytes(urllib.request.urlopen(url).read())
                        kb = a.output.stat().st_size // 1024
                        print(f"[a14b] DONE {time.time()-t0:.0f}s  {a.output} ({kb} KB)")
                        return 0
        if pid in h and h[pid].get("status", {}).get("status_str") == "error":
            print("[a14b] ERROR:", json.dumps(h[pid]["status"])[:500], file=sys.stderr)
            return 1
    print("[a14b] timeout", file=sys.stderr)
    return 1

if __name__ == "__main__":
    sys.exit(main())
