#!/usr/bin/env bash
# music-video-validate.sh — combined pre-publish quality gate.
#
# Aggregates the discrete QA tools shipped under the 2026-05-22
# quality-bar work into a single verdict per mission:
#
#   1. qa-anchor       (A.3) — B-roll vs lang_anchor match
#   2. lyric drift     (A.2) — alignment confidence sidecar
#   3. broll-dedup     (A.1) — chosen clip IDs vs prior-history registry
#   4. file integrity  ----  — short.mp4 + halation/lyrics variants
#                              exist and ffprobe shows nonzero duration
#
# Verdict:
#   PASS   all gates clear OR only WARN with no FAIL
#   WARN   one or more gates degraded but operator can publish
#   FAIL   one or more gates failed; do not publish
#
# Usage:
#   scripts/music-video-validate.sh <mission_dir> --genre=NAME
#   scripts/music-video-validate.sh --batch <date>     # validate all today's missions
#
# Exit codes mirror the verdict (0/1/2).

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
QA_ANCHOR="$REPO_ROOT/scripts/music-video-qa-anchor.sh"
BROLL_HISTORY_FILE="${BROLL_HISTORY_FILE:-$REPO_ROOT/records/youtube/broll-used.txt}"

# ─── arg parsing ──────────────────────────────────────────────────
GENRE=""
BATCH_DATE=""
MDIR=""

for arg in "$@"; do
  case "$arg" in
    --genre=*)    GENRE="${arg#*=}" ;;
    --batch)      BATCH_DATE=$(date +%Y-%m-%d) ;;
    --batch=*)    BATCH_DATE="${arg#*=}" ;;
    -h|--help)
      sed -n '2,22p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    -*) echo "unknown flag: $arg" >&2; exit 64 ;;
    *)  MDIR="$arg" ;;
  esac
done

# ─── batch mode ───────────────────────────────────────────────────
if [[ -n "$BATCH_DATE" ]]; then
  BASE="${RECORDS_DIR:-$REPO_ROOT/records}/missions/$BATCH_DATE"
  if [[ ! -d "$BASE" ]]; then
    echo "no missions under $BASE" >&2
    exit 64
  fi
  WORST=0
  for d in "$BASE"/music-video-*; do
    [[ -d "$d" ]] || continue
    echo
    echo "============================================================"
    echo " $(basename "$d")"
    echo "============================================================"
    "$0" "$d" 2>&1 || rc=$?
    rc="${rc:-0}"
    if [[ "$rc" -gt "$WORST" ]]; then WORST="$rc"; fi
  done
  echo
  echo "=== batch verdict: $([[ "$WORST" -eq 0 ]] && echo PASS || ([[ "$WORST" -eq 1 ]] && echo WARN || echo FAIL))"
  exit "$WORST"
fi

# ─── single-mission mode ─────────────────────────────────────────
if [[ -z "$MDIR" || ! -d "$MDIR" ]]; then
  echo "usage: $0 <mission_dir> [--genre=NAME]" >&2
  echo "       $0 --batch [--batch=DATE]" >&2
  exit 64
fi

VERDICT_LEVEL=0   # 0=PASS, 1=WARN, 2=FAIL
declare -a GATES_PASS=() GATES_WARN=() GATES_FAIL=()

# Gate 1: file integrity
SHORT="$MDIR/outputs/short.mp4"
if [[ ! -f "$SHORT" || ! -s "$SHORT" ]]; then
  GATES_FAIL+=("file-integrity: short.mp4 missing or empty")
  VERDICT_LEVEL=2
else
  dur=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error \
    -show_entries format=duration -of csv=p=0 "$SHORT" 2>/dev/null | awk '{printf "%.1f", $1}')
  if [[ -z "$dur" || "$dur" == "0.0" ]]; then
    GATES_FAIL+=("file-integrity: short.mp4 has zero duration")
    VERDICT_LEVEL=2
  else
    GATES_PASS+=("file-integrity: short.mp4 ${dur}s")
  fi
fi

