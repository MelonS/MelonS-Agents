# Pro music-video practices for vocal-track shorts — 2026-05-22

Companion research for the music-video pipeline (skills/music-video).
Purpose: identify what professional music-video editors, lyric-video
designers, and short-form directors do that the current automated
pipeline does not — then map each finding to a concrete ffmpeg-feasible
change.

Scope: vocal tracks (not instrumental), 9:16 vertical (1080×1920),
60s short-form (TikTok / YT Shorts / Reels).  Companion to
`docs/research/2026-05-22-shader-vocabulary.md` (which catalogs the
effect layer in isolation) and `docs/research/2026-05-21-music-shorts-formats-landscape.md`
(which surveys the format landscape).

## 1. Executive summary

- **Cut-density is genre-locked, not aesthetic preference.**  Modern
  short-form completion-rate data points at 0.5–1.0s cuts for
  pop/dance and 4–8s holds for ballad/ambient; the pipeline's current
  per-clip duration field needs a `cut_density` axis (dense / moderate
  / sparse) wired to genre presets.
- **The Hollywood "teal-and-orange" grade does not transfer to KR
  vocal content.**  K-drama / K-pop ballad convention is warm pastels
  with high white balance and gentle highlights — the opposite signal.
  A `grade_profile` field (kr_warm_pastel / hollywood_teal_orange /
  synthwave_neon / lofi_warm_grain / etc.) belongs in genre-presets.
- **Lyric placement must respect the 1080×1420 cross-platform safe
  zone.**  Current overlay places text in the bottom third — that is
  the zone reserved for platform-generated captions, comments stack,
  and the "More" expander.  Move to upper-middle and the same lyric
  reads on TikTok, Reels, and Shorts without UI eating it.
- **Vocal-onset cuts beat beat-cuts for vocal tracks.**  Pro
  editors of vocal content cut on the vocal transient (consonant
  attack) when the vocal is the carrier; beat-cuts dominate
  instrumental and dance.  The pipeline already has whisper alignment;
  it can expose a `cut_on: vocal|beat|phrase` mode per preset.
- **The 23-shader catalog maps cleanly to industry vocabulary.**
  Halation, bloom, chromatic aberration, grain, scanline, light leak
  are all DaVinci/AE-native effect names.  No rename required; the
  pipeline can ship a `industry_name` doc-comment per shader for
  operator discoverability without changing the function names.

## 2. Color grading conventions per genre

### Findings

Professional colorists treat color grade as a per-genre signature,
not a transferable look.  The dominant trends across the source
material:

- **K-pop ballad / R&B (vocal-emotional family)**: warm pastels, high
  white balance, soft whites + pale grey + cool blue background palette
  with skin tones intentionally pushed warm.  Specific palettes named in
  the kultscene / theseoulstory analyses include "millennial pink + rose
  gold" with misting filters and beauty-lighting softboxes.  K-drama
  convention (Dramabeans) follows the same direction — earthy warm tones
  with restrained neutrals.
- **Synthwave / vaporwave (nostalgic family)**: complementary neon —
  pink + cyan + magenta + deep purple stacked over a dark base, with
  saturated highlights.  Vaporwave specifically uses the pastel base
  variant (pink + teal) rather than synthwave's neon-on-black.
- **City pop (nostalgic family)**: high saturation, 80s Japanese neon
  palette, often with golden-hour warm-cast on the human elements.
- **Lo-fi hip-hop (mellow family)**: muted analogous cool palette
  (blue / pink / purple) with warm desk-lamp accents, intentional
  desaturation, fine film grain.  The "easy-on-the-eyes pastels +
  muted tones" framing recurs across every lo-fi source.
- **R&B (vocal-emotional)**: golden hour, low-key warm, intimate
  framing; long shadows; warm-side colour temp on skin.
- **Hollywood "teal-and-orange"**: orange in midtones/highlights
  (skin), teal pushed into shadows.  Blockbuster standard, but
  cross-source consensus is that it does **not** read as appropriate
  for KR / Asian vocal content — K-drama convention is the inverse
  (warm-pastel + gentle highlights, not high-contrast complementary).

LUT directions exist in commercial packs (Kodachrome, Fuji Pro 160S,
K-Tone) but specific HSL coefficients are not published — the LUT
files themselves are paid product.  The directional rules are
extractable; the exact numbers are not.

### Sources

- Mixx Studios K-pop LUT store (custom LUTs released per new K-pop
  song): `https://www.mixx-studios.com/store`
- Presetpro 2026 free LUT roundup (Fuji Pro 160S, K-Tone descriptions):
  `https://www.presetpro.com/best-free-luts-color-grading-2026/`
