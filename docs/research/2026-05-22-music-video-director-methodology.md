# Music-Video Director Methodology — 2026-05-22

Companion research to `docs/research/2026-05-22-music-video-pro-practices.md`
(technique-level: LUT numbers, cut frequencies, ffmpeg mapping).  This
document covers the layer *above* technique: how working music-video
directors think, what abstract principles they prove, and which of
those principles are achievable with our current ffmpeg + Pexels +
Ollama stack vs. which require additional scripts, paid services, or
operator-curated assets.

Purpose: when the automated pipeline reaches a quality plateau, the
remaining gap is usually not a shader or LUT — it is a director-level
choice (what to repeat, what to hold, where to leave the frame empty,
what to align across the chorus).  This doc inventories those choices
and tags each with a feasibility level so future planning can stop
reaching for tools and start reaching for craft.

Cross-references:

- Shader vocabulary: `docs/research/2026-05-22-shader-vocabulary.md`
- Quality bar directive: `docs/research/2026-05-22-music-video-quality-bar.md`
- Format landscape: `docs/research/2026-05-21-music-shorts-formats-landscape.md`
- Diagnosis of prior shader-song mismatches: `docs/research/2026-05-21-shader-song-mismatch-diagnosis.md`

---

## Executive summary

- Working music-video directors share five operational primitives:
  **shot-list discipline**, **visual rhyme** (recurring motif),
  **color contract** (a 2–3 swatch palette held for the whole video),
  **coverage vs. hold** (knowing when to cut and when to let one shot
  breathe), and **negative space** (intentional emptiness for the eye
  and for text).
- Of these, our pipeline already has weak versions of two: visual
  rhyme via `KEYWORDS[0]` recurrence, and color contract via the
  per-genre `lut_direction` field.  We have **no** explicit shot-list
  step, no hold/coverage decision other than `phrase_beats`, and no
  legibility-aware negative-space check.
- The single highest-leverage director-level concept to adopt this
  week is the **per-track shot plan** as an intermediate artefact:
  before fetching B-roll, write a 12–20 row plan (segment → emotion
  → keyword → cut behaviour → motif slot) and let the rest of the
  pipeline consume it.  This is a one-script addition over existing
  tools and unlocks every other director-level concept below.
- Director-level work that **requires AI image/video gen** (eyeline
  continuity across cuts, custom motif staging, true narrative
  through-line) is correctly out of scope for the current local-only
  + Pexels stack and stays parked.
- K-pop industry conventions (versions culture, member rotation,
  saturated monochrome blocks, "15-second rule") are mostly
  **artist-asset dependent**, not directorial — they require real
  performers or proprietary footage and don't map to a B-roll pipeline.
  The exception is the 15-second rule, which is a sequencing
  constraint we can adopt directly.

---

## Director profiles

Each profile is signature technique + 1–2 representative works + the
abstract principle that work proves.

### Hiro Murai

- **Signature.**  Surrealism inside everyday spaces.  Long-take
  choreography intercut with sudden symbolic violence.  Frames
  "puzzle-solved" so each shot fits the next without spelling out the
  narrative.  Influences: Takeshi Kitano + David Lynch.
- **Representative works.**  Childish Gambino, *This Is America*
  (2018); Flying Lotus, *Never Catch Me*; multiple *Atlanta* episodes.
- **Principle proved.**  *Juxtaposition is the cheapest narrative
  tool that works.*  Two unrelated images cut together produce
  meaning the audience supplies for free.  The director's job is
  selecting the second image.
- **Source.**  Creative Review profile; Wikipedia; IndieWire on
  the *City of God + mother!* reference set for *This Is America*.

### Michel Gondry

- **Signature.**  Practical effects built on set rather than added in
  post.  Frame-by-frame stop-motion, forced perspective, palindromic
  long takes, hand-built kinetic geometry.  Sets are designed around
  the effect, not the other way round.
- **Representative works.**  The White Stripes, *The Hardest Button
  to Button* (32 identical Ludwig kits, three 16-hour daylight days,
  shot in reverse); The Chemical Brothers, *Star Guitar* (every
  landscape feature on the train window pre-timed to the beat); Björk,
  *Bachelorette*.
- **Principle proved.**  *Constraint as concept.*  Pick one
  impossible rule (every drum hit appears in space; every passing
  building is on a beat) and let the entire video be that rule
  executed honestly.
