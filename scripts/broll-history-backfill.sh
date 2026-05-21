#!/usr/bin/env bash
# broll-history-backfill.sh — seed the B-roll history registry from
# previously-rendered missions.
#
# Walks records/missions/*/resources/pexels/*.meta.json (all the
# sidecar files written by pexels-fetch.sh) and appends each unique
# id to records/youtube/broll-used.txt.  Idempotent — re-running adds
# nothing new.
#
# Usage:
#   scripts/broll-history-backfill.sh                # full repo walk
#   scripts/broll-history-backfill.sh <dir>          # walk a subtree
#
# Companion to the dedup behavior in scripts/pexels-fetch.sh
# (BROLL_HISTORY=on by default).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT="${1:-$REPO_ROOT/records/missions}"
HIST="${BROLL_HISTORY_FILE:-$REPO_ROOT/records/youtube/broll-used.txt}"

mkdir -p "$(dirname "$HIST")"
touch "$HIST"

before=$(wc -l < "$HIST" | tr -d ' ')

# Format A — pexels-fetch.sh sidecars: `<id>.meta.json` with `{"id": N, "source":"pexels", ...}`.
# Format B — music-video pipeline cache: `win-<keyword>.json` with the raw Pexels API response
#            shape `{"videos":[{"id":N, ...}]}`.  Files live under
#            records/missions/*/resources/clips/.  Backfill conservatively
#            takes the top-3 candidate ids per file (typical N picked from
#            top of the per_page response — over-exclusion is fine).

found=0
while IFS= read -r f; do
  # Format A: single id at root.
  id_a=$(jq -r 'if type=="object" and .id and (.source=="pexels" or has("video_files")|not) then .id else empty end' "$f" 2>/dev/null)
  if [[ -n "$id_a" ]]; then
    found=$(( found + 1 ))
    grep -qxF "$id_a" "$HIST" 2>/dev/null || echo "$id_a" >> "$HIST"
    continue
  fi
  # Format B: top-3 candidate ids inside .videos[].
  while IFS= read -r id_b; do
    [[ -z "$id_b" ]] && continue
    found=$(( found + 1 ))
    grep -qxF "$id_b" "$HIST" 2>/dev/null || echo "$id_b" >> "$HIST"
  done < <(jq -r '.videos[0:3][]?.id // empty' "$f" 2>/dev/null)
done < <(find "$ROOT" -type f \( -name '*.meta.json' -o -path '*/clips/*.json' -o -path '*/pexels/*.json' \) 2>/dev/null)

# Also walk $FIXTURE_DIR/pexels (the cache used by ad-hoc renders) if
# present — these may not have an enclosing mission directory.
fixture_pexels="${FIXTURE_DIR:-/tmp/smoke}/pexels"
if [[ -d "$fixture_pexels" ]]; then
  while IFS= read -r meta; do
    id=$(jq -r '.id // empty' "$meta" 2>/dev/null)
    [[ -z "$id" ]] && continue
    found=$(( found + 1 ))
    grep -qxF "$id" "$HIST" 2>/dev/null || echo "$id" >> "$HIST"
  done < <(find "$fixture_pexels" -maxdepth 1 -name '*.meta.json' 2>/dev/null)
fi

after=$(wc -l < "$HIST" | tr -d ' ')
added=$(( after - before ))

echo "[backfill] scanned $found sidecars under $ROOT (and $fixture_pexels if present)"
echo "[backfill] history: $before → $after entries (+$added new)"
echo "[backfill] registry: $HIST"
