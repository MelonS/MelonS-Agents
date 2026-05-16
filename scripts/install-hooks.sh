#!/usr/bin/env bash
# Install/uninstall the project's git hooks.  Currently:
#
#   post-commit  — fires a focused contract audit when a commit touches
#                  drift-risk surfaces (agents/, .claude/agents/, config/,
#                  CLAUDE.md, operator-contract, audit-run.sh, settings).
#                  See scripts/hooks/post-commit.sh for the rules.
#
# Why this isn't auto-run by bootstrap.sh: the hook fires `audit-run.sh`
# which uses the Anthropic API (auditor subagent is Sonnet).  A first-
# time cloner shouldn't get unexpected API spend on their first commit.
# Maintainers opt in by running this script once.
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

install_hook() {
  local name="$1"
  local src="$HOOKS_SRC/$name.sh"
  local dst="$HOOKS_DST/$name"
  if [[ ! -f "$src" ]]; then
    echo "❌ source missing: $src" >&2
    return 1
  fi
  if [[ -e "$dst" && ! -L "$dst" ]]; then
    echo "⚠ $dst exists and is not a symlink — moved to $dst.bak"
    mv "$dst" "$dst.bak"
  fi
  ln -sf "../../scripts/hooks/$name.sh" "$dst"
  chmod +x "$src"
  echo "✓ installed: $dst → $src"
}

uninstall_hook() {
  local name="$1"
  local dst="$HOOKS_DST/$name"
  if [[ -L "$dst" ]]; then
    rm -f "$dst"
    echo "✓ removed: $dst"
  elif [[ -e "$dst" ]]; then
    echo "⚠ $dst is not a symlink (not managed by this script) — leaving alone"
  else
    echo "(not installed: $dst)"
  fi
}

status_hook() {
  local name="$1"
  local dst="$HOOKS_DST/$name"
  if [[ -L "$dst" ]]; then
    echo "[$name] symlink → $(readlink "$dst")"
  elif [[ -e "$dst" ]]; then
    echo "[$name] file present but NOT managed by install-hooks.sh"
  else
    echo "[$name] NOT installed"
  fi
}

op="${1:-install}"
case "$op" in
  install)    install_hook post-commit ;;
  uninstall)  uninstall_hook post-commit ;;
  status)     status_hook post-commit ;;
  *)
    echo "usage: $0 {install|uninstall|status}" >&2
    exit 64 ;;
esac
