#!/usr/bin/env bash
# status.sh — one-screen dashboard of which job-hunt feature flags
# are currently ON, OFF, or blocked by a missing env key.
#
# Reads the declarative manifest at `config/activation.tsv` and
# checks each flag's current env state.  No external dependencies
# beyond bash + awk; no parsing of the SKILL.md frontmatter.
#
# Usage:
#   bash skills/job-hunt/scripts/status.sh
#   bash skills/job-hunt/scripts/status.sh --quiet   # one-line summary only
#   bash skills/job-hunt/scripts/status.sh --json    # machine output
#
# Exit codes:
#   0  manifest parsed cleanly (regardless of how many flags are ON/OFF)
#   2  manifest file missing or unreadable
#   3  manifest row malformed

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
MANIFEST="$SKILL_DIR/config/activation.tsv"

mode="full"
case "${1:-}" in
  --quiet) mode="quiet" ;;
  --json)  mode="json" ;;
  -h|--help)
    sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'
    exit 0
    ;;
esac

if [[ ! -r "$MANIFEST" ]]; then
  echo "[status] manifest not readable: $MANIFEST" >&2
  exit 2
fi

# Load .env if present so locally-set keys are visible
if [[ -f "$(git rev-parse --show-toplevel 2>/dev/null)/.env" ]]; then
  set -o allexport
  # shellcheck disable=SC1090
  source "$(git rev-parse --show-toplevel 2>/dev/null)/.env"
  set +o allexport
fi

declare -a rows_http=()
declare -a rows_claude=()
total=0
on=0
off=0
blocked=0

while IFS=$'\t' read -r flag path env_keys; do
  # skip blanks + comments
  [[ -z "${flag:-}" ]] && continue
  [[ "${flag:0:1}" == "#" ]] && continue
  if [[ -z "${path:-}" ]] || [[ -z "${env_keys:-}" ]]; then
    echo "[status] malformed manifest row: '$flag'" >&2
    exit 3
  fi
  total=$((total + 1))

  # Resolve current flag value (default 0)
  flag_val="${!flag:-0}"

  # Check required env keys (skip placeholder '-')
  missing=()
  if [[ "$env_keys" != "-" ]]; then
    IFS=',' read -ra keys <<<"$env_keys"
    for k in "${keys[@]}"; do
      [[ -z "${!k:-}" ]] && missing+=("$k")
    done
  fi

  if [[ "$flag_val" == "1" ]]; then
    state="ON"
    on=$((on + 1))
    if (( ${#missing[@]} > 0 )); then
      state="ON⚠"   # flag set but key missing — script will fail at runtime
      blocked=$((blocked + 1))
    fi
  else
    if (( ${#missing[@]} > 0 )); then
      state="OFF (key needed: ${missing[*]})"
      blocked=$((blocked + 1))
    else
      state="OFF"
    fi
    off=$((off + 1))
  fi

  row=$(printf '  %-30s %s' "$flag" "$state")
  case "$path" in
    sources/*)  rows_http+=("$row") ;;
    scripts/*)  rows_claude+=("$row") ;;
    *)          rows_claude+=("$row") ;;
  esac
done < "$MANIFEST"

if [[ "$mode" == "json" ]]; then
  printf '{"total":%d,"on":%d,"off":%d,"blocked":%d}\n' \
    "$total" "$on" "$off" "$blocked"
  exit 0
fi

if [[ "$mode" == "quiet" ]]; then
  echo "job-hunt activation: $on/$total ON, $blocked blocked"
  exit 0
fi

cat <<EOF

job-hunt activation status  ($MANIFEST)
─────────────────────────────────────────────────────────────

HTTP source plugins
EOF
printf '%s\n' "${rows_http[@]}"

cat <<EOF

Claude-call helpers
EOF
printf '%s\n' "${rows_claude[@]}"

cat <<EOF

─────────────────────────────────────────────────────────────
total: $total    ON: $on    OFF: $off    blocked-by-missing-key: $blocked
EOF
