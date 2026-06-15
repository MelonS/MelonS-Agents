#!/usr/bin/env python3
"""Alpha-driven wrapping specular — a volume cue with ZERO label risk.

Given the stage-B canvas (RGBA, product centred on a transparent 1080x1920
field), estimate a *pseudo* surface from the product's alpha silhouette
(distance-transform -> rounded heightfield -> normals) and render a moving
specular highlight that WRAPS that form instead of sliding flat like the old
geq sweep.

Label safety is mathematical, not heuristic: the specular is multiplied by the
product alpha (0 outside the product) and is only ever screen-blended additively
downstream, so it can never alter the label's RGB.  The real photo stays the
literal pixels.

Usage:
  shade_normals.py <canvas_rgba.png> <out_dir> <frames> [shininess] [opacity]

Writes <out_dir>/spec_%04d.png  (grayscale, one per frame).
"""
import os
import sys
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt, gaussian_filter, sobel

canvas_png = sys.argv[1]
out_dir = sys.argv[2]
frames = int(sys.argv[3])
shininess = float(sys.argv[4]) if len(sys.argv) > 4 else 40.0
opacity = float(sys.argv[5]) if len(sys.argv) > 5 else 1.0

img = np.asarray(Image.open(canvas_png).convert("RGBA")).astype(np.float32) / 255.0
alpha = img[..., 3]
mask = alpha > 0.5                       # excludes the faint (0.16) reflection

# pseudo-heightfield: distance to silhouette edge -> rounded bulge
dist = distance_transform_edt(mask).astype(np.float32)
dmax = float(dist.max()) if dist.max() > 0 else 1.0
depth = np.sqrt(dist / dmax)
depth = gaussian_filter(depth, sigma=6)  # the smoothing (not a per-row trick) kills faceting

# normals from the heightfield gradient
gx = sobel(depth, axis=1)
gy = sobel(depth, axis=0)
nx, ny = -gx, -gy
nz = np.full_like(depth, 1.0 / 2.5)
inv = 1.0 / (np.sqrt(nx * nx + ny * ny + nz * nz) + 1e-8)
nx *= inv; ny *= inv; nz *= inv

os.makedirs(out_dir, exist_ok=True)
denom = max(frames - 1, 1)
for i in range(frames):
    t = i / denom
    # light sweeps left -> right across the form
    lx, ly, lz = -1.0 + 2.0 * t, -0.3, 0.6
    hx, hy, hz = lx, ly, lz + 1.0       # half-vector with view (0,0,1)
    hinv = 1.0 / ((hx * hx + hy * hy + hz * hz) ** 0.5 + 1e-8)
    hx *= hinv; hy *= hinv; hz *= hinv
    ndoth = np.clip(nx * hx + ny * hy + nz * hz, 0.0, 1.0)
    spec = (ndoth ** shininess) * alpha * opacity
    Image.fromarray((np.clip(spec, 0, 1) * 255).astype(np.uint8), mode="L").save(
        os.path.join(out_dir, f"spec_{i:04d}.png"))

print(f"shade_normals: wrote {frames} spec frames to {out_dir}", file=sys.stderr)
