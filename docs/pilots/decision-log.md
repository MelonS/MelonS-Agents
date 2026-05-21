# Faceless pilots — A/B decision log

End-to-end shorts produced by `agents/missions/faceless-short/run.sh` against the local stack (Ollama llama3.2:3b → Kokoro-ONNX / macOS Yuna → whisper.cpp small → Pexels B-roll → ffmpeg + libass). $0 marginal cost. Pilots stay in `records/missions/...` (gitignored, ~12–42 MB each); only thumbnails + scripts live in this folder so the repo stays light.

Each topic is rendered in two language variants so the operator can compare voice + caption rendering side by side using identical B-roll:
- **EN** (English) — Kokoro-ONNX `am_michael` voice + English captions
- **KO** (Korean) — macOS `Yuna` voice + Korean captions, AppleGothic font

## Format spec (held constant across pilots)

| Field | Value |
|---|---|
| Aspect / size | 1080×1920, 30 fps, H.264 + AAC |
| Length target | ~60 s (130–160 EN words / ~300 KO chars) |
| Framing | **Screen-fill 9:16** — center-crop, no letterbox-blur (TikTok/Reels style) |
| Captions | whisper.cpp small → script-aware token alignment → **single-line enforcement** (cues > 28 chars split at natural punctuation, `scripts/split-long-captions.py`) → libass burn-in, bottom-center safe zone |
| B-roll | 8 clips with **per-window keyword extraction** — caption SRT is grouped into 8 temporal windows, each window's text generates its own search term so the clip on screen matches what's being said.  Variable per-clip durations matching the natural narration beats.  Pexels License (commercial-OK) |
| Attribution | Always-on top-left drawtext overlay |

The pipeline is deterministic given the same topic prompt + script. Repro any pilot with:

```bash
./agents/missions/faceless-short/run.sh <id> "<topic_prompt>"
```

Localized variant (same content, different language, reuses B-roll):

```bash
FACELESS_VOICE=Yuna WHISPER_LANG=ko \
  FACELESS_SCRIPT_OVERRIDE=<korean_script.txt> \
  FACELESS_REUSE_BROLL=<en_mission_dir> \
  TTS_LABEL="macOS Yuna TTS" \
  ./agents/missions/faceless-short/run.sh <id-ko> "<topic in any language>"
```

Ready-to-paste upload copy is in [`upload-metadata/`](upload-metadata/) — one markdown file per pilot. Run `./scripts/gen-upload-metadata.sh <mission_dir>` to regenerate against a mission.

## Pilots

### Pilot 1 — Hittites (history × Bible)

|  | EN | KO |
|---|---|---|
| Mission id | `faceless-hittites-032538` (v5: single-line captions) | `faceless-hittites-ko-032653` (v5) |
| Voice | Kokoro `am_michael` | macOS `Yuna` |
| Duration | 62.7 s | 60.3 s |
| Size | 49 MB | 35 MB |
| Caption cues (post-split) | 32 (from 18 split) | 23 (from 10 split) |
| Thumbnail | [`screens/hittites-en-caption-verify.jpg`](screens/hittites-en-caption-verify.jpg) | [`screens/hittites-ko-caption-verify.jpg`](screens/hittites-ko-caption-verify.jpg) |
| Script | [`screens/hittites-en-script.txt`](screens/hittites-en-script.txt) | [`screens/hittites-ko-script.txt`](screens/hittites-ko-script.txt) |
| Caption corrections | [`hittites-en-caption-corrections.log`](screens/hittites-en-caption-corrections.log) | [`hittites-ko-caption-corrections.log`](screens/hittites-ko-caption-corrections.log) |
| Per-window keywords | [`hittites-en-keywords.json`](screens/hittites-en-keywords.json) | [`hittites-ko-keywords.json`](screens/hittites-ko-keywords.json) |
| Upload metadata | [`upload-metadata/hittites.md`](upload-metadata/hittites.md) | _(EN only — KO would need its own draft pass)_ |
| Output path | `records/missions/2026-05-17/faceless-hittites-032538/outputs/short.mp4` | `records/missions/2026-05-17/faceless-hittites-ko-032653/outputs/short.mp4` |

**Topic**: _The Hittites — a biblical kingdom dismissed by historians as legend until 1906._

**Production notes**

