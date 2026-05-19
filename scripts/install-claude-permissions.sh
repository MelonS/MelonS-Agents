#!/usr/bin/env bash
# install-claude-permissions.sh — merge the project's Claude Code allow
# list into the USER-level ~/.claude/settings.json so per-tool permission
# prompts stop firing during a first-time clone-and-go session.
#
# Why this exists (2026-05-19, friend-at-2pm observed friction):
# The project-level .claude/settings.json (rendered by
# install-claude-local.sh) covers in-project tool calls, but Claude Code
# also consults the user-level ~/.claude/settings.json — and on first
# opening of the project, the user-level file is what governs the
# initial trust experience.  Without bulk grants at the user level, a
# fresh-clone session asks per-tool for ~70 distinct commands the
# project's scripts use, turning onboarding into a click marathon.
#
# Operator's framing: "다 하나씩 승인하기에는 너무 장벽이커.. 첨에
# 권한관련해서도 승인하면 어느정도 넘어가게 되어야 할듯" — approve
# once, get covered for the project's normal range of commands.
#
# What this script does:
#
#   1. Detects whether Claude Code is in use (~/.claude/ exists).
#   2. If --prompt is passed (default in interactive bootstrap), asks
#      the operator once for consent before touching the user file.
#      --yes skips the prompt (autonomous mode).
#   3. Reads the project's rendered allow list from .claude/settings.json
#      (which install-claude-local.sh produced for this machine).
#   4. Merges those entries into ~/.claude/settings.json under
#      .permissions.allow, deduplicated, preserving anything already
#      there.  Existing deny list is preserved untouched.
#   5. Writes a small _notes block recording the source + date so a
#      future audit can trace which entries came from this script.
#
# Idempotent: re-running adds nothing new if the entries already exist.
# Safe: never deletes anything from the user file; only appends.
#
# Usage:
#   scripts/install-claude-permissions.sh --prompt   # ask Y/n once
#   scripts/install-claude-permissions.sh --yes      # no prompt
#   scripts/install-claude-permissions.sh --dry-run  # show diff, no write

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

MODE="prompt"  # prompt | yes | dry-run
for arg in "$@"; do
  case "$arg" in
    --prompt)  MODE="prompt" ;;
    --yes|-y)  MODE="yes" ;;
    --dry-run) MODE="dry-run" ;;
    -h|--help)
      sed -n '2,40p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *)
      echo "unknown arg: $arg (use --prompt / --yes / --dry-run)" >&2
      exit 64
      ;;
  esac
done

PROJECT_SETTINGS=".claude/settings.json"
USER_SETTINGS="$HOME/.claude/settings.json"

# ----------------------------------------------------------------------
# Preconditions
# ----------------------------------------------------------------------

if ! command -v jq >/dev/null 2>&1; then
  echo "❌ jq is required (brew install jq / apt install jq)" >&2
  exit 1
fi

if [[ ! -f "$PROJECT_SETTINGS" ]]; then
  echo "ℹ  $PROJECT_SETTINGS not found — running install-claude-local.sh first" >&2
  if ! ./scripts/install-claude-local.sh >/dev/null 2>&1; then
    echo "❌ install-claude-local.sh failed; cannot proceed" >&2
    exit 1
  fi
fi

if [[ ! -d "$HOME/.claude" ]]; then
  echo "ℹ  ~/.claude/ does not exist — Claude Code likely not installed yet"
  echo "   Skipping user-level permission install.  If you install Claude Code"
  echo "   later, re-run: scripts/install-claude-permissions.sh"
  exit 0
fi

# ----------------------------------------------------------------------
# Consent prompt (interactive mode)
# ----------------------------------------------------------------------

if [[ "$MODE" == "prompt" ]]; then
  cat <<EOM

=== Claude Code user-level permission bootstrap ===

The MelonS-Agents project's scripts use ~70 distinct commands (ffmpeg,
ollama, aubio, jq, curl, git, etc.).  Without bulk pre-approval Claude
Code will prompt you for each one the first time you run a mission.

