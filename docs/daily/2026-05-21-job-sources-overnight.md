# 2026-05-21 overnight — job-source survey + 5 live-ready plugins

> Autonomous run.  Operator instruction 2026-05-20 ~23:50 KST:
> "암튼 난 잘테니 공고올라는오는 모든곳을 다찾아보고 가져올수있는지
> 없는지 확인해봐 낼 아침10시까지 쉬지않고 해".  Deliverable
> by 10:00 KST on 2026-05-21.

## Summary in one line

`job-hunt` skill goes from "scaffolded across 4 KR boards in mock
mode" → "5 live-ready plugins pulling 5,000+ real postings without
any operator key".

## What landed

Three commits on `main` overnight:

| Commit | What |
|---|---|
| `b3789ba` | `docs/research/job-sources-survey-2026-05-21.md` — robots + legality + endpoint audit of 30+ candidate boards.  Tier 1-5 classification by legal posture. |
| `58a2b58` | First 3 plugins: `global-ats` (Greenhouse + Ashby + Lever 27-board curated list), `global-remoteok`, `global-remotive`.  `kr-jobkorea` + `kr-programmers` converted to permanent-mock with precedent reason in headers.  Orchestrator argv-limit fix (--slurpfile pattern).  Schema accepts `global-*` prefix. |
| `a6c39c4` | 2 more plugins: `global-hn-whoshiring` (HN monthly thread via Algolia HN Search) + `kr-worknet` (정부 공공고용서비스 SSR parser). |
| `91c0a40` | README EN+KO + roadmap Done cadence batch. |

## End-to-end live test

```
JH_GLOBAL_ATS_LIVE=1 JH_GLOBAL_REMOTEOK_LIVE=1 \
JH_GLOBAL_REMOTIVE_LIVE=1 JH_GLOBAL_HN_LIVE=1 \
JH_WORKNET_LIVE=1 \
skills/job-hunt/scripts/run.sh --seed "Problem Solver" --quiet
```

Per-source raw postings (Problem Solver filter applied at orchestrator):

| Plugin | Raw | Matched |
|---|---:|---:|
| `global-ats` | 5,019 | 171 |
| `global-hn-whoshiring` | 334 | 28 |
| `global-remoteok` | 99 | 1 |
| `global-remotive` | 19 | 0 |
| `kr-worknet` | 3 | 0 |
| **Total** | **5,474** | **200** |

Top matches (first 10 of 200) — all real, all clickable:

- Applied AI Engineer · Anthropic · Tokyo
- Applied AI Engineer · Anthropic · London
- Applied AI Engineer, Beneficial Deployments · Anthropic · Bangalore
- Applied AI Engineer, Enterprise Tech · Anthropic · SF / NYC / Seattle
- Applied AI Engineer, Life Sciences · Anthropic · SF / NYC
- Enterprise Account Executive, Industries · Anthropic · Sydney
- Forward Deployed Engineer, Applied AI · Anthropic · Boston / NYC / SF / Seattle / DC
- Manager, Forward Deployed Engineering · Anthropic · SF / NYC / Seattle
- Applied AI Engineer, Enterprise · Scale AI · London
- Applied AI Engineer, Enterprise GenAI · Scale AI · SF / NYC

Full digest at `records/jobs/2026-05-21/digest.md` (gitignored —
view locally).

## What did NOT pass

Tier 5 (blocked / impractical) from the survey — these all got
filed in the survey doc with their specific block reasons:

- **LinkedIn**: robots.txt prohibits automation outright + hiQ
  precedent + ToS.
- **잡코리아**: robots `Disallow /Search/?stext=` (only SSR path);
  also the 2017 잡코리아 vs 사람인 precedent (~9억 KRW for scrape +
  redistribute).  Permanent-mock conversion landed.
- **프로그래머스 채용**: service permanently closed 2025-05-19;
  career.programmers.co.kr is NXDOMAIN.  Deprecation stub landed.
