#!/usr/bin/env bash
# Render the pending review queue to docs/review-digest.md.
#
# Lever 3 in docs/research/2026-05-22-intervention-reduction.md.
# Operator runs this when they want to drain accumulated taste
# decisions in one sitting.
#
# Usage:
#   scripts/review-queue-digest.sh
#
# Writes:
#   docs/review-digest.md  — single-page contact sheet
#
# Skips entries where the artifact_path no longer exists (clean-up
# happens elsewhere; the digest just doesn't show ghost rows).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
QUEUE_DIR="$REPO_ROOT/outputs/review-queue/pending"
OUT="$REPO_ROOT/docs/review-digest.md"

entries=$(find "$QUEUE_DIR" -maxdepth 1 -name "*.json" -type f 2>/dev/null | sort)

if [[ -z "$entries" ]]; then
  cat > "$OUT" <<EOF
# Review digest

_Generated $(date -u +%FT%TZ)._

Queue is empty.  No pending artifacts awaiting review.

To enqueue manually:

\`\`\`bash
scripts/review-queue-add.sh <mission_id> <path/to/short.mp4> ['reason']
\`\`\`

See [\`outputs/review-queue/README.md\`](../outputs/review-queue/README.md)
for the queue convention.
EOF
  echo "[review-digest] empty queue → $OUT"
  exit 0
fi

count=$(echo "$entries" | wc -l | tr -d ' ')

{
  echo "# Review digest"
  echo
  echo "_Generated $(date -u +%FT%TZ) — $count pending entries._"
  echo
  echo "Drain queue with verdict commands:"
  echo
  echo '```bash'
  echo "scripts/review-queue-decide.sh <mission_id> approve|reject|archive ['note']"
  echo '```'
  echo
  echo "---"
  echo

  i=0
  while IFS= read -r f; do
    i=$((i+1))
    mission_id=$(jq -r '.mission_id // "?"' "$f")
    mission_type=$(jq -r '.mission_type // "?"' "$f")
    artifact=$(jq -r '.artifact_path // ""' "$f")
    preview=$(jq -r '.preview_jpg // ""' "$f")
    queued=$(jq -r '.queued_at // ""' "$f")
    music=$(jq -r '.music_file // ""' "$f")
    dur=$(jq -r '.duration_s // 0' "$f")
    size=$(jq -r '.size_bytes // 0' "$f")
    reason=$(jq -r '.reason // ""' "$f")
    keywords=$(jq -r '.mood_keywords | if type=="array" then join(", ") else . end' "$f" 2>/dev/null)

    # Skip ghosts.
    if [[ -n "$artifact" && ! -f "$artifact" ]]; then
      echo "## ${i}. ${mission_id} (ARTIFACT MISSING)"
      echo
      echo "- Queued: $queued"
      echo "- Original path: \`$artifact\` — no longer exists"
      echo "- Recommend: \`scripts/review-queue-decide.sh $mission_id archive 'auto-pruned'\`"
      echo
      echo "---"
      echo
      continue
    fi

    size_mb=$(awk -v b="$size" 'BEGIN{printf "%.1f", b/1024/1024}')
    artifact_rel="${artifact#$REPO_ROOT/}"
    preview_rel=""
    [[ -n "$preview" && -f "$preview" ]] && preview_rel="${preview#$REPO_ROOT/}"

    echo "## ${i}. ${mission_id}"
    echo
    if [[ -n "$preview_rel" ]]; then
      echo "![preview frame for $mission_id](../$preview_rel)"
      echo
    fi
    echo "| Field | Value |"
    echo "|-------|-------|"
    echo "| Type | $mission_type |"
    echo "| Artifact | \`$artifact_rel\` |"
    echo "| Duration | ${dur}s |"
    echo "| Size | ${size_mb} MB |"
    [[ -n "$music" ]] && echo "| Music | \`$(basename "$music")\` |"
    [[ -n "$keywords" && "$keywords" != "null" ]] && echo "| Mood | $keywords |"
    echo "| Queued | $queued |"
    [[ -n "$reason" ]] && echo "| Reason | $reason |"
    echo
    echo "Verdict:"
    echo
    echo '```bash'
    echo "scripts/review-queue-decide.sh $mission_id approve  # upload candidate"
    echo "scripts/review-queue-decide.sh $mission_id reject   # not for publish"
    echo "scripts/review-queue-decide.sh $mission_id archive  # neutral park"
    echo '```'
    echo
    echo "---"
    echo
  done <<< "$entries"
} > "$OUT"

echo "[review-digest] wrote $OUT ($count entries)"
