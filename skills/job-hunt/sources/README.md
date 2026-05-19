# `skills/job-hunt/sources/` — per-board fetch plugins

Each file in this directory is a single Bash plugin that knows
how to fetch postings from one job board and emit normalized JSON
on stdout.

## File naming

`<locale>-<board>.sh` — e.g. `kr-wanted.sh`, `kr-saramin.sh`,
`us-linkedin.sh`.

The `<locale>` prefix is mandatory.  Today only `kr-*` plugins
will ship; future PRs may add other locales without touching the
orchestrator.

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

## Planned sources (Korean)

Order is "easiest to implement first":

| Source | Auth | Anti-bot | API quality |
|---|---|---|---|
| `kr-wanted.sh` | API key (`WANTED_API_KEY`) | low | clean JSON API |
| `kr-programmers.sh` | none / login optional | low | GraphQL; dev-only |
| `kr-jobkorea.sh` | scrape | medium | HTML parse |
| `kr-saramin.sh` | scrape | high | HTML + anti-bot |

`kr-wanted` ships first when implementation begins.  Saramin lands
last because anti-bot tuning needs operator-supervised live testing.

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

## Status

All entries below are placeholders.  Live implementation begins
after operator review of the scaffold.

- [ ] `kr-wanted.sh`
- [ ] `kr-programmers.sh`
- [ ] `kr-jobkorea.sh`
- [ ] `kr-saramin.sh`
