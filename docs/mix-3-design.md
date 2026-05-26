# Mix #3 design — Hero-loop architecture

> Designed 2026-05-26 ~05:30 KST in response to operator feedback after
> Mix #2 upload review.  Mix #2 used 598 LTX-Video clips × 4.5s each
> (visual diversity strategy) — operator found *visual changes
> distracting* + *AI-generated text artifacts* on signs / shop fronts.
>
> Mix #3 pivots to **single hero clip × infinite loop**: 1-3 hand-curated
> high-quality clips, looped seamlessly for the full audio duration.

## Operator-stated rationale (2026-05-26)

> *"영상이 바뀌는건 오히려 별로인게 오래걸려서 만들었는데 글자
> 나오는게 별로임 글자는 안나와야 할듯 최대한. 글자는 직접쓰던지
> 생성형ai가 만들어내는 글자들 괴상하고 흉측해 보임. 그래서 그냥
> 고퀄영상 하나를 무한반복하면서 사운드만 바뀌는게 더 나은듯."*

Translated lessons:
- AI-generated text (signs, posters, neon shop names) is grotesque (same category as AI hands)
- Frequent cuts in long-form lofi = distracting; lofi audience wants *focus*
- Hours of clip-generation effort → less satisfying than a single high-quality loop

## Architecture

```
INPUT
  audio file (e.g., mix1-full.mp3, 44min)
       │
       ▼
┌─ STEP 1. Hero clip generation (3-5 candidates)  ────────────────┐
│   • LTX-Video, max quality settings:                             │
│     - 1080p (1920x1080) native render                            │
│     - 40-50 sampling steps (vs 30 in Mix #2)                     │
│     - 12-15 sec per clip (vs 4-5s)                               │
│     - Text-avoid prompts (NO signs/shops/letters/logos)          │
│   • Prompt pool (atmospheric, no humans, no text):               │
│     - "wide shot rainy seoul rooftop night, cinematic, no signs" │
│     - "abstract neon void with smoke wisps, slow drift"          │
│     - "stylized rain on glass close-up, bokeh blur background"   │
│     - "endless mist rolling over distant mountain silhouettes"   │
│     - "subtle aurora-like color shift across dark sky"           │
│   • Generation time: ~80s/clip × 5 = ~7 min total                │
└──────────────────────────────────────────────────────────────────┘
       │
       ▼
┌─ STEP 2. Operator picks 1-3 hero clips  ────────────────────────┐
│   • Manual review of candidates                                  │
│   • Pick the most atmospheric, least artifact-prone              │
│   • Future automation possible: frame analysis for text/face     │
│     detection (CLIP / OpenCV text spotting)                      │
└──────────────────────────────────────────────────────────────────┘
       │
       ▼
┌─ STEP 3. Seamless-loop conditioning (per hero)  ────────────────┐
│   • Crossfade-loop: re-encode hero with crossfade (last 1s of    │
│     clip A blends into first 1s of clip A) to mask the seam      │
│   • OR: hero clip's start/end frames already match enough that   │
│     plain stream_loop works (test empirically)                   │
└──────────────────────────────────────────────────────────────────┘
       │
       ▼
┌─ STEP 4. ffmpeg loop + audio mux  ──────────────────────────────┐
│   ffmpeg -stream_loop -1 -i hero.mp4 \                           │
│          -i audio.mp3 \                                          │
│          -map 0:v -map 1:a -c:v copy -c:a aac -b:a 192k \        │
│          -shortest \                                             │
│          -t <audio_duration> \                                   │
│          mix-3.mp4                                               │
│   • -c:v copy: NO re-encode of hero (instant)                    │
│   • For multi-hero (2-3 alternating): use concat filter +        │
│     stream_loop per segment                                      │
│   • Wall-clock: < 1 min for 44min output                         │
└──────────────────────────────────────────────────────────────────┘
       │
       ▼
OUTPUT: outputs/publish/mix-3/yt-mix-3-<theme>-<date>.mp4
```

## Comparison vs Mix #2

| | Mix #2 (598-clip diversity) | Mix #3 (hero-loop) |
|---|---|---|
| Hero clips | 598 × 4.5s | 1-3 × 8-15s |
| Render time (GPU) | ~5 hours | ~10-15 min |
| Wall-clock total | ~5 hours | ~30 min |
| AI text/sign artifact risk | High (598 × 5% chance = ~30 frames with bad text) | Low (1-3 clips, hand-reviewed) |
| Visual cuts | 597 transitions | 0-2 transitions |
| Listener attention demand | High (constantly changing) | Low (stable, focus-friendly) |
| Operator review burden | Impossible (44 min) | 1-3 short clips |
| Resumability | Yes (598 segments) | N/A (so fast) |

## Constraints (inherited from operator memory)

