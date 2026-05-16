# Faceless pilots — A/B decision log

End-to-end shorts produced by `agents/missions/faceless-short/run.sh` against the local stack (Ollama llama3.2:3b → Kokoro-ONNX → whisper.cpp small → Pexels B-roll → ffmpeg+libass). $0 marginal cost. Pilots stay in `records/missions/...` (gitignored, ~13–20 MB each); only thumbnails + scripts live in this folder so the repo stays light.

## Format spec (held constant across pilots)

| Field | Value |
|---|---|
| Aspect / size | 1080×1920, 30 fps, H.264 + AAC |
| Length target | ~60 s (130–160 words at conversational pace) |
| Voice | Kokoro `am_michael` (English documentary tone) |
| Captions | whisper.cpp small → script-aware token alignment → libass burn-in, bottom-center safe zone |
| B-roll | 6 clips × ~9.5 s each, Pexels License (commercial-OK) |
| Attribution | Always-on top-left drawtext overlay |

The pipeline is deterministic given the same topic prompt — repro any pilot with:

```bash
./agents/missions/faceless-short/run.sh <id> "<topic_prompt>"
```

## Pilots

### Pilot 1 — Hittites (history × Bible)

| | |
|---|---|
| Mission id | `faceless-hittites-233021` (v2 after script-aware caption correction) |
| Topic prompt | _The Hittites — a biblical kingdom dismissed by historians as legend until 1906_ |
| Output | `records/missions/2026-05-16/faceless-hittites-233021/outputs/short.mp4` |
| Duration | 55.8 s |
| Size | 21 MB |
| Script words | 142 |
| Caption segments | 21 (5 auto-corrected against source script) |
| Thumbnail | [`screens/hittites-caption-verify.jpg`](screens/hittites-caption-verify.jpg) |
| Script | [`screens/hittites-script.txt`](screens/hittites-script.txt) |
| Caption corrections | [`screens/hittites-caption-corrections.log`](screens/hittites-caption-corrections.log) |

**Production notes**

- B-roll relevance improved markedly on the v2 LLM keyword pass — search terms `Hittite capital ruins`, `Cuneiform script tablets`, `Ancient Hittite cityscape`, `German archaeologist digging` returned thematically tight stock. The v1 run's failure ("Battle of Kadesh" → costumed Latin American reenactment) was driven by the LLM picking named historical events over abstract visual hooks; rerolling produced sharper terms organically.
- Whisper small's proper-noun drift is now fully handled by [`scripts/correct-captions.py`](../../scripts/correct-captions.py), which runs after whisper and aligns whisper tokens to the source script (whisper provides TIMING, script provides TEXT — script is ground truth because we synthesized the audio from it). v2 corrections logged: `Hadusa` → `Hattusa`, `Winkler` → `Winckler`, `Sipululiumii.` → `Suppiluliuma I.`, plus 2 spelling normalizations (`archeological` → `archaeological`).

**Platform results** _(fill in after upload)_

| Platform | URL | Views | Watch-time | CTR | Likes | Comments | Saves |
|---|---|---|---|---|---|---|---|
| YouTube Shorts | _pending_ | | | | | | |
| TikTok | _pending_ | | | | | | |
| Instagram Reels | _pending_ | | | | | | |

---

### Pilot 2 — Hydrogen (science)

| | |
|---|---|
| Mission id | `faceless-hydrogen-233219` (v2 after script-aware caption correction) |
| Topic prompt | _Hydrogen — 75 percent of the universe and 10 percent of your body_ |
| Output | `records/missions/2026-05-16/faceless-hydrogen-233219/outputs/short.mp4` |
| Duration | 51.0 s |
| Size | 15 MB |
| Script words | 129 |
| Caption segments | 18 (4 auto-corrected against source script) |
| Thumbnail | [`screens/hydrogen-caption-verify.jpg`](screens/hydrogen-caption-verify.jpg) |
| Script | [`screens/hydrogen-script.txt`](screens/hydrogen-script.txt) |
| Caption corrections | [`screens/hydrogen-caption-corrections.log`](screens/hydrogen-caption-corrections.log) |

**Production notes**

- B-roll fit stays strong on science vocabulary — `hydrogen atoms`, `water molecules`, `human tissue cells`, `protein structures`, `carbohydrate bonds` all returned thematically accurate stock. Science vocabulary has dense coverage on stock-footage sites; named historical events do not (the v1 history pilot exposed that asymmetry).
- Caption corrections logged: `75%` → `75 percent` and `10%` → `10 percent` (script renders `percent` long-form for clarity; whisper transcribed the spoken word as `%`), plus an em-dash restoration around `H2O` and a period removal.
- The narration claim "10 percent of your body" is slightly off (hydrogen is ~10% by mass, ~63% by atom count) — the topic prompt phrasing led the LLM to commit to the mass figure. Acceptable as written but flag for future science pilots: pre-cite the framing in the topic prompt so the model doesn't drift.

**Platform results** _(fill in after upload)_

| Platform | URL | Views | Watch-time | CTR | Likes | Comments | Saves |
|---|---|---|---|---|---|---|---|
| YouTube Shorts | _pending_ | | | | | | |
| TikTok | _pending_ | | | | | | |
| Instagram Reels | _pending_ | | | | | | |

---

## Comparison frame — what we're learning from A/B

| Dimension | Hittites (history+Bible) | Hydrogen (science) |
|---|---|---|
| B-roll relevance | Weak — named events have poor stock coverage | Strong — molecular/biological visuals plentiful |
| Caption accuracy | Proper-noun-heavy → relies more on the correction pass (5 fixes per pilot) | Mostly clean (4 fixes, mostly punctuation/numbers) |
| Global appeal hypothesis | Region-skewed (Bible-belt + Mediterranean Christian audiences) | Global — universal science curiosity |
| Repeatability | Each topic needs new historical research | Periodic-element / molecule format generalizes to 100+ topics |
| Risk | Religious/historical framing can attract debate | Lower-stakes; correctness is the only risk |

**Decision to be made (after platform data lands):**
1. Single-niche commit (history+Bible OR science) and scale that channel to 30 uploads.
2. Dual-channel run, comparing growth velocity at the 30-upload mark.
3. Iterate the format itself (different voice, faster cuts, on-screen text instead of burned captions, etc.) before scaling either niche.

Default plan if no platform data yet: ship 3 more of each niche to amortize the "first upload always underperforms" effect before deciding.

## Cost accounting

Per-pilot marginal cost: **$0** (Pexels free tier within quota, all other stages local). Time per pilot: ~2 minutes wall-clock on Apple M2. The Pexels free quota (200 req/hr, 20k req/month) limits this pipeline to ~3300 pilots per month before any cost — well past any realistic upload cadence.
