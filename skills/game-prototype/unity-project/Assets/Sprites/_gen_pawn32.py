# -*- coding: utf-8 -*-
"""_gen_pawn32.py — PawnSim Art v2: 32x32 colonist (rim) sprite sheets.

Spec: G:/ai/_artv2_staging/artv2-style-spec.md
Palette: imported from Assets/Sprites/palette.py (single source of truth).

Sheet layout per spec §3: 256x96, cols=8 (idle, walk1-4, work1-2, reserved),
rows=3 (S, E, N).  8 variants pawn32_v0..v7 (cloth x hair x skin combos).
Tools: axe32.png / pick32.png 12x12, grip anchor (3,10).
Hand anchors (frame top-left origin): S(22,20) E(21,20) N(9,20); work1 y-1, work2 y+1.
Deterministic (seed 42).  No dithering, flat fills, 1px OUTLINE_STORY.
"""
import sys, os, colorsys, random

sys.stdout.reconfigure(encoding='utf-8')

# palette.py lives next to this script — derive cross-platform (was G:/ hardcoded).
PAL_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, PAL_DIR)
from palette import (OUTLINE_STORY, OUTLINE_OBJ,
                     GRASS_DK, GRASS_MD, GRASS_LT,
                     ROCK_MD, ROCK_LT,
                     WOOD_DK, WOOD_MD, WOOD_LT,
                     SKIN_MD, SKIN_SH, HAIR_DK,
                     CLOTH_BLUE, CLOTH_RUST, CLOTH_LINEN,
                     CLOTH_BLUE_DK, CLOTH_RUST_DK, CLOTH_LINEN_DK,
                     TROUSER_BLUE, TROUSER_RUST, TROUSER_LINEN)
from PIL import Image

# staging out: env override, else G:/ on Windows, else repo-local (cross-platform).
OUT = os.environ.get("ARTV2_OUT") or (
    r"G:/ai/_artv2_staging/out" if os.name == "nt"
    else os.path.join(os.path.dirname(os.path.abspath(__file__)), "_artv2_out"))
FRAMES = os.path.join(OUT, "frames")
FRAMES_TOOL = os.path.join(OUT, "frames_tool")
for d in (OUT, FRAMES, FRAMES_TOOL):
    os.makedirs(d, exist_ok=True)

SCALE = 8  # preview magnification


# ── palette-rule-compliant derivation (HSV value scale, ±12% max) ──────
def shade(c, vmul):
    r, g, b, a = c
    h, s, v = colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)
    v = max(0.0, min(1.0, v * vmul))
    r2, g2, b2 = colorsys.hsv_to_rgb(h, s, v)
    return (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)


SKIN_TONES = [(shade(SKIN_MD, m), shade(SKIN_SH, m)) for m in (1.12, 1.00, 0.94, 0.88)]
HAIR_HI_MUL = 1.12  # top-left shine on hair cap

CLOTH_SETS = {
    'blue':  (CLOTH_BLUE,  CLOTH_BLUE_DK,  TROUSER_BLUE),
    'rust':  (CLOTH_RUST,  CLOTH_RUST_DK,  TROUSER_RUST),
    'linen': (CLOTH_LINEN, CLOTH_LINEN_DK, TROUSER_LINEN),
}

# 8 representative combos: every cloth, every hair style, every skin tone covered
VARIANTS = [
    # (cloth, hair_style, skin_idx, hair_vmul)
    ('blue',  'bang',  1, 1.00),   # v0
    ('blue',  'short', 0, 1.12),   # v1
    ('rust',  'pony',  2, 0.94),   # v2
    ('rust',  'bun',   3, 1.00),   # v3
    ('linen', 'bald',  1, 1.00),   # v4
    ('linen', 'short', 2, 0.88),   # v5
    ('blue',  'pony',  3, 1.06),   # v6
    ('rust',  'bang',  0, 0.94),   # v7
]

HAND_ANCHOR = {'S': (22, 20), 'E': (21, 20), 'N': (9, 20)}
TOOL_GRIP = (3, 10)  # in 12x12 tool sprite


# ── low-level paint helpers ────────────────────────────────────────────
def rect(im, x0, y0, x1, y1, c):
    p = im.load()
    W, H = im.size
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if 0 <= x < W and 0 <= y < H:
                p[x, y] = c


