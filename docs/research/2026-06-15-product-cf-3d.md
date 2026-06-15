# product-cf: free/local "looks 3D" from a single product photo

**Date:** 2026-06-15
**Question (operator):** Can the product-cf hero shots be made to look *genuinely
3D* — not just a flat image being zoomed — for free?
**Method:** 11-agent research workflow (5 technique families researched in
parallel, each adversarially verified on-machine, then synthesized).

## Verdict

Not hard. Free, local, label-safe 3D-look is an off-the-shelf pipeline on this
Mac (Apple Silicon, no CUDA, no paid APIs). The old output read flat for one
reason only: stage E was a single-plane **affine zoompan** — every pixel moves
by the same rule, so parallax is zero. The fix is a **depth map** driving a
renderer where near pixels shift more than far ones.

Honest ceiling: 2.5D relief + slight turntable, **not** walk-around-the-bottle
full 3D. That is exactly the regime label-integrity requires anyway — the
constraint and the technique agree.

## Techniques ranked (free / local on Apple Silicon / label-safe)

| Technique | Realism | Effort | Label risk | Status |
|-----------|:------:|:-----:|:----------:|--------|
| Alpha→normal-map wrapping specular (numpy/scipy) | 3/5 | low | **none** | **shipped** (Phase 1) |
| Depth-Anything V2 Small → DIBR parallax warp | 4/5 | low | low | **shipped** (Phase 2) |
| Depth-Anything V2 Small (depth map) | — | low | none | enabling input (used) |
| Blender headless depth-mesh + Cycles Metal orbit | 3/5 | high | low | deferred (bench later) |
| TripoSR single-image→mesh + tiny turntable | 4/5 | high | **high** | rejected (re-bakes label) |

## What was implemented

- **Phase 1 — wrapping specular** (`scripts/shade_normals.py`): distance-transform
  the product alpha into a pseudo-rounded heightfield → normals → a moving
  highlight that *wraps* the form. Label risk mathematically zero (`spec*alpha`).
- **Phase 2 — depth parallax** (`scripts/depth_estimate.py` + `depth_parallax.py`):
  Depth-Anything V2 Small (Apache-2.0, torch-MPS) estimates depth; a numpy/scipy
  **backward warp** shifts near pixels more than far → genuine parallax (bottle
  appears to turn slightly). Real photo is only resampled, never regenerated; a
  focus plane pins the label. Specular is baked in before the warp so it stays
  aligned. Gated by `HERO_PARALLAX` / `PCF_PARALLAX` (default 0 = zoompan).

## Why DepthFlow was rejected (the workflow's top recommendation)

DepthFlow (depth GLSL parallax) was the synthesis's recommended path, but on this
machine the install failed: it resolves to 0.9.1 and drags in
`shaderflow → pyfluidsynth → pyaudio`, which needs `portaudio.h` to compile. Add
its AGPL license (vs the MIT skill) and macOS-OpenGL-deprecation fragility
(past Apple-Silicon segfaults), and a **self-contained DIBR** (depth model + our
own numpy warp) is the more robust engineering choice — same algorithm, zero
fragile deps, MIT-clean.

## Environment notes (verified)

- Depth model lives in `/Users/melons/ai/.venv` (torch 2.12, transformers 5.8.1,
  MPS=True). rembg + numpy/scipy live in `~/.cache/melons-ai/venvs/product-cf`
  (Python 3.14). Three-venv split is intentional; scripts name each explicitly.
- **Only** Depth-Anything V2 *Small* is Apache-2.0 (commercial-safe). Base/Large/
  Giant are CC-BY-NC = blocked for ad output. Pin Small.
- numpy 2.x removed `ndarray.ptp()`; use `np.ptp(arr)`.
- White/plain-bg studio shots give monocular depth low intra-object contrast, so
  intra-bottle volume is subtle — but **bottle-vs-background** parallax is strong
  and reads as 3D. Main quality lever if needed: a hand-authored cylindrical
  depth map. Knobs: `HERO_PARALLAX_AMP` (shift px), `HERO_PARALLAX_FOCUS` (pinned plane).

## Follow-up: "still looks 2D" → cylinder-wrap turntable (the real win)

DIBR parallax still read as a sliding 2D card because monocular depth on a
frontal product shot is near-flat. Two findings closed the gap:

1. **Synthesized cylinder depth** (`depth_parallax.py`, `HERO_PARALLAX_CYL`):
   replace the flat monocular depth with a per-row half-cylinder profile so a
   lateral move makes the centre travel more than the edges → the bottle turns.
2. **Cylinder-wrap turntable** (`cylinder_turntable.py`, `HERO_TURNTABLE`) — the
   strongest result. A beverage is a cylinder and a can's label wraps 360°, so
   re-projecting the front photo onto a rotating cylinder is *geometrically
   correct*: each output column samples the real label texel facing the camera
   at that rotation (per-row centre+radius). Pure numpy, no Blender, label only
   resampled. On the Coca-Cola bottle the label visibly wraps left/right — a
   real rotating-bottle look from ONE photo.

### Honest ceiling on "perfect 3D"

A faithful walk-around from one frontal photo is **impossible** — the sides/back
aren't in the data (information limit, not tooling). For **cylindrical** products
(the whole beverage use case) the cylinder-wrap is effectively "perfect 3D" for a
moderate spin, free and label-safe. For **non-cylindrical** products (boxes,
irregular shapes) the faithful path is **multi-angle capture** → frame-sequence
turntable (operator shoots ~12–24 photos / a 10s spin). Generation paths
(TripoSR / paid I2V) trade label fidelity and were not adopted.

Modes in the runner: `PCF_TURNTABLE=1` (cylinder spin) > `PCF_PARALLAX=1` (DIBR) >
default zoompan. Turntable knobs: `HERO_TURNTABLE_DEG`, `HERO_TURNTABLE_MODE`
(ping|spin).

## Follow-up 2: operator rejected ALL fake-3D ("다 별로") → pivot to generative I2V

The FFmpeg/numpy compositing ceiling (cutout-on-void, label-warping cylinder)
was below "good CF". Pivoted to real image-to-video, free+local first.

**Verified local I2V finding (LTX-Video 2B 0.9.5, diffusers, torch 2.12 MPS):**
- It is the only local I2V whose VAE runs on MPS (SVD's ConvTranspose3d crashes;
  CogVideoX-5B / Wan2.2-14B don't fit 16GB).
- Still quality is genuinely good — a clean, correctly-lit, label-intact bottle
  (validated frame). The I2V *direction* is right.
- **But impractical on this 16GB box:** a tiny 384×512×49f / 6-step clip took
  **~66 min wall at ~5% CPU** = constant memory swapping (cpu-offload thrash),
  and the motion was near-static (mean frame Δ 0.2/255). 16GB is too small for
  video diffusion; bigger res/frames/steps = worse/OOM. Not viable for iteration
  or production here.
- Script kept at `scripts/ltx_i2v.py` (works on a ≥32–64GB / CUDA box; gated, not
  wired into the runner).

**Conclusion:** good local I2V needs more RAM/GPU than this machine has. The
realistic paths to an actually-good result are **paid cloud I2V** (Kling /
Runway / Hailuo / Veo — minutes, no local compute, money-firewall) or **real
footage** of the physical product. Decision pending with operator.

## Deferred / next

- Blender depth-mesh orbit (±12° relief) — higher effort, ~3/5; bench only if the
  DIBR ceiling isn't enough.
- TripoSR — only for a "boldest" tier and only with the original cutout composited
  back over near-frontal frames (it re-bakes a new, label-warping texture).
