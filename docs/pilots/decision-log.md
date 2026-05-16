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
| Captions | whisper.cpp small → script-aware token alignment → libass burn-in, bottom-center safe zone |
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
| Mission id | `faceless-hittites-014312` (v4 windowed) | `faceless-hittites-ko-014703` (v4 windowed) |
| Voice | Kokoro `am_michael` | macOS `Yuna` |
| Duration | 62.8 s | 57.8 s |
| Size | 49 MB | 32 MB |
| Caption segments | 44 (5 auto-corrected) | 31 (12 auto-corrected) |
| Thumbnail | [`screens/hittites-en-caption-verify.jpg`](screens/hittites-en-caption-verify.jpg) | [`screens/hittites-ko-caption-verify.jpg`](screens/hittites-ko-caption-verify.jpg) |
| Script | [`screens/hittites-en-script.txt`](screens/hittites-en-script.txt) | [`screens/hittites-ko-script.txt`](screens/hittites-ko-script.txt) |
| Caption corrections | [`hittites-en-caption-corrections.log`](screens/hittites-en-caption-corrections.log) | [`hittites-ko-caption-corrections.log`](screens/hittites-ko-caption-corrections.log) |
| Per-window keywords | [`hittites-en-keywords.json`](screens/hittites-en-keywords.json) | [`hittites-ko-keywords.json`](screens/hittites-ko-keywords.json) |
| Upload metadata | [`upload-metadata/hittites.md`](upload-metadata/hittites.md) | _(EN only — KO would need its own draft pass)_ |
| Output path | `records/missions/2026-05-17/faceless-hittites-014312/outputs/short.mp4` | `records/missions/2026-05-17/faceless-hittites-ko-014703/outputs/short.mp4` |

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
| Mission id | `faceless-hydrogen-014508` (v4 windowed) | `faceless-hydrogen-ko-014816` (v4 windowed) |
| Voice | Kokoro `am_michael` | macOS `Yuna` |
| Duration | 63.7 s | 38.9 s |
| Size | 22 MB | 13 MB |
| Caption segments | 47 (2 auto-corrected) | 26 (8 auto-corrected) |
| Thumbnail | [`screens/hydrogen-en-caption-verify.jpg`](screens/hydrogen-en-caption-verify.jpg) | [`screens/hydrogen-ko-caption-verify.jpg`](screens/hydrogen-ko-caption-verify.jpg) |
| Script | [`screens/hydrogen-en-script.txt`](screens/hydrogen-en-script.txt) | [`screens/hydrogen-ko-script.txt`](screens/hydrogen-ko-script.txt) |
| Caption corrections | [`hydrogen-en-caption-corrections.log`](screens/hydrogen-en-caption-corrections.log) | [`hydrogen-ko-caption-corrections.log`](screens/hydrogen-ko-caption-corrections.log) |
| Per-window keywords | [`hydrogen-en-keywords.json`](screens/hydrogen-en-keywords.json) | [`hydrogen-ko-keywords.json`](screens/hydrogen-ko-keywords.json) |
| Upload metadata | [`upload-metadata/hydrogen.md`](upload-metadata/hydrogen.md) | _(EN only — KO would need its own draft pass)_ |
| Output path | `records/missions/2026-05-17/faceless-hydrogen-014508/outputs/short.mp4` | `records/missions/2026-05-17/faceless-hydrogen-ko-014816/outputs/short.mp4` |

**Topic**: _Hydrogen — 75 percent of the universe and 10 percent of your body._

**Production notes**

- This pilot's narration drifts across runs on the body-percentage figure (10 % by mass vs 60 % by atom count — both are scientifically valid).  Model temperature effect, not a pipeline bug.
- v4 window 5 in KO landed `sugar bottle` for the caption "약 1킬로그램, 큰 설탕 한 봉지 정도" — the exact metaphor in the narration.  Window 2 → `water molecule structure` for "물 분자의 약 3분의 2".  Per-window keyword extraction picks up these literal phrasings reliably.
- Yuna voice handles 외래어 transliteration cleanly (탄수화물, 단백질, 킬로그램); the small-model whisper drift was mostly punctuation + number-format restoration (`10%` → `10퍼센트`, `1kg` → `1킬로그램,`).
- EN and KO no longer share visuals in v4 — see Hittites notes above for the rationale.

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

## Cost accounting

Per-pilot marginal cost: **$0** (Pexels free tier within quota, all other stages local). Time per pilot: ~2 minutes wall-clock on Apple M2; Korean variants ~50 seconds since B-roll is reused. The Pexels free quota (200 req/hr, 20k req/month) limits this pipeline to ~3300 pilots per month before any cost — well past any realistic upload cadence.
