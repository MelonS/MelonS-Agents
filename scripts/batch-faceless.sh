#!/usr/bin/env bash
# Throttled batch runner for the faceless-short mission.
#
# Addresses Gemini's critique #3 (concurrency control): the prior
# ad-hoc /tmp batch scripts ran renders sequentially, which is safe
# but slow.  Naively backgrounding multiple `bash run.sh` calls would
# OOM an M2 — each mission peaks ffmpeg + whisper + ollama
# simultaneously and the inferred 100% concurrent peak is ~16 GB of
# working memory PLUS GPU pressure on Metal.
#
# This script lets you set the parallel count explicitly and enforces
# it with a job-count semaphore (`jobs -r | wc -l` polled every second
# — bash 3.2 compatible, no `wait -n` required).
#
# Usage:
#   scripts/batch-faceless.sh -f <topics_file> [--max-parallel N]
#   scripts/batch-faceless.sh <short_id> <topic> [<short_id> <topic> ...] [--max-parallel N]
#
# Topics file format (one entry per line, `|`-delimited):
#   <short_id>|<topic_string>|<voice>|<wlang>|[<script_override_path>]
#   # comments + blank lines OK
#
# Examples:
#   scripts/batch-faceless.sh -f /tmp/topics.txt
#   scripts/batch-faceless.sh -f /tmp/topics.txt --max-parallel 2
#   scripts/batch-faceless.sh hittites "The Hittites..." --max-parallel 1
#
# Defaults:
#   MAX_PARALLEL=1            # sequential is the safe default for an M2
#   FACELESS_VOICE=am_michael # Kokoro English voice
#   WHISPER_LANG=en
#
# Resource-pressure heuristic (operator-machine specific):
#   1 = always safe
#   2 = OK on M2 16GB+ if no other heavy GPU process running
#   3+ = high risk of OOM during the ffmpeg stitch stage
set -u

cd "$(dirname "${BASH_SOURCE[0]}")/.."
set -a; source .env; set +a

MAX_PARALLEL=1
TOPICS_FILE=""
INLINE_ARGS=()

# Parse args
while [[ $# -gt 0 ]]; do
  case "$1" in
    --max-parallel)
      MAX_PARALLEL="${2:-1}"; shift 2 ;;
    --max-parallel=*)
      MAX_PARALLEL="${1#*=}"; shift ;;
    -f)
      TOPICS_FILE="${2:-}"; shift 2 ;;
    -h|--help)
      sed -n '/^# Usage:/,/^set -u/p' "$0" | sed 's/^# \?//; /^set -u/d'
      exit 0 ;;
    --)
      shift; INLINE_ARGS+=("$@"); break ;;
    *)
      INLINE_ARGS+=("$1"); shift ;;
  esac
done

if (( MAX_PARALLEL < 1 )); then
  echo "ERROR: --max-parallel must be >= 1" >&2
  exit 64
fi
if (( MAX_PARALLEL >= 3 )); then
  echo "WARNING: --max-parallel=$MAX_PARALLEL is high-risk for an M2 16GB.  Ctrl+C to abort, or continue." >&2
  sleep 2
fi

# Build the run list as a single array of pipe-delimited entries.
ENTRIES=()
if [[ -n "$TOPICS_FILE" ]]; then
  [[ -f "$TOPICS_FILE" ]] || { echo "ERROR: topics file not found: $TOPICS_FILE" >&2; exit 64; }
  while IFS= read -r line; do
    line="${line%%#*}"
    line="$(echo "$line" | sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//')"
    [[ -n "$line" ]] && ENTRIES+=("$line")
  done < "$TOPICS_FILE"
fi
# Inline: short_id + topic pairs.
if (( ${#INLINE_ARGS[@]} > 0 )); then
  if (( ${#INLINE_ARGS[@]} % 2 != 0 )); then
    echo "ERROR: inline args must be (short_id, topic) pairs" >&2
    exit 64
  fi
  for ((i=0; i<${#INLINE_ARGS[@]}; i+=2)); do
    ENTRIES+=("${INLINE_ARGS[$i]}|${INLINE_ARGS[$((i+1))]}|am_michael|en|")
  done
fi

if (( ${#ENTRIES[@]} == 0 )); then
  echo "ERROR: no topics — pass -f <file> or inline (short_id topic) pairs" >&2
  exit 64
fi

LOG_DIR="records/batch-faceless"
mkdir -p "$LOG_DIR"
RUN_TS="$(date +%Y%m%d-%H%M%S)"
RUN_LOG="$LOG_DIR/run-$RUN_TS.log"
SUMMARY="$LOG_DIR/summary-$RUN_TS.tsv"
echo -e "idx\tshort_id\trc\telapsed_s\tstart\tend" > "$SUMMARY"

say() { printf '[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*" | tee -a "$RUN_LOG" >&2; }
say "batch-faceless start  entries=${#ENTRIES[@]}  max_parallel=$MAX_PARALLEL  log=$RUN_LOG"

# Per-job runner.
run_one() {
  local idx="$1" entry="$2"
  IFS='|' read -r short_id topic voice wlang script_override <<< "$entry"
  voice="${voice:-am_michael}"
  wlang="${wlang:-en}"
  local start=$(date +%s)
  local job_log="$LOG_DIR/job-$RUN_TS-$idx-$short_id.log"

  local override_env=""
  [[ -n "$script_override" && -f "$script_override" ]] && override_env="FACELESS_SCRIPT_OVERRIDE=$script_override"

  if env $override_env \
        FACELESS_VOICE="$voice" \
        WHISPER_LANG="$wlang" \
        bash agents/missions/faceless-short/run.sh "$short_id" "$topic" \
        > "$job_log" 2>&1; then
    local rc=0
  else
    local rc=$?
  fi
  local end=$(date +%s)
  local elapsed=$((end - start))
  printf '%s\t%s\t%s\t%s\t%s\t%s\n' \
    "$idx" "$short_id" "$rc" "$elapsed" "$(date -r $start +%H:%M:%S)" "$(date -r $end +%H:%M:%S)" \
    >> "$SUMMARY"
  if (( rc == 0 )); then
    say "[$idx/${#ENTRIES[@]}] $short_id  OK   elapsed=${elapsed}s  log=$job_log"
  else
    say "[$idx/${#ENTRIES[@]}] $short_id  FAIL rc=$rc  elapsed=${elapsed}s  log=$job_log"
  fi
}

# Semaphore loop: block until jobs-running count < MAX_PARALLEL, then
# dispatch the next entry in the background.  `jobs -r` lists running
# child jobs; the count goes down as children finish.
idx=0
for entry in "${ENTRIES[@]}"; do
  idx=$((idx + 1))
  while (( $(jobs -r | wc -l | tr -d ' ') >= MAX_PARALLEL )); do
    sleep 1
  done
  run_one "$idx" "$entry" &
done
wait
say "batch-faceless done  summary=$SUMMARY"

# Print summary table to stdout.
echo
echo "=== summary ==="
column -t -s $'\t' < "$SUMMARY"
