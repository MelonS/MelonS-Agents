# Morning Brief — 2026-05-22

Autonomous block: 2026-05-22 ~01:30 → ~04:00 KST (≈2.5h actual work
inside the 9h window operator allocated until 11:00 KST).

## What landed (12 commits)

### Quality-bar response (6 directives → 5 phases shipped)

Operator gave six directives 2026-05-22 ~01:30 KST after reviewing
the 12 vocal demos from the overnight 5/21 batch.  Decomposed in
[`docs/research/2026-05-22-music-video-quality-bar.md`](../research/2026-05-22-music-video-quality-bar.md);
shader research in [`docs/research/2026-05-22-shader-vocabulary.md`](../research/2026-05-22-shader-vocabulary.md).

| # | Directive | Phase | Commit | Mechanism |
|---|-----------|-------|--------|-----------|
| 1 | No B-roll reuse across shorts | A.1 | `05e6c2a` | `records/youtube/broll-used.txt` registry, 196 ids seeded |
| 3 | Lyric sync ±200ms | A.2 | `fa1ec72` | whisper word-level (KR) / segment-level (EN) + difflib alignment |
| 5+6 | KR→KR person / EN→global | A.3 | `cce3f40` | `lang_anchor` field per preset, person-keyword injection every 4th seg |
| 2 | Shader restraint | C.1 | `52b6eb4` | `shader_active_ratio` per preset, blend-back-to-original |
| 4 | Shader catalog expand | B.1 | `1c87377`, `7d8e9b1` | 8 new shaders across Stage-1/2/3 (now 23 total) |

### YT stats Phase 1 (snapshot open item C)

| Commit | Content |
|--------|---------|
| `a43057f` | `yt-stats-collect.sh` + `yt-stats-diff.sh` — Data API daily snapshot, 25 videos collected, no new OAuth |
| `ec5a5bb` | `com.melons.agents.yt-stats` launchd job, daily 09:00 KST.  Dreampop `KirKdDUWOpc` defused via videos.update (privacyStatus=private, publishAt cleared) |

### Documentation

| Commit | Content |
|--------|---------|
| `0703be8` | roadmap.md Done entries + Stage-2/3 + C.1 Phase-2 + A.3 QA gate suggest block |

## Anchor-coverage progression (same kpop_ballad track, 3 renders)

| Demo | Commit | Anchor match | Notes |
|------|--------|--------------|-------|
| #1   | shipped 02:55 KST | 3/8 = 38% | rate 25%, original pool |
| #2   | shipped 03:08 KST | 4/8 = 50% | rate 33%, original pool |
| #4   | shipped 03:50 KST | 8/8 = **100%** | rate 33%, tightened pool |

100% is the ceiling — every keyword in the genre's pool now carries
an explicit Seoul/Korean anchor.  Frame 30s of demo #4 shows Korean
crowd walking past a Seoul landmark, Korean lyric "변한 건 너의
자리뿐" centered, halation+vignette in the phrase_climax window —
the literal realization of directive #5.

EN demo #3 (uspop): 6/10 = 60% PASS (regex expanded + clean visuals
post fix arc).  Lyric overlay correctly silent on 60s render because
Suno take drifted from prompt and aligned at 01:01+ (script reports
this honestly via 12/13 autofilled lines).

## Refinements shipped after MVP

- **C.1 Phase 2** `77535c3` — `phrase_climax` gate mode.  Shader
  fires only in the center `RATIO × duration` window (with 0.5s
  trapezoid fade-in/out) instead of being uniformly attenuated.
  Reads as "shader fires at the climax" — closer to production
  music-video editing convention.  Activate per render with
  `MUSIC_VIDEO_SHADER_GATE=phrase_climax`.

- **A.3 injection tune** `9057d77` — lang-anchor rate bumped 25% →
  33% (every 4th → every 3rd seg).  Demo-frame sampling showed only
  2/8 clips were KR-anchored on the kpop_ballad demo; bumping to
  every 3rd gives 3 anchored / 2 motif / 3 scenery on a typical
  8-seg render.

