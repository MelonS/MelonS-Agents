#!/usr/bin/env bash
# sources/global-hn-whoshiring.sh — Hacker News monthly "Who is hiring?" thread.
#
# Status (2026-05-21): MOCK-FALLBACK mode default.  Live aggregation
# gated on JH_GLOBAL_HN_LIVE=1.
#
# HN's @whoishiring account posts a single "Ask HN: Who is hiring?
# (<Month> <Year>)" thread on the first business day of each month.
# Each top-level reply is one company's posting; convention is:
#
#   Company | Role | Location | Type | (Salary, link, etc.)
#
#   <body — description, stack, contact info>
#
# Algolia HN Search exposes this content as JSON without auth:
#   1. Find most recent author=whoishiring story:
#      https://hn.algolia.com/api/v1/search_by_date?tags=author_whoishiring,story
#      (filtered to exclude "Who wants to be hired" sibling threads)
#   2. Fetch that story's items:
#      https://hn.algolia.com/api/v1/items/<id>  → .children[] are
#      the postings.
#
# robots.txt: hn.algolia.com has no posted policy; Algolia exposes
# the HN dataset as a public read API used by many tools.
#
# Plugin contract: see ../sources/README.md.

# shellcheck shell=bash

# Decode the common HTML entities HN stores in comment text.
_hn_decode() {
  # Use python3 (always present on macOS + bootstrap-validated for
  # Linux) since `html` module gives correct semantics; sed/tr-only
  # would miss numeric refs.
  python3 -c "
import sys, html
print(html.unescape(sys.stdin.read()), end='')
" 2>/dev/null
}

fetch_postings() {
  local fetched_at
  fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"

  if [[ "${JH_GLOBAL_HN_LIVE:-0}" == "1" ]]; then
    local search
    search=$(/usr/bin/curl -sS --max-time 12 \
      -A "MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
      "https://hn.algolia.com/api/v1/search_by_date?tags=author_whoishiring,story&hitsPerPage=5" 2>/dev/null) || {
        echo "[global-hn-whoshiring] curl search failed — falling back to mock" >&2
        _hn_mock "$fetched_at"
        return 0
      }

    # First "Who is hiring?" (not "Who wants to be hired") thread.
    local thread_id thread_title
    read -r thread_id thread_title < <(echo "$search" | jq -r '
      .hits
      | map(select(.title // "" | test("Who is hiring\\?"; "i")))
      | sort_by(.created_at_i) | reverse | .[0]
      | "\(.objectID) \(.title)"
    ' 2>/dev/null)
    if [[ -z "$thread_id" || "$thread_id" == "null" ]]; then
      echo "[global-hn-whoshiring] could not locate current thread — falling back to mock" >&2
      _hn_mock "$fetched_at"
      return 0
    fi

    local item
    item=$(/usr/bin/curl -sS --max-time 15 \
      -A "MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
      "https://hn.algolia.com/api/v1/items/${thread_id}" 2>/dev/null) || {
        echo "[global-hn-whoshiring] curl item failed — falling back to mock" >&2
        _hn_mock "$fetched_at"
        return 0
      }

    # Build postings: each child = one posting.  First non-tag line
    # becomes title (after entity decode + strip).  HN comments are
    # HTML-decoded inline below.
    local tmpdir
    tmpdir=$(mktemp -d -t hn-postings.XXXXXX)
    echo "$item" > "$tmpdir/item.json"

    # Parse comments → normalized postings JSON array to a file.
    # The thread can have 300+ children; staying on-disk avoids the
    # OS argv-size limit when later feeding jq.
    python3 - "$tmpdir" <<'PYEOF' > "$tmpdir/postings.json"
import json, sys, html, re
tmpdir = sys.argv[1]
item = json.load(open(f"{tmpdir}/item.json"))
out = []
for c in item.get("children") or []:
    raw = c.get("text") or ""
    if not raw:
        continue
    # HN comments use <p> separators; first paragraph carries the
    # title-line per Who-is-hiring convention.
    parts = re.split(r"<p>", raw, maxsplit=1)
    first = html.unescape(parts[0])
    rest = html.unescape(parts[1]) if len(parts) > 1 else ""
    # Strip simple HTML tags (HN allows <a>, <i>, <b>, <p>).
    first = re.sub(r"<[^>]+>", " ", first).strip()
    rest = re.sub(r"<[^>]+>", " ", rest).strip()
    first = re.sub(r"\s+", " ", first)
    rest = re.sub(r"\s+", " ", rest)
    if not first:
        continue
    bits = [b.strip() for b in first.split("|")]
    company = bits[0] if bits else ""
    title = " | ".join(bits[1:3]) if len(bits) > 1 else first
    location = bits[2] if len(bits) >= 3 else ""
    out.append({
        "title": title or first[:80],
        "company": company or "(unknown)",
        "region": location or "—",
        "posted_at": (c.get("created_at") or "")[:10],
        "url": f"https://news.ycombinator.com/item?id={c.get('id')}",
        "summary": "HN Who's Hiring — " + (first[:160] + ("…" if len(first) > 160 else "")) + (" — " + rest[:160] if rest else ""),
        "apply_url": f"https://news.ycombinator.com/item?id={c.get('id')}",
    })
print(json.dumps(out, ensure_ascii=False))
PYEOF

    if [[ ! -s "$tmpdir/postings.json" ]]; then
      rm -rf "$tmpdir"
      echo "[global-hn-whoshiring] parse produced empty file — falling back to mock" >&2
      _hn_mock "$fetched_at"
      return 0
    fi

    jq --arg fa "$fetched_at" --slurpfile p "$tmpdir/postings.json" \
       -n '{ source: "global-hn-whoshiring", fetched_at: $fa, postings: ($p[0]) }'
    rm -rf "$tmpdir"
    return 0
  fi

  _hn_mock "$fetched_at"
}

_hn_mock() {
  local fetched_at="$1"
  cat <<EOF
{
  "source": "global-hn-whoshiring",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "Senior Software / AI Engineer | NYC (hybrid)",
      "company": "Pathos AI",
      "region": "NYC (hybrid, 3-4 days onsite)",
      "posted_at": "2026-05-01",
      "url": "https://news.ycombinator.com/item?id=MOCK_HN_1",
      "summary": "HN Who's Hiring — Pathos AI | Senior Software / AI Engineer | NYC (hybrid) | Full-time | \$180-200K + equity (mock — set JH_GLOBAL_HN_LIVE=1 for live monthly thread)",
      "apply_url": "https://news.ycombinator.com/item?id=MOCK_HN_1"
    },
    {
      "title": "Backend Engineer | Berlin / Remote",
      "company": "NetBird",
      "region": "Berlin, Germany | ONSITE & Remote for some roles",
      "posted_at": "2026-05-01",
      "url": "https://news.ycombinator.com/item?id=MOCK_HN_2",
      "summary": "HN Who's Hiring — NetBird | Backend Engineer | Berlin/Remote | Full-time — open source secure remote networking (mock)",
      "apply_url": "https://news.ycombinator.com/item?id=MOCK_HN_2"
    }
  ]
}
EOF
}
