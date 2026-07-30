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
# 9:16 band plan. Anything below ~1650 is covered by the Shorts player UI
# (title / channel / description), so the legal band must sit above it.
CAPTION_Y = 1400        # burned narration captions
DISCLOSURE_Y = 1524     # mandatory fan-content + AI lines

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
ACCENT = (255, 92, 141)      # headline type accent (not a data encoding)
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


def fit(d, s, kind, size, max_w):
    """Shrink a string until it fits `max_w`.

    The display face is far wider than the text face at the same nominal size,
    so a size that fit before the type restyle can run off the frame. Measuring
    beats guessing — every headline goes through here.
    """
    lines = s.splitlines() or [s]
    while size > 16:
        f = font(kind, size)
        if max(text_w(d, ln, f) for ln in lines) <= max_w:
            return f
        size -= 2
    return font(kind, size)


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
    """Stat tiles as a 2x2 grid in the UPPER band.

    A four-row list runs the full height and lands squarely on the footage;
    a 2x2 grid fits entirely above the video band, so the subject stays visible
    while the numbers are read. The number is still the chart — no bars.
    """
    a = fade(t)
    text(d, (W // 2, 344), S.get("summary_title", "정리하면"),
         fit(d, S.get("summary_title", "정리하면"), "display", 60, 900), dim(ACCENT, a))

    tiles = S["summary_tiles"][:4]
    col_x = (300, 782)
    row_y = (430, 560)
    for i, tile in enumerate(tiles):
        b = min(a, ease_out((t - 0.06 - i * 0.11) / 0.26))
        if b <= 0:
            continue
        cx = col_x[i % 2]
        ry = row_y[i // 2]
        text(d, (cx, ry), tile["label"],
             fit(d, tile["label"], "display", 34, 430), dim(INK_2, b))
        text(d, (cx, ry + 62), tile["value"],
             fit(d, tile["value"], "display", 70, 430), dim(INK, b))
    d.line([(W // 2, 396), (W // 2, 628)], fill=dim(GRID, a), width=1)


def scene_outro(d, t, S):
    a = fade(t, 0.06, 0.16)   # the one place a slow fade is wanted — the ending
    text(d, (W // 2, 800), S["closing"], fit(d, S["closing"], "display", 132, 940), dim(INK, a), spacing=18)
    if t > 0.35:
        b = min(a, ease_out((t - 0.35) / 0.3))
        text(d, (W // 2, 1090), S["question"], fit(d, S["question"], "display", 64, 940), dim(ACCENT, b))


def scene_hero(d, t, S):
    """Generic opening stat tile — the number is the chart."""
    a = fade(t)
    h = S["hero"]
    target = h["value"]
    shown = int(round(target * ease_out(t / 0.5))) if t < 0.5 else target
    text(d, (W // 2, 840), f"{shown:,}", font("display", 300), dim(INK, a))
    text(d, (W // 2, 1030), h["unit"], font("display", 116), dim(ACCENT, a))
    if t > 0.42:
        b = min(a, ease_out((t - 0.42) / 0.25))
        text(d, (W // 2, 1216), h["sub"], fit(d, h["sub"], "display", 60, 940), dim(INK, b), spacing=18)


def scene_chart(d, t, S):
    """Chart-position journey. Rank 1 is BEST, so the axis is inverted and the
    scale is logarithmic — a linear axis would flatten 100→98 into nothing and
    make the whole story look like a single instant spike."""
    a = fade(t)
    c = S["chart"]
    pts = c["points"]
    text(d, (W // 2, 402), c["title"], fit(d, c["title"], "display", 58, 940), dim(ACCENT, a))

    x0, x1 = 150, W - 150
    ytop, ybot = 700, 1240

    def px(i):
        return x0 + (x1 - x0) * (i / max(len(pts) - 1, 1))

    def py(rank):
        f = math.log10(max(rank, 1)) / math.log10(c.get("axis_max", 100))
        return ytop + (ybot - ytop) * clamp(f)

    for rank in c.get("gridlines", [1, 10, 100]):
        gy = py(rank)
        d.line([(x0, gy), (x1, gy)], fill=dim(GRID, a), width=1)
        text(d, (x0 - 20, gy), f"#{rank}", font("display", 34), dim(MUTED, a), anchor="rm")

    prog = ease_in_out(clamp(t / 0.62)) * (len(pts) - 1)
    poly = []
    for i, p in enumerate(pts):
        if i > prog:
            break
        poly.append((px(i), py(p["rank"])))
    if prog > len(poly) - 1 and len(poly) >= 1 and len(poly) < len(pts):
        i = len(poly) - 1
        f = prog - i
        nx, ny = px(i + 1), py(pts[i + 1]["rank"])
        poly.append((poly[i][0] + (nx - poly[i][0]) * f,
                     poly[i][1] + (ny - poly[i][1]) * f))
    if len(poly) > 1:
        d.line(poly, fill=dim(SERIES_1, a), width=6, joint="curve")

    for i, p in enumerate(pts):
        if i > prog:
            continue
        cx, cy = px(i), py(p["rank"])
        d.ellipse([cx - 13, cy - 13, cx + 13, cy + 13], fill=dim(SERIES_1, a))
        d.ellipse([cx - 13, cy - 13, cx + 13, cy + 13], outline=dim(SURFACE, a), width=3)
        mid = 0 < i < len(pts) - 1
        anch = "lm" if i == 0 else ("rm" if i == len(pts) - 1 else "mm")
        dx = 30 if i == 0 else (-30 if i == len(pts) - 1 else 0)
        # middle markers sit on a gridline, so lift their labels clear of it
        text(d, (cx + dx, cy - (104 if mid else 54)), p["label"],
             font("display", 44), dim(INK, a), anchor=anch)
        text(d, (cx + dx, cy - (64 if mid else 16)), p["when"],
             font("display", 30), dim(ACCENT, a), anchor=anch)

    if t > 0.7:
        b = min(a, ease_out((t - 0.7) / 0.24))
        text(d, (W // 2, 1432), c["caption"], fit(d, c["caption"], "display", 78, 940), dim(INK, b), spacing=16)


def scene_chain(d, t, S):
    """The causal chain, dated. Each step reveals in order — the order IS the claim."""
    a = fade(t)
    ch = S["chain"]
    text(d, (W // 2, 372), ch["title"], fit(d, ch["title"], "display", 68, 900), dim(ACCENT, a))
    steps = ch["steps"]
    top, gap = 556, 166
    for i, s in enumerate(steps):
        b = min(a, ease_out((t - 0.08 - i * 0.15) / 0.24))
        if b <= 0:
            continue
        y = top + i * gap
        if i > 0:
            d.line([(160, y - gap + 30), (160, y - 30)], fill=dim(SERIES_1, b * 0.6), width=4)
        d.ellipse([146, y - 14, 174, y + 14], fill=dim(SERIES_1, b))
        text(d, (216, y - 30), s["when"], fit(d, s["when"], "display", 38, 760), dim(ACCENT, b), anchor="lm")
        text(d, (216, y + 24), s["what"], fit(d, s["what"], "display", 52, 800), dim(INK, b), anchor="lm")


def scene_slot(d, t, S):
    """Placeholder for cleared subject footage.

    Rendered as an explicit, labelled empty frame — never a fake. If the clip is
    never cleared this beat is cut, not shipped as-is.
    """
    a = fade(t)
    sl = S["slot"]
    x0, y0, x1, y1 = 90, 620, W - 90, 1360
    for i in range(0, (x1 - x0), 28):
        d.line([(x0 + i, y0), (x0 + i, y1)], fill=dim((24, 24, 23), a), width=1)
    d.rectangle([x0, y0, x1, y1], outline=dim(AXIS, a), width=3)
    text(d, (W // 2, (y0 + y1) / 2 - 40), sl["label"], font("text", 46), dim(INK_2, a))
    text(d, (W // 2, (y0 + y1) / 2 + 40), sl["note"], font("text", 34), dim(MUTED, a))


def scene_calendar(d, t, S):
    """A month as a real weekday grid — the core form of schedule analysis.

    A day either had a publicly-known engagement or it didn't; that is a binary,
    so it gets emphasis (one hue on/off), NOT a value ramp. Cells that carry a
    second fact (a multi-city day) get a marker, never a second hue.
    """
    a = fade(t)
    c = S["calendar"]
    text(d, (W // 2, 400), c["title"], font("text", 52), dim(MUTED, a))

    active = set(c["active_days"])
    starred = set(c.get("star_days", []))
    n_days, first_col = c["days_in_month"], c["first_weekday_col"]

    cols, cw, gap = 7, 112, 10
    grid_w = cols * cw + (cols - 1) * gap
    x0, y0 = (W - grid_w) // 2, 640
    for i, lab in enumerate(["월", "화", "수", "목", "금", "토", "일"]):
        text(d, (x0 + i * (cw + gap) + cw / 2, y0 - 42), lab, font("text", 30), dim(MUTED, a))

    shown = 0
    for day in range(1, n_days + 1):
        idx = first_col + day - 1
        cx = x0 + (idx % cols) * (cw + gap)
        cy = y0 + (idx // cols) * (cw + gap)
        b = min(a, ease_out((t - 0.10 - (day / n_days) * 0.38) / 0.20))
        if b <= 0:
            continue
        on = day in active
        if on:
            shown += 1
        d.rounded_rectangle([cx, cy, cx + cw, cy + cw], radius=10,
                            fill=dim(SERIES_1, b) if on else dim((26, 26, 25), b))
        text(d, (cx + cw / 2, cy + cw / 2), str(day), font("text", 34),
             dim(INK if on else MUTED, b))
        if day in starred:
            d.ellipse([cx + cw - 24, cy + 9, cx + cw - 9, cy + 24], fill=dim(INK, b))

    hb = min(a, ease_out((t - 0.52) / 0.22))
    if hb > 0:
        text(d, (W // 2, 1352), f"{len(active)}일", font("display", 126), dim(INK, hb))
        text(d, (W // 2, 1444), c["hero_sub"], font("text", 38), dim(MUTED, hb))


def scene_streak(d, t, S):
    """Two schedule statistics that only exist once you have the calendar."""
    a = fade(t)
    st = S["streak"]
    x0, y = 100, 760
    strip_w = W - 200
    n = st["days_in_month"]
    cell = strip_w / n
    active = set(st["active_days"])
    lo, hi = st["run_from"], st["run_to"]

    text(d, (W // 2, 470), st["title"], font("text", 52), dim(MUTED, a))
    for i in range(n):
        day = i + 1
        x = x0 + i * cell
        inrun = lo <= day <= hi
        b = min(a, ease_out((t - 0.08 - (i / n) * 0.24) / 0.2))
        if b <= 0:
            continue
        col = SERIES_1 if day in active else (26, 26, 25)
        h = 96 if inrun else (60 if day in active else 30)
        alpha = b if (inrun or day not in active) else b * 0.42
        d.rectangle([x + 1, y + (96 - h), x + cell - 2, y + 96], fill=dim(col, alpha))

    rb = min(a, ease_out((t - 0.32) / 0.22))
    if rb > 0:
        rx0, rx1 = x0 + (lo - 1) * cell, x0 + hi * cell - 2
        d.line([(rx0, y + 124), (rx1, y + 124)], fill=dim(INK, rb), width=3)
        for rx in (rx0, rx1):
            d.line([(rx, y + 112), (rx, y + 136)], fill=dim(INK, rb), width=3)
        text(d, ((rx0 + rx1) / 2, y + 178), st["run_label"], font("text", 38), dim(INK, rb))

    for i, row in enumerate(st["stats"]):
        b = min(a, ease_out((t - 0.50 - i * 0.14) / 0.24))
        if b <= 0:
            continue
        ry = 1180 + i * 132
        d.line([(120, ry + 74), (W - 120, ry + 74)], fill=dim(GRID, b), width=1)
        text(d, (120, ry), row["label"], font("text", 42), dim(MUTED, b), anchor="lm")
        text(d, (W - 120, ry - 4), row["value"], font("display", 76), dim(INK, b), anchor="rm")


def scene_route(d, t, S):
    """One day's stops as a route diagram — straight-line km between waypoints.

    Not a map: a map would imply a road path we did not measure. The diagram
    states exactly what it computes — great-circle distance between two places.
    """
    a = fade(t)
    r = S["route"]
    text(d, (W // 2, 430), r["title"], font("display", 108), dim(INK, a))
    text(d, (W // 2, 540), r["subtitle"], font("text", 42), dim(MUTED, a))

    stops = r["stops"]
    total = 0.0
    legs = []
    for i in range(len(stops) - 1):
        km = haversine_km(stops[i]["lat"], stops[i]["lon"],
                          stops[i + 1]["lat"], stops[i + 1]["lon"])
        legs.append(km)
        total += km

    top, bottom = 700, 1180
    step = (bottom - top) / max(len(stops) - 1, 1)
    cx = 260
    for i, s in enumerate(stops):
        y = top + i * step
        b = min(a, ease_out((t - 0.12 - i * 0.16) / 0.22))
        if b <= 0:
            continue
        if i > 0:
            py = top + (i - 1) * step
            seg = min(1.0, max(0.0, (t - 0.12 - (i - 1) * 0.16) / 0.16))
            d.line([(cx, py), (cx, py + (y - py) * ease_out(seg))],
                   fill=dim(SERIES_1, b), width=6)
            text(d, (cx + 46, (py + y) / 2), f"{legs[i - 1]:,.0f} km",
                 font("text", 36), dim(SERIES_1, b), anchor="lm")
        d.ellipse([cx - 16, y - 16, cx + 16, y + 16], fill=dim(SERIES_1, b))
        d.ellipse([cx - 16, y - 16, cx + 16, y + 16], outline=dim(SURFACE, b), width=3)
        text(d, (cx + 46, y - 22), s["place"], font("text", 46), dim(INK, b), anchor="lm")
        text(d, (cx + 46, y + 22), s["what"], font("text", 34), dim(MUTED, b), anchor="lm")

    fb = min(a, ease_out((t - 0.66) / 0.24))
    if fb > 0:
        label = r.get("total_label", "합계 {km} km").replace("{km}", f"{total:,.0f}")
        text(d, (W // 2, 1290), label, font("display", 80), dim(INK, fb))
        text(d, (W // 2, 1366), "정류지 간 직선거리 합", font("text", 30), dim(MUTED, fb))
    nb = min(a, ease_out((t - 0.80) / 0.20))
    if nb > 0 and r.get("note"):
        text(d, (W // 2, 1452), r["note"], font("text", 38), dim(INK_2, nb), spacing=10)


SCENES = {
    "hook": scene_hook, "days": scene_days, "month": scene_month, "globe": scene_globe,
    "scale": scene_scale, "timezone": scene_timezone, "summary": scene_summary,
    "outro": scene_outro, "calendar": scene_calendar, "streak": scene_streak,
    "route": scene_route, "hero": scene_hero, "chart": scene_chart,
    "chain": scene_chain, "slot": scene_slot,
}


# ── chrome (persistent) ───────────────────────────────────────────────────────
def draw_captions(d, S, t_sec):
    """Burned captions in the lower band.

    Timings come from the TTS engine's own character alignment (see
    beat-narration.py), not from ASR — so they cannot drift out of sync.
    """
    caps = S.get("_captions") or []
    for c in caps:
        if c["start"] - 0.05 <= t_sec <= c["end"] + 0.22:
            f = fit(d, c["text"], "display", 60, 930)
            bb = d.textbbox((W // 2, CAPTION_Y), c["text"], font=f, anchor="mm")
            d.rounded_rectangle([bb[0] - 28, bb[1] - 18, bb[2] + 28, bb[3] + 18],
                                radius=18, fill=(0, 0, 0))
            text(d, (W // 2, CAPTION_Y), c["text"], f, INK)
            return


def draw_chrome(d, S, t_global, total):
    text(d, (60, 118), S["brand"], font("text", 26), dim(MUTED, 0.85), anchor="lm")
    if S.get("fixed_title"):
        ft = fit(d, S["fixed_title"], "display", 62, 940)
        text(d, (W // 2, 218), S["fixed_title"], ft, INK, spacing=10)
    # progress hairline — recessive, one shade off the surface
    d.line([(0, 2), (W * (t_global / total), 2)], fill=GRID, width=4)
    # Legibility at Shorts scale is won by WEIGHT and CONTRAST, not point size:
    # 26px of a 1080-wide frame is ~10px on a phone. Heavy face + near-white +
    # short lines reads small and stays out of the way. A dark plate is not an
    # option — the layer is screen-blended, so black is transparent.
    y = DISCLOSURE_Y
    for i, line in enumerate(S["disclosures"]):
        f = fit(d, line, "display", 30 - i * 2, 950)
        text(d, (W // 2, y + i * 44), line, f, dim(INK, 0.94 - i * 0.06))
    text(d, (W // 2, y + 44 * len(S["disclosures"]) + 12),
         S["source_note"], font("text", 20), dim(MUTED, 0.85))


# ── render ────────────────────────────────────────────────────────────────────
def render(spec_path, out_path, fps=FPS_DEFAULT, preview_only=False):
    S = json.loads(Path(spec_path).read_text(encoding="utf-8"))

    # derive geo values so the spec can't drift from the math (skip when this
    # episode has no origin/dest pair — not every episode is about a journey)
    A, B = S.get("origin"), S.get("dest")
    if A and B:
        S["distance_km"] = int(round(haversine_km(A["lat"], A["lon"], B["lat"], B["lon"])))
        S["round_trip_km"] = S["distance_km"] * 2
    S.setdefault("earth_circumference_km", 40075)
    # A graphics layer destined to be screen-blended over footage must sit on
    # PURE black — any lift in the surface colour lightens the whole frame.
    if S.get("surface"):
        global SURFACE
        SURFACE = tuple(S["surface"])
    # Tiles quote the derived distance via a token so a hand-typed number in the
    # spec can never drift from the geometry actually drawn on screen.
    for tile in S.get("summary_tiles", []):
        for token in ("distance_km", "round_trip_km"):
            if token in S:
                tile["value"] = tile["value"].replace("{%s}" % token, f"{S[token]:,}")

    cap = Path(spec_path).parent / S.get("captions_file", "")
    if S.get("captions_file") and cap.is_file():
        S["_captions"] = json.loads(cap.read_text(encoding="utf-8"))
        print(f"[data-chart] captions: {len(S['_captions'])} cues from {cap.name}")
    beats = S["beats"]
    total_f = sum(b["frames"] for b in beats)
    total_s = total_f / fps
    print(f"[data-chart] {len(beats)} beats · {total_f} frames · {total_s:.1f}s @ {fps}fps")
    if A and B:
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
            draw_captions(d, S, (f0 + b["frames"] * 0.82) / fps)
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
            draw_captions(d, S, f_global / fps)
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