- **Source.**  No Film School ("How Gondry Pulled off Those Insane
  Effects"); Wikipedia (*Hardest Button to Button*); Films Fatale.

### Spike Jonze

- **Signature.**  Genre-pastiche + a single performance idea
  carried to absurdity.  Skate-video energy fused with sitcom
  framing; dance as primary narrative beat.  Production design that
  feels lived-in rather than designed.
- **Representative works.**  Beastie Boys, *Sabotage* (presented as
  a fake 1970s cop-show opening title sequence); Fatboy Slim,
  *Weapon of Choice* (Christopher Walken dances through a deserted
  hotel; six MTV VMA wins including direction, choreography, visual
  effects).
- **Principle proved.**  *One idea, fully committed.*  The strongest
  videos pick a single unlikely image (Walken flying, a band playing
  cops) and refuse to dilute it.
- **Source.**  Wikipedia (*Weapon of Choice*); No Film School;
  Far Out Magazine "8 best Spike Jonze music videos".

### Director X (Julien Christian Lutz)

- **Signature.**  Geometric minimalist sets where lighting *is* the
  set.  Few elements, hard color blocks, performer-as-figure rather
  than performer-as-actor.  Lineage traced to Sean Paul's *Gimme the
  Light* (2002); audiences read it as James Turrell.
- **Representative work.**  Drake, *Hotline Bling* (2015) — under
  six minimal set pieces, Spandex-over-frame light boxes backlit by
  Chroma-Q Color Force LED banks, undirected Drake dancing.
- **Principle proved.**  *A single lit volume can carry an entire
  video if the lighting is honest.*  Removes set-dressing as a
  variable so colour and performer-silhouette do all the work.
- **Source.**  Azure Magazine; Rolling Stone; CBC News.

### Joseph Kahn

- **Signature.**  Feature-film production language imported into the
  four-minute form.  Multi-location, action-set-piece, ensemble cast,
  heavy colour-grade.  Treats videos like trailers for movies that
  don't exist.
- **Representative works.**  Taylor Swift, *Bad Blood* (compared to
  *Sin City*, *Kill Bill*, *Mad Max: Fury Road* — Grammy Best Music
  Video, MTV Video of the Year); Eminem, *Without Me*.
- **Principle proved.**  *Production value is itself a narrative
  statement.*  When the form is so much bigger than the song
  requires, the size becomes the message.  Inverse of Director X's
  minimalism; both work because they are *committed* to the level
  chosen.
- **Source.**  Wikipedia (Joseph Kahn director); Billboard "Essential
  Music Videos"; Washington Post profile.

### Dave Meyers

- **Signature.**  Surreal tableaux organised by composition rather
  than story.  Symbolic framing (Last Supper, preacher, fisheye
  Christ), dramatic chiaroscuro lighting, motifs that recur across a
  single artist's discography.
- **Representative work.**  Kendrick Lamar, *HUMBLE.* (2017, with
  the Little Homies) — six 2017 MTV VMA wins including Best
  Direction; Grammy Best Music Video 2018.
- **Principle proved.**  *Composition beats narrative.*  A non-linear
  series of strong images held longer than expected reads as
  intentional, not random.  The viewer assembles the story.
- **Source.**  Dave Meyers official page; HotNewHipHop "15 Classic
  Music Videos"; D&AD Awards archive.

### Colin Tilley

- **Signature.**  Choreography-as-architecture.  Big-budget sets and
  costumes built to support a specific dance routine — not the other
  way round.  Visual style references (Tim Burton, Dr. Seuss for
  *WAP*) chosen to match the choreography's tone.
- **Representative works.**  Kendrick Lamar, *Alright* (stark black
  and white; 2016 Grammy nomination for Video of the Year); Cardi B
  feat. Megan Thee Stallion, *WAP* (JaQuel Knight choreography, 3–4
  weeks of rehearsal, distorted Burton-Seuss visual language).
- **Principle proved.**  *Build the set for the move.*  When dance
  is the content, every other element exists to make the dance
  legible — sight lines, floor finish, costume silhouette against
  background.
- **Source.**  Wikipedia (*WAP*); Variety on Tilley; Billboard
  interview with Tilley & Boy in the Castle team.

### Shin Woo-seok (Dolphiners) — NewJeans

- **Signature.**  "Seeing from the outside, differently."  Idol
  members framed via an outsider character's POV (Ban Hee-soo in
  *Ditto*), 4:3 camcorder aesthetic, soft palettes, analog texture,
  fourth-wall breaks (*OMG*).  Inverts standard K-pop idol-as-product
  framing into idol-as-friend-of-the-camera.
- **Representative works.**  NewJeans, *Ditto* (2022) — top trending
  in KR / JP / MENA / SA / NA; NewJeans, *OMG* (2023) — sparked
  industry debate about MV as critique-of-the-industry artefact.
- **Principle proved.**  *Whose eyes are we behind matters more than
  what they see.*  POV reassignment is a costless reframe that
  changes the entire emotional register.
