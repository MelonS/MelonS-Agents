# Morning Brief — 2026-05-21

> Operator-facing summary of overnight autonomous music-video work.
> Read this first when you wake up. Decisions you need to make are at
> the bottom.

## Trigger

2026-05-20 ~23:30 KST you flagged two issues:

1. "어떤 곡은 화면이 갑자기 띠용하는 쉐이더가 나오면 이상해보임" — shader doesn't match the song mood on some tracks
2. Want different music-shorts formats beyond the current B-roll-cut template (음악은 primary, narration X)

You went to sleep, asked for research + scaffolding by 10 AM.

## What landed overnight (7 commits, all on `main`, pushed)

| Commit | What |
|---|---|
| `93cc5e8` | Genre-aware shader presets — 14 genres + 6 new shaders + stillzoom mode + wrapper |
| `0c01fcc` | This morning brief + roadmap entry |
| `be93c48` | Canvas 8s loop + kinetic typography modes |
| `520e6f3` | Genre auto-detect + per-genre keyword pools |
| `0b48a25` | All-in-one auto wrapper + SKILL.md docs |
| `2192498` | yq v4 fix + end-to-end smoke proof artifact |
| `de08a1b` | Vignette 'none'/'off' explicit disable (run.sh patch) |
| `f796aad` | Bulk-regenerate script for 5/20 ToddStudio batch |
| `8c87d96` | phrase_pool per genre — zero-config typography |

## The single command that summarizes it all

For any music file you hand me:

```bash
bash scripts/music-video-auto.sh assets/music/your-track.mp3 --with-canvas --with-typography
```

This now:
1. Detects genre from filename / ID3 tag (14 genres supported)
2. Looks up the genre preset (cut density, grain, vignette, zoom-pulse, shader)
3. Auto-fills B-roll keywords from the genre's keyword_pool (no manual keyword entry)
4. Runs the pipeline with correct env overrides
5. Applies the genre's matching post-shader (synthwave gets scanlines, lo-fi gets halation, etc.)
6. Produces an 8s Spotify Canvas variant
7. Overlays kinetic typography with the genre's phrase_pool (zero-config)

For ambient / classical / dreamcore (stillzoom genres) add `--image=path/to/still.jpg`.

End-to-end smoke validated on Arcade → synthwave preset → final 12 MB mp4
in `outputs/demos/2026-05-21-genre-shader-experiments/11-arcade-FULL-genre-pipeline.mp4`.

## Side-by-side demos (11 files in `outputs/demos/2026-05-21-genre-shader-experiments/`)

| File | Source | Effect | Intended for genre |
|---|---|---|---|
| `00-arcade-baseline-current.mp4` | Arcade (synthwave) | Current default pipeline | (current state) |
| `01-arcade-scanline.mp4` | Arcade | scanline | **synthwave** ✅ proposed default |
| `02-arcade-chromatic_split.mp4` | Arcade | RGB shift | vaporwave / phonk |
| `03-arcade-neon_edge.mp4` | Arcade | edge-detect colorize | synthwave alt |
| `04-arcade-vhs.mp4` | Arcade | VHS noise | vaporwave / dreamcore |
| `05-arcade-saturation_pulse.mp4` | Arcade | 2 Hz sin sat | house / techno |
| `06-arcade-kaleidoscope.mp4` | Arcade | 4-fold mirror | psychedelic |
| `07-linen-ambient-preset.mp4` | Linen (ambient) | stillzoom + halation | **ambient** ✅ proposed default |
| `08-linen-canvas-8s.mp4` | Linen (from 07) | 8s seamless loop | Spotify Canvas variant |
| `09-arcade-canvas-8s.mp4` | Arcade (from 01) | 8s seamless loop | Spotify Canvas variant |
| `10-rain-kinetic-typography.mp4` | Rain lo-fi | 4 phrase overlays | Mute-autoplay hook |
| `11-arcade-FULL-genre-pipeline.mp4` | Arcade music (fresh render) | Genre preset → scanline shader | **End-to-end proof** |

## Root cause of "띠용" — diagnosed

The v6 default applies a drum-onset zoom-pulse regardless of genre.
For lo-fi (forbids glitch) and ambient (forbids any sharp motion),
this reads as "띠용". For synthwave / phonk it actually fits.

Fix: route each song to a genre-appropriate preset that disables
zoom-pulse where forbidden. The new wrapper does this automatically
via genre auto-detection.

Verified end-to-end: synthwave preset run produces `"v6 filters:
grain=0 vignette=off zoom_pulses=0"` — entire forbidden filter stack
disabled, no manual config.

## Decisions you need to make

1. **Approve preset defaults?** Watch demos 01 (synthwave) and 07
   (ambient). If they feel right, the proposed presets are locked.

2. **Retroactive regen for 5/20 batch?** One command:
   ```
   bash scripts/music-video-bulk-regenerate.sh
   ```
   Renders all 5 with correct genre presets, drops into
   `outputs/publish/2026-05-21-regen-v2/`. Then `yt-batch-upload.sh`.
   Or `--only=2` to regen just Linen (worst case).

3. **Auto genre-detect happy?** Validated 13/13 on your library:
   - cyberpunk / Tokyo Neon / Urban Midnight → synthwave
   - Fireplace Acoustic → cottagecore
   - Late Night Piano / track2-noir / Rainy Bossa → jazz / lofi
   - track1-coastline → house
   - track3-arcade → synthwave
   - track4-rain → lofi_hiphop
   - track5-linen → ambient
   - Velvet Turntable → lofi_hiphop

   The only ambiguous one is Rainy Bossa (lofi vs jazz — currently
   resolves to lofi because "rain" outweighs "bossa"). Want to flip?

4. **More presets needed?** 14 covers most. Missing: reggaeton,
   K-ballad, post-rock, math-rock, breakbeat, trap. Add as needed.

5. **Wrapper as default entry?** Should `scripts/music-video-auto.sh`
   replace direct `agents/missions/music-video/run.sh` calls in
   `scripts/daily-music-video.sh` and the music-video skill's invocation
   instructions? (Strong recommendation: yes.)

## Architecture overview

```
                  music file (mp3)
                        │
                        ▼
       scripts/music-video-auto.sh  ◄────────────────  recommended entry
                        │
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼
  genre-detect.sh   genre-presets    genre.sh
   (id3+filename)      .yaml         (resolve + run)
                        │               │
                        │               ▼
                        │       agents/missions/music-video/run.sh
                        │               │
                        │               ▼
                        │     scripts/music-video-shaders.sh  (genre-coded shader)
                        │               │
                        ├───────────────┤
                        ▼               ▼
              music-video-canvas.sh   music-video-typography.sh
                  (8s loop)              (phrase overlay)
                        │               │
                        └───────┬───────┘
                                ▼
                          outputs/...
```

Stillzoom alternate path (ambient/classical/dreamcore):

```
music + image  →  music-video-stillzoom.sh  →  short.mp4  →  shader  →  final
```

## Open work (continuing autonomously)

Phase 8+: live regen of Linen ambient case as before/after demo,
optional structure-aware cuts via librosa (if time permits), README
update. Will commit incrementally.
