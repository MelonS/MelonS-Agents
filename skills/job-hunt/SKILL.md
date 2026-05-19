---
name: job-hunt
description: Aggregate job postings from Korean job boards (사람인 / 잡코리아 / 원티드 / 프로그래머스 etc.) matched against operator filters (직군 + 지역 + 키워드), produce a dated markdown digest under `records/jobs/<date>/digest.md`, and surface apply-assist links per posting.  Use when the user wants a daily/on-demand summary of new openings matching their criteria.  Currently `locale: kr` only; the `sources/` directory is plugin-shaped so other locales can be added via PR (`sources/us-linkedin.sh`, etc.) without changing the orchestrator.
license: MIT
compatibility: Requires `bash`, `curl`, `jq`, optional `pup` or `python3` for HTML parsing depending on source.  Per-source authentication varies — see `sources/<name>.sh` headers.  macOS or Linux.  Live scraping not yet implemented in this scaffold — see "Status" section below.
metadata:
  authors: MelonS-Agents
  version: "0.0.1-scaffold"
  pipeline-source: scripts/run.sh (this skill is self-contained — no agents/missions/ counterpart)
  spec: agentskills.io
  added: "2026-05-20"
  status: scaffold-only — no live source implementation yet
allowed-tools: Bash(bash:*) Bash(curl:*) Bash(jq:*) Bash(python3:*) Read Write
---

# job-hunt

Daily / on-demand digest of new job postings matched against an
operator-defined filter (직군 + 지역 + 키워드), with apply-assist
links per posting.

## Status (2026-05-20)

End-to-end pipeline **functional in mock-fallback mode**.  Live
HTTP integration is gated on operator validation per source.

| Component | Status |
|---|---|
| Orchestrator (`scripts/run.sh`) | ✅ wired; filter + dedupe + diff + render |
| Markdown digest renderer (`scripts/digest.sh`) | ✅ working |
| Apply-assist link derivation (`scripts/apply-assist.sh`) | ✅ working |
| `sources/_mock.sh` | ✅ deterministic 8-posting fixture |
| `sources/kr-wanted.sh` | ⚠️ mock-fallback default; live path documented + commented; flip via `JH_WANTED_LIVE=1` + `WANTED_API_KEY` |
| `sources/kr-programmers.sh` | ⚠️ mock-fallback default; live path documented + commented; flip via `JH_PROGRAMMERS_LIVE=1` |
| `sources/kr-jobkorea.sh` | ⚠️ mock-fallback default; live HTML-scrape path documented; flip via `JH_JOBKOREA_LIVE=1` |
| `sources/kr-saramin.sh` | ⚠️ mock-fallback default; live OpenAPI path documented; flip via `JH_SARAMIN_LIVE=1` + `SARAMIN_KEY` |
| `tests/smoke.sh` | ✅ structural + end-to-end mock test (5 sources) |

The live HTTP path for each `kr-*` plugin is intentionally
disabled by default because:
1. Endpoint shapes need operator confirmation against the
   current API surface (these change without notice).
2. API keys (where required) are operator-supplied secrets.
3. Anti-bot patterns (for scraped sources) need supervised
   tuning, not unattended bursts.

Mock-fallback mode keeps the orchestrator + filter + dedupe +
digest path continuously testable.  Live integration is a
flip-the-flag operation once each plugin's curl/jq translation is
operator-validated.

## What this produces

Given:

- A filter file (`config/filters.yaml`) listing 직군 (job category),
  지역 (region — typically Korean sido / sigungu level), and
  optional keyword include/exclude lists.
- A `locale:` key (currently only `kr` supported; future PRs can
  add other locales).
- One or more enabled `sources/<name>.sh` plugins.

Produces:

- A dated digest at `records/jobs/<YYYY-MM-DD>/digest.md` with:
  - One section per source (사람인, 원티드, etc.).
  - Per posting: title, company, region, posted-at timestamp,
    summary, and an **apply-assist link** that pre-fills the
    operator's filter context where the source supports it.
  - A "new since last run" delta when prior digests exist.
- A `records/jobs/<YYYY-MM-DD>/raw/<source>.json` cache of the raw
  fetch so subsequent runs can compute deltas without re-fetching.

## How to invoke

User-facing invocation: `/job-hunt` (zero-arg uses
`config/filters.yaml`).

Programmatic:

```bash
skills/job-hunt/scripts/run.sh                 # uses config/filters.yaml
skills/job-hunt/scripts/run.sh --filters=path  # explicit filter file
skills/job-hunt/scripts/run.sh --sources=kr-wanted,kr-programmers
```

## Filter schema (`config/filters.yaml`)

```yaml
locale: kr                         # required; only "kr" supported today
job_categories:                    # 직군 — typically a small list
  - 백엔드 개발자
  - AI 엔지니어
regions:                           # 지역 — sido or sido + sigungu
  - 서울
  - 경기 성남
  - 원격                           # remote-friendly opt-in
keywords:
  include: [Python, AI, agent]     # OR semantics within a posting
  exclude: [SI, 파견]              # exclude any posting matching
sources:                           # enabled source plugins
  - kr-saramin
  - kr-wanted
  - kr-programmers
output:
  records_root: ./records/jobs     # where digests land
  format: markdown                 # md | json (md is default)
```

`config/filters.example.yaml` ships as a documented starting
point; operator copies to `filters.yaml` and edits.

## Adding a locale

Each locale is a directory of source plugins matching the pattern
`sources/<locale>-<board>.sh`.  Each plugin exposes a single
function `fetch_postings()` that prints normalized JSON on stdout:

```json
{
  "source": "kr-saramin",
  "fetched_at": "2026-05-20T00:30:00+09:00",
  "postings": [
    {
      "title": "백엔드 개발자",
      "company": "Example Co.",
      "region": "서울 강남구",
      "posted_at": "2026-05-19",
      "url": "https://...",
      "summary": "..."
    }
  ]
}
```

The orchestrator (`scripts/run.sh`) merges output from all enabled
sources, applies the keyword include/exclude filter, deduplicates
on `url`, computes the delta against the most recent prior
digest, and writes the markdown file.

Adding a new locale → drop a new `sources/<locale>-<board>.sh`
that implements `fetch_postings()` and add the source name to the
operator's `filters.yaml` `sources:` list.  No orchestrator
change needed.  PRs welcome.

## Scope explicitly out (for now)

- **Resume tailoring** — separate skill if pursued.
- **Application submission automation** — anti-bot territory; the
  skill stops at apply-assist *links*, not auto-submission.
- **Interview prep** — separate skill.
- **Non-Korean locales** — design supports extension; ship is kr
  only until operator validates the kr path.

## Privacy / data handling

- Operator filter file (`config/filters.yaml`) **is** committed
  unless it contains personally identifying job-seeker context.
  An example file ships; the real one is per-machine.
- Output digests under `records/jobs/` are gitignored (the repo's
  `records/` convention).
- No source credentials are stored in this skill — each source
  plugin reads its key from `.env` if needed.