def pset(im, pts, c):
    p = im.load()
    W, H = im.size
    for x, y in pts:
        if 0 <= x < W and 0 <= y < H:
            p[x, y] = c


def outline(im, color):
    """1px outer outline: every transparent pixel 4-adjacent to body."""
    p = im.load()
    W, H = im.size
    edge = []
    for y in range(H):
        for x in range(W):
            if p[x, y][3] == 0:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < W and 0 <= ny < H and p[nx, ny][3] != 0:
                        edge.append((x, y))
                        break
    for x, y in edge:
        p[x, y] = color


def paste_clipped(dst, src, ox, oy, under=False):
    """Paste src onto dst at (ox,oy) with clipping. under=True → only on transparent dst."""
    dp, sp = dst.load(), src.load()
    DW, DH = dst.size
    SW, SH = src.size
    for sy in range(SH):
        for sx in range(SW):
            c = sp[sx, sy]
            if c[3] == 0:
                continue
            tx, ty = ox + sx, oy + sy
            if 0 <= tx < DW and 0 <= ty < DH:
                if under and dp[tx, ty][3] != 0:
                    continue
                dp[tx, ty] = c


def upscale(im, s=SCALE):
    return im.resize((im.width * s, im.height * s), Image.NEAREST)


# ── pawn painters per direction ────────────────────────────────────────
# Body proportions (spec §3): hair y2-8, face y8-14, torso y14-23,
# legs y23-29, floor margin y29-31.  bounce b shifts head/torso down 1px
# on pass frames (feet stay grounded).

def _legs_front(im, leg, trouser, b):
    """S/N legs: left x12-14, right x17-19; trouser then WOOD_DK boots.
    Lift levels for a smoother 6-frame cycle: grounded (boot y27-28) ->
    half (y26-27, 1px up) -> contact (y25-26, 2px up).  The half pose is a
    geometric in-between so the foot rises/falls 1px/frame instead of
    teleporting 2px (removes the march-y stutter)."""
    for side, x0, x1 in (('l', 12, 14), ('r', 17, 19)):
        lift2 = (leg == 'r_fwd' and side == 'l') or (leg == 'l_fwd' and side == 'r')
        lift1 = (leg == 'r_half' and side == 'l') or (leg == 'l_half' and side == 'r')
        if lift2:    # back leg raised 2px (contact)
            rect(im, x0, 23 + b, x1, 24, trouser)
            rect(im, x0, 25, x1, 26, WOOD_DK)
        elif lift1:  # back leg raised 1px (mid-stride transition)
            rect(im, x0, 23 + b, x1, 25, trouser)
            rect(im, x0, 26, x1, 27, WOOD_DK)
        else:        # grounded
            rect(im, x0, 23 + b, x1, 26, trouser)
            rect(im, x0, 27, x1, 28, WOOD_DK)


def _arms_front(im, arm, cloth, cloth_dk, skin, b, work_side):
    """S/N hanging arms + work poses.  work_side: 'r' (S, viewer right x20-21)
    or 'l' (N, viewer left x10-11 = pawn's right hand)."""
    if arm in ('idle', 'w1', 'w3'):
        dyl = {-1: -1, 0: 0, 1: 1}[{'idle': 0, 'w1': -1, 'w3': 1}[arm]]
        dyr = -dyl
        # left arm x10-11
        rect(im, 10, 15 + b + dyl, 11, 19 + b + dyl, cloth)
        rect(im, 10, 20 + b + dyl, 11, 21 + b + dyl, skin)
        # right arm x20-21 (outer column dark)
        rect(im, 20, 15 + b + dyr, 20, 19 + b + dyr, cloth)
        rect(im, 21, 15 + b + dyr, 21, 19 + b + dyr, cloth_dk)
        rect(im, 20, 20 + b + dyr, 21, 21 + b + dyr, skin)
        return
    # work pose: one arm special, the other neutral
    if work_side == 'r':
        # neutral left arm
        rect(im, 10, 15 + b, 11, 19 + b, cloth)
        rect(im, 10, 20 + b, 11, 21 + b, skin)
        if arm == 'raise':
            rect(im, 20, 15 + b, 21, 17 + b, cloth)
            rect(im, 21, 17 + b, 22, 18 + b, cloth_dk)
            rect(im, 21, 18 + b, 22, 19 + b, skin)   # hand → anchor (22,19)
        else:  # strike
            rect(im, 20, 15 + b, 21, 19 + b, cloth)
            rect(im, 21, 20 + b, 22, 21 + b, skin)   # hand → anchor (22,21)
    else:  # 'l' (N back view, pawn right hand = viewer left)
        # neutral right arm
        rect(im, 20, 15 + b, 20, 19 + b, cloth)
        rect(im, 21, 15 + b, 21, 19 + b, cloth_dk)
        rect(im, 20, 20 + b, 21, 21 + b, skin)
        if arm == 'raise':
            rect(im, 10, 15 + b, 11, 17 + b, cloth)
            rect(im, 9, 17 + b, 10, 18 + b, cloth_dk)
            rect(im, 9, 18 + b, 10, 19 + b, skin)    # hand → anchor (9,19)
        else:  # strike
            rect(im, 10, 15 + b, 11, 19 + b, cloth)
            rect(im, 9, 20 + b, 10, 21 + b, skin)    # hand → anchor (9,21)


