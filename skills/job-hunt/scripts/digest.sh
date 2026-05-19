#!/usr/bin/env bash
# scripts/digest.sh — render aggregated postings to markdown.
#
# Reads aggregated JSON from stdin (or path arg) and prints
# markdown digest to stdout.  Optionally diffs against a prior
# digest's JSON to mark "new since".
#
# Input JSON shape (produced by scripts/run.sh):
# {
#   "generated_at": "ISO-8601",
#   "locale": "kr",
#   "sources": ["kr-wanted", "_mock", ...],
#   "filter_summary": "직군: ... / 지역: ... / 키워드: include=... exclude=...",
#   "postings_total": N,
#   "postings_new": M,                  # 0 if no prior digest
#   "by_source": {
#     "kr-wanted": [ { ...posting... }, ... ],
#     "_mock":     [ ... ]
#   },
#   "new_urls": ["https://...", ...]    # URLs flagged as new since prior
# }
#
# Usage:
#   scripts/digest.sh < aggregated.json > digest.md
#   scripts/digest.sh aggregated.json > digest.md

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

INPUT="${1:-/dev/stdin}"

if ! command -v jq >/dev/null 2>&1; then
  echo "[digest] jq required but not found on PATH" >&2
  exit 2
fi

# Slurp the input once into a variable so we can issue multiple jq
# queries against it without re-reading stdin.
data="$(cat "$INPUT")"

generated_at=$(echo "$data" | jq -r '.generated_at')
locale=$(echo "$data" | jq -r '.locale')
filter_summary=$(echo "$data" | jq -r '.filter_summary')
postings_total=$(echo "$data" | jq -r '.postings_total')
postings_new=$(echo "$data" | jq -r '.postings_new // 0')
sources_list=$(echo "$data" | jq -r '.sources | join(", ")')

# Header.
cat <<EOF
# Job-hunt digest — ${generated_at%T*}

> **Generated**: ${generated_at}
> **Locale**: \`${locale}\`
> **Sources**: ${sources_list}
> **Filter**: ${filter_summary}
> **Total postings**: ${postings_total}${postings_new:+ — **${postings_new} new since last digest**}

EOF

# If there are "new since" URLs, list them up front for skim-reading.
if [[ "$postings_new" -gt 0 ]]; then
  echo "## New since last digest"
  echo ""
  echo "$data" | jq -r '
    .by_source | to_entries[] |
    .key as $src | .value[] |
    select(. as $p | ($p.url) as $url |
      (input_filename | null) // true) |
    "- **\(.title)** · \(.company) · \(.region) · `\($src)` → [posting](\(.url))"
  ' 2>/dev/null | head -50
  echo ""
fi

# Per-source sections, full detail.
echo "## All postings (this run)"
echo ""

echo "$data" | jq -r '
  .by_source | to_entries[] |
  .key as $src |
  "### \($src) (\(.value | length))",
  "",
  (.value[] |
    "- **\(.title)** · \(.company)",
    "  - 지역: \(.region) · 게시: \(.posted_at)",
    "  - 요약: \(.summary // "-")",
    # Suppress the apply link when it equals the posting URL —
    # several KR sources route both to the same authenticated
    # apply flow.  Showing two identical links is noise.
    "  - [posting](\(.url))" + (
      if .apply_url and .apply_url != .url
      then " · [apply](\(.apply_url))"
      else ""
      end
    ),
    ""
  )
'

# Footer.
cat <<EOF

---

_Digest produced by \`skills/job-hunt\` orchestrator._
_Raw fetch JSON per source: see \`raw/<source>.json\` in this same digest directory._
EOF
