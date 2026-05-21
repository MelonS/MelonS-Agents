#!/usr/bin/env bash
# audit-skill-drift.sh — detect drift between SKILL.md activation
# manifests (config/activation.tsv) and the actual gated scripts.
#
# A skill's manifest declares: "this LIVE flag gates this script,
# and the script optionally requires these env keys."  Drift
# happens when:
#
#   1. The script referenced by the manifest does not exist.
#   2. The manifest declares a flag but the gated script never
#      checks it (no `${FLAG:-0}` or similar test in the file).
#   3. The gated script tests a flag that is not declared in the
#      manifest (the inverse: hidden flag, not in dashboard).
#
# Findings are printed one per line in the format:
#
#   <severity>\t<skill>\t<flag-or-script>\t<one-line description>
#
# Severities: medium = missing-script / undeclared-flag,
# low = manifest-flag-not-checked-in-script (could be a doc-only
# entry, but flagged for review).
#
# Exit codes:
#   0  no drift
#   1  one or more findings printed to stdout
#   2  internal error (manifest unreadable, etc.)
#
# Usage:
#   bash scripts/audit-skill-drift.sh
#   bash scripts/audit-skill-drift.sh --quiet     # only summary line
#   bash scripts/audit-skill-drift.sh --json      # machine output

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

mode="full"
case "${1:-}" in
  --quiet) mode="quiet" ;;
  --json)  mode="json" ;;
  -h|--help)
    sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'
    exit 0
    ;;
esac

findings=()

# Locate every activation.tsv file under skills/<name>/config/
shopt -s nullglob
manifests=(skills/*/config/activation.tsv)
shopt -u nullglob

if (( ${#manifests[@]} == 0 )); then
  # No skills with activation manifests yet — not an error.
  if [[ "$mode" == "json" ]]; then
    printf '{"findings":[],"count":0,"skills_audited":0}\n'
  elif [[ "$mode" == "quiet" ]]; then
    echo "skill-drift: 0 findings (no manifests yet)"
  else
    echo "[skill-drift] no skills/*/config/activation.tsv files found — nothing to audit"
  fi
  exit 0
fi

for manifest in "${manifests[@]}"; do
  skill_dir="$(dirname "$(dirname "$manifest")")"
  skill_name="$(basename "$skill_dir")"

  # Pass 1 — for each declared flag, check the gated script exists
  # and tests the flag.
  declared_flags=()
  while IFS=$'\t' read -r flag path env_keys; do
    [[ -z "${flag:-}" ]] && continue
    [[ "${flag:0:1}" == "#" ]] && continue
    declared_flags+=("$flag")

    script_path="$skill_dir/$path"
    if [[ ! -f "$script_path" ]]; then
      findings+=("medium	$skill_name	$path	manifest references missing script")
      continue
    fi

    # Look for an actual test of the flag in the script.  Accept
    # ${FLAG:-0}, $FLAG, "$FLAG" — any literal mention of the var
    # in a conditional context.  Cheap regex: just look for the
    # bare token.
    if ! grep -q "${flag}" "$script_path"; then
      findings+=("low	$skill_name	$flag	declared in manifest but token not found in $path")
    fi
  done < "$manifest"

  # Pass 2 — for each script under skill_dir, look for tokens that
  # look like a LIVE flag (UPPERCASE_WITH_UNDERSCORES ending in
  # _LIVE) and check the manifest declares them.
  shopt -s nullglob
  for script in "$skill_dir"/scripts/*.sh "$skill_dir"/sources/*.sh; do
    [[ -f "$script" ]] || continue
    # extract candidate flag tokens
    while IFS= read -r token; do
      # is it declared?
      declared=0
      for f in "${declared_flags[@]}"; do
        if [[ "$f" == "$token" ]]; then
          declared=1
          break
        fi
      done
      if (( declared == 0 )); then
        rel="${script#$skill_dir/}"
        findings+=("medium	$skill_name	$token	flag checked in $rel but not declared in manifest")
      fi
    done < <(grep -oE '\b[A-Z][A-Z0-9_]*_LIVE\b' "$script" | sort -u)
  done
  shopt -u nullglob
done

# De-duplicate findings (a flag checked in multiple scripts only
# needs one finding per skill).  Use sort -u over a temp file so
# macOS bash 3.2 works (no `mapfile`, no associative arrays).
uniq_findings=()
if (( ${#findings[@]} > 0 )); then
  while IFS= read -r line; do
    uniq_findings+=("$line")
  done < <(printf '%s\n' "${findings[@]}" | sort -u)
fi

count=${#uniq_findings[@]}

case "$mode" in
  quiet)
    echo "skill-drift: $count findings (${#manifests[@]} manifest(s) audited)"
    ;;
  json)
    # naive JSON build; values are tab-separated, no embedded quotes
    printf '{"findings":['
    sep=''
    for f in "${uniq_findings[@]:-}"; do
      IFS=$'\t' read -r severity skill subject desc <<<"$f"
      printf '%s{"severity":"%s","skill":"%s","subject":"%s","description":"%s"}' \
        "$sep" "$severity" "$skill" "$subject" "$desc"
      sep=','
    done
    printf '],"count":%d,"skills_audited":%d}\n' "$count" "${#manifests[@]}"
    ;;
  full)
    cat <<EOF

skill-drift audit  (${#manifests[@]} manifest(s) under skills/*/config/activation.tsv)
─────────────────────────────────────────────────────────────

EOF
    if (( count == 0 )); then
      echo "no drift detected — manifests and gated scripts agree"
    else
      printf '%-8s  %-14s  %-30s  %s\n' "severity" "skill" "subject" "description"
      echo "──────── ─────────────  ──────────────────────────────  ──────────────────────────────"
      for f in "${uniq_findings[@]}"; do
        IFS=$'\t' read -r severity skill subject desc <<<"$f"
        printf '%-8s  %-14s  %-30s  %s\n' "$severity" "$skill" "$subject" "$desc"
      done
    fi
    cat <<EOF

─────────────────────────────────────────────────────────────
total findings: $count
EOF
    ;;
esac

if (( count > 0 )); then
  exit 1
fi
exit 0
