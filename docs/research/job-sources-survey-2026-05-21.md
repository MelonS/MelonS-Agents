# Job-source survey — 2026-05-21

**Mission**: operator asked (2026-05-20 ~23:50 KST) to enumerate
*every* place job postings appear and test whether each one can
be fetched **safely / legally / reliably**. Deliverable by
2026-05-21 10:00 KST.

**Method**: for each candidate site, probe
  1. `robots.txt` (`User-agent: *` policy)
  2. A representative job-list URL (with browser-shape UA, ~10 s timeout)
  3. Where the HTTP response is 200, inspect for SSR cards / embedded
     JSON / RSS / public API; record content size as a fingerprint
     for SSR-vs-SPA placeholder distinction.

**Legal frame** anchored on the Korean precedent **잡코리아 vs 사람인
(2017)** — a Supreme Court case finding scraping + republishing
of job-board content infringed database-creator rights (~9억 KRW
damages).  Inheriting from that case the working test is:

- **재게시 (republishing) → 위험.** If our skill emits scraped
  postings into a public artifact (committed digest), that is the
  fact pattern the precedent litigated.
- **개인용 digest (private) → 약관 위반에 머무름.** Civil ToS
  breach exists but rarely litigated against personal use.
- **robots.txt 명시 차단을 위반하면** 약관 위반 + 신뢰 손상.  도덕
  적으로도 안 함.
- **공식 API / RSS / sitemap을 통한 접근** → 명시적 허용이므로 명백
  안전.

Per [[no-pii-in-repo]] this survey treats the repo as public-facing.
A site that requires scraping + republishing is graded RED even if
technically possible.

---

## Tier 1 — official APIs / RSS (key-free, no ToS friction)

These return structured data on a documented endpoint.  No
authentication, no anti-bot wall, no ambiguity.

| Source | Endpoint | Auth | Verified | Volume |
|---|---|---|---|---|
| **RemoteOK** | `https://remoteok.com/api` | none | 200, 151 KB JSON | ~50 fresh remote postings/day |
| **Remotive** | `https://remotive.com/api/remote-jobs` | none | 200, 48 KB JSON | ~100 active remote postings |
| **WeWorkRemotely** | `https://weworkremotely.com/categories/<cat>.rss` | none | 200, 40 KB RSS | ~30 per category |
| **HN Algolia** | `https://hn.algolia.com/api/v1/search_by_date?tags=story,author_whoishiring` | none | 200, 29 KB JSON | monthly "Who's Hiring" thread → 100s of postings as comments |

These are **immediately wirable** as plugins.  No operator key, no
re-validation as endpoints drift.  All four are conventional
public-facing endpoints.

## Tier 2 — ATS public board APIs (key-free, employer-mediated)