- Frameset on music-video grading mood-setting:
  `https://site.frameset.app/post/color-grading-in-music-videos-setting-the-tone-and-mood`
- Van Paugam on city-pop aesthetic conventions:
  `https://vanpaugam.com/blog/2020/10/20/city-pop-aesthetics`
- Dramabeans on K-drama color palette:
  `https://dramabeans.com/2019/11/k-drama-color-the-power-of-the-palette/`
- Cined on common film color schemes:
  `https://www.cined.com/film-color-schemes-cinematic-color-design/`
- Aesthetics Wiki lo-fi entry:
  `https://aesthetics.fandom.com/wiki/Lo-fi_Art`
- Adobe Express on vaporwave conventions:
  `https://www.adobe.com/uk/express/learn/blog/what-is-vaporwave`
- Premiumbeat on synthwave/vaporwave visual styles:
  `https://www.premiumbeat.com/blog/synthwave-vaporwave-visual-styles/`
- The Seoul Story on K-pop MV visuals:
  `https://theseoulstory.com/feature-k-pop-music-video-production-the-art-of-visuals/`

### ffmpeg feasibility

All grade directions are achievable in ffmpeg with `eq`, `curves`,
`colorbalance`, `colorchannelmixer`, and `hue` filters.  The pipeline
already has shader-level color manipulation (`halation`, `duotone`);
the missing piece is a base-grade applied to the source B-roll
*before* the shader stage.

Example ffmpeg directions per profile:

```
kr_warm_pastel:
  eq=saturation=0.85:contrast=0.95:gamma=1.05,
  colorbalance=rs=0.05:gs=0.0:bs=-0.05:rm=0.05:gm=0.02:bm=-0.05,
  curves=preset=lighter

hollywood_teal_orange:
  colorbalance=rs=0.1:gs=0.0:bs=-0.1:rm=0.05:gm=0.0:bm=0.0:
              rh=0.05:gh=0.0:bh=-0.05

synthwave_neon:
  eq=saturation=1.4:contrast=1.15,
  colorchannelmixer=rr=1.1:gb=0.15:bb=1.1,
  curves=preset=vintage

lofi_warm_grain:
  eq=saturation=0.75:contrast=0.92:brightness=-0.02,
  colorbalance=rm=0.04:bm=-0.04,
  noise=alls=6:allf=t+u

city_pop_neon:
  eq=saturation=1.3:contrast=1.1,
  colorbalance=rs=-0.05:bs=0.1:rm=0.1:bm=0.05
```

### Proposed pipeline change

Add `grade_profile:` to `skills/music-video/data/genre-presets.yaml`
with the seven values above plus `neutral` (no grade).  Implement
`scripts/music-video-grade.sh` that emits the filter graph fragment
to splice into the main render pipeline before the shader stage.
Default value `neutral` keeps backward compatibility.

## 3. Cut rhythm conventions per genre

### Findings

Cut frequency is the single most genre-locked variable identified
in this research:

- **Short-form general**: SocialRails / Socialinsider 2025 benchmarks
  put 0.5–1.0s cuts as the rule for completion-rate optimization on
  TikTok / Reels; varying clip length within that range sustains
  attention more than uniform fast cutting.
- **Dance / EDM / hyperpop**: cut on every kick or every snare;
  pro convention is the "four-beat compact" or "eight-beat normal"
  rhythm map.  Kweenmedia and FilmDaft describe the rule as "cut
  where the kick drum hits, transitions on the snare".
- **Hip-hop / R&B mid-tempo**: 1–2s shots, cut on bar lines, occasional
  vocal-onset cuts when a lyrical phrase lands hard.
- **Ballad / ambient / classical**: 4–8s shots, sometimes single takes
  for full verses.  OK Go's "The One Moment" is the extreme case —
  4.2s of real-time stretched to a full song.  For vertical short-form,
  the practical floor is 3–4s holds on ballads.
- **Spotify Canvas (8s loop)**: explicit rule from Spotify is *avoid
  rapid cuts entirely* — subtle continuous motion only.  This is a
  separate output target than the 60s short; both deserve dedicated
  presets.

The historical trend (ResearchGate / Vashi Visuals): feature-film ASL
fell from 8–11s (1930s) to 3–4s (post-MTV 1980s); music videos drove
that compression and still set the pace.

### Sources

- Shortimize 2025 video-length sweet spots:
  `https://www.shortimize.com/blog/video-length-sweet-spots-tiktok-reels-shorts`
