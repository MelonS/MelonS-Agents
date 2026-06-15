#!/usr/bin/env python3
"""Cylinder-wrap turntable — a genuine 3D spin of a cylindrical product (can,
bottle) from a SINGLE front photo.

A beverage is approximately a cylinder, and a can's label wraps the full 360°,
so re-projecting the front photo onto a rotating cylinder is geometrically
*correct*, not a fake: each output column samples the real label texel that
would face the camera at that rotation.  The label is only ever resampled
(honest foreshortening as it turns), never regenerated.

Per row y the product has its own centre cx and radius r (so a bottle's narrow
neck and wide body rotate consistently in ANGLE).  For rotation θ:

    screen column X  ->  surface angle  ψ = asin((X-cx)/r)
    the texel now at ψ was, before rotating, at  ψ-θ, i.e. at photo column
        x0 = cx + r*sin(ψ-θ)
    visible only while |ψ-θ| <= ~88°  (else it has rotated out of sight)

Mild Lambert shading (0.78 + 0.22·cosψ) adds cylindrical roundness without
double-darkening the photo's own shading.

Usage:
  cylinder_turntable.py <canvas_rgba.png> <out_dir> <frames> [max_deg] [mode]
    mode: ping  (oscillate -max..+max..-max, default — safe near photographed front)
          spin  (continuous 0..360, only honest for a full-wrap can label)
Writes <out_dir>/p_%04d.png  (RGBA).  Run with the product-cf venv (numpy/scipy/PIL).
"""
import os
import sys
import numpy as np
from PIL import Image
from scipy.ndimage import map_coordinates

canvas_png = sys.argv[1]
out_dir = sys.argv[2]
frames = int(sys.argv[3])
max_deg = float(sys.argv[4]) if len(sys.argv) > 4 else 38.0
mode = sys.argv[5] if len(sys.argv) > 5 else "ping"

canvas = np.asarray(Image.open(canvas_png).convert("RGBA")).astype(np.float32)
H, W = canvas.shape[:2]
alpha = canvas[..., 3] / 255.0
mask = alpha > 0.5

# per-row centre & radius from the silhouette extent
idx = np.arange(W, dtype=np.float32)[None, :]
big = np.where(mask, idx, np.float32(W))
sml = np.where(mask, idx, np.float32(-1))
xL = big.min(axis=1)            # (H,)
xR = sml.max(axis=1)
row_ok = mask.any(axis=1)
cx = np.where(row_ok, 0.5 * (xL + xR), 0.0)
r = np.where(row_ok, np.maximum(0.5 * (xR - xL), 1.0), 1.0)

yy = np.mgrid[0:H, 0:W][0].astype(np.float32)
xx = np.tile(idx, (H, 1))
cxc = cx[:, None]
rc = r[:, None]

os.makedirs(out_dir, exist_ok=True)
denom = max(frames - 1, 1)
LIMIT = np.sin(np.radians(88.0))


def angle_at(i):
    t = i / denom
    if mode == "spin":
        return 2 * np.pi * t
    # ping: smooth -max .. +max .. -max
    return np.radians(max_deg) * np.sin(2 * np.pi * t)


for i in range(frames):
    th = angle_at(i)
    sinpsi = np.clip((xx - cxc) / rc, -1.0, 1.0)
    psi = np.arcsin(sinpsi)
    orig = psi - th
    sin_orig = np.sin(orig)
    visible = (np.abs(sin_orig) <= LIMIT) & mask
    x0 = cxc + rc * sin_orig
    coords = np.array([yy.ravel(), x0.ravel()])

    out = np.empty((H, W, 4), np.float32)
    for c in range(4):
        out[..., c] = map_coordinates(canvas[..., c], coords, order=1,
                                      mode="constant", cval=0.0).reshape(H, W)
    shade = (0.78 + 0.22 * np.cos(psi))[..., None]
    out[..., :3] *= shade
    out[..., 3] *= visible.astype(np.float32)      # texels rotated out of view vanish
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA").save(
        os.path.join(out_dir, f"p_{i:04d}.png"))

print(f"cylinder_turntable: wrote {frames} frames ({mode}, ±{max_deg}deg) to {out_dir}", file=sys.stderr)