def _torso_front(im, cloth, cloth_dk, b):
    rect(im, 11, 14 + b, 20, 14 + b, cloth)          # shoulders
    rect(im, 18, 14 + b, 20, 14 + b, cloth_dk)
    rect(im, 12, 15 + b, 19, 21 + b, cloth)          # body core
    rect(im, 18, 15 + b, 19, 21 + b, cloth_dk)       # right shade (light = top-left)
    rect(im, 12, 22 + b, 19, 22 + b, WOOD_DK)        # belt 1px


def draw_S(v, leg, arm, b):
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    cloth, cloth_dk, trouser = v['cloth']
    skin, skin_sh = v['skin']
    hair, hair_hi = v['hair_c'], v['hair_hi']
    style = v['hair']

    # face
    rect(im, 11, 8 + b, 20, 12 + b, skin)
    rect(im, 12, 13 + b, 19, 13 + b, skin)           # rounded jaw
    rect(im, 19, 9 + b, 20, 12 + b, skin_sh)         # right shade
    rect(im, 13, 13 + b, 19, 13 + b, skin_sh)        # jaw shade

    # hair
    if style == 'bald':
        rect(im, 12, 3 + b, 19, 3 + b, skin)
        rect(im, 11, 4 + b, 20, 7 + b, skin)
        rect(im, 19, 4 + b, 20, 7 + b, skin_sh)
    elif style == 'bun':
        rect(im, 14, 1 + b, 17, 2 + b, hair)         # top knot
        rect(im, 12, 3 + b, 19, 3 + b, hair)
        rect(im, 11, 4 + b, 20, 7 + b, hair)
        rect(im, 11, 8 + b, 11, 9 + b, hair)
        rect(im, 20, 8 + b, 20, 9 + b, hair)
        pset(im, [(12, 4 + b), (13, 4 + b), (14, 4 + b)], hair_hi)
    else:
        rect(im, 12, 2 + b, 19, 2 + b, hair)
        rect(im, 11, 3 + b, 20, 7 + b, hair)
        rect(im, 11, 8 + b, 11, 9 + b, hair)         # sideburns
        rect(im, 20, 8 + b, 20, 9 + b, hair)
        pset(im, [(12, 3 + b), (13, 3 + b), (14, 3 + b)], hair_hi)
        if style == 'bang':
            rect(im, 12, 8 + b, 18, 8 + b, hair)     # fringe over forehead
        elif style == 'pony':
            rect(im, 10, 6 + b, 10, 9 + b, hair)     # tail tufts peeking at sides
            rect(im, 21, 6 + b, 21, 9 + b, hair)

    # eyes 2px near-black (S/E only)
    rect(im, 13, 10 + b, 13, 11 + b, OUTLINE_STORY)
    rect(im, 18, 10 + b, 18, 11 + b, OUTLINE_STORY)

    _torso_front(im, cloth, cloth_dk, b)
    _arms_front(im, arm, cloth, cloth_dk, skin, b, work_side='r')
    _legs_front(im, leg, trouser, b)

    outline(im, OUTLINE_STORY)
    # 1px ground-contact shadow
    p = im.load()
    for x in range(12, 20):
        p[x, 29] = OUTLINE_STORY
    return im


