#!/usr/bin/env bash
# Shared source-attribution resolver — called by every mission's run.sh so
# the attribution / license bookkeeping stays consistent.
#
# Resolution order (first hit wins):
#   1. SOURCE_ATTRIBUTION env var (explicit caller override)
#   2. config/fixtures.yaml catalog match by URL or path
#   3. URL domain (if SOURCE looks like an http(s) URL)
#   4. Basename of the local file
#
# Side effects:
#   - Sets SOURCE_ATTRIBUTION (always populated) and FIXTURE_LICENSE (only when
#     a catalog hit gives a license string; otherwise left empty).
#   - Logs the resolution outcome via log_info.
#
# Usage:
#   source agents/lib/attribution.sh
#   resolve_source_attribution "$SOURCE"
#   write_sources_record "$MISSION_ID" "$SOURCE" "$MDIR/outputs/SOURCES.txt"

resolve_source_attribution() {
  local source="$1"
  FIXTURE_LICENSE="${FIXTURE_LICENSE:-}"

  if [[ -z "${SOURCE_ATTRIBUTION:-}" ]]; then
    local catalog="$REPO_ROOT/config/fixtures.yaml"
    if [[ -f "$catalog" ]]; then
      local match
      match=$(python3 - "$catalog" "$source" <<'PY'
import sys, re
cat, src = sys.argv[1], sys.argv[2]
text = open(cat).read()
items, cur = [], None
for line in text.splitlines():
    m = re.match(r"^  - id: (.+)$", line)
    if m:
        if cur: items.append(cur)
        cur = {"id": m.group(1).strip()}
        continue
    if cur is None: continue
    for key in ("url", "path", "source_attribution", "license"):
        m = re.match(rf"^    {key}: \"?(.+?)\"?$", line)
        if m: cur[key] = m.group(1).strip()
if cur: items.append(cur)
for it in items:
    if it.get("url") == src or it.get("path") == src:
        print((it.get("source_attribution","") + "\t" + it.get("license","")).rstrip())
        break
PY
)
      if [[ -n "$match" ]]; then
        SOURCE_ATTRIBUTION="${match%%$'\t'*}"
        FIXTURE_LICENSE="${match#*$'\t'}"
        log_info "catalog match: $SOURCE_ATTRIBUTION ($FIXTURE_LICENSE)"
      fi
    fi
  fi

  if [[ -z "${SOURCE_ATTRIBUTION:-}" ]]; then
    if [[ "$source" =~ ^https?:// ]]; then
      local domain
      domain=$(echo "$source" | awk -F/ '{print $3}')
      SOURCE_ATTRIBUTION="Source: $domain"
    else
      SOURCE_ATTRIBUTION="Source: $(basename "$source")"
    fi
  fi

  log_info "source attribution: $SOURCE_ATTRIBUTION"
  export SOURCE_ATTRIBUTION FIXTURE_LICENSE
}

# Write the machine-readable companion record to outputs/SOURCES.txt.
# Required by docs/copyright-policy.md before any publish step is wired up.
write_sources_record() {
  local mission_id="$1" source="$2" out_path="$3"
  cat > "$out_path" <<TXTEOF
mission_id: $mission_id
source: $source
attribution: ${SOURCE_ATTRIBUTION:-unknown}
license: ${FIXTURE_LICENSE:-unknown}
recorded_at: $(date -u +%Y-%m-%dT%H:%M:%SZ)
TXTEOF
}
