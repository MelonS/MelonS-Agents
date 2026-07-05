#!/usr/bin/env bash
# yt-batch-upload.sh — YouTube Data API v3 batch uploader for music shorts (or any video).
#
# Usage:
#   scripts/yt-batch-upload.sh <metadata-dir>
#
# Each *.json in <metadata-dir> must be a youtubeuploader-compatible metadata file
# with one extra custom key — `_filename` — pointing to the mp4 to upload.
#
# Metadata JSON example:
#   {
#     "_filename": "outputs/publish/2026-05-20/2100-rain-lofi.mp4",
#     "title": "rainy window | lo-fi to study to",
#     "description": "...",
#     "tags": ["shorts", "lofi"],
#     "privacyStatus": "private",
#     "publishAt": "2026-05-20T12:00:00+00:00",
#     "madeForKids": false,
#     "categoryId": "10",
#     "language": "en"
#   }
#
# OAuth credentials are read from $HOME/.config/youtubeuploader/ (override with
# YT_SECRETS and YT_CACHE env vars). request.token is refreshed automatically.
#
# Strategy notes:
# - publishAt MUST be a future RFC3339 timestamp; past times → published-now +
#   privacyStatus stays private (manual flip needed).
# - YouTube Data API quota: 10000 units/day, upload = 1600 units → ~6 uploads/day
#   on the default free tier. Plan batches accordingly.

set -euo pipefail

META_DIR="${1:-}"
if [[ -z "$META_DIR" || ! -d "$META_DIR" ]]; then
  echo "Usage: $0 <metadata-dir>" >&2
  exit 1
fi

SECRETS="${YT_SECRETS:-$HOME/.config/youtubeuploader/client_secrets.json}"
CACHE="${YT_CACHE:-$HOME/.config/youtubeuploader/request.token}"

if [[ ! -f "$SECRETS" ]]; then
  echo "ERROR: OAuth client secrets not found at $SECRETS" >&2
  exit 1
fi
if [[ ! -f "$CACHE" ]]; then
  echo "ERROR: OAuth token cache not found at $CACHE" >&2
  echo "Run youtubeuploader manually once to complete first-run OAuth consent." >&2
  exit 1
fi
if ! command -v youtubeuploader >/dev/null 2>&1; then
  echo "ERROR: youtubeuploader not on PATH. Install: brew install youtubeuploader" >&2
  exit 1
fi

shopt -s nullglob
metas=("$META_DIR"/*.json)
shopt -u nullglob

if [[ ${#metas[@]} -eq 0 ]]; then
  echo "No *.json metadata files found in $META_DIR" >&2
  exit 1
fi

ok=0
fail=0
for meta in "${metas[@]}"; do
  mp4=$(python3 -c "import json,sys;print(json.load(open(sys.argv[1],encoding='utf-8')).get('_filename',''))" "$meta")
  if [[ -z "$mp4" ]]; then
    echo "SKIP: $meta — missing '_filename' field" >&2
    fail=$((fail + 1))
    continue
  fi
  if [[ ! -f "$mp4" ]]; then
    echo "SKIP: $meta — mp4 not found: $mp4" >&2
    fail=$((fail + 1))
    continue
  fi

  echo "=== $(basename "$meta") → $(basename "$mp4") ==="
  if youtubeuploader \
      -secrets "$SECRETS" \
      -cache   "$CACHE" \
      -filename "$mp4" \
      -metaJSON "$meta"; then
    ok=$((ok + 1))
  else
    echo "FAIL: $meta" >&2
    fail=$((fail + 1))
  fi
  echo
done

echo "── Summary ──"
echo "OK:   $ok"
echo "FAIL: $fail"
[[ $fail -eq 0 ]]
