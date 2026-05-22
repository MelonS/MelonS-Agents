#!/usr/bin/env bash
# intervention-log-add.sh — append an entry to docs/intervention-log.md.
#
# Companion of docs/intervention-log.md.  Captures the SUBSTANCE of an
# operator intervention (what they surfaced, why it mattered, what
# shipped, what tag) so the qualitative shaping signal isn't lost
# alongside the quantitative chart at docs/metrics/intervention.png.
#
# Per the file's privacy contract: synthesize, don't paste verbatim
# prompts.  Short paraphrased Korean is fine; extended verbatim is not.
#
# Usage:
#   scripts/intervention-log-add.sh "summary" \
#     --why "constraint or insight" \
#     --shipped "<sha> or <path> or deferred/rejected/research-only" \
#     --tag direction|taste|correction|hypothesis-rejection|preference|guard|constraint
#
# Auto-creates today's date header if absent.  Append-only — does not
# rewrite or de-dup existing entries.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
LOG="$REPO_ROOT/docs/intervention-log.md"

[[ -f "$LOG" ]] || { echo "[intervention-log] $LOG missing" >&2; exit 1; }

SUMMARY=""
WHY=""
SHIPPED=""
TAG=""
TIME_OVERRIDE=""

while (( $# )); do
  case "$1" in
    --why)      WHY="$2"; shift 2 ;;
    --shipped)  SHIPPED="$2"; shift 2 ;;
    --tag)      TAG="$2"; shift 2 ;;
    --time)     TIME_OVERRIDE="$2"; shift 2 ;;
    --help|-h)
      sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *)
      [[ -z "$SUMMARY" ]] && SUMMARY="$1"
      shift
      ;;
  esac
done

[[ -z "$SUMMARY" ]] && { echo "[intervention-log] missing summary (first positional arg)" >&2; exit 64; }
[[ -z "$WHY" ]]     && { echo "[intervention-log] missing --why" >&2; exit 64; }
[[ -z "$SHIPPED" ]] && { echo "[intervention-log] missing --shipped" >&2; exit 64; }
[[ -z "$TAG" ]]     && { echo "[intervention-log] missing --tag" >&2; exit 64; }

case "$TAG" in
  direction|taste|correction|hypothesis-rejection|preference|guard|constraint) ;;
  *) echo "[intervention-log] invalid --tag '$TAG' (allowed: direction|taste|correction|hypothesis-rejection|preference|guard|constraint)" >&2
     exit 64 ;;
esac

if [[ -n "$TIME_OVERRIDE" ]]; then
  ts="$TIME_OVERRIDE"
else
  ts=$(TZ=Asia/Seoul date +%H:%M)
fi
today=$(TZ=Asia/Seoul date +%Y-%m-%d)

# Create today's date header if absent.  Insert above the first
# existing `## YYYY-MM-DD` header so chronological order (newest
# section at top) holds.
if ! grep -q "^## ${today}" "$LOG"; then
  if grep -q "^## 2026-" "$LOG"; then
    awk -v today="$today" '
      BEGIN { inserted = 0 }
      /^## 2026-/ && !inserted {
        printf "## %s\n\n", today
        inserted = 1
      }
      { print }
    ' "$LOG" > "$LOG.tmp" && mv "$LOG.tmp" "$LOG"
  else
    printf "\n## %s\n\n" "$today" >> "$LOG"
  fi
fi

# Append entry under today's section.  Newest-first within the day.
awk -v today="$today" -v ts="$ts" -v summary="$SUMMARY" -v why="$WHY" \
    -v shipped="$SHIPPED" -v tag="$TAG" '
  /^## / && match($0, "^## " today) {
    print
    print ""
    print "- **`" ts " KST`** — " summary
    print "  - **why**: " why
    print "  - **shipped**: " shipped
    print "  - **tag**: " tag
    in_section = 1
    next
  }
  in_section && /^$/ {
    in_section = 0
    next
  }
  { print }
' "$LOG" > "$LOG.tmp" && mv "$LOG.tmp" "$LOG"

echo "[intervention-log] $today $ts → $SUMMARY  ($TAG)"
