#!/usr/bin/env bash
# sources/_mock.sh — deterministic synthetic source.
#
# Returns a fixed set of fake job postings spanning multiple
# regions, categories, and keyword profiles.  Useful for testing
# the orchestrator pipeline (filter → dedupe → render) without
# any network or auth.
#
# Plugin contract: see ../sources/README.md.
#
# Invocation: orchestrator dot-sources this file then calls
# fetch_postings.  Environment context inputs read but ignored
# (mock data is fixed): JH_REGIONS, JH_CATEGORIES,
# JH_KEYWORDS_INCLUDE, JH_KEYWORDS_EXCLUDE.

# shellcheck shell=bash

fetch_postings() {
  # Deterministic ISO timestamp so digest output is stable across
  # test runs.  Real sources use the current wall clock.
  local fetched_at="${JH_MOCK_FETCH_AT:-2026-05-20T00:00:00+09:00}"

  cat <<EOF
{
  "source": "_mock",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "백엔드 개발자 (AI/LLM 통합)",
      "company": "MockCorp",
      "region": "서울 강남구",
      "posted_at": "2026-05-19",
      "url": "https://mock.example.com/jobs/100",
      "summary": "Python/FastAPI 기반 LLM agent 통합 백엔드. 사내 RAG 시스템과 외부 API orchestration 담당.",
      "apply_url": "https://mock.example.com/apply/100"
    },
    {
      "title": "풀스택 개발자 (React/Node)",
      "company": "MockStartup",
      "region": "서울 마포구",
      "posted_at": "2026-05-19",
      "url": "https://mock.example.com/jobs/101",
      "summary": "Next.js + Node.js. AI 챗봇 인터페이스 신규 구축 프로젝트.",
      "apply_url": "https://mock.example.com/apply/101"
    },
    {
      "title": "AI 엔지니어 (멀티에이전트)",
      "company": "MockLabs",
      "region": "경기 성남",
      "posted_at": "2026-05-18",
      "url": "https://mock.example.com/jobs/102",
      "summary": "LangGraph / agent orchestration / 멀티-스킬 시스템 설계. Python.",
      "apply_url": "https://mock.example.com/apply/102"
    },
    {
      "title": "프론트엔드 개발자 (React)",
      "company": "MockMedia",
      "region": "서울 종로구",
      "posted_at": "2026-05-19",
      "url": "https://mock.example.com/jobs/103",
      "summary": "React/TypeScript. 영상 편집기 UI 신규 개발.",
      "apply_url": "https://mock.example.com/apply/103"
    },
    {
      "title": "단순 운영 백엔드 SI",
      "company": "MockSI",
      "region": "서울 영등포구",
      "posted_at": "2026-05-15",
      "url": "https://mock.example.com/jobs/104",
      "summary": "기존 SI 시스템 유지보수. 파견 가능자 우대.",
      "apply_url": "https://mock.example.com/apply/104"
    },
    {
      "title": "AI 백엔드 (재택 가능)",
      "company": "MockRemote",
      "region": "원격",
      "posted_at": "2026-05-20",
      "url": "https://mock.example.com/jobs/105",
      "summary": "Python + LLM agent.  Remote-first.  Async 협업.",
      "apply_url": "https://mock.example.com/apply/105"
    },
    {
      "title": "Game Client 개발자 (Unity/C#)",
      "company": "MockGames",
      "region": "서울 강남구",
      "posted_at": "2026-05-19",
      "url": "https://mock.example.com/jobs/106",
      "summary": "Unity 모바일 게임 클라이언트.  AI 통합 경험 우대.",
      "apply_url": "https://mock.example.com/apply/106"
    },
    {
      "title": "AI 엔지니어 (이미 응시)",
      "company": "MockCorp",
      "region": "서울 강남구",
      "posted_at": "2026-05-18",
      "url": "https://mock.example.com/jobs/100",
      "summary": "DUPLICATE — same URL as posting 100, should be deduped.",
      "apply_url": "https://mock.example.com/apply/100"
    }
  ]
}
EOF
}
