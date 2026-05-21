# Quality-bar demo verification — 2026-05-22 03:00 KST

End-to-end render of the new pipeline against vocal-kpop-ballad-eodi-
jjeumiya1.mp3 (Korean ballad, 60s slice).  Each of the five 2026-05-22
quality-bar phases (A.1, A.2, A.3, C.1, B.1) exercised in a single
chain.

## Mission

```
records/missions/2026-05-22/music-video-qb-demo-eodi-025509/
├── outputs/
│   ├── short.mp4                       — base render (27 MB)
│   ├── short-halation.mp4              — halation @ ratio=0.35 (22 MB)
│   ├── short-halation-lyrics.mp4       — final w/ aligned lyrics (22 MB)
│   ├── preview-frame.jpg
│   └── SOURCES.txt
└── resources/
    └── clips/
        ├── raw-asian_man_walking_seoul_night.mp4     ← A.3 lang_anchor injection
        ├── raw-cafe_alone_evening.mp4                ← genre pool
        ├── raw-city_skyline_rain.mp4                 ← genre pool
        ├── raw-empty_street_lamp_post.mp4            ← genre pool
        ├── raw-korean_woman_cafe_window.mp4          ← A.3 lang_anchor injection
        ├── raw-rainy_seoul_window_night.mp4          ← genre pool
        ├── raw-subway_window_blurred.mp4             ← genre pool
        └── raw-winter_coat_alone_street.mp4          ← genre pool
```

Mp4 records gitignored; runtime artifacts only.

## Per-phase verification

| Phase | Mechanism | Verified at |
|-------|-----------|-------------|
| A.1 B-roll dedup | `records/youtube/broll-used.txt` registry consulted before fetch + appended after download | Registry grew 196 → 212 (+16 unique ids).  No clip overlap with prior session's 196 ids. |
| A.2 Lyric onset sync | LRC derived via `music-video-lyric-align.sh` (whisper word-level + difflib SequenceMatcher).  ±200 ms lead. | 11/13 lines hi-confidence; 2 autofilled (instrumental intro before first vocal).  Marker leak bug fix verified at frame 25s — lyric "변한 건 너의 자리뿐" renders cleanly, no `# autofilled` suffix. |
| A.3 Lang anchor | Vocal genres inject person-anchored KR keywords at every 4th segment | 8 fetched clips, 2 lang-anchored (`korean_woman_cafe_window`, `asian_man_walking_seoul_night`) — 25% injection rate matches design. |
| C.1 Shader ratio | `shader_active_ratio: 0.35` for kpop_ballad; blend back to original | Halation visible but restrained at frame 45s (red+pink glow instead of full saturation halo).  Mathematically: shader output blended at 35%, original at 65%. |
| B.1 Catalog | 23 shaders total (15 + 8 new); kpop_ballad still uses `halation` per existing preset | Preset re-mapping not yet shifted; operator decides per the table in shader-vocabulary doc. |

## Frame observations

- **25s**: Western-subject B-roll (`winter_coat_alone_street`).  Lyric overlay clean.  Halation restrained.
- **45s**: ambiguous-ethnicity subject under red neon glow.  Top-left lyric position (matches 4-position rotation: top-center / bottom-center / left-aligned / right-aligned).

## Open question for operator

The injection rate of person-anchored clips (25%, i.e., 2 of 8) reads
as too low at single-frame sample.  For vocal genres where the lyric
language is the primary cultural signal, raising the rate to 33–40%
(every 3rd segment) may better satisfy the directive.  Trade-off:
fewer scenery moments, more close-up portraits.  Operator's call.

## Verification frames

Extracted at 5/15/25/35/45/55 s, stored at `/tmp/qb-demo-frames/`
(gitignored).  Local-only; not committed.

## Compare against the OLD pipeline render

The original 5/21 render of the same track is at
`outputs/demos/2026-05-21-genre-shader-experiments/22-kpop-ballad-eodi1-FULL-lyrics.mp4`
(FULL 205s, not 60s like the new demo).  Direct frame-for-frame
compare imperfect due to duration mismatch, but the operator can
pause at the same musical moment in both to feel:

- Less B-roll uniqueness in OLD (no dedup) vs deliberate variety in NEW
- Heavier halation in OLD (default ratio=1.0) vs subdued in NEW (0.35)
- Auto-spaced lyric timing in OLD vs vocal-onset aligned in NEW
- No Korean-anchor injection in OLD vs 25% in NEW
