# Sonnet trial — script quality benchmark vs llama3.2:3b

**Date**: 2026-05-17 04:50–07:25 KST.
**Trigger**: operator feedback after watching v5 pilots (locally generated scripts):

> "기획이 중요. 첫 5초에 시선 끌만한게 없음. 정보 모르는 입장에서 첨부터 본다고 생각하면 이해 안 됨.
>  과학쪽 수소가 10%인지 60%인지 헷갈림.  대부분 이탈할듯."

This trial routes ONE pipeline stage — the script-generation step — from the local `llama3.2:3b` model to Claude Sonnet via the existing `claude` CLI (subscription quota, no incremental dollar spend).  All other stages (Kokoro TTS, whisper.cpp, caption correction + single-line split, Pexels B-roll fetch, ffmpeg render) remain Tier-2 / local / unchanged.

## What was generated

| | EN | KO |
|---|---|---|
| Hittites script | [`hittites-en-script.txt`](hittites-en-script.txt) (142 words) | [`hittites-ko-script.txt`](hittites-ko-script.txt) (~770 chars) |
| Hydrogen script | [`hydrogen-en-script.txt`](hydrogen-en-script.txt) (155 words) | [`hydrogen-ko-script.txt`](hydrogen-ko-script.txt) (~780 chars) |
| Render mp4 (gitignored) | `records/missions/2026-05-17/faceless-hittites-v6-071919/outputs/short.mp4` (64.1s, 40 MB) | `records/missions/2026-05-17/faceless-hittites-v6-ko-072106/outputs/short.mp4` (45.5s, 23 MB) |
| Hydrogen render | `records/missions/2026-05-17/faceless-hydrogen-v6-072159/outputs/short.mp4` (57.4s, 42 MB) | `records/missions/2026-05-17/faceless-hydrogen-v6-ko-072343/outputs/short.mp4` (45.6s, 26 MB) |
| Caption-verify | [`screens/hittites-en-v6-caption-verify.jpg`](screens/hittites-en-v6-caption-verify.jpg) | [`screens/hittites-ko-v6-caption-verify.jpg`](screens/hittites-ko-v6-caption-verify.jpg) |
| Hydrogen caption-verify | [`screens/hydrogen-en-v6-caption-verify.jpg`](screens/hydrogen-en-v6-caption-verify.jpg) | [`screens/hydrogen-ko-v6-caption-verify.jpg`](screens/hydrogen-ko-v6-caption-verify.jpg) |

## Hook-line comparison — same topic, different model

### Hittites (history × Bible)

| Model | Hook (first ~1.5 s of narration) |
|---|---|
| llama3.2:3b (v5) | "What if the biblical account of the Hittites was more than just a myth?" |
| **Sonnet (v6)** | **"Scholars called the Hittites fiction — a Bronze Age kingdom the Bible placed alongside Egypt as a military power, with no ruins to confirm it."** |

The v5 hook follows the explicitly-forbidden "What if…" pattern (Sonnet's system prompt rejects this; llama3.2:3b couldn't be prompted out of it across multiple iterations).  The v6 hook makes a bare-statement claim viewers can immediately push back against, then invites them to stay to see how the conflict resolves.

### Hydrogen (science)

| Model | Hook |
|---|---|
| llama3.2:3b (v5) | "What if hydrogen, the lightest and most abundant element in the universe, is also the most underappreciated in our own bodies?" |
| **Sonnet (v6)** | **"63 of every 100 atoms in your body is hydrogen."** |

The v5 hook is abstract + uses the forbidden "What if".  More importantly, the v5 script later cited "hydrogen makes up 10% of your body" without disambiguating the frame (mass vs atom count — the operator caught this mid-watch as a "10% or 60%?  헷갈리네" moment, which is exactly the kind of confusion that triggers a skip).  The v6 script uses **atom count consistently throughout** (63%, 13.8 billion years, 620 million tons per second) — one frame, internally coherent.

## Per-window B-roll keyword examples (Hittites EN v6)

Showing how Sonnet's specific-detail prose feeds clean keyword extraction:

```
window 0 ("Scholars called the Hittites fiction…")  →  ancient biblical scroll
window 1 ("Bronze Age kingdom the Bible placed…")   →  Bronze Age ruins
window 2 ("In 1906, a German archaeologist…")       →  Turkish village excavation
window 3 ("Within weeks, his team had pulled…")     →  cuneiform tablets
window 4 ("The tablets described an empire…")       →  Hittite palace rubble
window 5 ("…fielded 3,500 chariots at the Battle…") →  chariots at battle
window 6 ("…signed a peace agreement with Ramesses") →  Ramesses II treaty
window 7 ("Hattusa, their capital, still stands…")  →  Hittite capital ruins
```

Compare to v5 where one keyword-extraction variant collapsed all 8 windows to the same Pexels clip — the Sonnet script is specific enough that each window has its own concrete visual target.

## Generation cost

Each Sonnet call ≈ 500 tokens prompt + 200 tokens output ≈ 700 tokens total.  Four scripts = ~2,800 tokens.  Against the Max-plan weekly Sonnet quota this is **under 1%** of one week's allowance — operationally negligible.

Money-firewall analysis: this is **inside** the existing pre-paid subscription quota.  Per `docs/operator-contract.md` §3, the firewall guards against *new* paid resources (separate APIs, SaaS subscriptions, cloud spend) — not quota usage on a subscription the operator already holds.

## Why this stage and not others

The trial routes only the **script-generation** stage to Sonnet.  Other Tier-2 stages stay local.

| Stage | Tier | Why |
|---|---|---|
| Script generation | **Sonnet (Tier 1)** | One-shot creative; quality compounds over the next 60 seconds; ~700 tokens per call is operationally negligible |
| TTS | Kokoro / Yuna (Tier 2) | Mechanical conversion; quality is bounded by the voice model regardless of upstream choice |
| Whisper transcription | whisper.cpp (Tier 2) | Mechanical conversion; small-model drift is corrected downstream by script-aware caption alignment |
| Caption correction | Python (Tier 2) | Deterministic algorithm (no model); operates on script ground-truth |
| Caption split | Python (Tier 2) | Deterministic algorithm |
| Per-window B-roll keywords | llama3.2:3b (Tier 2) | High call volume (8 windows × 4 missions = 32 calls per pilot batch); good-enough for "noun phrase from a sentence"; Sonnet wouldn't earn its cost here |
| Pexels fetch | curl (Tier 2) | I/O, not generation |
| ffmpeg render | ffmpeg (Tier 2) | Pure CPU/GPU work |

The lesson is documented in [`docs/cost-model.md`](../../cost-model.md#when-tier-2-is-the-wrong-default--creative-stages): Tier-2-as-default applies to **mechanical / high-volume** stages.  One-shot creative stages route to Tier-1 because quality compounds and per-call cost is bounded.

## Operator decision still needed

The trial demonstrates the quality ceiling difference but does NOT commit to either niche.  The operator's choice (history × Bible vs science) remains open.  Sonnet routing affects script quality regardless of niche — picking a niche commits to topic backlog, not to which model writes scripts.

The remaining `docs/goal.md` subgoal (#4 — operator pick in `decision-log.md`) is unchanged; this trial is supporting evidence for the pick, not the pick itself.
