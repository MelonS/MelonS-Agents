# Content-Shorts Pipeline — 리서치 → 제작 ⇄ 법률 → 출시

A four-team production line that turns a **topic seed** into an upload-ready
60-second 9:16 short, with a **legal review loop** between production and
release.  Three operator-facing skills sit on top of one shared pipeline:

| Skill (slash)      | Korean name | Profile  | What it is |
|--------------------|-------------|----------|------------|
| `/info-short`      | 정보쇼츠    | `info`   | Evergreen explainer / educational fact short |
| `/news-short`      | 뉴스쇼츠    | `news`   | Current-event recap, recency-gated, defamation-screened |
| `/idol-short`      | 아이돌쇼츠   | `idol`| Short ABOUT a **real** idol/artist (subject configured in `config/subjects/<id>.yaml`). Real people + trademarked group → the legal gate is load-bearing: default-safe path (synthetic narration + sourced facts + license-clean generic B-roll + text; no member imagery / group audio / agency media) + portrait-publicity-rights, media-rights-reuse, fan-content-disclaimer checks |

All three **reuse the existing `faceless-short` render core** (ollama script →
TTS → whisper captions → Pexels B-roll → ffmpeg stitch).  They differ only in
**profile** (tone, recency rules, legal strictness, disclosure) and, for
`idol`, a **subject overlay** (channel branding + fan-content & AI-narration
disclaimers) layered on top of the faceless output — the same "builds-on, never
edits the base" pattern `product-cf` uses over `music-video`. The `idol` skill
is genre-abstract for public-repo IP-safety; the specific artist lives in
`config/subjects/<id>.yaml`.

---

## The four teams

```
  ┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
  │  리서치팀     │────▶│   제작팀      │◀───▶│   법률팀      │────▶│   출시팀      │
  │ research-team│     │production-team│     │  legal-team  │     │ release-team │
  └──────────────┘     └──────────────┘     └──────────────┘     └──────────────┘
   facts + sources       render the short      gate: license /       upload package:
   (license-screened,    (faceless core +       accuracy / defamation  title·desc·tags,
    recency-checked)      subject overlay)       / disclosure / rights  thumbnail, disclosure,
                                ▲                       │                attribution manifest,
                                └───── REVISE ──────────┘                publish checklist
                                   (fix list, re-render)
```

- **리서치팀 (research-team)** — Claude subagent. Web research. Produces
  `research.json`: claims each tied to fact-sources, B-roll media hints,
  recency stamp, and pre-screened **media** sources. Separates *fact citations*
  (fair-use factual reporting — news articles, encyclopedias) from *media reuse*
  (clips that get downloaded and composited — must pass the copyright allowlist).
- **제작팀 (production-team)** — drives `agents/missions/content-short/run.sh
  --stage produce`, which wraps `faceless-short` with the profile's defaults.
  On a `REVISE` verdict it re-renders with the legal team's fix list applied
  (script override, term swap, disclosure burn-in).
- **법률팀 (legal-team)** — Claude subagent + `scripts/legal-gate.sh`. The new,
  load-bearing addition. Runs the deterministic license gate
  (`agents/lib/copyright.sh::guard_publish`) **and** content-legal judgment
  (factual accuracy, defamation/identifiable-person risk, unverifiable claims,
  synthetic-narration disclosure, trademark/IP exposure, required disclaimers; and
  for `idol` shorts portrait-publicity-rights, media-rights-reuse, fan-content
  disclaimer). Emits `legal-verdict.json` with `PASS | REVISE | BLOCK`.
  The `⇄` loop: `REVISE` bounces a fix list back to production; re-render;
  re-review; until `PASS` or `--max-legal-iters` is hit (then surface to operator).
- **출시팀 (release-team)** — runs only after a `PASS`. Produces the upload
  package (`gen-upload-metadata.sh` + per-platform copy + thumbnail + disclosure
  line + attribution manifest + publish checklist). **Never auto-uploads** —
  the operator uploads manually through each platform's UI (2026-05-18
  threat-model decision; public URLs stay out of the repo).

### Why two layers (bash spine + Claude subagents)

The repo's established split applies here too:

