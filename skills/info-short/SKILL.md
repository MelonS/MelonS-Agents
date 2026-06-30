---
name: info-short
description: 정보쇼츠 — produce a 60-second 9:16 vertical INFORMATIONAL/explainer short from a topic, through the four-team pipeline 리서치팀 → 제작팀 ⇄ 법률팀 → 출시팀. Use when the operator gives an evergreen educational topic (a fact, a how/why, a concept) and wants a sourced, license-clean, narrated short ready for manual upload. Reuses the faceless-short render core; adds a research stage (sourced claims) and a legal gate before release.
license: MIT
compatibility: Requires the faceless-short deps (ffmpeg libass, ollama, whisper.cpp, jq, Pexels API key) plus python3. Web research uses the agent's WebSearch/WebFetch. macOS / Linux / Windows-via-Git-Bash.
metadata:
  authors: MelonS-Agents
  version: "0.1.0"
  pipeline-source: agents/missions/content-short/run.sh
  builds-on: faceless-short
  profile: info
  spec: agentskills.io
  added: "2026-07-01"
allowed-tools: Agent Bash(bash:*) Bash(ffmpeg:*) Bash(ffprobe:*) Bash(ollama:*) Bash(jq:*) Bash(python3:*) Read Write WebSearch WebFetch
---

# info-short (정보쇼츠)

Turn an evergreen topic into a sourced, license-clean 60-second explainer short
— through four teams, with a legal gate before it can be released.

## How to invoke

```
/info-short "<topic>"
/info-short "Why the sky is blue — and why sunsets are red"
/info-short "The Antikythera mechanism — a 2000-year-old analog computer"
```

If no topic is given, ask for one. Optionally pass a short id:
`/info-short "<topic>" --id=skyblue`.

## What runs (the four teams)

This skill hands the topic to the **content-director**, which sequences:

1. **리서치팀 (research-team)** → `resources/research.json`: an angle + hook,
   claims each tied to a real **fact source** it actually read, per-beat stock
   visual terms, and risk flags. Evergreen — no recency gate.
2. **제작팀 (production-team)** → `outputs/short.mp4` via
   `agents/missions/content-short/run.sh --profile=info` (faceless core:
   ollama script → Kokoro TTS → whisper captions → Pexels B-roll → 9:16 render
   with burned captions + source attribution).
3. **법률팀 (legal-team) ⇄ 제작팀** → `legal/legal-verdict.json`: license gate
   (`guard_publish`) + accuracy / unverifiable-claim / disclosure checks. A
   `REVISE` returns a fix list to production; re-render; re-review until `PASS`.
4. **출시팀 (release-team)** → `release/`: per-platform title/description/tags,
   thumbnail, attribution manifest, disclosure lines, and `PUBLISH-CHECKLIST.md`.

## Headless quick path (no research subagent)

For a fast draft with ollama-only scripting (no web-sourced claims), the
deterministic spine runs standalone:

```bash
agents/missions/content-short/run.sh skyblue --profile=info \
  --topic="Why the sky is blue" --stage=all
```

This produces + runs the deterministic legal gate + (on PASS) the release
package — but skips the sourced-research and legal-judgment stages, so it's a
draft, not a release-grade short. Prefer the full `/info-short` flow for upload.

## What it produces
- `outputs/short.mp4` — 1080×1920, ~60s, burned captions + Pexels attribution.
- `legal/legal-verdict.json` — the gate result + any fix list.
- `release/` — upload-ready copy + thumbnail + checklist (manual upload only).

## What it does NOT do
- Auto-upload (operator uploads manually; public URLs stay out of the repo).
- Cite a fact it didn't verify (research-team reads sources before claiming).
- Use paid APIs without confirmation (default path is $0).

## See also
- `docs/content-shorts-pipeline.md` — architecture + data contract.
- Sibling profiles: `/news-short` (뉴스쇼츠), `/idol-short` (아이돌쇼츠).
- Render core: `agents/missions/faceless-short/run.sh`.
