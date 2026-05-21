# Music-video pipeline reference — 2026-05-22

Consolidated reference for the music-video Skill (`skills/music-video/`)
after the 2026-05-22 quality-bar work.  Single page index of every
env var, flag, and preset field a operator might touch.

## Entry points

| Caller | When to use |
|--------|-------------|
| `scripts/music-video-auto.sh <track>` | The default operator-facing wrapper.  Genre auto-detect + full pipeline + optional Canvas / typography / lyrics / AI-still. |
| `scripts/music-video-genre.sh <genre> <id> <track>` | Manual genre selection (skip auto-detect).  Same pipeline. |
| `agents/missions/music-video/run.sh <id> <track>` | Low-level mission entry.  No genre routing.  Operator overrides via env. |

## Genre preset fields (in `skills/music-video/data/genre-presets.yaml`)

| Field | Type | Meaning | Default if unset |
|-------|------|---------|------------------|
| `aliases` | `[string]` | Alternate genre names accepted | — |
| `lang_anchor` | `ko / en / mixed / neutral` | Ethnicity / language anchor for B-roll + AI-still | `neutral` |
| `phrase_beats` | int | Cut every N beats | `12` (run.sh default) |
| `grain_intensity` | int 0–10 | Film grain | `8` |
| `vignette_angle` | string (ffmpeg expr) | Vignette PI/N angle, or `off` / `none` | `PI/5` |
| `zoom_pulse_amp` | float 0–1 | Drum-onset zoom-pulse strength | `0.08` |
| `shader` | string | Post-shader effect (see catalog below) | none |
| `shader_pool` | `[string]` | Alternative — pool to rotate through per render | — |
| `shader_active_ratio` | float 0–1 | Shader strength (0 = off, 1 = full) | `1.0` |
| `lut_direction` | string | LUT/color grading direction (descriptive) | — |
| `cut_mode` | `cuts / stillzoom` | Discrete cuts vs Ken-Burns on still | `cuts` |
| `forbidden_effects` | `[string]` | Effects to NEVER apply to this genre | — |
| `keyword_pool` | `[string]` | Default Pexels queries | — |
| `phrase_pool` | `[string]` | Kinetic typography phrases (instrumental) | — |
| `notes` | text | Operator-facing rationale | — |

## Shader catalog (23 effects)

| Family | Shaders |
|--------|---------|
| Classic | `pond`, `breathing`, `halation`, `combo` |
| Genre-coded | `scanline`, `chromatic_split`, `neon_edge`, `vhs`, `saturation_pulse`, `kaleidoscope` |
| Beat-synced | `beat_burst`, `strobe`, `shake`, `color_burst`, `light_rays` |
| Stage-1 (cinematic accent) | `light_leak`, `duotone`, `vignette_pulse` |
| Stage-2 (texture) | `paper_grain`, `dust_speck`, `posterize` |
| Stage-3 (temporal) | `trail_echo`, `soft_bloom` |

Genre-fit recommendations in `docs/research/2026-05-22-shader-vocabulary.md`.

## Shader gate modes (`MUSIC_VIDEO_SHADER_GATE` env)

| Mode | Behavior |
|------|----------|
| `uniform` (default) | Shader applied across full duration, attenuated by `shader_active_ratio` |
| `phrase_climax` | Shader active only in the center window of width `RATIO × duration`, with 0.5s trapezoid fade |
| `onsets` | Shader fires as Gaussian bell at every Nth drum onset (N derived from ratio).  Auto-fallback to `beats` if onsets too sparse; or to `uniform` if both <5 events |
| `beats` | Same as onsets but reads `beats-real.txt` instead.  More "regular" pulse |

## Music-video CLI flags (`music-video-auto.sh`)

| Flag | Purpose |
|------|---------|
| `--short-id=ID` | Override short_id (default: derived from filename) |
| `--with-canvas` | Also produce 8s Canvas variant |
| `--with-typography "phrase1,phrase2,..."` | Kinetic mood phrases (or genre pool if no value given) |
| `--with-lyrics=PATH` | Lyric overlay (plain text → whisper-aligned; or LRC → as-is) |
| `--lyrics-no-align` | Skip whisper alignment, use auto-spaced |
| `--with-audio-reactive` | Audio-reactive saturation grading variant |
| `--ai-still` | Pollinations.ai still for stillzoom (vs Pexels) |
| `--full-length` | Use music's actual duration (vs default 60s) |
| `--duration=N` | Explicit duration in seconds |
| `--image=PATH` | Force a specific still for stillzoom mode |