- **Deterministic spine** = `agents/missions/content-short/run.sh`. Render,
  the license gate, metadata generation — reproducible, headless, `$0` runtime.
- **Judgment stages** = `research-team` / `legal-team` Claude subagents. Web
  research and legal judgment need a reasoning model, not bash. They read/write
  the JSON contract below; the spine consumes their output.

The `content-director` subagent (or the top-level conversation) orchestrates:
`research-team → run.sh produce → legal-team → {loop on REVISE} → run.sh release`.

---

## The data contract

Both JSON artifacts live in the mission folder
`$RECORDS_DIR/missions/<date>/content-<profile>-<short_id>-<HHMMSS>/`.

### `resources/research.json` — research-team → production + legal

```json
{
  "profile": "info | news | idol",
  "topic": "operator topic seed",
  "angle": "the specific framing/hook chosen",
  "hook": "one striking opening line",
  "fact_sources": [
    { "url": "https://apnews.com/...", "title": "...", "publisher": "AP",
      "date": "2026-06-30", "kind": "news|reference|primary",
      "key_facts": ["fact A", "fact B"] }
  ],
  "media_sources": [
    { "url": "https://www.pexels.com/...", "intended_use": "B-roll window 3",
      "license_screen": "allowed|local|blocked", "license": "Pexels|CC-BY-3.0",
      "note": "screened via check_source_allowed" }
  ],
  "claims": [
    { "text": "factual claim used in the narration",
      "fact_source_urls": ["..."], "confidence": "high|med|low" }
  ],
  "recency": { "required_within_days": 3, "newest_source_date": "2026-06-30",
               "ok": true },
  "script_seed": "optional 130-160w draft narration the producer may refine",
  "visual_terms": ["per-beat stock-footage search hints"],
  "risk_flags": ["named-living-person", "medical-claim", "financial-advice",
                 "trademark", "graphic-event"]
}
```

- `media_sources[].license_screen` is filled by `scripts/research-screen.sh`,
  which calls `check_source_allowed` per URL. A `blocked` media source must be
  dropped before production (the producer falls back to Pexels keyword search,
  which is always license-clean).
- `fact_sources` are **not** license-gated — citing a fact is fair-use factual
  reporting, not media reuse. They are checked for *credibility* and *recency*,
  not for a media license. Keeping these two lists separate is the whole point
  of having a legal team rather than a single allowlist grep.

### `legal/legal-verdict.json` — legal-team → production / release

```json
{
  "verdict": "PASS | REVISE | BLOCK",
  "iteration": 1,
  "profile": "news",
  "checks": [
    { "id": "media-license",   "status": "pass|fail|warn", "evidence": "guard_publish exit 0 (platform=public)" },
    { "id": "fact-accuracy",   "status": "pass", "evidence": "3/3 claims trace to fact_sources" },
    { "id": "defamation",      "status": "pass", "evidence": "no unverified allegation against a named living person" },
    { "id": "unverifiable",    "status": "warn", "evidence": "claim 2 confidence=low" },
    { "id": "synthetic-disclosure", "status": "pass", "evidence": "AI-narration disclosure line present" },
    { "id": "trademark-ip",    "status": "pass", "evidence": "no brand/IP shown without nominative-use basis" },
    { "id": "required-disclaimer", "status": "pass", "evidence": "news 'as of <date>' stamp present" }
  ],
  "_idol_checks_when_profile_idol": [
    { "id": "portrait-publicity-rights", "status": "pass", "evidence": "no real-member likeness used" },
    { "id": "media-rights-reuse", "status": "pass", "evidence": "generic B-roll only; no agency media / group audio" },
    { "id": "fan-content-disclaimer", "status": "pass", "evidence": "unofficial / not-affiliated line present" }
  ],
  "required_fixes": [
    { "target": "script|sources|visuals|disclosure",
      "instruction": "concrete change the producer must make",
      "blocking": true }
  ]
}
```

`verdict` is `BLOCK` if any check is `fail` and unfixable (e.g. a struck source,
defamation with no source); `REVISE` if `fail`/`warn` items have concrete fixes;
`PASS` only when every blocking check is `pass`.

