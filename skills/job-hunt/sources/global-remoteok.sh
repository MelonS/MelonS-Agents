#!/usr/bin/env bash
# sources/global-remoteok.sh — RemoteOK public job feed.
#
# Status (2026-05-21): MOCK-FALLBACK mode default.  Live fetch
# gated on JH_GLOBAL_REMOTEOK_LIVE=1.
#
# Endpoint: https://remoteok.com/api  (public JSON, no auth)
# robots.txt posture: User-agent: * Allow: /  Crawl-delay: 1.
# Returns a JSON array where index 0 is a "legal" metadata object
# and indices 1..N are postings.
#
# Plugin contract: see ../sources/README.md.

# shellcheck shell=bash

fetch_postings() {
  local fetched_at
  fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"

  if [[ "${JH_GLOBAL_REMOTEOK_LIVE:-0}" == "1" ]]; then
    local raw
    raw=$(/usr/bin/curl -sS --max-time 12 \
      -A "MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
      --compressed \
      "https://remoteok.com/api" 2>/dev/null) || {
        echo "[global-remoteok] curl failed — falling back to mock" >&2
        _remoteok_mock "$fetched_at"
        return 0
      }

    local out
    out=$(echo "$raw" | jq --arg fa "$fetched_at" '
      # First element is a metadata object; skip it.
      [ .[1:][]
        | select(.position != null and .position != "")
        | {
            title: (.position // ""),
            company: (.company // ""),
            region: ((.location // "Remote") | tostring),
            posted_at: ((.date // "") | tostring | .[0:10]),
            url: (.url // .apply_url // ""),
            summary: ("RemoteOK — " + (.company // "") + " — " + (.position // "") + (if (.tags // [] | length) > 0 then " · " + (.tags | join(", ")) else "" end)),
            apply_url: (.apply_url // .url // "")
          }
      ] | { source: "global-remoteok", fetched_at: $fa, postings: . }
    ' 2>/dev/null)

    if [[ -n "$out" ]] && echo "$out" | jq -e '.postings | type == "array"' >/dev/null 2>&1; then
      echo "$out"
      return 0
    fi
    echo "[global-remoteok] response parse failed — falling back to mock" >&2
    _remoteok_mock "$fetched_at"
    return 0
  fi

  _remoteok_mock "$fetched_at"
}

_remoteok_mock() {
  local fetched_at="$1"
  cat <<EOF
{
  "source": "global-remoteok",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "Forward Deployed Engineer (Remote)",
      "company": "RemoteOK Mock A",
      "region": "Remote — Worldwide",
      "posted_at": "2026-05-20",
      "url": "https://remoteok.com/remote-jobs/MOCK_RO_1",
      "summary": "RemoteOK — RemoteOK Mock A — Forward Deployed Engineer (Remote) · python, llm, agents (mock — set JH_GLOBAL_REMOTEOK_LIVE=1 for live fetch)",
      "apply_url": "https://remoteok.com/remote-jobs/MOCK_RO_1#apply"
    },
    {
      "title": "Applied AI Engineer",
      "company": "RemoteOK Mock B",
      "region": "Remote — US/EU",
      "posted_at": "2026-05-19",
      "url": "https://remoteok.com/remote-jobs/MOCK_RO_2",
      "summary": "RemoteOK — RemoteOK Mock B — Applied AI Engineer · rag, llm, fastapi (mock)",
      "apply_url": "https://remoteok.com/remote-jobs/MOCK_RO_2#apply"
    }
  ]
}
EOF
}
