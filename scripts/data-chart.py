#!/usr/bin/env python3
"""data-chart.py — data-graphics renderer for the schedule-data shorts series.

Renders a full 9:16 vertical video ENTIRELY from self-drawn data graphics —
no stock B-roll, no third-party media, no subject imagery. That is both the
visual identity of the series and its legal posture: every pixel is ours.

Design rules come from the `dataviz` skill:
  - dark surface, one accent hue; two identities use validated categorical
    slots 1+2 (#3987e5 / #d95926 — six checks PASS vs surface #0d0d0d)
  - hero number / stat tile instead of a one-bar chart
  - hairline recessive grid, thin marks, selective direct labels
  - no dual axis, no rainbow ramp, no number on every point

Usage:
  python scripts/data-chart.py <spec.json> <out.mp4> [--fps 30] [--preview-only]

The spec carries the DATA (subject-specific, keep it in records/); this file
carries the DRAWING (reusable, committed).
"""

import json
import math
import subprocess
import sys
from datetime import date
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

# ── canvas ────────────────────────────────────────────────────────────────────
W, H = 1080, 1920
FPS_DEFAULT = 30

# ── palette (dataviz skill, dark mode, validated vs surface #0d0d0d) ─────────
SURFACE = (13, 13, 13)
INK = (255, 255, 255)
INK_2 = (195, 194, 183)
MUTED = (137, 135, 129)
GRID = (44, 44, 42)
AXIS = (56, 56, 53)
SERIES_1 = (57, 135, 229)    # blue   — slot 1 (Seoul / primary accent)
SERIES_2 = (217, 89, 38)     # orange — slot 2 (Los Angeles)
# sequential blue ramp, dark-surface direction (near-zero recedes to surface)
BLUE_DIM = (16, 66, 129)
BLUE_MID = (37, 106, 191)

REPO = Path(__file__).resolve().parents[1]
FONT_DISPLAY = REPO / "assets/fonts/BlackHanSans-Regular.ttf"
FONT_TEXT_CANDIDATES = [
    REPO / "skills/game-prototype/unity-project/Assets/Resources/Fonts/NotoSansKR.ttf",
    REPO / "skills/game-prototype/unity-project/Assets/Resources/Fonts/GowunDodum.ttf",
    FONT_DISPLAY,
]

_font_cache = {}


def font(kind, size):
    key = (kind, size)
    if key not in _font_cache:
        if kind == "display":
            path = FONT_DISPLAY
        else:
            path = next((p for p in FONT_TEXT_CANDIDATES if p.exists()), FONT_DISPLAY)
        _font_cache[key] = ImageFont.truetype(str(path), size)
    return _font_cache[key]


# ── small helpers ─────────────────────────────────────────────────────────────
def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else hi if v > hi else v


def ease_out(t):
    """Cubic ease-out — fast start, settled end. Used for every reveal."""
    return 1 - (1 - clamp(t)) ** 3


def ease_in_out(t):
    t = clamp(t)
    return 4 * t * t * t if t < 0.5 else 1 - (-2 * t + 2) ** 3 / 2


def fade(t, hold_in=0.045, hold_out=0.04):
    """Opacity envelope for a scene-local normalized time.

    Kept deliberately tight: a long envelope on both sides of a beat boundary
    leaves ~1s of near-empty screen at every cut, which reads as dead air in a
    short. Shorts want the cut, not the dissolve.
    """
    t = clamp(t)
    a = clamp(t / hold_in) if hold_in > 0 else 1.0
    b = clamp((1 - t) / hold_out) if hold_out > 0 else 1.0
    return min(a, b)


def mix(c1, c2, t):
    t = clamp(t)
    return tuple(int(round(a + (b - a) * t)) for a, b in zip(c1, c2))


def dim(color, alpha):
    """Composite `color` over the surface at `alpha` — cheap opacity on RGB."""
    return mix(SURFACE, color, alpha)


