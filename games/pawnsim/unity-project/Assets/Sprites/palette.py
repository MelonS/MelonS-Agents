# -*- coding: utf-8 -*-
"""PawnSim master palette v1 — warm muted earth, the reference sim-grounded.

Single source of truth for ALL sprite generators.  Import here; never
define ad-hoc colors in individual generators.

Usage:
    from palette import GRASS_MD, OUTLINE_STORY, SKIN_MD, ...

Groupings follow the visual hierarchy in the backlog north-star spec
(docs/design-improvement-backlog.md, "GLOBAL PALETTE" section).
"""

# ── Outlines (hierarchy) ────────────────────────────────────────────
# Use OUTLINE_STORY for pawns/animals (2px), OUTLINE_OBJ for buildings/
# items (1px), OUTLINE_PLANT for foliage edge or no outline.
OUTLINE_STORY = (26, 20, 16, 255)    # #1A1410  pawns + animals — 2px
OUTLINE_OBJ   = (40, 30, 22, 255)    # #281E16  buildings / items — 1px
OUTLINE_PLANT = (34, 56, 32, 255)    # #223820  plant dark-green edge

# ── Terrain — LOW saturation; must recede behind pawns ──────────────
GRASS_DK      = (74,  92,  58, 255)  # #4A5C3A  olive shadow
GRASS_MD      = (96, 116,  70, 255)  # #607446  base grass (muted khaki-green)
GRASS_LT      = (118, 138, 86, 255)  # #768A56  dappled highlight

DIRT_DK       = (86,  66,  46, 255)  # #56422E
DIRT_MD       = (112, 88,  62, 255)  # #70583E  tilled soil / paths
DIRT_LT       = (138, 112, 80, 255)  # #8A7050

# TOP-1 (visual-polish-backlog 2026-06-11): 한색 블루그레이가 웜톤 잔디/흙 옆에서
#  보라색으로 읽혀 "QR코드 지면" 인상의 주범 — 웜그레이로 이동 (hue ~40°, 저채도).
ROCK_DK       = (76,  72,  64, 255)  # #4C4840  warm dark stone
ROCK_MD       = (106, 100, 88, 255)  # #6A6458  warm grey
ROCK_LT       = (140, 132, 118, 255) # #8C8476  warm light

WATER_DK      = (52,  78,  96, 255)  # #344E60
WATER_MD      = (74, 108, 128, 255)  # #4A6C80
WATER_LT      = (104, 140, 160, 255) # #688CA0

# ── Wood — ONE ramp; walls/floors/beds/benches all use this ─────────
# Retire the three separate wood ramps that previously existed.
WOOD_DK       = (92,  60,  36, 255)  # #5C3C24
WOOD_MD       = (140, 92,  54, 255)  # #8C5C36
WOOD_LT       = (188, 138, 88, 255)  # #BC8A58

# ── Pawn — the ONLY high-saturation focal subjects on screen ────────
SKIN_MD       = (224, 176, 132, 255) # #E0B084
SKIN_SH       = (176, 132, 96, 255)  # #B08460  shadow on skin

HAIR_DK       = (58,  38,  22, 255)  # #3A2616

