#!/usr/bin/env bash
# Install or uninstall the launchd job that drains the mission queue.
# Usage:
#   scripts/install-scheduler.sh install
#   scripts/install-scheduler.sh uninstall
#   scripts/install-scheduler.sh status
set -u

PLIST_SRC="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/com.melons.agents.queue.plist"
PLIST_DST="$HOME/Library/LaunchAgents/com.melons.agents.queue.plist"

op="${1:-status}"

case "$op" in
  install)
    mkdir -p "$(dirname "$PLIST_DST")"
    cp "$PLIST_SRC" "$PLIST_DST"
    launchctl unload "$PLIST_DST" 2>/dev/null || true
    launchctl load "$PLIST_DST"
    echo "installed: $PLIST_DST"
    ;;
  uninstall)
    launchctl unload "$PLIST_DST" 2>/dev/null || true
    rm -f "$PLIST_DST"
    echo "uninstalled: $PLIST_DST"
    ;;
  status)
    if [[ -f "$PLIST_DST" ]]; then
      echo "plist present at $PLIST_DST"
      launchctl list | grep com.melons.agents || echo "not loaded"
    else
      echo "plist NOT installed"
    fi
    ;;
  *)
    echo "usage: $0 {install|uninstall|status}"
    exit 64 ;;
esac
