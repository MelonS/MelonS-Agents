# Grade profile visual comparison — 2026-05-22

Visual validation of `scripts/music-video-grade.sh` (research §2
implementation, commit `1fef3f0`).  Same daytime Korean street
B-roll clip graded through 5 profiles to confirm each produces a
visually distinct base.

## Test method

- Source: `outputs/short.mp4` from the
  `music-video-qb-demo-kr2-035003` mission (Korean street + crowd,
  daylight, mixed neutral tones).
- One ffmpeg pass per profile via `scripts/music-video-grade.sh`.
- Frame extracted at t=25s (inside the phrase_climax window) for
  each output.
- All frames cached at `/tmp/grade-frames/*.jpg` (gitignored,
  local-only).

## Findings

| Profile | Visible effect | Match to genre intent |
|---------|----------------|-----------------------|
| `kr_warm_pastel` | Subtle.  Slight gamma lift + warm mids.  Reads as "softer / brighter daylight" but easy to miss on neutral daylight source. | ✓ for kpop_ballad ambient daylight scenes; needs darker / vocal-anchored source to show full effect |
| `hollywood_teal_orange` | Subtle.  Sky slightly cooler, midtones marginally warmer.  Closer to source than expected — the colorbalance numbers are conservative. | ⚠ delta too small on neutral source; will read on contrast-heavy scenes (sunset, night, person against sky) |
| `synthwave_neon` | Strong.  Sky shifts cyan-green, vintage curve adds magenta to highlights, +40% saturation pops the colored elements. | ✓✓ unmistakable retro/synthwave look — the most visually distinct profile |
| `lofi_warm_grain` | Strong.  Desaturated, slight warm midtone, visible temporal noise overlay.  Reads as "washed-out 70s film." | ✓✓ correct lofi visual code; the noise adds organic texture |
| `rnb_low_key` | Strong.  Darker midtones, crushed shadows (brightness -0.04 + contrast +10%), red lift in mids. | ✓ correct R&B noir / candle-light look |

## Validation

Three of five profiles produce visually unmistakable differences
from the un-graded source.  The two subtle profiles
(`kr_warm_pastel` + `hollywood_teal_orange`) work as intended but
on contrast-rich source material, not neutral daylight crowd shots.

The synthwave-neon-drive instrumental render in progress at
`records/missions/2026-05-22/music-video-qb-grade-synthwave-*/` will
provide the second validation: full pipeline including the grade
chain on a contrast-heavy source.

## Operator action

If the subtle profiles read too weakly on operator's vocal-track
renders, tune the numbers in `scripts/music-video-grade.sh`:

- `kr_warm_pastel`: bump `gamma` from 1.05 → 1.10 for stronger glow
- `hollywood_teal_orange`: bump `rs` from 0.10 → 0.15 (more teal in
  shadows), `rh` from 0.05 → 0.10 (more orange in highlights)

The conservative defaults come straight from the research doc
(`docs/research/2026-05-22-music-video-pro-practices.md` §2);
they err on the side of taste-preserving rather than dramatic
to avoid clashing with the shader layer that gets applied on top.

## Composition with shader

The grade stage runs BEFORE the shader (per
`scripts/music-video-genre.sh` integration commit `1fef3f0`).  So:

  source → base grade → shader → lyric overlay → thumbnail

This ordering means the shader operates on already-graded content,
which matches pro post-production sequence (color correction first,
then look development, then visual effects).

## End-to-end validation (synthwave demo)

Full pipeline render of `synthwave-neon-drive.mp3` through the new
chain — mission `qb-grade-synthwave-155730`:

1. base render (`short.mp4`, 24 MB)
2. grade pass — `synthwave_neon` (`short-grade.mp4`, 29 MB)
3. shader pass — `beat_burst` with shader_active_ratio=0.65
   (`short-grade-beat_burst.mp4`, 27 MB)
4. auto-thumbnail (`short-grade-beat_burst-thumb.jpg`)
5. auto upload-metadata template (`upload-metadata.md`)

Visual:
- Frame 25s: night-Shibuya Tokyo neon street, sky cyan tinted +
  magenta highlights on the saturated neon — unmistakable synthwave
  signature.  Pre-grade was good Pexels stock; post-grade is
  genre-coded.
- Frame 35s: warm-lit interior with two figures, magenta-shifted
  highlights + pink-lifted fleshtones — reads as retro arcade
  lounge aesthetic.

Confirms the chain composes correctly (no muddy stacking, no clipped
colors) AND that the grade is the differentiator that moves Pexels
stock from "generic" to "genre-coded".
