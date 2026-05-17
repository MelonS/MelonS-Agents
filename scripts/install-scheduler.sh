#!/usr/bin/env bash
# Install or uninstall the launchd jobs that keep MelonS-Agents running
# unattended.
#
# Four jobs:
#   • com.melons.agents.queue       — every 30 min, drains records/queue/
#                                     pending.txt and runs the highlight
#                                     mission on each entry.
#   • com.melons.agents.auditor     — once a day at 03:00 local, runs
#                                     scripts/audit-run.sh all (L3 baseline).
#   • com.melons.agents.audit-poll  — every 15 min, runs scripts/audit-poll.sh
#                                     which fires a focused audit only on
#                                     mission-anomaly patterns (L2 reactive
#                                     trigger; cheap no-op when nothing's
#                                     wrong).
#   • com.melons.agents.disk-watch  — every 30 min, runs scripts/disk-watch.sh
#                                     which checks free disk and emits a
#                                     macOS notification + alert file at
#                                     WARN (<15 GB) or CRITICAL (<5 GB).
#
# Usage:
#   scripts/install-scheduler.sh install [queue|auditor|audit-poll|disk-watch|all]
#   scripts/install-scheduler.sh uninstall [queue|auditor|audit-poll|disk-watch|all]
#   scripts/install-scheduler.sh status
#
# Default target is `all`.
#
# (Plain `case` dispatch — no bash 4 associative arrays, since macOS
#  still ships bash 3.2 by default.)
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LA_DIR="$HOME/Library/LaunchAgents"

plist_for() {
  case "$1" in
    queue)      echo "com.melons.agents.queue.plist" ;;
    auditor)    echo "com.melons.agents.auditor.plist" ;;
    audit-poll) echo "com.melons.agents.audit-poll.plist" ;;
    disk-watch) echo "com.melons.agents.disk-watch.plist" ;;
    *)          echo "" ;;
  esac
}

install_one() {
  local job="$1"
  local plist; plist=$(plist_for "$job")
  if [[ -z "$plist" ]]; then
    echo "❌ unknown job: $job" >&2
    return 1
  fi
  local src="$SCRIPT_DIR/$plist"
  local dst="$LA_DIR/$plist"
  if [[ ! -f "$src" ]]; then
    echo "❌ plist source missing: $src" >&2
    return 1
  fi
  mkdir -p "$LA_DIR"
  cp "$src" "$dst"
  launchctl unload "$dst" 2>/dev/null || true
  launchctl load "$dst"
  echo "installed: $dst"
}

uninstall_one() {
  local job="$1"
  local plist; plist=$(plist_for "$job")
  [[ -z "$plist" ]] && { echo "❌ unknown job: $job" >&2; return 1; }
  local dst="$LA_DIR/$plist"
  launchctl unload "$dst" 2>/dev/null || true
  rm -f "$dst"
  echo "uninstalled: $dst"
}

status_one() {
  local job="$1"
  local plist; plist=$(plist_for "$job")
  [[ -z "$plist" ]] && return 0
  local dst="$LA_DIR/$plist"
  if [[ -f "$dst" ]]; then
    echo "[$job] plist present at $dst"
    launchctl list | grep "com.melons.agents.$job" || echo "[$job] not loaded"
  else
    echo "[$job] plist NOT installed"
  fi
}

expand_targets() {
  if [[ "$1" == "all" ]]; then
    echo "queue auditor audit-poll disk-watch"
  else
    echo "$1"
  fi
}

op="${1:-status}"
target="${2:-all}"

case "$op" in
  install)
    for j in $(expand_targets "$target"); do
      install_one "$j"
    done
    ;;
  uninstall)
    for j in $(expand_targets "$target"); do
      uninstall_one "$j"
    done
    ;;
  status)
    for j in queue auditor audit-poll disk-watch; do
      status_one "$j"
    done
    ;;
  *)
    echo "usage: $0 {install|uninstall|status} [queue|auditor|audit-poll|disk-watch|all]"
    exit 64 ;;
esac
