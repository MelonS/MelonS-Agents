#!/usr/bin/env bash
# scripts/worktree-done.sh — finalize the current worktree's work
# and merge it back into main.
#
# Per operator-contract §6 (revised 2026-05-21).  Run this from
# INSIDE the worktree you want to merge — it detects the topic
# branch, rebases on latest origin/main, fast-forwards main, and
# tears down the worktree.
#
# What this script does (in order):
#   1. Verify CWD is a git worktree on a feat/<topic> branch
#      (refuses to run from main or detached HEAD).
#   2. Verify no uncommitted changes in the worktree.
#   3. Fetch + rebase the worktree's branch onto origin/main.
#      Conflicts → stop, surface them, exit non-zero so operator
#      can resolve manually.
#   4. Push the rebased branch.
#   5. Switch to the primary worktree's main, fast-forward merge
#      the topic branch.
#   6. Push main.
#   7. Remove the worktree directory + delete the local branch.
#      (Remote branch is also deleted.)
#
# Usage (from inside the worktree):
#   scripts/worktree-done.sh
#
# Options:
#   --no-merge      Push the branch but do not merge to main.
#                   Useful when operator wants to review the work
#                   before merging.
#   --keep          After merge, keep the worktree directory + branch
#                   (default removes them).
#
# Exit codes:
#   0  merge + cleanup complete
#   2  config / pre-flight error (uncommitted changes, wrong branch, etc.)
#   3  rebase conflict (operator must resolve)
#   4  push or merge failed

set -uo pipefail

NO_MERGE=0
KEEP=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-merge) NO_MERGE=1; shift ;;
    --keep)     KEEP=1; shift ;;
    *)          echo "unknown arg: $1" >&2; exit 2 ;;
  esac
done

CURRENT_BRANCH="$(git branch --show-current 2>/dev/null)" || {
  echo "error: not inside a git repository" >&2
  exit 2
}

if [[ -z "$CURRENT_BRANCH" ]]; then
  echo "error: detached HEAD — switch to a branch first" >&2
  exit 2
fi

if [[ "$CURRENT_BRANCH" == "main" ]]; then
  echo "error: this script runs from inside a worktree, not from main" >&2
  exit 2
fi

if [[ ! "$CURRENT_BRANCH" =~ ^feat/ ]]; then
  echo "error: current branch '$CURRENT_BRANCH' is not a feat/* branch" >&2
  echo "       worktree-done.sh only handles feat/<topic> branches" >&2
  exit 2
fi

if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "error: uncommitted changes in worktree:" >&2
  git status --short >&2
  echo "       commit (or stash + drop) them before running worktree-done.sh" >&2
  exit 2
fi

WORKTREE_DIR="$(git rev-parse --show-toplevel)"
COMMON_GIT_DIR="$(git rev-parse --git-common-dir)"
PRIMARY_REPO="$(cd "$COMMON_GIT_DIR/.." && pwd)"

echo "[worktree-done] worktree:        $WORKTREE_DIR"
echo "[worktree-done] primary repo:    $PRIMARY_REPO"
echo "[worktree-done] topic branch:    $CURRENT_BRANCH"
echo ""

echo "[worktree-done] fetching origin..."
git fetch origin --quiet || {
  echo "warning: git fetch failed; will rebase on local origin/main" >&2
}

echo "[worktree-done] rebasing $CURRENT_BRANCH on origin/main..."
if ! git rebase origin/main; then
  echo "" >&2
  echo "error: rebase conflict — resolve in this worktree, then either:" >&2
  echo "  git rebase --continue   (after fixing the conflict)" >&2
  echo "  git rebase --abort      (to back out and try a different approach)" >&2
  exit 3
fi

echo "[worktree-done] pushing $CURRENT_BRANCH to origin..."
git push --set-upstream origin "$CURRENT_BRANCH" || {
  echo "error: git push failed" >&2
  exit 4
}

if (( NO_MERGE == 1 )); then
  echo ""
  echo "[worktree-done] --no-merge specified — branch pushed; not merging or cleaning up."
  echo "[worktree-done] when ready to merge later, run from primary repo:"
  echo ""
  echo "  cd $PRIMARY_REPO"
  echo "  git checkout main && git pull && git merge --ff-only $CURRENT_BRANCH && git push"
  exit 0
fi

echo ""
echo "[worktree-done] merging into main (FF only) from primary..."
(
  cd "$PRIMARY_REPO" || exit 4
  git checkout main || exit 4
  git pull --ff-only || exit 4
  if ! git merge --ff-only "$CURRENT_BRANCH"; then
    echo "error: fast-forward merge to main failed (probably non-FF)" >&2
    echo "       investigate manually; this script does not force-merge." >&2
    exit 4
  fi
  git push || exit 4
) || exit 4

if (( KEEP == 1 )); then
  echo ""
  echo "[worktree-done] --keep specified; worktree + branch retained:"
  echo "  worktree: $WORKTREE_DIR"
  echo "  branch:   $CURRENT_BRANCH (local + remote)"
  exit 0
fi

echo ""
echo "[worktree-done] removing worktree + branch..."
(
  cd "$PRIMARY_REPO" || exit 4
  git worktree remove "$WORKTREE_DIR" || {
    echo "warning: worktree remove failed — clean up manually" >&2
  }
  git branch -d "$CURRENT_BRANCH" 2>/dev/null || true
  git push origin --delete "$CURRENT_BRANCH" 2>/dev/null || true
)

echo ""
echo "[worktree-done] done.  $CURRENT_BRANCH merged into main."
echo "[worktree-done] continue work from the primary checkout:"
echo ""
echo "  cd $PRIMARY_REPO"
echo ""
