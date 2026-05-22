# music-video skill — CHANGELOG

All notable changes to the music-video Skill (`skills/music-video/`)
plus its supporting scripts under `scripts/`.

Format: chronological newest-first, semver-ish (skill features →
minor bumps).  Skill version field in `SKILL.md` lags behind this
file — operator decides when to formally bump.

---

## Unreleased — 2026-05-23 research follow-on wave

Continuation of the 2026-05-22 work, picking up items 5-8 from the
research action lists.

### Pipeline features

- **KR Hangul + Romanization bilingual stack** (commit `e1e3838`)
  — `LYRICS_BILINGUAL=1` env activates two-line drawtext stack:
  Hangul above (88pt), Revised Romanization below (61pt, 70% size).
  Romanization derived from `scripts/romanize-hangul.sh` (Python
  stdlib unicodedata, no external lib).  KR-canonical convention
  per research §8.
- **J/L-cut trail for lyric overlay** (commit `9015d10`) —
  `LYRIC_TRAIL_MS` extends each lyric line's end past the next-line
  boundary.  Pairs symmetrically with `LYRIC_LEAD_MS` to form the
  J/L envelope pro music-video editors use.  Default 0 (back-compat).
- **Drop detection + drops gate** (commits `c242e4e`, `e9af4b7`) —
  `scripts/audio-analyze.sh` emits per-second RMS curve + drops
  timestamps (top-10% sustained ≥ 2s) via pure ffmpeg `astats`
  filter.  Auto-runs in run.sh step 2b.  New
  `MUSIC_VIDEO_SHADER_GATE=drops` mode fires shader as wide
  gaussian (σ=1.5s) at each detected drop — pro convention for
  climax accents.

### New utility scripts

- `scripts/romanize-hangul.sh` — Revised Romanization of Hangul
  text using only Python stdlib `unicodedata`.
- `scripts/audio-analyze.sh` — per-second RMS + drops detection.

### Bug fixes

- Stride math in shader-events branch was `NR % s == 1` which is
  never true for s=1; corrected to `(NR-1) % s == 0` — classical
  every-Nth starting from first.
- `set -u` unbound-variable trap on $REPO_ROOT_SCRIPT in lyrics.sh
  bilingual block — replaced with direct BASH_SOURCE derivation.

### Validated

- Drop detection on 3 genre track types (kpop_dance / kpop_ballad
  / synthwave) returns track-structure-meaningful drops (mid-song
  peak for dance, late climax for ballad, both chorus drops for
  synthwave).
- Bilingual stack renders cleanly on KR ballad source —
  "변한 건 너의 자리뿐" + "byeon han geon neo yi ja ri bbun" both
  visible in safe-band y=0.22h.

---

## Unreleased (2026-05-22 quality-bar batch)

Substantial work landed in the 2026-05-22 autonomous block addressing
six operator quality directives + scaffold around them.  See
[`docs/research/2026-05-22-music-video-quality-bar.md`](../../docs/research/2026-05-22-music-video-quality-bar.md)
for the directive decomposition and
[`docs/research/2026-05-22-qb-demo-verification.md`](../../docs/research/2026-05-22-qb-demo-verification.md)
for empirical validation across 4 demo renders.

### Pipeline features

- **B-roll dedup registry** (`records/youtube/broll-used.txt`, 271
  ids seeded).  Pexels caller paths consult + append; disable via
  `BROLL_HISTORY=off`.  Backfill from prior records via
  `scripts/broll-history-backfill.sh`.
- **Whisper-based lyric alignment** (`scripts/music-video-lyric-align.sh`).
  Word-level for KR, segment-level for EN.  Emits LRC + JSON sidecar
  with drift verdict (OK / WARN / FAIL).  Operator-configurable
  pre-roll via `LYRIC_LEAD_MS` (default 200ms).
- **Suno-drift gate** in `music-video-lyrics.sh` — skips overlay
  when alignment confidence too low.  Override with
  `LYRIC_FORCE_OVERLAY=1`.
- **Lang anchor** on every preset (`lang_anchor: ko|en|mixed|neutral`).
  Vocal-anchored genres inject person-keyword Pexels queries at
  every 3rd segment.  Tightened vocal genre keyword_pools with
  geographic markers (kpop_ballad → seoul-prefixed; uspop → NYC/LA;
  rnb → manhattan/harlem).
- **Shader restraint** (`shader_active_ratio` per preset, 0.35-1.0).
  Four gate modes via `MUSIC_VIDEO_SHADER_GATE`:
  - `uniform` — blend-back to original at ratio opacity (Phase 1).
  - `phrase_climax` — center-window trapezoid envelope (Phase 2).
  - `onsets` — gaussian bells at drum onsets (Phase 3); auto-falls
    back to `beats` if onsets too sparse, to `uniform` if both <5.
  - `beats` — same mechanism, regular-pulse from beat track.
  Event count capped at 30 (`SHADER_EVENT_CAP`) to dodge ffmpeg
  expr-length budget.
- **Shader catalog 15 → 23**:
  - Stage 1 (cinematic accent): `light_leak`, `duotone`, `vignette_pulse`.
  - Stage 2 (texture): `paper_grain`, `dust_speck`, `posterize`.
  - Stage 3 (temporal): `trail_echo`, `soft_bloom`.
