#!/usr/bin/env bash
# Fetch & probe the real-URL fixtures listed in config/fixtures.yaml.
# Lands files under $FIXTURE_DIR (default /tmp/smoke/) and verifies each
# file's resolution/duration/audio matches the catalog's expectations.
#
# Usage:
#   ./scripts/fetch-fixtures.sh           # fetch all
#   ./scripts/fetch-fixtures.sh bbb_1min  # fetch one by id
#
# Idempotent: skips downloads that already exist and pass probing.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh"

FIXTURE_DIR="${FIXTURE_DIR:-/tmp/smoke}"
CATALOG="$REPO_ROOT/config/fixtures.yaml"
WANTED_ID="${1:-}"

mkdir -p "$FIXTURE_DIR"

if ! command -v python3 >/dev/null 2>&1; then
  echo "❌ python3 required to parse $CATALOG" >&2
  exit 1
fi

# Emit "id<TAB>url<TAB>w<TAB>h<TAB>min_dur<TAB>has_audio<TAB>attribution" per fixture.
# (Avoid `mapfile` — macOS still ships bash 3.2 which doesn't have it.
#  Also avoid nesting a python heredoc inside `< <(...)` — bash parses the
#  inner stdin redirection ambiguously on 3.2; capture into a string first.)
PARSED=$(python3 - "$CATALOG" "$WANTED_ID" <<'PY'
import sys, re
path, wanted = sys.argv[1], sys.argv[2]
text = open(path).read()
# Minimal YAML walker — we only need top-level `fixtures:` list, no deps.
in_block = False
items = []
cur = None
for line in text.splitlines():
    if line.startswith("fixtures:"):
        in_block = True
        continue
    if in_block and re.match(r"^[A-Za-z_]+:", line):
        in_block = False
    if not in_block:
        continue
    m = re.match(r"^  - id: (.+)$", line)
    if m:
        if cur: items.append(cur)
        cur = {"id": m.group(1).strip(), "expected": {}}
        continue
    if cur is None: continue
    m = re.match(r"^    url: (.+)$", line)
    if m: cur["url"] = m.group(1).strip()
    m = re.match(r"^    source_attribution: \"?(.+?)\"?$", line)
    if m: cur["attr"] = m.group(1).strip()
    m = re.match(r"^      width: (\d+)$", line)
    if m: cur["expected"]["w"] = m.group(1)
    m = re.match(r"^      height: (\d+)$", line)
    if m: cur["expected"]["h"] = m.group(1)
    m = re.match(r"^      min_duration_s: (\d+)$", line)
    if m: cur["expected"]["min_dur"] = m.group(1)
    m = re.match(r"^      has_audio: (true|false)$", line)
    if m: cur["expected"]["audio"] = m.group(1)
if cur: items.append(cur)

for it in items:
    if wanted and it["id"] != wanted: continue
    e = it["expected"]
    print("\t".join([
        it["id"], it["url"], it.get("attr",""),
        e.get("w","0"), e.get("h","0"), e.get("min_dur","0"), e.get("audio","false"),
    ]))
PY
)

ROWS=()
while IFS= read -r row; do
  [[ -n "$row" ]] && ROWS+=("$row")
done <<< "$PARSED"

if (( ${#ROWS[@]} == 0 )); then
  echo "❌ no fixtures matched${WANTED_ID:+ id=$WANTED_ID}" >&2
  exit 2
fi

failed=0
for row in "${ROWS[@]}"; do
  IFS=$'\t' read -r fid url attr exp_w exp_h exp_dur exp_audio <<<"$row"
  out="$FIXTURE_DIR/${fid}.mp4"
  printf "── %s ──\n" "$fid"
  echo "  url:  $url"
  echo "  attr: $attr"

  # Skip download if already on disk and probes OK.
  if [[ -s "$out" ]]; then
    echo "  ✅ exists ($(du -h "$out" | awk '{print $1}'))"
  else
    echo "  ⤵  downloading…"
    if ! curl -fsSL --connect-timeout 10 -o "$out.part" "$url"; then
      echo "  ❌ download failed" >&2
      rm -f "$out.part"
      failed=$((failed+1))
      continue
    fi
    mv "$out.part" "$out"
    echo "  ✅ downloaded ($(du -h "$out" | awk '{print $1}'))"
  fi

  # Probe — width, height, duration, audio presence.
  w=$("$FFPROBE_BIN" -v error -select_streams v:0 -show_entries stream=width  -of csv=p=0 "$out" | head -1)
  h=$("$FFPROBE_BIN" -v error -select_streams v:0 -show_entries stream=height -of csv=p=0 "$out" | head -1)
  dur=$("$FFPROBE_BIN" -v error -show_entries format=duration -of default=nw=1:nk=1 "$out" | awk '{printf "%.0f",$1}')
  audio_idx=$("$FFPROBE_BIN" -v error -select_streams a:0 -show_entries stream=index -of csv=p=0 "$out" | head -1)
  has_audio=$([[ -n "$audio_idx" ]] && echo true || echo false)

  echo "  probed: ${w}x${h}, ${dur}s, audio=$has_audio"

  ok=1
  (( w >= exp_w ))           || { echo "  ❌ width $w < expected $exp_w"; ok=0; }
  (( h >= exp_h ))           || { echo "  ❌ height $h < expected $exp_h"; ok=0; }
  (( dur >= exp_dur ))       || { echo "  ❌ duration ${dur}s < expected ${exp_dur}s"; ok=0; }
  [[ "$has_audio" == "$exp_audio" ]] || { echo "  ❌ audio=$has_audio, expected $exp_audio"; ok=0; }

  if (( ok == 1 )); then
    echo "  ✅ probe matches catalog"
  else
    failed=$((failed+1))
  fi
done

echo
if (( failed > 0 )); then
  echo "❌ $failed fixture(s) failed verification"
  exit 1
fi
echo "✅ all fixtures present & verified under $FIXTURE_DIR"