- Trivision Studios on best TikTok length 2026:
  `https://trivisionstudios.com/best-length-for-tiktok-video-in-2026/`
- Kweenmedia "cut to the beat" guide:
  `https://kweenmedia.in/cut-to-the-beat-top-5-secrets-for-editing-music-videos-like-a-pro/`
- FilmDaft on beat-cut editing:
  `https://filmdaft.com/how-to-edit-video-clips-to-the-beat-of-music-the-easy-way/`
- ProVideoCoalition (Steve Hullfish) on pacing and rhythm:
  `https://www.provideocoalition.com/pacing-and-rhythm-in-editing/`
- OK Go "The One Moment" production analysis:
  `https://nofilmschool.com/2016/11/ok-go-the-one-moment-slow-motion-music-video`
- Spotify Canvas guidelines (no rapid cuts):
  `https://support.spotify.com/us/artists/article/canvas-guidelines/`
- Vashi Visuals on average shot length:
  `https://vashivisuals.com/music-video-editing-stats/`

### ffmpeg feasibility

Already implemented mechanically — the pipeline assembles B-roll into
a concat with per-clip durations.  Missing: the *policy* layer that
decides clip duration per preset.

### Proposed pipeline change

Add to `genre-presets.yaml`:

```yaml
cut_density: dense       # 0.5-1.0s per clip   (kpop_dance, hyperpop, phonk, techno)
cut_density: moderate    # 1.5-3.0s per clip   (uspop, citypop, synthwave, house, rnb)
cut_density: sparse      # 3.0-6.0s per clip   (kpop_ballad, rnb_slow, lofi_hiphop, jazz)
cut_density: continuous  # single-take, no cuts (ambient, drone, classical, dreamcore)
```

The B-roll assembler in `scripts/music-video-render.sh` reads this
field and replaces the current uniform `--clip-duration` argument with
a sampled distribution within the band.  `continuous` mode forces one
clip slowly zoomed via `zoompan` or `tmix` rather than concat.

For a future enhancement, vocal-onset detection from whisper output
(already present in `scripts/correct-captions.py`) can mark "preferred
cut moments" within the band, so cuts align to vocal transients
rather than wall-clock seconds.

## 4. Lyric video design patterns

### Findings

- **Typography choice mirrors brand**: Billie Eilish uses bold
  Helvetica + neon-green-on-black; NewJeans uses Y2K-flourish display
  faces under art director Min Hee-Jin; Jennie's ZEN SERIF blends
  Hangul stroke rhythm with European broad-nib calligraphy.  The
  consistent pattern: lyric typography is a brand signature, not a
  generic font choice.
- **Animation properties**: opacity fade (most common), scale pulse on
  beat, mask reveal (wipe / circle / gradient), position drift.
  Linearity and educationalvoice both list these as the kinetic-typo
  core toolkit.
- **Rhythm-coupled animation**: upbeat chorus → bouncing / pulsing
  text; soft verse → subdued fade.  Animation intensity tracks
  musical intensity.
- **Spotify Canvas avoids on-screen lyrics entirely** — clips are too
  short to sync meaningfully; brand cohesion (album art motion) is
  the recommended route.
- **Hangul-specific consideration**: Hangul's combined vertical +
  horizontal stroke composition creates a denser visual rhythm than
  Latin; vertical-format lyric overlay for Hangul can use tighter
  line-height (1.0–1.1) where Latin defaults to 1.2–1.3.

### Sources

- LyricsVideo4U on typography role in lyric videos:
  `https://lyricsvideo4u.com/blog/the-role-of-typography-in-high-quality-lyric-videos/`
- Linearity on kinetic typography overview:
  `https://www.linearity.io/blog/kinetic-typography/`
- Medium / Jeter Chou on Jennie's ZEN SERIF + Hangul typography:
  `https://jeterchou.medium.com/how-jennies-zen-serif-turns-typography-into-k-culture-aesthetics-63a0290b219f`
- Pixflow on text animation in Premiere:
  `https://pixflow.net/blog/captivating-text-in-minutes-master-eye-catching-animations-in-premiere-pro/`
- Spotify Canvas guidelines (no lyrics in Canvas):
  `https://support.spotify.com/us/artists/article/canvas-guidelines/`

### ffmpeg feasibility

Animation properties for ffmpeg lyric overlay:

- **Opacity fade**: `drawtext=alpha='if(lt(t,T_in+0.3),(t-T_in)/0.3,
  if(gt(t,T_out-0.3),(T_out-t)/0.3,1))'` — 300ms in / 300ms out.
