#!/usr/bin/env bash
# sources/kr-theteams.sh — 더팀스 (theteams.kr) source plugin.
#
# Status (2026-05-21): MOCK-FALLBACK mode default.  Live fetch
# gated on JH_THETEAMS_LIVE=1.
#
# theteams.kr is a Korean startup / 강소기업 / 중견기업-focused
# careers site.  robots.txt: `User-agent: *  Allow: /` (no
# restrictions), sitemap published openly.
#
# Strategy:
#   1. Pull the index of recruit sitemaps from
#      https://www.theteams.kr/gz_sitemap/recruit-1.xml — the
#      first sub-sitemap (recruit_000001.xml.gz) is the newest
#      (sitemap numbering ascends in age — sitemap 1 = newest
#      posting IDs in the high millions, sitemap 300 = oldest).
#   2. Decode the gzipped sub-sitemap, take the first N=100
#      `<loc>` URLs (newest postings).
#   3. For each URL, fetch the posting page (35-50 KB), extract
#      the og:title meta tag (format: "<title> by <company>")
#      and the OG image alt text for the region heuristic.
#   4. Emit normalized JSON.
#
# Rate-limit: 250ms between page fetches; ~25s total for 100 pages.
# theteams.kr has no anti-bot signaling but the courteous default
# limits load.
#
# Plugin contract: see ../sources/README.md.

# shellcheck shell=bash

