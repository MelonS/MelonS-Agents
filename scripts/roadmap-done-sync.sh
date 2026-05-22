#!/usr/bin/env bash
# roadmap-done-sync.sh — find commits since last Done-section sync and
# emit a compact bulk-reconciliation entry the operator can paste (or
# the agent can auto-commit with --apply).
#
# Removes the recurring manual work caught by audit cycles 39-44+ where
# the Done section drifted N commits behind HEAD.  Per §9 of the
# operator-contract every commit needs a Done entry; this script makes
# the catch-up step a single command instead of a careful read of git
# log + manual append.
#
# Usage:
#   scripts/roadmap-done-sync.sh           # preview (writes to stdout)
#   scripts/roadmap-done-sync.sh --apply   # prepend entry to roadmap Done section
#   scripts/roadmap-done-sync.sh --since=<sha>  # explicit base
#
# Reads:
#   - docs/roadmap.md       (auto-detects the latest sha mentioned in Done)
#   - git log <sha>..HEAD   (commits to backfill)
#
# Writes (only with --apply):
#   - docs/roadmap.md       (prepends new entry above current top entry)
#
# Skips:
#   - Commits whose short-sha already appears in the Done section
#     (idempotent against partial prior syncs).
#   - Roadmap Done sections that are already up-to-date with HEAD.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

ROADMAP="docs/roadmap.md"
APPLY=0
EXPLICIT_BASE=""
for arg in "$@"; do
  case "$arg" in
    --apply) APPLY=1 ;;
    --since=*) EXPLICIT_BASE="${arg#*=}" ;;
    -h|--help)
      sed -n '2,30p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
  esac
done

[[ -f "$ROADMAP" ]] || { echo "[done-sync] $ROADMAP missing" >&2; exit 1; }

# Resolve base commit: explicit --since wins; otherwise scan the Done
# section for the most-recently-mentioned 7-char SHA.  awk extracts the
# Done block; grep pulls all `<7-hex>` matches in order they appear.
if [[ -n "$EXPLICIT_BASE" ]]; then
  BASE="$EXPLICIT_BASE"
else
  done_block="$(awk '/^## Done/{in_done=1; next} in_done && /^## /{exit} {print}' "$ROADMAP")"
  # Each commit hash appearing in the block, newest first (because Done
  # is "most recent first" by maintenance contract).
  candidate_shas=$(printf '%s\n' "$done_block" | grep -oE '`[a-f0-9]{7}`' | tr -d '`' | uniq)
  # Find first candidate that resolves to a real git object — guards
  # against typos or pre-rewrite SHAs.
  BASE=""
  while IFS= read -r sha; do
    [[ -z "$sha" ]] && continue
    if git cat-file -e "${sha}^{commit}" 2>/dev/null; then
      BASE="$sha"
      break
    fi
  done <<< "$candidate_shas"
fi

if [[ -z "$BASE" ]]; then
  echo "[done-sync] could not resolve a base commit from Done section" >&2
  exit 1
fi

# Commit range = base..HEAD.  Drop merge commits.  Drop any commit
# whose short-sha already appears verbatim anywhere in the Done block
# (defense against partial prior syncs).
done_shas_set=$(awk '/^## Done/{in_done=1; next} in_done && /^## /{exit} {print}' "$ROADMAP" \
                | grep -oE '`[a-f0-9]{7}`' | tr -d '`' | sort -u)
range_shas=$(git log "${BASE}..HEAD" --no-merges --pretty=format:%H 2>/dev/null)

missing=()
while IFS= read -r full_sha; do
  [[ -z "$full_sha" ]] && continue
  short="${full_sha:0:7}"
  if ! grep -qx "$short" <<< "$done_shas_set"; then
    missing+=("$full_sha")
  fi
done <<< "$range_shas"

if (( ${#missing[@]} == 0 )); then
  echo "[done-sync] Done section already up-to-date with HEAD (base: $BASE)"
  exit 0
fi

# Compose the bulk-reconciliation block.
today=$(TZ=Asia/Seoul date +%Y-%m-%d)
clock=$(TZ=Asia/Seoul date +%H:%M)

entry="$(cat <<EOF

- **${today}** (~${clock} KST, bulk auto-sync via \`scripts/roadmap-done-sync.sh\`)
  **${#missing[@]} commits backfilled** from base \`${BASE:0:7}\` to HEAD.
  Per §9 every commit needs a Done entry — this is the catch-up batch
  the auditor would otherwise repeatedly flag.  Entries grouped by
  scope; operator may rewrite into narrative form if a specific
  cluster warrants it.

EOF
)"

# Reverse the order so newest commit appears first within the bulk
# block (matches Done's "most recent first" convention).
for full_sha in $(printf '%s\n' "${missing[@]}" | tac 2>/dev/null || printf '%s\n' "${missing[@]}" | tail -r); do
  short="${full_sha:0:7}"
  subject=$(git log -1 --format=%s "$full_sha")
  entry+=$'\n'"  - \`${short}\` — ${subject}"
done
entry+=$'\n'

if (( APPLY == 0 )); then
  echo "[done-sync] preview: ${#missing[@]} commits to backfill (base: ${BASE:0:7})"
  echo "[done-sync] re-run with --apply to insert into $ROADMAP"
  echo
  echo "──── ENTRY PREVIEW ────"
  printf '%s' "$entry"
  exit 0
fi

# Insert entry immediately after the "## Done" header line.  Write the
# entry to a tmp file so awk can stream it via getline — passing
# multi-line strings via -v breaks awk's tokenizer (regression caught
# in autonomous run: --apply nuked roadmap.md the first try).
entry_file="$(mktemp)"
printf '%s' "$entry" > "$entry_file"

tmp="$(mktemp)"
awk -v ef="$entry_file" '
  /^## Done/ && !inserted_after_header {
    print
    inserted_after_header = 1
    next
  }
  inserted_after_header && !inserted && /^- \*\*/ {
    while ((getline line < ef) > 0) print line
    close(ef)
    inserted = 1
  }
  { print }
' "$ROADMAP" > "$tmp"
rm -f "$entry_file"

# Sanity check: tmp file must be non-empty and bigger than what we
# started with (we only ever insert, never delete).
orig_size=$(stat -f %z "$ROADMAP" 2>/dev/null || stat -c %s "$ROADMAP")
new_size=$(stat -f %z "$tmp" 2>/dev/null || stat -c %s "$tmp")
if (( new_size <= orig_size )); then
  echo "[done-sync] aborting: new file ($new_size B) not larger than original ($orig_size B)" >&2
  rm -f "$tmp"
  exit 1
fi
mv "$tmp" "$ROADMAP"
echo "[done-sync] inserted ${#missing[@]}-commit bulk entry into $ROADMAP"
echo "[done-sync] review with: git diff $ROADMAP"
