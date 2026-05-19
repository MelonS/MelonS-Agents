#!/usr/bin/env bash
# job-hunt skill — orchestrator.
#
# Pipeline:
#   1. Load filters.yaml (or --filters=<path>).
#   2. For each enabled source: dot-source sources/<name>.sh,
#      call fetch_postings (with JH_* env vars set), capture
#      normalized JSON to records/jobs/<date>/raw/<source>.json.
#   3. Aggregate all source JSONs into one structure.
#   4. Apply keyword include/exclude filter (post-fetch).
#   5. Deduplicate by posting URL across sources.
#   6. Diff against the most recent prior digest's JSON to flag
#      new-since postings.
#   7. Render markdown digest via scripts/digest.sh.
#   8. Write to records/jobs/<date>/digest.md + index.json.
#
# Exit codes:
#   0   — digest written successfully.
#   2   — config error (missing filters.yaml, unsupported locale,
#         missing required tool, malformed source plugin).
#   3   — all enabled sources failed.
#   4   — partial success (>=1 source succeeded, >=1 failed);
#         digest written with a "sources failed" note.
#
# Status (2026-05-20): wired end-to-end against mock source.
# Live source plugins (kr-wanted, kr-programmers) ship with mock
# fallbacks today; live HTTP integration is operator-validated.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Defaults.
FILTERS_PATH=""
SOURCES_OVERRIDE=""
OUTPUT_ROOT=""           # falls back to filter file's output.records_root
DRY_RUN=0
QUIET=0

usage() {
  cat <<EOF
job-hunt — Korean job-posting digest

Usage:
  scripts/run.sh [--filters=<path>] [--sources=<csv>] [--output-root=<dir>]
                 [--dry-run] [--quiet]

Options:
  --filters=<path>       Path to filters.yaml (default: skills/job-hunt/config/filters.yaml,
                         fallback to filters.example.yaml).
  --sources=<csv>        Override the enabled sources list, e.g. --sources=_mock,kr-wanted.
  --output-root=<dir>    Override the digest output root (default: filter file's
                         output.records_root, e.g. ./records/jobs).
  --dry-run              Run the full pipeline but write outputs under a /tmp scratch
                         directory instead of the operator's records/.
  --quiet                Suppress progress logging on stderr.
  --help                 Show this message.

Exit codes: 0 success, 2 config, 3 all-sources-failed, 4 partial.
EOF
}

log() {
  if [[ "$QUIET" != "1" ]]; then
    echo "[job-hunt] $*" >&2
  fi
}

die() {
  echo "[job-hunt] ERROR: $*" >&2
  exit "${2:-2}"
}

# ----- arg parsing -----
for arg in "$@"; do
  case "$arg" in
    --filters=*)      FILTERS_PATH="${arg#--filters=}" ;;
    --sources=*)      SOURCES_OVERRIDE="${arg#--sources=}" ;;
    --output-root=*)  OUTPUT_ROOT="${arg#--output-root=}" ;;
    --dry-run)        DRY_RUN=1 ;;
    --quiet)          QUIET=1 ;;
    --help|-h)        usage; exit 0 ;;
    *)
      die "unknown arg: $arg" 2
      ;;
  esac
done

# ----- required tools -----
for tool in jq; do
  command -v "$tool" >/dev/null 2>&1 || die "$tool required but not found on PATH" 2
done

# ----- locate yaml parser -----
yaml_get() {
  # $1 = yaml file, $2 = jq-style path on the resulting JSON
  # (e.g. ".locale", ".sources[]", ".job_categories | join(\",\")")
  local file="$1" path="$2"
  if command -v yq >/dev/null 2>&1; then
    yq -o=json "$file" | jq -r "$path"
  elif command -v ruby >/dev/null 2>&1; then
    ruby -ryaml -rjson -e "puts YAML.load_file(ARGV[0]).to_json" "$file" | jq -r "$path"
  elif command -v python3 >/dev/null 2>&1 && python3 -c "import yaml" >/dev/null 2>&1; then
    python3 -c "import sys, yaml, json; print(json.dumps(yaml.safe_load(open(sys.argv[1]))))" "$file" | jq -r "$path"
  else
    die "no YAML parser available — install yq, or ensure ruby or python3+pyyaml" 2
  fi
}

