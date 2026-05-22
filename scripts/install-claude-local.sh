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
# Step 3: render operator-style block into ~/.claude/CLAUDE.md
# ----------------------------------------------------------------------
# The operator-style preferences (Dual-stack reporting, Terminal/shell
# format, Batch execution, Writing tone, Idle-state signaling,
# Scrum-master footer) were split out of docs/operator-contract.md on
# 2026-05-22 and now live in ~/.claude/CLAUDE.md under "Operator
# style".  That file is per-user / local; a fresh clone on a new
# machine has no copy.  The canonical content stays in this repo at
# config/claude-global.template.md and is rendered into
# ~/.claude/CLAUDE.md between BEGIN/END markers, idempotently.

GLOBAL_TEMPLATE="config/claude-global.template.md"
GLOBAL_TARGET="$OPERATOR_HOME/.claude/CLAUDE.md"

if [[ -f "$GLOBAL_TEMPLATE" ]]; then
  mkdir -p "$(dirname "$GLOBAL_TARGET")"
  echo "[install-claude-local] rendering $GLOBAL_TEMPLATE → $GLOBAL_TARGET"

  if [[ ! -f "$GLOBAL_TARGET" ]]; then
    # Fresh install — create with a small header + the substituted template block.
    {
      printf '# Global Claude Code Instructions\n\n'
      printf 'Project: `%s` — see that repo'\''s `CLAUDE.md` for project-specific behavior.\n\n' "$REPO_ROOT"
      printf -- '---\n\n'
      sed \
        -e "s|@@HOME_PARENT@@|$HOME_PARENT|g" \
        -e "s|@@HOME@@|$OPERATOR_HOME|g" \
        -e "s|@@REPO_ROOT@@|$REPO_ROOT|g" \
        -e "s|@@MEMORY_NAMESPACE@@|$MEMORY_NAMESPACE|g" \
        "$GLOBAL_TEMPLATE"
    } > "$GLOBAL_TARGET"
    echo "  ✓ created $GLOBAL_TARGET with operator-style block"
  elif grep -qE 'BEGIN repo-managed operator-style block|<!-- ┌─' "$GLOBAL_TARGET"; then
    # Pre-render the template with @@…@@ substitutions so the inserted
    # block has the operator's actual paths (the awk feed below reads
    # the substituted version, not the raw template).
    rendered_tmpl="$(mktemp)"
    sed \
      -e "s|@@HOME_PARENT@@|$HOME_PARENT|g" \
      -e "s|@@HOME@@|$OPERATOR_HOME|g" \
      -e "s|@@REPO_ROOT@@|$REPO_ROOT|g" \
      -e "s|@@MEMORY_NAMESPACE@@|$MEMORY_NAMESPACE|g" \
      "$GLOBAL_TEMPLATE" > "$rendered_tmpl"
    # Existing install with markers — replace between markers in-place.
    # Use awk for portable in-place replacement (BSD/GNU sed -i differ).
    #
    # Idempotency note: the prior template used a multi-line decorative
    # box (`<!-- ┌───┐ │ BEGIN repo-managed ... │ └───┘ -->`).  The old
    # awk pattern only matched the BEGIN line — so the decorative opener
    # `<!-- ┌─` line was passed through unchanged, and each install run
    # stacked one more opener on top of the prior one.  Operator saw 9
    # stacked openers after 9 installs.
    #
    # Fix: match the opener line `<!-- ┌─` OR the BEGIN line as the
    # block start; match the closer line `└─...-->` OR the END comment
    # as the block end.  Either marker family triggers a single
    # replacement.  Migration: when the old (decorative) format is
    # detected, the first `<!-- ┌─` triggers in_block, and all stacked
    # `<!-- ┌─` lines fall inside the block and get stripped together.
    tmp="$(mktemp)"
    awk -v tmpl="$rendered_tmpl" '
      # Block-start: either the new single-line BEGIN comment or any
      # legacy decorative opener.  Insert template exactly once via the
      # `inserted` guard so multiple stacked openers collapse into one.
      /^<!-- BEGIN repo-managed operator-style block/ ||
      /^<!-- ┌─/ ||
      /BEGIN repo-managed operator-style block/ {
        in_block = 1
        if (!inserted) {
          while ((getline line < tmpl) > 0) print line
          close(tmpl)
          inserted = 1
        }
        next
      }
      # Block-end: ONLY the single-line END comment.  The legacy
      # decorative closer `└─...┘ -->` is the END of the BEGIN
      # multi-line *comment*, NOT the end of the operator-style
      # block — treating it as block-end let the legacy body leak
      # through to output (regression caught by
      # test-install-claude-local.sh assertion #5).
      /^<!-- END repo-managed operator-style block/ {
        in_block = 0
        next
      }
      # Legacy decorative box-closer — consume silently while
      # in_block so it does not bleed through.
      /└─.*-->[[:space:]]*$/ {
        if (in_block) next
      }
      !in_block { print }
    ' "$GLOBAL_TARGET" > "$tmp"
    mv "$tmp" "$GLOBAL_TARGET"
    rm -f "$rendered_tmpl"
    echo "  ✓ refreshed operator-style block in $GLOBAL_TARGET (in place)"
  else
    # Existing install without markers — append substituted template,
    # leaving the operator's prior content above untouched.
    {
      printf '\n\n'
      printf -- '---\n\n'
      sed \
        -e "s|@@HOME_PARENT@@|$HOME_PARENT|g" \
        -e "s|@@HOME@@|$OPERATOR_HOME|g" \
        -e "s|@@REPO_ROOT@@|$REPO_ROOT|g" \
        -e "s|@@MEMORY_NAMESPACE@@|$MEMORY_NAMESPACE|g" \
        "$GLOBAL_TEMPLATE"
    } >> "$GLOBAL_TARGET"
    echo "  ✓ appended operator-style block to $GLOBAL_TARGET (prior content preserved above)"
  fi
  echo
fi

# ----------------------------------------------------------------------
# Step 4: (placeholder) .claude/agents → ../subagents symlink
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
