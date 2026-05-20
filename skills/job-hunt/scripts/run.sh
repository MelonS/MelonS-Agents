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
SEED=""                  # primary UX entry — short keyword expanded via role-synonyms.yaml
DRY_RUN=0
QUIET=0
FIT_SCORE=0              # --fit-score flag (Phase 2.3) — invokes fit-score.sh per posting

usage() {
  cat <<EOF
job-hunt — Korean job-posting digest

Usage (primary — short keyword, skill expands the rest):
  scripts/run.sh --seed "Problem Solver"
  scripts/run.sh --seed "Forward Deployed"
  scripts/run.sh --seed "AI agent builder"

Usage (advanced — full filters.yaml control):
  scripts/run.sh [--filters=<path>] [--sources=<csv>] [--output-root=<dir>]
                 [--dry-run] [--quiet]
  scripts/run.sh --list-sources

Options:
  --seed=<phrase>        Short keyword — matched against config/role-synonyms.yaml
                         to expand into the full set of equivalent titles used by
                         different companies (e.g. Problem Solver → also FDE,
                         Applied AI Engineer, AI Product Manager, Generalist, etc.).
                         When set, OVERRIDES filters.yaml include keywords.
  --filters=<path>       Path to filters.yaml (default: skills/job-hunt/config/filters.yaml,
                         fallback to filters.example.yaml).
  --sources=<csv>        Override the enabled sources list, e.g. --sources=_mock,kr-wanted.
  --output-root=<dir>    Override the digest output root (default: filter file's
                         output.records_root, e.g. ./records/jobs).
  --dry-run              Run the full pipeline but write outputs under a /tmp scratch
                         directory instead of the operator's records/.
  --quiet                Suppress progress logging on stderr.
  --fit-score            Invoke scripts/fit-score.sh per matched posting (Phase 2.3).
                         Adds a `fit` object to each posting in index.json + a fit
                         line to the rendered digest.  Defaults to scaffold mode
                         (no Claude call); set JH_FIT_SCORE_LIVE=1 for live scoring.
  --list-sources         Print available source plugins + their live-mode flag status.
  --help                 Show this message.

Exit codes: 0 success, 2 config, 3 all-sources-failed, 4 partial.
EOF
}

