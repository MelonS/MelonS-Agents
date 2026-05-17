#!/usr/bin/env bash
# Generate a faceless-short narration script via Claude (Sonnet).
#
# Why this exists alongside the llama3.2:3b path in
# `agents/missions/faceless-short/run.sh`:
# The 3B local model has a quality ceiling on shorts narration — it
# writes encyclopedia-style prose, doesn't construct a strong 5-second
# hook, and conflates close-but-distinct facts (e.g., hydrogen body %
# by mass vs by atom count).  When the operator needs script quality
# at the level required for a real niche-decision A/B, this script
# routes the script-generation step (and only that step) to Sonnet
# via the `claude` CLI.  No new paid resource — runs against the
# existing Max-plan subscription quota.
#
# Output gets piped through `FACELESS_SCRIPT_OVERRIDE` into the
# existing run.sh, so all downstream stages (TTS, whisper, caption
# correction, B-roll, ffmpeg) stay $0 / local / unchanged.
#
# Usage:
#   scripts/gen-script-claude.sh <topic-id> "<topic-prompt>" <lang> <out-file>
# Example:
#   scripts/gen-script-claude.sh hittites \
#     "The Hittites — a biblical kingdom dismissed by historians as legend until 1906" \
#     en docs/pilots/sonnet-trial/hittites-en-script.txt
set -euo pipefail

TOPIC_ID="${1:-}"
TOPIC="${2:-}"
LANG="${3:-en}"
OUT="${4:-}"
if [[ -z "$TOPIC_ID" || -z "$TOPIC" || -z "$OUT" ]]; then
  echo "usage: $0 <topic-id> \"<topic-prompt>\" <lang:en|ko> <out-file>" >&2
  exit 64
fi

case "$LANG" in
  en) LANG_NAME="English"; WORD_TARGET="130 to 160 words" ;;
  ko) LANG_NAME="Korean";  WORD_TARGET="약 300자에서 360자 (60초 분량 한국어 내레이션)" ;;
  *)  echo "unsupported lang: $LANG (use en or ko)" >&2; exit 64 ;;
esac

PROMPT="You are writing a 60-second YouTube Shorts narration in $LANG_NAME.  Mobile feed; the viewer decides in 1.5 seconds whether to keep watching.  Most shorts in this niche are skipped within 5 seconds — your hook has to earn the second 5 seconds, and the second 5 has to earn the next 10.

Topic: $TOPIC

Hard rules:

1. Length: $WORD_TARGET.  Count.  Over or under by more than 10% — rewrite, don't ship.

2. Hook (first ~1.5 seconds of narration, ~5-7 words in $LANG_NAME).  Pick whichever fits the topic best, but the hook MUST be one of:
   (a) A counter-intuitive specific fact stated as bare statement, no setup.  Numbers in figures (1906 not 'nineteen-oh-six').
   (b) A discovery moment in past tense, like the opening line of a story.
   (c) A sharp specific contradiction that primes the rest of the script.
   Forbidden openings: 'What if...', 'Did you know...', 'Imagine...', 'Have you ever...'.  These signal low-effort content and viewers swipe.

3. Body (4 beats): each beat = 1-2 sentences, EACH beat must contain at least one specific concrete detail — a name, a year, a number, a place.  No generic statements ('history is full of mysteries', 'science teaches us', etc.).

4. Tension turn between beat 2 and beat 3: a 'but', 'and yet', or '그런데', '하지만' that reframes what the viewer just heard.  The script should have an emotional shape, not be a list of facts.

5. Close: a single short concrete sentence.  Make the viewer remember ONE specific image.  No abstractions ('the legacy of', 'the importance of', '인류의 도전 정신').

6. Tone: confident documentary calm (Vsauce / Veritasium / Kurzgesagt).  No 'amazing', 'incredible', 'mind-blowing', '놀랍게도'.  No exclamation marks.  No emoji.

7. Factual precision: when you cite a number, it must be specifically correct.  Common trap: hydrogen makes up ~10% of the human body BY MASS but ~63% BY ATOM COUNT — these are different facts; do NOT mix them in one breath.  Pick one frame and stick with it.  Same trap for any '% of universe', '% of body', 'years ago' figures — pick the correct frame and be unambiguous.

8. No markdown, no labels, no stage directions, no '[pause]' notation.  Just the narration words a TTS engine should speak.

Think for a moment about which of the 3 hook patterns fits THIS topic best, then write the script.  Output ONLY the narration text — no preamble, no explanation, no 'here is the script:' line."