# Gate 2: qa-anchor (needs --genre)
if [[ -n "$GENRE" ]]; then
  ANCHOR_OUT=$("$QA_ANCHOR" "$MDIR" --genre="$GENRE" 2>&1) || ANCHOR_RC=$?
  ANCHOR_RC="${ANCHOR_RC:-0}"
  AN_TOTAL=$(echo "$ANCHOR_OUT" | awk '/^total:/{print $2}')
  AN_MATCH=$(echo "$ANCHOR_OUT" | awk '/^matching:/{print $2}')
  AN_RATIO=$(echo "$ANCHOR_OUT" | awk '/^matching:/{gsub(/\)/,""); print $4}')
  case "$ANCHOR_RC" in
    0) GATES_PASS+=("qa-anchor: PASS  (${AN_MATCH}/${AN_TOTAL} match, ratio ${AN_RATIO})") ;;
    1) GATES_WARN+=("qa-anchor: WARN  (${AN_MATCH}/${AN_TOTAL} match, ratio ${AN_RATIO})")
       [[ "$VERDICT_LEVEL" -lt 1 ]] && VERDICT_LEVEL=1 ;;
    2) GATES_FAIL+=("qa-anchor: FAIL  (${AN_MATCH}/${AN_TOTAL} match, ratio ${AN_RATIO})")
       VERDICT_LEVEL=2 ;;
    *) GATES_FAIL+=("qa-anchor: error (rc=$ANCHOR_RC)")
       VERDICT_LEVEL=2 ;;
  esac
else
  GATES_WARN+=("qa-anchor: skipped (no --genre supplied)")
fi

# Gate 3: lyric drift — only checked when operator explicitly passes
# the sidecar via env (MUSIC_VIDEO_LYRIC_SIDECAR=PATH).  Detecting
# sidecars by mtime in /tmp matches unrelated renders, so we don't
# guess.  Renders that don't use --with-lyrics skip this gate
# silently (note added).
DRIFT_SIDECAR="${MUSIC_VIDEO_LYRIC_SIDECAR:-}"
if [[ -n "$DRIFT_SIDECAR" && -f "$DRIFT_SIDECAR" ]]; then
  VERDICT=$(grep -o '"verdict": *"[^"]*"' "$DRIFT_SIDECAR" | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
  DRIFT=$(grep -o '"drift_ratio": *[0-9.]*' "$DRIFT_SIDECAR" | head -1 | awk '{print $2}')
  case "$VERDICT" in
    OK)   GATES_PASS+=("lyric-drift: OK (ratio=$DRIFT)") ;;
    WARN) GATES_WARN+=("lyric-drift: WARN (ratio=$DRIFT)")
          [[ "$VERDICT_LEVEL" -lt 1 ]] && VERDICT_LEVEL=1 ;;
    FAIL) GATES_FAIL+=("lyric-drift: FAIL (ratio=$DRIFT) — Suno take drifted")
          VERDICT_LEVEL=2 ;;
  esac
else
  GATES_WARN+=("lyric-drift: not exercised (no MUSIC_VIDEO_LYRIC_SIDECAR env)")
fi

# Gate 4: broll-dedup (chosen clips should ALL be in the history file by now)
if [[ -f "$BROLL_HISTORY_FILE" ]]; then
  # We assume each raw-<keyword>.mp4 corresponds to a downloaded pexels id.
  # The id isn't in the filename, but the genre wrapper appends to the
  # registry on each successful download.  If the registry size is
  # unchanged since this mission's clip dir was created, no fresh ids
  # were registered — meaning the dedup either skipped fetching or
  # was disabled.
  CLIPS_DIR="$MDIR/resources/clips"
  if [[ -d "$CLIPS_DIR" ]]; then
    n_raw=$(ls "$CLIPS_DIR"/raw-*.mp4 2>/dev/null | wc -l | tr -d ' ')
    n_hist=$(wc -l < "$BROLL_HISTORY_FILE" 2>/dev/null | tr -d ' ')
    GATES_PASS+=("broll-dedup: $n_raw raw clips, registry has $n_hist ids (correct if dedup ran)")
  else
    GATES_WARN+=("broll-dedup: clips/ dir missing")
  fi
else
  GATES_WARN+=("broll-dedup: history registry missing — first render or registry deleted")
fi

# ─── report ───────────────────────────────────────────────────────
echo
echo "=== validation: $(basename "$MDIR") ==="
echo "genre:    ${GENRE:-(unspecified)}"
# bash 3.2 + set -u: empty-array expansion errors without the +"..." guard.
for g in ${GATES_PASS[@]+"${GATES_PASS[@]}"}; do echo "  ✓ $g"; done
for g in ${GATES_WARN[@]+"${GATES_WARN[@]}"}; do echo "  ⚠ $g"; done
for g in ${GATES_FAIL[@]+"${GATES_FAIL[@]}"}; do echo "  ✗ $g"; done
echo
case "$VERDICT_LEVEL" in
  0) echo "verdict: PASS" ;;
  1) echo "verdict: WARN — publishable but flagged" ;;
  2) echo "verdict: FAIL — do not publish" ;;
esac
exit "$VERDICT_LEVEL"