def text(d, xy, s, f, fill, anchor="mm", spacing=14):
    if "\n" in s:
        d.multiline_text(xy, s, font=f, fill=fill, anchor=anchor,
                         align="center", spacing=spacing)
    else:
        d.text(xy, s, font=f, fill=fill, anchor=anchor)


def text_w(d, s, f):
    return d.textbbox((0, 0), s, font=f)[2]


def rounded_line(d, x0, y0, x1, y1, width, fill):
    """A thin mark with rounded data-ends (dataviz mark spec)."""
    d.line([(x0, y0), (x1, y1)], fill=fill, width=width)
    r = width / 2
    for (cx, cy) in ((x0, y0), (x1, y1)):
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill)


def count_up(target, t, decimals=0):
    """Roll a number to `target` on eased local time."""
    v = target * ease_out(t)
    return f"{v:,.{decimals}f}"


# ── geo ───────────────────────────────────────────────────────────────────────
def to_vec(lat, lon):
    la, lo = math.radians(lat), math.radians(lon)
    return (math.cos(la) * math.cos(lo), math.cos(la) * math.sin(lo), math.sin(la))


def to_latlon(v):
    x, y, z = v
    return math.degrees(math.asin(clamp(z, -1, 1))), math.degrees(math.atan2(y, x))


def slerp(v1, v2, t):
    dot = clamp(sum(a * b for a, b in zip(v1, v2)), -1, 1)
    omega = math.acos(dot)
    if omega < 1e-9:
        return v1
    s1, s2 = math.sin((1 - t) * omega) / math.sin(omega), math.sin(t * omega) / math.sin(omega)
    return tuple(a * s1 + b * s2 for a, b in zip(v1, v2))