- **Source.**  Cine21 interview (Shin Woo-seok); Korea Herald
  ("video maestros say videos offer the full K-pop package"); IMDb
  (Wooseok Shin).

### Hong Won-ki / Zanybros (홍원기)

- **Signature.**  Industrial-scale K-pop visual factory.  Founded
  2002.  Established the genre conventions most other K-pop MVs now
  follow.  Operates on a stated **"15-second rule"** — the most
  impactful visual hook must land in the first 15 seconds of the
  clip.  Worked with Seo Taiji, BTS, EXO, PSY, SHINee, HyunA, Mamamoo,
  Wanna One, KARD, Super Junior, Girls' Generation.
- **Principle proved.**  *Front-load the iconography.*  In a feed
  where the next swipe is one thumb-flick away, the 15-second
  attention contract is non-negotiable.  Hong estimates MVs account
  for roughly 70 % of K-pop's overseas success — the visual layer
  *is* the export product.
- **Source.**  Korea Times interview; Rolling Stone India "The
  Legacy of ZanyBros"; Wikipedia (Zanybros); Candy Magazine "What
  It's Really Like To Make K-pop MVs".

### Mathy & Fran (directing duo, UK)

- **Signature.**  Directing-as-debate process.  Multiple takes, then
  a "screening party" with DP and producer to vote on which take —
  not which technically-perfect take, but which emotionally-correct
  take.  Listen to the track *before* reading the artist brief to
  preserve a pre-brief first impression.
- **Principle proved.**  *The right take is rarely the cleanest
  take.*  Useful counterweight to automated pipelines that optimise
  for technical metrics (sharpness, exposure, beat-alignment) and
  silently throw away takes that scored worse on those axes but
  better on feel.
- **Source.**  Musicbed Blog "The Art of the Music Video: A
  Conversation with Mathy and Fran"; LBBOnline "Radar: Directing
  Duo Mathy and Fran".

---

## Director-level concepts mapped to our pipeline

For each concept: what it is, the canonical director who proves it,
current pipeline coverage, and concrete next-step feasibility.

### Shot list / storyboard discipline

- **What.**  Before shooting, the director writes a per-segment plan
  naming shot size, camera move, location, character action, and
  emotional intent for each beat of the song.  Industry guidance:
  20–40 frames for a 3–4 minute video, 3–6 frames per song section.
  The storyboard is a *communication tool, not art*.
- **Current coverage.**  None.  Our pipeline goes
  `keywords → Pexels query → 8 clips → align to onsets`.  There is
  no intermediate plan; the operator cannot inspect intent before
  the renderer commits.
- **Feasibility.**  *Achievable with one additional script.*  Add a
  `scripts/shot-plan.sh` that takes the song's aubio-detected phrase
  boundaries + the operator's keywords + the chosen genre preset, and
  emits a JSON table (segment_idx, t_start, t_end, emotion,
  keyword, cut_behaviour, motif_slot).  Downstream stages consume
  the JSON instead of the flat keyword list.  No new external tool.
- **Reference.**  StudioBinder shot-list template; Orphiq
  storyboard guide; Shai Creative 2025 guide.

### Visual rhyme (recurring motif)

- **What.**  A small visual element returns at predictable points
  (every chorus, every drop) so the viewer's eye learns to anticipate
  it.  Design-theory term: *repetition with variation* — same motif,
  slightly modified each return, exploiting the brain's
  "same/except" detector.
- **Current coverage.**  Weak.  `KEYWORDS[0]` recurs at every 3rd
  segment by accident of array indexing, not by design.  No variation
  applied — same query, often same clip pool.
- **Feasibility.**  *Achievable with current stack.*  Mark one
  keyword as the *chorus motif* in the shot plan; on each return,
  apply a small deterministic variation (different LUT slot, mirrored
  flip, longer hold, shader pulse).  Pure ffmpeg.
- **Reference.**  IxDF "Repetition, Pattern, Rhythm"; Khan Academy
  "Pattern, repetition and rhythm, variety and unity"; Grokipedia
  "repetition variation".

### Color contract

- **What.**  A 2–3 swatch palette declared up-front and held for the
  whole video.  Director X's *Hotline Bling* is the canonical
  minimal-palette case (per-section monochrome rooms cycling).
  K-pop convention: bright/cute = pastels; dark/powerful = blacks +
  blood reds + electric blues; retro/Y2K = warm grain + saturated
  greens + oranges.
- **Current coverage.**  Partial.  `lut_direction` field exists per
  genre preset; the renderer reads it as a hint but does not enforce
  a palette across clips.
