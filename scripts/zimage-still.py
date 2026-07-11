#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Z-Image Turbo 스틸 생성 (ComfyUI API) — FLUX 대체 후보 A/B용.
공식 템플릿 그래프: UNETLoader(int8) + CLIPLoader(qwen3, type=lumina2) + ModelSamplingAuraFlow(shift3)
 + KSampler(8step, cfg1, res_multistep/simple) + EmptySD3Latent + ConditioningZeroOut(neg) + VAE(z_image_ae).
사용: python zimage_still.py --prompt "..." --output out.png [--width 768 --height 1344 --seed N]"""
import argparse, json, sys, time, urllib.request, pathlib

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--prompt", required=True)
    p.add_argument("--output", required=True, type=pathlib.Path)
    p.add_argument("--server", default="http://127.0.0.1:8188")
    p.add_argument("--width", type=int, default=768)
    p.add_argument("--height", type=int, default=1344)
    p.add_argument("--steps", type=int, default=8)
    p.add_argument("--seed", type=int, default=1234)
    p.add_argument("--unet", default="z_image_turbo_bf16.safetensors")
    p.add_argument("--timeout", type=int, default=600)
    a = p.parse_args()
    g = {
        "u":  {"class_type": "UNETLoader", "inputs": {"unet_name": a.unet, "weight_dtype": "default"}},
        "c":  {"class_type": "CLIPLoader", "inputs": {"clip_name": "qwen_3_4b_fp8_mixed.safetensors", "type": "lumina2"}},
        "v":  {"class_type": "VAELoader", "inputs": {"vae_name": "z_image_ae.safetensors"}},
        "ms": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["u", 0], "shift": 3.0}},
        "tp": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["c", 0], "text": a.prompt}},
        "tn": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["c", 0], "text": ""}},
        "zn": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["tn", 0]}},
        "lat":{"class_type": "EmptySD3LatentImage", "inputs": {"width": a.width, "height": a.height, "batch_size": 1}},
        "k":  {"class_type": "KSampler", "inputs": {"model": ["ms", 0], "positive": ["tp", 0], "negative": ["zn", 0],
               "latent_image": ["lat", 0], "seed": a.seed, "steps": a.steps, "cfg": 1.0,
               "sampler_name": "res_multistep", "scheduler": "simple", "denoise": 1.0}},
        "d":  {"class_type": "VAEDecode", "inputs": {"samples": ["k", 0], "vae": ["v", 0]}},
        "sv": {"class_type": "SaveImage", "inputs": {"images": ["d", 0], "filename_prefix": "zimage"}},
    }
    req = urllib.request.Request(f"{a.server}/prompt", data=json.dumps({"prompt": g}).encode(),
                                 headers={"Content-Type": "application/json"})
    try:
        pid = json.loads(urllib.request.urlopen(req).read())["prompt_id"]
    except urllib.error.HTTPError as e:
        print("[zimage] SUBMIT ERROR:", e.read().decode()[:600], file=sys.stderr); return 1
    t0 = time.time()
    print(f"[zimage] gen -> {a.output.name} (seed={a.seed}, {a.width}x{a.height}, {a.steps}step)")
    while time.time() - t0 < a.timeout:
        time.sleep(2)
        h = json.loads(urllib.request.urlopen(f"{a.server}/history/{pid}").read())
        if pid in h and h[pid].get("outputs"):
            for out in h[pid]["outputs"].values():
                if "images" in out:
                    f = out["images"][0]
                    url = f"{a.server}/view?filename={f['filename']}&subfolder={f.get('subfolder','')}&type={f.get('type','output')}"
                    a.output.parent.mkdir(parents=True, exist_ok=True)
                    a.output.write_bytes(urllib.request.urlopen(url).read())
                    print(f"[zimage] DONE {time.time()-t0:.0f}s  {a.output} ({a.output.stat().st_size//1024} KB)")
                    return 0
        if pid in h and h[pid].get("status", {}).get("status_str") == "error":
            print("[zimage] ERROR:", json.dumps(h[pid]["status"])[:600], file=sys.stderr); return 1
    print("[zimage] timeout", file=sys.stderr); return 1

if __name__ == "__main__":
    sys.exit(main())
