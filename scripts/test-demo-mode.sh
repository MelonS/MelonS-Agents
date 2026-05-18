#!/usr/bin/env bash
# Demo-mode fresh-clone test — verifies the zero-account first-touch
# path produces a real music-video on a clean tree without ANY of
# Pexels signup, Suno round-trip, or operator .env edits.
#
# What it does:
#   1. Clones the public repo into a temp dir (or uses
#      FRESH_CLONE_REMOTE if set).
#   2. Runs scripts/bootstrap.sh inside the clone.
#   3. Runs MUSIC_VIDEO_DEMO_MODE=1 against the music-video mission
#      (no .env edit, no operator-supplied music file).
#   4. Asserts the produced short.mp4 is ≥ 1 MB AND duration ≥ 50s.
#   5. Asserts outputs/SOURCES.txt exists and contains a CC-BY
#      attribution line — required by both Blender CC-BY-3.0 and
#      Incompetech CC-BY-4.0 licenses if the output is ever
#      published.
#   6. Appends PASS/FAIL line to docs/onboarding/demo-mode-log.txt.
#
# Designed to be a *reproducibility* gate, not a unit test: when this
# passes against the public GitHub URL, any stranger running
# bootstrap + one command gets a real short.  When it fails, the
# error message + log entry are enough to diagnose the breakage.
#
# Usage:
#   scripts/test-demo-mode.sh
#
# Optional overrides:
#   FRESH_CLONE_REMOTE   git URL or local path (default: GitHub HTTPS)
#   FRESH_CLONE_KEEP     1 = leave the temp clone in place after run

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REMOTE="${FRESH_CLONE_REMOTE:-https://github.com/MelonS/MelonS-Agents.git}"
KEEP="${FRESH_CLONE_KEEP:-0}"
STAMP="$(date '+%Y%m%d-%H%M%S')"
WORK="${TMPDIR:-/tmp}/demo-mode-${STAMP}"
LOG_DIR="${REPO_ROOT}/docs/onboarding"
LOG="${LOG_DIR}/demo-mode-log.txt"

mkdir -p "$LOG_DIR"

VERDICT="FAIL"
SHORT_PATH=""
SHORT_SIZE_MB=""
SHORT_DURATION_S=""
SOURCES_LINE_COUNT=""
FAIL_REASON=""

write_log_entry() {
  local ts; ts="$(date '+%Y-%m-%d %H:%M:%S %z')"
  {
    if [[ "$VERDICT" == "PASS" ]]; then
      printf '%s  PASS  remote=%s  short=%s  size=%sMB  duration=%ss  sources_lines=%s\n' \
        "$ts" "$REMOTE" "${SHORT_PATH#$WORK/}" "$SHORT_SIZE_MB" "$SHORT_DURATION_S" "$SOURCES_LINE_COUNT"
    else
      printf '%s  FAIL  remote=%s  reason="%s"\n' \
        "$ts" "$REMOTE" "$FAIL_REASON"
    fi
  } >> "$LOG"
}

on_exit() {
  local rc="$?"
  if [[ "$VERDICT" != "PASS" && -z "$FAIL_REASON" ]]; then
    FAIL_REASON="exit code ${rc}"
  fi
  write_log_entry
  if [[ "$VERDICT" == "PASS" ]]; then
    echo
    echo "✅ demo-mode PASS — log appended to ${LOG#$REPO_ROOT/}"
  else
    echo
    echo "❌ demo-mode FAIL (${FAIL_REASON}) — log appended to ${LOG#$REPO_ROOT/}"
  fi
  if [[ "$KEEP" != "1" ]]; then
    rm -rf "$WORK"
  else
    echo "   (clone preserved at $WORK per FRESH_CLONE_KEEP=1)"
  fi
}
trap on_exit EXIT

echo "=== demo-mode fresh-clone test ==="
echo "  remote: $REMOTE"
echo "  work:   $WORK"
echo

# Step 1 — clone
echo "[1/5] git clone --depth 1 …"
mkdir -p "$WORK"
if ! git clone --depth 1 "$REMOTE" "$WORK/repo" >/dev/null 2>&1; then
  FAIL_REASON="git clone failed against $REMOTE"
  exit 1
