#!/usr/bin/env python3
"""flux-still.py — text → still image via FLUX.1-schnell fp8 on ComfyUI.

Anchor-still generator for the image-first I2V pipeline (master-note
workflow): Flux nails prompt adherence + detail (nine-tails test beat both
Wan-frame and SDXL-Turbo stills), then wan-i2v.py --image animates the
approved anchor with its composition and style locked.

schnell = Apache 2.0 (commercial-safe for the monetized channel; dev is
non-commercial — do not swap it in without a license check).

Usage: python3 scripts/flux-still.py --prompt "..." --output out.png
       [--width 768 --height 1344] [--steps 4] [--seed -1]
       [--server http://127.0.0.1:8188]
"""
from __future__ import annotations
import argparse, json, random, sys, time, urllib.request
from pathlib import Path

def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--prompt", required=True)
    p.add_argument("--output", required=True, type=Path)
    p.add_argument("--width", type=int, default=768)
    p.add_argument("--height", type=int, default=1344, help="768x1344 = 9:16, ~1MP flux sweet spot")
    p.add_argument("--steps", type=int, default=4, help="schnell is 4-step distilled")
    p.add_argument("--seed", type=int, default=-1)
    p.add_argument("--server", default="http://127.0.0.1:8188")
    p.add_argument("--ckpt", default="flux1-schnell-fp8.safetensors")
    p.add_argument("--lora", default=None, help="optional LoRA filename in models/loras")
    p.add_argument("--lora-strength", type=float, default=1.0)
    a = p.parse_args()
    seed = a.seed if a.seed >= 0 else random.randint(0, 2**31)

    graph = {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": a.ckpt}},
        "2": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["1", 1], "text": a.prompt}},
        "3": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["1", 1], "text": ""}},
        "4": {"class_type": "EmptySD3LatentImage",
              "inputs": {"width": a.width, "height": a.height, "batch_size": 1}},
        "5": {"class_type": "KSampler",
              "inputs": {"model": ["1", 0], "positive": ["2", 0], "negative": ["3", 0],
                         "latent_image": ["4", 0], "seed": seed, "steps": a.steps,
                         "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple",
                         "denoise": 1.0}},
        "6": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
        "7": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": "flux_still"}},
    }
    if a.lora:
        graph["9"] = {"class_type": "LoraLoaderModelOnly",
                      "inputs": {"model": ["1", 0], "lora_name": a.lora,
                                 "strength_model": a.lora_strength}}
        graph["5"]["inputs"]["model"] = ["9", 0]
    req = urllib.request.Request(f"{a.server}/prompt",
        data=json.dumps({"prompt": graph}).encode(),
        headers={"Content-Type": "application/json"})
    pid = json.loads(urllib.request.urlopen(req).read())["prompt_id"]
    t0 = time.time()
    for _ in range(300):
        time.sleep(1)
        h = json.loads(urllib.request.urlopen(f"{a.server}/history/{pid}").read())
        if pid in h and h[pid].get("outputs"):
            img = h[pid]["outputs"]["7"]["images"][0]
            url = (f"{a.server}/view?filename={img['filename']}"
                   f"&subfolder={img.get('subfolder','')}&type={img.get('type','output')}")
            a.output.parent.mkdir(parents=True, exist_ok=True)
            a.output.write_bytes(urllib.request.urlopen(url).read())
            print(f"[flux] DONE {time.time()-t0:.0f}s seed={seed} -> {a.output}")
            return 0
        if pid in h and h[pid].get("status", {}).get("status_str") == "error":
            print("[flux] ERROR:", json.dumps(h[pid]["status"])[:300], file=sys.stderr)
            return 1
    print("[flux] timeout", file=sys.stderr)
    return 1

if __name__ == "__main__":
    sys.exit(main())
