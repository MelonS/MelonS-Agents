---
name: idol-short
description: 아이돌/아티스트 콘텐츠 쇼츠 — produce a 60-second 9:16 vertical short ABOUT a real idol/artist (subject configured in config/subjects/<id>.yaml) through the four-team pipeline 리서치팀 → 제작팀 ⇄ 법률팀 → 출시팀. Use when the operator wants a fan-news/info short about a real artist. Because the subject is REAL PEOPLE + a trademarked group, the legal gate is the load-bearing stage — it enforces the default-safe path (synthetic narration + sourced facts + license-clean generic B-roll + on-screen text, NO member imagery / group audio / agency media) and flags any escalation. Reuses the faceless-short render core.
license: MIT
compatibility: Requires the faceless-short deps (ffmpeg libass, ollama, whisper.cpp, jq, Pexels API key) plus python3, and a unicode font in LAYOUT_DRAWTEXT_FONTFILE for the Hangul overlay. macOS / Linux / Windows-via-Git-Bash.
metadata:
  authors: MelonS-Agents
  version: "0.1.0"
  pipeline-source: agents/missions/content-short/run.sh
  builds-on: faceless-short
  profile: idol
  subject: config/subjects/<id>.yaml
  spec: agentskills.io
  added: "2026-07-01"
allowed-tools: Agent Bash(bash:*) Bash(ffmpeg:*) Bash(ffprobe:*) Bash(ollama:*) Bash(jq:*) Bash(python3:*) Read Write WebSearch WebFetch
---

# idol-short — 아이돌/아티스트 콘텐츠 쇼츠

A 60-second 9:16 short **about a real idol/artist**. The skill is genre-abstract;
the concrete artist is defined in a per-subject file (`config/subjects/<id>.yaml`)
selected with `--subject=<id>`. Real subject files are kept local (gitignored);
a tracked `config/subjects/example.yaml` is a placeholder template.

> **This is content about REAL PEOPLE.** That inverts the legal posture vs a
> synthetic character: **portrait/publicity rights (초상권·퍼블리시티권), music &
> MV copyright (agency-owned), trademark, and (Korean) defamation** all apply.
> That is exactly why this pipeline has a 법률팀 — a single allowlist grep is not
> enough here. See the default-safe path below.

## How to invoke

```
/idol-short "<topic>" --subject=<id>        # selects config/subjects/<id>.yaml
/idol-short "<topic>" --subject=<id> --base=news   # news-shaped (default) | info
```

Example topics (genre, not a specific artist): `"<artist> comeback single
release date"`, `"<artist> member's solo channel hits a subscriber milestone"`.

## The default-safe path (what this skill produces)

To stay clearly inside what a fan/news channel may publish, the default render
uses **only**:

- **synthetic AI narration** (a narrator voice — never a member's voice),
- **officially-announced, sourced facts** (research-team cites official channels
  + reputable outlets),
- **license-clean generic B-roll** (Pexels) + **on-screen text**,
- channel branding + mandatory disclaimers burned in.

It deliberately uses **NO member photos/video, NO group audio, and NO
agency-owned media** — those are the high-risk items. If the operator wants to
include any of them, the 법률팀 flags it (rights needed) before release.

## The subject file

`config/subjects/<id>.yaml` is the single source of truth for a subject: the
group's official channels (for source verification), branding (channel name +
colors, **text-based, no member likeness**), the legal posture (incl.
`has_minors`), and the two mandatory disclaimers. See
`config/subjects/README.md` for the schema; copy `example.yaml` and fill in a
real artist locally. Keep real subject files local — they are gitignored.

## What runs (the four teams)

1. **리서치팀** — gathers officially-announced facts (comeback dates, releases,
   member news) from the subject's **official channels** + reputable outlets,
   each claim sourced. Flags rumors / private facts / dating speculation as off-limits.
2. **제작팀** — `--profile=idol`: faceless render with the synthetic narrator,
   then `scripts/subject-overlay.sh` layers channel branding + the fan-content
   and AI-narration disclaimers (never re-encoding the base).
3. **법률팀 ⇄ 제작팀** — the load-bearing gate. Adds the **idol-content checks**:
   - **portrait-publicity-rights** — no real-member likeness used without rights.
   - **media-rights-reuse** — no agency-owned photo/MV/performance clip; no group
     audio (KOMCA + master rights / Content ID).
   - **fan-content-disclaimer** — the "unofficial / not affiliated" line present.
   - **defamation** — only officially-announced public info; no rumors. (Korea:
     사실적시 명예훼손 — even true private facts can be actionable.)
   - **minors** — if the subject file marks `has_minors: true`, heightened care.
   - **synthetic-disclosure** — AI-narration line present.
   `REVISE` returns a fix list; `BLOCK` for unfixable (e.g. unlicensed group audio).
4. **출시팀** — packages with the disclaimers + source list; manual upload only.

## What it produces
- `outputs/short.mp4` — 1080×1920, ~60s, branding + fan-content & AI-narration
  disclaimers burned in, no member likeness by default.
- `legal/legal-verdict.json` — gate result incl. the idol-content checks.
- `release/` — upload copy (with disclaimers) + thumbnail + checklist.

## What it does NOT do
- Use a real member's photo, video, voice, or the group's audio by default
  (rights — the legal gate blocks/flags it).
- Publish rumors or unverified/private claims about real members.
- Comment on minors' private life/appearance when the subject has minor members
  (the legal gate applies heightened care; officially-announced activity only).
- Imply official endorsement (this is unofficial fan content).
- Auto-upload, or commit real subject specifics to a public repo.

## See also
- `config/subjects/README.md` — subject schema + why real ones stay local.
- `config/subjects/example.yaml` — the placeholder template to copy.
- `scripts/subject-overlay.sh` — branding + disclaimer overlay.
- `docs/content-shorts-pipeline.md` — architecture + data contract.
- Sibling profiles: `/info-short` (정보쇼츠), `/news-short` (뉴스쇼츠).
