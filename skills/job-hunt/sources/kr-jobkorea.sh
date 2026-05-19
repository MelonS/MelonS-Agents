#!/usr/bin/env bash
# sources/kr-jobkorea.sh — JobKorea (잡코리아) source plugin.
#
# Status (2026-05-20): MOCK-FALLBACK mode.  Live HTTP integration
# is gated on JH_JOBKOREA_LIVE=1.  JobKorea's listing surface is
# scrape-based (no public API), so the live path involves HTML
# parsing with a realistic User-Agent and rate-limit pacing.
#
# Anti-bot posture: MEDIUM.  JobKorea checks UA + request rate but
# does not deploy aggressive captcha by default.  Safe-zone:
# 500 ms between requests, max ~60 requests per session.
#
# Mock fallback is the default.  Plugin contract: ../sources/README.md.

# shellcheck shell=bash

fetch_postings() {
  if [[ "${JH_JOBKOREA_LIVE:-0}" == "1" ]]; then
    # Live path — operator-validated before flipping.
    #
    # Anticipated approach: GET https://www.jobkorea.co.kr/Search/?stext=<keyword>&careerType=4
    # Returns HTML; parse with pup or python+bs4.  Each posting
    # appears as a <article class="list-item"> with:
    #   - title:    .information-title-link text
    #   - company:  .corp-name-link text
    #   - region:   .information-etc .location text
    #   - posted:   .information-etc .date text
    #   - url:      .information-title-link @href (relative; prepend host)
    #
    # Operator validation step:
    #   curl -sS -A "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_6_0) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15" \
    #     'https://www.jobkorea.co.kr/Search/?stext=AI%20%EC%97%94%EC%A7%80%EB%8B%88%EC%96%B4&careerType=4' \
    #     | head -200
    # and verify the .list-item class is still present + structure.
    # Then write a pup-based or python-based extractor and replace
    # this block.
    #
    # Rate-limit: sleep 0.5 between successive requests; never
    # parallelize per-keyword fetches against this source.

    echo "[kr-jobkorea] live path not yet operator-validated — see kr-jobkorea.sh comments" >&2
    return 1
  fi

  local fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"

  cat <<EOF
{
  "source": "kr-jobkorea",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "AI 백엔드 엔지니어",
      "company": "JobKorea Mock A",
      "region": "서울 영등포구",
      "posted_at": "2026-05-19",
      "url": "https://www.jobkorea.co.kr/Recruit/MOCK_JK_400",
      "summary": "Python/Django 또는 FastAPI 기반 AI 백엔드. (mock — JH_JOBKOREA_LIVE=1 for live HTML scrape.)",
      "apply_url": "https://www.jobkorea.co.kr/Recruit/MOCK_JK_400?action=apply"
    },
    {
      "title": "풀스택 (LLM 통합)",
      "company": "JobKorea Mock B",
      "region": "서울 송파구",
      "posted_at": "2026-05-18",
      "url": "https://www.jobkorea.co.kr/Recruit/MOCK_JK_401",
      "summary": "Node.js + Python LLM agent layer. (mock)",
      "apply_url": "https://www.jobkorea.co.kr/Recruit/MOCK_JK_401?action=apply"
    }
  ]
}
EOF
}