- The Korean script is a manual translation of the English script (llama3.2:3b's translation output was unusable — mixed Hindi/Thai/Russian script and topic-confusion across prompts). A 7B+ instruct model is the right path for automated Korean translation later.
- v4 swaps the previous "6 equal slots driven by global keyword extraction" for "8 temporal windows, one keyword per window."  Each window's keyword is generated from the local caption text only, so the clip on screen matches what's being said.  EN window 3 ("ancient Anatolia, military") → `chariots in battle`; window 6 ("Treaty of Kadesh") → `Treaty of Kadesh map`; KO window 4 ("이집트 양식이 어우러진") → `Mesopotamian architecture`; KO window 5 ("무와탈리 2세") → `Muwatalli II portrait`.  Topical fit is dramatically tighter than v3.
- EN and KO no longer share visuals — each language extracts its own keywords from its own captions, so context-fit takes precedence over visual-equality A/B.  (Visual-equality variant still available via `FACELESS_REUSE_BROLL=<en_mission_dir>` env if the previous "same visuals, swapped audio" comparison is wanted again.)
- whisper.cpp small drifts more on Korean (12 corrections out of 31 cues — proper nouns like `하투샤`, `빙클러`, `무와탈리`, `수수께끼` all needed fixing) than on English (5/44). Without `scripts/correct-captions.py` the Korean captions would be heavily wrong.

---

### Pilot 2 — Hydrogen (science)

|  | EN | KO |
|---|---|---|
| Mission id | `faceless-hydrogen-032742` (v5: single-line captions) | `faceless-hydrogen-ko-032846` (v5) |
| Voice | Kokoro `am_michael` | macOS `Yuna` |
| Duration | 59.7 s | 38.9 s |
| Size | 21 MB | 14 MB |
| Caption cues (post-split) | 34 (from 11 split) | 16 (from 6 split) |
| Thumbnail | [`screens/hydrogen-en-caption-verify.jpg`](screens/hydrogen-en-caption-verify.jpg) | [`screens/hydrogen-ko-caption-verify.jpg`](screens/hydrogen-ko-caption-verify.jpg) |
| Script | [`screens/hydrogen-en-script.txt`](screens/hydrogen-en-script.txt) | [`screens/hydrogen-ko-script.txt`](screens/hydrogen-ko-script.txt) |
| Caption corrections | [`hydrogen-en-caption-corrections.log`](screens/hydrogen-en-caption-corrections.log) | [`hydrogen-ko-caption-corrections.log`](screens/hydrogen-ko-caption-corrections.log) |
| Per-window keywords | [`hydrogen-en-keywords.json`](screens/hydrogen-en-keywords.json) | [`hydrogen-ko-keywords.json`](screens/hydrogen-ko-keywords.json) |
| Upload metadata | [`upload-metadata/hydrogen.md`](upload-metadata/hydrogen.md) | _(EN only — KO would need its own draft pass)_ |
| Output path | `records/missions/2026-05-17/faceless-hydrogen-032742/outputs/short.mp4` | `records/missions/2026-05-17/faceless-hydrogen-ko-032846/outputs/short.mp4` |

**Topic**: _Hydrogen — 75 percent of the universe and 10 percent of your body._

**Production notes**

- This pilot's narration drifts across runs on the body-percentage figure (10 % by mass vs 60 % by atom count — both are scientifically valid).  Model temperature effect, not a pipeline bug.
- v4 window 5 in KO landed `sugar bottle` for the caption "약 1킬로그램, 큰 설탕 한 봉지 정도" — the exact metaphor in the narration.  Window 2 → `water molecule structure` for "물 분자의 약 3분의 2".  Per-window keyword extraction picks up these literal phrasings reliably.
- Yuna voice handles 외래어 transliteration cleanly (탄수화물, 단백질, 킬로그램); the small-model whisper drift was mostly punctuation + number-format restoration (`10%` → `10퍼센트`, `1kg` → `1킬로그램,`).
- EN and KO no longer share visuals in v4 — see Hittites notes above for the rationale.

---

## Sonnet trial (v6) — script-quality benchmark

After watching the v5 pilots the operator flagged a script-quality
ceiling: weak hooks, encyclopedia-style flat prose, factual mixing
(hydrogen "10% of body" stated then atom-count figures later
without disambiguation — viewer confusion).  The pipeline's
**script-generation stage** was rerouted from `llama3.2:3b` (Tier 2)
to Claude Sonnet (Tier 1, Max-plan subscription quota) via
`scripts/gen-script-claude.sh`.  All other pipeline stages remain
Tier-2 unchanged.

Four v6 mp4s rendered with identical pipeline downstream of the
script swap.  Side-by-side hooks, full scripts, per-window keywords,
and caption-verify thumbnails live in
[`docs/pilots/sonnet-trial/`](sonnet-trial/) — see that page's
README for the full trial writeup.

Key observable differences vs v5:
- **Hooks**: Sonnet rejects "What if…" / "Did you know…" openings
  and lands on specific-number bare statements ("Scholars called
  the Hittites fiction", "63 of every 100 atoms in your body is
  hydrogen") that earn the next 5 seconds.
- **Factual coherence**: hydrogen script picks one frame (atom
  count) and stays in it — the "10% vs 60% 헷갈리네" failure mode
  is eliminated.
- **B-roll keyword quality**: Sonnet's specific-detail prose feeds
  cleaner per-window keyword extraction (Hittites EN windows landed
  `Turkish village excavation`, `cuneiform tablets`, `chariots at
  battle`, `Ramesses II treaty`, `Hittite capital ruins`).

Cost: ~2,800 tokens total across the 4 scripts (Sonnet) — under
1% of weekly Max-plan quota.  Money-firewall not triggered;
subscription quota usage, not new paid resource.

Architectural lesson captured in
[`docs/cost-model.md`](../cost-model.md#when-tier-2-is-the-wrong-default--creative-stages):
Tier-2-as-default applies to mechanical / high-volume stages.
One-shot creative stages (script hook) route to Tier-1 because
quality compounds and per-call cost is bounded.

**Operator pick still needed** — Sonnet routing is orthogonal to
niche choice.  The trial supports decision-making but does not
substitute for the niche pick.

---

## Music trial — niche-fit explored via operator-domain topics

After v6, the operator's read on Hittites + Hydrogen was honest:
"도저히 판단이 안됨, 둘 다 다큐멘터리 톤이라 비교 안 됨."  The pair
was too tonally similar to test niche fit.  They pivoted to music
(their professional domain — audio + generative vocal models —
which means they can fact-check the output directly).

Four music topics, two languages each = 8 new pilots through the
same Sonnet-script + local-everything-else v6 pipeline.  Full
writeup: [`music-trial/README.md`](music-trial/README.md).

**Highest score in the entire scorecard so far**: AutoTune EN at
**45 / 50** ("AutoTune was invented to find oil." → 1996 Andy
Hildebrand → 1998 Cher's Believe → T-Pain).  Clean cause-and-effect
arc, exceptional hook, factually clean.  Suggests if the operator
picks music, the strongest entry-point format is the **surprising
origin story** structure — applies equally to Moog synthesizer
history, 808 drum machine, MIDI invention, etc.

Other observations:
- **Earworms** at 43/50 — strong universal-experience anchor ("98%
  of people get a song stuck involuntarily once a week") but B-roll
  is necessarily abstract (Pexels has no "song-stuck-in-head"
  imagery; falls back to brain visuals + headphones).
- **AI Music** at 42/50 — operator's domain.  Strong recency
  (Suno/Udio launched 2024, lawsuits June 2024) but Pexels has no
  Suno-specific or lawsuit-specific imagery; falls back to generic
  studio shots.
- **Hatsune Miku** at 41/50 — exceptional hook ("She fills stadiums.
  She has no vocal cords.") but lowest visual specificity in the
  trial (no licensed Miku imagery on Pexels → dark concert
  silhouettes + abstract holographic projections).

**Operator pick still gates goal completion**, but the trial gives
two new data points: (1) music niche is fully feasible at the
current quality level; (2) within music, "surprising origin story"
beats "broad explainer" — AutoTune-style topics will outperform
abstract phenomenon explainers like Earworms in the same pipeline.

---

## Comparison frame — what we're learning from A/B

| Dimension | Hittites (history × Bible) | Hydrogen (science) |
|---|---|---|
| B-roll relevance | Improved on the v3 reroll — Egyptian-style wall carvings, archaeological dig | Strong — water droplet macros + DNA helix visualizations |
| Caption accuracy after correction | Excellent in both EN and KO (proper nouns fixed) | Clean in EN; KO needed more punctuation/number restoration |
| Global appeal | Region-skewed by topic (Mediterranean / Christian / Mediterranean Studies audience) | Globally appealing |
| Repeatability | Each topic needs new historical research | Periodic-element / molecule format generalizes to 100+ topics |
| Risk | Religious/historical framing can attract debate | Lower-stakes; correctness is the only risk |
| Voice impression (EN) | Kokoro `am_michael` documentary calm — fits topic | Same voice, same fit |
| Voice impression (KO) | Yuna naturalness lower than Kokoro EN — system TTS still — but glyph-clean | Same |

**Decision to be made (after viewing all four):**
1. Single-niche commit (history × Bible OR science) and scale that channel to 30 uploads.
2. Dual-channel run, comparing growth velocity at the 30-upload mark.
3. Iterate the format itself (different voice, faster cuts, on-screen text instead of burned captions, etc.) before scaling either niche.
4. Iterate the language: commit to EN-only first (Kokoro voice quality is higher), or run EN + KO parallel channels.

Default plan if no platform data yet: ship 3 more of each niche to amortize the "first upload always underperforms" effect before deciding. The next-5 queue lives at [`topic-backlog.md`](topic-backlog.md).

## Operator pick — 2026-05-17

After viewing the four `faceless-short` pilots and the eight music-trial
pilots, the operator pivoted away from all narration-driven formats above.

**Pick**: format option 3 from the list — **iterate the format itself**.
Specifically, ship a **music-video mode** that is qualitatively different from
faceless-short: music as sole audio, no narration, no captions, B-roll
selected by music mood + cut-aligned to phrase boundaries + micro-glitch
edits on detected drum onsets.

**Why this beat the other options:**

The original Hittites-vs-Hydrogen frame asked "which topic survives" given a
fixed format.  The music-trial extension ("음악 들어간 쇼츠") clarified that
the operator's interest was not _topics about music_ but _shorts that
contain music_ — a format question, not a topic question.  After producing
five music-video prototypes (v1 → v5, all on Velvet Turntable lo-fi
instrumental), the operator confirmed v5 was a validated baseline.

**What the music-video mode is, concretely:**

| Stage | Implementation |
|---|---|
| Audio | Operator-curated music file (free / Suno / own).  Sole audio track. |
| Beat extraction | `aubiotrack` filtered to real beats (sub-beat intervals < 0.4 s rejected) |
| Cut placement | Every Nth real beat (default N=12 — about one cut every 7.5 s at 95 BPM) → "phrase boundary" cuts |
| Visual sourcing | Pexels portrait (9:16) per mood keyword |
| Per-clip speed | `setpts` filter — slow scenes (reading, coffee) 0.55×; ambient (rain, lights) 0.70×; active (city) 0.80×; static-natural (turntable, table) 1.00× |
| Motif | First keyword reused at every 3rd segment; same Pexels clip cached + re-entered at varied in-points |
| Glitch | At one detected `aubioonset -O complex -t 2.0` drum hit inside each static-camera, non-motif, non-tail segment.  Reverse 0.20 s + forward jump-cut 0.20 s.  Audio untouched. |
| Caption / overlay | None.  Music is the message. |

**Mission script**: [`agents/missions/music-video/run.sh`](../../agents/missions/music-video/run.sh).
Usage:

```bash
agents/missions/music-video/run.sh <short_id> <music_file> [keywords_csv]
```

The Velvet Turntable1 + lo-fi/cafe keyword set is the validated default.
Other moods will need their own keyword pools — the script auto-classifies
each keyword's expected motion + speed via a heuristic in `classify_kw()`.

**What did NOT carry forward** from the faceless-short pilots:

- The Sonnet narration script generator (`scripts/gen-script-claude.sh`)
  and content-quality scorer (`scripts/score-content.sh`) are not used in
  music-video mode.  They remain alive for any future narration-driven
  mission types.
- The scorecard ([`scorecard.md`](scorecard.md)) measures dimensions tied
  to narration (hook + factual coherence).  Music-video output needs a
  new scorecard with different axes (mood-fit, beat-sync quality,
  motif memorability) — not yet implemented.
- The eight music-trial mp4s (autotune / earworms / aimusic / miku × EN/KO)
  remain in records as historical baseline.  They are not the production
  format; the niche-pivot replaces them.

**Next decisions explicitly deferred:**

- Sticker / effect overlays (film grain, vignette pulses, emoji icons) —
  exploratory, will iterate after a first production batch lands.
- Real platform watch-time data — production batch must ship to YouTube
  Shorts first; the music-video mode hasn't been platform-tested yet.
- Whether to keep faceless-short alive as a parallel narration channel or
  retire it.  Default: keep, low maintenance burden.

## Cost accounting

Per-pilot marginal cost: **$0** (Pexels free tier within quota, all other stages local). Time per pilot: ~2 minutes wall-clock on Apple M2; Korean variants ~50 seconds since B-roll is reused. The Pexels free quota (200 req/hr, 20k req/month) limits this pipeline to ~3300 pilots per month before any cost — well past any realistic upload cadence.

---

## TikTok automation — deferred (2026-05-22)

After landing YT Data API automation end-to-end (12 vocal demos
uploaded + scheduled via `scripts/yt-batch-upload.sh`, see commits
`a989eb2` and follow-ons), the obvious next move was the TikTok
equivalent.  Spent ~45 minutes evaluating TikTok's Content Posting
API path and walked it back.  What we found:

**Friction TikTok API imposes that YT does not:**

- Mandatory **URL verification** — Privacy Policy URL and Terms of
  Service URL must point to a domain whose ownership can be verified
  (meta-tag or DNS TXT record).  `github.com` is shared so the form
  surfaced "This URL is not verified" as a hard label.
- Mandatory **Web/Desktop URL** field assuming the app has a public
  website.  A 1-creator CLI tool isn't an "app" in TikTok's mental
  model — their API was designed for 3rd-party integrations (e.g.,
  social-network connectors), not for one creator automating their
  own channel.
- **Human review** for production scope: 2-14 days typical, possibly
  with rejection + resubmit cycles.  No auto-approval path for
  personal use.
- A **Sandbox** mode is offered for pre-approval testing but only
  publishes to test accounts, not the real ToddStudio channel.

**What we built and then removed:**

- `docs/legal/PRIVACY.md` and `docs/legal/TERMS.md` — minimal
  privacy + ToS pages drafted to populate the TikTok form's URL
  fields.  Committed as `c5a3619` then reverted as `40240aa` when
  the verification gate made the URL approach moot.
- A TikTok-for-Developers app draft (`ToddStudio Upload`) — created
  in TikTok's console up to the URL-fields step.  Operator chose to
  abandon rather than buy a domain to clear verification.

**Alternative chosen:**

- **TikTok Web's native scheduler** (`tiktok.com/upload`).  10-day
  schedule cap, ~30 s manual upload per video, no API approval, no
  domain cost, no maintenance burden.  For a single creator running
  ~2 videos/day this is competitive with API automation: 6 minutes
  per batch of 12 vs the 2-week wait + ongoing rejection risk of the
  API path.
- A cheat sheet at `~/Desktop/tiktok-upload-cheatsheet.md` (operator-
  local, not committed) holds the 11 captions + schedule slots so
  the manual upload session is mechanical.

**Reconsideration conditions (when TikTok automation becomes worth
the friction):**

1. YT performance data (1-2 weeks of vocal-pivot uploads, see the
   2026-05-21 demo batch) shows the vocal channel pulls non-trivial
   reach.  If it doesn't, TikTok automation is moot.
2. Cadence increases past ~5 videos/day per channel where manual
   click effort becomes meaningful (currently 2/day).
3. The operator acquires a real domain for other reasons (portfolio
   site, channel hub), at which point URL verification is a free
   side-effect.

Until at least one of those holds, the manual web path stays the
default and this section stands as a record so we don't re-explore
the same dead ends.

**Reusable artifacts kept:**

- The genre presets, full-length flag, lyrics overlay script, and CPU
  throttle — these benefit any output format and aren't TikTok-
  specific.  Stays.
- The 12 staged vocal demos (`outputs/demos/2026-05-21-genre-shader-
  experiments/`) — uploaded to YT, also feed the manual TikTok path.
  Stays.
