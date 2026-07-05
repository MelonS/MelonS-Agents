#!/usr/bin/env python3
"""esrgan-upscale.py — upscale an image via ComfyUI's RealESRGAN_x4plus.

Keeps composition pixel-identical (unlike re-generating at higher res, which
re-rolls the layout — see docs/wan22-generation-notes.md §4). Used to sharpen
approved storyboard anchor stills before I2V / sheet display.

Usage: python3 scripts/esrgan-upscale.py --image in.jpg --output out.png
       [--server http://127.0.0.1:8188] [--model RealESRGAN_x4plus.pth]
"""
from __future__ import annotations
import argparse, json, sys, time, urllib.request, uuid
from pathlib import Path

def post_image(server: str, path: Path) -> str:
    boundary = uuid.uuid4().hex
    data = path.read_bytes()
    body = (
        f"--{boundary}\r\nContent-Disposition: form-data; name=\"image\"; "
        f"filename=\"{path.name}\"\r\nContent-Type: application/octet-stream\r\n\r\n"
    ).encode() + data + f"\r\n--{boundary}--\r\n".encode()
    req = urllib.request.Request(
        f"{server}/upload/image", data=body,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})
    return json.loads(urllib.request.urlopen(req).read())["name"]

def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--image", required=True, type=Path)
    p.add_argument("--output", required=True, type=Path)
    p.add_argument("--server", default="http://127.0.0.1:8188")
    p.add_argument("--model", default="RealESRGAN_x4plus.pth")
    a = p.parse_args()

    name = post_image(a.server, a.image)
    graph = {
        "1": {"class_type": "UpscaleModelLoader", "inputs": {"model_name": a.model}},
        "2": {"class_type": "LoadImage", "inputs": {"image": name}},
        "3": {"class_type": "ImageUpscaleWithModel",
              "inputs": {"upscale_model": ["1", 0], "image": ["2", 0]}},
        "4": {"class_type": "SaveImage",
              "inputs": {"images": ["3", 0], "filename_prefix": "esrgan"}},
    }
    req = urllib.request.Request(f"{a.server}/prompt",
        data=json.dumps({"prompt": graph}).encode(),
        headers={"Content-Type": "application/json"})
    pid = json.loads(urllib.request.urlopen(req).read())["prompt_id"]

    for _ in range(120):
        time.sleep(1)
        hist = json.loads(urllib.request.urlopen(f"{a.server}/history/{pid}").read())
        if pid in hist and hist[pid].get("outputs"):
            img = hist[pid]["outputs"]["4"]["images"][0]
            url = f"{a.server}/view?filename={img['filename']}&subfolder={img.get('subfolder','')}&type={img.get('type','output')}"
            a.output.parent.mkdir(parents=True, exist_ok=True)
            a.output.write_bytes(urllib.request.urlopen(url).read())
            print(f"[esrgan] {a.image.name} -> {a.output} ({a.output.stat().st_size//1024} KB)")
            return 0
    print("[esrgan] timeout", file=sys.stderr)
    return 1

if __name__ == "__main__":
    sys.exit(main())
