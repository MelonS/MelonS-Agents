#!/usr/bin/env bash
# Schema-validation test for source plugins.
#
# For each sources/<name>.sh plugin, source it, call fetch_postings(),
# and validate the JSON output against
# sources/source-plugin.schema.json.
#
# Skips entirely if no JSON Schema validator is available on the host
# (python3+jsonschema preferred; ajv-cli also accepted).
#
# Usage: skills/job-hunt/tests/schema-validation.sh
#
# Exit codes:
#   0  — all plugin outputs validate, or check skipped
#   1  — at least one plugin output fails validation

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SCHEMA="$SKILL_DIR/sources/source-plugin.schema.json"
REPO_ROOT="$(cd "$SKILL_DIR/../.." && pwd)"

PASS=0
FAIL=0
note() { echo "[schema] $*"; }

# Locate a JSON Schema validator.
VALIDATOR=""
VALIDATOR_KIND=""

# 1) Project .venv python with jsonschema (preferred — repo has this).
if [[ -x "$REPO_ROOT/.venv/bin/python3" ]] && "$REPO_ROOT/.venv/bin/python3" -c "import jsonschema" >/dev/null 2>&1; then
  VALIDATOR="$REPO_ROOT/.venv/bin/python3"
  VALIDATOR_KIND="python-venv"
# 2) System python3 + jsonschema.
elif command -v python3 >/dev/null 2>&1 && python3 -c "import jsonschema" >/dev/null 2>&1; then
  VALIDATOR="python3"
  VALIDATOR_KIND="python-system"
# 3) ajv-cli (Node tool).
elif command -v ajv >/dev/null 2>&1; then
  VALIDATOR_KIND="ajv"
fi

if [[ -z "$VALIDATOR_KIND" ]]; then
  note "(skip) no JSON Schema validator available"
  note "      install one to enable: pip install jsonschema, or npm i -g ajv-cli"
  exit 0
fi

note "using validator: $VALIDATOR_KIND ($VALIDATOR)"

validate_with_python() {
  local plugin="$1"
  local output
  output=$(bash -c ". '$plugin' && fetch_postings" 2>/dev/null) || {
    echo "[plugin] could not run fetch_postings"; return 1
  }
  echo "$output" | "$VALIDATOR" -c "
import sys, json, jsonschema
schema = json.load(open('$SCHEMA'))
data = json.load(sys.stdin)
jsonschema.validate(data, schema)
" >/dev/null 2>&1
}

validate_with_ajv() {
  local plugin="$1"
  local tmpfile
  tmpfile=$(mktemp)
  bash -c ". '$plugin' && fetch_postings" >"$tmpfile" 2>/dev/null || {
    rm -f "$tmpfile"; return 1
  }
  ajv validate -s "$SCHEMA" -d "$tmpfile" >/dev/null 2>&1
  local rc=$?
  rm -f "$tmpfile"
  return $rc
}

for plugin in "$SKILL_DIR"/sources/*.sh; do
  name=$(basename "$plugin" .sh)
  case "$VALIDATOR_KIND" in
    python-venv|python-system) validate_with_python "$plugin" ;;
    ajv)                       validate_with_ajv "$plugin" ;;
  esac
  if [[ $? -eq 0 ]]; then
    note "✓ $name validates"
    PASS=$((PASS+1))
  else
    note "✗ $name failed validation"
    FAIL=$((FAIL+1))
  fi
done

note ""
note "Result: $PASS pass, $FAIL fail"

if [[ "$FAIL" -gt 0 ]]; then
  exit 1
fi
exit 0