- **Feasibility.**  *Achievable with current stack.*  Promote
  `lut_direction` from hint to enforced LUT pass (ffmpeg `lut3d` or
  `curves`), applied uniformly to every B-roll clip in the mission.
  Optionally allow the shot plan to override per-segment for chorus
  emphasis.
- **Reference.**  Archemist-in-the-Making blog "K-Pop Music Videos
  is My Architecture Escape"; Make It Pop colour palette analysis;
  Medium "Historical Styles in K-Pop Music Videos".

### Coverage vs. single-shot (hold vs. cut)

- **What.**  The director's choice between holding one shot long
  enough to feel a moment (Murai, Director X) and rapidly cutting
  to compress information (Kahn).  Both work; the failure mode is
  defaulting to medium-tempo cuts everywhere.
- **Current coverage.**  Partial.  `phrase_beats` per genre lets
  ambient → 999 (no cuts) and hyperpop → 4 (every 4 beats).  But no
  per-segment escalation — a song with a quiet verse and a loud
  chorus uses the same cadence throughout.
- **Feasibility.**  *Achievable with one additional script.*  The
  shot plan declares `cut_behaviour: hold | cut_on_beat |
  cut_on_phrase | strobe` per segment, derived from energy-curve
  analysis of the audio (aubio onset density per window).
- **Reference.**  Audio Network "Shot Lists: What Are They"; the
  technique companion doc covers cut-frequency numbers.

### Negative space

- **What.**  Intentional emptiness in the frame.  Two purposes:
  (1) compositional weight / breathing, (2) reserving a frame
  region where future text/lyric overlay won't fight the image.
  Research finding: lyric-video legibility fails most often because
  the image is too busy to host text, not because the font is wrong.
- **Current coverage.**  None.  We have no text overlay yet (skill
  is "music as primary audio, no captions" per validated mode), so
  the legibility branch is moot.  But the compositional-weight
  branch is also unaddressed — Pexels queries return busy
  thirds-rule footage by default.
- **Feasibility.**  *Achievable with current stack* for the
  compositional branch (add "minimal", "single subject", "empty
  background" to keyword expansion when genre preset is
  Atmospheric or Mellow).  *Requires manual curation* for true
  text-safe regions because Pexels API doesn't expose subject
  bounding boxes.
- **Reference.**  Wikipedia (negative space); Design4Users "Negative
  Space in Design"; Wondershare "Best Fonts for Lyric Video 2025";
  arXiv 2308.14922 "Automated Conversion of Music Videos into
  Lyric Videos".

### Eyeline / screen-direction continuity

- **What.**  Across cuts, if subject A is looking screen-right in
  shot 1, A still looks screen-right in shot 2 unless the eyeline
  is deliberately broken.  Same for movement direction.  Continuity
  editing convention; breaking it disorients.
- **Current coverage.**  None.  Pexels clips are concatenated in
  fetch order; subjects can flip left-right across consecutive cuts.
- **Feasibility.**  *Requires AI image/video gen or manual curation
  to do correctly.*  Pexels API metadata doesn't include subject
  pose / gaze direction.  Would need either (a) a vision model pass
  to estimate per-clip gaze direction then sort, or (b) operator-
  curated B-roll directories tagged by direction.  Out of scope for
  the current local-only stack; flag for the "if we add paid AI
  vision" tier.
- **Reference.**  Filmmakers Academy "Eyeline Match"; Wikipedia
  ("Eyeline match"); StudioBinder "How Important Is Eyeline
  Matching".

---

## Behind-the-scenes / interview sources, ranked by relevance

These are the actual sources an operator (or the agent that reads
this doc next) should watch / read to internalise director-level
craft.  Ranked by directness of pipeline application.

1. **Rolling Stone — Director X on *Hotline Bling*.**  Best single
   article on minimal-set + lighting-is-the-set design.  Maps
   directly to our `lut_direction` + colour-contract work.
   <https://www.rollingstone.com/music/music-features/director-x-on-making-drakes-dance-crazy-meme-ready-hotline-bling-74063/>

2. **Azure Magazine — Behind the Scenes of *Hotline Bling*.**
   Production-design level: Spandex membrane choice, Chroma-Q LED
   model, why it reads like Turrell.  Read alongside the Rolling
   Stone piece.
   <https://www.azuremagazine.com/article/behind-the-scenes-of-drakes-hotline-bling/>

3. **No Film School — How Gondry Pulled off the White Stripes
   Effects.**  The clearest "one impossible rule, executed honestly"
   case study; directly maps to *constraint as concept*.
   <https://nofilmschool.com/2017/05/watch-how-michel-gondry-pulled-off-all-those-insane-effects-white-stripes-videos>

