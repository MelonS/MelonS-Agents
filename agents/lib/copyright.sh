#!/usr/bin/env bash
# Copyright / licensing primitives — first-line defense for source URLs
# and the publish gate.  Implements the v1 subset of the punch list in
# docs/copyright-policy.md:
#
#   1. Domain allowlist  ─ check_source_allowed()
#   2. Publish gate hook ─ guard_publish()
#   3. Strike-record log ─ append_strike()
#
# Still TODO (see docs/copyright-policy.md):
#   - License-string probe for archive.org / wikimedia / vimeo CC channel
#     items (today the allowlist marks these "requires-per-item-probe"
#     and guard_publish refuses to publish them; the actual probe code
#     is not yet implemented).
#   - Audio fingerprint check (would need chromaprint/`fpcalc`).
#   - Logo / watermark detection.
#   - Per-platform reuse rules.

: "${COPYRIGHT_ALLOWLIST:=$REPO_ROOT/config/copyright-allowlist.yaml}"
: "${STRIKE_LOG:=$RECORDS_DIR/strikes.log}"

# Has this URL been struck before? Returns 0 if struck (third column of
# any row in $STRIKE_LOG matches), non-zero otherwise.  Echoes the
# offending strike row to stderr on a hit so the caller's error message
# can surface it.
check_url_struck() {
  local url="$1"
  [[ -f "$STRIKE_LOG" ]] || return 1
  # Strike log rows: <timestamp>\t<mission_id>\t<source_url>\t<reason>
  local hit
  hit=$(awk -F'\t' -v want="$url" '$3 == want { print; exit }' "$STRIKE_LOG")
  if [[ -n "$hit" ]]; then
    echo "$hit" >&2
    return 0
  fi
  return 1
}

# Returns 0 if SOURCE is allowed, non-zero with stderr explanation if not.
# Local paths (no http(s):// prefix) always pass — fixture catalog handles
# their licensing instead.  Echoes the matched license_default on success.
# Rejects URLs that have been recorded in $STRIKE_LOG.
check_source_allowed() {
  local source="$1"

  # Local file path — skip the URL check; fixture catalog handles it.
  if [[ ! "$source" =~ ^https?:// ]]; then
    echo "local-path"
    return 0
  fi

  # Strike check runs BEFORE the allowlist: a previously-struck URL is
  # never publishable even if its domain is on the allowlist.
  if check_url_struck "$source" 2>/tmp/.copyright-strike-row; then
    {
      echo "url '$source' has a prior strike — refusing"
      echo "  $(cat /tmp/.copyright-strike-row)"
    } >&2
    rm -f /tmp/.copyright-strike-row
    return 6
  fi
  rm -f /tmp/.copyright-strike-row

  if [[ ! -f "$COPYRIGHT_ALLOWLIST" ]]; then
    echo "❌ COPYRIGHT_ALLOWLIST not found: $COPYRIGHT_ALLOWLIST" >&2
    return 2
  fi

  local host
  host=$(echo "$source" | awk -F/ '{print tolower($3)}')

  python3 - "$COPYRIGHT_ALLOWLIST" "$host" <<'PY'
import sys, re
path, host = sys.argv[1], sys.argv[2]
text = open(path).read()
# Walk the "domains:" list — flat YAML so a regex pass is enough.
in_block = False
items, cur = [], None
for line in text.splitlines():
    if line.startswith("domains:"):
        in_block = True; continue
    if in_block and re.match(r"^[A-Za-z_]+:", line):
        in_block = False
    if not in_block:
        continue
    m = re.match(r"^  - domain: (.+)$", line)
    if m:
        if cur: items.append(cur)
        cur = {"domain": m.group(1).strip()}
        continue
    if cur is None:
        continue
    for key in ("license_default", "note"):
        m = re.match(rf"^    {key}: (.+)$", line)
        if m: cur[key] = m.group(1).strip()
if cur: items.append(cur)

for it in items:
    d = it["domain"].lower()
    if host == d or host.endswith("." + d):
        print(it.get("license_default", "unknown"))
        sys.exit(0)

print(f"host '{host}' not on copyright allowlist (config/copyright-allowlist.yaml)", file=sys.stderr)
sys.exit(1)
PY
}

# Publish gate — read a mission's SOURCES.txt + the allowlist's publish_rules,
# refuse the publish if license is unknown / blocked / missing.  Stub for
# now; the actual publish.sh that would call this isn't wired yet, but
# this lets any future publish script gate behind one line.
guard_publish() {
  local sources_txt="$1"

  if [[ ! -f "$sources_txt" ]]; then
    echo "❌ guard_publish: SOURCES.txt missing — refusing publish" >&2
    return 3
  fi

  local license
  license=$(awk -F': ' '/^license:/ {print $2; exit}' "$sources_txt")
  if [[ -z "$license" || "$license" == "unknown" ]]; then
    echo "❌ guard_publish: license is '${license:-empty}' — refusing publish (record license in fixture catalog)" >&2
    return 4
  fi

  python3 - "$COPYRIGHT_ALLOWLIST" "$license" <<'PY' || return 5
import sys, re
path, want = sys.argv[1], sys.argv[2]
text = open(path).read()
in_block = False
items, cur = [], None
for line in text.splitlines():
    if line.startswith("publish_rules:"):
        in_block = True; continue
    if in_block and re.match(r"^[A-Za-z_]+:", line):
        in_block = False
    if not in_block:
        continue
    m = re.match(r"^  - license: (.+)$", line)
    if m:
        if cur: items.append(cur)
        cur = {"license": m.group(1).strip()}
        continue
    if cur is None:
        continue
    m = re.match(r"^    publish_blocked: (true|false)$", line)
    if m: cur["publish_blocked"] = (m.group(1) == "true")
    m = re.match(r"^    reason: (.+)$", line)
    if m: cur["reason"] = m.group(1).strip()
if cur: items.append(cur)

for it in items:
    if it["license"] == want:
        if it.get("publish_blocked"):
            print(f"license '{want}' is publish-blocked: {it.get('reason','no reason recorded')}", file=sys.stderr)
            sys.exit(1)
        sys.exit(0)
# License not listed — be conservative and refuse.
print(f"license '{want}' has no publish rule in allowlist — refusing", file=sys.stderr)
sys.exit(1)
PY
  return 0
}

# Append-only strike log — call when a published short receives a takedown.
# The next render that names the same source URL can then be auto-rejected.
# (Auto-rejection lookup is TODO; the log is here so the data exists when
# we add it.)
append_strike() {
  local mission_id="$1" source_url="$2" reason="$3"
  mkdir -p "$(dirname "$STRIKE_LOG")"
  printf '%s\t%s\t%s\t%s\n' \
    "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
    "$mission_id" \
    "$source_url" \
    "$reason" \
    >> "$STRIKE_LOG"
  echo "$STRIKE_LOG"
}
