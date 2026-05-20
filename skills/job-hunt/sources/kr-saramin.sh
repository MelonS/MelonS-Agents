#!/usr/bin/env bash
# sources/kr-saramin.sh — Saramin (사람인) source plugin.
#
# Status (2026-05-20): MOCK-FALLBACK mode.  Live HTTP integration
# is gated on JH_SARAMIN_LIVE=1 AND SARAMIN_KEY=<token> (Saramin
# offers an OpenAPI for partners; key registration is via
# https://oapi.saramin.co.kr).
#
# Anti-bot posture: HIGH (for scrape path) / LOW (for OpenAPI path).
# This plugin uses the OpenAPI path; scrape fallback is NOT
# implemented and not planned — if a partner key is unavailable,
# the operator can prefer kr-wanted and kr-programmers instead.
#
# Mock fallback is the default.  Plugin contract: ../sources/README.md.

# shellcheck shell=bash

fetch_postings() {
  if [[ "${JH_SARAMIN_LIVE:-0}" == "1" ]]; then
    if [[ -z "${SARAMIN_KEY:-}" ]]; then
      echo "[kr-saramin] JH_SARAMIN_LIVE=1 but SARAMIN_KEY unset" >&2
      return 1
    fi

    # Saramin OpenAPI surface (as of latest public docs at
    # https://oapi.saramin.co.kr/guide):
    #
    #   GET https://oapi.saramin.co.kr/job-search
    #     ?access-key=<key>
    #     &keywords=<comma-joined>
    #     &loc_mcd=<region code, see Saramin location table>
    #     &job_type=1                 # 1 = 정규직
    #     &count=50
    #     &fields=keyword-code,industry-code
    #     &sort=pd                    # pd = posted-date desc
    #
    # Response:
    #   .jobs.job[] | {
    #     id, position{title, ind_cd, ...},
    #     company{detail{name}}, salary{...},
    #     opening-timestamp, expiration-date,
    #     url, ...
    #   }
    #
    # Operator validation step:
    #   curl -sS "https://oapi.saramin.co.kr/job-search?access-key=$SARAMIN_KEY&count=3&sort=pd" \
    #     | jq '.jobs.job[0]'
    # Compare field names to the assumed shape below and adjust.
    #
    # Rate-limit (per Saramin OpenAPI docs): 1000 calls/day, no
    # specific per-second cap.  Run once daily — no in-skill
    # parallelism needed.

    # Placeholder live call (commented; flip on after operator validates):
    # raw=$(curl -sS \
    #   "https://oapi.saramin.co.kr/job-search?access-key=$SARAMIN_KEY&count=50&sort=pd") || return 2
    # echo "$raw" | jq --arg fa "$(date -Iseconds)" '
    #   { source: "kr-saramin",
    #     fetched_at: $fa,
    #     postings: (.jobs.job // [] | map({
    #       title: .position.title,
    #       company: .company.detail.name,
    #       region: (.position.location.name // ""),
    #       posted_at: .["opening-timestamp"],
    #       url: .url,
    #       summary: (.position.["job-type"].name // ""),
    #       apply_url: .url
    #     })) }'

    echo "[kr-saramin] live path not yet operator-validated — see kr-saramin.sh comments" >&2
    return 1
  fi

  local fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"

  cat <<EOF
{
  "source": "kr-saramin",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "[Saramin OpenAPI mock] AI 엔지니어",
      "company": "Saramin Mock A",
      "region": "서울 강남구",
      "posted_at": "2026-05-19",
      "url": "https://www.saramin.co.kr/job/MOCK_SR_500",
      "summary": "Python/PyTorch.  LLM serving + agent infra.  (mock — needs SARAMIN_KEY + JH_SARAMIN_LIVE=1 for live OpenAPI.)",
      "apply_url": "https://www.saramin.co.kr/job/MOCK_SR_500"
    },
    {
      "title": "백엔드 개발자 (Python)",
      "company": "Saramin Mock B",
      "region": "서울 강남구",
      "posted_at": "2026-05-20",
      "url": "https://www.saramin.co.kr/job/MOCK_SR_501",
      "summary": "FastAPI / PostgreSQL.  AI 데이터 파이프라인 운영. (mock)",
      "apply_url": "https://www.saramin.co.kr/job/MOCK_SR_501"
    },
    {
      "title": "문제 해결사 (AI Agent)",
      "company": "Saramin Mock C",
      "region": "서울 강남구",
      "posted_at": "2026-05-20",
      "url": "https://www.saramin.co.kr/job/MOCK_SR_502",
      "summary": "쇼핑/이커머스 AI 에이전트 기획+개발+배포까지 직접 담당. PMF 탐색 cycle 주도. (mock)",
      "apply_url": "https://www.saramin.co.kr/job/MOCK_SR_502"
    }
  ]
}
EOF
}
