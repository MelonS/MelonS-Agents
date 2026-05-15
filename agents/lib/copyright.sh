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
# `check_url_struck` reads this log to refuse future renders of the same URL.
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

# Probe a remote source's machine-readable license metadata and write it
# to <out_json>.  Returns the canonical license tag (e.g. "CC-BY-3.0") on
# stdout, or empty + non-zero exit if the probe can't determine a license.
#
# Today this knows two hosts:
#   - archive.org: /metadata/<identifier> endpoint, reads `licenseurl`
#   - commons.wikimedia.org: extmetadata API, reads License.value
#
# Both return CC URLs / CC tags that map cleanly onto the publish_rules
# entries in config/copyright-allowlist.yaml.  Other hosts return non-zero
# so the caller can fall back to the "requires-per-item-probe" rule.
probe_license() {
  local url="$1" out_json="$2"
  local host
  host=$(echo "$url" | awk -F/ '{print tolower($3)}')

  python3 - "$url" "$host" "$out_json" <<'PY'
import sys, json, re, urllib.request, urllib.parse

url, host, out = sys.argv[1], sys.argv[2], sys.argv[3]

def cc_url_to_tag(u):
    # http://creativecommons.org/licenses/by/3.0/ → CC-BY-3.0
    m = re.match(r"https?://creativecommons\.org/licenses/([a-z\-]+)/([0-9.]+)/?", u or "")
    if not m: return ""
    return "CC-" + m.group(1).upper() + "-" + m.group(2)

def cc_tag_normalize(t):
    # "cc-by-3.0" → "CC-BY-3.0"
    if not t: return ""
    parts = t.split("-")
    return "-".join(p.upper() if p == "cc" or all(c.isalpha() for c in p) else p for p in parts)

def fetch_json(u, timeout=8):
    req = urllib.request.Request(u, headers={"User-Agent": "MelonS-Agents/1 (license-probe)"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read())

result = {"url": url, "host": host, "license": "", "license_url": "", "source": "probe"}

try:
    if host.endswith("archive.org"):
        # URL like https://archive.org/download/<id>/.../file.mp4
        m = re.search(r"archive\.org/(?:download|details)/([^/]+)", url)
        if not m:
            raise ValueError("could not extract archive.org item identifier")
        ident = m.group(1)
        meta = fetch_json(f"https://archive.org/metadata/{urllib.parse.quote(ident)}")
        license_url = meta.get("metadata", {}).get("licenseurl", "")
        result["license_url"] = license_url
        result["license"] = cc_url_to_tag(license_url)
        result["identifier"] = ident
    elif host.endswith("commons.wikimedia.org") or host.endswith("upload.wikimedia.org"):
        # commons.wikimedia.org/wiki/File:Foo.webm  OR
        # upload.wikimedia.org/wikipedia/commons/.../File.webm
        title = ""
        m = re.search(r"/wiki/(File:[^?#]+)", url)
        if m:
            title = urllib.parse.unquote(m.group(1))
        else:
            m = re.search(r"/wikipedia/commons/.+/([^/?#]+\.[A-Za-z0-9]+)$", url)
            if m: title = "File:" + urllib.parse.unquote(m.group(1))
        if not title:
            raise ValueError("could not extract wikimedia file title from URL")
        api = ("https://commons.wikimedia.org/w/api.php?"
               + urllib.parse.urlencode({
                   "action": "query",
                   "prop": "imageinfo",
                   "iiprop": "extmetadata",
                   "format": "json",
                   "titles": title,
               }))
        meta = fetch_json(api)
        pages = meta.get("query", {}).get("pages", {})
        for _, page in pages.items():
            info = page.get("imageinfo", [{}])[0]
            em = info.get("extmetadata", {})
            result["license"] = cc_tag_normalize(em.get("License", {}).get("value", ""))
            result["license_url"] = em.get("LicenseUrl", {}).get("value", "")
            result["title"] = page.get("title", title)
            result["artist"] = em.get("Artist", {}).get("value", "")
            break
    else:
        result["error"] = f"no probe implemented for host '{host}'"
except Exception as e:
    result["error"] = str(e)

with open(out, "w") as f:
    json.dump(result, f, indent=2)

if result["license"]:
    print(result["license"])
    sys.exit(0)
sys.exit(1)
PY
}

# Resolve the final license string for a source.  Precedence (most-specific
# first):
#   1. FIXTURE_LICENSE (set earlier by resolve_source_attribution if the
#      source matched config/fixtures.yaml)
#   2. probe_license result, IF the allowlist verdict was
#      "requires-per-item-probe"
#   3. The allowlist's license_default for the domain
#
# Side effect: sets FIXTURE_LICENSE to the resolved value so
# write_sources_record picks it up.  Writes resources/license.json under
# $mdir when a probe runs.
resolve_final_license() {
  local source="$1" allowlist_license="$2" mdir="$3"

  if [[ -n "${FIXTURE_LICENSE:-}" ]]; then
    return 0  # catalog already won
  fi

  if [[ "$allowlist_license" == "requires-per-item-probe" ]]; then
    mkdir -p "$mdir/resources"
    if probed=$(probe_license "$source" "$mdir/resources/license.json"); then
      FIXTURE_LICENSE="$probed"
      export FIXTURE_LICENSE
      return 0
    else
      echo "⚠ license probe failed for $source — leaving as 'requires-per-item-probe' (publish gate will refuse)" >&2
      FIXTURE_LICENSE="requires-per-item-probe"
      export FIXTURE_LICENSE
      return 0
    fi
  fi

  # Fall back to the allowlist default ("local-path", "CC-BY-3.0", etc.)
  if [[ -n "$allowlist_license" && "$allowlist_license" != "local-path" ]]; then
    FIXTURE_LICENSE="$allowlist_license"
    export FIXTURE_LICENSE
  fi
}
