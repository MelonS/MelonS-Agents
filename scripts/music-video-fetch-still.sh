#!/usr/bin/env bash
# Fetch a single Pexels portrait photo matching a mood keyword and save
# locally.  Used by music-video-auto.sh when --image is not supplied for
# a stillzoom-mode genre (ambient/classical/dreamcore).
#
# Usage:
#   scripts/music-video-fetch-still.sh <query> <output.jpg>
#
# Example:
#   scripts/music-video-fetch-still.sh "soft window light" /tmp/still.jpg
#
# Cache: ~/.cache/music-video-stills/<sha1-of-query>.jpg
# Re-uses if already fetched (deterministic per query — same query → same
# image, intentional for visual consistency across runs).
#
# Requires: PEXELS_API_KEY in .env, curl, jq, sha1sum (or shasum).

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
# shellcheck disable=SC1091
source "$PWD/agents/lib/env.sh" 2>/dev/null || true

QUERY="${1:-}"
DST="${2:-}"

if [[ -z "$QUERY" || -z "$DST" ]]; then
  echo "usage: $0 <query> <output.jpg>" >&2
  exit 64
fi
if [[ -z "${PEXELS_API_KEY:-}" ]]; then
  echo "❌ PEXELS_API_KEY not set in .env" >&2
  exit 1
fi

CACHE_DIR="$HOME/.cache/music-video-stills"
mkdir -p "$CACHE_DIR"

# Cache key = sha1 of query (deterministic)
if command -v sha1sum >/dev/null 2>&1; then
  KEY=$(printf '%s' "$QUERY" | sha1sum | awk '{print $1}')
else
  KEY=$(printf '%s' "$QUERY" | shasum | awk '{print $1}')
fi
CACHE="$CACHE_DIR/${KEY}.jpg"

if [[ -f "$CACHE" ]]; then
  echo "→ cache hit for '$QUERY' → $CACHE"
  cp "$CACHE" "$DST"
  echo "✓ still: $DST"
  exit 0
fi

# Pexels photo search — portrait orientation, large size
ENC=$(printf '%s' "$QUERY" | jq -sRr @uri)
JSON=$(curl -sS -H "Authorization: $PEXELS_API_KEY" \
  "https://api.pexels.com/v1/search?query=${ENC}&orientation=portrait&size=large&per_page=5" 2>&1)

# Pick first photo's "portrait" or "large" URL
URL=$(echo "$JSON" | jq -r '.photos[0].src.portrait // .photos[0].src.large // empty')

if [[ -z "$URL" || "$URL" == "null" ]]; then
  echo "❌ no photo result for '$QUERY'" >&2
  echo "  api response (first 200 chars): $(echo "$JSON" | head -c 200)" >&2
  exit 1
fi

curl -sSL "$URL" -o "$CACHE"
if [[ ! -s "$CACHE" ]]; then
  echo "❌ download failed: $URL" >&2
  rm -f "$CACHE"
  exit 1
fi

cp "$CACHE" "$DST"
echo "→ fetched '$QUERY' → $CACHE"

# Sidecar attribution file
PHOTOGRAPHER=$(echo "$JSON" | jq -r '.photos[0].photographer // "unknown"')
PHOTO_URL=$(echo "$JSON" | jq -r '.photos[0].url // ""')
cat > "${CACHE%.jpg}.meta.json" <<EOF
{
  "query": "$QUERY",
  "source": "pexels.com",
  "photographer": "$PHOTOGRAPHER",
  "page_url": "$PHOTO_URL",
  "license": "Pexels (free license, attribution appreciated)",
  "attribution_string": "Photo by $PHOTOGRAPHER on Pexels"
}
EOF

echo "✓ still: $DST  (attribution at ${CACHE%.jpg}.meta.json)"
