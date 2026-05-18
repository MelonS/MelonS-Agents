#!/usr/bin/env bash
# Fetch a curated demo music cache — no Suno account, no signup, no .env edit.
#
# Why this exists:
#   The music-video mission needs an operator-supplied music file as its
#   primary audio.  Today the supply path is Suno: signup → OAuth →
#   custom-mode prompt in the web UI → wait for generation → pick best
#   of N → download mp3 → manually drop in assets/music/ → manually
#   update SOURCES.md.  Six-step manual round-trip.  First-time clones
#   bail before the music-video pipeline ever runs.
#
#   This script pulls a small set of Kevin MacLeod tracks from
#   incompetech.com (all CC-BY 4.0) so a brand-new clone can produce a
#   demo music-video output with zero accounts.  Five moods cover the
#   keyword categories the mission already understands.
#
# Usage:
#   ./scripts/fetch-demo-music.sh                   # fetch all demo tracks
#   ./scripts/fetch-demo-music.sh carefree          # fetch one by id
#
# Output (per track):
#   $FIXTURE_DIR/demo-music/<id>.mp3                audio file
#   $FIXTURE_DIR/demo-music/<id>.meta.json          sidecar (composer,
#                                                   license, page URL)
#   $FIXTURE_DIR/demo-music/SOURCES.md              human-readable
#                                                   attribution index
#                                                   (regenerated each run)
#   stdout                                          one local path per
#                                                   fetched track
#
# Idempotent: skips downloads where the cache file already exists.
#
# All tracks are CC-BY 4.0; attribution MUST be surfaced in any output
# that uses them.  The music-video mission's
# resolve_source_attribution() reads the .meta.json sidecar and burns
# the credit string in.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# shellcheck disable=SC1091
[[ -f "$REPO_ROOT/agents/lib/env.sh" ]] && source "$REPO_ROOT/agents/lib/env.sh"

FIXTURE_DIR="${FIXTURE_DIR:-/tmp/smoke}"
DEST="$FIXTURE_DIR/demo-music"
mkdir -p "$DEST"

WANTED_ID="${1:-}"

# Curated CC-BY 4.0 set.  Each entry:
#   id|url|title|mood|composer|page_url
# Five tracks chosen to span the mood-keyword categories the
# music-video mission already understands (mellow / energetic /
# cinematic / chill / upbeat).
TRACKS=(
  "carefree|https://incompetech.com/music/royalty-free/mp3-royaltyfree/Carefree.mp3|Carefree|upbeat|Kevin MacLeod|https://incompetech.com/music/royalty-free/music.html"
  "sneaky-snitch|https://incompetech.com/music/royalty-free/mp3-royaltyfree/Sneaky%20Snitch.mp3|Sneaky Snitch|cinematic|Kevin MacLeod|https://incompetech.com/music/royalty-free/music.html"
  "hyperfun|https://incompetech.com/music/royalty-free/mp3-royaltyfree/Hyperfun.mp3|Hyperfun|energetic|Kevin MacLeod|https://incompetech.com/music/royalty-free/music.html"
  "local-forecast|https://incompetech.com/music/royalty-free/mp3-royaltyfree/Local%20Forecast.mp3|Local Forecast|mellow|Kevin MacLeod|https://incompetech.com/music/royalty-free/music.html"
  "wallpaper|https://incompetech.com/music/royalty-free/mp3-royaltyfree/Wallpaper.mp3|Wallpaper|chill|Kevin MacLeod|https://incompetech.com/music/royalty-free/music.html"
)

echo "[demo-music] destination: $DEST" >&2

FETCHED=0
SKIPPED=0
FAILED=0
SOURCES_BODY=""

for entry in "${TRACKS[@]}"; do
  IFS='|' read -r id url title mood composer page <<< "$entry"
  if [[ -n "$WANTED_ID" && "$id" != "$WANTED_ID" ]]; then
    continue
  fi

  out_mp3="$DEST/${id}.mp3"
  out_meta="$DEST/${id}.meta.json"
  attr="\"$title\" by $composer — incompetech.com"

  if [[ -s "$out_mp3" ]]; then
    echo "  [skip] $id (cached at $out_mp3)" >&2
    echo "$out_mp3"
    SKIPPED=$((SKIPPED + 1))
    SOURCES_BODY+="- **$title** ($mood) — $composer — [$page]($page) — CC-BY 4.0\n"
    continue
  fi

  echo "  [get ] $id from $url" >&2
  if ! curl -fsSL --connect-timeout 15 -o "$out_mp3.part" "$url"; then
    echo "  [fail] $id download failed" >&2
    rm -f "$out_mp3.part"
    FAILED=$((FAILED + 1))
    continue
  fi
  mv "$out_mp3.part" "$out_mp3"

  cat > "$out_meta" <<JSONEOF
{
  "id": "$id",
  "source": "incompetech-demo",
  "page_url": "$page",
  "file_url": "$url",
  "title": "$title",
  "composer": "$composer",
  "mood": "$mood",
  "license": "CC-BY-4.0",
  "license_url": "https://creativecommons.org/licenses/by/4.0/",
  "attribution_string": $(printf '%s' "$attr" | python3 -c 'import json,sys; print(json.dumps(sys.stdin.read()))'),
  "fetched_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
JSONEOF

  echo "$out_mp3"
  FETCHED=$((FETCHED + 1))
  SOURCES_BODY+="- **$title** ($mood) — $composer — [$page]($page) — CC-BY 4.0\n"
done

# Regenerate the human-readable attribution index.  This file is the
# operator-facing equivalent of outputs/SOURCES.txt; the mission's own
# attribution burn-in is independent of this file.
{
  echo "# Demo music sources"
  echo
  echo "Curated CC-BY 4.0 tracks fetched by \`scripts/fetch-demo-music.sh\`."
  echo "All tracks by Kevin MacLeod via incompetech.com — attribution required."
  echo
  printf "%b" "$SOURCES_BODY"
  echo
  echo "Generated: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
} > "$DEST/SOURCES.md"

echo "[demo-music] done — fetched=$FETCHED skipped=$SKIPPED failed=$FAILED" >&2

if [[ "$FAILED" -gt 0 && "$FETCHED" -eq 0 && "$SKIPPED" -eq 0 ]]; then
  exit 67
fi
exit 0
