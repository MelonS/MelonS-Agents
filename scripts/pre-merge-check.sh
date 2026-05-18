#!/usr/bin/env bash
# Pre-merge gate for feat/* → main fast-forward merges.
#
# Per operator-contract.md §6: four gates must pass before a feat
# branch merges to main.  This script automates gates 1 (audit
# CLEAN) and 3 (§5 marker compliance) and reports the manual
# gates 2 (functional test) and 4 (operator OK).
#
# Usage:
#   scripts/pre-merge-check.sh                       # current branch vs main
#   scripts/pre-merge-check.sh feat/<name>           # explicit branch vs main
#   PRE_MERGE_BASE=v0.1.0 scripts/pre-merge-check.sh feat/<name>
#                                                    # compare against tag
#
# Exits non-zero if any automated gate fails.  Designed to be
# called by the operator (or by Claude during merge prep) before
# the actual fast-forward merge command runs.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

BRANCH="${1:-$(git branch --show-current)}"
BASE_BRANCH="${PRE_MERGE_BASE:-main}"

echo "=== pre-merge gate ==="
echo "  branch: $BRANCH"
echo "  base:   $BASE_BRANCH"
echo

if ! git rev-parse --verify "$BRANCH" >/dev/null 2>&1; then
  echo "✗ branch not found: $BRANCH" >&2
  exit 64
fi
if ! git rev-parse --verify "$BASE_BRANCH" >/dev/null 2>&1; then
  echo "✗ base not found: $BASE_BRANCH" >&2
  exit 64
fi

if [[ "$BRANCH" == "$BASE_BRANCH" ]]; then
  echo "✗ branch equals base — nothing to merge" >&2
  exit 64
fi

COMMITS=$(git log "$BASE_BRANCH..$BRANCH" --format='%H' 2>/dev/null || true)
NCOMMITS=$(echo -n "$COMMITS" | grep -c . || true)
if [[ "$NCOMMITS" == "0" ]]; then
  echo "ℹ  no commits in $BRANCH that aren't on $BASE_BRANCH"
  echo "   (already merged, or branch is fast-forward of base — nothing to gate)"
  exit 0
fi

echo "📦 $NCOMMITS commit(s) to merge"
echo

# ----------------------------------------------------------------------
# Gate 1: Audit CLEAN
# ----------------------------------------------------------------------
echo "[gate 1/4] audit (this typically takes 2-3 min)..."

CURRENT_BRANCH="$(git branch --show-current)"
SWITCH_BACK=0
if [[ "$CURRENT_BRANCH" != "$BRANCH" ]]; then
  if ! git diff --quiet || ! git diff --cached --quiet; then
    echo "  ✗ working tree has uncommitted changes — commit or stash first" >&2
    exit 65
  fi
  git checkout "$BRANCH" >/dev/null 2>&1
  SWITCH_BACK=1
fi

AUDIT_LOG=$(mktemp -t pre-merge-audit-XXXXXX)
if ./scripts/audit-run.sh all >"$AUDIT_LOG" 2>&1; then
  if grep -q "audit verdict: CLEAN" "$AUDIT_LOG"; then
    echo "  ✓ audit CLEAN"
    GATE1=PASS
  else
    VERDICT=$(grep "audit verdict:" "$AUDIT_LOG" | tail -1 | sed 's/.*audit verdict: //')
    echo "  ✗ audit verdict: $VERDICT"
    REPORT="docs/audit/$(date +%Y-%m-%d)-all.md"
    [[ -f "$REPORT" ]] && echo "    full report: $REPORT"
    GATE1=FAIL
  fi
else
  echo "  ✗ audit-run.sh failed to execute (see $AUDIT_LOG)"
  GATE1=FAIL
fi
rm -f "$AUDIT_LOG"

if [[ "$SWITCH_BACK" == "1" ]]; then
  git checkout "$CURRENT_BRANCH" >/dev/null 2>&1
fi

echo

# ----------------------------------------------------------------------
# Gate 3: §5 marker compliance (automated)
# ----------------------------------------------------------------------
echo "[gate 3/4] §5 marker compliance (Requested-by: user)..."

# Find commits touching §5-scope paths
COMMITS_NEEDING_MARKER=()
while IFS= read -r commit; do
  files=$(git show --pretty="" --name-only "$commit" 2>/dev/null)
  # §5 scope per operator-contract.md:87-88 is `agents/*.md` and
  # `.claude/agents/*.md` — markdown only, not the whole subtree.
  if echo "$files" | grep -qE '^(\.claude/agents/.+\.md|agents/.+\.md)'; then
    COMMITS_NEEDING_MARKER+=("$commit")
  fi
