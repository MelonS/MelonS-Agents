#!/usr/bin/env bash
# sources/kr-programmers.sh — Programmers (프로그래머스) source plugin.
#
# Status (2026-05-21): **DEPRECATED — service permanently shut down.**
#
# Programmers officially closed its 채용 (recruiting) service on
# 2025-05-19.  The career.programmers.co.kr domain is now NXDOMAIN.
# Source: in-product notice fetched 2026-05-20 from
# programmers.co.kr/help/notice — "프로그래머스 채용 서비스는
# 아쉽게도 25년 5월 19일부로 종료될 예정입니다."
#
# This plugin is retained only so legacy filter configs that name it
# don't break the orchestrator outright.  It returns a single empty
# fixture noting the closure and the live-flag (JH_PROGRAMMERS_LIVE)
# is ignored — declared in config/activation.tsv for dashboard
# symmetry but never enables a real fetch.
#
# Removal candidates: when filters.example.yaml no longer references
# kr-programmers, this file can be deleted entirely.
#
# Plugin contract: see ../sources/README.md.

# shellcheck shell=bash

fetch_postings() {
  local fetched_at
  fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"
  cat <<EOF
{
  "source": "kr-programmers",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "[DEPRECATED] 프로그래머스 채용 서비스 종료 (2025-05-19)",
      "company": "프로그래머스 (그렙)",
      "region": "—",
      "posted_at": "2025-05-19",
      "url": "https://programmers.co.kr/",
      "summary": "프로그래머스 채용 서비스는 2025-05-19부로 공식 종료되었습니다. 이 플러그인은 호환성 stub만 남기고 동작하지 않습니다. filters.yaml에서 kr-programmers 항목을 제거하세요.",
      "apply_url": "https://programmers.co.kr/"
    }
  ]
}
EOF
}