list_sources() {
  # Enumerate every sources/*.sh plugin and report whether its live
  # flag is currently set (env-detected, not file-parsed).
  local sources_dir="$SKILL_DIR/sources"
  if [[ ! -d "$sources_dir" ]]; then
    echo "[list-sources] no sources directory at $sources_dir" >&2
    return 2
  fi
  printf '%-22s %-12s %s\n' "PLUGIN" "MODE" "LIVE-FLAG VAR"
  printf '%-22s %-12s %s\n' "----------------------" "------------" "---------------------------"
  for f in "$sources_dir"/*.sh; do
    [[ -f "$f" ]] || continue
    local name; name=$(basename "$f" .sh)
    local flag_var=""
    local mode=""
    case "$name" in
      _mock)           flag_var="(none — always mock)";   mode="mock" ;;
      kr-wanted)       flag_var="JH_WANTED_LIVE";         [[ "${JH_WANTED_LIVE:-0}"      == "1" ]] && mode="LIVE" || mode="mock" ;;
      kr-programmers)  flag_var="JH_PROGRAMMERS_LIVE";    [[ "${JH_PROGRAMMERS_LIVE:-0}" == "1" ]] && mode="LIVE" || mode="mock" ;;
      kr-jobkorea)     flag_var="JH_JOBKOREA_LIVE";       [[ "${JH_JOBKOREA_LIVE:-0}"    == "1" ]] && mode="LIVE" || mode="mock" ;;
      kr-saramin)      flag_var="JH_SARAMIN_LIVE";        [[ "${JH_SARAMIN_LIVE:-0}"     == "1" ]] && mode="LIVE" || mode="mock" ;;
      kr-worknet)      flag_var="JH_WORKNET_LIVE";        [[ "${JH_WORKNET_LIVE:-0}"     == "1" ]] && mode="LIVE" || mode="mock" ;;
      global-ats)      flag_var="JH_GLOBAL_ATS_LIVE";       [[ "${JH_GLOBAL_ATS_LIVE:-0}"      == "1" ]] && mode="LIVE" || mode="mock" ;;
      global-remoteok) flag_var="JH_GLOBAL_REMOTEOK_LIVE";  [[ "${JH_GLOBAL_REMOTEOK_LIVE:-0}" == "1" ]] && mode="LIVE" || mode="mock" ;;
      global-remotive) flag_var="JH_GLOBAL_REMOTIVE_LIVE";  [[ "${JH_GLOBAL_REMOTIVE_LIVE:-0}" == "1" ]] && mode="LIVE" || mode="mock" ;;
      global-hn-whoshiring) flag_var="JH_GLOBAL_HN_LIVE";   [[ "${JH_GLOBAL_HN_LIVE:-0}"      == "1" ]] && mode="LIVE" || mode="mock" ;;
      *)               flag_var="(unknown — check plugin)"; mode="?" ;;
    esac
    printf '%-22s %-12s %s\n' "$name" "$mode" "$flag_var"
  done
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
# Arg parsing accepts both `--key=value` and `--key value` forms.
while [[ $# -gt 0 ]]; do
  case "$1" in
    --seed=*)         SEED="${1#--seed=}"; shift ;;
    --seed)           SEED="${2:-}"; shift 2 ;;
    --filters=*)      FILTERS_PATH="${1#--filters=}"; shift ;;
    --filters)        FILTERS_PATH="${2:-}"; shift 2 ;;
    --sources=*)      SOURCES_OVERRIDE="${1#--sources=}"; shift ;;
    --sources)        SOURCES_OVERRIDE="${2:-}"; shift 2 ;;
    --output-root=*)  OUTPUT_ROOT="${1#--output-root=}"; shift ;;
    --output-root)    OUTPUT_ROOT="${2:-}"; shift 2 ;;
    --dry-run)        DRY_RUN=1; shift ;;
    --quiet)          QUIET=1; shift ;;
    --fit-score)      FIT_SCORE=1; shift ;;
    --list-sources)
      list_sources
      exit 0
      ;;
    --help|-h)        usage; exit 0 ;;
    *)
      die "unknown arg: $1" 2
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
# `kr-*` source plugins are the only fully-implemented locale today.
# `global-*` plugins (ATS aggregators, remote-job APIs, HN Who's Hiring)
# are locale-agnostic and ship alongside the kr stack — they work
# regardless of the `locale:` field, which still describes the
# operator's primary geography.
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

# ----- seed expansion (Phase 2.1) -----
# If --seed was given, look up the seed in role-synonyms.yaml.
# When a family contains the seed (case-insensitive substring on
# any synonym), expand the include keywords to that family's
# full synonym list, REPLACING any kw_include from filters.yaml.
# Rationale: --seed is the primary v2 UX (short keyword in,
# skill expands the rest); falling back to a hand-edited filter
# is the advanced path.
SYNONYMS_PATH="$SKILL_DIR/config/role-synonyms.yaml"
SEED_FAMILY=""
SEED_FAMILY_CANONICAL=""

if [[ -n "$SEED" ]]; then
  if [[ ! -f "$SYNONYMS_PATH" ]]; then
    die "--seed used but role-synonyms.yaml not found at $SYNONYMS_PATH" 2
  fi
  # Build a lowercase-needle.
  seed_lc=$(echo "$SEED" | tr '[:upper:]' '[:lower:]')
  # Enumerate family keys.
  family_keys=()
  slurp_into family_keys < <(yaml_get "$SYNONYMS_PATH" 'keys | .[]' 2>/dev/null)
  matched_family=""
  for fam in "${family_keys[@]:-}"; do
    fam_synonyms=()
    slurp_into fam_synonyms < <(yaml_get "$SYNONYMS_PATH" ".\"$fam\".synonyms[]" 2>/dev/null || true)
    for syn in "${fam_synonyms[@]:-}"; do
      syn_lc=$(echo "$syn" | tr '[:upper:]' '[:lower:]')
      if [[ "$syn_lc" == *"$seed_lc"* || "$seed_lc" == *"$syn_lc"* ]]; then
        matched_family="$fam"
        break 2
      fi
    done
  done
  if [[ -z "$matched_family" ]]; then
    die "seed '$SEED' did not match any role family in $SYNONYMS_PATH" 2
  fi
  SEED_FAMILY="$matched_family"
  SEED_FAMILY_CANONICAL=$(yaml_get "$SYNONYMS_PATH" ".\"$matched_family\".canonical" 2>/dev/null)
  # Override kw_include with the family's full synonym set.
  kw_include=()
  slurp_into kw_include < <(yaml_get "$SYNONYMS_PATH" ".\"$matched_family\".synonyms[]" 2>/dev/null)
  log "seed '$SEED' → family '$matched_family' (canonical: $SEED_FAMILY_CANONICAL)"
  log "expanded to ${#kw_include[@]} include keywords"
fi

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

  # Accumulate.  Large source payloads (ATS aggregators can return
  # 5k+ postings = several MB) exceed the OS argv limit when passed
  # via --argjson; use --slurpfile to stream from disk instead.
  acc_tmp=$(mktemp -t jh-acc.XXXXXX) || die "mktemp failed" 2
  printf '%s' "$all_postings_jq_input" > "$acc_tmp"
  all_postings_jq_input=$(jq \
    --slurpfile acc "$acc_tmp" \
    '. as $f | ($acc[0]) + ($f.postings | map(. + {source: $f.source}))' \
    "$raw_dir/${src}.json")
  rm -f "$acc_tmp"
done

# ----- failure modes -----
if (( ${#sources_succeeded[@]} == 0 )); then
  die "all enabled sources failed: ${sources_failed[*]:-none}" 3
fi

# ----- apply keyword + region filter -----
# include keywords: at least one must appear in title or summary
# exclude keywords: any in title or summary drops the posting
# regions (optional): at least one region token must substring-match
#   the posting region; "원격" / "remote" / "재택" all map to a regex
#   group so an English-region posting passes a Korean "원격" filter.
#   When the regions list is empty, no region filter is applied.
kw_include_json=$(printf '%s\n' "${kw_include[@]:-}" | jq -R . | jq -s 'map(select(length>0))')
kw_exclude_json=$(printf '%s\n' "${kw_exclude[@]:-}" | jq -R . | jq -s 'map(select(length>0))')
regions_json=$(printf '%s\n' "${regions[@]:-}" | jq -R . | jq -s 'map(select(length>0))')

filtered_postings=$(echo "$all_postings_jq_input" | jq \
  --argjson include "$kw_include_json" \
  --argjson exclude "$kw_exclude_json" \
  --argjson regions "$regions_json" '
  # Region tokens are expanded to include common synonym translations
  # so "원격" matches "Remote" / "remote" / "재택" and vice versa,
  # and the major KR cities map to their English names so an
  # English-region posting ("Seoul, South Korea") passes a Korean
  # "서울" filter.
  ($regions | map(
    . as $rt |
    if   ($rt | test("원격|remote|재택"; "i")) then ["원격", "재택", "Remote", "remote"]
    elif ($rt | contains("서울"))            then ["서울", "Seoul"]
    elif ($rt | contains("부산"))            then ["부산", "Busan"]
    elif ($rt | contains("인천"))            then ["인천", "Incheon"]
    elif ($rt | contains("대구"))            then ["대구", "Daegu"]
    elif ($rt | contains("대전"))            then ["대전", "Daejeon"]
    elif ($rt | contains("광주"))            then ["광주", "Gwangju"]
    elif ($rt | contains("울산"))            then ["울산", "Ulsan"]
    elif ($rt | contains("세종"))            then ["세종", "Sejong"]
    elif ($rt | contains("경기"))            then ["경기", "Gyeonggi"]
    elif ($rt | contains("성남"))            then ["성남", "Seongnam"]
    elif ($rt | contains("판교"))            then ["판교", "Pangyo"]
    elif ($rt | contains("강남"))            then ["강남", "Gangnam"]
    elif ($rt | contains("한국") or ($rt | contains("Korea")))
                                              then ["한국", "Korea"]
    else [$rt]
    end
  ) | flatten | unique) as $region_pool |
  map(
    . as $p |
    (($p.title // "") + " " + ($p.summary // "")) as $blob |
    ($p.region // "") as $region_field |
    select(
      ($include | length == 0 or any(.[]; . as $w | $blob | contains($w))) and
      ($exclude | length == 0 or all(.[]; . as $w | ($blob | contains($w) | not))) and
      ($region_pool | length == 0 or any(.[]; . as $rt | $region_field | contains($rt)))
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

# ----- fit-score per posting (Phase 2.3, opt-in via --fit-score) -----
if [[ "$FIT_SCORE" == "1" ]]; then
  fit_live="${JH_FIT_SCORE_LIVE:-0}"
  if [[ "$fit_live" == "1" ]]; then
    log "fit-score: live mode (per-posting Claude call)"
  else
    log "fit-score: scaffold mode (no Claude call; preview only)"
  fi

  posting_count=$(echo "$deduped" | jq 'length')
  scored=$(echo "$deduped" | jq -c '.[]' | (
    new_array='[]'
    while IFS= read -r posting; do
      # Capture stdout + rc separately.  Don't use `|| true` here —
      # that would mask the non-zero exit from scaffold mode (10) or
      # error (2/3), and we need to dispatch on rc.
      fit_out=$(echo "$posting" | "$SCRIPT_DIR/fit-score.sh" 2>/dev/null) && fit_rc=0 || fit_rc=$?
      if [[ "$fit_live" == "1" && "$fit_rc" == "0" ]]; then
        new_array=$(jq -n --argjson acc "$new_array" --argjson p "$posting" --argjson f "$fit_out" \
          '$acc + [($p + {fit: $f})]')
      elif [[ "$fit_live" != "1" && "$fit_rc" == "10" ]]; then
        # Scaffold mode: attach a minimal fit stub indicating
        # scaffold-mode-was-on so the digest can render a
        # "scoring scaffolded, flip JH_FIT_SCORE_LIVE=1" hint.
        new_array=$(jq -n --argjson acc "$new_array" --argjson p "$posting" \
          '$acc + [($p + {fit: {scaffold_mode: true}})]')
      else
        # Either live call failed (rc=3) or unexpected; carry posting
        # through without fit data and log.
        echo "[job-hunt] fit-score returned $fit_rc for posting $(echo "$posting" | jq -r '.url')" >&2
        new_array=$(jq -n --argjson acc "$new_array" --argjson p "$posting" \
          '$acc + [$p]')
      fi
    done
    echo "$new_array"
  ))
  deduped="$scored"
  log "fit-score: completed for $posting_count posting(s)"
fi

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
  --argjson postings "$deduped" \
  --arg seed_input "$SEED" \
  --arg seed_family "$SEED_FAMILY" \
  --arg seed_canonical "$SEED_FAMILY_CANONICAL" \
  --argjson seed_synonym_count "${#kw_include[@]}" '
  {
    generated_at: $gen,
    locale: $loc,
    sources: $srcs,
    filter_summary: $fsum,
    postings_total: $total,
    postings_new: $new,
    by_source: $bysrc,
    new_urls: $newurls,
    postings: $postings,
    seed: (if $seed_input != "" then {
      input: $seed_input,
      family: $seed_family,
      canonical: $seed_canonical,
      synonym_count: $seed_synonym_count
    } else null end)
  }')

# Persist index for next-run diffing.
echo "$index_json" >"$out_dir/index.json"

# ----- render digest -----
echo "$index_json" | "$SCRIPT_DIR/digest.sh" >"$out_dir/digest.md"

log "wrote: $out_dir/digest.md"
log "      $out_dir/index.json"
log "      $out_dir/raw/*.json"
log "sources succeeded: ${sources_succeeded[*]}"

# Final summary on stdout (parseable).  Print BEFORE the
# partial-failure exit so callers capture the digest path even
# when exit code is 4.
echo "$out_dir/digest.md"

if (( ${#sources_failed[@]} > 0 )); then
  log "sources failed: ${sources_failed[*]}"
  exit 4
fi
