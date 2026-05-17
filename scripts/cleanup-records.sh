#!/usr/bin/env bash
# scripts/cleanup-records.sh — selective records/ intermediate cleanup
#
# What this deletes:
#   records/missions/<date>/<mission-id>/resources/
#     (B-roll downloads, trimmed clips, narration.wav, concat-noaudio.mp4 —
#      all regeneratable by re-running the mission)
#
# What this keeps:
#   - outputs/short.mp4              ← the deliverable
#   - metrics.json, plan.md, qa-report.md, *.log
#   - resources/ of any mission whose ID matches the PRESERVE_RE
#     (current niche-decision evidence — v6 series + music-trial)
#
# Pre-v6 missions only lose their reproducible intermediates; the mp4 + the
# committed caption-verify thumbnails + the scorecard.json entry already
# encode everything we need.
#
# Usage:
#   scripts/cleanup-records.sh              # live run
#   DRY_RUN=1 scripts/cleanup-records.sh    # report what would be deleted
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

PRESERVE_RE='(-v6-|autotune|earworms|aimusic|miku)'
DRY_RUN="${DRY_RUN:-0}"

KEEP_COUNT=0
DEL_COUNT=0
TOTAL_KB=0

# Mission resources dirs live at depth 3 from records/missions/.
while IFS= read -r d; do
  mission=$(basename "$(dirname "$d")")
  if [[ "$mission" =~ $PRESERVE_RE ]]; then
    KEEP_COUNT=$((KEEP_COUNT + 1))
    echo "  KEEP $mission"
    continue
  fi
  size_kb=$(du -sk "$d" 2>/dev/null | awk '{print $1}')
  size_kb="${size_kb:-0}"
  TOTAL_KB=$((TOTAL_KB + size_kb))
  DEL_COUNT=$((DEL_COUNT + 1))
  if [[ "$DRY_RUN" == "1" ]]; then
    printf "  DRY  %s (%.1f MB)\n" "$mission" "$(awk -v k="$size_kb" 'BEGIN{print k/1024}')"
  else
    rm -rf "$d"
    printf "  RM   %s (%.1f MB)\n" "$mission" "$(awk -v k="$size_kb" 'BEGIN{print k/1024}')"
  fi
done < <(find records/missions -mindepth 3 -maxdepth 3 -type d -name resources 2>/dev/null)

echo
printf "kept:    %d missions (preserved by regex /%s/)\n" "$KEEP_COUNT" "$PRESERVE_RE"
printf "deleted: %d resources dirs\n" "$DEL_COUNT"
printf "freed:   %.1f MB\n" "$(awk -v k="$TOTAL_KB" 'BEGIN{print k/1024}')"