done <<< "$COMMITS"

if [[ ${#COMMITS_NEEDING_MARKER[@]} -eq 0 ]]; then
  echo "  ✓ no §5-scope changes in this branch (skip)"
  GATE3=PASS
else
  MISSING=()
  for c in "${COMMITS_NEEDING_MARKER[@]}"; do
    if ! git log -1 --format='%B' "$c" 2>/dev/null | grep -q "Requested-by:"; then
      MISSING+=("$c")
    fi
  done
  if [[ ${#MISSING[@]} -eq 0 ]]; then
    echo "  ✓ all ${#COMMITS_NEEDING_MARKER[@]} §5-scope commit(s) carry Requested-by: marker"
    GATE3=PASS
  else
    echo "  ✗ ${#MISSING[@]} commit(s) missing Requested-by: marker:"
    for c in "${MISSING[@]}"; do
      echo "    - $(git log -1 --format='%h %s' "$c")"
    done
    GATE3=FAIL
  fi
fi
echo

# ----------------------------------------------------------------------
# Manual gates: 2 (integration test) + 4 (operator OK)
# ----------------------------------------------------------------------
echo "[gate 2/4] functional integration test — MANUAL"

# Detect change scope to give specific instructions
CHANGED=$(git diff "$BASE_BRANCH...$BRANCH" --name-only 2>/dev/null)
SCOPE=""
echo "$CHANGED" | grep -qE '^agents/missions/music-video/' && SCOPE="$SCOPE music-video"
echo "$CHANGED" | grep -qE '^agents/missions/faceless-short/' && SCOPE="$SCOPE faceless-short"
echo "$CHANGED" | grep -qE '^skills/' && SCOPE="$SCOPE skills"
echo "$CHANGED" | grep -qE '^scripts/.+\.(sh|py)$' && SCOPE="$SCOPE scripts"
echo "$CHANGED" | grep -qE '^docs/' && [[ -z "$SCOPE" ]] && SCOPE="docs-only"

if [[ -z "$SCOPE" ]]; then
  SCOPE="unclassified"
fi

case "$SCOPE" in
  *music-video*)
    echo "  → music-video mission affected.  Run end-to-end against a real track:"
    echo "    ./agents/missions/music-video/run.sh pmtest \"assets/music/<track>.mp3\" \"<keywords>\""
    echo "    Compare resulting short.mp4 against a known-good output."
    ;;
  *faceless-short*)
    echo "  → faceless-short mission affected.  Run end-to-end:"
    echo "    ./agents/missions/faceless-short/run.sh pmtest \"<topic>\""
    ;;
  *skills*)
    echo "  → skills directory affected.  Invoke the changed skill via Claude Code"
    echo "    and verify output equivalence to the prior pipeline path."
    ;;
  *scripts*)
    echo "  → scripts/ tools changed.  Run the affected script with realistic inputs."
    ;;
  docs-only)
    echo "  → doc-only change.  Manual gate may be SKIPPED with operator OK."
    ;;
  *)
    echo "  → unclassified change scope.  Operator inspects feat branch diff and decides:"
    echo "    git diff $BASE_BRANCH...$BRANCH"
    ;;
esac
echo

echo "[gate 4/4] operator merge OK — MANUAL"
echo "  → Final gate is human approval.  Operator confirms in conversation"
echo "    after gates 1-3 pass and gate 2 result is verified."
echo

# ----------------------------------------------------------------------
# Summary
# ----------------------------------------------------------------------
echo "=== summary ==="
printf "  gate 1 (audit CLEAN):       %s   (automated)\n" "$GATE1"
printf "  gate 2 (integration test):  MANUAL  (scope: %s)\n" "${SCOPE# }"
printf "  gate 3 (§5 marker):         %s   (automated)\n" "$GATE3"
printf "  gate 4 (operator OK):       MANUAL\n"
echo

if [[ "$GATE1" == "PASS" && "$GATE3" == "PASS" ]]; then
  echo "✓ automated gates PASSED"
  echo
  echo "next steps:"
  echo "  1. complete gate 2 (functional integration test)"
  echo "  2. operator confirms merge OK (gate 4)"
  echo "  3. git checkout $BASE_BRANCH"
  echo "  4. git merge --ff-only $BRANCH"
  echo "  5. git push origin $BASE_BRANCH"
  echo "  6. git branch -d $BRANCH && git push origin --delete $BRANCH"
  echo "  7. (optional, if milestone) git tag -a v0.x.0 -m '...' && git push origin v0.x.0"
  exit 0
else
  echo "✗ automated gates FAILED — fix on $BRANCH before merging"
  exit 1
fi
