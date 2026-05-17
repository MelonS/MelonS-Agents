# Pilot scorecard — self-evaluation across versions

This file is the **objective evolution signal** that thumbnails alone don't carry.  Each pilot version (v4 / v5 / v6) is scored across five dimensions that map to short-form viewer retention.  Source-of-truth for the data:
[`scorecard.json`](scorecard.json) (machine-readable) drives
[`scripts/generate-scorecard-chart.py`](../../scripts/generate-scorecard-chart.py) → `docs/metrics/scorecard.png`.

Honest disclosure: scores are **assigned by Claude**, not by the
operator or external viewers.  They are a self-evaluation, not a
viewer-retention measurement.  When the project gets to actual
upload + platform performance data (post-niche-pick), those numbers
replace these.

## Five dimensions, why each

| Dimension | What it captures | Why it maps to retention |
|---|---|---|
| Hook strength | Whether the first ~5 seconds of narration earn the next 5 | Shorts viewers swipe in 1.5 s; the hook is the entire bottleneck for getting past the first beat |
| Visual ↔ caption sync | Whether the B-roll on screen matches the words being spoken at that moment | Mismatch costs immediate trust ("why is this clip showing while they say that?") |
| Caption readability | Single-line cues, no opaque-box overlap, sized to fit the safe zone | Most shorts are watched muted on mobile — captions ARE the audio |
| Factual coherence | Whether the script's claims are internally consistent and frame-stable (no '10% vs 63%' confusion in one breath) | One viewer-detected contradiction triggers a skip |
| Production polish | TTS prosody, screen-fill (no letterbox bars), attribution legibility, render artifacts | Cumulative; one weak axis drags the whole watchability down |

## Hittites EN — v4 → v5 → v6

| Dimension | v4 (`014312`) | v5 (`032538`) | v6 (`v6-071919`) |
|---|---|---|---|
| Hook strength | 3 — `"What if the biblical account…"` (abstract, forbidden pattern) | 3 — same script as v4 | **9** — `"Scholars called the Hittites fiction"` (bare statement, immediate conflict) |
| Visual ↔ caption sync | 6 — per-window keywords landed mostly OK; some generic clips | 6 — same B-roll as v4 (reused via `FACELESS_REUSE_BROLL`) | **9** — Sonnet's specific prose (1906, 10,000 tablets, 3,500 chariots, Kadesh, Ramesses II) feeds clean keyword extraction; each window has a concrete visual target |
| Caption readability | 4 — 2-line cues overlapped opaque boxes (operator flagged: "겹치면 너무 이상해보임") | 9 — single-line split via `split-long-captions.py` (`61fac70`) | 9 — same single-line pipeline |
| Factual coherence | 6 — `Tawagalawa` cited as Hittite diplomat (he was Mycenaean; small-model hallucination) | 6 — same script | **9** — Sonnet stays factually clean; cites real names + years |
| Production polish | 7 — screen-fill correct, attribution legible, TTS clean | 8 — caption polish lifts the whole frame | 8 — same baseline |
| **Total** | **26 / 50** | **32 / 50** | **44 / 50** |

## Hydrogen EN — v5 → v6

| Dimension | v5 (`032742`) | v6 (`v6-072159`) |
|---|---|---|
| Hook strength | 3 — `"What if hydrogen, the lightest…"` | **9** — `"63 of every 100 atoms in your body is hydrogen"` (specific number, immediate) |
| Visual ↔ caption sync | 5 — sometimes good (water droplet, DNA helix) sometimes generic | 8 — burning matter at "620 million tons", stellar fusion visuals match |
| Caption readability | 9 — single-line | 9 — same |
| Factual coherence | **3** — 10% vs 63% framework confusion (mass vs atom count mixed in one breath; operator caught this: "10%인지 60%인지 헷갈리네") | **9** — Sonnet picks atom-count frame and holds it across the whole script |
| Production polish | 8 | 8 |
| **Total** | **28 / 50** | **43 / 50** |

## What changed at each version boundary

- **v4 → v5**: caption split landed.  Lifted readability +5 but didn't touch script.  Best-case use of free-only quality.
- **v5 → v6**: script-generation stage routed from `llama3.2:3b` (Tier 2) to Claude Sonnet (Tier 1, Max-plan quota).  Lifted Hook +6, Coherence +3 (Hydrogen +6), Visual sync +3.  See [`sonnet-trial/README.md`](sonnet-trial/README.md) and the
  [`cost-model.md` routing rule](../cost-model.md#when-tier-2-is-the-wrong-default--creative-stages).

## Honest gaps

- **TTS prosody** — Kokoro `am_michael` and macOS `Yuna` both sound like a documentary VO floor; ElevenLabs / paid Korean TTS would lift Production polish by ~2 points but adds the money-firewall question.  Deferred to post-niche-pick.
- **Background music** — pipeline has no music underlay.  TTS over silent B-roll feels colder than the Reels/TikTok norm.  Adding a Pexels-licensed instrumental bed (or an AI-generated bed, given the operator's domain) would lift Production polish + emotional pull.  Deferred.
- **Operator perception ≠ Claude scoring** — viewers may weight Hook strength much higher than these scores treat it.  Once a real upload + retention curve exists, replace these self-scored numbers with actual platform watch-time percentile.
