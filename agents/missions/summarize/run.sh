#!/usr/bin/env bash
# Summarize mission: long-form video → bilingual structured summary.
set -euo pipefail

# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/env.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/log.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/ollama.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/whisper.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/ffmpeg.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/attribution.sh"
# shellcheck disable=SC1091
source "$(dirname "${BASH_SOURCE[0]}")/../../lib/retry.sh"

SOURCE="${1:-}"
[[ -z "$SOURCE" ]] && { log_err "usage: $0 <url_or_path>"; exit 64; }

require_bin "$FFMPEG_BIN" "$WHISPER_CLI_BIN" "$YT_DLP_BIN" jq
require_env OLLAMA_HOST OLLAMA_MODEL_HIGHLIGHT WHISPER_MODEL RECORDS_DIR

MISSION_ID="summarize-$(date +%H%M%S)"
MDIR="$RECORDS_DIR/missions/$(date +%Y-%m-%d)/$MISSION_ID"
mkdir -p "$MDIR/resources" "$MDIR/outputs"
log_step "mission: $MDIR"

cat > "$MDIR/plan.md" <<MDPLAN
# Plan: summarize $SOURCE

## Goal
Produce a structured bilingual summary (TL;DR + key points + original-language and mirror-language summaries).

## Acceptance criteria
- [ ] outputs/summary.md exists with required sections
MDPLAN

# 1. Source
SRC="$MDIR/resources/source.mp4"
if [[ -f "$SOURCE" ]]; then
  cp "$SOURCE" "$SRC"
else
  "$YT_DLP_BIN" -f "best[ext=mp4]/best" -o "$SRC" "$SOURCE" >&2
fi
log_ok "source ready: $SRC"

# Resolve source attribution + license, then record alongside the summary.
# Summarize doesn't render video, so there's no burned-in watermark — only
# the SOURCES.txt machine record + a footer in summary.md.
resolve_source_attribution "$SOURCE"
write_sources_record "$MISSION_ID" "$SOURCE" "$MDIR/outputs/SOURCES.txt"

# 2. Transcribe
TRANSCRIPT=$(whisper_transcribe "$SRC" "$MDIR/resources/transcript")
log_ok "transcript: $TRANSCRIPT"

# Build a plain-text transcript for the LLM
FULLTEXT="$MDIR/resources/transcript.txt"
jq -r '[.transcription[].text] | join(" ")' "$TRANSCRIPT" \
  | sed -E 's/  +/ /g' > "$FULLTEXT"
WC=$(wc -w < "$FULLTEXT" | tr -d ' ')
log_info "transcript words: $WC"

# 3-4: summarize + QA wrapped in a retry loop.  On FAIL, the next attempt
# re-prompts the model with the previous qa-report inlined so it knows
# which acceptance criteria to fix (missing TL;DR section, too few key
# points, etc.).  Cap is QA_RETRY_MAX (default 2 retries).
ollama_ensure_model "$OLLAMA_MODEL_HIGHLIGHT"
PROMPT_FILE="$(dirname "${BASH_SOURCE[0]}")/summarize.prompt.md"
SUMMARY_MD="$MDIR/outputs/summary.md"

VERDICT=""
QA_FEEDBACK=""
ATTEMPT=0
MAX_ATTEMPTS=$((QA_RETRY_MAX + 1))

while (( ATTEMPT < MAX_ATTEMPTS )); do
  ATTEMPT=$((ATTEMPT + 1))
  if (( ATTEMPT > 1 )); then
    log_warn "attempt $ATTEMPT/$MAX_ATTEMPTS — previous QA FAILED, retrying with feedback"
    QA_FEEDBACK=$(qa_extract_feedback "$MDIR/qa-report.md")
  fi

  PROMPT="$(cat "$PROMPT_FILE")$(qa_feedback_block)

TRANSCRIPT:
$(cat "$FULLTEXT")"

  SUMMARY_RAW=$(ollama_generate "$OLLAMA_MODEL_HIGHLIGHT" "$PROMPT" false)
  echo "$SUMMARY_RAW" | awk '!/^[[:space:]]*```/' | awk 'BEGIN{blank=0} /^[[:space:]]*$/ { if (!blank) print; blank=1; next } { print; blank=0 }' > "$SUMMARY_MD"

  # QA — runs against the raw summary body before the footer is appended,
  # so footer text doesn't accidentally count toward Key-points bullets.
  TL_OK=$(grep -q "^# TL;DR" "$SUMMARY_MD" && echo PASS || echo FAIL)
  KP_OK=$(grep -q "^# Key points" "$SUMMARY_MD" && echo PASS || echo FAIL)
  BUL_COUNT=$(awk '/^# Key points/{f=1; next} /^# /{f=0} f && /^- /{n++} END{print n+0}' "$SUMMARY_MD")
  if (( BUL_COUNT >= 3 )); then KP_N_VERDICT=PASS; else KP_N_VERDICT=FAIL; fi
  SIZE_B=$(stat -f%z "$SUMMARY_MD" 2>/dev/null || stat -c%s "$SUMMARY_MD")
  SIZE_OK=$(awk -v s="$SIZE_B" 'BEGIN{print (s < 51200) ? "PASS" : "FAIL"}')

  VERDICT=PASS
  for v in "$TL_OK" "$KP_OK" "$KP_N_VERDICT" "$SIZE_OK"; do
    [[ "$v" == "FAIL" ]] && VERDICT=FAIL
  done

  cat > "$MDIR/qa-report.md" <<MDQA
# QA report — $MISSION_ID (attempt $ATTEMPT of $MAX_ATTEMPTS)

**Verdict**: $VERDICT

## Acceptance criteria
- [$([ "$TL_OK" = PASS ] && echo x || echo ' ')] TL;DR section present
- [$([ "$KP_OK" = PASS ] && echo x || echo ' ')] Key points section present
- [$([ "$KP_N_VERDICT" = PASS ] && echo x || echo ' ')] At least 3 bullets ($BUL_COUNT found)
- [$([ "$SIZE_OK" = PASS ] && echo x || echo ' ')] Size under 50 KB ($SIZE_B bytes)

## Stats
- source words: $WC
- summary bytes: $SIZE_B
MDQA

  [[ "$VERDICT" == PASS ]] && break
done

# Append source-attribution footer so a summary read in isolation still
# credits the original — mirrors the burned-in watermark on rendered shorts.
cat >> "$SUMMARY_MD" <<MDFOOT

---

## Source & license

- source: \`$SOURCE\`
- attribution: $SOURCE_ATTRIBUTION
- license: ${FIXTURE_LICENSE:-unknown}
- record: \`outputs/SOURCES.txt\`
MDFOOT
log_ok "summary written"

jq -n --arg v "$VERDICT" --argjson w "$WC" --argjson b "$SIZE_B" '{verdict: $v, transcript_words: $w, summary_bytes: $b, mission_type: "summarize"}' > "$MDIR/metrics.json"
cat > "$MDIR/summary.md" <<MDS
# Summary — $MISSION_ID

- source: $SOURCE
- transcript words: $WC
- summary: $SUMMARY_MD
- verdict: $VERDICT
MDS

if [[ "$VERDICT" == "PASS" ]]; then
  log_ok "mission $MISSION_ID PASS on attempt $ATTEMPT"
else
  BLOCKER=$(qa_write_blocker "$MISSION_ID" "$MDIR" "$ATTEMPT")
  log_err "mission $MISSION_ID FAIL after $ATTEMPT attempts — blocker: $BLOCKER"
  exit 1
fi