def draw_N(v, leg, arm, b):
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    cloth, cloth_dk, trouser = v['cloth']
    skin, skin_sh = v['skin']
    hair, hair_hi = v['hair_c'], v['hair_hi']
    style = v['hair']

    # back of head: hair covers face region (no face/eyes)
    if style == 'bald':
        rect(im, 12, 3 + b, 19, 3 + b, skin)
        rect(im, 11, 4 + b, 20, 12 + b, skin)
        rect(im, 19, 4 + b, 20, 12 + b, skin_sh)
        rect(im, 13, 13 + b, 18, 13 + b, skin)       # neck
    elif style == 'bun':
        rect(im, 14, 1 + b, 17, 2 + b, hair)
        rect(im, 12, 3 + b, 19, 3 + b, hair)
        rect(im, 11, 4 + b, 20, 10 + b, hair)
        rect(im, 12, 11 + b, 19, 11 + b, hair)
        rect(im, 13, 12 + b, 18, 13 + b, skin)       # neck
        pset(im, [(12, 4 + b), (13, 4 + b), (14, 4 + b)], hair_hi)
    else:
        rect(im, 12, 2 + b, 19, 2 + b, hair)
        rect(im, 11, 3 + b, 20, 10 + b, hair)
        rect(im, 12, 11 + b, 19, 11 + b, hair)
        rect(im, 13, 12 + b, 18, 13 + b, skin)       # neck
        pset(im, [(12, 3 + b), (13, 3 + b), (14, 3 + b)], hair_hi)
        if style == 'pony':
            rect(im, 14, 11 + b, 17, 16 + b, hair)   # tail down over torso
            rect(im, 15, 17 + b, 16, 17 + b, hair)

    _torso_front(im, cloth, cloth_dk, b)
    _arms_front(im, arm, cloth, cloth_dk, skin, b, work_side='l')
    if style == 'pony':                               # tail over torso top
        rect(im, 14, 14 + b, 17, 16 + b, hair)
        rect(im, 15, 17 + b, 16, 17 + b, hair)
    _legs_front(im, leg, trouser, b)

    outline(im, OUTLINE_STORY)
    p = im.load()
    for x in range(12, 20):
        p[x, 29] = OUTLINE_STORY
    return im