- **A.2 follow-on** `a2e2d3f` — the alignment script's autofill
  marker (`# autofilled (low confidence)`) was being appended to
  the LRC line's TEXT field, so the lyric overlay rendered that
  suffix on-screen.  Moved the marker to a separate `# line N`
  comment line above the timed entry (LRC parser already skips
  lines without a `[mm:ss]` prefix).  Plus a tail-line audio-end
  clamp: LEAD_MS could shove the final line's start past the
  audio's last word, producing an inverted enable= interval that
  silently broke the last cue.  Both fixed before the second demo
  render run.

## What didn't land

- **Time-windowed shader gating** (C.1 Phase 2).  Current MVP uses
  uniform attenuation; a real "shader fires only at beat moments"
  implementation needs per-beat enable= expressions in every shader's
  filter graph.  ~3h follow-on; queued in roadmap suggest.

- **Per-genre `shader:` field re-mapping**.  New shaders are
  available but each preset's default `shader:` value is unchanged.
  Operator decides which presets shift toward new shaders (the table
  in the shader-vocabulary doc has recommendations).  ~30 min.

- **A.3 QA gate**.  Concrete render needed to score "% of B-roll
  contradicting lang_anchor".  Queued.

- **Suno take-vs-prompt drift detection**.  Surfaced during A.2
  testing: the uspop1 take drifted from the prompt lyric file ("Be
  with a different end" vs prompt "City lights are calling me
  again"), causing 12/13 lines to autofill on alignment.  Phase A.2
  correctly reported low confidence — a pre-publish QA check that
  fails-loud on low confidence would catch this; not yet wired.

## Render evidence

- Live demo render of `kpop_ballad` track (vocal-kpop-ballad-eodi-
  jjeumiya1.mp3) in `records/missions/2026-05-22/music-video-qb-
  demo-eodi-024553/outputs/`.  Shows the integrated A.1+A.2+A.3+C.1
  pipeline end-to-end:
  - 8 B-roll clips fetched, 2 of them lang-anchor-injected
    (`korean_woman_cafe_window`, `asian_man_walking_seoul_night`)
  - lyric overlay aligned via whisper (kpop-ballad.txt; 11/13 hi-
    conf, 2 autofilled on the silent intro)
  - halation shader applied at ratio=0.35 (vs default 1.0) —
    visible glow but not slathered

## YT stats snapshot (first daily collection)

`records/youtube/stats/2026-05-22.csv` — 25 videos, leaders:

| Rank | Views | Track |
|------|-------|-------|
| 1 | 316 | 천상에서 벌어진 실제 전쟁? (요한계시록 — 5/01 old) |
| 2 | 95  | Rainy Day Bossa Nova (lofi jazz, 5/18) |
| 3 | 87  | Late Night Jazz Loop (5/17) |
| 4 | 73  | 80s arcade synthwave (5/21) |
| 5 | 55  | rainy window lo-fi (5/20) |

12 vocal demos uploaded 5/22 ~23:12 KST have <12h on-platform —
daily snapshot at 09:00 KST will capture their first cohort.

## Decisions queued for operator

- (A.3 QA gate) Should renders fail at ≥30% anchor contradiction,
  or only warn?
- (Preset re-map) Which presets should shift to new shaders?  Table
  in `docs/research/2026-05-22-shader-vocabulary.md` recommends:
  kpop_ballad/rnb → vignette_pulse, synthwave → duotone+beat_burst,
  vaporwave → duotone, cottagecore → light_leak.
- (Stage-1/2/3 shader testing) Render one short per genre with the
  new shaders to visually confirm fit before shipping the re-map?
- (Suno drift) Should the publish flow gate-fail on <0.30 average
  alignment confidence?  Or just warn loud in the upload metadata?

## Don't re-explore

- TikTok automation — operator deferred 2026-05-22 (still parked)
- Playwright web automation — operator rejected (still parked)
- Manual workflows for operator — agent-does-everything per
  [[agent-does-everything]] memory; TikTok web upload is the only
  reserved exception
- Re-doing any of the 5 phases shipped tonight without operator
  feedback — the MVPs are working end-to-end; refinements should
  be in response to specific render output the operator reviews