4. **Korea Times — Interview with Zanybros / Hong Won-ki.**  Where
   the "15-second rule" is stated by its author.  Directly
   actionable as a sequencing constraint for our front-loaded edit.
   <https://www.koreatimes.co.kr/www/art/2022/09/732_290771.html>

5. **Rolling Stone India — The Legacy of ZanyBros.**  Companion to
   the Korea Times piece; covers the industrial-scale K-pop visual
   factory model and which artists shaped which conventions.
   <https://rollingstoneindia.com/the-legacy-of-zanybros/>

6. **Creative Review — Inside the Surreal World of Hiro Murai.**
   Best long-form on Murai's juxtaposition method, influences
   (Kitano, Lynch), and why his frames "puzzle-solve".
   <https://www.creativereview.co.uk/hiro-murai-director/>

7. **IndieWire — Hiro Murai on *This Is America* Influences.**
   Cites *City of God* + last 20 minutes of *mother!* as the
   reference set.  Useful for understanding how references are
   *composited*, not copied.
   <https://www.indiewire.com/features/general/hiro-miurai-this-is-america-director-influences-1201963031/>

8. **Premium Beat — Editor of *This Is America* on Building the
   Iconic Video.**  Rare BTS from the editor's chair; covers how
   long-take choreography was protected through the cut.
   <https://www.premiumbeat.com/blog/interview-editor-this-is-america/>

9. **Musicbed — Mathy & Fran on directing process.**  The "take six
   beat take one" anecdote is the cleanest argument against
   technical-metric-only selection in automated pipelines.
   <https://www.musicbed.com/articles/filmmaking/directing/the-art-of-the-music-video-a-conversation-with-mathy-and-fran/>

10. **Wikipedia — *The Hardest Button to Button*.**  Production
    numbers (32 kits, 16 mic stands, three 16-hour days, shot in
    reverse) — useful as the canonical "what does fully-committed
    practical effects actually cost" baseline.
    <https://en.wikipedia.org/wiki/The_Hardest_Button_to_Button>

11. **Wikipedia — *Weapon of Choice*.**  Catalogues the six MTV
    VMA wins and the simple plot (man hears song, dances around
    hotel, returns to chair) — clearest case of "one idea, fully
    committed".
    <https://en.wikipedia.org/wiki/Weapon_of_Choice_(song)>

12. **Wikipedia — Joseph Kahn (director).**  Filmography +
    movie-grammar-in-MV approach.  Useful as the *opposite pole* to
    Director X's minimalism.
    <https://en.wikipedia.org/wiki/Joseph_Kahn_(director)>

13. **Dave Meyers official site — *Humble* page.**  Director's own
    notes on the shoot.  Read alongside the D&AD entry for the
    awards-circuit framing of composition-over-narrative.
    <https://davemeyers.com/featured/kendrick-lamar-humble/>

14. **D&AD Awards archive — *HUMBLE.*.**  Awards-judged write-up
    of why the composition reads as intentional.
    <https://www.dandad.org/work/d-ad-awards-archive/humble>

15. **Cine21 — Shin Woo-seok interview (NewJeans director).**  The
    POV-reassignment principle in his own words.  Korean-language
    source via the @juantokki translation thread on X.
    <https://x.com/juantokki/status/1854102618513293609>

16. **Allkpop — Shin Woo-seok responds on *Ditto* / *Infinite
    Challenge* similarity.**  Useful for understanding the
    reference-vs-homage distinction in K-pop MV criticism.
    <https://www.allkpop.com/article/2022/12/dolphiners-film-ceo-shin-woo-seok-responds-to-the-similarities-between-newjeans-ditto-music-video-and-infinite-challenge>

17. **Korea Herald — Music, beauty, fashion: K-pop video maestros.**
    Industry-side overview; the "70 % of K-pop's overseas success"
    estimate is from this piece.
    <https://m.koreaherald.com/article/2895451>

18. **Korea Times — Why K-pop dance practice videos became
    popular.**  The "versions culture" (band ver. / dance ver. /
    techwear ver.) and why it works as a content multiplier.
    <https://www.koreatimes.co.kr/www/nation/2021/01/732_302847.html>

19. **Variety — Director X launches *Video Star* series.**  Director
    X's own documentary series examining iconic music videos; the
    closest thing to a serious music-video-craft TV show currently
    running.
    <https://variety.com/2023/music/news/director-x-video-star-music-videos-interview-1235568666/>

20. **StudioBinder — Music Video Shot List Template.**  Canonical
    industry shot-list reference; format we should model
    `scripts/shot-plan.sh` output on.
    <https://www.studiobinder.com/templates/shot-list/music-video-shot-list-template/>

