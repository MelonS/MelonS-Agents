# Demo mode — produce a music-video with zero accounts

The headline `music-video` mission has two paths:

1. **Demo path** (this document) — zero signup, ~2 minutes from
   clone to playable `short.mp4`.  Uses bundled CC-BY clips +
   tracks.  Fine for evaluation, comparison shots, and seeing what
   the pipeline produces before committing to the full path.
2. **Full path** — operator-supplied music file (Suno / YouTube
   Audio Library / your own) + Pexels API key for mood-matched
   B-roll.  Higher creative ceiling; documented in the main
   [`README.md`](../../README.md#quick-start).

If you cloned the repo, ran `bootstrap.sh`, and saw a message
pointing at `MUSIC_VIDEO_DEMO_MODE=1` — this is what that command
does.

## What demo mode produces

A real `short.mp4` from the same `agents/missions/music-video/run.sh`
that powers the full path.  Same beat detection (`aubiotrack`), same
phrase-aligned cuts, same v6 vintage visual treatment (film grain +
vignette + glitch-onset zoom pulse).  The only differences from a
full-path run are the source assets:

| Asset | Demo path | Full path |
|---|---|---|
| Music track | Auto-picked Kevin MacLeod CC-BY 4.0 (default: *Carefree*) | `assets/music/<your_track>.mp3` |
| B-roll clips | 3 Blender Foundation trailers cycled across keywords | Per-keyword Pexels API lookup |
| `outputs/SOURCES.txt` | CC-BY attribution required | Operator-set |

Output specs are identical in both modes: 1080×1920 (9:16),
H.264 + AAC, 60 s target, ~80 MB.

## Running demo mode

```bash
MUSIC_VIDEO_DEMO_MODE=1 ./agents/missions/music-video/run.sh demo
```

That's the whole command.  No `.env` edit required.  No
`PEXELS_API_KEY` needed.  No music file argument.

First-run timing (cold cache, including downloads):
- B-roll cache: ~30 s for 3 Blender clips totalling ~76 MB.
- Music cache: ~10 s for 5 Incompetech tracks totalling ~35 MB.
- Mission render: ~1 min 40 s (beat extraction + segment encode +
  v6 filter pass).

Subsequent runs hit the cache: ~1 min 40 s total.

Output lands at:

```
records/missions/<YYYY-MM-DD>/music-video-demo-<HHMMSS>/outputs/
├── short.mp4              ← the playable 9:16 video
├── preview-frame.jpg      ← mid-mission still frame
└── SOURCES.txt            ← CC-BY attribution rollup
```

## Attribution responsibility

Both demo source families carry attribution requirements:

- **Blender Foundation clips** — CC-BY-3.0.  Credit Blender
  Foundation + the relevant open-movie project (Durian, Peach,
  etc.).  License text:
  https://creativecommons.org/licenses/by/3.0/
- **Kevin MacLeod / Incompetech tracks** — CC-BY-4.0.  Credit
  "Kevin MacLeod" + the track title.  License text:
  https://creativecommons.org/licenses/by/4.0/

The mission writes `outputs/SOURCES.txt` automatically with a
deduplicated credit line per source.  If you publish a demo-path
short externally, copy that file's contents into your YouTube /
TikTok / Reels description.

## Source customization

Want different demo clips or tracks?  Edit the curated arrays in:

- [`scripts/fetch-demo-broll.sh`](../../scripts/fetch-demo-broll.sh)
  — `CLIPS=(…)` block.  New domain entries must also be added to
  [`config/copyright-allowlist.yaml`](../../config/copyright-allowlist.yaml)
  or the mission's publish gate will refuse them.
- [`scripts/fetch-demo-music.sh`](../../scripts/fetch-demo-music.sh)
  — `TRACKS=(…)` block.

Cache lives at `$FIXTURE_DIR/demo-broll/` and
`$FIXTURE_DIR/demo-music/` (default `/tmp/smoke/demo-{broll,music}/`).
Delete those directories to force a re-fetch.

## When to graduate to the full path

The demo path's B-roll comes from three Blender trailers cycled
across however many mood keywords the segment plan produces.  The
visual variety is limited compared to per-keyword Pexels fetches.
You'll outgrow it the moment you care about a specific aesthetic.

The track selection covers five moods (upbeat / cinematic /
energetic / mellow / chill) but they're all the same composer.
If your output needs a specific track for a specific edit, the
demo cache won't help.

The graduation path:
1. Sign up at https://www.pexels.com/api/ (free, 200 req/hour).
2. Add `PEXELS_API_KEY=<your_key>` to `.env`.
3. Drop a music file (mp3 / wav / m4a / flac / ogg) into
   `assets/music/` — gitignored, never committed.
4. Run without `MUSIC_VIDEO_DEMO_MODE=1`:
   ```bash
   ./agents/missions/music-video/run.sh mytitle "assets/music/your_track.mp3"
   ```

## Reproducibility test

`scripts/test-demo-mode.sh` exercises the demo path end-to-end
against a fresh clone — useful as a local CI step before merging
changes that touch the demo cache or the music-video mission.

```bash
# default: clones the public repo over HTTPS
./scripts/test-demo-mode.sh

# test against a local working copy (e.g. a feat branch)
FRESH_CLONE_REMOTE="$(pwd)" ./scripts/test-demo-mode.sh
```

PASS criteria:
- `short.mp4` ≥ 1 MB and duration ≥ 50 s
- `SOURCES.txt` exists with ≥ 2 credit lines including `CC-BY`

Results append to
[`docs/onboarding/demo-mode-log.txt`](demo-mode-log.txt) — same
PASS/FAIL log pattern as
[`fresh-clone-log.txt`](fresh-clone-log.txt) does for the full
clone-and-go path.
