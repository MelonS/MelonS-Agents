---
name: product-cf
description: Turn a single product photo into a CF-style (commercial) 9:16 vertical short — the product stays a pixel-perfect real cutout while the world moves around it (2.5D camera push, label light-sweep, liquid/particle B-roll intercut, beat-aligned cuts). Use when the operator has ONE product still (a beverage can, bottle, cosmetic, gadget) and wants an ad-feel Shorts/Reels/TikTok clip without distorting the product label or generating hands. Builds ON TOP of the music-video skill (reuses its beat detection, Pexels B-roll, shaders) — does not replace it.
license: MIT
compatibility: Requires ffmpeg, the music-video skill (and its deps: aubio CLI, jq, Pexels key or demo mode), and a rembg venv for background removal (PRODUCT_CF_VENV, default ~/.cache/melons-ai/venvs/product-cf). macOS or Linux.
metadata:
  authors: MelonS-Agents
  version: "0.1.0"
  pipeline-source: agents/missions/product-cf/run.sh
  builds-on: music-video
  spec: agentskills.io
  added: "2026-06-14"
allowed-tools: Bash(bash:*) Bash(ffmpeg:*) Bash(ffprobe:*) Bash(curl:*) Bash(jq:*) Read Write
---

# product-cf

Make a commercial-film-feel (CF) vertical short from **one product
photo**.  The defining constraint: **never regenerate the product.**
Generative video distorts labels and mangles hands — exactly the two
things a product ad cannot afford.  So the product stays a real,
pixel-perfect cutout and *the world moves around it*.

## Why a separate skill (not a music-video mode)

`music-video` is music-as-primary-audio + free-to-morph mood B-roll.
`product-cf` differs on all three split criteria:

| criterion       | music-video        | product-cf                      |
|-----------------|--------------------|---------------------------------|
| primary input   | a music file       | a product **photo**             |
| trigger intent  | "make a music vid" | "make an ad for this product"   |
| hard invariant  | none               | product label integrity (never morphs) |

So product-cf is its own skill, but it **reuses** music-video as a
black box for the parts that overlap (beat detection, Pexels B-roll,
9:16 render, shaders).  music-video's `run.sh` is never edited.

## The technique (compositing + 2.5D, no I2V on the product)

1. **Cutout** — `rembg` removes the background → product PNG with alpha.
2. **2.5D camera move** — product gets a slow push-in / drift; the
   background scene moves at a different rate → parallax depth.  Product
   pixels never change, only the virtual camera.
3. **Label light-sweep** — a specular highlight travels across the
   label, masked to the product alpha.  The "premium product shot" tell.
4. **Motion lives around the product, not on it** — splashes, ice,
   condensation, bokeh come from liquid B-roll (music-video's Pexels
   layer) **intercut** with the hero shot, or composited as foreground
   particles.  "Shaking / pouring / drinking" is *implied* via these
   cuts — the real product is never touched by a hand or liquid.
5. **Beat-synced assembly** — music-video supplies beat-aligned cut
   points; hero shots land on the strong beats, B-roll fills between.

## Usage

```
agents/missions/product-cf/run.sh <short_id> <product_image> [keywords_csv] [music_file]
```

- `<product_image>` — one still (png/jpg). Clean, single product, any background.
- `keywords_csv` — mood/B-roll keywords for the world around the product
  (e.g. `"water splash,ice cubes,citrus slice,condensation droplets,studio light"`).
- `music_file` — optional; falls back to music-video demo music.

## What it produces

- A **1080×1920** mp4, beat-aligned, product label intact throughout.
- Intermediate hero clip(s) under the mission `outputs/` for review
  before the full assembly.

## Stages worth knowing

- `scripts/product-hero.sh` — the cutout + 2.5D + light-sweep core
  (standalone-testable; produces one hero clip from one photo).
- `agents/missions/product-cf/run.sh` — orchestrator: hero layer +
  music-video B-roll base → beat-synced final.

## Upgrade paths (not default)

- **Local I2V** (Wan2.2 / LTX) for *background scenes only* — never the
  product body (label-distortion risk).
- **Paid I2V** (Kling / Runway) for product motion — best quality but
  **money-firewall**: requires explicit operator confirmation.
