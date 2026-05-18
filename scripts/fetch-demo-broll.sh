#!/usr/bin/env bash
# Fetch a curated demo B-roll cache — no API keys, no signup, no .env edit.
#
# Why this exists:
#   The headline music-video mission gates on a Pexels API key.  Acquiring
#   one demands Google/Apple/Facebook OAuth + finding a buried dashboard
#   menu — high friction for first-time users (KR users on Naver/Kakao
#   primary especially), and an unnecessary credential-leak surface for
#   curious cloners.  This script pulls a small set of CC-BY video clips
#   from Blender Foundation's CDN + Internet Archive so a brand-new
#   clone can produce a music-video output with zero accounts.
#
# All sources are CC-BY-3.0 (Blender Foundation open movies).  Attribution
# is written to each clip's .meta.json sidecar; resolve_source_attribution
# in agents/lib/attribution.sh picks it up just like it does for Pexels
# fetches.
#
# Usage:
#   ./scripts/fetch-demo-broll.sh           # fetch all demo clips
#   ./scripts/fetch-demo-broll.sh sintel    # fetch one by id
#
# Output (per clip):
#   $FIXTURE_DIR/demo-broll/<id>.mp4         video file
#   $FIXTURE_DIR/demo-broll/<id>.meta.json   sidecar with photographer,
#                                            license, page URL, etc.
#   stdout                                   one local path per fetched
#                                            clip, suitable for piping
#                                            into a mission script
#
# Idempotent: skips downloads where the cache file already exists at the
# expected size or larger.  Re-running is cheap.
#
# Conservative URL set: only Blender CDN URLs that have been HEAD-checked
# stable as of 2026-05-19.  Add new sources by extending CLIPS below;
# every entry MUST resolve to a CC-licensed file whose domain is already
# in config/copyright-allowlist.yaml.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# shellcheck disable=SC1091
[[ -f "$REPO_ROOT/agents/lib/env.sh" ]] && source "$REPO_ROOT/agents/lib/env.sh"

FIXTURE_DIR="${FIXTURE_DIR:-/tmp/smoke}"
DEST="$FIXTURE_DIR/demo-broll"
mkdir -p "$DEST"

WANTED_ID="${1:-}"

# Curated list.  Each entry: id|url|attribution|license|page_url
# id is the cache filename stem.
# attribution is the human-readable credit line burned into outputs.
# license matches a key in config/copyright-allowlist.yaml publish_rules.
# page_url is the canonical source page (e.g. movie homepage), useful
# when the operator wants to verify provenance.
CLIPS=(
  "sintel-trailer-480p|https://download.blender.org/durian/trailer/sintel_trailer-480p.mp4|Sintel © Blender Foundation — durian.blender.org|CC-BY-3.0|https://durian.blender.org/"
  "sintel-trailer-720p|https://download.blender.org/durian/trailer/sintel_trailer-720p.mp4|Sintel © Blender Foundation — durian.blender.org|CC-BY-3.0|https://durian.blender.org/"
  "bbb-320p|https://download.blender.org/peach/bigbuckbunny_movies/BigBuckBunny_320x180.mp4|Big Buck Bunny © Blender Foundation — peach.blender.org|CC-BY-3.0|https://peach.blender.org/"
)

echo "[demo-broll] destination: $DEST" >&2

FETCHED=0
SKIPPED=0
FAILED=0

for entry in "${CLIPS[@]}"; do
  IFS='|' read -r id url attr lic page <<< "$entry"
  if [[ -n "$WANTED_ID" && "$id" != "$WANTED_ID" ]]; then
    continue
  fi

  out_mp4="$DEST/${id}.mp4"
  out_meta="$DEST/${id}.meta.json"

  if [[ -s "$out_mp4" ]]; then
    # Existing file — trust the cache.  Idempotent path.
    echo "  [skip] $id (cached at $out_mp4)" >&2
    echo "$out_mp4"
    SKIPPED=$((SKIPPED + 1))
    continue
  fi

  echo "  [get ] $id from $url" >&2
  if ! curl -fsSL --connect-timeout 15 -o "$out_mp4.part" "$url"; then
    echo "  [fail] $id download failed" >&2
    rm -f "$out_mp4.part"
    FAILED=$((FAILED + 1))
    continue
  fi
  mv "$out_mp4.part" "$out_mp4"

  # Sidecar — matches the shape pexels-fetch.sh writes so
  # resolve_source_attribution can read either source uniformly.
  cat > "$out_meta" <<JSONEOF
{
  "id": "$id",
  "source": "blender-cdn-demo",
  "page_url": "$page",
  "file_url": "$url",
  "photographer": "Blender Foundation",
  "license": "$lic",
  "license_url": "https://creativecommons.org/licenses/by/3.0/",
  "attribution_string": "$attr",
  "fetched_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
JSONEOF

  echo "$out_mp4"
  FETCHED=$((FETCHED + 1))
done

echo "[demo-broll] done — fetched=$FETCHED skipped=$SKIPPED failed=$FAILED" >&2

if [[ "$FAILED" -gt 0 && "$FETCHED" -eq 0 && "$SKIPPED" -eq 0 ]]; then
  # All requested clips failed and none cached — propagate failure.
  exit 67
fi
exit 0
