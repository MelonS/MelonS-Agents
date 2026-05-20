#!/usr/bin/env bash
# Edge-case tests for the job-hunt skill.
#
# Companion to tests/smoke.sh.  Smoke validates the happy path
# (structural + end-to-end mock); this file validates failure
# modes — unknown args, malformed source output, total/partial
# source failure, empty filter sections, unknown source names.
#
# Usage: skills/job-hunt/tests/edge-cases.sh
#
# Exit codes:
#   0  — all edge-case behaviors are as documented
#   1  — at least one edge case behaves unexpectedly

set -uo pipefail
# Note: deliberately NOT `set -e` here.  We want to capture exit
# codes from each scenario without aborting the whole script.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
RUN="$SKILL_DIR/scripts/run.sh"

PASS=0
FAIL=0
note() { echo "[edge] $*"; }
record() {
  local label="$1" expected="$2" actual="$3"
  if [[ "$expected" == "$actual" ]]; then
    note "✓ $label  (got $actual)"
    PASS=$((PASS+1))
  else
    note "✗ $label  (expected $expected, got $actual)"
    FAIL=$((FAIL+1))
  fi
}

# ----- 1. Unknown CLI arg should exit 2 with non-empty stderr -----
output=$("$RUN" --bogus-arg 2>&1 >/dev/null)
rc=$?
record "unknown CLI arg → exit 2" 2 "$rc"
if echo "$output" | grep -q "unknown arg"; then
  note "✓ stderr mentions 'unknown arg'"
  PASS=$((PASS+1))
else
  note "✗ stderr does not mention 'unknown arg'"
  FAIL=$((FAIL+1))
fi

# ----- 2. --list-sources alone exits 0 and prints all known plugins -----
output=$("$RUN" --list-sources 2>/dev/null)
rc=$?
record "--list-sources → exit 0" 0 "$rc"
for src in _mock kr-wanted kr-programmers kr-jobkorea kr-saramin; do
  if echo "$output" | grep -q "^$src "; then
    note "✓ --list-sources mentions $src"
    PASS=$((PASS+1))
  else
    note "✗ --list-sources missing $src"
    FAIL=$((FAIL+1))
  fi
done

# ----- 3. Unknown source name → that source is skipped, others still run -----
DIGEST=$("$RUN" --sources=_mock,no-such-source --dry-run --quiet 2>/dev/null)
rc=$?
record "unknown source + valid source → exit 4 (partial)" 4 "$rc"
if [[ -n "$DIGEST" ]] && [[ -f "$DIGEST" ]]; then
  note "✓ partial-success still wrote digest"
  PASS=$((PASS+1))
else
  note "✗ no digest produced on partial success"
  FAIL=$((FAIL+1))
fi

# ----- 4. All sources fail → exit 3 -----
"$RUN" --sources=nope1,nope2 --dry-run --quiet >/dev/null 2>&1
rc=$?
record "all sources missing → exit 3" 3 "$rc"

# ----- 5. Synthesize a malformed source plugin and verify it's skipped -----
tmp_skill_root=$(mktemp -d)
mkdir -p "$tmp_skill_root/sources" "$tmp_skill_root/scripts" "$tmp_skill_root/config" "$tmp_skill_root/tests"
# Copy real plumbing.
cp "$SKILL_DIR/scripts/run.sh"          "$tmp_skill_root/scripts/run.sh"
cp "$SKILL_DIR/scripts/digest.sh"       "$tmp_skill_root/scripts/digest.sh"
cp "$SKILL_DIR/scripts/apply-assist.sh" "$tmp_skill_root/scripts/apply-assist.sh"
cp "$SKILL_DIR/sources/_mock.sh"        "$tmp_skill_root/sources/_mock.sh"
cp "$SKILL_DIR/config/filters.example.yaml" "$tmp_skill_root/config/filters.yaml"
chmod +x "$tmp_skill_root/scripts/"*.sh
# Plant a malformed plugin.
cat <<'MAL' > "$tmp_skill_root/sources/_malformed.sh"
fetch_postings() {
  echo "this is not JSON at all"
}
MAL
chmod +x "$tmp_skill_root/sources/_malformed.sh"

DIGEST=$("$tmp_skill_root/scripts/run.sh" --sources=_mock,_malformed --dry-run --quiet 2>/dev/null)
rc=$?
record "malformed source skipped → exit 4 (partial)" 4 "$rc"

if [[ -n "$DIGEST" ]] && [[ -f "$DIGEST" ]] && grep -q "_mock" "$DIGEST"; then
  note "✓ digest produced from the surviving _mock source"
  PASS=$((PASS+1))
else
  note "✗ malformed-skip path failed to leave a usable digest"
  FAIL=$((FAIL+1))
fi
rm -rf "$tmp_skill_root"

# ----- 6. Empty include/exclude filter — all postings should pass -----
tmp_filters=$(mktemp)
cat <<EOF >"$tmp_filters"
locale: kr
job_categories: [백엔드 개발자]
regions: [서울]
keywords:
  include: []
  exclude: []
sources:
  - _mock
output:
  records_root: ./records/jobs