def draw_E(v, leg, arm, b):
    """Side view facing east (+x).  W = runtime flipX."""
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    cloth, cloth_dk, trouser = v['cloth']
    trouser_far = shade(trouser, 0.88)
    skin, skin_sh = v['skin']
    hair, hair_hi = v['hair_c'], v['hair_hi']
    style = v['hair']

    # head: hair back x12-13, face front x14-19
    rect(im, 14, 8 + b, 19, 12 + b, skin)
    rect(im, 15, 13 + b, 19, 13 + b, skin)
    rect(im, 15, 13 + b, 19, 13 + b, skin_sh)        # jaw shade
    rect(im, 19, 12 + b, 19, 12 + b, skin_sh)

    if style == 'bald':
        rect(im, 13, 3 + b, 18, 3 + b, skin)
        rect(im, 12, 4 + b, 19, 7 + b, skin)
        rect(im, 12, 8 + b, 13, 10 + b, skin)
        rect(im, 12, 4 + b, 13, 10 + b, skin_sh)
    else:
        rect(im, 13, 2 + b, 18, 2 + b, hair)
        rect(im, 12, 3 + b, 19, 7 + b, hair)
        rect(im, 12, 8 + b, 13, 10 + b, hair)        # back of head
        pset(im, [(13, 3 + b), (14, 3 + b), (15, 3 + b)], hair_hi)
        if style == 'bang':
            rect(im, 17, 8 + b, 19, 8 + b, hair)     # fringe
        elif style == 'pony':
            rect(im, 10, 4 + b, 11, 11 + b, hair)    # tail hanging behind
            rect(im, 11, 12 + b, 11, 13 + b, hair)
        elif style == 'bun':
            rect(im, 10, 2 + b, 12, 4 + b, hair)     # knot at back-top

    # eye (single, 2px, near-black)
    rect(im, 18, 10 + b, 18, 11 + b, OUTLINE_STORY)

    # torso
    rect(im, 12, 14 + b, 18, 21 + b, cloth)
    rect(im, 17, 14 + b, 18, 21 + b, cloth_dk)
    rect(im, 12, 22 + b, 18, 22 + b, WOOD_DK)        # belt

    # legs.  Contact frames (walk1/walk3) use the SAME +-2px scissor
    # silhouette — alternation is told by swapping the near/far trouser
    # shade (classic side-view walk).  Previous l_fwd pinched the legs
    # together (narrower than idle) which read as a one-step limp.
    if leg == 'neutral':
        rect(im, 12, 23 + b, 14, 26, trouser_far)
        rect(im, 12, 27, 14, 28, WOOD_DK)
        rect(im, 15, 23 + b, 17, 26, trouser)
        rect(im, 15, 27, 17, 28, WOOD_DK)
    elif leg in ('r_half', 'l_half'):
        # half scissor — between neutral and full stride (smoother step-through).
        back_c, front_c = ((trouser_far, trouser) if leg == 'r_half'
                           else (trouser, trouser_far))
        rect(im, 11, 23 + b, 13, 26, back_c)    # back leg slightly back
        rect(im, 11, 27, 13, 28, WOOD_DK)
        rect(im, 15, 23 + b, 17, 26, front_c)   # front leg slightly fwd
        rect(im, 16, 27, 18, 28, WOOD_DK)
    else:
        back_c, front_c = ((trouser_far, trouser) if leg == 'r_fwd'
                           else (trouser, trouser_far))
        rect(im, 11, 23 + b, 13, 26, back_c)    # back leg: upper -1, boot -2
        rect(im, 10, 27, 12, 28, WOOD_DK)
        rect(im, 16, 23 + b, 18, 26, front_c)   # front leg: upper +1, boot +2
        rect(im, 17, 27, 19, 28, WOOD_DK)

    # near arm (over torso) / work poses
    if arm in ('idle', 'w1', 'w3'):
        dx = {'idle': 0, 'w1': -1, 'w3': 1}[arm]
        rect(im, 15 + dx, 15 + b, 16 + dx, 19 + b, cloth_dk)
        rect(im, 15 + dx, 20 + b, 16 + dx, 21 + b, skin)
    elif arm == 'raise':
        rect(im, 15, 15 + b, 16, 17 + b, cloth_dk)
        rect(im, 17, 16 + b, 19, 17 + b, cloth_dk)
        rect(im, 19, 18 + b, 19, 18 + b, cloth_dk)   # wrist link (no diagonal gap)
        rect(im, 20, 18 + b, 21, 19 + b, skin)       # hand → anchor (21,19)
    else:  # strike
        rect(im, 15, 16 + b, 16, 19 + b, cloth_dk)
        rect(im, 17, 19 + b, 19, 20 + b, cloth_dk)
        rect(im, 20, 20 + b, 21, 21 + b, skin)       # hand → anchor (21,21)

    outline(im, OUTLINE_STORY)
    p = im.load()
    for x in range(11, 21):
        p[x, 29] = OUTLINE_STORY
    return im


DRAW = {'S': draw_S, 'E': draw_E, 'N': draw_N}

# col → (name, leg, arm, bounce).  9 cols: idle | walk1..6 | work1..2.
#  6-frame walk (was 4): contact -> half-lift transition -> passing, per side.
#  The half frames make the foot rise/fall 1px/frame and the side-view legs
#  step through a mid scissor — removes the 4-frame march-y stutter.
#  NOTE: animator must use COLS=9, COL_WORK0=7, walk index = walkClock % 6.
POSES = [
    ('idle',  'neutral', 'idle',   0),
    ('walk1', 'r_fwd',   'w1',     0),   # contact R (left leg back/up, left arm fwd)
    ('walk2', 'r_half',  'idle',   0),   # mid-stride transition
    ('walk3', 'neutral', 'idle',   1),   # passing (feet together, body high)
    ('walk4', 'l_fwd',   'w3',     0),   # contact L
    ('walk5', 'l_half',  'idle',   0),   # mid-stride transition
    ('walk6', 'neutral', 'idle',   1),   # passing
    ('work1', 'neutral', 'raise',  0),
    ('work2', 'neutral', 'strike', 0),
]


# ── tools (12x12, grip (3,10), WOOD handle + ROCK head, OUTLINE_OBJ) ──
def _tool_handle(im):
    pset(im, [(3, 10), (4, 9), (5, 8), (6, 7), (7, 6), (8, 5)], WOOD_MD)
    pset(im, [(4, 10), (5, 9), (6, 8), (7, 7), (8, 6)], WOOD_DK)
    pset(im, [(3, 9)], WOOD_LT)


