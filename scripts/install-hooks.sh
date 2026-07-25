#!/usr/bin/env bash
# Install/uninstall the project's git hooks.  Currently:
#
#   pre-commit   — blocks a commit that would put README.md and README.ko.md
#                  out of parity (the same check main-protection.yml runs).
#                  Local-only, no API spend.  See scripts/hooks/pre-commit.sh.
#
#   post-commit  — fires a focused contract audit when a commit touches
#                  drift-risk surfaces (agents/, .claude/agents/, config/,
#                  CLAUDE.md, operator-contract, audit-run.sh, settings).
#                  See scripts/hooks/post-commit.sh for the rules.
#
# Why this isn't auto-run by bootstrap.sh: the post-commit hook fires
# `audit-run.sh` which uses the Anthropic API (auditor subagent is Sonnet).
# A first-time cloner shouldn't get unexpected API spend on their first
# commit.  Maintainers opt in by running this script once.
#
# Usage:
#   scripts/install-hooks.sh [install|uninstall|status]
#
# Default: install.
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
HOOKS_SRC="$SCRIPT_DIR/hooks"
HOOKS_DST="$REPO_ROOT/.git/hooks"

if [[ ! -d "$REPO_ROOT/.git" ]]; then
  echo "❌ $REPO_ROOT is not a git repo (no .git directory)" >&2
  exit 1
fi
mkdir -p "$HOOKS_DST"

# Installed hooks are 2-line trampolines that exec the tracked script in
# scripts/hooks/, not symlinks and not copies.  Why: on Windows/Git Bash
# `ln -s` silently degrades to a *copy* unless MSYS=winsymlinks:nativestrict
# and the shell is elevated.  That left the operator's machine (observed
# 2026-07-26) running stale snapshots of the hooks — edits to
# scripts/hooks/*.sh never reached .git/hooks/ — and `status` reporting
# "NOT managed" for hooks this script had just installed.  A trampoline
# behaves identically on macOS, Linux, and Windows and always runs the
# committed source.
MARKER="managed-by: scripts/install-hooks.sh"

is_managed() {
  [[ -f "$1" ]] && grep -q "$MARKER" "$1" 2>/dev/null
}

install_hook() {
  local name="$1"
  local src="$HOOKS_SRC/$name.sh"
  local dst="$HOOKS_DST/$name"
  if [[ ! -f "$src" ]]; then
    echo "❌ source missing: $src" >&2
    return 1
  fi
  # Only back up a foreign hook — never our own trampoline, or repeated
  # installs litter .git/hooks/ with .bak files.
  if [[ -e "$dst" || -L "$dst" ]] && ! is_managed "$dst"; then
    if [[ -L "$dst" ]] || ! diff -q "$dst" "$src" >/dev/null 2>&1; then
      echo "⚠ $dst exists and isn't managed by this script — moved to $dst.bak"
      mv -f "$dst" "$dst.bak"
    else
      rm -f "$dst"   # an identical legacy copy: nothing worth preserving
    fi
  fi
  cat > "$dst" <<EOF
#!/usr/bin/env bash
# $MARKER -> scripts/hooks/$name.sh  (do not edit; edit the source instead)
exec "\$(git rev-parse --show-toplevel)/scripts/hooks/$name.sh" "\$@"
EOF
  chmod +x "$dst" "$src"
  echo "✓ installed: $dst → $src"
}

uninstall_hook() {
  local name="$1"
  local dst="$HOOKS_DST/$name"
  if is_managed "$dst" || [[ -L "$dst" ]]; then
    rm -f "$dst"
    echo "✓ removed: $dst"
  elif [[ -e "$dst" ]]; then
    echo "⚠ $dst is not managed by this script — leaving alone"
  else
    echo "(not installed: $dst)"
  fi
}

status_hook() {
  local name="$1"
  local dst="$HOOKS_DST/$name"
  if is_managed "$dst"; then
    echo "[$name] installed → scripts/hooks/$name.sh"
  elif [[ -L "$dst" ]]; then
    echo "[$name] legacy symlink → $(readlink "$dst")"
  elif [[ -e "$dst" ]]; then
    echo "[$name] file present but NOT managed by install-hooks.sh"
  else
    echo "[$name] NOT installed"
  fi
}

HOOKS=(pre-commit post-commit)

op="${1:-install}"
case "$op" in
  install)    for h in "${HOOKS[@]}"; do install_hook   "$h"; done ;;
  uninstall)  for h in "${HOOKS[@]}"; do uninstall_hook "$h"; done ;;
  status)     for h in "${HOOKS[@]}"; do status_hook    "$h"; done ;;
  *)
    echo "usage: $0 {install|uninstall|status}" >&2
    exit 64 ;;
esac
