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

the reference sim's polish does **not** come from detail — it comes from **discipline**:
a tight, muted, warm-earth palette; a strict outline hierarchy; and clean,
readable silhouettes on a desaturated background. Per the reference sim's own art guide:
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
   closer to the reference sim and far cheaper to keep cohesive. The detailed
   `_gen_sprites.py` pawn/tree must be **re-done in the flat style** at matching
   PPU. Everything draws from `PALETTE` below.

2. **Outline hierarchy (the reference sim's core trick).**
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
# ── PawnSim master palette v1 (warm muted earth, the reference sim-grounded) ──
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
  patch and no clean outline, so it does NOT pop from the grass. the reference sim's
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
| **A5** | Tree is hyper-detailed gradient blob that competes with pawns. | Flat 2–3 tone canopy, `OUTLINE_PLANT` (dark green) or no outline, lower saturation than pawns so it *recedes*. | `_gen_sprites.py::gen_tree` rewritten flat | In a frame with trees + a pawn, the pawn pops and the trees smear into background (the reference sim rule). | **H** | M |
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

---

# Polish Wave v3 — from 7/10 toward 8.5–9/10

Game Director, 2026-05-29 (second pass). Wave v1/v2 (A1–A10, U1–U9) is DONE; QA
moved polish ~3/10 → ~7/10 and the "너무 별로" bar is cleared. The remaining gap
between "competent prototype" and "feels like a real game" is now **a different
category of problem**: v1/v2 fixed *coherence and readability* (every sprite is
clean, on-palette, pops correctly). What's left is **life, richness, and depth** —
the things that make a *still screenshot* of the reference sim feel like a paused living
world instead of a diorama.

## Honest assessment of the CURRENT build (`_design_build_check.png`)

Studied as a harsh art director. The frame is now *clean* — pawns pop with their
2 px outline, name plates are legible, the control bar reads as a real toolbar,
the wood wall/floor are one family, grass is muted. That is genuine 7/10 work.
What still reads as PROTOTYPE, loudest first:

1. **EVERYTHING IS FROZEN — the single biggest remaining gap.** Two pawns stand
   perfectly rigid on bare grass. Nothing moves, nothing breathes. A still of
   the reference sim *implies* motion (a pawn mid-stride, a flame, swaying grass); this
   still implies a paused diorama. The colony does not feel ALIVE. No amount of
   sprite polish fixes "dead world" — only motion does. **This is #1 by a wide
   margin and it's the cheapest big win we have left.**
2. **The world is empty and flat.** Huge uniform grass expanses with *nothing*
   scattered on them — no rocks, no flowers, no grass tufts, no terrain breakup.
   The v2 dappling helped the tile but the *world* still has zero incidental
   stuff. Real colony maps are quietly busy everywhere.
3. **The single tree is a flat blob.** Trees are the largest natural objects on
   screen; this one is a 2-tone lump. A layered canopy (a few internal tone
   clusters + a hint of trunk shadow) reads far richer while still receding per
   the plant rule.
4. **No grounding / depth.** Pawns, trees, and the wall sit on the same flat
   plane with no contact shadow, so they look *pasted on* rather than standing
   *in* the world. A single soft elliptical shadow blob under each
   pawn/tree/building is the standard 2D-game depth trick and is nearly free.
5. **Crop field reads as bare brown lines.** The tilled rows bottom-left have
   faint/absent plant markers (QA already flagged the crop-field markers + the
   inspector empty-state). Low-priority but it's visible dead space.

The locked constraints are respected by every item below: **1×1 pawn size,
60×60 grid, the established `palette.py` — no style change, no new art language.**
These items add *life and richness within* the existing flat-Kenney + outline
language, they do not restyle it.

## ART + PROGRAMMER STREAM — Polish Wave v3

| ID | stream | what's still prototype-level | target | file(s) | binary QA-checkable acceptance | impact | effort |
|----|--------|------------------------------|--------|---------|--------------------------------|--------|--------|
| **V1** | **programmer** | Everything is frozen — pawns stand rigid even while walking; world feels dead. | **Walk-bob**: a tiny vertical sine offset on the pawn's SPRITE child (NOT the root) while `PawnMovement.IsMoving`, plus a slow idle "breathe" bob at lower amplitude when stopped. Amplitude ≈ 1–1.5 px-equivalent (~0.04–0.06 world units at current PPU), freq ~6–8 Hz walking / ~1 Hz idle. Bob eases to zero on stop. | NEW `Assets/Scripts/PawnSpriteBob.cs` (reads `PawnMovement.IsMoving`); attached to pawn prefab in the SceneSetup pawn builder. **Must offset the SpriteRenderer's transform only** — root transform is what `PathGrid.WorldToCell` / movement / clamp read; bobbing root would desync pathfinding + reserved cells + floating bars. | Capture two frames of a walking pawn ~0.1 s apart: the pawn's body is at a *visibly different* vertical offset between frames (bobbing); a stopped pawn shows a much smaller, slow oscillation; the name plate / HP bar do NOT bob (they track root). No pawn drifts off its cell over 10 s of walking (pathfinding intact). | **H** | **S–M** |
| **V2** | **art** | World is empty/flat — no incidental scatter. | Low-density **scatter decals**: 3–4 tiny sprites (small rock, grass tuft, 2 wildflower colors) placed sparsely (~1 per 6–10 tiles, deterministic seed) on grass. Must use `palette.py` (rocks `ROCK_*`, tufts `GRASS_DK/LT`, flowers ONE muted accent each — NOT competing with pawn saturation), `OUTLINE_PLANT` or none, render BELOW pawns/structures. | NEW gen funcs in `_gen_fix_audit.py` (`gen_scatter_rock`, `gen_grass_tuft`, `gen_flower_a/b`); scatter placement in the ground/scenery builder (`SceneSetup.Game.*` ground partial). | Wide screenshot: grass is no longer uniform — small rocks/tufts/flowers are visibly scattered at low density; they recede (eye still lands on pawns first); no clutter, no tile-seam grid pattern; nothing brighter than a pawn. | **H** | **M** |
| **V3** | **art + programmer** | Pawns/trees/buildings look pasted on the flat plane — no grounding. | **Contact shadow**: one soft dark elliptical sprite (semi-transparent, `OUTLINE_STORY`-tone @ ~35% alpha, point-filtered) parented under each pawn/tree/building at its base, sorting just above ground and below the object. Static (no need to animate v1). | NEW `gen_blob_shadow` in `_gen_fix_audit.py`; programmer adds a `GroundShadow` child in the pawn + tree + building builders (or a tiny `BlobShadow.cs` that auto-attaches by sprite bounds). | Screenshot: every pawn/tree/building has a soft dark ellipse at its base; objects read as standing IN the world, not floating; shadow is subtle (not a hard black disc) and sits beneath the object's feet. | **H** | **S–M** |
| **V4** | **art** | The tree is a flat 2-tone blob — largest natural object, lowest effort-to-richness. | Re-do canopy with 3 tone clusters (`GRASS_DK` core → `GRASS_MD` → one `GRASS_LT` rim-light cluster offset up-left as if lit), subtle trunk-base shadow, `OUTLINE_PLANT`. Still flat, still recedes — richer SHAPE, not more detail. | `_gen_sprites.py::gen_tree` (or wherever A5 landed the flat tree). | Tree screenshot: canopy has visible internal light/shadow clustering (not one flat fill) and a consistent light direction; still lower saturation than pawns; still recedes behind a pawn placed next to it. | **M** | **S** |
| **V5** | **programmer** | Static world even where motion is *expected* — fire/stove dead, trees rigid. | **Ambient micro-motion**: (a) stove/fire/campfire sprite flickers (2-frame swap or scale/alpha pulse ~4 Hz on the FIRE_* pixels only); (b) optional very-subtle tree-canopy sway (±1 px horizontal sine, ~0.3 Hz) on the sprite child. Reuses the V1 sprite-child-offset pattern. | extend `PawnSpriteBob.cs` into a small shared `SpriteIdleAnim.cs`, OR per-object: `StoveFlicker.cs`; attach in stove/tree builders. | When a lit stove is on screen: its flame pixels visibly change between two frames 0.25 s apart (flicker). Trees (if swayed) show ≤1 px lateral drift, not a static lump. Nothing else jitters. | **M** | **S** |
| **V6** | **art** | Crop field = bare brown tilled lines; faint/absent plant markers (QA-flagged). | Distinct growth-stage markers ON the tilled rows: seedling (tiny `GRASS_LT` sprout dots) → growing (muted green rows) → ripe (`CROP_GOLD` heads, the reserved accent). Each stage clearly different at a glance. | `_gen_fix_audit.py::gen_crop_rice` growth-stage variants + the crop-field render wiring. | A planted field screenshot: tilled rows clearly carry plant sprites (not bare dirt); a ripe field is unmistakably gold and pops; an unripe field is muted green and recedes. | **M** | **S** |
| **V7** | **programmer** | Inspector empty-state is a bare "선택된 오브젝트 없음" line (QA-flagged) — looks unfinished. | Give the empty inspector a styled empty-state: header still drawn, a muted centered hint ("오브젝트를 선택하세요" + a small dimmed cursor/select glyph) inside the bordered panel, consistent with U5. | `PawnInfoPanel.cs` empty-state branch. | With nothing selected: the inspector still shows its bordered/titled panel with a centered muted hint and glyph, not a single cramped line of text on a bare rectangle. | **L** | **S** |
| **V8** | **art** | Day/night tint exists but the world has no atmospheric depth at the edges. | OPTIONAL subtle vignette / ambient edge-darkening overlay (a soft radial `OUTLINE_OBJ`-tone gradient at very low alpha) to add depth and focus the eye toward center. Must be barely perceptible — reject if it muddies the palette. | a full-screen overlay sprite + the camera/canvas builder. | Screenshot: frame corners are *very* slightly darker than center, adding depth; the effect is subtle enough that on-palette colors are unchanged in the play area. | **L** | **S** |

## Polish Wave v3 — dispatch order (impact-per-effort)

1. **V1 — Pawn walk-bob + idle breathe** (programmer). *The single biggest
   "feels like a real game" lever left. A dead-still colony reads as a tech demo;
   the moment pawns bob while walking, the whole scene comes alive — and it's
   cheap. Owns: NEW `PawnSpriteBob.cs`, sprite-child offset only.*
2. **V3 — Contact shadows** (art + programmer). *Grounds every object; kills the
   "pasted on" look across pawns, trees, AND buildings in one pass. Pairs
   naturally with V1 (both touch the pawn builder).*
3. **V2 — Low-density world scatter** (art). *Fills the empty world with quiet
   life; makes the whole map feel inhabited rather than a blank board.*

After these three: V4 (richer tree) → V5 (fire flicker / tree sway, completes the
"alive" story V1 starts) → V6 (crop stages) → V7 (inspector empty-state) → V8
(optional vignette). V8 is explicitly optional — reject if it muddies the palette.

QA gate unchanged: each item ships independently; QA Reads a fresh (and for V1/V5,
a TWO-frame) screenshot and checks the binary acceptance. Any item that violates a
north-star rule, changes pawn size, or restyles the art language = REJECT.
