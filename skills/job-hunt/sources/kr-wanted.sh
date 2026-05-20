#!/usr/bin/env bash
# sources/kr-wanted.sh — Wanted (원티드) source plugin.
#
# Status (2026-05-20): MOCK-FALLBACK mode.  When the WANTED_API_KEY
# environment variable is unset, this plugin returns deterministic
# synthetic data shaped like Wanted's real response — useful for
# pipeline development without touching live endpoints.
#
# Live integration is intentionally left disabled in this commit
# because the live HTTP path needs:
#   1. An operator-supplied API key (Wanted issues these to
#      partners; the public site flow requires a session cookie).
#   2. Validation of the actual endpoint surface (Wanted's
#      partner API surface is not fully publicly documented;
#      response shape needs to be confirmed before live use).
#   3. Rate-limit / pagination tuning against a live account.
#
# The "live" branch below is fully wired but exits early with a
# clear message until the operator sets `JH_WANTED_LIVE=1` AND
# `WANTED_API_KEY=<token>`.  Mock fallback is the default.
#
# Plugin contract: see ../sources/README.md.

# shellcheck shell=bash

fetch_postings() {
  # ----- live mode (gated by env vars) -----
  if [[ "${JH_WANTED_LIVE:-0}" == "1" ]]; then
    if [[ -z "${WANTED_API_KEY:-}" ]]; then
      echo "[kr-wanted] JH_WANTED_LIVE=1 but WANTED_API_KEY unset" >&2
      return 1
    fi

    # Live path — intentionally not executed until operator has
    # validated the endpoint surface.  Documented here so the
    # implementation step is short and review-able rather than
    # invented under autonomous overnight conditions.
    #
    # Anticipated request shape (to be confirmed against Wanted's
    # current API; the official partner endpoints have changed
    # before):
    #
    #   GET https://api.wanted.co.kr/v4/jobs
    #     ?country=kr
    #     &job_sort=job.latest_order
    #     &locations=$JH_REGIONS_AS_CODE
    #     &years=0,5
    #     &limit=50
    #   Headers:
    #     wanted-client-id:    <derived from WANTED_API_KEY>
    #     wanted-client-secret: <derived from WANTED_API_KEY>
    #     User-Agent:          MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)
    #
    # Anticipated response normalization:
    #   .data[] | {
    #     title: .position,
    #     company: .company.name,
    #     region: (.address.location // .company.address),
    #     posted_at: (.due_time // .created_at),
    #     url: ("https://www.wanted.co.kr/wd/" + (.id|tostring)),
    #     summary: .description // "",
    #     apply_url: ("https://www.wanted.co.kr/wd/" + (.id|tostring) + "/apply")
    #   }
    #
    # Operator validation step: with WANTED_API_KEY set, run
    #   curl -sS -H "wanted-client-id: $WANTED_API_KEY" \
    #     'https://api.wanted.co.kr/v4/jobs?country=kr&limit=3' | jq '.data[0]'
    # and compare the field names above against the actual response.
    # Update this plugin once the schema is confirmed; remove this
    # comment block and uncomment the live curl below.

    # Placeholder live call (commented out — see above):
    # raw=$(curl -sS \
    #   -H "wanted-client-id: $WANTED_API_KEY" \
    #   -H "User-Agent: MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
    #   "https://api.wanted.co.kr/v4/jobs?country=kr&job_sort=job.latest_order&limit=50") || return 2
    # echo "$raw" | jq --arg fa "$(date -Iseconds)" '
    #   { source: "kr-wanted",
    #     fetched_at: $fa,
    #     postings: (.data | map({
    #       title: .position,
    #       company: .company.name,
    #       region: (.address.location // .company.address // "원격"),
    #       posted_at: (.due_time // .created_at // ""),
    #       url: ("https://www.wanted.co.kr/wd/" + (.id|tostring)),
    #       summary: (.description // ""),
    #       apply_url: ("https://www.wanted.co.kr/wd/" + (.id|tostring) + "/apply")
    #     })) }'

    echo "[kr-wanted] live path not yet operator-validated — see kr-wanted.sh comments" >&2
    return 1
  fi

  # ----- mock fallback (default) -----
  local fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"

  cat <<EOF
{
  "source": "kr-wanted",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "Backend Engineer (AI Platform)",
      "company": "Wanted Mock A",
      "region": "서울 강남구",
      "posted_at": "2026-05-19",
      "url": "https://www.wanted.co.kr/wd/MOCK_W_200",
      "summary": "AI 플랫폼 백엔드 — Python/FastAPI, LLM 통합, multi-agent orchestration. (mock data — JH_WANTED_LIVE=1 with WANTED_API_KEY needed for live)",
      "apply_url": "https://www.wanted.co.kr/wd/MOCK_W_200/apply"
    },
    {
      "title": "AI 엔지니어 (Agent 시스템)",
      "company": "Wanted Mock B",
      "region": "서울 강남구",
      "posted_at": "2026-05-18",
      "url": "https://www.wanted.co.kr/wd/MOCK_W_201",
      "summary": "LangGraph/agent 시스템, RAG, 벡터 DB 운영 경험 우대. (mock)",
      "apply_url": "https://www.wanted.co.kr/wd/MOCK_W_201/apply"
    },
    {
      "title": "Senior Software Engineer (Game Client + AI)",
      "company": "Wanted Mock C",
      "region": "경기 성남",
      "posted_at": "2026-05-17",
      "url": "https://www.wanted.co.kr/wd/MOCK_W_202",
      "summary": "Unity 클라이언트 + AI 통합. C#/Lua/Python. (mock)",
      "apply_url": "https://www.wanted.co.kr/wd/MOCK_W_202/apply"
    },
    {
      "title": "AI Solutions Engineer",
      "company": "Wanted Mock D",
      "region": "서울 강남구",
      "posted_at": "2026-05-19",
      "url": "https://www.wanted.co.kr/wd/MOCK_W_203",
      "summary": "Enterprise customers를 위한 LLM agent 솔루션 빌드 + 통합 + 배포. Forward Deployed engineering 성격. (mock)",
      "apply_url": "https://www.wanted.co.kr/wd/MOCK_W_203/apply"
    }
  ]
}
EOF
}
