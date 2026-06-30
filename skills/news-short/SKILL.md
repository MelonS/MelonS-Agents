---
name: news-short
description: 뉴스쇼츠 — produce a 60-second 9:16 vertical NEWS recap short from a current event, through the four-team pipeline 리서치팀 → 제작팀 ⇄ 법률팀 → 출시팀. Use when the operator wants a timely, recency-gated, defamation-screened news short. Like info-short but with a freshness requirement (newest source within N days), multi-source corroboration, an "as of <date>" stamp, and a stricter legal gate (defamation / named-living-person / attribution). Reuses the faceless-short render core.
license: MIT
compatibility: Requires the faceless-short deps (ffmpeg libass, ollama, whisper.cpp, jq, Pexels API key) plus python3. Web research uses the agent's WebSearch/WebFetch. macOS / Linux / Windows-via-Git-Bash.
metadata:
  authors: MelonS-Agents
  version: "0.1.0"
  pipeline-source: agents/missions/content-short/run.sh
  builds-on: faceless-short
  profile: news
  spec: agentskills.io
  added: "2026-07-01"
allowed-tools: Agent Bash(bash:*) Bash(ffmpeg:*) Bash(ffprobe:*) Bash(ollama:*) Bash(jq:*) Bash(python3:*) Read Write WebSearch WebFetch
---

# news-short (뉴스쇼츠)

Turn a current event into a tight, sourced, recency-gated 60-second news recap —
with a **stricter legal gate** than info-short because news carries defamation,
accuracy, and freshness risk.

## How to invoke

```
/news-short "<event / headline>"
/news-short "summarize today's <topic> development"
/news-short "<event>" --id=quake0701 --within-days=2
```

If no event is given, ask. `--within-days=N` overrides the recency window
(default 3).

## What runs (the four teams)

Same four-team flow as info-short, with **news-profile** differences:

1. **리서치팀** — requires **≥2 independent reputable outlets** per claim, stamps
   `recency.newest_source_date`, and sets `recency.ok=false` if the story isn't
   fresh enough. If `recency.ok` is false, the director stops and tells you —
   stale news is not shipped.
2. **제작팀** — `--profile=news`: tighter 120–150-word wire-service recap, every
   claim attributed ("according to ..."), plus an **"As of `<date>`"** disclosure.
3. **법률팀 ⇄ 제작팀** — the strict gate. Adds, on top of info checks:
   - **defamation** — no unsourced allegation about a named living person
     (BLOCK-tier with no fix; otherwise REVISE to soften/attribute/cut).
   - **fact-accuracy** — every narrated claim must trace to a fact source.
   - **required-disclaimer** — the `As of <date>` stamp must be present.
4. **출시팀** — packages with the freshness note; news should be uploaded the
   same day it clears.

## What it produces
- `outputs/short.mp4` — 1080×1920, ~60s, attributed recap + "As of <date>".
- `legal/legal-verdict.json` — strict gate result + fix list.
- `release/` — upload copy (with the as-of line) + thumbnail + checklist.

## What it does NOT do
- Ship stale news (recency gate) or unsourced allegations (defamation gate).
- Auto-upload, or write public URLs into the repo.
- Editorialize — neutral recap only; speculation is a REVISE.

## See also
- `docs/content-shorts-pipeline.md` — architecture + data contract.
- Sibling profiles: `/info-short` (정보쇼츠), `/idol-short` (아이돌쇼츠).
