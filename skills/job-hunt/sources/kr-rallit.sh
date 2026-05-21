#!/usr/bin/env bash
# sources/kr-rallit.sh — 랄릿 (rallit.com) — Korean IT-specialist job platform.
#
# Status (2026-05-21): MOCK-FALLBACK mode default.  Live fetch
# gated on JH_RALLIT_LIVE=1.
#
# robots.txt: `User-agent: *  Allow: /resumes` plus minor Disallows
# for auth/my pages.  The listing surface (/, /?q=<keyword>) is
# under `Allow: /` and SSRs ~20 cards per page.
#
# Each card surfaces as
#   <a href="/positions/<id>/<company-slug>-<title-slug>">
# so the URL itself carries the company + title.  This plugin
# parses the slug rather than per-page fetching (cheap, ~1 fetch).
#
# Endpoint:
#   https://www.rallit.com/?q=<keyword>   # SSR with up to 20 cards
#
# Plugin contract: see ../sources/README.md.

# shellcheck shell=bash

fetch_postings() {
  local fetched_at
  fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"

  if [[ "${JH_RALLIT_LIVE:-0}" == "1" ]]; then
    # Build query from first include keyword (rallit's q param is
    # single-string, no comma OR).  Fall back to a broad term.
    local query
    if [[ -n "${JH_KEYWORDS_INCLUDE:-}" ]]; then
      query=$(printf '%s' "$JH_KEYWORDS_INCLUDE" | awk -F',' '{ gsub(/^[ \t]+|[ \t]+$/, "", $1); print $1 }')
    fi
    [[ -z "$query" ]] && query="AI"
    # URL-encode minimally (spaces → %20; Korean stays).
    local q_enc="${query// /%20}"

    local tmpdir
    tmpdir=$(mktemp -d -t rallit.XXXXXX)
    if ! /usr/bin/curl -sS --max-time 10 \
         -A "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 Chrome/120 Safari/537.36" \
         --compressed \
         "https://www.rallit.com/?q=${q_enc}" -o "$tmpdir/page.html"; then
      echo "[kr-rallit] curl failed — falling back to mock" >&2
      rm -rf "$tmpdir"
      _rallit_mock "$fetched_at"
      return 0
    fi

    python3 - "$tmpdir/page.html" "$fetched_at" <<'PYEOF'
import json, sys, re, html as htmllib, urllib.parse
html_path, fa = sys.argv[1], sys.argv[2]
h = open(html_path, encoding="utf-8", errors="ignore").read()
postings = []
seen = set()

# Each card carries an <a href="/positions/<id>/<company-slug>-<title-slug>">.
# Slug is %-encoded Korean text — decode then split on the first "-"
# (heuristic: company-slug typically has 1+ hyphens but the
# title-slug starts at a clear boundary).  Slug parsing is best-effort
# — if the split fails we surface the whole slug as title.
for m in re.finditer(r'href="(/positions/(\d+)/([^"]+))"', h):
    href, pid, slug = m.group(1), m.group(2), m.group(3)
    if pid in seen:
        continue
    seen.add(pid)
    slug_dec = urllib.parse.unquote(slug)
    # Heuristic slug split: pretty common Rallit pattern is
    # "<company>-<job-title>" where the title has English/letters
    # and the company is Korean.  Take everything up to the first
    # ASCII-letter word as company, rest as title.
    parts = slug_dec.split("-")
    if len(parts) >= 2:
        # Find first part starting with an ASCII alpha — assume title from there.
        cut = next((i for i, p in enumerate(parts) if re.match(r'^[A-Za-z]', p)), 1)
        company = " ".join(parts[:cut]) if cut > 0 else parts[0]
        title = " ".join(parts[cut:]) if cut < len(parts) else slug_dec
    else:
        company = slug_dec
        title = slug_dec
    url = "https://www.rallit.com" + href
    postings.append({
        "title": title.strip() or slug_dec,
        "company": (company or "").strip() or "(unknown)",
        "region": "—",
        "posted_at": "",
        "url": url,
        "summary": f"랄릿 — {company} — {title}",
        "apply_url": url,
    })
print(json.dumps({
    "source": "kr-rallit",
    "fetched_at": fa,
    "postings": postings,
}, ensure_ascii=False))
PYEOF

    rm -rf "$tmpdir"
    return 0
  fi

  _rallit_mock "$fetched_at"
}

_rallit_mock() {
  local fetched_at="$1"
  cat <<EOF
{
  "source": "kr-rallit",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "AI Engineer",
      "company": "Rallit Mock Startup",
      "region": "—",
      "posted_at": "2026-05-20",
      "url": "https://www.rallit.com/positions/MOCK_RL_1",
      "summary": "랄릿 — Rallit Mock Startup — AI Engineer (mock — JH_RALLIT_LIVE=1 for live)",
      "apply_url": "https://www.rallit.com/positions/MOCK_RL_1"
    }
  ]
}
EOF
}
