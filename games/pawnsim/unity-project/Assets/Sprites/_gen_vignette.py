# -*- coding: utf-8 -*-
"""Polish Wave v3 V8 — optional subtle vignette depth-cue sprite.

Produces a single full-frame radial-gradient PNG used as a screen-space
overlay to very slightly darken the frame corners, focusing the eye on
center without muddying on-palette gameplay colors.

  vignette.png     512x512   OUTLINE_OBJ-toned radial gradient,
                              alpha 0.0 at center → 0.22 at corners.
                              Transparent background (alpha channel only;
                              the RGB channels carry OUTLINE_OBJ so the
                              Image component can tint if needed, but the
                              low alpha keeps it imperceptible in the play
                              area).

DESIGN RULES (V8 spec):
  - Color STRICTLY from palette.py (OUTLINE_OBJ = (40,30,22,255)).
    No ad-hoc values.
  - REJECT if the alpha at center is nonzero (must be fully transparent).
  - Alpha ramps smoothly from 0.0 at center to max 0.22 at corners.
    Max is kept well under 0.25 so on-palette colors in the play area
    are not measurably shifted (a 22% dark veil at the very edge is the
    maximum spec allows; the gradient means the majority of the play area
    sees < 0.10 alpha).
  - Smooth radial falloff (Euclidean distance from center, clamped).
  - No hard edges, no banding — the gradient uses float math per-pixel.
  - Canvas 512x512: high enough resolution for the stretched overlay to
    look smooth at any window size; small enough to load instantly.
  - The overlay is stretched 1:1 across the screen by the Unity Canvas
    Image; aspect ratio mismatch is intentional (it creates a slightly
    asymmetric oval gradient which reads more naturally than a perfect
    circle on widescreen).

_preview_vignette.png  QA composite: vignette composited over a
    512x512 neutral grass-tile-tone panel so the effect is visible
    in isolation.  Gamma-approximate: the preview is NOT accurate to
    Unity's linear-space rendering, but is sufficient for a QA
    eyeball check.

WIRING (for QA / Build Engineer — this generator does NOT edit SceneSetup.cs):
  Add vignette.png to ForceImportAllSprites in SceneSetup.cs, OR let
  VignetteOverlay.cs (the companion self-attaching builder) load it at
  runtime via Resources.Load / AssetDatabase.LoadAssetAtPath — the .cs
  file handles its own import with a null-guard and a Debug.LogWarning
  if the sprite is missing.
  PPU: irrelevant for this asset (it is stretched to full screen by a
  Canvas Image, not placed in world space).
  TextureImporter: filterMode = Point, Bilinear also acceptable since
  the gradient is inherently smooth.

QA FLAG (V8 OPTIONAL):
  This is the OPTIONAL V8 vignette.  QA MUST REJECT if:
  - Corners are visibly muddy or feel compressed.
  - Any on-palette color in the play area (center half of screen) reads
    differently from a reference screenshot without the overlay.
  - The overlay intercepts clicks (raycastTarget must be false).
  - Gameplay UI panels (PawnInfoPanel, resource bars, clock) are obscured
    by the vignette instead of drawing above it.
"""
from __future__ import annotations
import math
from pathlib import Path
from PIL import Image

from palette import OUTLINE_OBJ

HERE = Path(__file__).resolve().parent

# ── Tunables ────────────────────────────────────────────────────────────────
CANVAS_SIZE  = 512       # square canvas; Unity stretches it to fill screen
ALPHA_CENTER = 0.00      # fully transparent at center
ALPHA_CORNER = 0.22      # max darkening at corners (22/255 ≈ faint shadow)
GAMMA        = 1.8       # falloff shaping: >1.0 = slower rise near center
                         # (keeps the play area almost fully transparent,
                         # concentrates the gradient near the edges)

# ── Preview tunables ─────────────────────────────────────────────────────────
# Neutral warm-grass tone for the preview backing panel.
PREVIEW_BG   = (96, 116, 70, 255)   # matches GRASS_MD from palette — readable mid-tone


def gen_vignette() -> Image.Image:
    """Generate a 512x512 RGBA vignette gradient.

    Alpha channel: 0 at center, ALPHA_CORNER at corners.
    RGB channels: OUTLINE_OBJ tone (so color blending in Unity is coherent
    if the Image color is set to white; the actual visual effect is the
    darkening from the alpha).
    """
    W = H = CANVAS_SIZE
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    pixels = im.load()

    cx = (W - 1) / 2.0
    cy = (H - 1) / 2.0
    # Max possible distance: corner.  We normalise so corner == 1.0.
    max_dist = math.sqrt(cx * cx + cy * cy)

    r_ch, g_ch, b_ch = OUTLINE_OBJ[0], OUTLINE_OBJ[1], OUTLINE_OBJ[2]

    for y in range(H):
        for x in range(W):
            dx = x - cx
            dy = y - cy
            # Normalised distance from center [0..1]
            dist_norm = math.sqrt(dx * dx + dy * dy) / max_dist
            # Shaped falloff: pow(dist, GAMMA) keeps center very transparent,
            # concentrates darkening at edges.
            shaped = pow(dist_norm, GAMMA)
            alpha_f = ALPHA_CENTER + (ALPHA_CORNER - ALPHA_CENTER) * shaped
            alpha_f = max(0.0, min(1.0, alpha_f))
            alpha_byte = int(round(alpha_f * 255))
            pixels[x, y] = (r_ch, g_ch, b_ch, alpha_byte)

    return im


def gen_preview(vignette: Image.Image) -> Image.Image:
    """Composite the vignette over a neutral grass-tone panel for QA."""
    W, H = vignette.size
    # Backing panel: solid grass mid-tone (approximates the world ground)
    panel = Image.new("RGBA", (W, H), PREVIEW_BG)
    # Alpha-composite the vignette over the panel
    panel.alpha_composite(vignette)
    # Scale up 2x for easy visual inspection (1024x1024 output)
    return panel.resize((W * 2, H * 2), Image.NEAREST)


def main():
    # --- Vignette PNG ---
    vignette = gen_vignette()
    out_path = HERE / "vignette.png"
    vignette.save(out_path)
    # Report alpha range for verification
    pixels = vignette.load()
    alpha_center = pixels[CANVAS_SIZE // 2, CANVAS_SIZE // 2][3]
    alpha_corner = pixels[0, 0][3]
    print(f"[gen_vignette] vignette.png  {vignette.width}x{vignette.height}")
    print(f"[gen_vignette]   alpha at center: {alpha_center}/255 ({alpha_center/255:.3f})")
    print(f"[gen_vignette]   alpha at corner: {alpha_corner}/255 ({alpha_corner/255:.3f})")
    print(f"[gen_vignette]   RGB tone: OUTLINE_OBJ = ({OUTLINE_OBJ[0]},{OUTLINE_OBJ[1]},{OUTLINE_OBJ[2]})")

    # --- QA Preview ---
    preview = gen_preview(vignette)
    prev_path = HERE / "_preview_vignette.png"
    preview.save(prev_path)
    print(f"[gen_vignette] _preview_vignette.png  {preview.width}x{preview.height}  (2x scale on GRASS_MD panel)")
    print("[gen_vignette] QA: corners should be barely perceptible; center must look identical to GRASS_MD (#607446)")


if __name__ == "__main__":
    main()
