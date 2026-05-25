#!/usr/bin/env python3
"""
KlingAI text-to-video generator.

Usage:
  kling-generate.py "prompt text" [--out=path.mp4] [--duration=5] [--mode=std]

Submits a generation job, polls until done, downloads mp4.
"""
import argparse
import os
import sys
import time
import json
import jwt
import requests
from pathlib import Path

CREDS_FILE = Path.home() / ".config" / "kling" / "credentials"
BASE_URL = "https://api.klingai.com"

def load_creds():
    creds = {}
    for line in CREDS_FILE.read_text().splitlines():
        if "=" in line:
            k, v = line.split("=", 1)
            creds[k.strip()] = v.strip()
    return creds["KLING_ACCESS_KEY"], creds["KLING_SECRET_KEY"]

def make_jwt(access_key, secret_key):
    now = int(time.time())
    payload = {"iss": access_key, "exp": now + 1800, "nbf": now - 5}
    headers = {"alg": "HS256", "typ": "JWT"}
    return jwt.encode(payload, secret_key, algorithm="HS256", headers=headers)

def submit(prompt, duration=5, mode="std", aspect_ratio="9:16"):
    ak, sk = load_creds()
    token = make_jwt(ak, sk)
    url = f"{BASE_URL}/v1/videos/text2video"
    headers = {"Authorization": f"Bearer {token}", "Content-Type": "application/json"}
    body = {
        "model_name": "kling-v1-6",  # standard model name; can also be kling-v2-master
        "prompt": prompt,
        "duration": str(duration),
        "mode": mode,
        "aspect_ratio": aspect_ratio,
        "cfg_scale": 0.5,
    }
    print(f"[SUBMIT] {json.dumps(body, ensure_ascii=False)}")
    r = requests.post(url, headers=headers, json=body, timeout=30)
    print(f"[HTTP] {r.status_code}")
    if r.status_code != 200:
        print(r.text)
        sys.exit(1)
    data = r.json()
    print(f"[RESP] {json.dumps(data, ensure_ascii=False, indent=2)}")
    if data.get("code") != 0:
        sys.exit(f"submission failed: {data}")
    return data["data"]["task_id"]

def poll(task_id, max_wait=600):
    ak, sk = load_creds()
    url = f"{BASE_URL}/v1/videos/text2video/{task_id}"
    start = time.time()
    while time.time() - start < max_wait:
        token = make_jwt(ak, sk)
        headers = {"Authorization": f"Bearer {token}"}
        r = requests.get(url, headers=headers, timeout=15)
        data = r.json()
        status = data.get("data", {}).get("task_status", "unknown")
        elapsed = int(time.time() - start)
        print(f"[POLL +{elapsed}s] {status}")
        if status == "succeed":
            videos = data["data"]["task_result"]["videos"]
            return videos[0]["url"]
        if status == "failed":
            sys.exit(f"task failed: {data}")
        time.sleep(8)
    sys.exit("timeout waiting for task")

def download(url, out_path):
    print(f"[DOWNLOAD] {url}")
    r = requests.get(url, stream=True, timeout=120)
    with open(out_path, "wb") as f:
        for chunk in r.iter_content(8192):
            f.write(chunk)
    print(f"[SAVED] {out_path} ({os.path.getsize(out_path)} bytes)")
    # Probe + scale to 1080x1920 if needed
    import subprocess
    probe = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v",
                            "-show_entries", "stream=width,height", "-of", "csv=p=0", out_path],
                           capture_output=True, text=True)
    dim = probe.stdout.strip()
    print(f"[PROBE] dimensions={dim}")
    if dim != "1080,1920":
        scaled = out_path.replace(".mp4", "-scaled.mp4")
        cmd = ["ffmpeg", "-y", "-loglevel", "error", "-i", out_path,
               "-vf", "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920",
               "-c:v", "libx264", "-preset", "medium", "-crf", "20",
               "-pix_fmt", "yuv420p", "-an", scaled]
        print(f"[SCALE] {dim} → 1080,1920 (with crop, no stretch)")
        subprocess.run(cmd, check=True)
        os.replace(scaled, out_path)
        # Verify
        probe2 = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v",
                                 "-show_entries", "stream=width,height", "-of", "csv=p=0", out_path],
                                capture_output=True, text=True)
        print(f"[SCALED-PROBE] dimensions={probe2.stdout.strip()}")

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("prompt", help="generation prompt")
    ap.add_argument("--out", default="kling-out.mp4", help="output file path")
    ap.add_argument("--duration", type=int, default=5, choices=[5, 10])
    ap.add_argument("--mode", default="std", choices=["std", "pro"])
    ap.add_argument("--aspect", default="9:16", choices=["16:9", "9:16", "1:1"])
    args = ap.parse_args()

    task_id = submit(args.prompt, args.duration, args.mode, args.aspect)
    print(f"[TASK] {task_id}")
    url = poll(task_id)
    download(url, args.out)

if __name__ == "__main__":
    main()
