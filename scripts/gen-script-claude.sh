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

claude --print --model claude-sonnet-4-6 "$PROMPT" > "$OUT"

# Strip leading/trailing blank lines + normalize trailing whitespace.
awk 'NF || found{found=1; print}' "$OUT" | sed 's/[[:space:]]*$//' > "$OUT.tmp"
mv "$OUT.tmp" "$OUT"

WORDS=$(wc -w < "$OUT" | tr -d ' ')
CHARS=$(wc -c < "$OUT" | tr -d ' ')
echo "✓ $OUT  ($WORDS words / $CHARS chars)"
