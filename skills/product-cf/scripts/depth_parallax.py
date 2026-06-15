#!/usr/bin/env python3
"""Depth-image-based-rendering (DIBR) parallax warp — the actual "3D motion".

Given the stage-B canvas (product centred on transparent 1080x1920) and a depth
map, render a camera move where NEAR pixels shift more than FAR pixels — genuine
parallax, the ingredient a single affine zoompan can never produce.  The real
photo is only ever *resampled* (backward warp), never regenerated, so the label
stays the literal pixels.  A focus plane is held near-still to pin the label.

Optionally screen-blends a pre-rendered wrapping-specular sequence (canvas-space,
from shade_normals.py) BEFORE warping, so highlight + parallax stay aligned.

Backward sampling (out[x] = in[x + disp]) is hole-free: dis-occluded regions
stretch slightly rather than tearing — kept invisible by small amplitude.

Usage:
  depth_parallax.py <canvas_rgba.png> <depth.png> <out_dir> <frames> \
                    <motion> <amp> <focus> [spec_dir] [zoom_amp]

  motion: panlr | panrl | orbit | push
Writes <out_dir>/p_%04d.png  (RGBA).
Run with the venv that has numpy/scipy/pillow (product-cf venv).
"""
import os
import sys
import numpy as np
from PIL import Image
from scipy.ndimage import map_coordinates, gaussian_filter

canvas_png = sys.argv[1]
depth_png = sys.argv[2]
out_dir = sys.argv[3]
frames = int(sys.argv[4])
motion = sys.argv[5] if len(sys.argv) > 5 else "panlr"
amp = float(sys.argv[6]) if len(sys.argv) > 6 else 42.0     # max px shift at depth=1, focus=0
focus = float(sys.argv[7]) if len(sys.argv) > 7 else 0.15   # depth plane held still (0..1)
spec_dir = sys.argv[8] if len(sys.argv) > 8 and sys.argv[8] not in ("", "-") else None
zoom_amp = float(sys.argv[9]) if len(sys.argv) > 9 else 0.06
cyl_blend = float(sys.argv[10]) if len(sys.argv) > 10 else 0.75  # 1=pure cylinder, 0=pure mono depth

canvas = np.asarray(Image.open(canvas_png).convert("RGBA")).astype(np.float32)
H, W = canvas.shape[:2]
alpha = canvas[..., 3] / 255.0
mask = alpha > 0.5

# Monocular depth (Depth-Anything): good global ordering, but near-FLAT inside a
# frontal product shot — on its own the bottle reads as a sliding 2D card.
mono = np.asarray(Image.open(depth_png).convert("L").resize((W, H))).astype(np.float32) / 255.0
if mask.any():
    m = mono[mask]
    mono = (mono - m.min()) / (m.max() - m.min() + 1e-6)
mono = mono * mask

# CYLINDER depth: a bottle/can is a cylinder, so its TRUE depth bulges —
# centre near, left/right edges far.  Synthesised per-row as a half-cylinder
# cross-section over the silhouette.  THIS is what makes a lateral move read as
# the product *turning* (centre travels more than edges) instead of sliding.
cyl = np.zeros((H, W), np.float32)
xs_full = np.arange(W, dtype=np.float32)
for y in range(H):
    cols = np.where(mask[y])[0]
    if cols.size < 2:
        continue
    xL, xR = cols[0], cols[-1]
    c = 0.5 * (xL + xR)
    half = max((xR - xL) / 2.0, 1.0)
    seg = np.clip(1.0 - ((xs_full[xL:xR + 1] - c) / half) ** 2, 0.0, 1.0)
    cyl[y, xL:xR + 1] = np.sqrt(seg)

depth = (cyl_blend * cyl + (1.0 - cyl_blend) * mono) * mask
depth = gaussian_filter(depth, 2.0)
d = depth - focus                     # signed depth relative to the pinned plane

yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
cx, cy = W / 2.0, H / 2.0
os.makedirs(out_dir, exist_ok=True)
denom = max(frames - 1, 1)


def ease(t):
    return 0.5 - 0.5 * np.cos(np.pi * t)   # smooth in/out 0..1


for i in range(frames):
    t = i / denom
    if motion == "panlr":
        sx, sy, zf = (ease(t) - 0.5) * 2.0, 0.0, 0.0
    elif motion == "panrl":
        sx, sy, zf = (0.5 - ease(t)) * 2.0, 0.0, 0.0
    elif motion == "orbit":
        sx, sy, zf = np.sin(t * 2 * np.pi), 0.0, 0.0
    elif motion == "pull":  # parallax dolly-OUT (start near, pull back)
        sx, sy, zf = 0.0, 0.0, 1.0 - ease(t)
    else:  # push — parallax dolly-in (near grows faster than far)
        sx, sy, zf = 0.0, 0.0, ease(t)

    base = canvas.copy()
    if spec_dir:
        sp = np.asarray(Image.open(os.path.join(spec_dir, f"spec_{i:04d}.png")).convert("L")).astype(np.float32) / 255.0
        sp = sp[..., None] * 0.30                      # screen blend at 0.30
        base[..., :3] = 255.0 * (1.0 - (1.0 - base[..., :3] / 255.0) * (1.0 - sp))

    # backward map: lateral parallax + depth-scaled dolly zoom about centre
    scale = 1.0 + zoom_amp * zf * depth                # near pixels zoom slightly more
    mapx = cx + (xx - cx) / np.maximum(scale, 1e-3) + d * amp * sx
    mapy = cy + (yy - cy) / np.maximum(scale, 1e-3) + d * amp * sy
    coords = np.array([mapy.ravel(), mapx.ravel()])

    out = np.empty_like(base)
    for c in range(4):
        out[..., c] = map_coordinates(base[..., c], coords, order=1,
                                      mode="constant", cval=0.0).reshape(H, W)
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA").save(
        os.path.join(out_dir, f"p_{i:04d}.png"))

print(f"depth_parallax: wrote {frames} frames ({motion}, amp={amp}) to {out_dir}", file=sys.stderr)