def make_axe():
    im = Image.new('RGBA', (12, 12), (0, 0, 0, 0))
    _tool_handle(im)
    pset(im, [(7, 3), (7, 4)], ROCK_MD)              # socket
    rect(im, 8, 1, 10, 3, ROCK_LT)                   # blade body (3x4 head)
    pset(im, [(8, 4), (9, 4), (10, 4)], ROCK_MD)     # blade lower shade
    pset(im, [(10, 1), (10, 2)], ROCK_MD)            # back of head darker
    outline(im, OUTLINE_OBJ)
    return im


def make_pick():
    im = Image.new('RGBA', (12, 12), (0, 0, 0, 0))
    _tool_handle(im)
    pset(im, [(5, 3), (6, 2), (7, 2), (8, 2), (9, 2), (10, 3)], ROCK_LT)  # arc top
    pset(im, [(6, 3), (7, 3), (8, 3), (9, 3), (10, 4), (11, 5)], ROCK_MD)
    pset(im, [(4, 4), (11, 6)], ROCK_MD)             # curved tips
    outline(im, OUTLINE_OBJ)
    return im


def rot_cw_with_grip(im, grip):
    """90° clockwise pre-rendered strike pose; grip (x,y) → (H-1-y, x)."""
    r = im.transpose(Image.ROTATE_270)
    gx, gy = grip
    return r, (im.height - 1 - gy, gx)


def mirror_with_grip(im, grip):
    m = im.transpose(Image.FLIP_LEFT_RIGHT)
    gx, gy = grip
    return m, (im.width - 1 - gx, gy)


def composite_tool(pawn, dirn, work, tool_raised, tool_strike, grip_r, grip_s):
    ax, ay = HAND_ANCHOR[dirn]
    if work == 'work1':
        tool, grip, hy = tool_raised, grip_r, ay - 1
    else:
        tool, grip, hy = tool_strike, grip_s, ay + 1
    if dirn == 'N':
        tool, grip = mirror_with_grip(tool, grip)
    ox, oy = ax - grip[0], hy - grip[1]
    out = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    paste_clipped(out, pawn, 0, 0)
    if dirn == 'N':
        paste_clipped(out, tool, ox, oy, under=True)   # tool behind pawn
    else:
        paste_clipped(out, tool, ox, oy)               # tool in front
    return out


# ── grass tile for contrast preview (spec §5 compliant) ───────────────
def grass_tile(rng):
    im = Image.new('RGBA', (32, 32), GRASS_MD)
    p = im.load()
    budget = 88                                      # ~10% of inner 30x30
    while budget > 0:
        x = rng.randint(1, 30)
        y = rng.randint(1, 29)
        if rng.random() < 0.5 and budget >= 2:       # 1x2 dark blade
            p[x, y] = GRASS_DK
            p[x, y + 1] = GRASS_DK
            budget -= 2
        else:                                        # light speck
            p[x, y] = GRASS_LT
            budget -= 1
    return im