- **Scale pulse**: `drawtext=fontsize='40+5*sin(2*PI*BPM/60*t)'`
  for beat-locked breathing.  Requires BPM known at render time.
- **Position drift**: `drawtext=x='(w-text_w)/2+10*sin(0.5*t)'` for
  gentle horizontal sway on long-hold lyrics.
- **Mask reveal (wipe)**: split base from overlay and use
  `crop=w='min(t-T_in,0.5)*W*2':h=H` on the text layer to wipe in
  left-to-right over 500ms.
- **Per-language font**: ffmpeg `drawtext` accepts `fontfile=` — the
  pipeline can ship `NotoSansKR-Bold.ttf` for `lang:ko` lyrics and
  `Helvetica-Bold.ttf` (or system equivalent) for `lang:en`.

### Proposed pipeline change

Extend lyric overlay spec in `genre-presets.yaml`:

```yaml
lyric_style:
  font_ko: NotoSansKR-Bold.ttf
  font_en: Inter-Bold.ttf       # or Helvetica
  size: 64                      # 1080-wide baseline
  line_height: 1.1              # tighter for KO, 1.2 for EN
  weight: bold
  position: upper_middle        # see §5 safe zones
  animation: fade               # fade | scale_pulse | mask_wipe | drift
  intensity_couples_to: chorus  # animation magnitude scales with chorus flag
```

Implementation lands in `scripts/lyric-overlay.sh` (currently the
drawtext invocation is hardcoded).

## 5. Vertical (9:16) framing rules

### Findings

- **Canvas + safe zones**: 1080×1920 px, with cross-platform safe
  zone (TikTok ∩ Reels ∩ Shorts) of **1080×1420 px centered**.  Stay
  inside that band and no UI eats your content.
- **Right-third UI risk**: TikTok and Reels stack engagement icons
  (like, comment, share, save, profile) on the right ~10% of frame.
  Faces and text positioned in the right third are at risk of being
  partially covered by avatars and follow-buttons.
- **Bottom-third caption risk**: TikTok auto-captions and the "More"
  expander occupy the bottom ~20%.  Lyrics placed there compete with
  platform UI.  Reels and Shorts have similar bottom-UI patterns.
- **Eye placement**: upper third, ~30–35% down from the top edge,
  gives natural headroom while keeping the face above the caption
  UI risk zone.
- **Composition rule**: vertical demands *centered* compositions
  more than horizontal rule-of-thirds because the side margins are
  UI territory.  Faces center-horizontally, eyes upper-third
  vertically.
- **K-pop fancam tradition** (KR-specific): single performer focus,
  centered, full-body or head-to-knee crop, follows the dancer
  smoothly.  This is the dominant vertical-video convention KR
  audiences grew up with and read as "professional".

### Sources

- EdicionVideoPro 9:16 aspect ratio guide:
  `https://edicionvideopro.com/en/editing-techniques/916-aspect-ratio-guide-vertical-video-for-tiktok-reels/`
- House of Marketers safe-zone guide:
  `https://houseofmarketers.com/guide-to-safe-zones-tiktok-facebook-instagram-stories-reels/`
- OrsonLord free safe-zone overlay templates:
  `https://orsonlord.com/articles/free-safe-zone-overlays-for-reels-tiktok-and-shorts`
- Kreatli TikTok safe-zone 2026:
  `https://kreatli.com/guides/tiktok-safe-zone`
- Posteverywhere TikTok aspect ratio 2026:
  `https://posteverywhere.ai/blog/tiktok-aspect-ratio`
- Sources Kpop on K-pop camera types (fancam tradition):
  `https://sourceskpop.com/blog/kpop-camera-types-guide`
- Refinery29 explainer on fancam culture:
  `https://www.refinery29.com/en-us/2020/02/9396499/what-is-a-fancam-twitter-videos-kpop`

### ffmpeg feasibility

Pipeline already outputs 1080×1920.  The missing piece is *layout
intent* — where lyrics go, where the optional thumbnail crop pulls
its face from, and where the auto-thumbnail picks its frame.

Concrete coordinates for a 1080×1920 canvas:

```
Lyric upper-middle band: y ∈ [400, 900]    (avoid 0-250 status bar,
                                            avoid 1500-1920 caption UI)
Face safe zone:          x ∈ [108, 972],   y ∈ [250, 1500]
Eye line:                y ≈ 576           (30% down)
Center column:           x ∈ [108, 972]    (90% safe, 10% margin each side)
Cross-platform safe zone: 1080×1420 centered → y ∈ [250, 1670]
```

### Proposed pipeline change