When a company uses a SaaS ATS, the ATS exposes a per-board public
JSON endpoint that the company's "careers" page consumes.  Anyone
can hit it (it's how the careers page renders).  No auth required.

### Greenhouse (`boards-api.greenhouse.io/v1/boards/<token>/jobs`)

**robots.txt**: `Disallow: /embed/` only — `*` policy is otherwise
open.  No anti-bot.

**Verified boards** (probe 2026-05-21 ~00:30 KST):

| Company | Status | JSON size |
|---|---|---|
| Anthropic | 200 | 22 KB |
| Airbnb | 200 | 184 KB |
| Stripe | 200 | 25 KB |
| Robinhood | 200 | 94 KB |
| Reddit | 200 | 283 KB |
| Discord | 200 | 62 KB |
| Dropbox | 200 | 92 KB |
| Lyft | 200 | 97 KB |
| Pinterest | 200 | 144 KB |
| Cloudflare | 200 | 130 KB |
| Datadog | 200 | 554 KB |
| MongoDB | 200 | 872 KB |
| GitLab | 200 | 118 KB |
| Elastic | 200 | 179 KB |
| Figma | 200 | 103 KB |
| ScaleAI | 200 | 141 KB |
| Instacart | 200 | 107 KB |
| OpenAI / Coinbase / HashiCorp / Snowflake / Linear / HuggingFace / Scale / DoorDash | 404 | — (not on Greenhouse) |

### Ashby (`api.ashbyhq.com/posting-api/job-board/<slug>`)

No robots.txt.  Effectively the AI-startup ATS of choice in 2025.

| Company | Status | JSON size |
|---|---|---|
| OpenAI | 200 | 4 MB |
| Cursor | 200 | 667 KB |
| Perplexity | 200 | 469 KB |
| Ramp | 200 | 1.8 MB |
| Notion | 200 | 1.9 MB |
| ElevenLabs | 200 | 1.9 MB |
| Linear | 200 | 293 KB |
| Pika | 200 | 51 KB |
| Anthropic / Vercel / Replicate / RunwayML / Glean / Midjourney | 404 or empty | — (different ATS) |

### Lever (`api.lever.co/v0/postings/<slug>?mode=json`)

`Allow: /` `Crawl-delay: 1`.

| Company | Status | JSON size |
|---|---|---|
| Spotify | 200 | 1.9 MB |
| AngelList | 200 | 203 KB |
| Lever demo | 200 | 2.4 MB |
| Netflix | timeout | — (might be Lever-hosted but slow) |
| Box / Eventbrite / Yelp / Quizlet / Ramp / Medium / Plaid | 404 or empty | — (different ATS) |

### Workable / Workday — operator-validated only

- Workable `api.workable.com/spi/v3/accounts/<slug>/jobs` returns
  **401** without an account API token.  Operator decision per-board.
- Workday boards are per-company subdomains (`<co>.wd1.myworkdayjobs.com`)
  with an undocumented but stable XHR — investigated below if
  operator wants it.

### Strategy for ATS layer

Rather than enumerate every company manually, the v1 ATS plugin
would take a curated **list of board tokens** (one per ATS) and
fan-out fetches.  Adding a new company = appending one line.
This is how aijobs.net / builtin.com / aggregator sites do it under
the hood.

**Initial curated list (AI-stack focus)** — operator picks:

```
greenhouse:  anthropic stripe airbnb scaleAI figma cloudflare datadog mongodb
ashby:       openai cursor perplexity notion ramp linear elevenlabs pika
lever:       spotify angellist
```

That gives ~20 high-signal boards → 100s of fresh AI/SaaS postings
on every run.  All Tier-2 sources are zero-friction (no key, no
ToS friction, no anti-bot evasion).  Best ROI in this entire
survey.

## Tier 3 — Korean sites with explicit listing allow

These have robots.txt that *allows* the listing path AND return
SSR content (cards present in the response HTML).  Re-distribution
in a *public* digest is still risky per the 2017 precedent, but
private digest use is on solid ground.

| Source | Endpoint | robots.txt | SSR check | Notes |
|---|---|---|---|---|
| **워크넷** (정부) | `/empInfo/empInfoSrch/list/dtlEmpSrchList.do` | no `*` rule encountered; only Googlebot specific | 200, **336 KB** | Government public-employment service; data is by definition public.  No anti-bot.  Best legal posture of any Korean site. |
| **점핏** | `jumpit.saramin.co.kr/positions?sort=rdate&jobCategory=1` | `*` Disallow only for resume/auth/profile; positions explicitly OK | 200, ~16 KB | Saramin subsidiary, IT-focused. |
| **리멤버 커리어** | `career.rememberapp.co.kr/jobs` + 4 sitemaps | `Allow: /job/`, sitemaps include `sitemap-jobs.xml` redirecting to `career-cdn.rememberapp.co.kr/upload/sitemap/job_posting.xml` (40 KB) | sitemap-rich | Strongly listing-friendly. |
| **더팀스** (theteams.kr) | `/sitemap.xml` → `/gz_sitemap/recruit-1.xml…3.xml` | `Allow: /` (no restrictions) | 200 | Cleanest robots posture of any Korean site. |
| **링커리어** | `linkareer.com/list/recruit` | `Allow: /` plus stem/learn private path Disallows | 200, 41 KB | Includes industry filters. |
| **랄릿** (rallit.com) | `rallit.com/?q=AI` | `Allow: /resumes` listed; no listing-page Disallow | 200, 32 KB | IT/스타트업 채용. |
| **디스퀴엣** (disquiet.io) | `disquiet.io/jobs` | `Allow: /jobs` explicit | 200, 2 KB (SPA placeholder; need API endpoint discovery) | Sitemap rich. |
| **당근 메인** (daangn.com) | `daangn.com/kr/jobs/` | robots posture not fully fetched | 200, **724 KB** SSR | Main Daangn site (not the alba subdomain).  Substantial SSR. |

**Caution on Korean Tier-3**: even with robots.txt allow, the
2017 정밀 precedent is jurisdiction-specific.  A skill that
**publishes a daily committed digest** from these sources to a
public repo is arguably reproducing their database.  Recommended
posture: keep `records/jobs/` strictly gitignored (already is),
and document Tier-3 plugins as **personal-use only — not for
public re-distribution**.

## Tier 4 — paid / partner API path

Official endpoints exist but require business validation.  Operator
must register; no scraping involved.

| Source | Endpoint | What's needed |
|---|---|---|
| **사람인 OpenAPI** | `oapi.saramin.co.kr/job-search` | Free signup at `oapi.saramin.co.kr` → `SARAMIN_KEY`; daily quota applies |
| **원티드 (Wanted Pre-Onboarding API)** | per company partnership | Partner agreement, not a casual signup |

Sites in this tier are **the cleanest legal posture** — operator
sees themselves as the data subject, granted access by the source.

## Tier 5 — blocked or impractical

Sites where the listing path is either explicitly disallowed,
walled by anti-bot, or returns nothing usable.

| Source | What blocks it |
|---|---|
| **LinkedIn** | robots.txt: *"use of robots or other automated means... is strictly prohibited"*.  The hiQ v LinkedIn US case ended badly for hiQ.  Personal-use only, no automation under any circumstance. |
| **잡코리아** (jobkorea.co.kr) | robots.txt explicitly `Disallow: /Search/?stext=` (the only path that SSR-renders the cards).  The allowed `/recruit/joblist` returns 0 SSR cards (SPA).  Also the named plaintiff in the 2017 precedent. |
| **프로그래머스 채용** | **Service discontinued 2025-05-19.**  `career.programmers.co.kr` is NXDOMAIN.  Plugin should be removed. |
| **잡플래닛** | `/jobs?query=*` and `/searches/list?query=*` return 403 (CloudFront anti-bot).  robots.txt allows Naver/Daum search bots only. |
| **원티드 web** | `/jobs` and probe paths return 403 (CloudFront).  Anti-bot. |
| **Indeed KR** | 403 on first request.  Indeed enforces strong anti-bot. |
| **Wellfound (AngelList Talent)** | 403.  Anti-bot. |
| **Stack Overflow Jobs** | **Service discontinued 2022.**  robots.txt also `Disallow: /` for `*`.  Plugin would never load. |
| **인크루트** | robots.txt `User-agent: * Disallow: /`.  Full block. |
| **알바몬** | All probed paths 404; appears to be a hard SPA with non-discoverable URL structure.  Sitemap is 186-byte stub. |
| **알바천국** | All probed paths bounce to `/error/error_msg.asp`.  Anti-bot or anti-non-browser. |
| **로켓펀치** | CloudFront 403.  Anti-bot. |
| **Otta / Welcome to the Jungle** | `/jobs` redirects through `welcometothejungle.com` returning 404; UK-anchored. |

## Synthesis

**The cleanest plugin set** (no key, no ToS friction, mostly
zero-trust):

1. **`global-ats`** — single plugin, parameterized by a curated
   board-token list across Greenhouse + Ashby + Lever.  ~20 boards
   covers most of the AI-stack.  **Probably the single highest-value
   data source.**
2. **`global-remote`** — RemoteOK + Remotive + WeWorkRemotely RSS
   merged into one feed.  All zero-auth.
3. **`global-hn-whoshiring`** — HN Algolia search for current month's
   "Who is hiring?" thread, then individual comment extraction.
4. **`kr-worknet`** — government public-employment data, no robots
   restriction.
5. **`kr-saramin`** (existing) — keep, switch to OpenAPI key path
   (operator-validated).  Already scaffolded.

**Plugins to deprecate**:

- `kr-programmers` (service shut down).
- `kr-jobkorea` (precedent + robots, can't be wired without
  the 2017 case applying).

**Plugins to add (in priority order)**:

- `global-ats-greenhouse` / `global-ats-ashby` / `global-ats-lever`
  (or one merged `global-ats` plugin).
- `global-remoteok`, `global-remotive`, `global-wwr-rss`.
- `global-hn-whoshiring`.
- `kr-worknet` (government).
- (Optional, kr-only-private digest): `kr-jumpit`, `kr-remember-career`,
  `kr-theteams`, `kr-rallit`, `kr-linkareer`, `kr-disquiet`,
  `kr-daangn`.

## Next steps if operator approves

1. Remove `kr-programmers` plugin + update SKILL.md status table.
2. Mark `kr-jobkorea` as deprecated permanently (precedent reason
   committed to plugin file header).
3. Add **`global-ats`** plugin (Greenhouse + Ashby + Lever) — this
   is the single biggest unlock; ~200 lines of bash + jq, no auth.
4. Add **`global-remoteok` / `global-remotive` / `global-hn-whoshiring`** —
   each is ~50 lines.
5. Add **`kr-worknet`** government plugin.
6. Mark `kr-jumpit` / `kr-remember-career` / `kr-theteams` / etc. as
   **private-digest-only**.  Keep `records/jobs/` gitignored.
7. README EN/KO cadence batch when 3+ new plugins are wired and
   tested end-to-end.

---

_Probes recorded under `/tmp/jobsurvey/batch[1-6].log`.  Cumulative
robots.txt + endpoint evidence preserved at probe time
(2026-05-20 ~23:50 → 2026-05-21 ~00:35 KST)._
