# Morning Brief — 2026-05-21

> Operator-facing summary of overnight autonomous music-video work.
> Major strategic shift mid-session: channel direction pivoted to
> **vocal-led content** based on operator's reaction to test tracks.

## Strategic Shift (~04:10 KST)

Operator reaction to vocal Suno tracks:
> "가사 있는 곡이 너무 신세계임. 가사 없는 건 그냥 좋네 그정도 였고...
> 데뷔한 신인 같음... 천재 작곡가 느낌... 한국 가사를 불러주는 게
> 느낌이 확실하게 옴. K-팝 / 미국팝송 / R&B / 빌보드 상위곡 수준으로
> 여러 장르로 해보는 게."

**Implication**: Instrumental tracks → secondary BGM tier.
Vocal tracks (especially Korean) → channel's actual headline content.

## What landed overnight (29 commits, all on main, pushed)

### Phase 1 — original task (shader-song mismatch fix)
14 genres + 6 new shaders + 4 entry points + auto-detect + bulk
regen (5/5 v2 ready) + Pollinations.ai (free AI image gen) + smoke
test 16/16.  See commits `93cc5e8` through `8cc49cb`.

### Phase 2 — vocal pivot (after operator's epiphany)
- `82cb15d` — 4 new vocal-centric presets:
  - **kpop_ballad** — 90 BPM Korean emotional ballad
  - **kpop_dance** — 120 BPM K-pop EDM
  - **rnb** — 70 BPM contemporary R&B / neo-soul
  - **uspop** — 100 BPM US synth-pop / Billboard top-40
- citypop preset (added earlier) now covers both Korean + English citypop
- `--full-length` flag — vocal tracks render at music's actual duration
  (162s, 226s, etc) instead of 60s default
- `scripts/music-video-lyrics.sh` — designer-style on-screen lyrics
  (4-position rotation, genre-themed colors, CJK font auto-detection,
  triangle fade in/out per line — NOT documentary subtitle style)

### Phase 3 — beat-synced "popping" shaders (operator request)
- `a348961` — 5 new beat-synced shaders: `beat_burst`, `strobe`,
  `shake`, `color_burst`, `light_rays`
- `eb7e64c` — wired into fast-genre presets: synthwave→beat_burst,
  phonk→shake, techno→strobe, house→color_burst, hyperpop→strobe

### Phase 4 — CPU throttle (operator urgent request)
- `f190929` — `scripts/ffmpeg-throttled.sh` + env.sh integration.
  All ffmpeg renders auto-route via `cpulimit -l 640 -i -- nice -n 19
  ffmpeg -threads 6` when `FFMPEG_THROTTLE=1` in `.env` (now default).
  Prevents thermal throttling on long batches.

## 19 total presets

| Family | Genres |
|--------|--------|
| Slow / sparse | ambient, drone, shoegaze, classical, dreamcore |
| Lo-fi / chill | lofi_hiphop, jazz |
| Vocal-centric | citypop, **kpop_ballad**, **rnb**, **uspop** |
| Hi-fi clean | house, techno, **kpop_dance** |
| Synthwave family | synthwave, vaporwave |
| Fast / chaotic | phonk, hyperpop |
| Folk / cottage | cottagecore |

13 vocal tracks (5 prompts × 2 variants + earlier citypop + dreampop)
all auto-detect to correct presets (13/13 ✓).

## Demo files (`outputs/demos/2026-05-21-genre-shader-experiments/`)

Already staged:
| # | File | Demonstrates |
|---|------|--------------|
| 00 | arcade baseline | current v6 default (the "띠용" problem) |
| 01-06 | arcade shader variants | 6 new shaders applied to same source |
| 07 | linen ambient preset | stillzoom + halation |
| 08-09 | canvas 8s loops | Spotify Canvas spec |
| 10 | rain typography | kinetic mood phrases (instrumental) |
| 11 | arcade full pipeline | genre wrapper end-to-end proof |
| 12 | citypop1 60s lyrics | 8 Korean lyric lines on 60s render |
| 13 | arcade 5 beat shaders | beat_burst/strobe/shake/color_burst/light_rays |
| 14-17 | 4 new tracks + new presets | synthwave-drive / phonk-drift / house-disco / citypop2 |
| 18 | citypop1 FULL 162s lyrics | full-length Korean vocal w/ designed typography |
| 19-29 | **all 11 vocal tracks DONE (07:04 KST)** | full-length + designer lyrics overlay per genre |

Demo 19-29 details:
- 19, 20: citypop English (Midnight Rambler) 226s, 86MB each
- 21: dreampop (Blue Hours) 181s base only, 56MB
- 22, 23: kpop_ballad (어디쯤이야) 205s/217s, 47/64MB w/ KR lyrics
- 24, 25: kpop_dance (사이렌) 177s/185s, 37/41MB w/ KR lyrics
- 26, 27: rnb (Late Light) 224s/220s, 80MB each w/ EN lyrics
- 28, 29: uspop (Tomorrow Is A Question) 206s/213s, 55/61MB w/ EN lyrics

Each Suno-generated track has 2 variants (operator couldn't pick).
Watch each pair side-by-side and choose the better take per genre.

## Distribution strategy (vocal tracks)

For 1-3 min vocal tracks (162-226s typical):
- **YouTube**: works as Short (3min cap) OR regular video
- **TikTok**: Short (10min cap, no issue)
- **Reels**: 90s cap — truncate or skip

Single 9:16 vertical mp4 → upload to YT (long-form or Short) +
TikTok (Short).

## Decisions when you wake up

1. **v2 batch re-upload?** 5 inst regens at
   `outputs/publish/2026-05-21-regen-v2/` + scheduled metadata at
   `outputs/publish/upload-meta-v2/`.  One command: `bash scripts/
   yt-batch-upload.sh outputs/publish/upload-meta-v2/`.

2. **Vocal channel direction confirmed?** Strategic pivot from
   instrumental shorts to vocal-led content (60s → 1-3min full-length,
   on-screen lyrics, K-pop / US pop / R&B / Billboard genre mix).
   Channel rebrand if so.

3. **Pollinations.ai (free AI) usage?** `--ai-still` flag exists for
   stillzoom genres.  Want to default to AI stills or stay Pexels?

4. **More vocal genres?** Currently 4 vocal-centric presets.  Could
   add: K-indie, K-OST, US R&B trap, latin pop, etc.

5. **Lyrics auto-extraction?** Currently lyrics are manual.  Could
   integrate Whisper for word-level alignment (free, local, slow).

## Disk usage

After overnight cleanup (~3GB freed):
- 16 GiB free / 228 GiB
- records/missions/2026-05-21 trimmed to 557 MB (was 2.9 GB)
- assets/music: 135 MB (added 11 vocal tracks, ~80 MB)
- outputs/demos: ~700 MB (will grow ~700 MB more with overnight batch)

If disk gets tight, oldest records/ folders (5-14, 5-15, 5-16, 5-17)
can be cleaned (~2.4 GB total).