def haversine_km(lat1, lon1, lat2, lon2, r=6371.0088):
    p1, p2 = math.radians(lat1), math.radians(lat2)
    dp, dl = math.radians(lat2 - lat1), math.radians(lon2 - lon1)
    a = math.sin(dp / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(dl / 2) ** 2
    return 2 * r * math.asin(math.sqrt(a))


def ortho(lat, lon, lat0, lon0, cx, cy, radius):
    """Orthographic projection. Returns (x, y, visible)."""
    la, lo = math.radians(lat), math.radians(lon)
    la0, lo0 = math.radians(lat0), math.radians(lon0)
    cos_c = math.sin(la0) * math.sin(la) + math.cos(la0) * math.cos(la) * math.cos(lo - lo0)
    x = math.cos(la) * math.sin(lo - lo0)
    y = math.cos(la0) * math.sin(la) - math.sin(la0) * math.cos(la) * math.cos(lo - lo0)
    return cx + x * radius, cy - y * radius, cos_c >= 0


def draw_globe(d, cx, cy, radius, lat0, lon0, alpha=1.0):
    """Wireframe globe — hairline graticule only. Recessive by design."""
    # faint body so the sphere separates from space; still one shade off surface
    d.ellipse([cx - radius, cy - radius, cx + radius, cy + radius],
              fill=dim((22, 26, 32), alpha))
    d.ellipse([cx - radius, cy - radius, cx + radius, cy + radius],
              outline=dim(AXIS, alpha), width=2)
    for lat in range(-60, 61, 30):
        pts = []
        for lon in range(-180, 181, 3):
            x, y, vis = ortho(lat, lon, lat0, lon0, cx, cy, radius)
            pts.append((x, y) if vis else None)
        _polyline(d, pts, dim(GRID, alpha), 1)
    for lon in range(-180, 180, 30):
        pts = []
        for lat in range(-90, 91, 3):
            x, y, vis = ortho(lat, lon, lat0, lon0, cx, cy, radius)
            pts.append((x, y) if vis else None)
        _polyline(d, pts, dim(GRID, alpha), 1)


def _polyline(d, pts, fill, width):
    run = []
    for p in pts:
        if p is None:
            if len(run) > 1:
                d.line(run, fill=fill, width=width)
            run = []
        else:
            run.append(p)
    if len(run) > 1:
        d.line(run, fill=fill, width=width)


# ══════════════════════════════════════════════════════════════════════════════
# scenes
# ══════════════════════════════════════════════════════════════════════════════
def scene_hook(d, t, S):
    """Hero number. The story is one number — so it is a stat tile, not a chart."""
    a = fade(t)
    n = S["days_to_first_win"]
    shown = int(round(n * ease_out(t / 0.55))) if t < 0.55 else n
    text(d, (W // 2, 860), f"{shown:,}", font("display", 300), dim(INK, a))
    text(d, (W // 2, 1046), "일", font("display", 96), dim(INK_2, a))
    if t > 0.5:
        b = min(a, ease_out((t - 0.5) / 0.25))
        text(d, (W // 2, 1210), "데뷔하고\n처음 1위를 하기까지", font("text", 62), dim(INK_2, b))


def scene_days(d, t, S):
    """835 days as a swept timeline. Selective labels: the two ends only."""
    a = fade(t)
    x0, x1, y = 120, W - 120, 980
    p = ease_in_out(clamp(t / 0.62))

    d.line([(x0, y), (x1, y)], fill=dim(AXIS, a), width=2)
    # year ticks — hairline, recessive
    for frac, lab in S["day_axis_ticks"]:
        tx = x0 + (x1 - x0) * frac
        d.line([(tx, y - 16), (tx, y + 16)], fill=dim(GRID, a), width=2)
        text(d, (tx, y + 52), lab, font("text", 34), dim(MUTED, a))

    head = x0 + (x1 - x0) * p
    rounded_line(d, x0, y, head, y, 8, dim(SERIES_1, a))
    d.ellipse([head - 13, y - 13, head + 13, y + 13], fill=dim(INK, a))

    shown = int(round(S["days_to_first_win"] * p))
    text(d, (W // 2, 690), f"{shown:,}일", font("display", 150), dim(INK, a))
    text(d, (W // 2, 806), S["days_to_first_win_human"], font("text", 46), dim(MUTED, a))
    text(d, (x0, y - 70), S["debut_label"], font("text", 36), dim(MUTED, a), anchor="lm")

    if p > 0.98:
        b = min(a, ease_out((t - 0.62) / 0.2))
        text(d, (x1, y - 70), S["first_win_label"], font("text", 40), dim(SERIES_1, b), anchor="rm")
        text(d, (W // 2, 1180), S["first_win_caption"], font("text", 56), dim(INK, b))
        if S.get("first_win_sub"):
            text(d, (W // 2, 1264), S["first_win_sub"], font("text", 38), dim(MUTED, b))


def scene_month(d, t, S):
    """One month as a day strip, with only the verified milestone days marked.

    Deliberately NOT a full activity heatmap: we know the days that were
    publicly reported, not the group's actual calendar. Marking only what is
    sourced — and saying so in the strip's own label — is the honest form.
    """
    a = fade(t)
    m = S["month"]
    text(d, (W // 2, 470), m["title"], font("display", 96), dim(INK, a))
    text(d, (W // 2, 574), m["subtitle"], font("text", 42), dim(MUTED, a))

    n = m["days_in_month"]
    x0, y, strip_w = 100, 760, W - 200
    cell = strip_w / n
    marks = {d_["day"]: d_ for d_ in m["marks"]}
    for i in range(n):
        day = i + 1
        x = x0 + i * cell
        on = day in marks
        b = min(a, ease_out((t - 0.10 - (i / n) * 0.30) / 0.25))
        col = dim(SERIES_1, b) if on else dim(GRID, b * 0.9)
        h = 84 if on else 40
        d.rectangle([x + 1, y + (84 - h), x + cell - 2, y + 84], fill=col)

    text(d, (x0, y + 128), m["strip_label"], font("text", 30), dim(MUTED, a), anchor="lm")

    hero_b = min(a, ease_out((t - 0.34) / 0.25))
    if hero_b > 0:
        text(d, (W // 2, 1010), m["hero"], font("display", 128), dim(INK, hero_b))

    for i, row in enumerate(m["rows"]):
        b = min(a, ease_out((t - 0.46 - i * 0.10) / 0.26))
        if b <= 0:
            continue
        ry = 1180 + i * 92
        d.ellipse([120, ry - 8, 136, ry + 8], fill=dim(SERIES_1, b))
        text(d, (172, ry), row["date"], font("text", 42), dim(INK, b), anchor="lm")
        text(d, (W - 120, ry), row["what"], font("text", 42), dim(INK_2, b), anchor="rm")


def scene_globe(d, t, S):
    """Pivot to distance. Arc draws; the counter is the label."""
    a = fade(t)
    cx, cy, r = W // 2, 1000, 380
    A, B = S["origin"], S["dest"]
    v1, v2 = to_vec(A["lat"], A["lon"]), to_vec(B["lat"], B["lon"])
    mlat, mlon = to_latlon(slerp(v1, v2, 0.5))
    # An orthographic view centred ON the path renders that great circle as a
    # STRAIGHT line (a great circle through the projection centre always does).
    # Drop the viewpoint south of the midpoint so the polar route reads as the
    # arc it actually is; both endpoints stay on the visible hemisphere.
    mlat -= S.get("globe_view_tilt_deg", 34)

    if t < 0.12:
        text(d, (W // 2, 940), S["pivot_line"], font("text", 66), dim(INK, fade(t / 0.12, 0.25, 0.25)))
        return

    tt = (t - 0.12) / 0.88
    draw_globe(d, cx, cy, r, mlat, mlon, a)

    p = ease_in_out(clamp(tt / 0.74))
    pts = []
    for i in range(121):
        f = i / 120
        if f > p:
            break
        lat, lon = to_latlon(slerp(v1, v2, f))
        x, y, vis = ortho(lat, lon, mlat, mlon, cx, cy, r)
        pts.append((x, y) if vis else None)
    _polyline(d, pts, dim(SERIES_1, a), 6)

    for pt, col, lab, side in ((A, SERIES_1, A["label"], -1), (B, SERIES_2, B["label"], 1)):
        x, y, vis = ortho(pt["lat"], pt["lon"], mlat, mlon, cx, cy, r)
        if not vis:
            continue
        show = a if side < 0 else min(a, ease_out((tt - 0.68) / 0.18))
        if show <= 0:
            continue
        d.ellipse([x - 12, y - 12, x + 12, y + 12], fill=dim(col, show))
        d.ellipse([x - 12, y - 12, x + 12, y + 12], outline=dim(SURFACE, show), width=2)
        # keep the direct label inside the frame — a point near the limb would
        # otherwise push its own name off-canvas
        lf = font("text", 38)
        half = text_w(d, lab, lf) / 2
        lx = min(max(x, half + 40), W - half - 40)
        text(d, (lx, y - 44), lab, lf, dim(INK, show))

    text(d, (W // 2, 470), f"{count_up(S['distance_km'], clamp(tt / 0.74))} km",
         font("display", 130), dim(INK, a))
    text(d, (W // 2, 570), "편도", font("text", 40), dim(MUTED, a))
    if tt > 0.76:
        b = min(a, ease_out((tt - 0.76) / 0.2))
        text(d, (W // 2, 1470), S["dest_caption"], font("text", 52), dim(INK_2, b))


def scene_scale(d, t, S):
    """Round trip vs Earth's circumference — part-to-whole, one ring, one arc."""
    a = fade(t)
    cx, cy, r = W // 2, 860, 300
    pct = S["round_trip_km"] / S["earth_circumference_km"]
    p = ease_out(clamp(t / 0.55))

    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=dim(AXIS, a), width=10)
    if p > 0:
        d.arc([cx - r, cy - r, cx + r, cy + r], -90, -90 + 360 * pct * p,
              fill=dim(SERIES_1, a), width=10)
    text(d, (cx, cy - 40), f"{pct * p * 100:.0f}%", font("display", 150), dim(INK, a))
    text(d, (cx, cy + 78), "지구 둘레의", font("text", 44), dim(MUTED, a))

    text(d, (W // 2, 1260), f"왕복 {S['round_trip_km']:,} km", font("display", 74), dim(INK, a))
    text(d, (W // 2, 1350), f"지구 한 바퀴 = {S['earth_circumference_km']:,} km",
         font("text", 40), dim(MUTED, a))


def scene_timezone(d, t, S):
    """Two identities, two validated hues, a legend — never color alone."""
    a = fade(t)
    off = S["timezone_offset_h"]
    text(d, (W // 2, 560), f"시차 {off}시간", font("display", 120), dim(INK, a))

    bar_w, bar_h, x0 = W - 200, 54, 100
    cell = bar_w / 24
    for row, (name, shift, col) in enumerate([
        (S["origin"]["tz_label"], 0, SERIES_1),
        (S["dest"]["tz_label"], -off, SERIES_2),
    ]):
        y = 860 + row * 200
        b = min(a, ease_out((t - 0.15 - row * 0.18) / 0.3))
        if b <= 0:
            continue
        text(d, (x0, y - 52), name, font("text", 40), dim(col, b), anchor="lm")
        for h in range(24):
            hh = (h + shift) % 24
            night = hh < 6 or hh >= 21
            fillc = dim(col, b * (0.22 if night else 0.95))
            x = x0 + h * cell
            d.rectangle([x + 1, y, x + cell - 2, y + bar_h], fill=fillc)  # 2px surface gap
            if hh == 0:
                text(d, (x + cell / 2, y + bar_h + 34), "0시", font("text", 28), dim(MUTED, b))

    if t > 0.6:
        b = min(a, ease_out((t - 0.6) / 0.3))
        text(d, (W // 2, 1330), S["timezone_caption"], font("text", 52), dim(INK, b))
        text(d, (W // 2, 1420), "밝은 칸 = 낮 · 어두운 칸 = 밤", font("text", 32), dim(MUTED, b))


def scene_summary(d, t, S):
    """Stat tiles — the number is the chart."""
    a = fade(t)
    text(d, (W // 2, 470), "정리하면", font("text", 54), dim(MUTED, a))
    tiles = S["summary_tiles"]
    for i, tile in enumerate(tiles):
        b = min(a, ease_out((t - 0.08 - i * 0.10) / 0.28))
        if b <= 0:
            continue
        y = 640 + i * 200
        d.line([(120, y + 118), (W - 120, y + 118)], fill=dim(GRID, b), width=1)
        text(d, (120, y + 34), tile["label"], font("text", 42), dim(MUTED, b), anchor="lm")
        text(d, (W - 120, y + 30), tile["value"], font("display", 82), dim(INK, b), anchor="rm")


def scene_outro(d, t, S):
    a = fade(t, 0.06, 0.16)   # the one place a slow fade is wanted — the ending
    text(d, (W // 2, 820), S["closing"], font("display", 84), dim(INK, a))
    if t > 0.35:
        b = min(a, ease_out((t - 0.35) / 0.3))
        text(d, (W // 2, 1080), S["question"], font("text", 56), dim(SERIES_1, b))


SCENES = {
    "hook": scene_hook, "days": scene_days, "month": scene_month, "globe": scene_globe,
    "scale": scene_scale, "timezone": scene_timezone, "summary": scene_summary,
    "outro": scene_outro,
}


# ── chrome (persistent) ───────────────────────────────────────────────────────
def draw_chrome(d, S, t_global, total):
    text(d, (60, 150), S["brand"], font("text", 34), MUTED, anchor="lm")
    # progress hairline — recessive, one shade off the surface
    d.line([(0, 2), (W * (t_global / total), 2)], fill=GRID, width=4)
    y = 1560
    for i, line in enumerate(S["disclosures"]):
        text(d, (W // 2, y + i * 44), line, font("text", 28), MUTED)
    text(d, (W // 2, 1700), S["source_note"], font("text", 26), (70, 70, 68))


# ── render ────────────────────────────────────────────────────────────────────
def render(spec_path, out_path, fps=FPS_DEFAULT, preview_only=False):
    S = json.loads(Path(spec_path).read_text(encoding="utf-8"))

    # derive geo values so the spec can't drift from the math
    A, B = S["origin"], S["dest"]
    S["distance_km"] = int(round(haversine_km(A["lat"], A["lon"], B["lat"], B["lon"])))
    S["round_trip_km"] = S["distance_km"] * 2
    S.setdefault("earth_circumference_km", 40075)
    # Tiles quote the derived distance via a token so a hand-typed number in the
    # spec can never drift from the geometry actually drawn on screen.
    for tile in S.get("summary_tiles", []):
        tile["value"] = (tile["value"]
                         .replace("{distance_km}", f"{S['distance_km']:,}")
                         .replace("{round_trip_km}", f"{S['round_trip_km']:,}"))

    beats = S["beats"]
    total_f = sum(b["frames"] for b in beats)
    total_s = total_f / fps
    print(f"[data-chart] {len(beats)} beats · {total_f} frames · {total_s:.1f}s @ {fps}fps")
    print(f"[data-chart] great-circle {A['label']}→{B['label']}: {S['distance_km']:,} km "
          f"(round trip {S['round_trip_km']:,} km = "
          f"{S['round_trip_km'] / S['earth_circumference_km'] * 100:.1f}% of Earth)")

    if preview_only:
        outdir = Path(out_path).parent / "preview"
        outdir.mkdir(parents=True, exist_ok=True)
        f0 = 0
        for b in beats:
            img = Image.new("RGB", (W, H), SURFACE)
            d = ImageDraw.Draw(img)
            SCENES[b["scene"]](d, 0.82, S)
            draw_chrome(d, S, f0 + b["frames"] * 0.82, total_f)
            img.save(outdir / f"{b['scene']}.png")
            f0 += b["frames"]
        print(f"[data-chart] preview stills → {outdir}")
        return

    ffmpeg = _ffmpeg_bin()
    Path(out_path).parent.mkdir(parents=True, exist_ok=True)
    proc = subprocess.Popen(
        [ffmpeg, "-y", "-loglevel", "error",
         "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(fps), "-i", "-",
         "-c:v", "libx264", "-preset", "medium", "-crf", "19",
         "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(out_path)],
        stdin=subprocess.PIPE)

    f_global = 0
    for b in beats:
        n = b["frames"]
        for i in range(n):
            img = Image.new("RGB", (W, H), SURFACE)
            d = ImageDraw.Draw(img)
            SCENES[b["scene"]](d, i / max(n - 1, 1), S)
            draw_chrome(d, S, f_global, total_f)
            proc.stdin.write(img.tobytes())
            f_global += 1
        print(f"  · {b['scene']:9s} {n:4d}f  ({f_global}/{total_f})")
    proc.stdin.close()
    rc = proc.wait()
    if rc != 0:
        sys.exit(f"[data-chart] ffmpeg failed rc={rc}")
    print(f"[data-chart] wrote {out_path}")


def _ffmpeg_bin():
    import os
    import shutil
    return os.environ.get("FFMPEG_BIN") or shutil.which("ffmpeg") or "ffmpeg"


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    flags = {a for a in sys.argv[1:] if a.startswith("--")}
    if len(args) < 2:
        sys.exit(__doc__)
    fps = FPS_DEFAULT
    for f in flags:
        if f.startswith("--fps="):
            fps = int(f.split("=", 1)[1])
    render(args[0], args[1], fps=fps, preview_only="--preview-only" in flags)
