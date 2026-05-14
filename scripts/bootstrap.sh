#!/usr/bin/env bash
# Bootstrap & health check for the multi-agent system.
# Verifies tool versions and env wiring. Idempotent.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

if [[ ! -f .env ]]; then
  echo "❌ .env missing. Run: cp .env.example .env && \$EDITOR .env"
  exit 1
fi

# Load .env (export every assignment, skip comments/blank lines)
set -a
# shellcheck disable=SC1091
source .env
set +a

echo "=== tool versions ==="
for var in FFMPEG_BIN OLLAMA_BIN; do
  bin="${!var:-}"
  if [[ -z "$bin" ]]; then
    echo "⚠  $var unset"
    continue
  fi
  if [[ ! -x "$bin" ]]; then
    echo "❌ $var=$bin (not executable)"
    continue
  fi
  printf "✅ %-12s " "$var"
  case "$var" in
    FFMPEG_BIN)  "$bin" -version | head -1 ;;
    OLLAMA_BIN)  "$bin" --version ;;
  esac
done

echo
echo "=== records dir ==="
RECORDS_DIR="${RECORDS_DIR:-./records}"
mkdir -p "$RECORDS_DIR"
echo "✅ $RECORDS_DIR (writable: $([[ -w $RECORDS_DIR ]] && echo yes || echo no))"

echo
echo "=== autonomy mode ==="
echo "AUTONOMY_MODE=${AUTONOMY_MODE:-false}  budget=\$${AUTONOMY_BUDGET_USD:-0}"

echo
echo "✅ bootstrap ok"
