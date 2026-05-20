#!/usr/bin/env bash
# sources/global-ats.sh — ATS public job-board aggregator.
#
# Status (2026-05-21): MOCK-FALLBACK mode default.  Live aggregation
# gated on JH_GLOBAL_ATS_LIVE=1.
#
# When live, fetches public JSON from Greenhouse / Ashby / Lever
# per-board endpoints (no auth required — these are the same
# endpoints the companies' own careers pages consume) and
# normalizes into the job-hunt schema.
#
# Board list source (in priority order):
#   1. Env: JH_ATS_GREENHOUSE_BOARDS / JH_ATS_ASHBY_BOARDS /
#      JH_ATS_LEVER_BOARDS (comma-separated token lists)
#   2. File: $SKILL_DIR/config/ats-boards.yaml (operator-edited)
#   3. File: $SKILL_DIR/config/ats-boards.example.yaml (default)
#
# Verified endpoints (2026-05-21 ~00:30 KST) — see
# docs/research/job-sources-survey-2026-05-21.md Tier-2 table.
#
# Plugin contract: see ../sources/README.md.

# shellcheck shell=bash

# Per-request timeout for individual board fetches.  ATS endpoints
# usually respond in <2s; 8s is generous + bounds total wall time
# when one board is misbehaving.
_ATS_TIMEOUT=8

_ats_log() {
  [[ "${QUIET:-0}" == "1" ]] || echo "[global-ats] $*" >&2
}

# Read a YAML list under a given key from ats-boards config.
# Mirrors the parser fallback chain used in scripts/run.sh.
_ats_yaml_list() {
  local file="$1" key="$2"
  if command -v yq >/dev/null 2>&1; then
    yq -o=json "$file" 2>/dev/null | jq -r ".${key}[]?" 2>/dev/null
  elif command -v python3 >/dev/null 2>&1 && python3 -c "import yaml" >/dev/null 2>&1; then
    python3 -c "
import sys, yaml, json
try:
    d = yaml.safe_load(open(sys.argv[1])) or {}
    for x in (d.get(sys.argv[2]) or []):
        print(x)
except Exception:
    pass
" "$file" "$key"
  elif command -v ruby >/dev/null 2>&1; then
    ruby -ryaml -e "
begin
  d = YAML.load_file(ARGV[0]) || {}
  Array(d[ARGV[1]]).each { |x| puts x }
rescue
end
" "$file" "$key"
  fi
}

_ats_resolve_boards() {
  # $1 = ats name (greenhouse|ashby|lever)
  local ats="$1"
  local env_var
  case "$ats" in
    greenhouse) env_var="JH_ATS_GREENHOUSE_BOARDS" ;;
    ashby)      env_var="JH_ATS_ASHBY_BOARDS" ;;
    lever)      env_var="JH_ATS_LEVER_BOARDS" ;;
    *) return 1 ;;
  esac
  local env_val="${!env_var:-}"
  if [[ -n "$env_val" ]]; then
    # Comma-split; trim whitespace per token.
    echo "$env_val" | tr ',' '\n' | awk '{$1=$1; print}' | grep -v '^$'
    return 0
  fi
  local file
  if   [[ -f "${SKILL_DIR:-}/config/ats-boards.yaml" ]]; then
    file="${SKILL_DIR}/config/ats-boards.yaml"
  elif [[ -f "${SKILL_DIR:-}/config/ats-boards.example.yaml" ]]; then
    file="${SKILL_DIR}/config/ats-boards.example.yaml"
  else
    return 0
  fi
  _ats_yaml_list "$file" "$ats"
}

