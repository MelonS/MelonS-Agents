#!/usr/bin/env bash
# scripts/disk-watch.sh — periodic free-disk monitor.
#
# Fires every 30 min via launchd (com.melons.agents.disk-watch.plist).
# Three signals when free disk drops below thresholds:
#   1. STDERR log line (visible in records/disk-watch/*.log)
#   2. macOS Notification Center via osascript (operator sees the toast
#      even when the terminal is closed)
#   3. Marker file at records/disk-alerts/<level>-<ts>.txt (auditor + manual scan)
#
# Thresholds (env-overridable):
#   DISK_WARN_GB=15   — visual heads-up; cleanup recommended
#   DISK_CRIT_GB=5    — render-blocker territory; cleanup mandatory
#
# Exit codes:
#   0 — OK (free >= WARN)
#   1 — WARN (free between CRIT and WARN)
#   2 — CRITICAL (free < CRIT)
#
# Companion to:
#   - scripts/cleanup-records.sh    (suggested fix)
#   - agents/missions/faceless-short/run.sh pre-render guard (immediate abort)
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

WARN_GB="${DISK_WARN_GB:-15}"
CRIT_GB="${DISK_CRIT_GB:-5}"

# Free space on the root volume (records/, ollama models, system all here).
# df -k prints 1K-blocks; convert to GB.  awk '$NF=="/"' tolerates header.
FREE_KB=$(df -k / | awk '$NF=="/" {print $4; exit}')
FREE_GB=$(( FREE_KB / 1024 / 1024 ))

ALERT_DIR="records/disk-alerts"
LOG_DIR="records/disk-watch"
mkdir -p "$ALERT_DIR" "$LOG_DIR"
TS=$(date '+%Y%m%dT%H%M%S')
LOG_LINE="[$(date '+%Y-%m-%d %H:%M:%S')] free=${FREE_GB}GB  warn=${WARN_GB}  crit=${CRIT_GB}"

notify() {
  # macOS Notification Center; survives even when terminal is closed.
  local title="$1" msg="$2"
  osascript -e "display notification \"$msg\" with title \"$title\"" 2>/dev/null || true
}

write_alert() {
  local level="$1" body="$2"
  local f="$ALERT_DIR/${level}-${TS}.txt"
  cat > "$f" <<EOF
$level: disk free is ${FREE_GB} GB at $(date '+%Y-%m-%d %H:%M:%S')
threshold: WARN<${WARN_GB} GB, CRIT<${CRIT_GB} GB

$body

Suggested action:
  scripts/cleanup-records.sh                    # records/ intermediates (~3 GB typical)
  Empty Trash (Finder)                          # if previously-deleted items still there
  rm /Applications/<unused-app>                 # large app purge

Then re-run:
  scripts/disk-watch.sh                         # confirm OK
EOF
  echo "$f"
}

if (( FREE_GB < CRIT_GB )); then
  echo "$LOG_LINE  CRITICAL" >> "$LOG_DIR/disk-watch.log"
  ALERT_PATH=$(write_alert "CRITICAL" "macOS swap/sleep image may stall.  Production renders WILL fail.")
  notify "MelonS-Agents disk CRITICAL" "Only ${FREE_GB}GB free.  See ${ALERT_PATH}."
  echo "[disk-watch] CRITICAL ${FREE_GB}GB free — $ALERT_PATH" >&2
  exit 2
elif (( FREE_GB < WARN_GB )); then
  echo "$LOG_LINE  WARN" >> "$LOG_DIR/disk-watch.log"
  ALERT_PATH=$(write_alert "WARN" "Heads up: not yet blocking but trending toward crisis.")
  notify "MelonS-Agents disk WARN" "${FREE_GB}GB free.  Cleanup recommended."
  echo "[disk-watch] WARN ${FREE_GB}GB free — $ALERT_PATH" >&2
  exit 1
fi

echo "$LOG_LINE  OK" >> "$LOG_DIR/disk-watch.log"
echo "[disk-watch] OK ${FREE_GB}GB free"
exit 0
