#!/usr/bin/env bash
# Pre-flight check before any external publish.  Reads a mission's
# outputs/SOURCES.txt + config/copyright-allowlist.yaml and refuses if:
#   - SOURCES.txt is missing
#   - license is empty / unknown / "requires-per-item-probe"
#   - license is listed publish_blocked in the allowlist
#
# Usage: ./scripts/publish-gate.sh <mission-dir>
#
# Exit status:
#   0  — safe to publish (license verified, attribution recorded)
#   3  — SOURCES.txt missing
#   4  — license empty / unknown
#   5  — license publish_blocked
#   6  — bad usage
#
# This script is the stub that any future publish.sh would call as its
# first action.  No publish.sh exists yet; ship the gate so the moment
# someone wires publish, it has the guard.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/log.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/copyright.sh"

MDIR="${1:-}"
if [[ -z "$MDIR" || ! -d "$MDIR" ]]; then
  echo "usage: $0 <mission-dir>" >&2
  exit 6
fi

SOURCES="$MDIR/outputs/SOURCES.txt"
log_info "publish gate: checking $SOURCES"

if guard_publish "$SOURCES"; then
  log_ok "publish gate PASSED — license verified, attribution recorded"
  awk -F': ' '/^license:/ {print "license: " $2; exit}' "$SOURCES"
  exit 0
fi

# guard_publish already printed the reason to stderr; surface non-zero.
log_err "publish gate REFUSED — see SOURCES.txt + config/copyright-allowlist.yaml"
exit "${?:-5}"
