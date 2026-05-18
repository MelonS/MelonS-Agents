# Music-video uploads — platform performance trail

Tracks the channel-level performance of music-video uploads starting
with the one that closed the 2026-05-17 active goal.  This is the
**post-goal** data — the goal itself was closed by operator approval
+ publish, but the *meaning* of the data only emerges after the
platform feeds back with real watch-time / impressions.  Subsequent
batch uploads (2026-05-18 onward) appended below the first.

---

## Upload #1 — 03e-velvet1-jazz-combo (2026-05-17)

| Field | Value |
|---|---|
| Title | `Late Night Jazz Loop · Lofi Vinyl Vibes 🎷` (final version may vary) |
| Source mp4 | `outputs/publish/03e-velvet1-jazz-combo.mp4` |
| Duration | 60.0 s |
| Resolution | 1080 × 1920 (9:16) |
| Music | `assets/music/Velvet Turntable1.mp3` (Suno free tier) |
| Visual mood | jazz / vinyl / vintage (lo-fi cafe pool — see `agents/missions/music-video/run.sh`) |
| Shader stack | combo (pond + halation, phrase-aware envelope, 95.8 BPM cadence) |
| Channel | operator's existing channel (4 subscribers, <2k total views pre-upload) |
| Publish time | 2026-05-17 ~23:30 KST |
| Visibility | Public |
| Audience | Not made for kids |

## Pre-upload context (snapshot)

- Channel subscriber count: **4**
- Channel total view count: **< 2,000**
- Monetization status: not monetized (well under 1,000 subs + 4,000
  watch-hour threshold for the standard YouTube Partner Program).
- Channel existed prior to this project — first music-video format
  upload, channel previously had other content.

## Stats to capture (operator action, 24h+ after publish)

Use YouTube Studio (`studio.youtube.com → Analytics`) to fill these:

- [ ] **Impressions** — how many times the thumbnail surfaced in
  someone's feed
- [ ] **Impressions click-through rate (CTR %)** — how many of those
  impressions led to a play
- [ ] **Views** — total play count (auto-play in feed counts as a view
  if watched >2 s on Shorts)
- [ ] **Average view duration** — how long the average viewer stayed
- [ ] **Watch-time** (in seconds, summed across all viewers)
- [ ] **Audience retention curve** — the drop-off chart; the spot
  where the curve goes vertical is the bottleneck for the next render
- [ ] **Likes / shares / comments** — engagement signals
- [ ] **Subscribers gained** — direct attribution to this upload
- [ ] **Geography** — top countries (helps decide hashtag / title
  language for next render)
- [ ] **Content ID claim status** — did Suno's free-tier audio trigger
  a copyright claim?  If yes, was it accepted / disputed?

## What these stats decide

| Signal | What it means | What to change next |
|---|---|---|
| CTR < 1 % | Thumbnail isn't grabbing | Frame extraction at peak motion / amber light (manual upload thumbnail) |
| Avg view duration < 15 s | Hook is dead | Restructure intro: stronger first beat, less envelope ramp-up |
| Retention vertical drop at 30-45 s | Mid-section bored | Shorten climax, faster cut cadence |
| Content ID claim | Suno fingerprinting active | Migrate to YouTube Audio Library / Jamendo for next render |
| 0 subscribers gained | Format not signaling "follow for more" | Title / description rewrite + sticker / branding |
| Strong subscriber gain | Format works | Daily cadence via `scripts/daily-music-video.sh`, queue the next 5 candidates |

## Per-day snapshot table

Append a row per check-in.  First row captured ~24 h after publish.

| Date / time | Hours since publish | Impressions | CTR | Views | Avg duration | Subs gained | Notes |
|---|---|---|---|---|---|---|---|
| _to be filled_ | 24 h | — | — | — | — | — | — |

## Companion data

- Goal closure entry: [`../goal.md`](../goal.md#past-goals) (this
  upload's mp4 was the deliverable that ticked the *Deliverable*
  subgoal of the 2026-05-17 goal, now in Past goals).
- Production mission: `records/missions/2026-05-17/music-video-upload1-203521/`
  (intermediates cleaned in cleanup-records pass; metrics.json kept).