# Fetch one Greenhouse board and emit a JSON array of normalized
# postings on stdout.  Empty on failure.
_ats_fetch_greenhouse() {
  local token="$1"
  local raw
  raw=$(/usr/bin/curl -sS --max-time "$_ATS_TIMEOUT" \
    -A "MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
    "https://boards-api.greenhouse.io/v1/boards/${token}/jobs" 2>/dev/null) || return 0
  echo "$raw" | jq --arg co "$token" '
    (.jobs // []) | map({
      title: (.title // ""),
      company: (.company_name // $co),
      region: ((.location.name // "Remote") | tostring),
      posted_at: ((.first_published // .updated_at // "") | tostring | .[0:10]),
      url: (.absolute_url // ""),
      summary: ("ATS: greenhouse — " + ($co | tostring) + " — " + (.title // "")),
      apply_url: (.absolute_url // "")
    })
  ' 2>/dev/null
}

_ats_fetch_ashby() {
  local slug="$1"
  local raw
  raw=$(/usr/bin/curl -sS --max-time "$_ATS_TIMEOUT" \
    -A "MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
    "https://api.ashbyhq.com/posting-api/job-board/${slug}" 2>/dev/null) || return 0
  echo "$raw" | jq --arg co "$slug" '
    (.jobs // []) | map({
      title: (.title // ""),
      company: ($co | tostring),
      region: ((.location // (.address.postalAddress.addressRegion // "Remote")) | tostring),
      posted_at: ((.publishedAt // "") | tostring | .[0:10]),
      url: (.jobUrl // .applyUrl // ""),
      summary: ("ATS: ashby — " + ($co | tostring) + " — " + (.title // "") + (if .isRemote then " — remote" else "" end)),
      apply_url: (.applyUrl // .jobUrl // "")
    })
  ' 2>/dev/null
}

_ats_fetch_lever() {
  local slug="$1"
  local raw
  raw=$(/usr/bin/curl -sS --max-time "$_ATS_TIMEOUT" \
    -A "MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
    "https://api.lever.co/v0/postings/${slug}?mode=json" 2>/dev/null) || return 0
  # Lever returns an array, not a wrapped object.
  echo "$raw" | jq --arg co "$slug" '
    if type == "array" then
      map({
        title: (.text // ""),
        company: ($co | tostring),
        region: (
          ((.categories.location // "Remote") | tostring) +
          (if .categories.team then " · " + (.categories.team | tostring) else "" end)
        ),
        posted_at: (
          if (.createdAt | type) == "number"
          then ((.createdAt / 1000) | strftime("%Y-%m-%d"))
          else ""
          end
        ),
        url: (.hostedUrl // ""),
        summary: ("ATS: lever — " + ($co | tostring) + " — " + (.text // "")),
        apply_url: (.applyUrl // .hostedUrl // "")
      })
    else [] end
  ' 2>/dev/null
}

fetch_postings() {
  local fetched_at
  fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"

  if [[ "${JH_GLOBAL_ATS_LIVE:-0}" == "1" ]]; then
    # Live aggregation across configured boards.
    local tmpfile
    tmpfile=$(mktemp -t global-ats-postings.XXXXXX)
    # Build a JSON array of all postings, growing as boards complete.
    echo "[]" > "$tmpfile"

    local count=0 ats token shard
    for ats in greenhouse ashby lever; do
      while IFS= read -r token; do
        [[ -z "$token" ]] && continue
        case "$ats" in
          greenhouse) shard=$(_ats_fetch_greenhouse "$token") ;;
          ashby)      shard=$(_ats_fetch_ashby      "$token") ;;
          lever)      shard=$(_ats_fetch_lever      "$token") ;;
        esac
        if [[ -n "$shard" ]] && echo "$shard" | jq -e 'type == "array"' >/dev/null 2>&1; then
          # Merge shard into running array.
          jq --argjson new "$shard" '. + $new' "$tmpfile" > "${tmpfile}.next" && mv "${tmpfile}.next" "$tmpfile"
          local n
          n=$(echo "$shard" | jq 'length' 2>/dev/null || echo 0)
          count=$((count + n))
          _ats_log "$ats/$token  +$n postings"
        else
          _ats_log "$ats/$token  fetch failed (empty/non-array response)"
        fi
        # Rate-limit between boards (per ATS robots.txt Crawl-delay: 1
        # on Lever; safe default 250ms across the board).
        sleep 0.25
      done < <(_ats_resolve_boards "$ats")
    done

    if (( count == 0 )); then
      rm -f "$tmpfile"
      _ats_log "no boards configured or all fetches failed — falling back to mock fixture"
      _ats_mock_fixture "$fetched_at"
      return 0
    fi

    jq --arg fa "$fetched_at" '{ source: "global-ats", fetched_at: $fa, postings: . }' "$tmpfile"
    rm -f "$tmpfile"
    return 0
  fi

  # ----- mock fallback (default) -----
  _ats_mock_fixture "$fetched_at"
}

_ats_mock_fixture() {
  local fetched_at="$1"
  cat <<EOF
{
  "source": "global-ats",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "Forward Deployed Engineer",
      "company": "Anthropic Mock",
      "region": "Remote",
      "posted_at": "2026-05-20",
      "url": "https://job-boards.greenhouse.io/anthropic/jobs/MOCK_GH_1",
      "summary": "ATS: greenhouse — anthropic — Forward Deployed Engineer (mock — set JH_GLOBAL_ATS_LIVE=1 to fetch live)",
      "apply_url": "https://job-boards.greenhouse.io/anthropic/jobs/MOCK_GH_1"
    },
    {
      "title": "Applied AI Engineer",
      "company": "OpenAI Mock",
      "region": "San Francisco · AI",
      "posted_at": "2026-05-20",
      "url": "https://jobs.ashbyhq.com/openai/MOCK_ASHBY_1",
      "summary": "ATS: ashby — openai — Applied AI Engineer (mock)",
      "apply_url": "https://jobs.ashbyhq.com/openai/MOCK_ASHBY_1/application"
    },
    {
      "title": "Solutions Engineer",
      "company": "Spotify Mock",
      "region": "Remote — Platform",
      "posted_at": "2026-05-19",
      "url": "https://jobs.lever.co/spotify/MOCK_LEVER_1",
      "summary": "ATS: lever — spotify — Solutions Engineer (mock)",
      "apply_url": "https://jobs.lever.co/spotify/MOCK_LEVER_1/apply"
    }
  ]
}
EOF
}