# Cloth tints — three colonist variants.  Keep muted; no neon.
# TOP-4 (2026-06-11): 올리브(잔디 hue 90~150° 밴드)는 들판 보호색 — 림이 배경에
#  매몰됐다.  이름은 유지하고 값만 머스터드로 (의상 금지 휴밴드: 잔디 녹색대).
#
# 밸류 리프트 (2026-07-29) — TOP-4 와 같은 문제의 밝기 축 버전.
#  실측: 상의 L 0.115~0.257 vs 잔디 0.400 / 모래 0.737 = 캐릭터가 지면보다
#  어두워 나무(L 0.108)와 같은 밴드로 읽혔다.  지면을 낮추고(extract-tinyswords
#  GRADE_*) 상의를 올려 "캐릭터가 화면에서 가장 밝은 움직이는 것" 위계를 복원.
#  hue 는 한 도(度)도 건드리지 않음 — 채도·명도만 (HSV V 리프트, S 유지).
#  L: 0.115->0.331 / 0.142->0.350 / 0.257->0.422
CLOTH_BLUE    = (121, 157, 215, 255) # #799DD7  denim (구 #4A6084)
CLOTH_RUST    = (239, 130, 88, 255)  # #EF8258  rust / reddish-brown (구 #9E563A)
# 3번 슬롯 = 올리브 → 머스터드(TOP-4) → 리넨(2026-07-29).  두 번 다 같은 이유로
#  옮겼다: 지형 휴 대역과 겹쳐 림이 배경에 매몰됐다.  머스터드 H41 은 모래 H51 과
#  **10°** 차이라, 지면을 어둡게 그레이드한 뒤 모래 위에서 사실상 위장색이 됐다
#  (합성 검증으로 육안 확인).  후보 4종(머스터드/플럼/리넨/틸)을 잔디·모래 양쪽에
#  올려 비교 → 틸은 물 H181 과 15° 로 같은 위반, 플럼은 흙빛 세계에서 과채도,
#  리넨은 **무채색이라 어떤 지형 휴와도 구조적으로 충돌 불가** + 마직 셔츠로 세계관 부합.
CLOTH_LINEN   = (215, 202, 180, 255) # #D7CAB4  linen (구 머스터드 #A88638, 구 올리브)

# 여우 모피 — 구(舊) CLOTH_RUST 값.  2026-07-29 이전에는 _gen_animal32 가
#  여우 색으로 CLOTH_RUST 를 직접 썼는데, 그건 우연한 결합이었다(셔츠와 모피가
#  같은 상수를 공유할 이유가 없다).  콜로니스트 상의 밸류 리프트가 동물까지
#  끌고 가지 않도록 전용 상수로 분리.
FOX_MD        = (158, 86,  58, 255)  # #9E563A
FOX_DK        = (110, 58,  36, 255)  # #6E3A24

# ── Semantic accents — saturated; reserve for story/meaning ─────────
CROP_GOLD     = (218, 178, 70, 255)  # #DAB246  ripe crop / wheat heads
FIRE_OR       = (232, 120, 44, 255)  # #E8782C  fire base
FIRE_LT       = (250, 196, 96, 255)  # #FAC460  fire highlight
MEAT_RED      = (172, 74,  60, 255)  # #AC4A3C  raw meat
DANGER_RED    = (210, 72,  60, 255)  # #D2483C  enemies / HP critical

# ── UI — mirrors UITheme.cs; keep in sync manually ──────────────────
UI_PANEL_BG   = (42, 31, 24, 240)    # #2A1F18 a0.94
UI_PANEL_HDR  = (73, 50, 35, 242)    # #493223
UI_BORDER     = (90, 65, 46, 255)    # #5A412E  every panel gets 1–2px border
UI_GOLD       = (244, 215, 138, 255) # #F4D78A  titles
UI_ORANGE     = (232, 181, 96, 255)  # #E8B560  active / selected state
UI_CREAM      = (242, 228, 208, 255) # #F2E4D0  body text
UI_MUTED      = (187, 170, 148, 255) # #BBAA94  hints / secondary labels

# ── Additional derived shades (computed once here) ──────────────────
# Cloth tint for dark shadow-side detail (base 의 HSV V x0.66)
# 2026-07-29 밸류 리프트에 맞춰 재계산 — 그림자면이 구(舊) 베이스색과 거의 같아져
#  이전 룩을 음영으로 흡수한다(색 아이덴티티 연속).
CLOTH_BLUE_DK  = (80, 103, 142, 255)  # dark denim shadow
CLOTH_RUST_DK  = (158, 86, 58, 255)   # dark rust shadow (= 구 CLOTH_RUST)
CLOTH_LINEN_DK = (142, 133, 119, 255) # dark linen shadow

# Leg / trouser — slightly darker than cloth so they read separately (V x0.78)
TROUSER_BLUE   = (94, 122, 168, 255)
TROUSER_RUST   = (186, 101, 68, 255)
TROUSER_LINEN  = (167, 158, 141, 255)