Note on YouTube BTS links: the search results surfaced a "Making of
*Hotline Bling* With Director X" on Framework's channel
(`youtube.com/watch?v=i8Owe3zUPcA`) and a "How K-pop music videos
are made / Meet legendary MV maker Zanybros"
(`youtube.com/watch?v=VCoDSemYvfU`).  Both are listed but unverified
in this doc — YouTube pages return only the footer to a text-mode
fetcher, so confirm in a browser before deep-linking from a published
artefact.

---

## K-pop specific MV craft

Hidden conventions vs. Western MVs.  Tagged with whether each maps
to our pipeline.

### Member close-up rotation

Every member of the group receives roughly equal close-up screen
time within the music video, frequently rotating in time with the
choreography's "highlight" moments.  Solo highlight passes (often
4–8 seconds each in a 3-minute song) are scheduled deliberately.
*Does not map to our pipeline* — we have no performers.  Cited so
the operator does not try to brute-force a "K-pop look" by adding
arbitrary face close-ups from Pexels; the convention requires a
known cast, not anonymous stock people.

### Versions culture

Standard release pattern: main MV + dance practice + dance ver. +
performance ver. + behind-the-scenes + concept-photo reveal.  TWICE
*Cry for Me* shipped two choreography videos with different
lighting and camera moves; aespa *Black Mamba* shipped standard +
techwear choreography versions.  *Does not directly map* but is a
**channel-strategy** signal: a single track can support several
short-form derivatives.  Applicable as a content-multiplier idea
(same music, different shader/genre preset → different upload).

### Saturated single-colour set blocks

Per-section monochrome wash (full-frame pink, then full-frame
electric blue, then full-frame green) — Bibi *Restless* (2020) is
the textbook example.  Used to compress emotional shift into a
single colour-change cue rather than a cut.  *Achievable with
current stack* — apply per-segment hue-bias LUT in the renderer;
already partially supported by `color_burst` shader.

### Crowd density tricks

K-pop MVs frequently use one of two tricks to imply scale: (1)
mirror reflection compositing for stage shots, (2) tightly-framed
multi-member shots that read as crowd in motion.  *Does not map*
directly; would require human B-roll we don't have.

### Choreography frame composition

Wide shot wide enough to show the full formation, but always
composed so the lead vocalist of that line is at golden-section
horizontal position, not dead-centre.  *Does not map* — depends on
dancer staging.  Cited so a future "dance-mode" pipeline knows
where to look.

### The 15-second rule (Zanybros)

The most impactful visual hook lands in the first 15 seconds.  In
short-form (60-second YouTube Shorts, TikTok, Reels), this is even
more compressed — the swipe-away decision happens at 3–5 seconds.
*Directly applicable* as a sequencing constraint: the strongest
clip in the fetched B-roll set should be placed in segment 1, not
ordered by API return rank.  Achievable today by adding a "hook
position" parameter to the shot plan.

---

## Prioritised action list — top 5 director-level adoptions

Ordered by impact-to-effort.  Each entry includes the principle
proved, the canonical director, current state, and the concrete
delta.

1. **Add `scripts/shot-plan.sh` as a pre-fetch intermediate.**
   *Principle:* shot-list discipline (every director).  *Delta:*
   emit JSON (segment_idx, t_start, t_end, emotion, keyword,
   cut_behaviour, motif_slot, hook_position) from
   aubio-detected phrase boundaries + operator keywords + genre
   preset.  Downstream stages consume the JSON; operator can
   inspect/edit before render commits.  Unblocks every concept
   below.

2. **Enforce `lut_direction` as a colour contract, not a hint.**
   *Principle:* colour contract (Director X).  *Delta:* promote
   `lut_direction` from text descriptor to enforced ffmpeg `lut3d`
   or `curves` pass applied uniformly across every B-roll clip in
   the mission.  Add a 2–3 swatch palette per genre preset and
   honour it.

3. **Implement chorus-motif return with variation.**
   *Principle:* visual rhyme (Hiro Murai, design-theory
   "repetition with variation").  *Delta:* mark one keyword as
   `motif: chorus` in the shot plan; on each chorus segment,
   re-use that clip pool with a deterministic variant (mirrored
   flip on return 1, longer hold on return 2, shader pulse on
   return 3).  Pure ffmpeg.

4. **Apply the 15-second rule to clip ordering.**
   *Principle:* front-loaded iconography (Hong Won-ki / Zanybros).
   *Delta:* in the shot plan, mark segment 1 as `hook_position:
   true`; the renderer assigns the highest-rated clip (by Pexels
   stat or by genre-affinity heuristic) to that slot rather than
   the API's default order.  For short-form, compress the rule
   further: best clip → first 5 seconds.

