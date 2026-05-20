# Morning Brief — 2026-05-21

> Operator-facing summary of overnight autonomous music-video work.
> Read this first when you wake up. Decisions you need to make are at
> the bottom.

## Trigger

2026-05-20 ~23:30 KST you flagged two issues:

1. "어떤 곡은 화면이 갑자기 띠용하는 쉐이더가 나오면 이상해보임" — shader doesn't match the song mood on some tracks
2. Want different music-shorts formats beyond the current B-roll-cut template (음악은 primary, narration X)

You went to sleep, asked for research + scaffolding by 10 AM.

## What landed (all on `main`, pushed)

**Commit `93cc5e8` — genre-aware shader presets**

| File | What it does |
|---|---|
| `skills/music-video/data/genre-presets.yaml` | 14-genre preset table (ambient, drone, shoegaze, lofi_hiphop, jazz, house, techno, synthwave, vaporwave, phonk, hyperpop, cottagecore, classical, dreamcore) |
| `scripts/music-video-shaders.sh` | +6 new shader effects: `scanline`, `chromatic_split`, `neon_edge`, `vhs`, `saturation_pulse`, `kaleidoscope` (existing pond/breathing/halation/combo unchanged) |
| `scripts/music-video-stillzoom.sh` | NEW entry point: image + music → 60s slow Ken-Burns 9:16 mp4 (no cuts) — required for ambient/classical/dreamcore |
| `scripts/music-video-genre.sh` | NEW wrapper: `<genre> <id> <music>` → resolves preset → exports env overrides → runs pipeline → chains matching post-shader |
| `docs/research/2026-05-21-music-shorts-formats-landscape.md` | 3.4K words: 2025-2026 viral patterns, 12 audio-primary format catalog, top-5 candidates, shader↔genre principles |
| `docs/research/2026-05-21-shader-song-mismatch-diagnosis.md` | Per-short analysis of 5/20 batch: Linen (ambient) and Rain (lo-fi) are worst mismatches, Noir (jazz) is best-matched |
| `outputs/demos/2026-05-21-genre-shader-experiments/` | **8 mp4s for side-by-side review** ↓ |

## Side-by-side demos to watch

Open `outputs/demos/2026-05-21-genre-shader-experiments/`:

| File | Compare against |
|---|---|
| `00-arcade-baseline-current.mp4` | (this is what synthwave looks like with current default treatment) |
| `01-arcade-scanline.mp4` | **proposed synthwave preset** — is this closer to actual synthwave visual? |
| `02-arcade-chromatic_split.mp4` | RGB-shift alternative |
| `03-arcade-neon_edge.mp4` | edge-detect neon alternative |
| `04-arcade-vhs.mp4` | VHS / vaporwave alternative |
| `05-arcade-saturation_pulse.mp4` | house / techno alternative |
| `06-arcade-kaleidoscope.mp4` | psychedelic alternative |
| `07-linen-ambient-preset.mp4` | **proposed ambient preset** — single image + slow zoom + halation, NO cuts, NO zoom-pulse. Compare to the 5/20 Linen short (cut-heavy, with zoom-pulse) which you flagged as the worst mismatch. |

## Root-cause of "띠용" — diagnosed

The pipeline default applies a **drum-onset zoom-pulse** (0.6s scale
bell at every detected drum hit) regardless of genre. For lo-fi
(forbids glitch) and ambient (forbids any sharp motion), this reads
as the "띠용". For synthwave and phonk it's actually appropriate
energy.

Fix: route each song to a genre-appropriate preset that disables
zoom-pulse where forbidden, swaps shader for genre-coded one
(scanline for synthwave, halation+stillzoom for ambient, etc.).

## Decisions you need to make

1. **Which preset(s) ship as defaults?** Demo files 01 + 07 are the
   two strongest candidates. Watch them and approve / reject /
   request tweaks.

2. **Retroactive regen for 5/20 batch?** The 5 uploaded shorts have
   the documented mismatches. Three options:
   - (a) Leave them — already public, mismatches are subtle, 4 subs
     don't notice
   - (b) Regenerate Linen + Rain (worst 2) and re-upload as v2 with
     correct presets, replace old privately
   - (c) Regenerate all 5

3. **Auto genre-detect?** Right now each render needs a `<genre>` arg.
   We could:
   - Filename sniff (`track1-coastline.mp3` → coastline → house)
   - Suno metadata read (if Suno embeds genre in mp3 tags)
   - Manual tag in a sidecar JSON
   - Stay manual (operator passes genre per render)

4. **More presets needed?** 14 covers most cases but missing:
   reggaeton, K-ballad, post-rock, math-rock, breakbeat, trap.
   Add as needed?

5. **Wrapper entry point**: should `scripts/music-video-genre.sh`
   become the default entry, replacing direct `run.sh` calls in
   `daily-music-video.sh` and the music-video skill?

## How to use right now

For any new track, instead of:

```bash
bash agents/missions/music-video/run.sh <id> <music.mp3> [keywords]
```

You'd do:

```bash
bash scripts/music-video-genre.sh <genre> <id> <music.mp3> [keywords]
# e.g.
bash scripts/music-video-genre.sh synthwave arcade02 assets/music/track3-arcade.mp3 \
  "neon city, retro arcade, sunset grid, palm trees"
```

For ambient/stillzoom genres add `--image=path/to/still.jpg`:

```bash
bash scripts/music-video-genre.sh --image=assets/stills/ambient-window.jpg \
  ambient linen02 assets/music/track5-linen.mp3
```

List available genres:

```bash
bash scripts/music-video-genre.sh --list
```

## Open work (continuing autonomously toward 10 AM)

Phase 2 (next): Spotify Canvas 8s loop mode (`--canvas` flag), kinetic
typography (`--phrases=phrases.txt`), structure-aware cuts (librosa
section detection). These are the remaining top-5 research candidates
beyond genre presets. Will commit incrementally as they land.