# ----- locate filter file -----
if [[ -z "$FILTERS_PATH" ]]; then
  if [[ -f "$SKILL_DIR/config/filters.yaml" ]]; then
    FILTERS_PATH="$SKILL_DIR/config/filters.yaml"
  elif [[ -f "$SKILL_DIR/config/filters.example.yaml" ]]; then
    FILTERS_PATH="$SKILL_DIR/config/filters.example.yaml"
    log "no filters.yaml — falling back to filters.example.yaml"
  else
    die "no filter file found at $SKILL_DIR/config/filters.yaml" 2
  fi
fi
[[ -f "$FILTERS_PATH" ]] || die "filter file not found: $FILTERS_PATH" 2

# ----- read filter config -----
locale=$(yaml_get "$FILTERS_PATH" '.locale')
[[ "$locale" == "kr" ]] || die "locale '$locale' not implemented — see SKILL.md 'Adding a locale'" 2

# bash 3.2 portable array fill (no `readarray` / `mapfile`).  Must
# use process substitution (`< <(...)`) not pipes — pipes spawn a
# subshell and the array fill wouldn't survive back to the parent.
slurp_into() {
  # $1 = array variable name; reads from stdin.
  local _var="$1"
  eval "$_var=()"
  local _line
  while IFS= read -r _line; do
    [[ -z "$_line" ]] && continue
    eval "$_var+=(\"\$_line\")"
  done
}

enabled_sources=()
slurp_into enabled_sources < <(yaml_get "$FILTERS_PATH" '.sources[]')
if [[ -n "$SOURCES_OVERRIDE" ]]; then
  IFS=',' read -ra enabled_sources <<<"$SOURCES_OVERRIDE"
