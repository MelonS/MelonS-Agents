#!/usr/bin/env bash
# Create the project's local Python venv (.venv/) and install the
# small set of packages needed by scripts that aren't pure bash.
#
# Right now this is just matplotlib (used by scripts/generate-charts.py
# to regenerate docs/metrics/*.png from records/missions/*/metrics.json).
# Add more packages here as the toolchain grows; keep the install
# scoped to the venv so the host's system Python stays untouched.
#
# Usage:
#   scripts/setup-venv.sh             # idempotent — creates .venv/ if
#                                       missing, installs/updates pkgs
#   scripts/setup-venv.sh --force     # delete and rebuild .venv/

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

if [[ "${1:-}" == "--force" ]]; then
  rm -rf .venv
fi

if [[ ! -d .venv ]]; then
  echo "↓ creating .venv/"
  python3 -m venv .venv
fi

if [[ ! -x .venv/bin/python ]]; then
  echo "❌ .venv/bin/python missing after `python3 -m venv .venv` — aborting" >&2
  exit 1
fi

echo "↓ installing packages into .venv/"
.venv/bin/pip install --quiet --upgrade pip
.venv/bin/pip install --quiet matplotlib

echo "✅ venv ready: .venv/bin/python"
echo "   regenerate metrics charts with:  .venv/bin/python scripts/generate-charts.py"
