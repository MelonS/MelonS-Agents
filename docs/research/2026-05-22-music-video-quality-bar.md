# Music-video quality bar — 2026-05-22

Operator-stated quality directives after reviewing the overnight batch
of 12 vocal demos (uploaded 5/22 23:12 KST).  This document
decomposes each into actionable implementation work, in priority
order based on impact vs. cost.

> **Frame**: these are not bug reports.  Each statement is a
> quality-bar definition for what the channel's content should and
> shouldn't be.  The 12 demos uploaded yesterday violate some of
> them — they are not regressions, they are the surface area where
> the bar is now visible.

## The six directives

| # | Directive | Surface area |
|---|-----------|--------------|
| 1 | No reusing B-roll across shorts | B-roll picker + history registry |
| 2 | Shader restraint — situational, not blanket | Shader applier gate |
| 3 | Lyric sync — slight-early OK, too-early/too-late bad | Lyric overlay timing |
| 4 | Shader catalog research / expansion | Research doc + preset expansion |
| 5 | Korean lyric → Korean person on screen | B-roll keyword + AI-still prompt |
| 6 | English lyric → global subject + matching on-screen language | B-roll keyword + AI-still prompt |

## Prioritization

Sequencing rule: ship cheap concrete wins first, then research-heavy
work, then deep mechanics.

### Phase A — concrete, cheap wins (~half-day total)

- **A.1** B-roll dedup registry (#1).  ~1h.
  - `records/youtube/broll-history.json` (gitignored) stores
    `{video_id, broll_clips: [{id, src, used_in_short}]}` for every
    published short.
  - `scripts/pexels-fetch.sh` extended with `--exclude-history`:
    reads the registry and adds `excluded_ids` to the picker logic.
  - Pollinations.ai AI-still uses prompt-hash registry analogously.
  - Backfill: ingest existing 12+2+more shorts to seed the history.

- **A.2** Lyric vocal-onset alignment (#3).  ~2-3h.
  - Use whisper.cpp (already available) to transcribe the vocal stem
    or full track with `--output-srt` and word timestamps.
  - Map lyric file lines to whisper-detected word ranges via
    fuzzy match (existing `scripts/correct-captions.py` does a
    similar thing for proper-noun correction).
  - Offset config: `LYRIC_LEAD_MS=200` default — 200ms early is the
    sweet spot most operators find.  Operator can tune.
  - Sanity check: bail out if drift exceeds ~600ms (likely a wrong
    lyric file or wrong stem); render without lyrics + log.

- **A.3** Ethnicity-language match (#5 + #6).  ~2h.
  - `skills/music-video/data/genre-presets.yaml` extended: each
    preset gets a `lang_anchor` (ko / en) and `keyword_pool` is split
    into `_ko` and `_en` variants where current pool is ambiguous.
  - `scripts/pexels-fetch.sh` query injects `+"korean"` for `ko`
    anchor; `+"new york" | +"london" | +"angeles"` for `en` anchor.
  - Pollinations.ai prompt template includes the anchor phrase.
  - QA gate: render fails if ≥30% of B-roll clips have descriptive
    keywords contradicting the anchor (e.g., `tokyo` clip in `ko`
    anchor — Japanese context, not Korean).

### Phase B — research investment (~1 session)

- **B.1** Shader vocabulary survey (#4).  ~3-4h research + write-up.
  - Catalog effects used in actual production music videos by mood
    family.  Sources: ShaderToy by category, AE preset libraries,
    music-video editor breakdowns (PremiumBeat, NoFilmSchool),
    Reddit r/VideoEditing post-process discussions.
  - Map per-mood-family: chill / mellow / energetic / dark /
    nostalgic / aggressive / dreamy — three shaders per family
    minimum, with situational notes ("scanline = retro, suits
    synthwave drop / suits arcade nostalgia, AVOID on warm acoustic
    ballad").
  - Output: `docs/research/shader-vocabulary-<date>.md` + extended
    `scripts/music-video-shaders.sh` (current 15 → target ~30).

### Phase C — design + deep mechanics (~1-2 sessions)

- **C.1** Shader restraint gating (#2).  ~3-4h.
  - Currently shaders are applied across the full duration via the
    preset `shader:` field.  Replace with `shader_events:` list:
    each entry has `at: (beat | onset | bar | timestamp)`,
    `duration_ms`, `intensity`.
  - Default behavior: small windows of shader activity (~500ms-2s)
    at musical events, vast majority of timeline left shader-free.
  - Per-genre defaults: dance/hyperpop more frequent triggers,
    ballad/ambient very sparse.
  - The current "full-cover" mode is preserved as an explicit
    `shader_always_on: true` opt-in for special cases.

## Quality-bar QA spec

After Phases A-C land, a music-video render should pass these gates
before being eligible for publish:

```
- [ ] No B-roll clip ID present in the published-history registry.
- [ ] If `lyric_lang: ko` → ≥1 B-roll clip matches a Korean anchor.
- [ ] If `lyric_lang: en` → no B-roll clip matches a CJK signage anchor.
- [ ] Lyric overlay cues within ±200ms of detected vocal onsets (95th pct).
- [ ] Shader-active duration ≤ 25% of total runtime (unless `shader_always_on`).
- [ ] Pexels attribution preserved for every clip used.
```

The first five are quality gates; the last is the existing license
check.

## Out of scope

- Suno-side generation choices (genre prompts, vocal direction) —
  separately captured under [[music-video-mode-validated]].
- Paid AI video generation — money firewall.
- TikTok-specific format adaptations — handled in
  `docs/pilots/decision-log.md`.

## Next action

Phase A.1 (B-roll dedup) starts now in this same session.  Phases
A.2, A.3, B.1, C.1 enter `docs/roadmap.md` "Next" queue with this
doc as their reference.