fi
(( ${#enabled_sources[@]} > 0 )) || die "no sources enabled" 2

regions=();    slurp_into regions    < <(yaml_get "$FILTERS_PATH" '.regions[]')
categories=(); slurp_into categories < <(yaml_get "$FILTERS_PATH" '.job_categories[]')
kw_include=(); slurp_into kw_include < <(yaml_get "$FILTERS_PATH" '.keywords.include[]' 2>/dev/null || true)
kw_exclude=(); slurp_into kw_exclude < <(yaml_get "$FILTERS_PATH" '.keywords.exclude[]' 2>/dev/null || true)

records_root=$(yaml_get "$FILTERS_PATH" '.output.records_root // "./records/jobs"')
if [[ -n "$OUTPUT_ROOT" ]]; then
  records_root="$OUTPUT_ROOT"
fi
if [[ "$DRY_RUN" == "1" ]]; then
  records_root="$(mktemp -d)/jobs"
  log "dry-run: writing to $records_root"
fi

# Filter summary one-liner for the digest header.
# Use ${arr[*]:-} guards because bash 3.2 treats empty arrays as
# unset under `set -u`.
filter_summary="직군: $(IFS=', '; echo "${categories[*]:-}") · 지역: $(IFS=', '; echo "${regions[*]:-}") · include=[$(IFS=','; echo "${kw_include[*]:-}")] exclude=[$(IFS=','; echo "${kw_exclude[*]:-}")]"

# ----- prepare output dir -----
today=$(date +%F)
out_dir="$records_root/$today"
raw_dir="$out_dir/raw"
mkdir -p "$raw_dir"

# Export filter context for source plugins.
export JH_REGIONS="$(IFS=$'\n'; echo "${regions[*]:-}")"
export JH_CATEGORIES="$(IFS=$'\n'; echo "${categories[*]:-}")"
export JH_KEYWORDS_INCLUDE="$(IFS=$'\n'; echo "${kw_include[*]:-}")"
export JH_KEYWORDS_EXCLUDE="$(IFS=$'\n'; echo "${kw_exclude[*]:-}")"

# ----- run each source -----
sources_succeeded=()
sources_failed=()
all_postings_jq_input='[]'

for src in "${enabled_sources[@]}"; do
  plugin="$SKILL_DIR/sources/${src}.sh"
  if [[ ! -f "$plugin" ]]; then
    log "source '$src' plugin not found at $plugin — skipping"
    sources_failed+=("$src")
    continue
  fi

  log "fetching: $src"
  # Run plugin in a subshell so fetch_postings redefinition doesn't
  # leak between sources.
  raw_json=$(
    (
      # shellcheck source=/dev/null
      . "$plugin"
      type fetch_postings >/dev/null 2>&1 || { echo "[plugin $src] missing fetch_postings" >&2; exit 1; }
      fetch_postings
    )
  ) || {
    log "source '$src' failed"
    sources_failed+=("$src")
    continue
  }

  # Validate JSON shape.
  if ! echo "$raw_json" | jq -e '.source and .postings and (.postings | type == "array")' >/dev/null 2>&1; then
    log "source '$src' returned malformed JSON — skipping"
    sources_failed+=("$src")
    continue
  fi

  # Persist raw.
  echo "$raw_json" | jq '.' >"$raw_dir/${src}.json"
  sources_succeeded+=("$src")

  # Accumulate.
  all_postings_jq_input=$(jq -n \
    --argjson acc "$all_postings_jq_input" \
    --argjson raw "$raw_json" \
    '$acc + ($raw.postings | map(. + {source: $raw.source}))')
done

# ----- failure modes -----
if (( ${#sources_succeeded[@]} == 0 )); then
  die "all enabled sources failed: ${sources_failed[*]:-none}" 3
fi

# ----- apply keyword filter -----
# include: at least one of kw_include must appear in title or summary
# exclude: any of kw_exclude in title or summary drops the posting
kw_include_json=$(printf '%s\n' "${kw_include[@]:-}" | jq -R . | jq -s 'map(select(length>0))')
kw_exclude_json=$(printf '%s\n' "${kw_exclude[@]:-}" | jq -R . | jq -s 'map(select(length>0))')

filtered_postings=$(echo "$all_postings_jq_input" | jq \
  --argjson include "$kw_include_json" \
  --argjson exclude "$kw_exclude_json" '
  map(
    . as $p |
    (($p.title // "") + " " + ($p.summary // "")) as $blob |
    select(
      ($include | length == 0 or any(.[]; . as $w | $blob | contains($w))) and
      ($exclude | length == 0 or all(.[]; . as $w | ($blob | contains($w) | not)))
    )
  )
')

# ----- dedupe by URL (keep first occurrence) -----
deduped=$(echo "$filtered_postings" | jq '
  reduce .[] as $p ({seen:{}, out:[]};
    if (.seen[$p.url] // false) then .
    else .seen[$p.url] = true | .out += [$p] end
  ) | .out
')

# ----- diff against most recent prior digest -----
prior_index=""
if [[ -d "$records_root" ]]; then
  prior_index=$(find "$records_root" -mindepth 2 -maxdepth 2 -name index.json -not -path "*/$today/*" 2>/dev/null | sort | tail -n1 || true)
fi

new_urls_json='[]'
if [[ -n "$prior_index" && -f "$prior_index" ]]; then
  log "diffing against prior digest: $prior_index"
  new_urls_json=$(jq -n \
    --slurpfile prev "$prior_index" \
    --argjson curr "$deduped" '
    ($prev[0].postings // []) as $prev_postings |
    ($prev_postings | map(.url)) as $prev_urls |
    $curr | map(select(.url as $u | $prev_urls | index($u) | not)) | map(.url)
  ')
fi

postings_total=$(echo "$deduped" | jq 'length')
postings_new=$(echo "$new_urls_json" | jq 'length')

# Re-group by source.
by_source_json=$(echo "$deduped" | jq '
  group_by(.source) | map({(.[0].source): map(del(.source))}) | add // {}
')

# ----- build aggregated index -----
generated_at=$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)
index_json=$(jq -n \
  --arg gen "$generated_at" \
  --arg loc "$locale" \
  --argjson srcs "$(printf '%s\n' "${sources_succeeded[@]}" | jq -R . | jq -s .)" \
  --arg fsum "$filter_summary" \
  --argjson total "$postings_total" \
  --argjson new "$postings_new" \
  --argjson bysrc "$by_source_json" \
  --argjson newurls "$new_urls_json" \
  --argjson postings "$deduped" '
  {
    generated_at: $gen,
    locale: $loc,
    sources: $srcs,
    filter_summary: $fsum,
    postings_total: $total,
    postings_new: $new,
    by_source: $bysrc,
    new_urls: $newurls,
    postings: $postings
  }')

# Persist index for next-run diffing.
echo "$index_json" >"$out_dir/index.json"

# ----- render digest -----
echo "$index_json" | "$SCRIPT_DIR/digest.sh" >"$out_dir/digest.md"

log "wrote: $out_dir/digest.md"
log "      $out_dir/index.json"
log "      $out_dir/raw/*.json"
log "sources succeeded: ${sources_succeeded[*]}"
if (( ${#sources_failed[@]} > 0 )); then
  log "sources failed: ${sources_failed[*]}"
  exit 4
fi

# Final summary on stdout (parseable).
echo "$out_dir/digest.md"