This script can merge the project's allow list into your USER-level
~/.claude/settings.json so those prompts are suppressed before they
fire.  Your existing user settings (deny list, statusline, model
preferences, etc.) will NOT be touched — only .permissions.allow is
extended.

Diff preview will be shown after consent.

EOM
  read -p "Proceed? [Y/n] " -r reply
  reply="${reply:-Y}"
  if [[ ! "$reply" =~ ^[Yy] ]]; then
    echo "Skipped.  Per-command prompts will continue to fire on first use."
    exit 0
  fi
fi

# ----------------------------------------------------------------------
# Merge logic
# ----------------------------------------------------------------------

# Extract the project's allow list.
project_allow=$(jq -c '.permissions.allow // []' "$PROJECT_SETTINGS")
project_count=$(echo "$project_allow" | jq 'length')

# Initialize user-settings file if missing.
if [[ ! -f "$USER_SETTINGS" ]]; then
  mkdir -p "$(dirname "$USER_SETTINGS")"
  echo '{}' > "$USER_SETTINGS"
fi

# Read current user allow list.
user_allow=$(jq -c '.permissions.allow // []' "$USER_SETTINGS" 2>/dev/null || echo '[]')
user_count_before=$(echo "$user_allow" | jq 'length')

# Compute the merged + deduplicated allow list, preserving order
# (user entries first, then project entries that are new).
merged_allow=$(jq -nc \
  --argjson user "$user_allow" \
  --argjson project "$project_allow" \
  '($user + $project) | unique_by(.)')
merged_count=$(echo "$merged_allow" | jq 'length')
added_count=$((merged_count - user_count_before))

echo "  user_settings:  $USER_SETTINGS"
echo "  before:         $user_count_before allow entry(ies)"
echo "  project source: $project_count entries"
echo "  after merge:    $merged_count entries"
echo "  net added:      $added_count"

if [[ "$MODE" == "dry-run" ]]; then
  echo
  echo "[dry-run] would add the following $added_count entry(ies):"
  jq -c --argjson user "$user_allow" --argjson merged "$merged_allow" \
    '$merged - $user' <<< 'null' | jq '.[]' 2>/dev/null
  exit 0
fi

if (( added_count == 0 )); then
  echo "  ✓ user settings already have all project entries — no change"
  exit 0
fi

# Apply the merge.  Also write a _notes.melons_agents block recording
# provenance.  We use a temp file so a partial write can't corrupt the
# user's existing file.
TMP_OUT="$(mktemp -t melons-perm-XXXXXX).json"
# jq string interpolation \(…) is the way to splice variables into a
# string value — the `+` operator from the host shell does not work
# inside a jq string literal.
jq --argjson merged "$merged_allow" \
   --arg date "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
   --arg source "$REPO_ROOT" \
   '.permissions = (.permissions // {}) |
    .permissions.allow = $merged |
    ._notes = (._notes // {}) |
    ._notes.melons_agents = {
      "source": $source,
      "merged_at": $date,
      "regenerate": "cd \($source) && scripts/install-claude-permissions.sh --yes"
    }' "$USER_SETTINGS" > "$TMP_OUT"

# Validate before overwrite.  Three checks:
# 1. jq invocation above must have produced non-empty output (an
#    empty stdout would still pass `jq empty < empty_file` since jq
#    treats empty input as valid).
# 2. The temp file must be parseable as a JSON document.
# 3. The expected .permissions.allow array must be present.
if [[ ! -s "$TMP_OUT" ]]; then
  echo "❌ merge produced empty output — refusing to overwrite" >&2
  rm -f "$TMP_OUT"
  exit 1
fi
if ! jq -e '.permissions.allow | type == "array"' "$TMP_OUT" >/dev/null 2>&1; then
  echo "❌ merge output missing .permissions.allow array — refusing to overwrite" >&2
  rm -f "$TMP_OUT"
  exit 1
fi

mv "$TMP_OUT" "$USER_SETTINGS"
echo "  ✓ $USER_SETTINGS updated (+${added_count} allow entries)"
echo
echo "Restart Claude Code to pick up the new user-level settings."
echo "If you only see permission prompts inside this project, the rendered"
echo ".claude/settings.json already covers those — restart not required."