Add to `scripts/lyric-overlay.sh` a `--position` arg accepting
`upper_third`, `middle`, `lower_third` (with `lower_third` flagged
as "DO NOT use for TikTok/Reels publish — caption-UI conflict").
Default to `middle` (y range 800–1100), shift to `upper_third`
(y range 400–700) for genres where face usually occupies the lower
half (kpop dance crops, fancam-style B-roll).

Pexels picker bias for vertical sources should prefer clips with
the subject already centered.  A simple pre-filter: reject clips
where the brightness-weighted centroid is in the right or left
20% of frame (likely a horizontal source padded to vertical).

## 6. Audio-visual sync techniques beyond beat-cuts

### Findings

- **Zoom-on-drop**: scale animation triggered on the bass drop / kick
  hit.  Cited as the canonical "feels the bass" effect across After
  Effects tutorials.  Trapcode Sound Keys is the AE-native tool;
  the trick is mapping a frequency band (50–150 Hz bass) to scale
  amplitude.
- **Camera shake on bass**: ditto, but on x/y position rather than
  scale.  Replicates the felt subwoofer sensation.
- **Strobe / flash on kick**: brief full-frame brightness pulse;
  pipeline already has `strobe` and `beat_burst`.
- **Color-on-vocal**: hue shift or saturation pulse driven by vocal
  presence (not beat).  Less common but distinctive when used; pairs
  well with vocal-emotional family where beat is sparse.
- **J-cuts and L-cuts** (audio leads / lags video): pro convention
  for smooth scene transitions; the next clip's audio begins ~200ms
  before the visual cut, or the previous clip's audio extends ~200ms
  past it.  For lyric overlay this maps to "show next line slightly
  before vocal onset" — already targeted by the existing
  `LYRIC_LEAD_MS=200` setting.
- **Particle bursts**: AE / Trapcode Particular style; not ffmpeg-
  feasible without GLSL.  Out of scope.
- **Spotify Canvas (no cuts)**: subtle continuous motion only —
  abstract patterns, slow zooms, breathing color shifts.

### Sources

- Superwebtricks BeatViz / bass-drop tool comparison:
  `https://www.superwebtricks.com/i-tested-10-ai-music-video-tools-only-beatviz-ai-actually-heard-the-bass-drop/`
- Tekedia on bass-controlled animation:
  `https://www.tekedia.com/bass-controlled-animation-when-the-beat-drives-the-visuals/`
- MotionArray Premiere Pro bass-shake tutorial:
  `https://motionarray.com/learn/premiere-pro/premiere-pro-bass-shake-effects/`
- Adobe on L-cut / J-cut technique:
  `https://www.adobe.com/creativecloud/video/post-production/cuts-in-film/l-and-j-cut.html`
- Epidemic Sound on J/L cut examples:
  `https://www.epidemicsound.com/blog/j-cuts-and-l-cuts/`
- Editorskeys on music-video sync techniques:
  `https://www.editorskeys.com/blogs/news/how-to-edit-music-videos-like-a-pro-sync-techniques-creative-effects-transitions`

### ffmpeg feasibility