fetch_postings() {
  local fetched_at
  fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"

  if [[ "${JH_THETEAMS_LIVE:-0}" == "1" ]]; then
    local max_postings="${JH_THETEAMS_MAX:-50}"
    local tmpdir
    tmpdir=$(mktemp -d -t theteams.XXXXXX)

    # 1. Index of recruit sitemaps.
    if ! /usr/bin/curl -sS --max-time 8 \
         -A "MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
         "https://www.theteams.kr/gz_sitemap/recruit-1.xml" \
         -o "$tmpdir/idx.xml"; then
      echo "[kr-theteams] sitemap index fetch failed — falling back to mock" >&2
      rm -rf "$tmpdir"
      _theteams_mock "$fetched_at"
      return 0
    fi

    # First sub-sitemap = newest postings.
    local first_sub
    first_sub=$(grep -oE "<loc>[^<]+</loc>" "$tmpdir/idx.xml" \
                | head -1 \
                | sed 's|<[^>]*>||g')
    if [[ -z "$first_sub" ]]; then
      echo "[kr-theteams] sitemap index empty — falling back to mock" >&2
      rm -rf "$tmpdir"
      _theteams_mock "$fetched_at"
      return 0
    fi

    # 2. Decode the gzipped sub-sitemap; take first N URLs.
    if ! /usr/bin/curl -sS --max-time 10 \
         -A "MelonS-Agents/0.4 (+github.com/MelonS/MelonS-Agents)" \
         "$first_sub" -o "$tmpdir/sub.xml.gz"; then
      echo "[kr-theteams] sub-sitemap fetch failed — falling back to mock" >&2
      rm -rf "$tmpdir"
      _theteams_mock "$fetched_at"
      return 0
    fi
    gunzip -f "$tmpdir/sub.xml.gz" 2>/dev/null || {
      echo "[kr-theteams] gunzip failed — falling back to mock" >&2
      rm -rf "$tmpdir"
      _theteams_mock "$fetched_at"
      return 0
    }

    local urls
    urls=$(grep -oE "<loc>[^<]+</loc>" "$tmpdir/sub.xml" \
           | sed 's|<[^>]*>||g' \
           | head -"$max_postings")

    if [[ -z "$urls" ]]; then
      echo "[kr-theteams] sub-sitemap had no URLs — falling back to mock" >&2
      rm -rf "$tmpdir"
      _theteams_mock "$fetched_at"
      return 0
    fi

    # 3. For each posting URL, fetch + extract OG metadata.
    : > "$tmpdir/postings.tsv"
    local n=0
    while IFS= read -r url; do
      [[ -z "$url" ]] && continue
      n=$((n + 1))
      local html
      html=$(/usr/bin/curl -sS --max-time 6 \
              -A "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 Chrome/120 Safari/537.36" \
              --compressed "$url" 2>/dev/null)
      [[ -z "$html" ]] && continue

      # og:title is "<title> by <company>".  Fall back to <title> tag.
      local og_title title company
      og_title=$(printf '%s' "$html" | grep -oE '<meta property="og:title" content="[^"]+' | sed -E 's/.*content="//; s/"$//' | head -1)
      if [[ -n "$og_title" && "$og_title" == *" by "* ]]; then
        title="${og_title% by *}"
        company="${og_title##* by }"
      else
        title=$(printf '%s' "$html" \
                | grep -oE '<title>[^<]+</title>' \
                | head -1 \
                | sed -E 's|</?title>||g; s| \| 더팀스$||')
        company=""
      fi
      # Region: theteams.kr renders the region as plain text on the
      # posting page, not inside an OG tag.  Match the standard KR
      # sido + sigungu pattern directly off the HTML body.  Fall back
      # to a bare-sido match, then "—".
      local region
      region=$(printf '%s' "$html" \
               | grep -oE '(서울특별시|부산광역시|인천광역시|대구광역시|대전광역시|광주광역시|울산광역시|세종특별자치시|경기도|강원특별자치도|강원도|충청북도|충청남도|전라북도|전북특별자치도|전라남도|경상북도|경상남도|제주특별자치도|제주도|서울|부산|인천|대구|대전|광주|울산|세종|경기|강원|충북|충남|전북|전남|경북|경남|제주)[ ]?[가-힣]{1,5}(시|구|군)' \
               | head -1)
      if [[ -z "$region" ]]; then
        region=$(printf '%s' "$html" \
                 | grep -oE '원격|재택|Remote' \
                 | head -1)
      fi
      if [[ -z "$region" ]]; then
        region=$(printf '%s' "$html" \
                 | grep -oE '(서울|부산|인천|대구|대전|광주|울산|세종|경기|강원|충북|충남|전북|전남|경북|경남|제주)' \
                 | head -1)
      fi
      [[ -z "$region" ]] && region="—"

      # TSV: title<tab>company<tab>region<tab>url
      printf '%s\t%s\t%s\t%s\n' "$title" "$company" "$region" "$url" >> "$tmpdir/postings.tsv"

      # Rate-limit between page fetches.
      sleep 0.25
    done <<<"$urls"

    # 4. Compose JSON.
    if [[ ! -s "$tmpdir/postings.tsv" ]]; then
      echo "[kr-theteams] no postings parsed — falling back to mock" >&2
      rm -rf "$tmpdir"
      _theteams_mock "$fetched_at"
      return 0
    fi

    python3 - "$tmpdir/postings.tsv" "$fetched_at" <<'PYEOF'
import json, sys
tsv_path, fa = sys.argv[1], sys.argv[2]
postings = []
for line in open(tsv_path):
    parts = line.rstrip("\n").split("\t")
    if len(parts) < 4:
        continue
    title, company, region, url = parts[0], parts[1], parts[2], parts[3]
    if not title or not url:
        continue
    postings.append({
        "title": title.strip(),
        "company": (company or "(unknown)").strip(),
        "region": (region or "—").strip(),
        "posted_at": "",
        "url": url,
        "summary": f"더팀스 — {company} — {title}",
        "apply_url": url,
    })
print(json.dumps({
    "source": "kr-theteams",
    "fetched_at": fa,
    "postings": postings,
}, ensure_ascii=False))
PYEOF

    rm -rf "$tmpdir"
    return 0
  fi

  _theteams_mock "$fetched_at"
}

_theteams_mock() {
  local fetched_at="$1"
  cat <<EOF
{
  "source": "kr-theteams",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "AI 엔지니어",
      "company": "더팀스 Mock Startup A",
      "region": "서울 강남구",
      "posted_at": "2026-05-20",
      "url": "https://www.theteams.kr/recruit/wanted/MOCK_TT_1",
      "summary": "더팀스 — 더팀스 Mock Startup A — AI 엔지니어 (mock — JH_THETEAMS_LIVE=1 for live)",
      "apply_url": "https://www.theteams.kr/recruit/wanted/MOCK_TT_1"
    },
    {
      "title": "Founding Engineer",
      "company": "더팀스 Mock Startup B",
      "region": "서울 마포구",
      "posted_at": "2026-05-20",
      "url": "https://www.theteams.kr/recruit/wanted/MOCK_TT_2",
      "summary": "더팀스 — 더팀스 Mock Startup B — Founding Engineer (mock)",
      "apply_url": "https://www.theteams.kr/recruit/wanted/MOCK_TT_2"
    }
  ]
}
EOF
}
