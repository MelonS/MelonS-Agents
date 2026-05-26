"""sprite_proc — procedural 2D sprite generation (no LLM, no GPU).

Artist agent's fast-path: when the asset is a simple shape (circle,
square, character outline) the Artist generates locally in <1s
instead of:
  - waiting on SDXL (~5s + GPU + quality fluctuation)
  - waiting on Kenney download (fast but limited catalog)

Shapes supported (Day-1):
  circle    : colored disc with outline + soft top-left highlight
  square    : flat color tile (used for walls, floors)
  outline   : tiny pixel-art "character" (head circle + body triangle)
  line      : thin horizontal warning bar (e.g. drop-line in Suika)

Outputs PNG with proper alpha.  Antialiased via 4x supersample +
LANCZOS downsample.  Pixel-art games can pass --no-aa for crisp edges.
"""
from __future__ import annotations

from pathlib import Path
from typing import Optional, Tuple

try:
    from PIL import Image, ImageDraw
except ImportError as e:
    raise SystemExit("sprite_proc requires Pillow.  pip install Pillow") from e


RGBA = Tuple[int, int, int, int]


def _parse_color(spec: str) -> RGBA:
    """Accept '#ff0000', 'ff0000', 'rgb(255,0,0)', 'rgba(255,0,0,255)'."""
    s = spec.strip().lower().replace(" ", "")
    if s.startswith("rgba(") and s.endswith(")"):
        parts = [int(p) for p in s[5:-1].split(",")]
        return (parts[0], parts[1], parts[2], parts[3] if len(parts) > 3 else 255)
    if s.startswith("rgb(") and s.endswith(")"):
        parts = [int(p) for p in s[4:-1].split(",")]
        return (parts[0], parts[1], parts[2], 255)
    if s.startswith("#"):
        s = s[1:]
    if len(s) in (3, 4):
        s = "".join(c * 2 for c in s)
    if len(s) == 6:
        return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)
    if len(s) == 8:
        return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), int(s[6:8], 16))
    raise ValueError(f"can't parse color: {spec!r}")


def _darken(c: RGBA, factor: float = 0.55) -> RGBA:
    return (int(c[0] * factor), int(c[1] * factor), int(c[2] * factor), c[3])


def gen_circle(size: int, fill: RGBA, outline: Optional[RGBA] = None,
               highlight: bool = True, aa: bool = True) -> Image.Image:
    """Disc with optional outline + top-left highlight."""
    outline = outline or _darken(fill)
    if aa:
        big = size * 4
        im = Image.new("RGBA", (big, big), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        pad = 2 * 4
        d.ellipse([pad, pad, big - pad, big - pad], fill=fill, outline=outline, width=8)
        if highlight:
            hsize = big // 4
            hx, hy = big // 4, big // 4
            d.ellipse([hx, hy, hx + hsize, hy + hsize], fill=(255, 255, 255, 220))
        return im.resize((size, size), Image.LANCZOS)
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(im).ellipse([1, 1, size - 1, size - 1], fill=fill, outline=outline)
    return im


def gen_square(size: int, fill: RGBA) -> Image.Image:
    return Image.new("RGBA", (size, size), fill)


def gen_line(w: int = 16, h: int = 2, fill: RGBA = (240, 80, 80, 200)) -> Image.Image:
    return Image.new("RGBA", (w, h), fill)


def gen_outline(size: int, fill: RGBA, outline: Optional[RGBA] = None) -> Image.Image:
    """Tiny pixel-art character: head circle + body triangle."""
    outline = outline or _darken(fill)
    big = size * 4
    im = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    head_r = big // 6
    head_cx, head_cy = big // 2, head_r + big // 12
    d.ellipse([head_cx - head_r, head_cy - head_r, head_cx + head_r, head_cy + head_r],
              fill=fill, outline=outline, width=6)
    # body trapezoid
    bx0 = big // 4
    bx1 = big - big // 4
    by0 = head_cy + head_r + big // 16
    by1 = big - big // 8
    d.polygon([(bx0 + big // 16, by0), (bx1 - big // 16, by0), (bx1, by1), (bx0, by1)],
              fill=fill, outline=outline)
    return im.resize((size, size), Image.LANCZOS)


def generate(shape: str, size: int, color_spec: str,
             out: Path, outline_spec: Optional[str] = None,
             highlight: bool = True, aa: bool = True,
             w: Optional[int] = None, h: Optional[int] = None) -> Path:
    """Top-level dispatch.  Returns the path written."""
    fill = _parse_color(color_spec)
    outline = _parse_color(outline_spec) if outline_spec else None
    shape = shape.lower()
    if shape == "circle":
        im = gen_circle(size, fill, outline, highlight=highlight, aa=aa)
    elif shape == "square":
        im = gen_square(size, fill)
    elif shape == "line":
        im = gen_line(w or 16, h or 2, fill)
    elif shape == "outline":
        im = gen_outline(size, fill, outline)
    else:
        raise ValueError(f"unknown shape: {shape!r}. supported: circle, square, line, outline")
    out.parent.mkdir(parents=True, exist_ok=True)
    im.save(out)
    print(f"[sprite_proc] {shape} {size}x{size} -> {out}")
    return out
