"""gen_fruits.py — programmatic Suika fruit sprite generator.

5 tiers: cherry / orange / lemon / melon / watermelon.  Sizes grow per
tier (16/24/32/48/64 px).  Colored circle with darker outline +
2-pixel highlight for tactile look (Kenney-aesthetic-friendly).

Resourcer-agent territory.  Future [OPQ-007]: extract to
modules/asset_proc.py under game-dev-agent.
"""
from __future__ import annotations

from pathlib import Path
from PIL import Image, ImageDraw


FRUITS = [
    ("tier1_cherry",   16, (220,  40,  60), (130,  20,  35)),
    ("tier2_orange",   24, (240, 140,  40), (155,  85,  20)),
    ("tier3_lemon",    32, (240, 220,  70), (160, 140,  30)),
    ("tier4_melon",    48, (110, 200, 100), ( 50, 120,  50)),
    ("tier5_watermelon", 64, (220, 90, 120), (140, 40, 70)),
]


def gen_circle(size: int, fill, outline, hl=(255, 255, 255, 220)) -> Image.Image:
    """Render a 4x supersample circle, downsample to size for AA."""
    big = size * 4
    im = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    pad = 2 * 4
    d.ellipse([pad, pad, big - pad, big - pad], fill=fill + (255,), outline=outline + (255,), width=8)
    # Soft highlight (upper-left)
    hsize = big // 4
    hx = big // 4
    hy = big // 4
    d.ellipse([hx, hy, hx + hsize, hy + hsize], fill=hl)
    return im.resize((size, size), Image.LANCZOS)


def gen_white_pixel() -> Image.Image:
    im = Image.new("RGBA", (4, 4), (240, 240, 240, 255))
    return im


def gen_drop_line() -> Image.Image:
    """Thin horizontal warning line at the game-over threshold."""
    im = Image.new("RGBA", (16, 2), (240, 80, 80, 200))
    return im


def main():
    out_dir = Path(__file__).resolve().parent.parent / "unity-project" / "Assets" / "Sprites"
    out_dir.mkdir(parents=True, exist_ok=True)
    for name, size, fill, outline in FRUITS:
        im = gen_circle(size, fill, outline)
        out = out_dir / f"{name}.png"
        im.save(out)
        print(f"wrote {out} ({size}x{size})")
    wp = out_dir / "wall_white.png"
    gen_white_pixel().save(wp)
    print(f"wrote {wp}")
    dl = out_dir / "drop_line.png"
    gen_drop_line().save(dl)
    print(f"wrote {dl}")


if __name__ == "__main__":
    main()