- **shader_pool** field — preset can declare a pool; picker is
  deterministic md5(short_id) mod len.  `MUSIC_VIDEO_SHADER_VARIANT=N`
  forces a specific index.
- **`MUSIC_VIDEO_SHADER=NAME` env override** — force any shader on
  any genre for one-off tests.
- **Auto-wrap long lyric lines** in `music-video-lyrics.sh` —
  language-aware thresholds (KO 14ch, EN 22ch).
- **Apostrophe → U+2019 substitution** in lyric escape — bypasses
  the ffmpeg drawtext close-escape-reopen leak that rendered the
  alpha expression as visible text.

### New utility scripts

- `scripts/first-touch.sh` — single-command guided first-run wizard
  (zero-account demo; Build Day Seoul prep payload).
- `scripts/music-video-batch.sh` — multi-track render wrapper with
  auto lyric pairing.
- `scripts/music-video-validate.sh` — combined gate aggregating
  file-integrity + qa-anchor + lyric-drift + broll-dedup.
- `scripts/music-video-qa-anchor.sh` — scores B-roll vs lang_anchor;
  emits JSON + exit 0/1/2 (PASS/WARN/FAIL).
- `scripts/music-video-thumbnail.sh` — extract 1080×1920 JPEG at
  midpoint (or custom `--at=N|N%`).  Auto-chained by music-video-auto.
- `scripts/music-video-trim.sh` — trim to YT Shorts (180s),
  TikTok-compact (60s), or arbitrary duration.  Stream-copy fast
  path; re-encode only on non-zero start.
- `scripts/music-video-upload-meta.sh` — generate per-platform
  upload metadata template with genre-appropriate hashtag bundle.
  Auto-chained by music-video-auto.
- `scripts/lyric-extract.sh` — whisper-based lyric pull from a
  vocal track, for tracks with no prompt lyric file or where Suno
  drifted from the prompt.
- `scripts/broll-history-backfill.sh` — seed the dedup registry
  from existing mission records.

### Tests

- `scripts/test-shader-gates.sh` — 6 cases covering all 4 gate modes
  + fallbacks.
- `scripts/test-first-touch.sh` — 3 cases (help, check mode, error).
- `scripts/test-music-video-batch.sh` — 4 cases (help, dry-run
  enumerate, lyric pairing, unknown flag).
- `scripts/test-qa-anchor.sh` — 6 cases (all anchor families +
  fallbacks + missing genre).
- `scripts/test-music-video-validate.sh` — 4 cases (KR all match,
  KR zero match, no-genre WARN, missing mp4 FAIL).
- `scripts/test-lyric-extract.sh` — 4 cases (no-args, real extract,
  marker strip, header).
- `scripts/test-thumbnail.sh` — 5 cases (no-args, default midpoint,
  seconds, percent, unknown flag).
- `scripts/test-music-video-trim.sh` — 6 cases (no-args, short-on-
  short copy, trim-to-5s, shorts-max, start-offset, unknown).
- `scripts/test-music-video-upload-meta.sh` — 4 cases (no-args, KR
  hashtags, genre inferred, ko lang detected).
- `scripts/test-all.sh` — aggregator (excludes slow tests by default;
  11/11 fast tests PASS in ~80s).

### Operator-facing artifacts

- `docs/music-video-pipeline-reference.md` — single-page env var +
  flag + shader catalog + QA hook index.
- `docs/daily/2026-05-22-morning-brief.md` — autonomous block summary.
- `docs/research/2026-05-22-music-video-quality-bar.md` — directive
  decomposition.
- `docs/research/2026-05-22-shader-vocabulary.md` — shader catalog +
  mood family taxonomy + per-genre re-mapping recommendations.
- `docs/research/2026-05-22-qb-demo-verification.md` — empirical
  validation of demo renders #1-4.
- `docs/engineering-case-studies.md` §9 — "The quality-bar wasn't
  a bug — it was 6 contracts the system didn't enforce".

### Open operator decisions (queued in `docs/roadmap.md` suggest block)

- Per-genre `shader_pool` activation: which presets should rotate
  through which pools?
- Per-genre `shader_active_ratio` re-tuning after operator visually
  evaluates demos.
- Per-genre `lang_anchor` for the 14 currently-neutral instrumental
  genres if operator wants ethnicity anchoring on those too.
- `LYRIC_FORCE_OVERLAY` policy: default skip-on-drift is safe;
  operator may want WARN-and-render for content authority.

---

## v1.0.0 — 2026-05-19

Initial agentskills.io-compliant Skill ship.  `SKILL.md` shipped at
`skills/music-video/SKILL.md` with `scripts/run.sh` symlinked to
`agents/missions/music-video/run.sh` (no logic duplication).

Pipeline at this point:
- aubiotrack beat detection + phrase boundaries (every Nth beat)
- aubioonset drum-hit detection for glitch placement
- Per-segment Pexels B-roll fetch (mood keywords)
- Variable per-clip speed by keyword class
- 4 post-shader effects: pond / breathing / halation / combo
- Vintage v6 processing: film grain, vignette, zoom-pulse

Validated against Hermes Agent runtime via drop-in test (12/12 pass).
