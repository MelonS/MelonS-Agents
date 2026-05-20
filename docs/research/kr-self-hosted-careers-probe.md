# Korean self-hosted careers — direct-scrape feasibility probe

**Date**: 2026-05-21 ~04:55 KST autonomous run
**Driver**: operator's afternoon insight that the digest is biased
toward global ATS results; KR-domestic companies that don't use
Greenhouse / Ashby / Lever are invisible to the skill.  Looked at
whether the major KR companies' own careers sites can be scraped
directly (bypassing ATS).

## Sites probed

| Domain | DNS | robots.txt | listing path | SSR cards |
|---|---|---|---|---|
| `career.kakaocorp.com` | ❌ NXDOMAIN | n/a | n/a | n/a |
| `recruit.kakao.com` | ❌ NXDOMAIN | n/a | n/a | n/a |
| `tossteam.com` | ❌ NXDOMAIN | n/a | n/a | n/a |
| `toss.im/career/jobs` | ✅ | `Allow: /career/jobs` | 200, 98 KB | **0 cards (SPA)** |
| `recruit.navercorp.com` | ✅ | (no `*` rules) | 200, 125 KB | **0 cards (SPA)** |
| `careers.woowahan.com` | ❌ NXDOMAIN | n/a | n/a | n/a |
| `www.linecorp.com/en/career` | ✅ | (Cloudflare 403) | 403 | n/a |
| `careers.linecorp.com/ko/jobs` | ✅ | `Allow: /` (mostly) | 200, 33 KB | **0 cards (SPA)** |
| `careers.toss.im` | ❌ NXDOMAIN | n/a | n/a | n/a |
| `tossbank.com/career` | ✅ | `Allow: /` | 404 (path wrong) | n/a |
| `careers.yanolja.in` | ❌ NXDOMAIN | n/a | n/a | n/a |
| `careers.ridi.com` | ❌ NXDOMAIN | n/a | n/a | n/a |
| `team.dunamu.com` | ❌ NXDOMAIN | n/a | n/a | n/a |
| `careers.bucketplace.com` | ❌ NXDOMAIN | n/a | n/a | n/a |

## Headline

**Every reachable KR-major careers site is a SPA.**  The companies
that adopted a modern static-site careers page either don't expose
it on the obvious domain (Kakao / Woowahan / Yanolja / Ridi /
Dunamu / Bucketplace all returned NXDOMAIN from the probed
addresses), or they SSR a shell with zero job cards embedded
(Toss / Naver / Line).

A client-side fetch endpoint exists on each of these — that's how
the SPA renders jobs — but it's undocumented and brittle, *and*
robots.txt's intent is clearly "browsers welcome, automation
discouraged" (Toss explicitly disallows individual `gh_jid=N`
detail URLs even though `Allow: /career/jobs` covers the index).

## What this means for the skill

1. **Direct-scrape coverage of KR-major companies is not viable.**
   Investment in per-company plugins for Toss / Naver / Kakao /
   Line / 우아한형제 / Yanolja / Ridi / Dunamu / Bucketplace would
   be high-effort + brittle + ethically grey.
2. **The Saramin OpenAPI is the right path.**  These companies all
   post on Saramin (legally required disclosure plus marketing
   reach).  One key fetches them all through a documented,
   keyrate-limited, ToS-compliant interface.
3. **theteams.kr is a partial backstop** for KR-startup / 강소기업
   coverage — its sitemap + permissive robots make the recently
   shipped `kr-theteams` plugin a clean addition.

## Companies that DID expose an ATS (already in `ats-boards.example.yaml`)

These four KR companies use Greenhouse and are reachable today:

- Coupang (486 jobs)
- Daangn / Karrot (44 jobs)
- Sendbird (18 jobs)
- Krafton (54 jobs)

For everyone else, Saramin is the unblock.

## Decision

- Do **not** invest in per-company scrapers for the SPA-only sites.
- Plant a comment in `sources/README.md` so a future contributor
  doesn't re-probe the same ground.
- Wait on Saramin key (operator-pending as of 2026-05-21 ~14:30 KST).