5. **Per-segment cut-behaviour derived from audio energy.**
   *Principle:* coverage vs. hold (Murai's long takes, Kahn's
   rapid cutting).  *Delta:* per-segment `cut_behaviour: hold |
   cut_on_beat | cut_on_phrase | strobe`, derived from aubio
   onset-density-per-window analysis.  Replaces the current
   global `phrase_beats` constant.  No new external tool.

These five together address every director-level concept that is
*achievable with current stack* or *achievable with one additional
script + existing tools*.  The remaining concepts (eyeline,
narrative through-line, performer composition) are gated on assets
or paid models and stay out of scope.

---

## Out-of-scope items

Documented here so the next planner does not re-evaluate them from
scratch.

- **Eyeline / movement direction continuity across cuts.**  Needs
  vision-model gaze estimation or operator-curated tagged B-roll.
  Re-evaluate if/when paid vision API enters the money-firewall
  budget.

- **True narrative through-line (Murai, Jonze).**  Requires
  performers, locations, and a script.  Outside a B-roll
  recomposition pipeline.

- **Choreography integration (Tilley).**  Requires dancers, a
  choreographer, and a studio.  Outside scope.

- **Member close-up rotation, crowd-density tricks, choreography
  framing.**  All require known cast and proprietary footage.
  Cited for completeness; not a current goal.

- **Frame-by-frame practical effects on built sets (Gondry).**
  Honourable mention only — fundamentally incompatible with an
  automated stock-footage pipeline.  The *principle* (constraint
  as concept) is transferable; the *method* is not.

- **Multi-version release strategy (K-pop versions culture).**
  Channel-strategy concern rather than a pipeline change; if we
  do adopt it, it sits at the publishing layer (one music file →
  N renders with different genre presets), not in the renderer
  itself.

---

## Sources

Verified during research (2026-05-22).  Music-video director
profiles, BTS articles, design-theory references.

