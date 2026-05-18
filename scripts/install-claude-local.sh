#!/usr/bin/env bash
# install-claude-local.sh — render machine-specific Claude Code config
# from tracked templates, and wire .claude/ symlinks to top-level
# tracked sources.  Idempotent.  Cross-platform (macOS + Linux).
#
# Purpose: operator-contract §8 principles 3 (machine-resilient) +
# 4 (multi-machine portable).  `git clone` + this script = a fully-
# functional Claude Code environment on any qualified machine.
#
# Called automatically by scripts/bootstrap.sh, but safe to run
# directly whenever a tracked template changes.
#
# What it does:
#
#   1. Renders config/claude-settings.template.json → .claude/settings.json
#      with @@HOME@@ / @@REPO_ROOT@@ / @@HOME_PARENT@@ / @@MEMORY_NAMESPACE@@
#      substitutions for the current operator + machine.
#
#   2. Symlinks .claude/skills → ../skills so Claude Code's default
#      project-skill discovery path resolves to our top-level
#      tracked skills/ directory.
#
#   3. (Future, when subagent migration lands) symlinks .claude/agents
#      → ../subagents the same way.
#
#   4. Verifies the result by reading back key markers.
#
# All operations are idempotent — running twice is safe.

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# ----------------------------------------------------------------------
# Detect environment
# ----------------------------------------------------------------------

OPERATOR_HOME="${HOME:-}"
if [[ -z "$OPERATOR_HOME" ]]; then
  echo "❌ \$HOME is not set" >&2
  exit 1
fi

# /Users on macOS, /home on Linux (parent of $HOME)
HOME_PARENT="$(dirname "$OPERATOR_HOME")"

# Claude Code's memory-namespace convention: leading dash + path with
# slashes replaced by dashes.  E.g., /Users/melons/ai → -Users-melons-ai
MEMORY_NAMESPACE="$(echo "$REPO_ROOT" | sed 's/\//-/g')"

OS="$(uname -s)"

echo "[install-claude-local] environment:"
echo "  REPO_ROOT         = $REPO_ROOT"
echo "  HOME              = $OPERATOR_HOME"
echo "  HOME_PARENT       = $HOME_PARENT"
echo "  MEMORY_NAMESPACE  = $MEMORY_NAMESPACE"
echo "  OS                = $OS"
echo

# ----------------------------------------------------------------------
# Step 1: render .claude/settings.json from template
# ----------------------------------------------------------------------

TEMPLATE="config/claude-settings.template.json"
RENDERED=".claude/settings.json"

if [[ ! -f "$TEMPLATE" ]]; then
  echo "❌ template not found: $TEMPLATE" >&2
  exit 1
fi

mkdir -p .claude

echo "[install-claude-local] rendering $TEMPLATE → $RENDERED"

# sed-based substitution.  Order matters — substitute longer
# placeholders first to avoid partial replacements (e.g.,
# @@HOME_PARENT@@ before @@HOME@@).
sed \
  -e "s|@@HOME_PARENT@@|$HOME_PARENT|g" \
  -e "s|@@HOME@@|$OPERATOR_HOME|g" \
  -e "s|@@REPO_ROOT@@|$REPO_ROOT|g" \
  -e "s|@@MEMORY_NAMESPACE@@|$MEMORY_NAMESPACE|g" \
  "$TEMPLATE" > "$RENDERED.tmp"

# Verify no remaining placeholders
if grep -q '@@[A-Z_]\+@@' "$RENDERED.tmp"; then
  echo "❌ unresolved placeholders in rendered output:" >&2
  grep -n '@@[A-Z_]\+@@' "$RENDERED.tmp" >&2
  rm -f "$RENDERED.tmp"
  exit 1
fi

# Verify JSON parses
if ! python3 -c "import json,sys; json.load(open('$RENDERED.tmp'))" 2>/dev/null; then
  echo "❌ rendered $RENDERED is not valid JSON" >&2
  rm -f "$RENDERED.tmp"
  exit 1
fi

mv "$RENDERED.tmp" "$RENDERED"
echo "  ✓ $RENDERED rendered + validated"
echo

