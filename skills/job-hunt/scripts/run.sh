#!/usr/bin/env bash
# job-hunt skill — orchestrator stub.
#
# Status (2026-05-20): scaffold only — no live source implemented.
# When sources are added under skills/job-hunt/sources/<locale>-<board>.sh
# this script will:
#   1. Load skills/job-hunt/config/filters.yaml (or --filters=<path>).
#   2. For each enabled source: invoke the plugin's fetch_postings(),
#      capture normalized JSON to records/jobs/<date>/raw/<source>.json.
#   3. Apply keyword include/exclude filters.
#   4. Deduplicate on URL across sources.
#   5. Diff against the prior digest (if any) to compute "new since".
#   6. Render the markdown digest to records/jobs/<date>/digest.md.
#
# Design notes — why this stub exists separately from
# agents/missions/: the job-hunt skill is self-contained.  Unlike
# music-video (which symlinks scripts/run.sh into
# agents/missions/music-video/run.sh to inherit pipeline tuning),
# job-hunt has no pre-existing mission to inherit.  The skill is
# the canonical implementation.
#
# Exit codes (to be implemented):
#   0   — digest written successfully (with or without new postings).
#   2   — config error (missing filters.yaml, unsupported locale, etc.).
#   3   — all sources failed (network / parse / auth).
#   4   — partial success: at least one source returned, at least
#         one failed; digest written with a "sources failed" note.
#   10  — scaffold-only marker: live implementation not yet present.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "[job-hunt] scaffold-only — live source implementation not yet present." >&2
echo "[job-hunt] skill dir: $SKILL_DIR" >&2
echo "[job-hunt] see $SKILL_DIR/SKILL.md 'Status' section for plan." >&2
exit 10