- **Zoom-on-drop**: `zoompan=z='if(eq(on,DROP_FRAME),1.15,zoom)':
  d=15:s=1080x1920` — pulse scale to 1.15× over 15 frames (0.5s at
  30fps) at the drop frame.  Requires drop-frame detection upstream
  (audio analysis with librosa or ffmpeg's `astats`).
- **Camera shake**: `crop=w=1000:h=1900:x='10*sin(20*t)':
  y='10*cos(20*t)'` with t-gated by event window.
- **Color-on-vocal**: `hue=h='if(VOCAL_ON,30*sin(t),0)'` driven
  by whisper presence flags.
- **J/L cut for lyrics**: extend `LYRIC_LEAD_MS` to support per-line
  override and a complementary `LYRIC_TRAIL_MS` for L-cut style
  hold-past-vocal.

### Proposed pipeline change

`shader_events` (already on Phase C roadmap from quality-bar doc):
extend the event types beyond `at: onset|beat|bar|timestamp` to
include `at: drop` and `at: vocal_on`.  Drop detection lands in
`scripts/audio-analyze.sh` (librosa onset_strength with bass-band
filter); vocal_on flags reuse the existing whisper alignment.

## 7. Mood-keyword vocabulary expansion

### Findings

There is no published taxonomy mapping mood-words to B-roll search
terms — every mood-board guide (StudioBinder, Milanote, Linearity,
Unhurd) lists categories but not search-term expansions.  However,
the consistent expansion pattern across sources:

- "**Nostalgic**" expands to: polaroid + warm light + handwritten note,
  super-8 / VHS textures, early-2000s digital-camera, faded photos,
  film grain.
- "**Dreamy / ethereal**": fog / mist, soft focus, golden hour, slow
  motion fabric, water reflections, lens flares.
- "**Cinematic**": anamorphic flares, depth of field, framed-in-doorway
  composition, low-key lighting, location wide-shots.
- "**Cozy / warm** (mellow)**": indoor lamp, rain on window, hands +
  coffee mug, knit textures, candlelight, low-stakes domesticity.
- "**Energetic / dynamic**" (pop/dance): jump cuts, neon, motion blur,
  crowds, lights at speed.
- "**Aggressive / hard** (phonk/drift)**": drift cars, neon street,
  cyberpunk, smoke, low-angle, skull imagery (per the phonk-aesthetic
  sources).
- "**Vaporwave**": Greco-Roman statues, Japanese text signage, pink+
  teal palette, grids, palm trees, early-computer imagery.

The pattern: each mood word has 5–8 *visual primitives* that
collectively define the keyword for B-roll search.  The pipeline
currently has `keyword_pool_ko` / `keyword_pool_en` per genre — the
gap is that those pools are single-axis genre labels, not
multi-primitive mood expansions.

### Sources

- StudioBinder music-video mood board template:
  `https://www.studiobinder.com/templates/mood-boards/music-video-mood-board-template/`
- Milanote film mood-board guide:
  `https://milanote.com/guide/film-moodboard`
- Unhurd Music on artist visual identity:
  `https://www.unhurdmusic.com/blog/find-your-visual-identity-how-to-build-an-artist-mood-board-that-feels-you`
- Creative Market on vaporwave mood-board references:
  `https://creativemarket.com/blog/moodboard-series-vaporwave`
- Aesthetics Wiki Drift Phonk entry:
  `https://aesthetics.fandom.com/wiki/Drift_Phonk`
- Aesthetics Wiki Phonk entry:
  `https://aesthetics.fandom.com/wiki/Phonk`
- Aesthetics Wiki Vaporwave entry:
  `https://aesthetics.fandom.com/wiki/Vaporwave`
- Pexels mood/emotion footage stock (search vocabulary baseline):
  `https://www.pexels.com/search/videos/emotions/`

### ffmpeg feasibility

Not a render-stage concern — this is a B-roll fetch / picker concern.
The change lives in `scripts/pexels-fetch.sh` and is implementable
today.

### Proposed pipeline change

Add `skills/music-video/data/mood-vocabulary.yaml` keyed by mood
word, valued as a list of visual primitives.  Example:

```yaml
nostalgic:
  primitives:
    - polaroid
    - warm light
    - handwritten note
    - super 8
    - faded
    - vintage

dreamy:
  primitives:
    - fog
    - soft focus
    - golden hour
    - slow motion fabric
    - water reflection
    - lens flare

cozy:
  primitives:
    - indoor lamp
    - rain window
    - coffee mug
    - knit
    - candle
    - domestic
```

Each genre preset gains a `mood:` field that resolves to the
primitive list at fetch time.  Pexels query rotates through the
primitives across requests rather than searching the literal mood
word once.  This both (a) raises B-roll diversity (the existing
dedup registry forces repeats now) and (b) anchors the search in
visual concepts instead of abstract feelings.

## 8. Korean vs Western lyric-video conventions

### Findings

- **Color**: KR audiences read warm pastels + high white balance as
  "premium / professional"; teal-and-orange Hollywood look reads as
  "Western action movie" rather than music video.  K-drama and K-pop
  ballad share this convention.
- **Subject framing**: KR fancam tradition centers a single performer
  full-body; Western pop convention often features wider ensemble
  shots or environment-as-subject.  Vertical-format KR content
  inherits the fancam framing.
- **Typography**: Hangul has denser glyph composition than Latin,
  meaning equivalent legibility is achieved at smaller font sizes
  but tighter line-height; pixel-perfect overlay specs must vary
  by language.
- **Lyric placement convention**: KR lyric videos often place
  Hangul + Romanization stacked (InkiStyle, the most-cited KR
  lyric-video site, uses this convention universally).  Stacked
  bilingual overlay is a KR convention not seen in EN lyric videos.
- **Aesthetic tropes**: KR ballad lyric videos lean misting-filter
  + soft pastel; Western indie lyric videos lean handheld + film
  grain + typewriter typography.  KR pop lyric videos lean Y2K-revival
  display fonts (post-NewJeans / Min Hee-Jin).

### Sources

- InkiStyle (canonical KR lyric source, stacked Hangul + Romanized
  convention): `https://inkistyle.com/newjeans-eta-mv/`
- Foster Flyer on visually pleasing K-pop MVs:
  `https://fosterflyer.com/2449/entertainment/top-15-most-visually-pleasing-k-pop-music-videos/`
- Kultscene on K-pop visual appeal:
  `https://kultscene.com/12-visually-appealing-k-pop-music-videos/`
- The Seoul Story on K-pop MV visual production:
  `https://theseoulstory.com/feature-k-pop-music-video-production-the-art-of-visuals/`
- Medium / Jeter Chou on ZEN SERIF / K-culture typography:
  `https://jeterchou.medium.com/how-jennies-zen-serif-turns-typography-into-k-culture-aesthetics-63a0290b219f`
- Refinery29 on fancam culture:
  `https://www.refinery29.com/en-us/2020/02/9396499/what-is-a-fancam-twitter-videos-kpop`
- Dramabeans K-drama color palette:
  `https://dramabeans.com/2019/11/k-drama-color-the-power-of-the-palette/`

### ffmpeg feasibility

All differences are configuration-layer, not render-stage:

- Color: handled by `grade_profile` field (§2).
- Framing: handled by Pexels picker bias (§5).
- Typography: handled by per-language font + line-height in
  `lyric_style` (§4).
- Stacked bilingual: lyric overlay supports two `drawtext` instances
  vertically stacked when `lyric_romanization:` is also provided.

### Proposed pipeline change

Add optional `lyric_romanization:` field to the input lyric file
spec.  When present and `lang_anchor: ko`, render both lines stacked
with the Hangul line above (larger) and the Romanization below
(smaller, ~70% size, opacity 0.7).  This matches the KR-canonical
lyric video convention without breaking the existing single-line
output.

## 9. Industry vocabulary mapping for shaders

### Findings

The 23 shaders (15 original + 8 from the 2026-05-22 expansion) all
have DaVinci Resolve / After Effects equivalents:

| Pipeline name | Industry name(s) | Source |
|---|---|---|
| `pond` | Displacement Map / Caustics | AE Displacement Map, Resolve Displacement |
| `breathing` | Slow Push-In / Zoom | Resolve Transform, AE Scale keyframe |
| `halation` | Halation / Bloom | Dehancer Halation, Resolve OFX Halation (17.3+) |
| `combo` | Composite (halation + displacement) | (no direct industry equivalent — pipeline-specific) |
| `scanline` | Scanlines / CRT | Red Giant Holomatrix, AE CC Scanlines |
| `chromatic_split` | Chromatic Aberration / Prism | Resolve Prism Blur, AE Optics Compensation |
| `neon_edge` | Edge Detect + Glow | Resolve Edge Detect + Glow, AE Find Edges + Glow |
| `vhs` | VHS / Tape Damage | Red Giant VHS, AE Bad TV preset |
| `saturation_pulse` | Saturation Animation | Resolve Color Saturation key, AE Hue/Saturation |
| `kaleidoscope` | Kaleidoscope | AE CC Kaleida, Resolve Mirror |
| `beat_burst` | Light Flash / Hit | AE Brightness pulse, Resolve Curves animation |
| `strobe` | Strobe / Flash | (universal name) |
| `shake` | Camera Shake | AE Wiggler, Resolve Camera Shake |
| `color_burst` | Hue Shift / Color Pop | AE Hue/Saturation, Resolve Hue rotate |
| `light_rays` | God Rays / Volumetric Light | AE Trapcode Shine, Resolve OFX Light Rays |
| `light_leak` | Light Leak / Lens Flare | Dehancer Light Leak, Red Giant Light Factory |
| `duotone` | Duotone / Two-Tone | Resolve Color Effect Duotone, AE Tritone |
| `vignette_pulse` | Vignette Animation | Resolve Vignette, AE CC Vignette + keyframe |
| `paper_grain` | Texture / Canvas Overlay | AE Grain, Resolve Texture |
| `dust_speck` | Film Dust / Damage | Resolve Film Damage, AE CC Particle World |
| `posterize` | Posterize / Color Steps | Resolve Color Generator, AE Posterize |
| `trail_echo` | Echo / Motion Blur Trails | AE Echo effect, Resolve Frame Blending |
| `soft_bloom` | Soft Bloom / Glow | DaVinci Resolve 19 Halation+Bloom combo, AE Glow |

### Sources

- Dehancer halation article:
  `https://blog.dehancer.com/articles/halation/`
- WriteDirect Dehancer DaVinci usage:
  `https://writedirect.co/how-to-use-dehancer-in-davinci-resolve/`
- MonoNodes film emulation overview:
  `https://mononodes.com/film-emulation/`
- YouTube — chromatic aberration in DaVinci:
  `https://www.youtube.com/watch?v=Z9SGfZg7P10`

### Proposed pipeline change

No code change.  Add a `# Industry name:` comment line above each
shader function in `scripts/music-video-shaders.sh` so operators
googling for tutorials find the right vocabulary.  Mention the
mapping in `docs/research/2026-05-22-shader-vocabulary.md` as a
cross-reference table.

## 10. Prioritized action list

Ranked by impact × ease-of-implementation:

| Pri | Change | File(s) | Effort | Impact |
|---|---|---|---|---|
| 1 | `cut_density` per genre preset | genre-presets.yaml, render | ~3h | High — fixes per-genre pacing mismatch |
| 2 | Lyric `position: upper_middle` default | lyric-overlay.sh | ~1h | High — fixes cross-platform UI conflict |
| 3 | `grade_profile` field + base-grade stage | genre-presets.yaml, new music-video-grade.sh | ~4h | High — biggest visual quality lift per the kpop/lofi/synthwave divergence |
| 4 | `mood-vocabulary.yaml` + Pexels primitive rotation | new mood-vocabulary.yaml, pexels-fetch.sh | ~3h | High — multiplies B-roll diversity, anchors search in visuals |
| 5 | KR stacked bilingual lyric (Hangul + Romanization) | lyric-overlay.sh, lyric spec | ~2h | Medium — KR-canonical convention |
| 6 | Per-language font in lyric overlay | lyric-overlay.sh, ship NotoSansKR | ~1h | Medium — typography hygiene |
| 7 | Industry-name doc comments in shader script | music-video-shaders.sh, shader-vocab doc | ~30min | Low impact but trivial |
| 8 | Drop detection + zoom-on-drop event | audio-analyze.sh, shader_events extension | ~5h | Medium — distinctive but only for tracks with clear drops |
| 9 | J/L cut for lyric overlay (`LYRIC_TRAIL_MS`) | lyric-overlay.sh | ~2h | Low-Medium — subtle polish |
| 10 | Pexels centered-subject filter | pexels-fetch.sh | ~2h | Medium — improves vertical framing for B-roll |

Items 1–4 are the recommended next-phase batch — together they
address the cross-genre monotony, the platform-UI conflict, the
color-grade mismatch, and the B-roll repetition simultaneously.
That's the largest visible-quality delta available before any of
the more speculative work (drop detection, J/L cut nuance).

## 11. Out of scope

- **Paid AI-video generation** (Runway, Sora, Pika, Kling) — money
  firewall.  These would obsolete much of the B-roll layer but
  require explicit operator confirmation per pricing tier.
- **Paid LUT packs** (FilterGrade, FCPX Full Access, Cutting Room FX,
  Mixx Studios K-pop LUTs) — money firewall.  Pipeline's
  `grade_profile` field implements the *directions* of those LUTs in
  ffmpeg-native filters instead of buying the .cube files.
- **GLSL-only effects** (Trapcode Particular particle systems,
  CC Particle World, Trapcode Shine volumetric rays beyond `light_rays`'s
  baked god-ray approximation) — ffmpeg-only constraint.
- **Real-time VJ tools** (TouchDesigner, Resolume Arena) — pipeline
  is offline-render, not live-performance.
- **DaVinci Resolve OFX plugins** (Halation, Glow, Film Look) —
  binary-coupled to Resolve.  Pipeline reimplements equivalents
  with `geq`, `blend`, `eq`.
- **AE Trapcode Sound Keys** (audio-to-keyframe extraction in
  After Effects) — replaced in pipeline by librosa onset-strength
  + custom shader_events.
- **Manual color-grade per shot** — pipeline operates per-render, not
  per-shot.  Operator-supplied grade lookup tables would be a
  feasible extension but out of current scope.

## 12. Companion documents

- `docs/research/2026-05-22-shader-vocabulary.md` — the effect-layer
  taxonomy this doc references in §9.
- `docs/research/2026-05-22-music-video-quality-bar.md` — the
  operator-stated directives that motivated this entire research
  phase.
- `docs/research/2026-05-21-music-shorts-formats-landscape.md` — the
  format-landscape companion (8s Spotify Canvas vs 60s short vs
  full 3-min vertical MV).
- `docs/research/2026-05-21-shader-song-mismatch-diagnosis.md` —
  the diagnosis doc that identified per-genre monotony as the root
  quality issue this research helps close.
