#!/usr/bin/env bash
# sources/global-remotive.sh — Remotive public job feed.
#
# Status (2026-05-21): MOCK-FALLBACK mode default.  Live fetch
# gated on JH_GLOBAL_REMOTIVE_LIVE=1.
#
# Endpoint: https://remotive.com/api/remote-jobs  (public JSON, no auth)
# Category filter (optional): ?category=software-dev
# robots.txt: no specific User-agent: * Disallow on this path.
#
# Plugin contract: see ../sources/README.md.

# shellcheck shell=bash

fetch_postings() {
  local fetched_at
  fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"

  if [[ "${JH_GLOBAL_REMOTIVE_LIVE:-0}" == "1" ]]; then
    local cat_qs=""
    [[ -n "${JH_GLOBAL_REMOTIVE_CATEGORY:-}" ]] && cat_qs="?category=${JH_GLOBAL_REMOTIVE_CATEGORY}"

    local raw
    raw=$(/usr/bin/curl -sS --max-time 12 \
      -A "MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
      --compressed \
      "https://remotive.com/api/remote-jobs${cat_qs}" 2>/dev/null) || {
        echo "[global-remotive] curl failed — falling back to mock" >&2
        _remotive_mock "$fetched_at"
        return 0
      }

    local out
    out=$(echo "$raw" | jq --arg fa "$fetched_at" '
      [ (.jobs // [])[]
        | {
            title: (.title // ""),
            company: (.company_name // ""),
            region: ((.candidate_required_location // "Remote") | tostring),
            posted_at: ((.publication_date // "") | tostring | .[0:10]),
            url: (.url // ""),
            summary: ("Remotive — " + (.company_name // "") + " — " + (.title // "") + (if .category then " · " + (.category | tostring) else "" end) + (if .job_type then " · " + (.job_type | tostring) else "" end)),
            apply_url: (.url // "")
          }
      ] | { source: "global-remotive", fetched_at: $fa, postings: . }
    ' 2>/dev/null)

    if [[ -n "$out" ]] && echo "$out" | jq -e '.postings | type == "array"' >/dev/null 2>&1; then
      echo "$out"
      return 0
    fi
    echo "[global-remotive] response parse failed — falling back to mock" >&2
    _remotive_mock "$fetched_at"
    return 0
  fi

  _remotive_mock "$fetched_at"
}

_remotive_mock() {
  local fetched_at="$1"
  cat <<EOF
{
  "source": "global-remotive",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "Senior Backend Engineer (Remote)",
      "company": "Remotive Mock A",
      "region": "Remote — EU",
      "posted_at": "2026-05-20",
      "url": "https://remotive.com/remote-jobs/software-dev/MOCK_RV_1",
      "summary": "Remotive — Remotive Mock A — Senior Backend Engineer (Remote) · Software Development · Full-time (mock — set JH_GLOBAL_REMOTIVE_LIVE=1 for live fetch)",
      "apply_url": "https://remotive.com/remote-jobs/software-dev/MOCK_RV_1"
    },
    {
      "title": "AI Engineer",
      "company": "Remotive Mock B",
      "region": "Remote — Worldwide",
      "posted_at": "2026-05-19",
      "url": "https://remotive.com/remote-jobs/software-dev/MOCK_RV_2",
      "summary": "Remotive — Remotive Mock B — AI Engineer · Software Development · Contract (mock)",
      "apply_url": "https://remotive.com/remote-jobs/software-dev/MOCK_RV_2"
    }
  ]
}
EOF
}
