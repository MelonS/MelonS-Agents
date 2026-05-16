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
| B-roll | 6 clips × ~6–11 s each, Pexels License (commercial-OK) |
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
| Mission id | `faceless-hittites-000112` | `faceless-hittites-ko-000654` |
| Voice | Kokoro `am_michael` | macOS `Yuna` |
| Duration | 55.2 s | 52.9 s |
| Size | 42 MB | 40 MB |
| Caption segments | 26 (6 auto-corrected) | 15 (12 auto-corrected) |
| Thumbnail | [`screens/hittites-en-caption-verify.jpg`](screens/hittites-en-caption-verify.jpg) | [`screens/hittites-ko-caption-verify.jpg`](screens/hittites-ko-caption-verify.jpg) |
| Script | [`screens/hittites-en-script.txt`](screens/hittites-en-script.txt) | [`screens/hittites-ko-script.txt`](screens/hittites-ko-script.txt) |
| Caption corrections | [`hittites-en-caption-corrections.log`](screens/hittites-en-caption-corrections.log) | [`hittites-ko-caption-corrections.log`](screens/hittites-ko-caption-corrections.log) |
| Upload metadata | [`upload-metadata/hittites.md`](upload-metadata/hittites.md) | _(EN only — KO would need its own draft pass)_ |
| Output path | `records/missions/2026-05-17/faceless-hittites-000112/outputs/short.mp4` | `records/missions/2026-05-17/faceless-hittites-ko-000654/outputs/short.mp4` |

**Topic**: _The Hittites — a biblical kingdom dismissed by historians as legend until 1906._

**Production notes**

- The Korean script is a manual translation of the English script (llama3.2:3b's translation output was unusable — mixed Hindi/Thai/Russian script and topic-confusion across prompts). A 7B+ instruct model is the right path for automated Korean translation later.
- B-roll matches across EN/KO since the Korean variant uses `FACELESS_REUSE_BROLL` to copy the English pilot's stitched footage. The A/B is purely audio + captions.
- whisper.cpp small drifts more on Korean (12 corrections out of 15 cues — proper nouns like `하투샤`, `빙클러`, `무와탈리`, `수수께끼` all needed fixing) than on English (6/26). Without `scripts/correct-captions.py` the Korean captions would be heavily wrong.

---

### Pilot 2 — Hydrogen (science)

|  | EN | KO |
|---|---|---|
| Mission id | `faceless-hydrogen-000112` | `faceless-hydrogen-ko-000755` |
| Voice | Kokoro `am_michael` | macOS `Yuna` |
| Duration | 38.5 s | 38.9 s |
| Size | 12 MB | 12 MB |
| Caption segments | 24 (3 auto-corrected) | 10 (8 auto-corrected) |
| Thumbnail | [`screens/hydrogen-en-caption-verify.jpg`](screens/hydrogen-en-caption-verify.jpg) | [`screens/hydrogen-ko-caption-verify.jpg`](screens/hydrogen-ko-caption-verify.jpg) |
| Script | [`screens/hydrogen-en-script.txt`](screens/hydrogen-en-script.txt) | [`screens/hydrogen-ko-script.txt`](screens/hydrogen-ko-script.txt) |
| Caption corrections | [`hydrogen-en-caption-corrections.log`](screens/hydrogen-en-caption-corrections.log) | [`hydrogen-ko-caption-corrections.log`](screens/hydrogen-ko-caption-corrections.log) |
| Upload metadata | [`upload-metadata/hydrogen.md`](upload-metadata/hydrogen.md) | _(EN only — KO would need its own draft pass)_ |
| Output path | `records/missions/2026-05-17/faceless-hydrogen-000112/outputs/short.mp4` | `records/missions/2026-05-17/faceless-hydrogen-ko-000755/outputs/short.mp4` |

**Topic**: _Hydrogen — 75 percent of the universe and 10 percent of your body._

**Production notes**

- This pilot's narration drifted on the body-percentage figure: the v3 EN narration says "60 percent hydrogen by weight" while v2 said "10 percent." Both are scientifically valid (hydrogen is ~10 % by mass, ~60 % by atom count, ~63 % by atom count if you include all body water); the inconsistency is a model temperature effect, not a pipeline bug.
- Korean B-roll matches EN exactly (water droplet macros) via `FACELESS_REUSE_BROLL`.
- Yuna voice handles 외래어 transliteration cleanly (탄수화물, 단백질, 킬로그램); the small-model whisper drift was mostly punctuation + number-format restoration (`10%` → `10퍼센트`, `1kg` → `1킬로그램,`).

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
