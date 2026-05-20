#!/usr/bin/env bash
# sources/kr-worknet.sh — 워크넷 (work.go.kr) — Korean government public employment service.
#
# Status (2026-05-21): MOCK-FALLBACK mode default.  Live fetch
# gated on JH_WORKNET_LIVE=1.
#
# Endpoint:
#   https://www.work.go.kr/empInfo/empInfoSrch/list/dtlEmpSrchList.do
# SSR HTML; ~336 KB per page (~10-20 postings per page in the
# default listing).  No API key, no anti-bot beyond standard
# browser-shape UA.
#
# robots.txt: no specific `User-agent: *` rules encountered for
# /empInfo/.  The site is a government public-employment service
# (정부 공공고용서비스); listing data is by definition public-domain
# and intended for redistribution.
#
# Detail page (per posting): each card carries a wantedAuthNo
# referencing the canonical work24.go.kr detail URL:
#   https://www.work24.go.kr/wk/a/b/1500/empDetailAuthView.do?wantedAuthNo=<N>&infoTypeCd=VALIDATION&infoTypeGroup=tb_workinfoworknet
#
# Plugin contract: see ../sources/README.md.

# shellcheck shell=bash

fetch_postings() {
  local fetched_at
  fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"

  if [[ "${JH_WORKNET_LIVE:-0}" == "1" ]]; then
    local html
    html=$(/usr/bin/curl -sS --max-time 12 \
      -A "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36" \
      --compressed \
      "https://www.work.go.kr/empInfo/empInfoSrch/list/dtlEmpSrchList.do" 2>/dev/null) || {
        echo "[kr-worknet] curl failed — falling back to mock" >&2
        _worknet_mock "$fetched_at"
        return 0
      }

    local tmpdir
    tmpdir=$(mktemp -d -t worknet.XXXXXX)
    printf '%s' "$html" > "$tmpdir/list.html"

    # Parse the SSR rows with python + regex.  Each posting is in
    # a <tr id="listN"> block; checkbox value carries pipe-joined
    # metadata: wantedAuthNo|infoType|company|title.
    python3 - "$tmpdir" <<'PYEOF' > "$tmpdir/postings.json"
import json, sys, re, html as htmllib
tmpdir = sys.argv[1]
h = open(f"{tmpdir}/list.html", encoding="utf-8", errors="ignore").read()
postings = []
for m in re.finditer(r'<tr id="list\d+"[^>]*>(.*?)</tr>', h, re.S):
    tr = m.group(1)
    # Checkbox carries pipe-joined metadata.
    cb = re.search(r'value="([^"]*\|[^"]*\|[^"]*\|[^"]*)"', tr)
    if not cb:
        continue
    parts = cb.group(1).split("|")
    if len(parts) < 4:
        continue
    wanted_no = parts[0].strip()
    info_type = parts[1].strip() or "VALIDATION"
    company = htmllib.unescape(parts[2].strip())
    title = htmllib.unescape(parts[3].strip())
    if not wanted_no or not title:
        continue
    # Region / hire-level chips appear as <em>X</em><em>Y</em><em>Z</em>.
    em_chunks = re.findall(r"<em>([^<]+)</em>", tr)
    em_chunks = [htmllib.unescape(c).strip() for c in em_chunks if c.strip()]
    # First <em> is usually hire-level (신입/경력/무관), second is
    # education, third is region; pick the longest as region heuristic.
    region = ""
    for c in em_chunks:
        if any(k in c for k in ("도 ", "시 ", "구 ", "특별", "광역", "원격", "재택")):
            region = c
            break
    if not region and em_chunks:
        region = em_chunks[-1]
    detail_url = f"https://www.work24.go.kr/wk/a/b/1500/empDetailAuthView.do?wantedAuthNo={wanted_no}&infoTypeCd={info_type}&infoTypeGroup=tb_workinfoworknet"
    postings.append({
        "title": title,
        "company": company or "(불명)",
        "region": region or "—",
        "posted_at": "",
        "url": detail_url,
        "summary": f"워크넷 — {company} — {title}" + (f" · {' · '.join(em_chunks[:3])}" if em_chunks else ""),
        "apply_url": detail_url,
    })
print(json.dumps(postings, ensure_ascii=False))
PYEOF

    if [[ ! -s "$tmpdir/postings.json" ]] || [[ "$(jq 'length' "$tmpdir/postings.json")" == "0" ]]; then
      rm -rf "$tmpdir"
      echo "[kr-worknet] parser produced 0 postings — falling back to mock" >&2
      _worknet_mock "$fetched_at"
      return 0
    fi

    jq --arg fa "$fetched_at" --slurpfile p "$tmpdir/postings.json" \
       -n '{ source: "kr-worknet", fetched_at: $fa, postings: ($p[0]) }'
    rm -rf "$tmpdir"
    return 0
  fi

  _worknet_mock "$fetched_at"
}

_worknet_mock() {
  local fetched_at="$1"
  cat <<EOF
{
  "source": "kr-worknet",
  "fetched_at": "${fetched_at}",
  "postings": [
    {
      "title": "[익산][정규직][주간] 반도체 생산설비 생산직 채용",
      "company": "주식회사 에이유이 (AUE CORP.)",
      "region": "전북특별자치도 익산시",
      "posted_at": "2026-05-20",
      "url": "https://www.work24.go.kr/wk/a/b/1500/empDetailAuthView.do?wantedAuthNo=MOCK_WORKNET_1&infoTypeCd=VALIDATION&infoTypeGroup=tb_workinfoworknet",
      "summary": "워크넷 — 주식회사 에이유이 — [익산][정규직][주간] 반도체 생산설비 생산직 채용 (mock — set JH_WORKNET_LIVE=1 for government public-employment data)",
      "apply_url": "https://www.work24.go.kr/wk/a/b/1500/empDetailAuthView.do?wantedAuthNo=MOCK_WORKNET_1&infoTypeCd=VALIDATION&infoTypeGroup=tb_workinfoworknet"
    },
    {
      "title": "[서울][공공기관] 데이터 분석가",
      "company": "Worknet Mock 공공기관",
      "region": "서울특별시",
      "posted_at": "2026-05-20",
      "url": "https://www.work24.go.kr/wk/a/b/1500/empDetailAuthView.do?wantedAuthNo=MOCK_WORKNET_2&infoTypeCd=VALIDATION&infoTypeGroup=tb_workinfoworknet",
      "summary": "워크넷 — 공공기관 — 데이터 분석가 (mock)",
      "apply_url": "https://www.work24.go.kr/wk/a/b/1500/empDetailAuthView.do?wantedAuthNo=MOCK_WORKNET_2&infoTypeCd=VALIDATION&infoTypeGroup=tb_workinfoworknet"
    }
  ]
}
EOF
}
