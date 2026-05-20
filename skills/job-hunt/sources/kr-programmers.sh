#!/usr/bin/env bash
# sources/kr-programmers.sh — Programmers (프로그래머스) source plugin.
#
# Status (2026-05-20): MOCK-FALLBACK mode.  When `JH_PROGRAMMERS_LIVE=1`
# is set, this plugin attempts the live programmers.co.kr job
# listing.  Programmers' job listing surface is public (no auth
# required for browsing) but the precise GraphQL / REST shape used
# by the current site changes periodically — live integration is
# gated on operator validation of the endpoint as of the moment
# of use.
#
# Mock fallback is the default.  Live integration is documented
# below for operator validation.
#
# Plugin contract: see ../sources/README.md.

# shellcheck shell=bash

fetch_postings() {
  # ----- live mode (gated by env var) -----
  if [[ "${JH_PROGRAMMERS_LIVE:-0}" == "1" ]]; then
    # Anticipated approach: Programmers exposes a public-facing
    # listing at https://career.programmers.co.kr/job — the page
    # data is hydrated client-side from /api/job_postings (subject
    # to change).  Two viable approaches:
    #
    #   A) Public REST: GET https://career.programmers.co.kr/api/job_positions
    #      Query params (typical):
    #        career=junior,mid,senior
    #        page=1
    #        order=recent
    #        positions=back,front,full,ai
    #
    #   B) Sitemap walk: GET https://career.programmers.co.kr/sitemap_job_positions.xml
    #      and pull each <loc> into the digest as a candidate.
    #
    # (A) is preferred for fresh data but is brittle to layout
    # changes.  (B) is more stable but provides only URLs (no
    # title/company without a follow-up fetch per posting).
    #
    # Anticipated normalization (path A):
    #   .data[] | {
    #     title: .title,
    #     company: .company.name,
    #     region: (.locations[0].name // "Remote"),
    #     posted_at: .created_at,
    #     url: ("https://career.programmers.co.kr/job_positions/" + (.id|tostring)),
    #     summary: .summary,
    #     apply_url: ("https://career.programmers.co.kr/job_positions/" + (.id|tostring) + "/apply")
    #   }
    #
    # Operator validation step: run
    #   curl -sS -A "MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
    #     'https://career.programmers.co.kr/api/job_positions?page=1&order=recent' | jq '.[0]'
    # Compare field names against the assumed shape above, then
    # uncomment the live curl below.

    # raw=$(curl -sS \
    #   -A "MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
    #   "https://career.programmers.co.kr/api/job_positions?page=1&order=recent") || return 2
    # echo "$raw" | jq --arg fa "$(date -Iseconds)" '
    #   { source: "kr-programmers",
    #     fetched_at: $fa,
    #     postings: (. | map({
    #       title: .title,
    #       company: (.company.name // .company // "Unknown"),
    #       region: ((.locations // []) | map(.name) | join(", ") // "Remote"),
    #       posted_at: (.created_at // ""),
    #       url: ("https://career.programmers.co.kr/job_positions/" + (.id|tostring)),
    #       summary: (.summary // ""),
    #       apply_url: ("https://career.programmers.co.kr/job_positions/" + (.id|tostring) + "/apply")
    #     })) }'

    echo "[kr-programmers] live path not yet operator-validated — see kr-programmers.sh comments" >&2
    return 1
  fi

  # ----- mock fallback (default) -----
  local fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"

  cat <<EOF
{
  "source": "kr-programmers",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "AI Engineer — LLM Agent Systems",
      "company": "Programmers Mock A",
      "region": "서울 마포구",
      "posted_at": "2026-05-19",
      "url": "https://career.programmers.co.kr/job_positions/MOCK_P_300",
      "summary": "LLM agent orchestration, RAG pipelines, Python. 시니어. (mock)",
      "apply_url": "https://career.programmers.co.kr/job_positions/MOCK_P_300/apply"
    },
    {
      "title": "Backend Developer (Python/FastAPI)",
      "company": "Programmers Mock B",
      "region": "원격",
      "posted_at": "2026-05-20",
      "url": "https://career.programmers.co.kr/job_positions/MOCK_P_301",
      "summary": "Python FastAPI, PostgreSQL, AI 통합 백엔드. 재택 가능. (mock)",
      "apply_url": "https://career.programmers.co.kr/job_positions/MOCK_P_301/apply"
    },
    {
      "title": "Founding Engineer",
      "company": "Programmers Mock C",
      "region": "서울 강남구",
      "posted_at": "2026-05-19",
      "url": "https://career.programmers.co.kr/job_positions/MOCK_P_302",
      "summary": "Early-stage AI 스타트업 첫 엔지니어.  Ship MVPs, iterate to PMF, agent 시스템 빌드 + 운영 전부 owns. (mock)",
      "apply_url": "https://career.programmers.co.kr/job_positions/MOCK_P_302/apply"
    }
  ]
}
EOF
}
