# First upload — platform performance trail

Tracks the channel-level performance of the first music-video upload
that closed the 2026-05-17 active goal.  This is the **post-goal**
data — the goal itself is closed by operator approval + publish, but
the *meaning* of the data only emerges after the platform feeds back
with real watch-time / impressions.

---

## Upload metadata

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

- Goal closure entry: [`../goal.md`](../goal.md#active-goal) (this
  upload's mp4 is the deliverable that ticked the
  *Deliverable* subgoal).
- Production mission: `records/missions/2026-05-17/music-video-upload1-203521/`
  (intermediates cleaned in cleanup-records pass; metrics.json kept).
- Engineering case study covering the shader work that produced this
  output: [`../engineering-case-studies.md` §5](../engineering-case-studies.md#5-shader-effects-in-ffmpeg--knowing-where-the-wall-is).