# ----------------------------------------------------------------------
# Step 2: .claude/skills → ../skills symlink
# ----------------------------------------------------------------------

SKILLS_LINK=".claude/skills"
SKILLS_TARGET="../skills"

if [[ -L "$SKILLS_LINK" ]]; then
  current="$(readlink "$SKILLS_LINK")"
  if [[ "$current" == "$SKILLS_TARGET" ]]; then
    echo "[install-claude-local] $SKILLS_LINK already points at $SKILLS_TARGET — OK"
  else
    echo "[install-claude-local] $SKILLS_LINK points at $current — fixing"
    rm "$SKILLS_LINK"
    ln -s "$SKILLS_TARGET" "$SKILLS_LINK"
    echo "  ✓ fixed"
  fi
elif [[ -e "$SKILLS_LINK" ]]; then
  echo "❌ $SKILLS_LINK exists and is not a symlink — refusing to overwrite" >&2
  echo "   inspect manually; expected: symlink → $SKILLS_TARGET" >&2
  exit 1
else
  ln -s "$SKILLS_TARGET" "$SKILLS_LINK"
  echo "[install-claude-local] $SKILLS_LINK → $SKILLS_TARGET created"
fi
echo

# ----------------------------------------------------------------------
# Step 3: (placeholder) .claude/agents → ../subagents symlink
# ----------------------------------------------------------------------

# Will activate when subagent migration lands (see docs/ideas.md
# Layer 6).  Currently .claude/agents/ is a real directory with
# tracked .md files, so this step is a no-op until migration.

if [[ -d "subagents" ]]; then
  AGENTS_LINK=".claude/agents"
  AGENTS_TARGET="../subagents"
  if [[ -L "$AGENTS_LINK" ]]; then
    current="$(readlink "$AGENTS_LINK")"
    if [[ "$current" == "$AGENTS_TARGET" ]]; then
      echo "[install-claude-local] $AGENTS_LINK already points at $AGENTS_TARGET — OK"
    fi
  elif [[ -d "$AGENTS_LINK" ]] && [[ -z "$(ls -A "$AGENTS_LINK" 2>/dev/null)" ]]; then
    rmdir "$AGENTS_LINK"
    ln -s "$AGENTS_TARGET" "$AGENTS_LINK"
    echo "[install-claude-local] $AGENTS_LINK → $AGENTS_TARGET created (subagent migration)"
  else
    echo "[install-claude-local] $AGENTS_LINK has real content — subagent migration not yet applied (OK)"
  fi
  echo
fi

# ----------------------------------------------------------------------
# Step 4: verification
# ----------------------------------------------------------------------

echo "[install-claude-local] verification:"

# 4a. settings.json has substituted paths and parses as JSON
if grep -q '@@[A-Z_]\+@@' "$RENDERED"; then
  echo "  ✗ $RENDERED still has placeholders" >&2
  exit 1
fi
echo "  ✓ $RENDERED has no unresolved placeholders"

if python3 -c "import json,sys; json.load(open('$RENDERED'))" 2>/dev/null; then
  echo "  ✓ $RENDERED is valid JSON"
else
  echo "  ✗ $RENDERED is not valid JSON" >&2
  exit 1
fi

# 4b. skills symlink resolves to a real directory
if [[ -L "$SKILLS_LINK" ]] && [[ -d "$SKILLS_LINK/" ]]; then
  count="$(find "$SKILLS_LINK/" -maxdepth 1 -mindepth 1 -name 'SKILL.md' -o -name '*' -type d 2>/dev/null | wc -l | tr -d ' ')"
  skill_count="$(find "$SKILLS_LINK/" -mindepth 2 -name 'SKILL.md' 2>/dev/null | wc -l | tr -d ' ')"
  echo "  ✓ $SKILLS_LINK resolves to a directory with $skill_count skill(s)"
else
  echo "  ✗ $SKILLS_LINK does not resolve to a directory" >&2
  exit 1
fi

echo
echo "[install-claude-local] ✓ done.  .claude/ now reflects this machine's environment."
echo "                       Re-run after editing config/claude-settings.template.json"
echo "                       or adding a new skill under skills/."