- Engineering case study covering the shader work that produced this
  output: [`../engineering-case-studies.md` §5](../engineering-case-studies.md#5-shader-effects-in-ffmpeg--knowing-where-the-wall-is).

---

## Upload #2 — 06-bossa-rainy (2026-05-18 ~15:00 KST, immediate)

| Field | Value |
|---|---|
| Title | `Rainy Day Bossa Nova · 60s lofi jazz loop #shorts` |
| Source mp4 | `outputs/publish/06-bossa-rainy.mp4` |
| Duration | 60.0 s |
| Resolution | 1080 × 1920 (9:16) |
| Music | `assets/music/Rainy Bossa.mp3` (Suno free tier, 75 BPM bossa nova lofi) |
| Visual mood | rainy neon street, raindrops on glass, jazz cafe, vinyl, wet pavement |
| Shader stack | base mission render (no post-shader applied) |
| Visibility | Public |
| Publish time | 2026-05-18 ~15:00 KST (immediate) |
| Production mission | `records/missions/2026-05-18/music-video-upload3-000333/` |
| Public URL | _to be filled — operator paste after upload_ |

## Upload #3 — 09-vibraphone-tokyo (2026-05-18 19:00 KST, scheduled)

| Field | Value |
|---|---|
| Title | `Tokyo Neon Vibraphone · 60s dreamy city pop loop #shorts` |
| Source mp4 | `outputs/publish/09-vibraphone-tokyo.mp4` |
| Duration | 60.0 s |
| Resolution | 1080 × 1920 (9:16) |
| Music | `assets/music/Tokyo Neon.mp3` (Suno free tier, 65 BPM city-pop vibraphone) |
| Visual mood | Shibuya night, Akihabara neon, ramen shop, vending machine alley, subway, Shinjuku rain |
| Shader stack | base mission render (no post-shader applied) |
| Visibility | Scheduled → Public at 2026-05-18 19:00 KST |
| Publish time | 2026-05-18 19:00 KST (scheduled via YouTube Studio) |
| Production mission | `records/missions/2026-05-18/music-video-upload6-003009/` |
| Public URL | _to be filled — operator paste after the 19:00 auto-publish_ |

## Upload #4 — 08-hiphop-urban (2026-05-19 08:00 KST, scheduled)

| Field | Value |
|---|---|
| Title | `Urban Midnight · Lofi Hip-Hop Noir · 60s loop #shorts` |
| Source mp4 | `outputs/publish/08-hiphop-urban.mp4` |
| Duration | 60.0 s |
| Resolution | 1080 × 1920 (9:16) |
| Music | `assets/music/Urban Midnight.mp3` (Suno free tier, 85 BPM lofi hip-hop jazz) |
| Visual mood | skyline at midnight, lonely silhouette, empty subway, neon alley rain, smoke under neon, Tokyo crossing |
| Shader stack | base mission render (no post-shader applied) |
| Visibility | Scheduled → Public at 2026-05-19 08:00 KST |
| Publish time | 2026-05-19 08:00 KST (scheduled via YouTube Studio) |
| Production mission | `records/missions/2026-05-18/music-video-upload5-002812/` |
| Taste signal | operator's sister (piano major) picked this as the favorite of the batch — first non-operator quality vote on this format |
| Content note | `smoke_neon_glow` B-roll uses a Pexels clip whose page title mentions smoking; operator reviewed and elected to keep as-is — if YT auto-flags for age restriction, that's the platform's call |
| Public URL | _to be filled — operator paste after the 08:00 auto-publish_ |

## Cross-upload comparison (when data lands)

| Upload | Vibe | Hours since pub | Views | CTR | Avg duration | Subs gained |
|---|---|---|---|---|---|---|
| #1 03e-velvet1-jazz-combo | lo-fi jazz vinyl | _to fill_ | — | — | — | — |
| #2 06-bossa-rainy | rainy bossa | _to fill_ | — | — | — | — |
| #3 09-vibraphone-tokyo | tokyo vibraphone | _to fill_ | — | — | — | — |
| #4 08-hiphop-urban | urban hip-hop noir | _to fill_ | — | — | — | — |

Once two or more rows are filled, the comparison decides which mood
gets queued next.  If one upload outperforms the others by 3×+ on
view-through, that's the format taste signal — copy it.