fi
cd "$WORK/repo"

# Step 2 — bootstrap
echo "[2/5] bootstrap (auto-creates .env if missing, no manual edit)"
if ! ./scripts/bootstrap.sh >"$WORK/bootstrap.log" 2>&1; then
  FAIL_REASON="bootstrap.sh exited non-zero (see $WORK/bootstrap.log)"
  tail -20 "$WORK/bootstrap.log" >&2
  exit 1
fi

# Step 3 — demo-mode mission
# .env is the auto-created template version; no PEXELS_API_KEY set.
# That's the whole point — the demo path must work in this state.
echo "[3/5] MUSIC_VIDEO_DEMO_MODE=1 music-video run"
if ! MUSIC_VIDEO_DEMO_MODE=1 ./agents/missions/music-video/run.sh demo \
     >"$WORK/mission.log" 2>&1; then
  FAIL_REASON="music-video demo run exited non-zero (see $WORK/mission.log)"
  tail -30 "$WORK/mission.log" >&2
  exit 1
fi

# Step 4 — locate + measure the produced short
echo "[4/5] locate + probe short.mp4"
SHORT_PATH="$(ls -1t "$WORK/repo"/records/missions/*/music-video-demo-*/outputs/short.mp4 2>/dev/null | head -1)"
if [[ -z "$SHORT_PATH" || ! -f "$SHORT_PATH" ]]; then
  FAIL_REASON="no short.mp4 found under records/missions/.../music-video-demo-*/outputs/"
  exit 1
fi

# Size assertion
SIZE_BYTES=$(stat -f '%z' "$SHORT_PATH" 2>/dev/null || stat -c '%s' "$SHORT_PATH" 2>/dev/null)
SHORT_SIZE_MB=$(( SIZE_BYTES / 1024 / 1024 ))
if (( SIZE_BYTES < 1024 * 1024 )); then
  FAIL_REASON="short.mp4 too small: ${SIZE_BYTES} bytes (expected ≥ 1 MB)"
  exit 1
fi

# Duration assertion (ffprobe; respect repo's FFPROBE_BIN if set)
# shellcheck disable=SC1091
source "$WORK/repo/agents/lib/env.sh" 2>/dev/null || true
FFPROBE="${FFPROBE_BIN:-ffprobe}"
DUR=$("$FFPROBE" -v error -show_entries format=duration -of default=nw=1:nk=1 "$SHORT_PATH" 2>/dev/null | awk '{printf "%d", $1}')
SHORT_DURATION_S="$DUR"
if (( DUR < 50 )); then
  FAIL_REASON="short.mp4 too short: ${DUR}s (expected ≥ 50s for a 60s target)"
  exit 1
fi

# Step 5 — attribution sanity check
echo "[5/5] SOURCES.txt sanity check"
SOURCES_FILE="$(dirname "$SHORT_PATH")/SOURCES.txt"
if [[ ! -f "$SOURCES_FILE" ]]; then
  FAIL_REASON="SOURCES.txt missing alongside short.mp4 — CC-BY attribution incomplete"
  exit 1
fi
SOURCES_LINE_COUNT=$(grep -cE '^- ' "$SOURCES_FILE" 2>/dev/null || echo 0)
if (( SOURCES_LINE_COUNT < 2 )); then
  FAIL_REASON="SOURCES.txt has only $SOURCES_LINE_COUNT credit line(s) — expected ≥ 2 (music + B-roll)"
  cat "$SOURCES_FILE" >&2
  exit 1
fi
if ! grep -qE 'CC-BY' "$SOURCES_FILE"; then
  FAIL_REASON="SOURCES.txt has no CC-BY license tag — license metadata may be lost"
  exit 1
fi

VERDICT="PASS"
echo
echo "  short:    $SHORT_PATH"
echo "  size:     ${SHORT_SIZE_MB} MB"
echo "  duration: ${SHORT_DURATION_S}s"
echo "  sources:  $SOURCES_LINE_COUNT credit line(s) including CC-BY"
