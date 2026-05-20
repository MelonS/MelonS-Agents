# Music-Video Genre-Aware Demos — 2026-05-21 overnight

29 demo mp4s generated autonomously across the 2026-05-21 session.
All gitignored — local-only.  Compare side-by-side to evaluate the
new genre-aware pipeline.

## Phase 1 — Shader catalog (00-13)

Source: existing Arcade synthwave v2 render (where indicated).

| # | File | Demonstrates |
|---|------|--------------|
| 00 | `00-arcade-baseline-current.mp4` | Current default treatment (the "띠용" problem) |
| 01 | `01-arcade-scanline.mp4` | scanline shader (synthwave-coded) |
| 02 | `02-arcade-chromatic_split.mp4` | RGB shift (vaporwave/phonk-coded) |
| 03 | `03-arcade-neon_edge.mp4` | edge-detect neon colorize (synthwave alt) |
| 04 | `04-arcade-vhs.mp4` | VHS noise + chroma blur (dreamcore-coded) |
| 05 | `05-arcade-saturation_pulse.mp4` | 2 Hz sin saturation (house-coded) |
| 06 | `06-arcade-kaleidoscope.mp4` | 4-fold mirror (psychedelic) |
| 13 | `13-arcade-{beat_burst,strobe,shake,color_burst,light_rays}.mp4` | 5 beat-synced shaders |

## Phase 2 — Format catalog (07-12, 14-18)

| # | File | Demonstrates |
|---|------|--------------|
| 07 | `07-linen-ambient-preset.mp4` | stillzoom mode + halation (ambient preset) |
| 08 | `08-linen-canvas-8s.mp4` | Spotify Canvas 8s loop |
| 09 | `09-arcade-canvas-8s.mp4` | Canvas 8s loop on cut-based source |
| 10 | `10-rain-kinetic-typography.mp4` | mood phrase overlay (instrumental) |
| 11 | `11-arcade-FULL-genre-pipeline.mp4` | End-to-end synthwave preset → scanline |
| 12 | `12-citypop-vocal-lyrics-kr.mp4` | 60s citypop + Korean lyrics overlay |
| 14 | `14-NEW-synthwave-neon-drive-beat_burst.mp4` | New synthwave preset on operator's track |
| 15 | `15-NEW-phonk-drift-tokyo-shake.mp4` | New phonk preset (shake shader) |
| 16 | `16-NEW-house-disco-mirrorball-color_burst.mp4` | New house preset (color_burst) |
| 17 | `17-NEW-citypop-vocal2-lyrics.mp4` | 60s citypop2 + Korean lyrics |
| 18 | `18-NEW-citypop1-FULL-162s-lyrics.mp4` | citypop1 **FULL-LENGTH 162s** + lyrics |

## Phase 3 — Vocal channel pivot (19-29)

Operator's strategic shift mid-session: "가사 있는 곡이 너무 신세계임...
K-팝 / 미국팝송 / R&B / 빌보드 상위 곡 수준으로 여러 장르로."  All
full-length (162-225s) with appropriate genre presets + lyrics overlays
where applicable.

| # | File | Genre | Lyrics | Size |
|---|------|-------|--------|------|
| 19 | `19-citypop-eng-midnight1-FULL-lyrics.mp4` | citypop (eng) | "Midnight Rambler" 12 lines | 86 MB |
| 20 | `20-citypop-eng-midnight2-FULL-lyrics.mp4` | citypop (eng) | same lyrics, variant render | 86 MB |
| 21 | `21-dreampop-blue-hours-FULL.mp4` | shoegaze | base render only (no lyrics file) | 56 MB |
| 22 | `22-kpop-ballad-eodi1-FULL-lyrics.mp4` | kpop_ballad | "어디쯤이야" 13 KR lines | 47 MB |
| 23 | `23-kpop-ballad-eodi2-FULL-lyrics.mp4` | kpop_ballad | same lyrics, variant | 64 MB |
| 24 | `24-kpop-dance-siren1-FULL-lyrics.mp4` | kpop_dance | "사이렌" 13 KR lines | 37 MB |
| 25 | `25-kpop-dance-siren2-FULL-lyrics.mp4` | kpop_dance | same lyrics, variant | 41 MB |
| 26 | `26-rnb-late-light1-FULL-lyrics.mp4` | rnb | "Late Light" 13 lines | 80 MB |
| 27 | `27-rnb-late-light2-FULL-lyrics.mp4` | rnb | same lyrics, variant | 80 MB |
| 28 | `28-uspop-tomorrow1-FULL-lyrics.mp4` | uspop | "Tomorrow Is A Question" 13 lines | 55 MB |
| 29 | `29-uspop-tomorrow2-FULL-lyrics.mp4` | uspop | same lyrics, variant | 61 MB |

## How to compare

For each genre, compare variant 1 vs variant 2 → choose the better
take (Suno generates 2 versions per prompt, operator kept both).

Watch for:
- **Lyrics legibility** at 4 rotating positions (top / bottom /
  left-aligned / right-aligned).  Genre-themed color palettes.
- **B-roll coherence** with the genre's keyword_pool (warm Tokyo
  neon for citypop, candle/red-velvet for rnb, fashion/LA for uspop,
  neon stage for kpop_dance, etc.).
- **Visual energy match** with vocal energy.  No "띠용" zoom-pulses
  on slow ballads (kpop_ballad / rnb); color_burst lands on kpop_dance
  drops.

## Total size

~1.3 GB across 29 mp4s (gitignored).
