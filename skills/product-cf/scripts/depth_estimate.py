#!/usr/bin/env python3
"""Monocular depth map via Depth-Anything V2 Small (Apache-2.0, commercial-safe).

Runs locally on Apple Silicon (torch MPS, no CUDA).  First call downloads the
~50MB Small weights from Hugging Face.  Output is a grayscale PNG where
brighter = nearer (the convention the parallax warp expects).

IMPORTANT licensing: ONLY the *Small* variant is Apache-2.0.  Base/Large/Giant
are CC-BY-NC and must NOT be used for commercial (CF) output.

Usage:  depth_estimate.py <image_in> <depth_out.png>
Run with the venv that has torch+transformers (e.g. /Users/melons/ai/.venv).
"""
import sys
from PIL import Image
import torch
from transformers import pipeline

inp, out = sys.argv[1], sys.argv[2]
dev = "mps" if torch.backends.mps.is_available() else "cpu"
pipe = pipeline("depth-estimation",
                model="depth-anything/Depth-Anything-V2-Small-hf",
                device=dev)
img = Image.open(inp).convert("RGB")
res = pipe(img)
depth = res["depth"]                 # PIL 'L', uint8, brighter = nearer
depth = depth.resize(img.size)       # ensure same size as input
depth.save(out)
print(f"depth_estimate: {out} ({depth.size[0]}x{depth.size[1]}) on {dev}", file=sys.stderr)
