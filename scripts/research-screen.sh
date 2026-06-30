#!/usr/bin/env bash
# research-screen.sh — license-screen the MEDIA sources in a research.json.
#
# The 리서치팀 (research-team) subagent gathers two kinds of source:
#   - fact_sources : citations for factual claims (fair-use factual reporting,
#                    NOT license-gated — left untouched here)
#   - media_sources: clips/images that will be DOWNLOADED and composited into
#                    the short (these MUST pass the copyright allowlist)
#
# This script walks media_sources[] and stamps each with license_screen
# (allowed | local | blocked) + the resolved license, using the same
# agents/lib/copyright.sh::check_source_allowed gate the render pipeline uses.
# A `blocked` media source is a hard signal to the producer: drop it and fall
# back to Pexels keyword search (always license-clean).
#
# Usage:   scripts/research-screen.sh <research.json>   [--in-place]
# Output:  prints screened JSON to stdout; with --in-place, rewrites the file.
# Exit:    0 all media allowed/local · 3 one or more blocked · 64 usage

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh" 2>/dev/null || true
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/log.sh" 2>/dev/null || { log_warn(){ echo "⚠ $*" >&2; }; log_info(){ echo "  $*"; }; }
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/copyright.sh" 2>/dev/null || true
# UTF-8 mode so non-ASCII source titles survive Windows cp949 stdout.
export PYTHONUTF8=1
# env.sh enables `set -e`; check_source_allowed returns non-zero for blocked
# URLs by design, which we inspect — relax -e and do our own handling.
set +e

RJSON="${1:-}"
IN_PLACE=0
[[ "${2:-}" == "--in-place" ]] && IN_PLACE=1
if [[ -z "$RJSON" || ! -f "$RJSON" ]]; then
  echo "usage: $0 <research.json> [--in-place]" >&2
  exit 64
fi

# Pull media URLs out, screen each in bash (so check_source_allowed's allowlist
# logic is the single source of truth), feed the verdicts back into python for
# a clean JSON rewrite.
mapfile -t URLS < <(python3 -c '
import json,sys
d=json.load(open(sys.argv[1],encoding="utf-8"))
for m in d.get("media_sources",[]):
    print(m.get("url",""))
' "$RJSON")

SCREEN_TSV="$(mktemp)"
trap 'rm -f "$SCREEN_TSV"' EXIT
BLOCKED=0
for url in "${URLS[@]}"; do
  [[ -z "$url" ]] && { printf '%s\t%s\t%s\n' "" "blocked" "empty-url" >> "$SCREEN_TSV"; BLOCKED=1; continue; }
  if declare -f check_source_allowed >/dev/null 2>&1; then
    if lic="$(check_source_allowed "$url" 2>/dev/null)"; then
      if [[ "$lic" == "local-path" ]]; then
        printf '%s\t%s\t%s\n' "$url" "local" "local-path" >> "$SCREEN_TSV"
      else
        printf '%s\t%s\t%s\n' "$url" "allowed" "$lic" >> "$SCREEN_TSV"
      fi
    else
      printf '%s\t%s\t%s\n' "$url" "blocked" "not-on-allowlist" >> "$SCREEN_TSV"
      BLOCKED=1
      log_warn "blocked media source: $url"
    fi
  else
    printf '%s\t%s\t%s\n' "$url" "blocked" "copyright.sh-unavailable" >> "$SCREEN_TSV"
    BLOCKED=1
  fi
done

OUT="$(python3 - "$RJSON" "$SCREEN_TSV" <<'PY'
import json, sys
rj, tsv = sys.argv[1], sys.argv[2]
d = json.load(open(rj, encoding="utf-8"))
screens = {}
order = []
for ln in open(tsv, encoding="utf-8"):
    parts = ln.rstrip("\n").split("\t")
    if len(parts) == 3:
        url, screen, lic = parts
        order.append((url, screen, lic))
# map by position (media_sources order preserved), fall back to url match
ms = d.get("media_sources", [])
for i, m in enumerate(ms):
    if i < len(order):
        _, screen, lic = order[i]
        m["license_screen"] = screen
        if lic and lic not in ("not-on-allowlist", "empty-url", "copyright.sh-unavailable"):
            m["license"] = lic
        elif screen == "blocked":
            m.setdefault("license", "unknown")
            m["screen_reason"] = lic
print(json.dumps(d, ensure_ascii=False, indent=2))
PY
)"

if [[ $IN_PLACE -eq 1 ]]; then
  printf '%s\n' "$OUT" > "$RJSON"
  log_info "screened in place: $RJSON"
else
  printf '%s\n' "$OUT"
fi

if [[ $BLOCKED -eq 1 ]]; then
  log_warn "research-screen: one or more media sources BLOCKED — producer must drop them"
  exit 3
fi
exit 0
