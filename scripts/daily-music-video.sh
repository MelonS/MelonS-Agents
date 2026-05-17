#!/usr/bin/env bash
# Daily queue runner for the music-video mission.
#
# Reads pending entries from records/queue/music-video-pending.txt,
# picks the first non-comment line, runs the music-video mission +
# the combo shader, places the final mp4 in outputs/publish/<slug>.mp4,
# logs the outcome, and removes the processed line from pending.
#
# Designed for cron / launchd ("run once daily, process one entry") or
# manual "drain all" invocations.  Intentionally a separate script
# (not an edit to agents/missions/music-video/run.sh) so this can land
# without an agent-definition logic change.
#
# Queue file format (one entry per line, `|`-delimited):
#   <slug>|<music_file_relative_path>|<keywords_csv>
#   # comments + blank lines OK
#
# Example pending.txt:
#   piano-latenight|assets/music/Late Night Piano.mp3|late night cafe,piano keys,dim lamp
#   bossa-rainy|assets/music/Rainy Bossa.mp3|rainy street,saxophone,wet pavement
#
# Usage:
#   scripts/daily-music-video.sh              # process 1 entry
#   scripts/daily-music-video.sh --all        # drain entire queue
#   scripts/daily-music-video.sh --dry-run    # show what would run
#
# Side-effects:
#   - reads/writes records/queue/music-video-pending.txt
#   - appends records/queue/music-video-done.log
#   - appends records/queue/music-video-run.log
#   - writes records/queue/music-video-inprogress.txt during run
#   - produces outputs/publish/<slug>.mp4 + <slug>-thumb.jpg
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
source agents/lib/env.sh 2>/dev/null || true

QUEUE_DIR="records/queue"
PENDING="$QUEUE_DIR/music-video-pending.txt"
INPROG="$QUEUE_DIR/music-video-inprogress.txt"
DONE_LOG="$QUEUE_DIR/music-video-done.log"
RUN_LOG="$QUEUE_DIR/music-video-run.log"
SHADER="$PWD/scripts/music-video-shaders.sh"
PUBLISH_DIR="$PWD/outputs/publish"
MAX_PROCESS="${1:-one}"   # "one" (default), "--all", "--dry-run"

mkdir -p "$QUEUE_DIR" "$PUBLISH_DIR"
touch "$PENDING" "$DONE_LOG" "$RUN_LOG"

ts() { date +%Y-%m-%dT%H:%M:%S%z; }
say() { printf '[%s] %s\n' "$(ts)" "$*" >>"$RUN_LOG"; printf '%s\n' "$*"; }

process_one() {
  local raw="$1"
  local slug music keywords
  IFS='|' read -r slug music keywords <<< "$raw"
  slug="$(echo "$slug" | sed -E 's/^[[:space:]]+//;s/[[:space:]]+$//')"
  music="$(echo "$music" | sed -E 's/^[[:space:]]+//;s/[[:space:]]+$//')"
  keywords="$(echo "$keywords" | sed -E 's/^[[:space:]]+//;s/[[:space:]]+$//')"

  if [[ -z "$slug" || -z "$music" ]]; then
    say "❌ malformed: $raw"
    return 1
  fi
  if [[ ! -f "$music" ]]; then
    say "❌ missing music file: $music"
    return 1
  fi

  # Mission ID prefix gets a timestamp + slug to make it collision-free
  # across days and re-runs ("dq-2026-05-18-piano-latenight" instead of
  # generic "dq-piano-latenight").  Without this, two runs of the same
  # slug on different days would both match "dq-${slug}-*" and the
  # ls -td ordering decides which mp4 gets picked up — which is exactly
  # how the 2026-05-17 → 2026-05-18 overnight batch corrupted 06/07.
  local mission_id="dq-$(date +%Y%m%d)-$slug"

  # Marker file lets us find ONLY the mission dir created by THIS
  # run (find -newer), which is robust to:
  #   (a) midnight crossings (the mission's date directory may differ
  #       from `date +%Y-%m-%d` evaluated at search time);
  #   (b) collision with older missions that share a TID prefix.
  local marker
  marker=$(mktemp -t dq-marker-XXXXXX)
  say "▶ START: $slug  (music=$music, mission_id=$mission_id)"

  if ! ./agents/missions/music-video/run.sh "$mission_id" "$music" "$keywords" >>"$RUN_LOG" 2>&1; then
    say "✗ mission FAILED: $slug"
    echo "$(ts) FAIL $slug mission-error" >>"$DONE_LOG"
    rm -f "$marker"
    return 1
  fi

  # Find newest mission dir created since the marker — regardless of date dir.
  local latest
  latest=$(find records/missions -type d -name "music-video-${mission_id}-*" -newer "$marker" 2>/dev/null | sort | tail -1)
  rm -f "$marker"
  if [[ -z "$latest" || ! -f "$latest/outputs/short.mp4" ]]; then
    say "✗ short.mp4 missing for $slug (searched all date dirs)"
    echo "$(ts) FAIL $slug short-not-found" >>"$DONE_LOG"
    return 1
  fi

  local out="$PUBLISH_DIR/${slug}.mp4"
  if ! "$SHADER" combo "$latest/outputs/short.mp4" "$out" >>"$RUN_LOG" 2>&1; then
    say "⚠ shader FAILED for $slug — using base mp4 (no shader)"
    cp "$latest/outputs/short.mp4" "$out"
  fi
  "${FFMPEG_BIN:-ffmpeg}" -y -loglevel error -ss 30 -i "$out" -frames:v 1 "${out%.mp4}-thumb.jpg" 2>/dev/null || true

  local sz
  sz=$(du -h "$out" | cut -f1)
  say "✓ PASS: $slug → $out ($sz)"
  echo "$(ts) PASS $slug $out" >>"$DONE_LOG"
  return 0
}

drain_one_line() {
  # Pop first non-blank / non-comment line from PENDING
  local line=""
  while IFS= read -r raw; do
    local trimmed="${raw%%#*}"
    trimmed="$(echo "$trimmed" | sed -E 's/^[[:space:]]+//;s/[[:space:]]+$//')"
    if [[ -n "$trimmed" ]]; then
      line="$trimmed"
      break
    fi
  done < "$PENDING"

  if [[ -z "$line" ]]; then
    return 2  # queue empty
  fi

  # Remove that line from pending
  awk -v target="$line" 'BEGIN{removed=0}
    { if (!removed && $0 == target) { removed=1; next } print }' "$PENDING" > "$PENDING.tmp"
  mv "$PENDING.tmp" "$PENDING"
  echo "$line" > "$INPROG"

  process_one "$line"
  local rc=$?
  : > "$INPROG"
  return $rc
}

case "$MAX_PROCESS" in
  --dry-run)
    say "dry-run — pending queue:"
    grep -vE '^\s*(#|$)' "$PENDING" | nl -ba
    ;;
  --all)
    say "draining entire queue..."
    while true; do
      drain_one_line
      rc=$?
      if [[ $rc -eq 2 ]]; then
        say "queue empty"
        break
      fi
    done
    ;;
  one|""|*)
    drain_one_line
    rc=$?
    [[ $rc -eq 2 ]] && say "queue empty (no entries to process)"
    ;;
esac
