#!/usr/bin/env bash
# scripts/worktree-new.sh — open a sibling worktree for a new
# parallel-session topic.
#
# Per operator-contract §6 (revised 2026-05-21), parallel Claude
# Code sessions on this machine each get their own git worktree
# alongside the primary checkout — sibling layout, e.g.
#
#   /Users/melons/ai              ← primary checkout (this script's repo)
#   /Users/melons/ai-job-hunt     ← worktree opened by this script
#   /Users/melons/ai-music-video  ← another worktree from another session
#
# What this script does (in order):
#   1. Validate operator passed a topic slug.
#   2. Resolve the primary checkout's parent directory.
#   3. Fetch latest origin/main so the new branch starts from real HEAD.
#   4. Create a fresh feat/<topic> branch from origin/main.
#   5. git worktree add ../ai-<topic> feat/<topic>.
#   6. Copy operator-local files that are gitignored but needed
#      (.env primarily) so the worktree can run anything without
#      re-bootstrap.
#   7. Print the cd command for the operator (or the next Claude
#      Code session) to enter the worktree.
#
# Usage:
#   scripts/worktree-new.sh <topic>          # default branch name feat/<topic>
#   scripts/worktree-new.sh <topic> --from <base>   # branch from <base> instead of origin/main
#
# Exit codes:
#   0  worktree created + ready
#   2  argument / state error
#   3  git operation failed

set -uo pipefail

TOPIC="${1:-}"
BASE="origin/main"

if [[ -z "$TOPIC" ]]; then
  echo "usage: scripts/worktree-new.sh <topic> [--from <base>]" >&2
  echo "" >&2
  echo "  <topic> = short kebab-case slug, becomes feat/<topic> branch and ai-<topic> directory" >&2
  echo "" >&2
  echo "examples:" >&2
  echo "  scripts/worktree-new.sh job-hunt-globals" >&2
  echo "  scripts/worktree-new.sh music-video-genre-shaders" >&2
  exit 2
fi

shift
while [[ $# -gt 0 ]]; do
  case "$1" in
    --from)   BASE="$2"; shift 2 ;;
    --from=*) BASE="${1#--from=}"; shift ;;
    *)        echo "unknown arg: $1" >&2; exit 2 ;;
  esac
done

# Slug validation — keep it filesystem + git-branch safe.
if ! [[ "$TOPIC" =~ ^[a-z][a-z0-9-]*$ ]]; then
  echo "error: topic '$TOPIC' must be lowercase letters / digits / hyphens, starting with a letter" >&2
  exit 2
fi

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || {
  echo "error: not inside a git repository" >&2
  exit 2
}
REPO_PARENT="$(dirname "$REPO_ROOT")"
REPO_NAME="$(basename "$REPO_ROOT")"

BRANCH="feat/$TOPIC"
WT_DIR="$REPO_PARENT/${REPO_NAME}-$TOPIC"

if [[ -e "$WT_DIR" ]]; then
  echo "error: target path already exists: $WT_DIR" >&2
  echo "       remove it first (or pick a different topic)" >&2
  exit 2
fi

if git show-ref --verify --quiet "refs/heads/$BRANCH"; then
  echo "error: branch $BRANCH already exists locally — pick a different topic or delete the old branch" >&2
  exit 2
fi

echo "[worktree-new] fetching latest from origin..."
git fetch origin --quiet || {
  echo "warning: git fetch failed; continuing with local state of $BASE" >&2
}

echo "[worktree-new] creating worktree:"
echo "  path:    $WT_DIR"
echo "  branch:  $BRANCH"
echo "  base:    $BASE"

if ! git worktree add "$WT_DIR" -b "$BRANCH" "$BASE"; then
  echo "error: git worktree add failed" >&2
  exit 3
fi

# Copy operator-local files that are gitignored but useful.
if [[ -f "$REPO_ROOT/.env" ]]; then
  cp "$REPO_ROOT/.env" "$WT_DIR/.env"
  echo "[worktree-new] copied .env into worktree"
fi
if [[ -f "$REPO_ROOT/skills/job-hunt/config/operator-profile.md" ]]; then
  mkdir -p "$WT_DIR/skills/job-hunt/config"
  cp "$REPO_ROOT/skills/job-hunt/config/operator-profile.md" \
     "$WT_DIR/skills/job-hunt/config/operator-profile.md"
  echo "[worktree-new] copied operator-profile.md into worktree"
fi

echo ""
echo "[worktree-new] done.  enter the worktree with:"
echo ""
echo "  cd $WT_DIR"
echo ""
echo "when work is finished, run inside the worktree:"
echo ""
echo "  scripts/worktree-done.sh"
echo ""
