#!/usr/bin/env bash
# Fetch stock B-roll from Pexels Videos API by search query.
#
# Pexels free tier (no card required): 200 req/hr, 20k req/month.
# All content is published under the Pexels License (commercial reuse
# allowed, attribution not required but appreciated).  See:
#   https://www.pexels.com/license/
#
# Usage:
#   ./scripts/pexels-fetch.sh "search query" [count=1] [min_height=720]
#
# Output:
#   $FIXTURE_DIR/pexels/<id>.mp4              video file
#   $FIXTURE_DIR/pexels/<id>.meta.json        sidecar with photographer,
#                                             page URL, license, etc.
#   stdout                                    one URL per line, suitable
#                                             for feeding into a mission
#                                             (./agents/missions/highlight/run.sh)
#
# The fetcher writes content under $FIXTURE_DIR/pexels/ rather than the
# regular $FIXTURE_DIR root so the synthetic + CC + stock layers stay
# separable.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh"

QUERY="${1:-}"
COUNT="${2:-1}"
MIN_HEIGHT="${3:-720}"

if [[ -z "$QUERY" ]]; then
  echo "usage: $0 <query> [count=1] [min_height=720]" >&2
  exit 64
fi
if [[ -z "${PEXELS_API_KEY:-}" ]]; then
  echo "❌ PEXELS_API_KEY not set in .env" >&2
  echo "   Get a free key at https://www.pexels.com/api/" >&2
  exit 65
fi

FIXTURE_DIR="${FIXTURE_DIR:-/tmp/smoke}"
DEST="$FIXTURE_DIR/pexels"
mkdir -p "$DEST"

# Pexels caps per_page at 80; we ask for 3× COUNT so we can skip videos
# whose best file is below MIN_HEIGHT.
PER_PAGE=$(( COUNT * 3 ))
if (( PER_PAGE > 80 )); then PER_PAGE=80; fi

API_URL="https://api.pexels.com/videos/search"

echo "[pexels] query='$QUERY' count=$COUNT min_height=$MIN_HEIGHT" >&2
RESPONSE=$(curl -fsSL --connect-timeout 15 \
  -H "Authorization: $PEXELS_API_KEY" \
  -G --data-urlencode "query=$QUERY" \
  --data-urlencode "per_page=$PER_PAGE" \
  "$API_URL")

# Pull out up to COUNT video entries that have a video_file ≥ MIN_HEIGHT.
# Emit tab-separated rows: id<TAB>page_url<TAB>photographer<TAB>file_url<TAB>width<TAB>height<TAB>duration.
# Pass the API response through an env var rather than stdin so we don't
# clash with python's `-` (read script from stdin) when nested in $(...).
PICKS=$(PEXELS_RESPONSE="$RESPONSE" python3 - "$COUNT" "$MIN_HEIGHT" <<'PY'
import sys, json, os
want, min_h = int(sys.argv[1]), int(sys.argv[2])
data = json.loads(os.environ["PEXELS_RESPONSE"])
out = []
for v in data.get("videos", []):
    # Prefer the smallest file at or above MIN_HEIGHT — keeps download
    # sizes manageable while honouring the resolution floor.
    candidates = sorted(
        (f for f in v.get("video_files", []) if f.get("height", 0) >= min_h and f.get("link")),
        key=lambda f: (f.get("height", 0), f.get("width", 0)),
    )
    if not candidates:
        continue
    f = candidates[0]
    out.append("\t".join([
        str(v.get("id", "")),
        v.get("url", ""),
        (v.get("user") or {}).get("name", "") or "Pexels user",
        f["link"],
        str(f.get("width", "?")),
        str(f.get("height", "?")),
        str(v.get("duration", "?")),
    ]))
    if len(out) >= want:
        break
print("\n".join(out))
PY
)

if [[ -z "$PICKS" ]]; then
  echo "❌ no Pexels results matched query='$QUERY' at min_height=$MIN_HEIGHT" >&2
  echo "   raw response:" >&2
  echo "$RESPONSE" | head -c 500 >&2
  exit 66
fi

# Process each pick.
while IFS=$'\t' read -r vid page user link w h dur; do
  [[ -z "$vid" ]] && continue
  out_mp4="$DEST/${vid}.mp4"
  out_meta="$DEST/${vid}.meta.json"

  if [[ -s "$out_mp4" ]]; then
    echo "  [skip] $out_mp4 already exists" >&2
  else
    echo "  [get ] $vid (${w}x${h}, ${dur}s) by '$user'" >&2
    if ! curl -fsSL --connect-timeout 15 -o "$out_mp4.part" "$link"; then
      echo "  [fail] download failed for $vid" >&2
      rm -f "$out_mp4.part"
      continue
    fi
    mv "$out_mp4.part" "$out_mp4"
  fi

  # Sidecar metadata — picked up by resolve_source_attribution when the
  # fixture catalog is re-scanned with this id, and also useful for
  # standalone audits later.
  cat > "$out_meta" <<JSONEOF
{
  "id": "$vid",
  "source": "pexels",
  "page_url": "$page",
  "file_url": "$link",
  "photographer": "$user",
  "width": $w,
  "height": $h,
  "duration_s": "$dur",
  "license": "pexels-license",
  "license_url": "https://www.pexels.com/license/",
  "attribution_string": "Video by $user on Pexels",
  "fetched_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
JSONEOF

  # Emit the LOCAL path so callers can pipe directly into mission scripts.
  echo "$out_mp4"
done <<< "$PICKS"