EOF
DIGEST=$("$RUN" --filters="$tmp_filters" --dry-run --quiet 2>/dev/null)
rc=$?
record "empty include/exclude → exit 0" 0 "$rc"
if [[ -n "$DIGEST" ]]; then
  TOTAL=$(jq -r '.postings_total' "$(dirname "$DIGEST")/index.json" 2>/dev/null || echo 0)
  # _mock has 11 raw postings, 1 dedupe → 10 should pass when no
  # keyword filter applied.  (11 raw = 8 original + 3 new Problem
  # Solver family entries added in v2 seed-expansion work.)
  record "empty filter passes 10 postings (11 raw - 1 dedupe)" 10 "$TOTAL"
fi
rm -f "$tmp_filters"

# ----- 6b. Seed expansion (v2 primary UX) -----
# `--seed "Problem Solver"` should match the problem-solver family
# in config/role-synonyms.yaml and expand to ~24 include keywords.
# Against the _mock fixture, 3 of the 11 entries should match
# (the Problem Solver, Forward Deployed Engineer, and Generalist
# additions seeded specifically for this path).
DIGEST=$("$RUN" --seed "Problem Solver" --sources=_mock --dry-run --quiet 2>/dev/null)
rc=$?
record "--seed 'Problem Solver' → exit 0" 0 "$rc"
if [[ -n "$DIGEST" ]]; then
  TOTAL=$(jq -r '.postings_total' "$(dirname "$DIGEST")/index.json" 2>/dev/null || echo 0)
  record "--seed matches 3 mock Problem-Solver family entries" 3 "$TOTAL"
fi

# Alias inside the same family — `FDE` should match identically.
DIGEST=$("$RUN" --seed "FDE" --sources=_mock --dry-run --quiet 2>/dev/null)
rc=$?
record "--seed 'FDE' (alias) → exit 0" 0 "$rc"
if [[ -n "$DIGEST" ]]; then
  TOTAL=$(jq -r '.postings_total' "$(dirname "$DIGEST")/index.json" 2>/dev/null || echo 0)
  record "--seed 'FDE' yields same 3 matches" 3 "$TOTAL"
fi

# Unknown seed → exit 2 with clear error.
output=$("$RUN" --seed "definitely-not-a-real-role" --sources=_mock --dry-run --quiet 2>&1 >/dev/null)
rc=$?
record "--seed unknown → exit 2" 2 "$rc"
if echo "$output" | grep -q "did not match any role family"; then
  note "✓ unknown-seed stderr is actionable"
  PASS=$((PASS+1))
else
  note "✗ unknown-seed stderr missing actionable text"
  FAIL=$((FAIL+1))
fi

# ----- 7. Missing keywords section entirely -----
tmp_filters=$(mktemp)
cat <<EOF >"$tmp_filters"
locale: kr
job_categories: [백엔드 개발자]
regions: [서울]
sources:
  - _mock
output:
  records_root: ./records/jobs
EOF
"$RUN" --filters="$tmp_filters" --dry-run --quiet >/dev/null 2>&1
rc=$?
record "missing keywords: section → exit 0" 0 "$rc"
rm -f "$tmp_filters"

# ----- 8. Unsupported locale → exit 2 -----
tmp_filters=$(mktemp)
cat <<EOF >"$tmp_filters"
locale: us
job_categories: [Software Engineer]
regions: [Remote]
sources:
  - _mock
output:
  records_root: ./records/jobs
EOF
output=$("$RUN" --filters="$tmp_filters" --dry-run --quiet 2>&1 >/dev/null)
rc=$?
record "locale=us → exit 2" 2 "$rc"
if echo "$output" | grep -q "locale 'us' not implemented"; then
  note "✓ unsupported-locale stderr is actionable"
  PASS=$((PASS+1))
else
  note "✗ unsupported-locale stderr missing actionable text"
  FAIL=$((FAIL+1))
fi
rm -f "$tmp_filters"

# ----- 9. Diff against a forged prior digest — flags new URLs -----
PERSISTENT=$(mktemp -d)
# Run 1: today only.
"$RUN" --sources=_mock --output-root="$PERSISTENT" --quiet >/dev/null 2>&1
# Move today→yesterday so run 2 sees the prior index.
mv "$PERSISTENT/$(date +%F)" "$PERSISTENT/2026-05-19"
# Doctor yesterday's index to drop one URL — that URL should appear
# as "new since" when we re-run today.
TARGET_URL="https://mock.example.com/jobs/100"
jq --arg drop "$TARGET_URL" '
  .postings |= map(select(.url != $drop))
' "$PERSISTENT/2026-05-19/index.json" > "$PERSISTENT/2026-05-19/index.json.tmp"
mv "$PERSISTENT/2026-05-19/index.json.tmp" "$PERSISTENT/2026-05-19/index.json"

# Run 2: should detect $TARGET_URL as new.
"$RUN" --sources=_mock --output-root="$PERSISTENT" --quiet >/dev/null 2>&1
rc=$?
record "diff path → exit 0" 0 "$rc"
NEW_URLS=$(jq -r '.new_urls[]' "$PERSISTENT/$(date +%F)/index.json" 2>/dev/null)
if echo "$NEW_URLS" | grep -qx "$TARGET_URL"; then
  note "✓ diff correctly flagged $TARGET_URL as new"
  PASS=$((PASS+1))
else
  note "✗ diff missed $TARGET_URL — new_urls was: $NEW_URLS"
  FAIL=$((FAIL+1))
fi
rm -rf "$PERSISTENT"

note ""
note "Result: $PASS pass, $FAIL fail"

if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
exit 0
