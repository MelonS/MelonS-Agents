---
name: job-hunt
description: Find Korean job postings for a target role with minimal user input — pass a short `--seed` keyword like "Problem Solver" or "Forward Deployed Engineer" and the skill expands it to the full family of equivalent titles used at different companies (FDE / Applied AI Engineer / Generalist / Solutions Engineer / etc.), fetches matching postings from Korean job boards (사람인 / 잡코리아 / 원티드 / 프로그래머스), and produces a dated markdown digest under `records/jobs/<date>/digest.md` with per-posting apply-assist links.  Use when the user knows roughly what role they want but doesn't want to enumerate every alternative title themselves.  Advanced mode (`--filters=<path>`) accepts a hand-edited filters.yaml for full control.  Currently `locale: kr` only; `sources/` directory is plugin-shaped so other locales can be added via PR.
license: MIT
compatibility: Requires `bash`, `curl`, `jq`, optional `pup` or `python3` for HTML parsing depending on source.  Per-source authentication varies — see `sources/<name>.sh` headers.  macOS or Linux.  Live scraping not yet implemented in this scaffold — see "Status" section below.
metadata:
  authors: MelonS-Agents
  version: "0.1.0"
  pipeline-source: scripts/run.sh (this skill is self-contained — no agents/missions/ counterpart)
  spec: agentskills.io
  added: "2026-05-20"
  status: end-to-end functional in mock-fallback mode; live HTTP per-plugin flag-gated
allowed-tools: Bash(bash:*) Bash(curl:*) Bash(jq:*) Bash(python3:*) Read Write
---

# job-hunt

Daily / on-demand digest of new job postings matched against an
operator-defined filter (직군 + 지역 + 키워드), with apply-assist
links per posting.

## Status (2026-05-20)

End-to-end pipeline **functional in mock-fallback mode**, with the
v2 short-keyword UX (`--seed "Problem Solver"` → automatic role
family expansion via `config/role-synonyms.yaml`) wired in.  Live
HTTP integration is gated on operator validation per source.

| Component | Status |
|---|---|
| Orchestrator (`scripts/run.sh`) | ✅ wired; filter + dedupe + diff + render + `--fit-score` integration |
| Markdown digest renderer (`scripts/digest.sh`) | ✅ working; fit-score line per posting |
| Apply-assist link derivation (`scripts/apply-assist.sh`) | ✅ working |
| `config/role-synonyms.yaml` | ✅ Phase 2.1 — 5 families, 50+ synonyms (problem-solver, ai-engineer-ml, agent-engineer, ai-product-manager, backend-engineer) |
| `config/operator-profile.example.md` | ✅ Phase 2.2 — generic template (operator copies → operator-profile.md, gitignored) |
| `sources/_mock.sh` | ✅ deterministic 11-posting fixture incl. Problem Solver family |
| **`sources/global-ats.sh`** | ⭐ **live-ready** — Greenhouse + Ashby + Lever public boards, no auth; 2026-05-21 e2e test pulled 5,015 raw / 169 Problem-Solver-matched postings; flip `JH_GLOBAL_ATS_LIVE=1` |
| **`sources/global-remoteok.sh`** | ⭐ **live-ready** — `remoteok.com/api`, no auth; flip `JH_GLOBAL_REMOTEOK_LIVE=1` |
| **`sources/global-remotive.sh`** | ⭐ **live-ready** — `remotive.com/api/remote-jobs`, no auth; flip `JH_GLOBAL_REMOTIVE_LIVE=1` |
| `sources/kr-wanted.sh` | ⚠️ mock-fallback default + 1 Problem Solver entry; live HTTP flip via `JH_WANTED_LIVE=1` + `WANTED_API_KEY` (operator-validated) |
| `sources/kr-saramin.sh` | ⚠️ mock-fallback default + 1 문제 해결사 entry; live OpenAPI flip via `JH_SARAMIN_LIVE=1` + `SARAMIN_KEY` (free signup at oapi.saramin.co.kr) |
| `sources/kr-jobkorea.sh` | ⛔ **permanent mock-only** — robots.txt forbids `/Search/?stext=` (only SSR path); 2017 잡코리아 vs 사람인 precedent applies. See `docs/research/job-sources-survey-2026-05-21.md`. |
| `sources/kr-programmers.sh` | ⛔ **deprecated stub** — Programmers 채용 service permanently closed 2025-05-19, domain NXDOMAIN |
| `scripts/fit-score.sh` | ⚠️ Phase 2.3 — scaffold mode default; per-posting Claude call gated on `JH_FIT_SCORE_LIVE=1` |
| `scripts/cover-letter-draft.sh` | ⚠️ Phase 2.5 — scaffold mode default; gated on `JH_COVER_LETTER_LIVE=1` |
| `scripts/company-research.sh` | ⚠️ Phase 2.5 — scaffold mode default; gated on `JH_COMPANY_RESEARCH_LIVE=1` |
| `scripts/interview-prep.sh` | ⚠️ Phase 2.5 — scaffold mode default; gated on `JH_INTERVIEW_PREP_LIVE=1` |
| `scripts/derive-profile.sh` | ⚠️ Phase 2.4 — scaffold mode default; reads repo and drafts `operator-profile.md`; gated on `JH_DERIVE_PROFILE_LIVE=1` |
| `tests/smoke.sh` + `edge-cases.sh` + `schema-validation.sh` | ✅ 66/66 PASS (32 + 26 + 8) |

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

- Operator filter file (`config/filters.yaml`) is **gitignored
  by default** (see `.gitignore`).  Per [[repo-as-credibility-
  signal]] memory rule, specific 직군 / 지역 / exclusion lists
  reveal a job-seeker's personal target and don't belong in
  committed files.  Operators who *want* to commit a generic,
  non-personally-identifying filter (e.g. to share a domain
  starter-template) can `git add -f` it explicitly.
- `config/filters.example.yaml` is committed as the documented
  generic starting point — categories like "백엔드 개발자" /
  "AI 엔지니어" are deliberately broad and contain no operator
  specifics.
- Output digests under `records/jobs/` are gitignored (the repo's
  `records/` convention).  All raw fetched JSON + rendered
  markdown stays local.
- No source credentials are stored in this skill — each source
  plugin reads its key from `.env` (gitignored) if needed.
