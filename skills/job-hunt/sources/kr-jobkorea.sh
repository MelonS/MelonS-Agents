#!/usr/bin/env bash
# sources/kr-jobkorea.sh — JobKorea (잡코리아) source plugin.
#
# Status (2026-05-21): **MOCK-ONLY (permanent).**
#
# Live HTTP integration is intentionally NOT implemented because
# JobKorea's policy posture forbids the only viable scraping path:
#
#  1. robots.txt (fetched 2026-05-21) has
#     `User-agent: *  Disallow: /Search/?stext=` — the search URL
#     is the *only* path that returns SSR cards.  The allowed
#     `/recruit/joblist` returns a SPA shell with zero embedded
#     postings (CardJob count: 0).
#  2. The 2017 Korean Supreme Court precedent in
#     **잡코리아 vs 사람인** (~9억 KRW damages) established that
#     scraping + redistributing job postings infringes
#     database-creator rights (저작권법 §93).  JobKorea is the
#     plaintiff in that precedent.
#  3. The Programmers footer crawler-prohibition language
#     (사이트의 모든 콘텐츠, 정보, UI, HTML 소스 등에 대한 무단
#     복제, 전송, 배포, 크롤링, 스크래핑 등의 행위를 거부하며,
#     이러한 행위는 관련 법령에 의해 엄격히 금지됩니다.) reflects
#     the common ToS posture across the major Korean job boards
#     (잡코리아 / 잡플래닛 / 인크루트), reinforcing the legal risk.
#
# JobKorea contains a large fraction of Korea's posting inventory,
# so deletion is not yet decided — the plugin remains as a clearly
# marked dead end so future operators understand why the path was
# closed.  See docs/research/job-sources-survey-2026-05-21.md for
# the full audit trail.
#
# Plugin contract: see ../sources/README.md.

# shellcheck shell=bash

fetch_postings() {
  local fetched_at
  fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"
  # JH_JOBKOREA_LIVE is honored only insofar as it surfaces the
  # closure notice; there is no live HTTP path.
  cat <<EOF
{
  "source": "kr-jobkorea",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "[MOCK-ONLY] 잡코리아 LIVE path — 정책상 비활성",
      "company": "—",
      "region": "—",
      "posted_at": "2026-05-21",
      "url": "https://www.jobkorea.co.kr/",
      "summary": "잡코리아 robots.txt가 /Search/?stext= (검색 SSR 경로)를 명시적으로 차단하고, 2017 잡코리아 vs 사람인 판례 (저작권법상 데이터베이스제작자 권리 침해 약 9억 KRW)로 크롤링+재배포는 민사 위험. docs/research/job-sources-survey-2026-05-21.md 참고. 대체: global-ats / global-remoteok / global-remotive / 사람인 OpenAPI.",
      "apply_url": "https://www.jobkorea.co.kr/"
    }
  ]
}
EOF
}
