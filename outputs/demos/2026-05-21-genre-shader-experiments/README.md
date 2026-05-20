# Genre-shader experiments — 2026-05-21 morning brief demos

Generated overnight by autonomous music-video improvement run. Compare
side-by-side to decide which presets to ship as defaults.

## Files

| File | Source | Effect | Intended for genre |
|---|---|---|---|
| `00-arcade-baseline-current.mp4` | Arcade (synthwave) | Current pipeline (grain + vignette + zoom-pulse) | (no preset — current state) |
| `01-arcade-scanline.mp4` | Arcade | scanline | **synthwave** (proposed default) |
| `02-arcade-chromatic_split.mp4` | Arcade | chromatic_split (RGB shift) | vaporwave / phonk |
| `03-arcade-neon_edge.mp4` | Arcade | neon_edge (edge-detect + colorize) | synthwave alt |
| `04-arcade-vhs.mp4` | Arcade | vhs (chromatic + noise + chroma blur) | vaporwave / dreamcore |
| `05-arcade-saturation_pulse.mp4` | Arcade | saturation_pulse (sin wave 2 Hz) | house / techno |
| `06-arcade-kaleidoscope.mp4` | Arcade | kaleidoscope (4-fold mirror) | psychedelic / electronic |
| `07-linen-ambient-preset.mp4` | Linen (minimal ambient) | stillzoom + halation | **ambient** (proposed default) |
| `08-linen-canvas-8s.mp4` | Linen (from 07) | Canvas 8s seamless loop @720×1280 | Spotify Canvas variant |
| `09-arcade-canvas-8s.mp4` | Arcade (from 01) | Canvas 8s seamless loop | Spotify Canvas variant |
| `10-rain-kinetic-typography.mp4` | Rain lo-fi (uploaded) | 4 phrase overlays (mood text on phrase boundaries) | Mute-autoplay hook |

## Decision point

Compare `00` vs `01` — same source (Arcade synthwave track), current
treatment vs proposed synthwave preset. Which feels more "synthwave"?

Compare `00` (Arcade with current treatment) implicitly with `07` (Linen
with ambient preset) — note how 07 has **zero cuts and zero zoom-pulse**.
For a 90 BPM ambient track, this is much closer to the genre's visual
contract than the cut-heavy current pipeline.

## Why these matter

Operator flagged 2026-05-20 ~23:30 KST that "어떤 곡은 화면이 갑자기 띠용
하는 쉐이더가 나오면 이상해보임. 곡과 맞아야 함." Root-cause analysis in
`docs/research/2026-05-21-shader-song-mismatch-diagnosis.md` traces this to
the v6 zoom-pulse firing on drum onsets regardless of genre — which is
*forbidden* by the lo-fi and ambient genres' visual contracts.

The fix: **genre-aware shader selection** via
`scripts/music-video-genre.sh <genre> <id> <music>`. 14 genres now have
declarative presets at `skills/music-video/data/genre-presets.yaml`.

## Next operator decisions (logged in morning brief)

1. Approve which preset(s) to lock in as defaults
2. Retroactively regenerate 5/20 batch with correct presets? Or apply to next batch only?
3. Add more presets beyond 14? (Reggaeton, K-ballad, post-rock missing)
4. Build a `detect-genre` helper that auto-tags Suno-generated tracks from filename / metadata, or keep manual tag?