- [Hiro Murai — Wikipedia](https://en.wikipedia.org/wiki/Hiro_Murai)
- [Inside the surreal world of Hiro Murai — Creative Review](https://www.creativereview.co.uk/hiro-murai-director/)
- [Hiro Murai on *This Is America* influences — IndieWire](https://www.indiewire.com/features/general/hiro-miurai-this-is-america-director-influences-1201963031/)
- [Editor of *This Is America* — Premium Beat](https://www.premiumbeat.com/blog/interview-editor-this-is-america/)
- [How Michel Gondry pulled off the White Stripes effects — No Film School](https://nofilmschool.com/2017/05/watch-how-michel-gondry-pulled-off-all-those-insane-effects-white-stripes-videos)
- [*The Hardest Button to Button* — Wikipedia](https://en.wikipedia.org/wiki/The_Hardest_Button_to_Button)
- [The Magic of Michel Gondry — Films Fatale](https://www.filmsfatale.com/blog/2021/5/7/the-magic-of-michel-gondry)
- [Weapon of Choice — Wikipedia](https://en.wikipedia.org/wiki/Weapon_of_Choice_(song))
- [The Evolution of Spike Jonze through every music video — No Film School](https://nofilmschool.com/spike-jonze-music-videos)
- [8 of Spike Jonze's best music videos — Far Out Magazine](https://faroutmagazine.co.uk/spike-jonze-8-best-music-videos/)
- [Behind the Scenes of Drake's *Hotline Bling* — Azure Magazine](https://www.azuremagazine.com/article/behind-the-scenes-of-drakes-hotline-bling/)
- [Director X on making *Hotline Bling* — Rolling Stone](https://www.rollingstone.com/music/music-features/director-x-on-making-drakes-dance-crazy-meme-ready-hotline-bling-74063/)
- [Director X on *Hotline Bling* — CBC News](https://www.cbc.ca/news/entertainment/hotline-bling-director-x-toronto-1.3376333)
- [Joseph Kahn (director) — Wikipedia](https://en.wikipedia.org/wiki/Joseph_Kahn_(director))
- [Bad Blood is literally an action movie — Film Industry Network](https://filmindustry.network/taylor-swift-bad-blood-music-video-is-an-action-movie/29054)
- [Joseph Kahn essential music videos — Billboard](https://www.billboard.com/music/awards/taylor-swift-bad-blood-director-joseph-kahn-essential-video-6561531/)
- [Dave Meyers — *Kendrick Lamar Humble*](https://davemeyers.com/featured/kendrick-lamar-humble/)
- [*HUMBLE.* — D&AD Awards archive](https://www.dandad.org/work/d-ad-awards-archive/humble)
- [Dave Meyers' best music videos — Uproxx](https://uproxx.com/music/dave-meyers-best-music-vidoes/)
- [Director Colin Tilley sets sights on movies — Variety](https://variety.com/2021/film/global/camerimage-colin-tilley-1235098665/)
- [Colin Tilley & Boy in the Castle interview — Billboard](https://assets.billboard.com/articles/columns/pop/9561276/colin-tilley-video-director-interview-justin-bieber-cardi-b)
- [WAP (song) — Wikipedia](https://en.wikipedia.org/wiki/WAP_(song))
- [Shin Woo-seok / Dolphiners — Cine21 interview thread](https://x.com/juantokki/status/1854102618513293609)
- [Shin Woo-seok on *Ditto* / *Infinite Challenge* similarity — Allkpop](https://www.allkpop.com/article/2022/12/dolphiners-film-ceo-shin-woo-seok-responds-to-the-similarities-between-newjeans-ditto-music-video-and-infinite-challenge)
- [Wooseok Shin filmography — IMDb](https://www.imdb.com/name/nm7115199/)
- [The Legacy of ZanyBros — Rolling Stone India](https://rollingstoneindia.com/the-legacy-of-zanybros/)
- [Zanybros — Wikipedia](https://en.wikipedia.org/wiki/Zanybros)
- [Zanybros interview — Korea Times](https://www.koreatimes.co.kr/www/art/2022/09/732_290771.html)
- [What it's really like to make K-pop MVs — Candy Magazine](https://www.candymag.com/all-access/videographer-k-pop-music-video-zanybros-a00306-20200715)
- [Why K-pop dance practice videos became popular — Korea Times](https://www.koreatimes.co.kr/www/nation/2021/01/732_302847.html)
- [K-pop's love affair with dance — Dance Magazine](https://dancemagazine.com/k-pop-dance/)
- [K-pop video maestros offer the full package — Korea Herald](https://m.koreaherald.com/article/2895451)
- [Mathy and Fran — Musicbed Blog](https://www.musicbed.com/articles/filmmaking/directing/the-art-of-the-music-video-a-conversation-with-mathy-and-fran/)
- [Radar: Directing Duo Mathy and Fran — LBBOnline](https://www.lbbonline.com/news/radar-directing-duo-mathy-and-fran)
- [Music Video Shot List Template — StudioBinder](https://www.studiobinder.com/templates/shot-list/music-video-shot-list-template/)
- [How to Storyboard a Music Video — Orphiq](https://orphiq.com/resources/music-video-storyboard-guide)
- [Shot Lists: What Are They — Audio Network](https://blog.audionetwork.com/the-edit/production/shot-list)
- [How to Storyboard a Music Video 2025 — Shai Creative](https://shaicreative.ai/how-to-storyboard-a-music-video-the-complete-2025-guide/)
- [Repetition, Pattern, Rhythm — IxDF](https://ixdf.org/literature/article/repetition-pattern-and-rhythm)
- [Pattern, repetition and rhythm, variety and unity — Khan Academy](https://www.khanacademy.org/humanities/ap-art-history/start-here-apah/principles-of-composition-apah/a/pattern-repetition-and-rhythm-variety-and-unity)
- [Repetition variation — Grokipedia](https://grokipedia.com/page/repetition_variation)
- [Eyeline Match — Filmmakers Academy](https://www.filmmakersacademy.com/glossary/eyeline-match/)
- [Eyeline match — Wikipedia](https://en.wikipedia.org/wiki/Eyeline_match)
- [How Important Is Eyeline Matching — StudioBinder](https://www.studiobinder.com/blog/what-is-an-eyeline-match/)
- [Negative space — Wikipedia](https://en.wikipedia.org/wiki/Negative_space)
- [Negative Space in Design — Design4Users](https://design4users.com/negative-space-in-design/)
- [Automated Conversion of Music Videos into Lyric Videos — arXiv 2308.14922](https://arxiv.org/pdf/2308.14922)
- [K-Pop Music Videos is My Architecture Escape — Archemist in the Making](https://www.archemistinthemaking.com/blog/kpopmusicvideos)
- [How K-pop Can Help You Find Your Next Colour Palette — Make It Pop](https://www.makeitpopgame.com/blog/2019/02/22/how-k-pop-can-help-you-find-your-next-colour-palette/)
- [Historical Styles in K-Pop Music Videos — Medium / Ronadine](https://ronadine.medium.com/ready-set-go-an-analysis-of-historical-styles-used-kpop-music-video-set-designs-35f18eadbde1)
- [Director X launches *Video Star* series — Variety](https://variety.com/2023/music/news/director-x-video-star-music-videos-interview-1235568666/)
