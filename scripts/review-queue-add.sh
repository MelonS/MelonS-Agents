#!/usr/bin/env bash
# Append an artifact to the review queue.
#
# Lever 3 from docs/research/2026-05-22-intervention-reduction.md:
# instead of pinging the operator per-render, missions enqueue here
# and the operator drains via scripts/review-queue-digest.sh on
# their own cadence.
#
# Usage:
#   scripts/review-queue-add.sh <mission_id> <artifact_path> [reason]
#
# Auto-discovers preview_jpg (sibling preview-frame.jpg), reads music
# file + mood keywords from the mission's metrics.json when present.
# Idempotent: if an entry with the same mission_id already exists in
# pending/, the script no-ops (avoids duplicates from retry loops).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
QUEUE_DIR="$REPO_ROOT/outputs/review-queue/pending"
mkdir -p "$QUEUE_DIR"

mission_id="${1:?usage: review-queue-add.sh <mission_id> <artifact_path> [reason]}"
artifact="${2:?missing artifact path}"
reason="${3:-auto-enqueued for batch review}"

if [[ ! -f "$artifact" ]]; then
  echo "[review-queue-add] artifact missing: $artifact" >&2
  exit 1
fi

# Idempotent guard.
existing=$(find "$QUEUE_DIR" -name "*-${mission_id}.json" 2>/dev/null | head -1)
if [[ -n "$existing" ]]; then
  echo "[review-queue-add] already queued: $(basename "$existing")"
  exit 0
fi

date_str=$(date -u +%Y-%m-%d)
out="$QUEUE_DIR/${date_str}-${mission_id}.json"

# Auto-discover sidecar fields.
artifact_dir="$(dirname "$artifact")"
preview_jpg="$artifact_dir/preview-frame.jpg"
metrics_json="$(dirname "$artifact_dir")/metrics.json"

mission_type="unknown"
music_file=""
duration_s=0
mood_keywords="[]"

if [[ -f "$metrics_json" ]] && command -v jq >/dev/null 2>&1; then
  mission_type=$(jq -r '.mission_type // "unknown"' "$metrics_json" 2>/dev/null)
  music_file=$(jq -r '.music_file // ""' "$metrics_json" 2>/dev/null)
  duration_s=$(jq -r '.actual_duration_s // 0' "$metrics_json" 2>/dev/null)
  # mood_keywords stored as array in some metrics; otherwise leave [].
  kw=$(jq -c '.mood_keywords // []' "$metrics_json" 2>/dev/null)
  [[ -n "$kw" ]] && mood_keywords="$kw"
fi

size_bytes=$(stat -f %z "$artifact" 2>/dev/null || stat -c %s "$artifact" 2>/dev/null || echo 0)
queued_at=$(date -u +%FT%TZ)

# Render JSON via jq for proper escaping.
jq -n \
  --arg mission_id "$mission_id" \
  --arg mission_type "$mission_type" \
  --arg artifact "$artifact" \
  --arg preview_jpg "$([[ -f "$preview_jpg" ]] && echo "$preview_jpg" || echo "")" \
  --arg queued_at "$queued_at" \
  --argjson mood_keywords "$mood_keywords" \
  --arg music_file "$music_file" \
  --argjson duration_s "$duration_s" \
  --argjson size_bytes "$size_bytes" \
  --arg reason "$reason" \
  '{
    mission_id: $mission_id,
    mission_type: $mission_type,
    artifact_path: $artifact,
    preview_jpg: (if $preview_jpg == "" then null else $preview_jpg end),
    queued_at: $queued_at,
    mood_keywords: $mood_keywords,
    music_file: $music_file,
    duration_s: $duration_s,
    size_bytes: $size_bytes,
    reason: $reason
  }' > "$out"

echo "[review-queue-add] queued: $(basename "$out")"
