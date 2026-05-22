#!/usr/bin/env bash
# test-roadmap-done-sync.sh — regression test for roadmap-done-sync.sh.
#
# Per [[idempotency-test-first]] memory: scripts that mutate persistent
# state ship with a regression test in the same window.  The first
# --apply run nuked roadmap.md (1383 lines deleted) because the entry
# variable contained newlines that broke awk -v.  This test pins that
# class of failure plus the normal-path invariants.
#
# Usage: scripts/test-roadmap-done-sync.sh
# Exit: 0 on all PASS, 1 on any fail.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SANDBOX="$(mktemp -d -t done-sync-test-XXXX)"
trap 'rm -rf "$SANDBOX"' EXIT

pass=0
fail=0
say_pass() { echo "  ✓ $1"; pass=$((pass + 1)); }
say_fail() { echo "  ✗ $1"; fail=$((fail + 1)); }

# Build a minimal git repo + roadmap fixture inside sandbox so we can
# safely run --apply without touching the real roadmap.
cd "$SANDBOX"
git init -q .
git config user.email "test@example.com"
git config user.name  "test"

mkdir -p docs scripts
cp "$REPO_ROOT/scripts/roadmap-done-sync.sh" scripts/

# Seed roadmap with a Done section + one existing entry whose SHA we'll
# create as the first commit.
cat > docs/roadmap.md <<'INIT'
# Roadmap

## Now

(boilerplate)

## Done — most recent first

- **2026-05-22** **Initial entry** — base for sync test.
INIT

git add docs/roadmap.md scripts/roadmap-done-sync.sh
git commit -qm "init"
BASE_SHA=$(git rev-parse --short HEAD)

# Patch the seeded Done entry to reference the real BASE_SHA so the
# script can resolve it as base.
sed -i.bak "s|Initial entry|Initial entry (\\\`${BASE_SHA}\\\`)|" docs/roadmap.md
rm -f docs/roadmap.md.bak
git add docs/roadmap.md && git commit -qm "patch seed entry with base SHA"

# Add 5 more commits to backfill.
for i in 1 2 3 4 5; do
  echo "change $i" >> docs/roadmap.md
  git add docs/roadmap.md
  git commit -qm "feat: change $i"
done

orig_lines=$(wc -l < docs/roadmap.md)
orig_md5=$(md5 -q docs/roadmap.md 2>/dev/null || md5sum docs/roadmap.md | awk '{print $1}')

# Run preview — should not modify anything.
bash scripts/roadmap-done-sync.sh >/dev/null 2>&1
post_preview_md5=$(md5 -q docs/roadmap.md 2>/dev/null || md5sum docs/roadmap.md | awk '{print $1}')
[[ "$orig_md5" == "$post_preview_md5" ]] \
  && say_pass "preview does not modify roadmap" \
  || say_fail "preview modified roadmap (md5 changed)"

# Run --apply — file must grow (regression: --apply v1 nuked the file).
bash scripts/roadmap-done-sync.sh --apply >/dev/null 2>&1
new_lines=$(wc -l < docs/roadmap.md)
[[ "$new_lines" -gt "$orig_lines" ]] \
  && say_pass "--apply grew roadmap ($orig_lines → $new_lines lines)" \
  || say_fail "--apply did NOT grow roadmap (orig=$orig_lines, new=$new_lines) — possible nuke regression"

# All 5 backfilled SHAs should appear in roadmap.
expected_shas=$(git log "${BASE_SHA}..HEAD" --pretty=format:%H --no-merges | head -10)
all_found=1
while IFS= read -r full_sha; do
  [[ -z "$full_sha" ]] && continue
  short="${full_sha:0:7}"
  grep -q "$short" docs/roadmap.md || all_found=0
done <<< "$expected_shas"
[[ "$all_found" == "1" ]] \
  && say_pass "all 6 new commits' short-SHAs present in roadmap (1 seed-patch + 5 changes)" \
  || say_fail "one or more new commit SHAs missing"

# Idempotency — re-running --apply must NOT duplicate entries.
lines_before=$(wc -l < docs/roadmap.md)
bash scripts/roadmap-done-sync.sh --apply >/dev/null 2>&1
lines_after=$(wc -l < docs/roadmap.md)
[[ "$lines_before" == "$lines_after" ]] \
  && say_pass "re-running --apply is idempotent (no duplicate entries)" \
  || say_fail "re-run added $((lines_after - lines_before)) lines (should be 0)"

# Pre-existing Done content preserved.
grep -q "Initial entry" docs/roadmap.md \
  && say_pass "pre-existing Done entry preserved" \
  || say_fail "pre-existing entry was stripped"

echo
echo "[test] result: $pass passed, $fail failed"
exit $(( fail > 0 ? 1 : 0 ))
