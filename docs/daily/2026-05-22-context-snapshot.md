# Context Snapshot — 2026-05-22 ~01:10 KST

Tight handoff doc.  Written when session context was near its limit.
Picks up where the 2026-05-21 morning brief left off.

## What's done (no re-explanation needed)

- **YT: 12 vocal demos uploaded + scheduled**, 5/22 09:00 → 5/28 21:00.
  Video IDs and full schedule recorded in chat + reproducible from
  `outputs/demos/2026-05-21-genre-shader-experiments/README.md`.
- **Genre system shipped**: 19 presets (`skills/music-video/data/
  genre-presets.yaml`), 15 effects (4 classic + 6 genre-coded + 5
  beat-synced), 13 scripts under `scripts/music-video-*.sh`.
- **CPU throttle live** (`FFMPEG_THROTTLE=1` in `.env`, see
  `scripts/ffmpeg-throttled.sh`).  No thermal events since.
- **TikTok automation deferred** — see
  `docs/pilots/decision-log.md` (last section).  Cheat sheet for
  manual web upload at `~/Desktop/tiktok-upload-cheatsheet.md`
  (operator-local, not in repo).
- **Lyrics overlay**: works for both Korean (AppleSDGothicNeo) and
  English (SF Mono Regular); apostrophe-escape bug fixed
  (`d9c7c06`).  Used in 10 of the 12 YT uploads.

## In flight / open items

1. **TikTok manual upload (operator side)** — 11 videos (dreampop
   removed as broken; 11 left).  Cheat sheet ready.  Operator
   doing this manually via tiktok.com/upload.

2. **YT broken dreampop (`KirKdDUWOpc`)** still scheduled
   for 5/24 21:00.  Decision pending: ignore (lets bad render
   publish), push publishAt out via API, or replace with fresh
   render.  Operator hasn't decided.

3. **Stats collection automation** — proposed but not built.  Two
   tiers:
   - Basic (Data API `videos.list`, no new OAuth scope): viewCount /
     likeCount / commentCount per video.  ~30 min implementation.
   - Detailed (Analytics API, new scope + re-OAuth): retention,
     traffic sources, demographics.  Defer until basic shows worth.

4. **Vocal-vs-instrumental performance verdict** — needs
   24-48h data.  Early TikTok signal (1 video each) suggests
   **lo-fi instrumental (Rain) outperforms early** (754 vs 30
   views), but vocals just started publishing.  Re-evaluate
   2026-05-23 / 2026-05-24.

## Persisted documents (read these to restore context)

- `docs/daily/2026-05-21-morning-brief.md` — overnight work summary
- `docs/pilots/decision-log.md` — TikTok decision (latest section)
- `docs/research/2026-05-21-music-shorts-formats-landscape.md` —
  3.4K-word format research
- `docs/research/2026-05-21-shader-song-mismatch-diagnosis.md` —
  per-short diagnosis of original 5/20 batch
- `assets/lyrics/2026-05-21-suno-vocal-prompts.md` — Suno prompts
  + full lyrics for F-J tracks (12 demos these came from)
- `outputs/demos/2026-05-21-genre-shader-experiments/README.md` —
  29-file demo index

## Open decisions for operator (when they get to it)

- (A) YT dreampop: keep / push out / replace?
- (B) TikTok upload: continue manual or pause?
- (C) Stats automation: build Phase 1 now, or wait?
- (D) Vocal vs instrumental channel direction: hold judgement until
  ~5/24 data.

## Don't re-explore

- TikTok Content Posting API — deferred 2026-05-22 (decision-log).
  Re-eval conditions in that doc.
- Playwright web automation for any platform — operator rejected
  ("Playwright 안 씀.. 믿지 못함").
- Manual / clicking workflows for operator — operator does not
  click ([[agent-does-everything]]).  Manual is reserved for
  TikTok web upload only (where API friction makes manual
  competitive).