## Environment variable index

### Pipeline / mission

| Var | Effect |
|-----|--------|
| `PEXELS_API_KEY` | Required for non-demo Pexels fetches |
| `RECORDS_DIR` | Mission output root (default `./records`) |
| `FFMPEG_THROTTLE=1` | Route ffmpeg through cpulimit + nice (default per `.env`) |
| `MUSIC_VIDEO_DURATION=N` | Render duration in seconds |
| `MUSIC_VIDEO_DEMO_MODE=1` | Bypass Pexels API + operator-music with bundled CC-BY caches |
| `MUSIC_VIDEO_GENRE=NAME` | Override auto-detect |
| `MUSIC_VIDEO_BROLL_DIR=PATH` | Use operator-supplied clips instead of Pexels fetch |

### B-roll dedup (quality-bar #1)

| Var | Effect |
|-----|--------|
| `BROLL_HISTORY` | `on` (default) / `off` per-render disable |
| `BROLL_HISTORY_FILE` | Path to registry (default `records/youtube/broll-used.txt`) |

### Lang-anchor (quality-bar #5/#6)

| Var | Effect |
|-----|--------|
| `MUSIC_VIDEO_LANG_ANCHOR` | Override the preset's anchor (`ko` / `en` / `mixed` / `neutral`) |

### Shader gating (quality-bar #2)

| Var | Effect |
|-----|--------|
| `MUSIC_VIDEO_SHADER_RATIO` | Override the preset's `shader_active_ratio` |
| `MUSIC_VIDEO_SHADER_GATE` | Pick gate mode (default `uniform`) |
| `MUSIC_VIDEO_SHADER_BEATS` | Beats file for `beats`/`onsets-fallback` |
| `MUSIC_VIDEO_SHADER_ONSETS` | Onsets file for `onsets` mode |
| `MUSIC_VIDEO_SHADER_VARIANT=N` | Pick `shader_pool[N-1]` explicitly |

### Lyrics + alignment (quality-bar #3)

| Var | Effect |
|-----|--------|
| `LYRIC_LEAD_MS` | Pre-roll for vocal anticipation (default 200ms) |
| `WHISPER_LANG` | Force whisper language (else auto from lyric chars) |
| `LYRIC_FORCE_OVERLAY=1` | Render overlay even when Suno-drift gate would skip |
| `LYRICS_FONT` | Override font path |
| `LYRICS_SIZE` | Override font size (default 88 for 1080×1920) |
| `LYRICS_FADE` | Fade in/out duration (default 0.4s) |

## QA hooks

| Tool | What it checks |
|------|----------------|
| `scripts/music-video-qa-anchor.sh <mission_dir> --genre=NAME` | Score B-roll vs lang_anchor.  Emits JSON + exit 0/1/2 (PASS/WARN/FAIL) |
| `<lrc>.json` sidecar | Per-render alignment confidence verdict (auto-emitted by `music-video-lyric-align.sh`) |

## Quality-bar gate summary (from 2026-05-22 directives)

A music-video render meets the quality bar when:

- B-roll history registry has the chosen clip IDs (auto-enforced).
- `lang_anchor` matches the lyric language and B-roll respects it
  (QA gate scores ≥30% anchor-matching).
- Lyric overlay timing within ±200ms of vocal cues (whisper align
  confidence ≥0.50 on ≥40% of lines).
- Shader is restrained (uniform ≤ ratio attenuation OR
  phrase_climax / onsets gating).
- No reused B-roll across the channel's history.

## Implementation reference

- `docs/research/2026-05-22-music-video-quality-bar.md` — full
  directive decomposition + phase plan
- `docs/research/2026-05-22-shader-vocabulary.md` — shader catalog +
  mood taxonomy + per-genre re-mapping recommendations
- `docs/research/2026-05-22-qb-demo-verification.md` — empirical
  validation across 4 demo renders (KR + EN)
- `docs/daily/2026-05-22-morning-brief.md` — autonomous-block
  summary with all commits

## Operator decision points (open)

- Per-genre `shader_pool` activation: which presets should rotate?
- Per-genre `shader_active_ratio` re-tuning after viewing demos.
- Per-genre `lang_anchor` for the 14 currently-neutral instrumental
  genres if operator wants anchor on those too.
- `LYRIC_FORCE_OVERLAY` policy: default skip-on-drift is safe, but
  operator may want WARN-and-render for content authority.
