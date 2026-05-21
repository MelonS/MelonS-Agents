#!/usr/bin/env bash
# Daily regen of docs/metrics/intervention.{png,json}.
#
# Invoked by com.melons.agents.intervention-chart launchd job
# (rendered from the .plist.template by scripts/install-scheduler.sh)
# at 02:00 local each night, after the day's commits have landed but
# before the operator opens the laptop in the morning.
#
# The generator script reads two data sources:
#  - git log (always available)
#  - ~/.claude/projects/-Users-melons-ai/*.jsonl (operator's local
#    Claude Code session logs; missing on a fresh clone — that's
#    handled by the generator, which falls back to commit-only).
#
# matplotlib is the only Python dependency; if it's missing we
# install into a local venv under scripts/.venv so the rest of the
# repo stays system-Python-friendly.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

PY="$(command -v python3 || true)"
if [[ -z "$PY" ]]; then
  echo "[intervention-chart] python3 not on PATH" >&2
  exit 1
fi

# Prefer venv if it exists; otherwise probe system python for matplotlib.
if [[ -x "$SCRIPT_DIR/.venv/bin/python" ]]; then
  PY="$SCRIPT_DIR/.venv/bin/python"
fi

if ! "$PY" -c "import matplotlib" 2>/dev/null; then
  echo "[intervention-chart] matplotlib missing — bootstrapping venv"
  if [[ ! -d "$SCRIPT_DIR/.venv" ]]; then
    "$(command -v python3)" -m venv "$SCRIPT_DIR/.venv"
  fi
  PY="$SCRIPT_DIR/.venv/bin/python"
  "$PY" -m pip install -q --upgrade pip
  "$PY" -m pip install -q matplotlib
fi

echo "[intervention-chart] regenerating with $PY"
"$PY" "$SCRIPT_DIR/generate-intervention-chart.py"
echo "[intervention-chart] done at $(date -u +%FT%TZ)"