# ── main ───────────────────────────────────────────────────────────────
def main():
    random.seed(42)
    rng = random.Random(42)

    # tools
    axe = make_axe()
    pick = make_pick()
    axe_strike, axe_grip_s = rot_cw_with_grip(axe, TOOL_GRIP)
    pick_strike, pick_grip_s = rot_cw_with_grip(pick, TOOL_GRIP)
    axe.save(os.path.join(OUT, "axe32.png"))
    pick.save(os.path.join(OUT, "pick32.png"))

    # spec §8: 2-pose tool sheets 24x12 — raised | strike (prerendered 90°)
    for name, base, strike in (("tool_axe_v2", axe, axe_strike),
                               ("tool_pickaxe_v2", pick, pick_strike)):
        sh = Image.new('RGBA', (24, 12), (0, 0, 0, 0))
        sh.paste(base, (0, 0))
        sh.paste(strike, (12, 0))
        sh.save(os.path.join(OUT, f"{name}.png"))

    tools = {'axe': (axe, axe_strike, TOOL_GRIP, axe_grip_s),
             'pick': (pick, pick_strike, TOOL_GRIP, pick_grip_s)}

    # tool preview on grass
    tp = Image.new('RGBA', (64, 16), GRASS_MD)
    paste_clipped(tp, axe, 2, 2)
    paste_clipped(tp, axe_strike, 18, 2)
    paste_clipped(tp, pick, 34, 2)
    paste_clipped(tp, pick_strike, 50, 2)
    upscale(tp).save(os.path.join(OUT, "_preview_tools.png"))

    all_frames = {}   # (vi, dirn, pose) -> Image
    for vi, (cloth_key, hair_style, skin_i, hair_mul) in enumerate(VARIANTS):
        v = {
            'cloth': CLOTH_SETS[cloth_key],
            'skin': SKIN_TONES[skin_i],
            'hair': hair_style,
            'hair_c': shade(HAIR_DK, hair_mul),
            'hair_hi': shade(HAIR_DK, min(hair_mul * HAIR_HI_MUL, 1.12 * 1.12)),
        }
        sheet = Image.new('RGBA', (len(POSES) * 32, 96), (0, 0, 0, 0))
        for ri, dirn in enumerate(('S', 'E', 'N')):
            for ci, (pname, leg, arm, b) in enumerate(POSES):
                fr = DRAW[dirn](v, leg, arm, b)
                all_frames[(vi, dirn, pname)] = fr
                sheet.paste(fr, (ci * 32, ri * 32), fr)
                fr.save(os.path.join(FRAMES, f"pawn32_v{vi}_{dirn}_{pname}.png"))
                if pname in ('work1', 'work2'):
                    for tname, (tr, ts, gr, gs) in tools.items():
                        comp = composite_tool(fr, dirn, pname, tr, ts, gr, gs)
                        all_frames[(vi, dirn, pname, tname)] = comp
                        comp.save(os.path.join(
                            FRAMES_TOOL, f"pawn32_v{vi}_{dirn}_{pname}_{tname}.png"))
        sheet.save(os.path.join(OUT, f"pawn32_v{vi}.png"))
        upscale(sheet).save(os.path.join(OUT, f"_preview_pawn32_v{vi}.png"))
        print(f"pawn32_v{vi}: cloth={cloth_key} hair={hair_style} skin={skin_i}")

    # variants overview: 8 variants x 3 dirs (idle)
    ov = Image.new('RGBA', (256, 96), (0, 0, 0, 0))
    for vi in range(8):
        for ri, dirn in enumerate(('S', 'E', 'N')):
            fr = all_frames[(vi, dirn, 'idle')]
            ov.paste(fr, (vi * 32, ri * 32), fr)
    upscale(ov).save(os.path.join(OUT, "_preview_pawn32_variants.png"))

    # grass contrast composite
    g = Image.new('RGBA', (192, 96), (0, 0, 0, 0))
    gtiles = [grass_tile(rng) for _ in range(3)]
    for ty in range(3):
        for tx in range(6):
            g.paste(gtiles[(tx + ty) % 3], (tx * 32, ty * 32))
    placements = [
        ((8, 2),   (0, 'S', 'idle')),
        ((44, 2),  (1, 'S', 'walk1')),
        ((80, 2),  (2, 'E', 'walk1')),
        ((116, 2), (3, 'E', 'work2', 'pick')),
        ((152, 2), (6, 'N', 'idle')),
        ((8, 50),  (4, 'N', 'walk1')),
        ((44, 50), (5, 'S', 'work1', 'axe')),
        ((80, 50), (0, 'S', 'work2', 'axe')),
        ((116, 50), (2, 'S', 'walk3')),
        ((152, 50), (3, 'N', 'work2', 'pick')),
    ]
    for (px_, py_), key in placements:
        fr = all_frames[key]
        g.paste(fr, (px_, py_), fr)
    upscale(g).save(os.path.join(OUT, "_preview_pawn32_grass.png"))

    # sanity: hand-anchor pixels must be painted (tool grip lands there)
    for dirn, work, (hx, hy) in (('S', 'work1', (22, 19)), ('S', 'work2', (22, 21)),
                                 ('E', 'work1', (21, 19)), ('E', 'work2', (21, 21)),
                                 ('N', 'work1', (9, 19)),  ('N', 'work2', (9, 21))):
        px = all_frames[(0, dirn, work)].load()[hx, hy]
        assert px[3] != 0, f"hand anchor empty: {dirn} {work} ({hx},{hy})"
    print("anchor sanity: OK")

    print("sheets: 8 (256x96), frames:", 8 * 3 * 7, "tool comps:", 8 * 3 * 2 * 2)
    print("anchors: grip(3,10) | S(22,20) E(21,20) N(9,20), work1 y-1 / work2 y+1")
    print("done ->", OUT)


if __name__ == '__main__':
    main()
