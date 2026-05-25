#!/usr/bin/env bash
# Bulk recovery for batch-rejected mission outputs.
# Scans records/missions/<date>/music-video-batch*-<label>-* for failed renders
# that have a final mp4 produced but failed validation (duration mismatch).
# Applies video-audio-pad.sh and copies to publish folder.
set -uo pipefail

PUBLISH="${1:-outputs/publish/shorts-2026-05-23-batch}"
PREFIX="${BATCH_PREFIX:-batch3}"  # match e.g. batch3-monday-v1

cd /Users/melons/ai
[[ ! -d "$PUBLISH" ]] && { echo "missing publish dir: $PUBLISH"; exit 1; }

RECOVERED=0
SKIPPED=0
for M in records/missions/2026-05-24/music-video-${PREFIX}-* records/missions/2026-05-23/music-video-${PREFIX}-*; do
  [[ ! -d "$M" ]] && continue
  LABEL=$(basename "$M" | sed "s/music-video-${PREFIX}-//; s/-[0-9]*$//")

  # Index inference: monday-v1 → 01, monday-v2 → 02, etc.
  case "$LABEL" in
    monday-v1) IDX=01 ;;
    monday-v2) IDX=02 ;;
    convenience-v1) IDX=03 ;;
    convenience-v2) IDX=04 ;;
    smallhand-folk-v1) IDX=05 ;;
    smallhand-folk-v2) IDX=06 ;;
    smallhand-younha-v1) IDX=07 ;;
    smallhand-younha-v2) IDX=08 ;;
    smallhand-en-v1) IDX=09 ;;
    smallhand-en-v2) IDX=10 ;;
    *) echo "  unknown label: $LABEL"; continue ;;
  esac

  DST="$PUBLISH/${IDX}-${LABEL}.mp4"
  # Skip if already in publish folder (was either passed validation or already recovered)
  if [[ -f "$DST" ]]; then
    SKIPPED=$((SKIPPED+1))
    continue
  fi

  # Find final mp4 (prefer lyrics > shader > grade > raw)
  FINAL=$(ls -t "$M"/outputs/short-*-lyrics.mp4 2>/dev/null | head -1)
  [[ -z "$FINAL" ]] && FINAL=$(ls -t "$M"/outputs/short-grade-*.mp4 2>/dev/null | head -1)
  [[ -z "$FINAL" ]] && FINAL=$(ls -t "$M"/outputs/short-grade.mp4 2>/dev/null | head -1)
  [[ -z "$FINAL" ]] && FINAL=$(ls -t "$M"/outputs/short.mp4 2>/dev/null | head -1)

  if [[ -z "$FINAL" || ! -f "$FINAL" ]]; then
    echo "  $IDX $LABEL: no final mp4, skip"
    continue
  fi

  echo "[RECOVERY] $IDX $LABEL ← $FINAL"
  bash scripts/video-audio-pad.sh "$FINAL" "$DST"

  # Copy thumb + meta
  THUMB=$(ls -t "$M"/outputs/*-thumb.jpg 2>/dev/null | head -1)
  [[ -n "$THUMB" ]] && cp "$THUMB" "$PUBLISH/${IDX}-${LABEL}-thumb.jpg"
  [[ -f "$M/outputs/upload-metadata.md" ]] && cp "$M/outputs/upload-metadata.md" "$PUBLISH/${IDX}-${LABEL}-meta.md"
  RECOVERED=$((RECOVERED+1))
done

echo ""
echo "[SUMMARY] recovered=$RECOVERED skipped=$SKIPPED"
echo "[PUBLISH] $PUBLISH"
ls "$PUBLISH"/*.mp4 2>/dev/null | wc -l | awk '{print "  total mp4s in publish: " $1}'
