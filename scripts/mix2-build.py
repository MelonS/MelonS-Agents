#!/usr/bin/env python3
"""mix2-build.py — Mix #2 orchestrator.

Reads segments.json, generates a still image per segment via Pollinations
(or local SDXL via ComfyUI if --image-backend comfyui), turns each still
into a motion clip via ltx-img2vid.py, then composes everything with
ffmpeg (xfade transitions + audio mux + NVENC encode).

Resumable: each step checks if its output exists and skips if so.
Designed for overnight unattended operation.

Cross-platform.  Uses env vars:
  FFMPEG_BIN     — ffmpeg path (default: ffmpeg on PATH)
  COMFYUI_URL    — ComfyUI server URL (default: http://127.0.0.1:8188)
  RECORDS_DIR    — output base (default: ./records)

Usage:
  python scripts/mix2-build.py \\
      --segments segments.json \\
      --audio path/to/concatenated-music.mp3 \\
      --output-dir outputs/publish/mix-2 \\
      --stage all
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

FFMPEG = os.environ.get("FFMPEG_BIN", "ffmpeg")
COMFYUI_URL = os.environ.get("COMFYUI_URL", "http://127.0.0.1:8188")
DEFAULT_WIDTH = 768
DEFAULT_HEIGHT = 432
TARGET_WIDTH = 1920
TARGET_HEIGHT = 1080
SCRIPT_DIR = Path(__file__).resolve().parent
LTX_IMG2VID = SCRIPT_DIR / "ltx-img2vid.py"

# Forbidden tokens; if any leak through into a prompt, fail closed.
# Reflects [[ai-hands-avoid]] operator memory.
HAND_FORBIDDEN_PROMPT_TOKENS = (
    "hand reaching", "hand holding", "fingers ",
    "person holding", "playing instrument",
)


def validate_prompt(prompt: str) -> tuple[bool, str | None]:
    lc = prompt.lower()
    for tok in HAND_FORBIDDEN_PROMPT_TOKENS:
        if tok in lc:
            return False, tok
    return True, None


SDXL_NEGATIVE = (
    "low quality, blurry, watermark, text, signature, frame border, "
    "people, person, face, faces, hand, hands, fingers, "
    "deformed, distorted, ugly, oversaturated"
)

SDXL_TURBO_CHECKPOINT = "sd_xl_turbo_1.0_fp16.safetensors"


def fetch_comfyui_sdxl_still(
    prompt: str,
    output_path: Path,
    width: int,
    height: int,
    seed: int,
    server: str = "http://127.0.0.1:8188",
    steps: int = 4,
    cfg: float = 1.0,
    timeout_seconds: int = 90,
) -> bool:
    """Generate a still via local SDXL-Turbo on ComfyUI.  Much faster than
    Pollinations (~5-10s vs ~60s) at the cost of one-time model download.
    """
    import json
    import urllib.request
    import urllib.error
    if output_path.exists() and output_path.stat().st_size > 1024:
        return True
    # Round to nearest multiple of 32 for SDXL latent compatibility
    w = (width // 32) * 32
    h = (height // 32) * 32
    if w < width: w += 32
    if h < height: h += 32

    workflow = {
        "1": {
            "class_type": "CheckpointLoaderSimple",
            "inputs": {"ckpt_name": SDXL_TURBO_CHECKPOINT},
        },
        "2": {
            "class_type": "CLIPTextEncode",
            "inputs": {"clip": ["1", 1], "text": prompt},
        },
        "3": {
            "class_type": "CLIPTextEncode",
            "inputs": {"clip": ["1", 1], "text": SDXL_NEGATIVE},
        },
        "4": {
            "class_type": "EmptyLatentImage",
            "inputs": {"width": w, "height": h, "batch_size": 1},
        },
        "5": {
            "class_type": "KSampler",
            "inputs": {
                "model": ["1", 0],
                "seed": seed,
                "steps": steps,
                "cfg": cfg,
                "sampler_name": "euler_ancestral",
                "scheduler": "normal",
                "denoise": 1.0,
                "positive": ["2", 0],
                "negative": ["3", 0],
                "latent_image": ["4", 0],
            },
        },
        "6": {
            "class_type": "VAEDecode",
            "inputs": {"samples": ["5", 0], "vae": ["1", 2]},
        },
        "7": {
            "class_type": "SaveImage",
            "inputs": {"images": ["6", 0], "filename_prefix": output_path.stem},
        },
    }
    data = json.dumps({"prompt": workflow, "client_id": f"sdxl_{seed}"}).encode("utf-8")
    try:
        req = urllib.request.Request(
            f"{server}/prompt", data=data,
            headers={"Content-Type": "application/json"}, method="POST",
        )
        with urllib.request.urlopen(req, timeout=30) as resp:
            result = json.loads(resp.read())
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="ignore")[:500]
        print(f"[sdxl] ERROR {e.code}: {body}", file=sys.stderr)
        return False
    except Exception as e:
        print(f"[sdxl] ERROR: {e}", file=sys.stderr)
        return False

    prompt_id = result.get("prompt_id")
    if not prompt_id:
        return False

    t0 = time.time()
    while True:
        time.sleep(2)
        if time.time() - t0 > timeout_seconds:
            print(f"[sdxl] TIMEOUT for seed={seed}", file=sys.stderr)
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
                if entry["status"].get("status_str") != "success":
                    return False
                # Find produced image
                comfy_out = Path(os.environ.get("COMFYUI_OUTPUT_DIR", "")) if os.environ.get("COMFYUI_OUTPUT_DIR") else None
                if comfy_out is None:
                    # Best-effort: use the same lookup as ltx-img2vid.py
                    for candidate in [
                        Path("G:/ai/ComfyUI_windows_portable/ComfyUI/output"),
                        Path.home() / "ComfyUI" / "output",
                    ]:
                        if candidate.exists():
                            comfy_out = candidate
                            break
                if comfy_out is None:
                    print("[sdxl] cannot locate ComfyUI output dir", file=sys.stderr)
                    return False
                for _, node_out in entry.get("outputs", {}).items():
                    for img in node_out.get("images", []):
                        if img.get("filename", "").lower().endswith((".png", ".jpg", ".jpeg")):
                            comfy_file = comfy_out / img.get("subfolder", "") / img["filename"]
                            if comfy_file.exists():
                                output_path.parent.mkdir(parents=True, exist_ok=True)
                                shutil.move(str(comfy_file), str(output_path))
                                return True
                return False


def fetch_pollinations_still(
    prompt: str,
    output_path: Path,
    width: int,
    height: int,
    seed: int,
    timeout_seconds: int = 90,
) -> bool:
    if output_path.exists() and output_path.stat().st_size > 1024:
        return True
    encoded = urllib.parse.quote(prompt)
    url = (
        f"https://image.pollinations.ai/prompt/{encoded}"
        f"?width={width}&height={height}&seed={seed}&nologo=true&model=flux"
    )
    req = urllib.request.Request(url, headers={"User-Agent": "mix2-build/1.0"})
    try:
        with urllib.request.urlopen(req, timeout=timeout_seconds) as resp:
            data = resp.read()
            if len(data) < 1024:
                print(f"[pollinations] WARN: response too small ({len(data)} bytes) for seg seed={seed}", file=sys.stderr)
                return False
            output_path.parent.mkdir(parents=True, exist_ok=True)
            output_path.write_bytes(data)
            return True
    except (urllib.error.HTTPError, urllib.error.URLError, TimeoutError) as e:
        print(f"[pollinations] ERROR: {e} for seed={seed}", file=sys.stderr)
        return False


def stage_stills(segments: list[dict], stills_dir: Path, backend: str, batch_sleep: float) -> int:
    """Generate one image per segment.  Returns number of failures."""
    print(f"[stage:stills] backend={backend} dir={stills_dir} count={len(segments)}")
    fail = 0
    for seg in segments:
        idx = seg["idx"]
        out_path = stills_dir / f"{idx:04d}.jpg"
        if out_path.exists() and out_path.stat().st_size > 1024:
            continue
        ok, bad = validate_prompt(seg["prompt"])
        if not ok:
            print(f"[stage:stills] FORBIDDEN TOKEN in seg {idx}: {bad!r}", file=sys.stderr)
            fail += 1
            continue
        if backend == "pollinations":
            ok = fetch_pollinations_still(
                prompt=seg["prompt"],
                output_path=out_path,
                width=DEFAULT_WIDTH,
                height=DEFAULT_HEIGHT,
                seed=seg["seed"],
            )
            if not ok:
                fail += 1
            else:
                time.sleep(batch_sleep)
        elif backend == "comfyui-sdxl":
            ok = fetch_comfyui_sdxl_still(
                prompt=seg["prompt"],
                output_path=out_path,
                width=DEFAULT_WIDTH,
                height=DEFAULT_HEIGHT,
                seed=seg["seed"],
                server=COMFYUI_URL,
            )
            if not ok:
                fail += 1
        else:
            print(f"[stage:stills] unknown backend: {backend}", file=sys.stderr)
            return -1
    print(f"[stage:stills] DONE  fail={fail}/{len(segments)}")
    return fail


def stage_clips(segments: list[dict], stills_dir: Path, clips_dir: Path, per_clip_timeout: int) -> int:
    """For each segment, run ltx-img2vid.py to produce a motion clip."""
    print(f"[stage:clips] dir={clips_dir} count={len(segments)}")
    fail = 0
    for seg in segments:
        idx = seg["idx"]
        still_path = stills_dir / f"{idx:04d}.jpg"
        clip_path = clips_dir / f"{idx:04d}.mp4"
        if clip_path.exists() and clip_path.stat().st_size > 10240:
            continue
        if not still_path.exists():
            print(f"[stage:clips] SKIP seg {idx}: still missing", file=sys.stderr)
            fail += 1
            continue
        # LTX-Video frames: target ~24fps * segment_duration, clamp to LTX valid range (9, max step 8)
        target_frames = max(9, int(round(seg["duration"] * 24 / 8)) * 8 + 1)
        target_frames = min(target_frames, 257)  # LTX practical cap
        cmd = [
            sys.executable, str(LTX_IMG2VID),
            "--image", str(still_path),
            "--prompt", seg["prompt"],
            "--output", str(clip_path),
            "--width", str(DEFAULT_WIDTH),
            "--height", str(DEFAULT_HEIGHT),
            "--length", str(target_frames),
            "--fps", "24",
            "--steps", "30",
            "--seed", str(seg["seed"]),
            "--skip-existing",
            "--timeout", str(per_clip_timeout),
            "--server", COMFYUI_URL,
        ]
        result = subprocess.run(cmd)
        if result.returncode != 0:
            print(f"[stage:clips] FAIL seg {idx} rc={result.returncode}", file=sys.stderr)
            fail += 1
    print(f"[stage:clips] DONE  fail={fail}/{len(segments)}")
    return fail


def ffmpeg_concat_with_xfade(
    clips_dir: Path,
    segments: list[dict],
    audio_path: Path,
    output_path: Path,
    target_width: int = TARGET_WIDTH,
    target_height: int = TARGET_HEIGHT,
    xfade_duration: float = 0.3,
) -> int:
    """Concat clips with xfade transitions + scale to target + mux audio + NVENC encode."""
    print(f"[stage:compose] {len(segments)} clips -> {output_path}")

    # Build a concat-with-xfade ffmpeg filter graph.
    # For now keep it simple: use concat demuxer (no xfade), then a single
    # full-clip xfade-equivalent grade pass.  Real xfade across N clips
    # requires the xfade filter applied pairwise; that's brittle for 600
    # clips and will be added in a follow-up if needed.
    # First pass: concat demuxer + scale + grade + NVENC.

    concat_list = output_path.parent / "_concat.txt"
    concat_list.parent.mkdir(parents=True, exist_ok=True)
    with concat_list.open("w", encoding="utf-8") as f:
        for seg in segments:
            clip = clips_dir / f"{seg['idx']:04d}.mp4"
            if not clip.exists():
                print(f"[stage:compose] WARN: missing clip {clip.name}", file=sys.stderr)
                continue
            # Bash-style single-quote escape for ffmpeg concat file syntax.
            f.write(f"file '{clip.as_posix()}'\n")

    # Grade profile: lofi_warm_grain — eq + colorbalance + curves
    vf = (
        "scale={w}:{h}:force_original_aspect_ratio=increase,"
        "crop={w}:{h},"
        "eq=saturation=0.92:contrast=0.98:gamma=1.04,"
        "colorbalance=rs=0.04:gs=0.0:bs=-0.04,"
        "format=yuv420p"
    ).format(w=target_width, h=target_height)

    cmd = [
        FFMPEG, "-y", "-hide_banner", "-loglevel", "warning",
        "-f", "concat", "-safe", "0", "-i", str(concat_list),
        "-i", str(audio_path),
        "-vf", vf,
        "-c:v", "h264_nvenc",
        "-preset", "p4",
        "-rc", "vbr",
        "-cq", "19",
        "-b:v", "12M",
        "-maxrate", "15M",
        "-bufsize", "20M",
        "-c:a", "aac", "-b:a", "192k",
        "-map", "0:v", "-map", "1:a",
        "-shortest",
        str(output_path),
    ]
    print(f"[stage:compose] running ffmpeg ({len(cmd)} args)...")
    result = subprocess.run(cmd)
    return result.returncode


def main():
    p = argparse.ArgumentParser(description="Mix #2 orchestrator")
    p.add_argument("--segments", required=True, type=Path)
    p.add_argument("--audio", required=True, type=Path)
    p.add_argument("--output-dir", required=True, type=Path)
    p.add_argument("--stage", choices=["stills", "clips", "compose", "all"], default="all")
    p.add_argument("--image-backend", choices=["pollinations", "comfyui-sdxl"], default="pollinations",
                   help="pollinations = free Pollinations.ai flux (slow, no setup); comfyui-sdxl = local SDXL-Turbo via ComfyUI (fast, requires sd_xl_turbo_1.0_fp16.safetensors in models/checkpoints/)")
    p.add_argument("--still-batch-sleep", type=float, default=1.5, help="rate-limit Pollinations requests (sec)")
    p.add_argument("--per-clip-timeout", type=int, default=300)
    args = p.parse_args()

    segments = json.loads(args.segments.read_text(encoding="utf-8"))

    stills_dir = args.output_dir / "stills"
    clips_dir = args.output_dir / "clips"
    final_mp4 = args.output_dir / f"yt-mix-2-{args.output_dir.name}-{time.strftime('%Y-%m-%d')}.mp4"

    rc = 0
    if args.stage in ("stills", "all"):
        fail = stage_stills(segments, stills_dir, args.image_backend, args.still_batch_sleep)
        if fail < 0:
            sys.exit(2)
        if fail > 0:
            print(f"[mix2-build] stills had {fail} failures; continuing", file=sys.stderr)

    if args.stage in ("clips", "all"):
        fail = stage_clips(segments, stills_dir, clips_dir, args.per_clip_timeout)
        if fail > 0:
            print(f"[mix2-build] clips had {fail} failures; continuing", file=sys.stderr)

    if args.stage in ("compose", "all"):
        if not args.audio.exists():
            print(f"[mix2-build] audio file not found: {args.audio}", file=sys.stderr)
            sys.exit(3)
        rc = ffmpeg_concat_with_xfade(clips_dir, segments, args.audio, final_mp4)
        if rc == 0:
            print(f"[mix2-build] FINAL: {final_mp4}")
            # Probe + report.  Resolve ffprobe next to ffmpeg if FFMPEG_BIN is
            # a path; fall back to PATH if it's a bare name.
            ffprobe = os.environ.get("FFPROBE_BIN", "")
            if not ffprobe:
                if "/" in FFMPEG or "\\" in FFMPEG:
                    ffprobe = str(Path(FFMPEG).parent / ("ffprobe.exe" if FFMPEG.endswith(".exe") else "ffprobe"))
                else:
                    ffprobe = FFMPEG.replace("ffmpeg", "ffprobe")
            probe_cmd = [
                ffprobe,
                "-v", "error",
                "-show_entries", "stream=codec_name,width,height,pix_fmt",
                "-show_entries", "format=duration,bit_rate",
                "-of", "default=nw=1",
                str(final_mp4),
            ]
            try:
                probe = subprocess.run(probe_cmd, capture_output=True, text=True)
                if probe.returncode == 0:
                    print(probe.stdout)
            except FileNotFoundError:
                print(f"[mix2-build] (ffprobe at {ffprobe!r} not found; skipping post-encode probe)")

    sys.exit(rc)


if __name__ == "__main__":
    main()