- **잡플래닛 / 원티드 web / Indeed KR / Wellfound / 로켓펀치**:
  CloudFront-fronted, return 403 to non-browser fetch.
- **Stack Overflow Jobs**: shut down 2022.
- **인크루트**: robots.txt `User-agent: * Disallow: /`.
- **알바몬 / 알바천국**: SPA without discoverable URL structure;
  alba.co.kr bounces to /error/.
- **점핏**: SSR returns SPA placeholder (CardJob count = 0);
  client-side endpoint not discoverable from HTML alone.
- **디스퀴엣**: same — SPA, sitemap has only `posts-*.xml` + no
  jobs split.

## Tier 4 (operator-key path, no scraping risk)

Still scaffolded, key required:

- **사람인 OpenAPI** — free signup at `oapi.saramin.co.kr`, then
  flip `JH_SARAMIN_LIVE=1` + `SARAMIN_KEY`.  Recommended next
  operator action.
- **Wanted** — partner key; operator decides whether the partner
  flow is worth pursuing for personal-digest use.

## Tests

68/68 PASS (32 smoke + 26 edge + 10 schema — schema expanded to
validate the 5 new plugins; smoke updated for new mock-counts of
the 2 deprecated KR stubs).

## Recommended next ops (for operator decision when awake)

1. **Confirm/edit** `config/ats-boards.example.yaml` — current
   27-board list emphasizes AI labs (Anthropic / OpenAI / Cursor /
   Perplexity / Scale AI / Notion / Cursor / Ramp / Linear) +
   big-tech SaaS (Stripe / Datadog / MongoDB / etc).  Operator can
   add/remove tokens; one line each.  Also setable via env vars:
   `JH_ATS_GREENHOUSE_BOARDS=anthropic,openai,...`.
2. **Saramin key** — 5-10 min signup at oapi.saramin.co.kr unlocks
   the kr-saramin plugin's live path.
3. **fit-score + cover-letter utility** — once profile.md is
   filled in and `JH_FIT_SCORE_LIVE=1`, the digest gets per-posting
   Claude-driven match scores for the 200 matched postings.

## Files committed

```
docs/research/job-sources-survey-2026-05-21.md  (b3789ba — 257 LoC)
skills/job-hunt/config/ats-boards.example.yaml  (58a2b58 — 49 LoC)
skills/job-hunt/sources/global-ats.sh           (58a2b58 — 210 LoC)
skills/job-hunt/sources/global-remoteok.sh      (58a2b58 —  77 LoC)
skills/job-hunt/sources/global-remotive.sh      (58a2b58 —  78 LoC)
skills/job-hunt/sources/global-hn-whoshiring.sh (a6c39c4 — 175 LoC)
skills/job-hunt/sources/kr-worknet.sh           (a6c39c4 — 130 LoC)
```

Plus updates to orchestrator + SKILL.md + sources/README.md +
filters.example.yaml + smoke.sh + schema + README EN/KO + roadmap.

## Connections to existing work

- `[[scaffold-pattern]]`: every new plugin defaults to mock fallback,
  live path behind `JH_*_LIVE=1` env flag.  Composes with operator's
  prior 5 utility scaffolds (fit-score / cover-letter / etc).
- `[[no-pii-in-repo]]`: `config/ats-boards.example.yaml` is generic
  (AI labs + major SaaS); operator can override per-machine via
  `ats-boards.yaml` (gitignored) or env vars.
- `[[goal-vs-queue]]`: doesn't change goal.md (Skill #2 deliverable
  subgoal still gated on "operator filter + `records/jobs/<date>/digest.md`
  with personal-fit posting") but materially advances the path —
  digest can now be filled with real postings.
- `[[branch-strategy-strict]]`: the first cherry-picked commit
  (58a2b58) landed on main correctly after a mid-work correction;
  the wrong-branch slip is in the session transcript for the
  archive.
