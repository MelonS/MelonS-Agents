#!/usr/bin/env bash
# Generate ready-to-paste upload metadata for a faceless-short mission.
#
# For a given mission directory this script:
#   1. Reads the source narration script.
#   2. Reads all Pexels B-roll sidecars (.meta.json) and aggregates the
#      photographer attribution credits.
#   3. Asks ollama to draft platform-specific copy (YouTube Short title +
#      description, TikTok caption, Instagram Reels caption, hashtag set).
#   4. Writes outputs/upload-metadata.md next to short.mp4 so the
#      operator can copy-paste per platform on upload day.
#
# Usage:
#   ./scripts/gen-upload-metadata.sh <mission_dir>
#   ./scripts/gen-upload-metadata.sh records/missions/2026-05-16/faceless-hittites-233021
#
# Output:
#   <mission_dir>/outputs/upload-metadata.md

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/log.sh"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/ollama.sh"

MDIR="${1:-}"
if [[ -z "$MDIR" ]]; then
  log_err "usage: $0 <mission_dir>"
  exit 64
fi
if [[ ! -d "$MDIR/resources" || ! -d "$MDIR/outputs" ]]; then
  log_err "mission dir missing resources/ or outputs/: $MDIR"
  exit 65
fi

SCRIPT_PATH="$MDIR/resources/script.txt"
SHORT_PATH="$MDIR/outputs/short.mp4"
META_OUT="$MDIR/outputs/upload-metadata.md"
[[ -f "$SCRIPT_PATH" ]] || { log_err "missing $SCRIPT_PATH"; exit 66; }
[[ -f "$SHORT_PATH" ]] || { log_err "missing $SHORT_PATH"; exit 66; }

require_env OLLAMA_HOST OLLAMA_MODEL_HIGHLIGHT

NARRATION="$(cat "$SCRIPT_PATH")"

# Aggregate Pexels attribution from all B-roll sidecars.  Dedup by
# photographer; the page-URL list goes verbatim into the description so
# Pexels attribution is satisfied even though pexels-license doesn't
# require it (good citizen + survives reposts).
ATTRIBUTION_BLOCK="$(
  find "$MDIR/resources/broll" -name "*.meta.json" -type f 2>/dev/null \
    | xargs -I{} jq -r '"\(.photographer)\t\(.page_url)"' {} 2>/dev/null \
    | sort -u
)"

CREDITS_TEXT="$(
  echo "$ATTRIBUTION_BLOCK" | awk -F'\t' '
    NF==2 && $1 != "" {
      printf "- %s — %s\n", $1, $2
    }
  '
)"
if [[ -z "$CREDITS_TEXT" ]]; then
  CREDITS_TEXT="- (no Pexels sidecars found in resources/broll)"
fi

log_step "drafting platform copy via ollama"

PROMPT="You are writing upload copy for a 60-second faceless YouTube/TikTok/Reels short.

The narration is:
---
$NARRATION
---

Produce STRICT JSON in this exact shape:
{
  \"youtube_title\": \"...\",
  \"youtube_description\": \"...\",
  \"tiktok_caption\": \"...\",
  \"reels_caption\": \"...\",
  \"hashtags\": [\"#tag1\", \"#tag2\", \"#tag3\", \"#tag4\", \"#tag5\", \"#tag6\", \"#tag7\", \"#tag8\"]
}

Rules:
- youtube_title: <= 80 characters, hook-driven, no clickbait punctuation spam, no all-caps.
- youtube_description: 2 short paragraphs. First paragraph repeats the hook + 1 concrete fact. Second paragraph is one neutral sentence. Do NOT include hashtags here — they go in the hashtags array.
- tiktok_caption: <= 150 characters, conversational, hook-first.
- reels_caption: <= 200 characters, similar tone to tiktok_caption but slightly more descriptive.
- hashtags: exactly 8 hashtags ranked most-specific to most-general (niche topic → broad category). All lowercase, no spaces, no punctuation except #.
- Tone: neutral documentary curiosity, not sensational. No ALL CAPS. No emoji. No promises like 'mind-blowing'.
- Output JSON only — no markdown fence, no commentary."

JSON_RAW="$(ollama_generate "$OLLAMA_MODEL_HIGHLIGHT" "$PROMPT" true)"

# Validate JSON; if ollama produced unparseable output, surface that and
# stub the file with a manual-fill template rather than failing silently.
if ! echo "$JSON_RAW" | jq . >/dev/null 2>&1; then
  log_warn "ollama returned non-JSON; writing stub template"
  JSON_RAW='{
    "youtube_title": "<TODO: fill manually>",
    "youtube_description": "<TODO: fill manually>",
    "tiktok_caption": "<TODO: fill manually>",
    "reels_caption": "<TODO: fill manually>",
    "hashtags": ["#todo1", "#todo2", "#todo3", "#todo4", "#todo5", "#todo6", "#todo7", "#todo8"]
  }'
fi

YT_TITLE=$(echo "$JSON_RAW" | jq -r '.youtube_title')
YT_DESC=$(echo "$JSON_RAW"  | jq -r '.youtube_description')
TT_CAP=$(echo "$JSON_RAW"   | jq -r '.tiktok_caption')
IG_CAP=$(echo "$JSON_RAW"   | jq -r '.reels_caption')
HASHTAGS=$(echo "$JSON_RAW" | jq -r '.hashtags | join(" ")')

MISSION_ID="$(basename "$MDIR")"
TOPIC="$(jq -r '.topic // "—"' "$MDIR/metrics.json" 2>/dev/null || echo "—")"
DUR="$(jq -r '.output_duration_s // "—"' "$MDIR/metrics.json" 2>/dev/null || echo "—")"

cat > "$META_OUT" <<MARKDOWN
# Upload metadata — $MISSION_ID

| | |
|---|---|
| Mission | \`$MISSION_ID\` |
| Topic | $TOPIC |
| Duration | ${DUR}s |
| File | \`$(basename "$SHORT_PATH")\` (next to this file) |
| Generated | $(date -u +%Y-%m-%dT%H:%M:%SZ) |

> Drafted by \`scripts/gen-upload-metadata.sh\` via \`$OLLAMA_MODEL_HIGHLIGHT\`.  Review before uploading — model copy is a starting point, not finished marketing.

---

## YouTube Shorts

### Title (paste into title field, ≤100 chars)

\`\`\`
$YT_TITLE
\`\`\`

### Description

\`\`\`
$YT_DESC

$HASHTAGS

— credits —
$CREDITS_TEXT

Narration: synthetic (Kokoro TTS, Apache 2.0).
\`\`\`

---

## TikTok

### Caption (paste into post-description field, ≤2200 chars)

\`\`\`
$TT_CAP

$HASHTAGS
\`\`\`

---

## Instagram Reels

### Caption (paste into post-caption field, ≤2200 chars)

\`\`\`
$IG_CAP

$HASHTAGS

credits: B-roll via Pexels (pexels.com/license). See repo for full per-clip attribution.
\`\`\`

---

## Attribution credits (per Pexels good-citizen — not strictly required by license)

$CREDITS_TEXT

## Raw ollama JSON (for re-templating)

\`\`\`json
$JSON_RAW
\`\`\`
MARKDOWN

log_ok "wrote $META_OUT"
echo
echo "next: review $META_OUT before uploading.  All four platform fields are starter copy — adjust the hook line on each before posting."
