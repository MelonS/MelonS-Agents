#!/usr/bin/env python3
"""wan-flf2v.py — first frame + last frame → interpolated video (Wan 2.1 FLF2V 14B GGUF).

The Seedance-style control our storyboard END column exists for: give the
start AND end stills and the model synthesizes the transition. No official
lightning distill exists for FLF2V, so:
  - baseline: full sampling (default 24 steps, cfg 5.5) — slow (~20 min/cut)
  - --lightx2v: bolt on the Wan2.1 lightx2v step-distill LoRA (community hack,
    unofficial for FLF2V) — 6 steps, cfg 1.0 — validate quality before trusting.

Usage:
  python3 scripts/wan-flf2v.py --start a.png --end b.png --prompt "..." --output out.mp4
  [--lightx2v] [--steps N] [--cfg F] [--width 704 --height 1280] [--length 81] [--seed -1]
"""
from __future__ import annotations
import argparse, json, random, sys, time, urllib.request, uuid
from pathlib import Path

def upload(server: str, path: Path) -> str:
    b = uuid.uuid4().hex
    body = (f"--{b}\r\nContent-Disposition: form-data; name=\"image\"; "
            f"filename=\"{path.name}\"\r\nContent-Type: application/octet-stream\r\n\r\n"
            ).encode() + path.read_bytes() + f"\r\n--{b}--\r\n".encode()
    req = urllib.request.Request(f"{server}/upload/image", data=body,
        headers={"Content-Type": f"multipart/form-data; boundary={b}"})
    return json.loads(urllib.request.urlopen(req).read())["name"]

def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--start", required=True, type=Path)
    p.add_argument("--end", required=True, type=Path)
    p.add_argument("--prompt", required=True)
    p.add_argument("--output", required=True, type=Path)
    p.add_argument("--server", default="http://127.0.0.1:8188")
    p.add_argument("--width", type=int, default=704)
    p.add_argument("--height", type=int, default=1280)
    p.add_argument("--length", type=int, default=81)
    p.add_argument("--fps", type=int, default=16)
    p.add_argument("--seed", type=int, default=-1)
    p.add_argument("--lightx2v", action="store_true", help="bolt on step-distill LoRA (hack)")
    p.add_argument("--steps", type=int, default=None)
    p.add_argument("--cfg", type=float, default=None)
    p.add_argument("--timeout", type=int, default=3600)
    a = p.parse_args()
    seed = a.seed if a.seed >= 0 else random.randint(0, 2**31)
    steps = a.steps if a.steps else (6 if a.lightx2v else 24)
    cfg = a.cfg if a.cfg is not None else (1.0 if a.lightx2v else 5.5)

    s_img, e_img = upload(a.server, a.start), upload(a.server, a.end)
    g = {
        "u": {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": "wan2.1-flf2v-14b-720p-Q4_K_M.gguf"}},
        "c": {"class_type": "CLIPLoader", "inputs": {"clip_name": "umt5_xxl_fp8_e4m3fn_scaled.safetensors", "type": "wan"}},
        "v": {"class_type": "VAELoader", "inputs": {"vae_name": "wan_2.1_vae.safetensors"}},
        "cv": {"class_type": "CLIPVisionLoader", "inputs": {"clip_name": "clip_vision_h.safetensors"}},
        "si": {"class_type": "LoadImage", "inputs": {"image": s_img}},
        "ei": {"class_type": "LoadImage", "inputs": {"image": e_img}},
        "cvs": {"class_type": "CLIPVisionEncode", "inputs": {"clip_vision": ["cv", 0], "image": ["si", 0], "crop": "none"}},
        "cve": {"class_type": "CLIPVisionEncode", "inputs": {"clip_vision": ["cv", 0], "image": ["ei", 0], "crop": "none"}},
        "tp": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["c", 0], "text": a.prompt}},
        "tn": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["c", 0], "text": ""}},
        "lat": {"class_type": "WanFirstLastFrameToVideo",
                "inputs": {"positive": ["tp", 0], "negative": ["tn", 0], "vae": ["v", 0],
                           "width": a.width, "height": a.height, "length": a.length, "batch_size": 1,
                           "clip_vision_start_image": ["cvs", 0], "clip_vision_end_image": ["cve", 0],
                           "start_image": ["si", 0], "end_image": ["ei", 0]}},
        "ms": {"class_type": "ModelSamplingSD3", "inputs": {"model": ["u", 0], "shift": 8.0}},
        "k": {"class_type": "KSampler",
              "inputs": {"model": ["ms", 0], "positive": ["lat", 0], "negative": ["lat", 1],
                         "latent_image": ["lat", 2], "seed": seed, "steps": steps, "cfg": cfg,
                         "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
        "d": {"class_type": "VAEDecode", "inputs": {"samples": ["k", 0], "vae": ["v", 0]}},
        "mk": {"class_type": "CreateVideo", "inputs": {"images": ["d", 0], "fps": a.fps}},
        "sv": {"class_type": "SaveVideo",
               "inputs": {"video": ["mk", 0], "filename_prefix": "flf2v", "format": "mp4", "codec": "h264"}},
    }
    if a.lightx2v:
        g["lo"] = {"class_type": "LoraLoaderModelOnly",
                   "inputs": {"model": ["u", 0], "lora_name": "Wan21_lightx2v_step_distill_rank32.safetensors",
                              "strength_model": 1.0}}
        g["ms"]["inputs"]["model"] = ["lo", 0]

    req = urllib.request.Request(f"{a.server}/prompt",
        data=json.dumps({"prompt": g}).encode(), headers={"Content-Type": "application/json"})
    pid = json.loads(urllib.request.urlopen(req).read())["prompt_id"]
    t0 = time.time()
    mode = "lightx2v-hack" if a.lightx2v else "baseline"
    print(f"[flf2v:{mode}] steps={steps} cfg={cfg} seed={seed} -> {a.output.name}")
    while time.time() - t0 < a.timeout:
        time.sleep(4)
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
                        print(f"[flf2v:{mode}] DONE {time.time()-t0:.0f}s ({a.output.stat().st_size//1024} KB)")
                        return 0
        if pid in h and h[pid].get("status", {}).get("status_str") == "error":
            print("[flf2v] ERROR:", json.dumps(h[pid]["status"])[:500], file=sys.stderr)
            return 1
    print("[flf2v] timeout", file=sys.stderr)
    return 1

if __name__ == "__main__":
    sys.exit(main())
