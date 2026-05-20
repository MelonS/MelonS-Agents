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

    # Saramin OpenAPI spec (verified 2026-05-21 against
    # https://oapi.saramin.co.kr/guide/job-search):
    #
    # GET https://oapi.saramin.co.kr/job-search
    #   ?access-key=<key>                         (required)
    #   &keywords=<term1,term2,...>               (OR-search across company / title / industry)
    #   &count=100                                (max 110, default 10)
    #   &sort=pd                                  (pd = posting-date desc, default)
    #   &fields=posting-date,expiration-date      (adds ISO 8601 date fields)
    #
    # Response shape:
    #   { "jobs": { "count": N, "start": 0, "total": "N",
    #               "job": [ { id, url, active,
    #                          company.detail.name,
    #                          position.title,
    #                          position.location.name,
    #                          position.job-type.name,
    #                          position.experience-level.name,
    #                          posting-date / opening-timestamp,
    #                          ...
    #                        } ] } }
    #
    # Rate-limit: 1000 calls/day per Saramin OpenAPI docs; no
    # per-second cap.  This plugin issues one call per run.

    # Build keyword query from orchestrator's expanded include
    # list.  Cap at first 10 to stay within URL length and keep
    # search broad enough.
    local kw_query
    if [[ -n "${JH_KEYWORDS_INCLUDE:-}" ]]; then
      kw_query=$(printf '%s' "$JH_KEYWORDS_INCLUDE" | awk -F',' '{
        n = (NF > 10 ? 10 : NF)
        for (i = 1; i <= n; i++) {
          gsub(/^[ \t]+|[ \t]+$/, "", $i)
          if (length($i)) printf "%s%s", (i>1?",":""), $i
        }
      }')
    else
      kw_query="AI,LLM,agent"
    fi

    # URL-encode minimally (commas + Korean stay; encode spaces).
    kw_query=${kw_query// /%20}

    local raw
    raw=$(/usr/bin/curl -sS --max-time 12 \
      -A "MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
      "https://oapi.saramin.co.kr/job-search?access-key=${SARAMIN_KEY}&keywords=${kw_query}&count=100&sort=pd&fields=posting-date,expiration-date") || {
        echo "[kr-saramin] curl failed — falling back to mock" >&2
        _saramin_mock "${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null)}"
        return 0
      }

    if ! echo "$raw" | jq -e '.jobs' >/dev/null 2>&1; then
      echo "[kr-saramin] malformed response (no .jobs key) — first 200 chars:" >&2
      printf '%s' "$raw" | head -c 200 >&2
      echo "" >&2
      echo "[kr-saramin] falling back to mock" >&2
      _saramin_mock "${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null)}"
      return 0
    fi

    echo "$raw" | jq --arg fa "$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)" '
      { source: "kr-saramin",
        fetched_at: $fa,
        postings: (.jobs.job // [] | map({
          title: (.position.title // ""),
          company: (.company.detail.name // ""),
          region: ((.position.location.name // "") | tostring),
          posted_at: ((.["posting-date"] // .["opening-timestamp"] // "") | tostring | .[0:10]),
          url: (.url // ""),
          summary: ("사람인 — " + (.company.detail.name // "") + " — " + (.position.title // "")
                  + (if .position["experience-level"].name then " · 경력: " + (.position["experience-level"].name | tostring) else "" end)
                  + (if .position["job-type"].name then " · " + (.position["job-type"].name | tostring) else "" end)
                  + (if .position["required-education-level"].name then " · " + (.position["required-education-level"].name | tostring) else "" end)
                  ),
          apply_url: (.url // "")
        })) }'
    return 0
  fi

  _saramin_mock "${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"
}

_saramin_mock() {
  local fetched_at="$1"

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
