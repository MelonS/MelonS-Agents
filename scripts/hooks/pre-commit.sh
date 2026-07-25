#!/usr/bin/env bash
# Pre-commit hook — blocks a commit that would push README.md <-> README.ko.md
# out of parity, i.e. the exact check `main-protection.yml` runs in CI.
#
# Why this exists: 2026-07-25 commit 8c67d7e added a code block to README.md
# only (the start-here.sh split).  `main` went red on the next push and stayed
# red for 5 consecutive runs, because the parity guard lived *only* in CI and
# docs/ko-style-guide.md §3 Layer 1 asks the author to run it by hand.  A
# manual step that is skipped once stays skipped.  This hook makes Layer 1
# mechanical.
#
# Install with:
#   scripts/install-hooks.sh install
#
# Disable with:
#   scripts/install-hooks.sh uninstall        # remove the hook
#   PARITY_HOOK_DISABLED=1 git commit ...     # one-off skip
#   git commit --no-verify ...                # git's own escape hatch
#
# Design notes:
# - Checks the *staged* blobs (`git show :README.md`), not the working tree.
#   That is exactly what lands in the commit and therefore what CI will see;
#   a partially-staged README would otherwise pass or fail for the wrong
#   reason.  Both files are read from the index, which always carries an
#   entry for every tracked file (unmodified ones match HEAD).
# - Only fires when at least one README is staged.  Every other commit pays
#   nothing.
# - Soft-skips (exit 0 + warning) when no Python interpreter is on PATH.
#   Blocking the operator's commit over a missing interpreter is worse than
#   deferring to CI, which owns the hard gate.
# - Runs in the FOREGROUND, unlike post-commit.sh's background audit: the
#   whole point is to fail before the commit object exists.  The check is
#   pure-Python string counting on two files, ~30ms.
set -u

if [[ "${PARITY_HOOK_DISABLED:-0}" == "1" ]]; then
  exit 0
fi

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)"
[[ -z "$REPO_ROOT" || ! -d "$REPO_ROOT/.git" ]] && exit 0

STAGED=$(git diff --cached --name-only 2>/dev/null)
if ! echo "$STAGED" | grep -qE '^README(\.ko)?\.md$'; then
  exit 0
fi

PARITY="$REPO_ROOT/scripts/readme-parity.py"
if [[ ! -f "$PARITY" ]]; then
  echo "[parity-hook] scripts/readme-parity.py missing — skipping (CI still gates)" >&2
  exit 0
fi

PY=""
for candidate in python3 python; do
  if command -v "$candidate" >/dev/null 2>&1; then
    PY="$candidate"
    break
  fi
done
if [[ -z "$PY" ]]; then
  echo "[parity-hook] no python3/python on PATH — skipping (CI still gates)" >&2
  exit 0
fi

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

# Index versions = what this commit will contain.
if ! git show :README.md    > "$TMP/README.md"    2>/dev/null \
  || ! git show :README.ko.md > "$TMP/README.ko.md" 2>/dev/null; then
  echo "[parity-hook] could not read both READMEs from the index — skipping (CI still gates)" >&2
  exit 0
fi

if ! "$PY" "$PARITY" "$TMP/README.md" "$TMP/README.ko.md"; then
  cat >&2 <<'MSG'

[parity-hook] commit BLOCKED — README.md <-> README.ko.md drift (see above).
              Fix README.ko.md to match README.md, then re-stage it.
              Style rules + glossary: docs/ko-style-guide.md
              One-off override: PARITY_HOOK_DISABLED=1 git commit ...
MSG
  exit 1
fi

exit 0