# Content-quality feedback loop:
# - Generate script
# - Score it (scripts/score-content.sh, 2-axis hook + factual coherence)
# - If both axes >= threshold (default 7), accept
# - Else regenerate with axis-specific feedback prepended, up to MAX_RETRIES retries
# - Emit a JSONL trail to <out>.scoring.log for reproducibility
MAX_RETRIES="${SCRIPT_SCORE_MAX_RETRIES:-2}"
THRESHOLD="${SCORE_THRESHOLD:-7}"
SCORING_LOG="${OUT}.scoring.log"
: > "$SCORING_LOG"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

normalize() {
  awk 'NF || found{found=1; print}' "$1" | sed 's/[[:space:]]*$//' > "$1.tmp"
  mv "$1.tmp" "$1"
}

attempt=0
feedback_block=""
while :; do
  current_prompt="$PROMPT"
  if [[ -n "$feedback_block" ]]; then
    current_prompt="$PROMPT

PREVIOUS ATTEMPT (do not repeat its failure mode):
$feedback_block"
  fi

  claude --print --model claude-sonnet-4-6 "$current_prompt" > "$OUT"
  normalize "$OUT"

  # Score the candidate.
  if SCORE=$(SCORE_THRESHOLD="$THRESHOLD" "$REPO_ROOT/scripts/score-content.sh" "$TOPIC" "$OUT" 2>/dev/null); then
    echo "$SCORE" | jq -c --arg attempt "$attempt" '. + {attempt:($attempt|tonumber)}' >> "$SCORING_LOG"
    if [[ "$(echo "$SCORE" | jq -r .ok)" == "true" ]]; then
      MIN=$(echo "$SCORE" | jq -r .min_score)
      HOOK=$(echo "$SCORE" | jq -r .hook_strength)
      FACT=$(echo "$SCORE" | jq -r .factual_coherence)
      WORDS=$(wc -w < "$OUT" | tr -d ' ')
      CHARS=$(wc -c < "$OUT" | tr -d ' ')
      echo "✓ $OUT  ($WORDS words / $CHARS chars) — hook=$HOOK factual=$FACT (attempt $((attempt + 1))/$((MAX_RETRIES + 1)))"
      break
    fi
  else
    # Scoring failed (claude unavailable, parse error, etc).  Accept the
    # current draft rather than block — log the failure for visibility.
    echo "{\"attempt\":$attempt,\"error\":\"score-content.sh non-zero\"}" >> "$SCORING_LOG"
    WORDS=$(wc -w < "$OUT" | tr -d ' ')
    CHARS=$(wc -c < "$OUT" | tr -d ' ')
    echo "✓ $OUT  ($WORDS words / $CHARS chars) — scoring unavailable, accepted as-is"
    break
  fi

  attempt=$((attempt + 1))
  if (( attempt > MAX_RETRIES )); then
    # Exhausted retries; ship the last attempt anyway with a warning.
    HOOK=$(echo "$SCORE" | jq -r .hook_strength)
    FACT=$(echo "$SCORE" | jq -r .factual_coherence)
    WORDS=$(wc -w < "$OUT" | tr -d ' ')
    CHARS=$(wc -c < "$OUT" | tr -d ' ')
    echo "⚠ $OUT  ($WORDS words / $CHARS chars) — hook=$HOOK factual=$FACT (below threshold $THRESHOLD, exhausted retries — kept anyway)" >&2
    break
  fi

  # Build axis-specific feedback for the next attempt.
  HOOK_REASON=$(echo "$SCORE" | jq -r .hook_reasoning)
  FACT_REASON=$(echo "$SCORE" | jq -r .factual_reasoning)
  HOOK_SCORE=$(echo "$SCORE" | jq -r .hook_strength)
  FACT_SCORE=$(echo "$SCORE" | jq -r .factual_coherence)
  PREV_SCRIPT=$(cat "$OUT")

  feedback_block="The previous draft scored hook=$HOOK_SCORE/10, factual=$FACT_SCORE/10.  Both must be >= $THRESHOLD.

Hook critique: $HOOK_REASON
Factual critique: $FACT_REASON

Previous draft (DO NOT reuse this opening; rewrite with a different hook pattern):

$PREV_SCRIPT

Now produce a new draft that fixes the dimensions below threshold.  Do not repeat the previous draft's exact opening line.  Output ONLY the new narration text."

  echo "  (attempt $attempt scored hook=$HOOK_SCORE factual=$FACT_SCORE — retrying with feedback)" >&2
done
