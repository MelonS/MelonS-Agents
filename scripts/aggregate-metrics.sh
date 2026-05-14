#!/usr/bin/env bash
# Aggregate per-mission metrics across all mission types.
# Real logic in scripts/agg-metrics.py (avoids heredoc-in-heredoc nightmares).
set -u
cd "$(dirname "${BASH_SOURCE[0]}")/.."
mkdir -p records/metrics docs
python3 scripts/agg-metrics.py
