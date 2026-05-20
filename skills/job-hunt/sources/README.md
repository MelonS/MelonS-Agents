# `skills/job-hunt/sources/` — per-board fetch plugins

Each file in this directory is a single Bash plugin that knows
how to fetch postings from one job board and emit normalized JSON
on stdout.

## File naming

`<locale>-<board>.sh` — e.g. `kr-wanted.sh`, `kr-saramin.sh`.

For aggregator / locale-agnostic plugins use the reserved
`global-` prefix instead: `global-ats.sh`, `global-remoteok.sh`,
`global-remotive.sh`, etc.  These do not honor the operator's
`locale:` field — they ship as part of the kr stack but their
data is locale-independent (often remote / global).

## Plugin contract

Each plugin sources a single function `fetch_postings()` that:

1. Reads filter context from environment variables set by the
   orchestrator (`JH_REGIONS`, `JH_CATEGORIES`, `JH_KEYWORDS_INCLUDE`,
   `JH_KEYWORDS_EXCLUDE`).
2. Authenticates to the board (where the board supports an API
   token, read it from `.env` via the conventional name —
   `WANTED_API_KEY`, `SARAMIN_KEY`, etc.).
3. Fetches matching postings (rate-limit-aware; respects robots.txt
   where one exists).
4. Emits normalized JSON on stdout matching the schema in
   `../SKILL.md` "Adding a locale" section.
5. Returns exit 0 on success, exit 1 on auth failure (orchestrator
   may continue with other sources), exit 2 on network failure.

## Live-flag convention

Per [[scaffold-pattern]] every plugin defaults to a mock fixture
and gates the live HTTP path on a `JH_<NAME>_LIVE=1` env flag.
The orchestrator's `--list-sources` enumerates each plugin's
flag.  Mock fallback never goes to the network; live mode hits
the documented endpoint and may incur per-source quota.

## Source roster (2026-05-21)

| Plugin | Status | Live flag | Path |
|---|---|---|---|
| `_mock` | mock-only (test fixture) | none | — |
| **`global-ats`** | live-ready (Greenhouse / Ashby / Lever ⭐) | `JH_GLOBAL_ATS_LIVE` | Public ATS board APIs, no auth |
| **`global-remoteok`** | live-ready (RemoteOK ⭐) | `JH_GLOBAL_REMOTEOK_LIVE` | `https://remoteok.com/api` |
| **`global-remotive`** | live-ready (Remotive ⭐) | `JH_GLOBAL_REMOTIVE_LIVE` | `https://remotive.com/api/remote-jobs` |
| `kr-wanted` | live-path scaffolded; needs operator validation | `JH_WANTED_LIVE` + `WANTED_API_KEY` | Wanted partner API |
| `kr-saramin` | live-path scaffolded; needs operator validation | `JH_SARAMIN_LIVE` + `SARAMIN_KEY` | Saramin OpenAPI (`oapi.saramin.co.kr`) |
| `kr-jobkorea` | **mock-only (permanent)** — robots.txt + 2017 precedent | n/a | See file header |
| `kr-programmers` | **deprecated stub** — service closed 2025-05-19 | n/a | See file header |

⭐ = added 2026-05-21 from the job-sources survey
(`docs/research/job-sources-survey-2026-05-21.md`).  Each is fully
working in live mode with zero auth required.

## Anti-bot guidance

For sources that require HTML scraping:

- Always include a realistic User-Agent.  Don't use `curl/8.x`
  default UA on any Korean job board (instant block).
- Respect rate limits.  500ms minimum between requests on the
  same source; longer for Saramin (~2s).
- If a source returns Captcha challenges, the plugin must fail
  cleanly (exit 1) rather than retry-loop.  Operator intervention
  required to clear the block.
- Cache responses under `records/jobs/<date>/raw/<source>.json`
  so debug runs don't re-hit live endpoints.

The recommended posture for *new* sources is to look for an
official API or RSS feed first — see the survey doc above for
the legal frame and per-site verdicts.

## Adding a new locale or source plugin

1. Create `sources/<locale>-<board>.sh` (or `global-<board>.sh`).
2. Implement `fetch_postings()` per the contract.
3. Add a `case` arm in `scripts/run.sh` `list_sources()` for the
   new plugin's live-flag env var.
4. Reference the plugin in `config/filters.example.yaml` under
   `sources:` if it should be enabled by default.
5. PRs welcome.
