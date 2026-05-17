#!/usr/bin/env bash
# Score a faceless-short narration script on two retention-mapping
# dimensions (hook + factual coherence) via Claude Sonnet, output strict
# JSON to stdout.
#
# Why only two of the five scorecard dimensions: this scorer runs
# BEFORE B-roll fetch + caption render, so visual-sync, readability,
# and production-polish aren't observable yet.  Those land at the
# rendered-mp4 stage (future scope).  Hook + factual cover the two
# axes that v5 → v6 lifted from 3 to 9 each — the two the operator
# actually surfaces as broken.
#
# Usage:
#   scripts/score-content.sh <topic_string> <script_path>
#
# Output (stdout):
#   {
#     "hook_strength":    <0-10>,
#     "hook_reasoning":   "<one line>",
#     "factual_coherence":<0-10>,
#     "factual_reasoning":"<one line>",
#     "min_score":        <0-10>,    # min of the two axes
#     "ok":               <bool>     # true if both >= threshold (default 7)
#   }
#
# Exit codes:
#   0  — scoring completed (regardless of pass/fail; check .ok)
#   1  — Sonnet call failed or returned unparseable JSON
#   64 — usage error
set -euo pipefail

TOPIC="${1:-}"
SCRIPT_PATH="${2:-}"
THRESHOLD="${SCORE_THRESHOLD:-7}"

if [[ -z "$TOPIC" || -z "$SCRIPT_PATH" || ! -f "$SCRIPT_PATH" ]]; then
  echo "usage: $0 <topic_string> <script_path>" >&2
  exit 64
fi

SCRIPT_TEXT=$(cat "$SCRIPT_PATH")

PROMPT="You are evaluating a 60-second faceless YouTube Shorts narration on two retention-mapping dimensions.  These two are the dimensions that, in our scorecard history, jumped from 3/10 to 9/10 when the same pipeline routed script generation from a 3B local model to Sonnet — i.e., these are the dimensions LLM-script-quality actually controls.

Topic: $TOPIC

Script:
$SCRIPT_TEXT

Rate each dimension 0–10 with concise reasoning.  Be honest — under-rate rather than inflate, since this score gates whether the script gets regenerated.

1. HOOK STRENGTH (does the first ~1.5 seconds of narration earn the next 5)
   Strong (8-10): bare-statement specific fact, counter-intuitive number, discovery moment in past tense, sharp specific contradiction.  Example landmarks: 'Scholars called the Hittites fiction', 'AutoTune was invented to find oil', '63 of every 100 atoms in your body is hydrogen'.
   Mid (5-7): clear claim but generic phrasing, weak verb choice, hook buried after 2 sentences.
   Weak (0-4): 'What if...', 'Did you know...', 'Imagine...', abstract framing, no concrete number, requires viewer to invest before any payoff.

2. FACTUAL COHERENCE (internal consistency, frame stability)
   Strong (8-10): one frame held throughout (e.g., 'atom count' OR 'mass', not both in one breath), specific verifiable numbers (years, percentages, names), no obvious hallucinations.
   Mid (5-7): mostly consistent but minor frame switching, one or two unverifiable claims.
   Weak (0-4): mixing frames mid-script (e.g., 'hydrogen is 10% of body' then '63%' without disambiguation), obvious hallucinations (e.g., 'Tawagalawa was a Hittite diplomat' when he was Mycenaean), claims that contradict each other.

Output strict JSON only.  No preamble, no markdown fences, no explanation outside the JSON:

{\"hook_strength\":<0-10 integer>,\"hook_reasoning\":\"<one line>\",\"factual_coherence\":<0-10 integer>,\"factual_reasoning\":\"<one line>\"}"

# Call Sonnet via the claude CLI (subscription quota, no incremental dollar spend).
RAW=$(claude --print --model claude-sonnet-4-6 "$PROMPT" 2>/dev/null) || {
  echo "scoring: claude CLI call failed" >&2
  exit 1
}

# Strip any code-fence wrapping the model might emit despite the
# strict-JSON instruction.  Tolerate ```json ... ``` and ``` ... ```.
JSON=$(echo "$RAW" | awk '
  /^```/{ if (in_fence) { in_fence=0; next } else { in_fence=1; next } }
  in_fence || NR == 1 || /^[ \t]*[{"]/ { print }
' | sed 's/^```json//; s/^```//; s/```$//')

# Fallback: if awk strip produced nothing usable, try raw output directly.
if ! echo "$JSON" | jq -e . >/dev/null 2>&1; then
  JSON="$RAW"
fi

# Validate it's parseable JSON and contains the four expected keys.
if ! echo "$JSON" | jq -e '.hook_strength and .factual_coherence and .hook_reasoning and .factual_reasoning' >/dev/null 2>&1; then
  echo "scoring: unparseable response from claude. raw output below:" >&2
  echo "$RAW" >&2
  exit 1
fi

# Augment with derived fields (min_score + ok flag).
echo "$JSON" | jq --argjson th "$THRESHOLD" '
  . + {
    min_score: ([.hook_strength, .factual_coherence] | min),
    ok: (.hook_strength >= $th and .factual_coherence >= $th)
  }
'
