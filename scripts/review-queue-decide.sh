#!/usr/bin/env bash
# Record an operator verdict on a queued artifact.
#
# Lever 3 from docs/research/2026-05-22-intervention-reduction.md.
# Moves outputs/review-queue/pending/<...>.json → decided/<...>.json
# with verdict + optional note merged in.
#
# Usage:
#   scripts/review-queue-decide.sh <mission_id> approve|reject|archive [note]
#
# Idempotent: if the entry is already in decided/, the latest verdict
# wins (overwrites the prior).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PENDING="$REPO_ROOT/outputs/review-queue/pending"
DECIDED="$REPO_ROOT/outputs/review-queue/decided"
mkdir -p "$PENDING" "$DECIDED"

mission_id="${1:?usage: review-queue-decide.sh <mission_id> approve|reject|archive [note]}"
verdict="${2:?missing verdict (approve|reject|archive)}"
note="${3:-}"

case "$verdict" in
  approve|reject|archive) ;;
  *) echo "[review-decide] invalid verdict: $verdict (must be approve|reject|archive)" >&2; exit 1 ;;
esac

src=$(find "$PENDING" -name "*-${mission_id}.json" | head -1)
if [[ -z "$src" ]]; then
  echo "[review-decide] no pending entry for $mission_id" >&2
  echo "  pending entries:" >&2
  find "$PENDING" -name "*.json" -exec basename {} \; >&2
  exit 1
fi

decided_at=$(date -u +%FT%TZ)
dst="$DECIDED/$(basename "$src")"

jq \
  --arg verdict "$verdict" \
  --arg decided_at "$decided_at" \
  --arg note "$note" \
  '. + {verdict: $verdict, decided_at: $decided_at, decision_note: $note}' \
  "$src" > "$dst"

rm -f "$src"
echo "[review-decide] $mission_id → $verdict ($(basename "$dst"))"