---

## The executable spine — `content-short/run.sh`

```
agents/missions/content-short/run.sh <short_id> --profile=<info|news|idol> \
   [--stage=all|produce|legal|release] [--mission-dir=<existing dir>] \
   [--research=<research.json>] [--topic="..."] \
   [--legal-verdict=<legal-verdict.json>] [--max-legal-iters=2]
```

**One mission dir, threaded across stages.** A produce run prints a
`MISSION_DIR=<path>` line; the orchestrator captures it and passes
`--mission-dir=<path>` to every later stage so legal / re-render / release all
act on the SAME produced short. The stages are **decoupled** — legal and release
do **not** re-render; they `require_produced` (refuse if there's no
`outputs/short.mp4` in the dir).

- `--stage=produce` — consume `research.json` (or fall back to `--topic` +
  ollama, faceless-style) → render via `faceless-short` with profile env →
  apply subject overlay if `profile=idol` → write `short.mp4`, `script.txt`,
  `outputs/SOURCES.txt`, `outputs/disclosures.txt`. Prints `MISSION_DIR=`.
  Re-rendering on `REVISE` passes the same `--mission-dir` to overwrite in place.
- `--stage=legal` (needs `--mission-dir`) — run the **deterministic** gate
  (`legal-gate.sh`: `guard_publish` + disclosure presence) and merge with the
  `--legal-verdict` the legal-team subagent wrote. Where both the gate and the
  subagent assessed a check (e.g. `required-disclaimer`), the **worse** status
  wins. Exit 0 PASS / 1 REVISE / 2 BLOCK so the orchestrator can loop.
- `--stage=release` (needs `--mission-dir`) — only proceeds if the latest verdict
  is `PASS`; runs `gen-upload-metadata.sh` + writes the disclosure-stamped
  release package.
- `--stage=all` — produce → legal(deterministic baseline) → release in ONE
  freshly-minted dir, for headless runs where no subagent judgment is in the
  loop. The richer agent-orchestrated loop calls the stages individually and
  threads `--mission-dir`.

Profiles resolve from `config/content-short-profiles.yaml`; the `idol` subject
resolves from `config/subjects/<id>.yaml` (real subject files gitignored; the
tracked `example.yaml` is a placeholder template — select with `--subject=<id>`).

---

## Profiles at a glance

| Field | info (정보) | news (뉴스) | idol (아이돌) |
|-------|-------------|-------------|------------------|
| Subject | any topic | any current event | a **real** idol/artist (config/subjects) |
| Tone | neutral explainer | tight wire-service recap | neutral fan-news, synthetic narrator |
| Voice | `am_michael` | `am_michael` | synthetic narrator (subject file; never a member) |
| Recency gate | none | newest source ≤ N days | newest source ≤ N days |
| Legal strictness | standard | **high** (defamation, accuracy, "as of") | **high** (rights + defamation + disclaimers) |
| Mandatory disclosure | source-attribution | + "as of `<date>`" stamp | + fan-content (unofficial) + AI-narration burn-in |
| Overlay | no | no | **subject overlay** (channel branding + disclaimers, no member likeness) |
| Extra legal checks | — | — | portrait-publicity-rights · media-rights-reuse · fan-content-disclaimer |

---

## What this pipeline does NOT do

- Auto-upload. The operator uploads manually (public URLs intentionally not
  committed).
- Generate music (operator supplies, as with `music-video`).
- Bypass the money firewall — Suno/Kling/paid-API stages require explicit
  operator confirmation. The default path is `$0` (local ollama + Kokoro TTS +
  Pexels free tier).
- Show reference-IP or brand names in any committed README (per the IP-caution
  rule — abstract to genre).

## See also

- Render core: `agents/missions/faceless-short/run.sh`
- Legal primitives: `agents/lib/copyright.sh`, `agents/lib/attribution.sh`,
  `config/copyright-allowlist.yaml`, `docs/copyright-policy.md`
- Builds-on precedent: `skills/product-cf/SKILL.md`
- Team subagents: `.claude/agents/{research,production,legal,release}-team.md`,
  `.claude/agents/content-director.md`
