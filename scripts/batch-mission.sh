#!/usr/bin/env bash
# Run the highlight mission across multiple sources.
# Usage:
#   scripts/batch-mission.sh <source1> [<source2> ...]
#   scripts/batch-mission.sh -f <list_file>
set -u
cd "$(dirname "${BASH_SOURCE[0]}")/.."

SOURCES=()
if [[ "${1:-}" == "-f" ]]; then
  shift
  LIST="$1"; shift
  while IFS= read -r line; do
    line="${line%%#*}"
    line="$(echo "$line" | sed -E 's/^[[:space:]]+//;s/[[:space:]]+$//')"
    [[ -n "$line" ]] && SOURCES+=("$line")
  done < "$LIST"
fi
SOURCES+=("$@")

if (( ${#SOURCES[@]} == 0 )); then
  echo "usage: $0 [-f LIST] <source ...>" >&2
  exit 64
fi

RESULTS=()
for src in "${SOURCES[@]}"; do
  echo "─── $src ───"
  if ./agents/missions/highlight/run.sh "$src"; then
    LAST=$(ls -1dt records/missions/$(date +%Y-%m-%d)/highlight-* 2>/dev/null | head -1)
    RESULTS+=("PASS|$src|$LAST")
  else
    RC=$?
    RESULTS+=("FAIL[$RC]|$src|")
  fi
done

echo
echo "═══ batch summary ═══"
printf '%s\n' "${RESULTS[@]}"
