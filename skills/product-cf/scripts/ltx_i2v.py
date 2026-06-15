#!/usr/bin/env python3
"""LTX-Video 2B image-to-video — free, local, runs on 16GB Apple Silicon (MPS).

The one local I2V model whose VAE (causal Conv3d) actually works on MPS today
(SVD's ConvTranspose3d hard-crashes on MPS fp16; CogVideoX-5B / Wan2.2-14B don't
fit 16GB).  Produces a short clip of believable SUBTLE product motion from one
photo — the "moving plate" that feeds the product-cf ffmpeg pipeline.

16GB is the binding constraint: cpu-offload + VAE tiling + attention slicing are
mandatory (a full .to('mps') will swap/OOM).  Expect MINUTES per clip, ~512x768,
65-97 frames.  Run with /Users/melons/ai/.venv (torch 2.12 + diffusers).

Usage:
  ltx_i2v.py <image> <out.mp4> [width] [height] [frames] [steps] [prompt]

Env: set PYTORCH_ENABLE_MPS_FALLBACK=1 and PYTORCH_MPS_HIGH_WATERMARK_RATIO=0.0
"""
import os
import sys
import torch
from diffusers import LTXImageToVideoPipeline
from diffusers.utils import export_to_video, load_image

img_path = sys.argv[1]
out_path = sys.argv[2]
width = int(sys.argv[3]) if len(sys.argv) > 3 else 512
height = int(sys.argv[4]) if len(sys.argv) > 4 else 768
frames = int(sys.argv[5]) if len(sys.argv) > 5 else 97   # 8k+1; AVOID 69
steps = int(sys.argv[6]) if len(sys.argv) > 6 else 8     # distilled: low steps
prompt = sys.argv[7] if len(sys.argv) > 7 else (
    "commercial product shot of a beverage bottle on a clean studio background, "
    "subtle condensation droplets forming, slow gentle rotation, soft moving "
    "studio light sweep, shallow depth of field, photorealistic, high detail")
neg = "warped label, morphing text, distorted logo, jitter, wobble, worst quality, blurry"

model_id = os.environ.get("LTX_MODEL_ID", "Lightricks/LTX-Video-0.9.5")
print(f"[ltx] load {model_id} (bf16)…", file=sys.stderr)
pipe = LTXImageToVideoPipeline.from_pretrained(model_id, torch_dtype=torch.bfloat16)
pipe.enable_model_cpu_offload()      # MANDATORY on 16GB — never full .to('mps')
pipe.vae.enable_tiling()             # caps VAE peak memory
try:
    pipe.enable_attention_slicing()
except Exception as e:
    print(f"[ltx] attn slicing skipped: {e}", file=sys.stderr)

image = load_image(img_path)
print(f"[ltx] generate {width}x{height} x{frames} steps={steps}…", file=sys.stderr)
video = pipe(
    image=image,
    prompt=prompt,
    negative_prompt=neg,
    width=width, height=height,
    num_frames=frames,
    num_inference_steps=steps,
    guidance_scale=1.0,              # distilled = no CFG
).frames[0]

os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
export_to_video(video, out_path, fps=24)
print(f"[ltx] wrote {out_path}", file=sys.stderr)
