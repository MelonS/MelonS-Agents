# PawnSim — Design + UI Improvement Backlog

Game Director directive, 2026-05-29. Operator complaint (escalating):
"디자인 구리고 프로토타입 수준도 안됨" → "아직 너무 별로".

This document is the single source of visual truth. The **artist** owns the ART
stream, the **programmer** owns the UI stream (using my styling spec). **QA**
verifies each item against its binary acceptance criterion by Reading a
screenshot. Items are ordered **highest visual-impact-per-effort first** — the
operator sees the top items land first, so they must be the ones that most
visibly kill the "cheap" feeling.

---

## Visual coherence north star (every item MUST respect this)

RimWorld's polish does **not** come from detail — it comes from **discipline**:
a tight, muted, warm-earth palette; a strict outline hierarchy; and clean,
readable silhouettes on a desaturated background. Per RimWorld's own art guide:
*"making the graphics really simple reduces noise… story-relevant things have a
2–3 px black border so characters pop while plants smear into the background."*

Our prototype currently breaks every one of these rules. The rules below are
non-negotiable; reject any asset/UI that violates them.

1. **One palette, globally.** Today there are TWO competing sprite generators
   (`_gen_sprites.py` = ultra-detailed, no outlines, high-saturation; and
   `_gen_fix_audit.py` = flat Kenney with `OUTLINE=(40,26,18)`). They use
   different browns, different greens, different styles. This single
   inconsistency is the #1 reason it looks "cheap." **Kill one style. Keep the
   flat-Kenney-with-outline style** (`_gen_fix_audit.py`'s approach) — it is
   closer to RimWorld and far cheaper to keep cohesive. The detailed
   `_gen_sprites.py` pawn/tree must be **re-done in the flat style** at matching
   PPU. Everything draws from `PALETTE` below.

2. **Outline hierarchy (RimWorld's core trick).**
   - Pawns, animals, enemies: **2 px** outline, color `OUTLINE_STORY` (near-black warm).
   - Buildings, items (wall/door/stove/bench/bed/wood pile/stone): **1 px** outline, `OUTLINE_OBJ`.
   - Plants (tree/bush/crop): **dark-green outline or none** — they must
     *recede*, never compete with pawns.
   - Result: in any frame, the eye lands on pawns first, structures second,
     foliage last.

3. **Desaturate the world, save saturation for what matters.** Grass/dirt/rock
   are LOW-saturation, muted. The only saturated colors on screen are pawns
   (skin/clothes), ripe crops (gold), fire (orange), and danger (red). If the
   ground is as vivid as a pawn, the pawn stops popping.

4. **Warm, not lurid.** Greens lean olive/khaki, not neon. Browns lean warm but
   muted. No pure `(0,255,0)`-adjacent values anywhere.

5. **UI is a single dark warm-brown system.** Every panel = same bg, same 1 px
   border, same corner treatment, same Korean font, same padding scale. The
   `UITheme` class already defines good colors — the failure is that panels draw
   as **borderless flat rectangles with no structure**. Add borders, headers,
   and consistent padding; never invent a new panel color inline.

6. **Pixel-perfect, no blur.** All sprites import Point filter, no compression,
   PPU consistent (16 for 1-cell objects, matching multiples for larger). The
   floating bars currently use a **Bilinear** white sprite → they look soft and
   foreign against crisp pixel art. Everything must be Point-filtered.

---

## GLOBAL PALETTE (the single most important deliverable)

Every sprite generator imports these. No ad-hoc colors. This is what makes 15
separate assets read as one game instead of a pile of upgrades.

```python
# ── PawnSim master palette v1 (warm muted earth, RimWorld-grounded) ──
# Outlines (hierarchy)
OUTLINE_STORY = (26, 20, 16, 255)    # #1A1410  pawns/animals — 2px
OUTLINE_OBJ   = (40, 30, 22, 255)    # #281E16  buildings/items — 1px
OUTLINE_PLANT = (34, 56, 32, 255)    # #223820  plant dark-green edge

# Terrain (LOW saturation — must recede)
GRASS_DK      = (74,  92,  58, 255)  # #4A5C3A  olive shadow
GRASS_MD      = (96, 116,  70, 255)  # #607446  base grass (muted khaki-green)
GRASS_LT      = (118, 138, 86, 255)  # #768A56  dappled highlight
DIRT_DK       = (86,  66,  46, 255)  # #56422E
DIRT_MD       = (112, 88,  62, 255)  # #70583E  tilled soil / paths
DIRT_LT       = (138, 112, 80, 255)  # #8A7050
ROCK_DK       = (66,  66,  74, 255)  # #42424A  cool grey-blue stone
ROCK_MD       = (104, 104, 114, 255) # #686872
ROCK_LT       = (150, 150, 160, 255) # #9696A0
WATER_DK      = (52,  78,  96, 255)  # #344E60
WATER_MD      = (74, 108, 128, 255)  # #4A6C80
WATER_LT      = (104, 140, 160, 255) # #688CA0

# Wood (ONE wood ramp for walls/floors/beds/benches — currently 3 different ones)
WOOD_DK       = (92,  60,  36, 255)  # #5C3C24
WOOD_MD       = (140, 92,  54, 255)  # #8C5C36
WOOD_LT       = (188, 138, 88, 255)  # #BC8A58

# Pawn (the ONLY high-saturation focal subjects)
SKIN_MD       = (224, 176, 132, 255) # #E0B084
SKIN_SH       = (176, 132, 96, 255)  # #B08460
HAIR_DK       = (58,  38,  22, 255)  # #3A2616
CLOTH_BLUE    = (74,  96, 132, 255)  # #4A6084  muted denim (default colonist)
CLOTH_RUST    = (158, 86,  58, 255)  # #9E563A  rust (variant)
CLOTH_OLIVE   = (108, 112, 70, 255)  # #6C7046  olive (variant)

# Semantic accents (saturated — reserve for meaning)
CROP_GOLD     = (218, 178, 70, 255)  # #DAB246  ripe crop / wheat
FIRE_OR       = (232, 120, 44, 255)  # #E8782C
FIRE_LT       = (250, 196, 96, 255)  # #FAC460
MEAT_RED      = (172, 74,  60, 255)  # #AC4A3C
DANGER_RED    = (210, 72,  60, 255)  # #D2483C  enemies/HP-low

# UI (mirror of UITheme.cs — keep in sync)
UI_PANEL_BG   = (42, 31, 24, 240)    # #2A1F18 a0.94
UI_PANEL_HDR  = (73, 50, 35, 242)    # #493223
UI_BORDER     = (90, 65, 46, 255)    # #5A412E  every panel gets this 1–2px border
UI_GOLD       = (244, 215, 138, 255) # #F4D78A  titles
UI_ORANGE     = (232, 181, 96, 255)  # #E8B560  active state
UI_CREAM      = (242, 228, 208, 255) # #F2E4D0  body text
UI_MUTED      = (187, 170, 148, 255) # #BBAA94  hints
```

`UITheme.cs` already encodes the UI block correctly — programmer keeps using it.
Artist adds the world/sprite block as `palette.py` imported by every generator.

---

## Honest assessment of the current screenshot

What an art director sees in `_refactor_current.png`, harshest first:

- **Pawns look like potatoes in pots.** The colonist sprite reads as a brown
  blob (the `_gen_sprites.py` 64×64 over-detailed coat) with a bright blue belly
  patch and no clean outline, so it does NOT pop from the grass. RimWorld's
  whole point is the pawn popping. This one sinks in.
- **Ground is one flat green with a single ugly dark blob** (the dirt patch
  bottom-left). No tile cohesion, no subtle dappling, saturation too high so it
  competes with pawns.
- **Style civil war.** The wood wall (top-left) is flat-Kenney; the tree is
  hyper-detailed gradient; the pawns are detailed-no-outline. Three art styles
  in one frame = amateur.
- **Floating HP/mood bars are blurry** (bilinear white sprite) and float oddly;
  the green name text ("지훈", "민지") has no plate behind it so it's hard to
  read on light grass.
- **Bottom GUI bar is a row of identical flat brown rectangles** — no borders,
  no icons, no grouping dividers, text-only. Reads as a debug toolbar, not a
  game UI.
- **Top bar** is bare text "석재: 0 · 목재: 40 …" with no icons, no panel
  structure, dot separators. Functional but characterless.
- **Inspector panel (right)** is a borderless dark rectangle with cramped text;
  "선택된 오브젝트 없음" — no header bar, no padding rhythm.
- **Tutorial banner** (top center dark pill) and the two corner `S`/`L` buttons
  are yet another unrelated style.

The good news: `UITheme.cs` exists and its colors are right; the flat-Kenney gen
is a sound base. The fix is **unification + outline hierarchy + UI structure**,
not a from-scratch rebuild.

---

## ART STREAM (game-artist owns)

| ID | what's wrong now | target | file(s) | acceptance (QA reads a screenshot) | impact | effort |
|----|------------------|--------|---------|-------------------------------------|--------|--------|
| **A1** | Two generators, two palettes, three styles — root cause of "cheap." | One `palette.py` with the master palette above; deprecate `_gen_sprites.py`'s detailed style; all gens import shared colors. | new `Assets/Sprites/palette.py`; edits across `_gen_fix_audit.py`, `_gen_bed.py`, `_gen_sprites.py` | Open any 2 sprites side by side: browns/greens are identical hex; no asset uses a color outside `palette.py`. | **H** | M |
| **A2** | Colonist = detailed brown blob, no outline, bright-blue belly, doesn't pop. | Flat-style 16×16 (or 32×32 @PPU32) top-down colonist: muted cloth body (`CLOTH_BLUE`), skin head, simple 2-color hair, **2 px `OUTLINE_STORY`** all around. Clear human silhouette. | `_gen_sprites.py::gen_pawn` rewritten flat (or new `_gen_pawn.py`) | Pawn on grass: a crisp dark outline separates it from ground; reads as a person from across the frame, NOT a blob; no neon-blue patch. | **H** | M |
| **A3** | Grass is one flat over-saturated green + ugly dark dirt blob; no cohesion. | 2–3 muted grass tile variants (`GRASS_DK/MD/LT` dappling, low saturation) + a proper `dirt`/path tile. Tiles seamless, no harsh seams. Saturation clearly below pawn. | the terrain/ground generator (locate grass tile gen or `SceneSetup` ground partial) | Ground reads as soft muted khaki-green with gentle variation; pawns/buildings visibly pop above it; no single hard dark rectangle. | **H** | M |
| **A4** | Wall/floor/door/bench/bed each on their own wood ramp → patchwork. | All wood structures use the single `WOOD_DK/MD/LT` ramp + 1 px `OUTLINE_OBJ`. Door distinct from wall; floor subtly darker than walls. | `_gen_fix_audit.py` (wall/floor/stove/bench), `_gen_bed.py` (beds), door gen | A built room screenshot: walls, floor, door, bed all share one wood tone family; structure is legible. | **H** | M |
| **A5** | Tree is hyper-detailed gradient blob that competes with pawns. | Flat 2–3 tone canopy, `OUTLINE_PLANT` (dark green) or no outline, lower saturation than pawns so it *recedes*. | `_gen_sprites.py::gen_tree` rewritten flat | In a frame with trees + a pawn, the pawn pops and the trees smear into background (RimWorld rule). | **H** | M |
| **A6** | Stone vein / chunk grey clashes (one cool-grey, one neutral). | Unify on `ROCK_DK/MD/LT` (cool grey-blue) with 1 px outline; ore specks = `CROP_GOLD`. | `_gen_fix_audit.py::gen_stone_vein, gen_stone_chunk` | Vein and chunk share identical grey ramp; ore specks are the only warm accent. | **M** | S |
| **A7** | Deer/wolf colors and outlines inconsistent with new hierarchy. | Animals get **2 px `OUTLINE_STORY`** (same tier as pawns — they're story actors). Deer warm-brown, wolf cool-grey, both muted-but-readable silhouettes. | `_gen_fix_audit.py::gen_deer, gen_wolf` | Deer and wolf each read as a clear 4-legged silhouette with a thick outline matching the pawn's; distinguishable at a glance. | **M** | S |
| **A8** | Crop sprite (rice/wheat) saturation/读 unclear vs grass. | Growing crop = muted green rows; **ripe** crop = `CROP_GOLD` (the reserved saturated accent). Strong ripe/unripe contrast. | `_gen_fix_audit.py::gen_crop_rice` (+ growth-stage variants if present) | Ripe field is unmistakably gold and pops; unripe field is muted green and recedes. | **M** | S |
| **A9** | Item drops (wood pile, meat, stone chunk) read inconsistently as "loot." | All ground-item sprites get a consistent small drop-shadow + 1 px outline + shared palette so they read as a class. | `_gen_fix_audit.py` (wood_pile/meat_pile/stone_chunk), `_gen_sprites.py` (any leftover) | The three drop items on grass share a visual family (same shadow, same outline weight). | **L** | S |
| **A10** | Bushes / minor foliage (if present) likely on old style. | Re-do in flat plant style, `OUTLINE_PLANT`, recede into background. | bush/foliage generator | Bushes recede like trees; don't compete with pawns. | **L** | S |

## UI STREAM (game-programmer owns, my styling spec)

### Style spec (apply uniformly — this is the contract)
- **Panel:** bg `UITheme.PanelBg`; **1–2 px border** `UITheme.Divider` (#5A412E)
  on all four edges (currently MISSING everywhere — add a border child Image or a
  9-slice). Header strip = `UITheme.HeaderBg` with `AccentGold` title text.
- **Padding rhythm:** outer panel pad 12 px; inter-row gap 6 px; label↔value
  gap 8 px. One scale, everywhere.
- **Font:** `UITheme.LoadKoreanFont()` everywhere (some scripts re-implement
  their own fallback list — consolidate to the UITheme call).
- **Buttons:** inactive `BtnInactiveBg` + 1 px `Divider` border; hover
  `BtnHover`; active `BtnActiveBg` (orange) with `TextDark`. Bold 18–20 label,
  11 muted hint. Add the missing border so buttons read as buttons.
- **Group dividers:** vertical 1 px `Divider` line between button groups in the
  control bar (speed | draft | tabs | build | research).

| ID | what's wrong now | target | file(s) | acceptance | impact | effort |
|----|------------------|--------|---------|-----------|--------|--------|
| **U1** | Bottom control bar = borderless flat brown rectangles, no structure, debug-toolbar look. | Apply style spec: panel border, per-button 1 px border + hover/active states, vertical group-divider lines, consistent padding. | `GuiControlBar.cs` (`BuildLayout`, `MakeBtn`) | Bottom bar reads as a finished game toolbar: each button has a visible edge, groups are visually separated, active button glows orange. | **H** | S |
| **U2** | Floating HP/mood bars are blurry (bilinear) and float with no frame. | Point-filtered 1×1 white sprite; add a 1 px dark outline frame around each bar; tighten to head. | `PawnFloatingBars.cs` (`WhiteSprite` filterMode → Point; add bg outline) | Zoomed screenshot: bars are crisp-edged (no blur), sit just above the head with a clean dark frame. | **H** | S |
| **U3** | Pawn name labels = floating green/blue text, hard to read on grass. | Add a small semi-transparent `PanelBg` plate behind name+status text; name `AccentGold`/cream, status muted. Point-crisp. | `PawnNameLabel.cs` | Names are legible over any terrain because they sit on a subtle dark plate; consistent gold/cream coloring. | **H** | S |
| **U4** | Top resource bar = bare text + dot separators, no icons, characterless. | Add small palette-matched icons before each counter (wood/food/meal/stone), wrap bar in the panel style with a bottom border; keep one font/size. | `Editor/SceneSetup.Game.TopBar.cs` (+ tiny icon sprites from artist) | Top bar shows an icon next to each number; bar has a defined bottom edge; alignment is even. | **H** | M |
| **U5** | Inspector panel = borderless dark rectangle, cramped, no header. | Apply style spec: header strip with `AccentGold` title (pawn name), 1 px border, padding rhythm, section labels in gold, values in cream. | `PawnInfoPanel.cs` + its SceneSetup builder | Inspector has a titled header bar, a clear border, and breathing room between rows; sections (상태/능력치/기분/장비) are visually grouped. | **H** | M |
| **U6** | Tooltip is a plain box; ok but off-style and unbordered. | Apply panel style (border + padding); ensure Korean font via `UITheme`; small but consistent. | `HoverTooltip.cs` | Hover tooltip matches every other panel: same bg, same 1 px border, same font. | **M** | S |
| **U7** | Architect/build menu (if styled separately) likely inconsistent. | Apply full style spec: bordered panel, header, button states matching U1. | `ArchitectMenu.cs` (+ builder) | Architect menu panels and buttons are visually identical in treatment to the control bar. | **M** | S |
| **U8** | Tutorial banner + corner S/L buttons are an unrelated style. | Re-skin banner as a bordered `PanelBg` pill with cream text; S/L buttons get the standard button treatment. | banner/save-load UI builder | Banner and S/L buttons share the global UI style; nothing on screen looks like a different game. | **M** | S |
| **U9** | Font fallback re-implemented in 3+ scripts (drift risk). | Route every UI text through `UITheme.LoadKoreanFont()`. | `GuiControlBar.cs`, `HoverTooltip.cs`, any other inline font loaders | All UI text renders in one consistent Korean font; no Arial fallback visible. | **L** | S |

---

## TOP 6 — dispatch order (highest impact-per-effort, operator sees these first)

1. **A1 — Global palette unification** (`palette.py` + adopt master palette).
   *Foundational — every later art item depends on it. Do this first or the
   stream stays incoherent.*
2. **A2 — Flat colonist sprite with 2 px outline.** *The pawn is the emotional
   center and currently the worst offender ("potato in a pot"). Biggest single
   "it stopped looking cheap" win.*
3. **U1 — Control bar restyle** (borders, group dividers, button states).
   *Cheapest UI win with the most screen real estate; kills the "debug toolbar"
   read instantly.*
4. **A3 — Muted cohesive grass/dirt tiles.** *The background is half the frame;
   desaturating it makes A2's pawn pop for free.*
5. **U3 — Name-label plates + U2 floating-bar crispness.** *Small effort, high
   readability payoff; pawns immediately feel "alive and labeled" instead of
   floating debug text.*
6. **A4 — Unified wood structures.** *Built rooms (the player's creations) stop
   looking like patchwork; high pride-of-ownership impact.*

After these six, proceed: A5 (trees recede) → U4 (top bar icons) → U5 (inspector)
→ A6/A7/A8 (stone/animals/crops) → remaining UI consistency (U6–U9) → A9/A10.

QA gate: each item ships independently; QA Reads a fresh screenshot and checks
the binary acceptance. Any asset/UI that violates a north-star rule = REJECT
back to the owning agent regardless of effort spent.