- **Vocal never cut**: n/a — Mix #3 instrumental
- **AI hands avoid**: enforced in prompts (no figures / no people / silhouettes only if necessary)
- **AI text avoid** (NEW 2026-05-26): enforced in prompts — no signs, posters, shops, neon text, logos, billboards, phone screens, book covers
- **B-roll dedup**: each Mix #3 picks new hero clips (registered in `records/youtube/broll-used.txt`)
- **Public repo**: hero clip output mp4 stays gitignored under `outputs/publish/`

## Motion design — continuous frame-wide motion (v2 revision)

**Lesson (2026-05-26 operator-shared reference)**:
[The Japanese Town - 90's Chill Lofi](https://www.youtube.com/watch?v=sF80I-TQiW0)
(12-hour lofi rain playlist, 24M views).  Operator note: *"계속 같은
화면이지만 비가 계속 오는식의 움직임이 크거든"*.

Initial v1 hero prompts used "subtle slow camera drift" which produced
visually static-looking clips.  Successful lofi loop channels use
**continuous frame-wide motion** (heavy rain, snow, steam, flames,
particles) where the *entire frame is alive* — not just camera movement.

v2 prompt design principles:
- **Camera = locked / static** ("Static fixed camera view")
- **Subject motion = continuous + frame-wide** (rain streaming, steam
  rising, flames flickering, snow falling)
- **Particle density = visible** (thousands of raindrops / snowflakes,
  not subtle drizzle)
- **Motion variety per-frame** (different speeds, sizes, directions for
  parallax depth — defeats loop-seam perception)

This pairs naturally with `ffmpeg -stream_loop -1`: heavy motion makes
the seam between loop iterations imperceptible.

## Hero prompt design — text-free atmospheric pool

Each prompt:
- Wide shot or abstract — minimizes object detail that AI can mis-render
- No humans, no animals, no machinery with text labels
- No urban signage close-ups
- Subtle motion (drift / breath / particle flow) — fits looping
- Lofi color palette baked in (warm magenta + cool teal + low saturation)

Candidate prompts (Mix #3 v1 batch):

1. **"rooftop wide rainy night"**: aerial wide shot of Seoul rooftop at deep night, light rain falling, distant city glow with soft bokeh, no visible signs or text, slow subtle camera drift, cinematic lofi color grade, 35mm film grain
2. **"abstract neon void"**: abstract dark space with magenta and cyan smoke wisps drifting slowly, no objects, no letters, just color and motion, dreamy lofi atmosphere, very slow movement
3. **"rain on glass macro"**: extreme close-up of rain droplets running down a dark glass surface, blurred warm orange bokeh in background, no objects visible behind, slow subtle motion, intimate cinematic lofi
4. **"endless mist mountains"**: distant mountain silhouettes layered through soft drifting mist, pale blue and warm orange dawn light blending, no buildings, no text, wide panoramic composition, slow camera pan
5. **"aurora dark sky"**: dark night sky with subtle aurora-like color ribbons drifting (magenta + green + cyan), no stars rendered as text-like dots, no city silhouette, abstract atmospheric motion

## Implementation

`scripts/mix3-hero-loop.py` — orchestrator (this commit):
- Stage 1: generate N candidates via LTX-Video high-quality preset
- Stage 2: write candidate listing for operator review (paths + thumbnails)
- Stage 3: pick hero by argument (`--hero <idx>`) OR auto-pick first
- Stage 4: ffmpeg loop + audio mux

`scripts/ltx-hero.py` — variant of `ltx-img2vid.py` for higher quality:
- Steps: 40 (vs 30)
- Resolution: 1024x576 or 1280x720 (vs 768x432)
- Length: 257 frames (max, ~10.7s @ 24fps)
- May go text2video instead of img2vid for fully synthetic hero

## Open decisions (operator on return)

1. **Pure-loop vs multi-hero**: 1 clip loops for 44 min, OR 3 hero clips each looped 15 min (more variety but still stable)
2. **Hero source**: text2video LTX-Video direct, OR SDXL-Turbo still → LTX img2vid (more control via prompt iteration on still first)
3. **Audio**: same Mix #1 source music, OR new Suno generation
4. **Length**: stay 44min, or shorter (20-30 min easier to consume)
5. **Replace Mix #2** (delete from YT) or **publish Mix #3 separately** for A/B vs Mix #2

Default assumptions (autonomous tonight):
- Multi-hero with 3 candidates, operator picks on return
- LTX text2video direct (skip SDXL still step — fewer artifact opportunities)
- Same Mix #1 audio
- Same 44 min length
- Publish Mix #3 alongside Mix #2 (don't delete Mix #2)

## Success criteria

- [ ] Hero clip(s) at 1080p, 8-15s each, with seamless loop verified
- [ ] Final mp4 duration matches audio (44min ±1s)
- [ ] pix_fmt yuv420p, h264 + aac
- [ ] No visible text in any frame (operator visual check on hero clips, not full mp4)
- [ ] No visible hands / faces (same)
- [ ] Operator verdict: "고퀄 하나 무한반복" 형식 의도 충족
