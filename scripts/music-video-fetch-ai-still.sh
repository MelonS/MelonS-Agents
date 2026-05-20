#!/usr/bin/env bash
# Fetch an AI-generated still image from Pollinations.ai (free, no-signup
# image gen API).  Drop-in alternative to music-video-fetch-still.sh
# (which uses Pexels stock photos) — designed for stillzoom genres
# where a unique stylized aesthetic beats stock photography.
#
# Usage:
#   scripts/music-video-fetch-ai-still.sh <prompt> <output.jpg>
#     [--width=1080] [--height=1920] [--seed=N] [--model=NAME]
#
# Examples:
#   scripts/music-video-fetch-ai-still.sh "cyberpunk tokyo neon street, rain, cinematic" /tmp/still.jpg
#   scripts/music-video-fetch-ai-still.sh "dreamy pastel liminal hallway, vaporwave aesthetic" /tmp/still.jpg
#
# API:
#   https://image.pollinations.ai/prompt/{URL_ENCODED_PROMPT}?width=W&height=H&seed=S&nologo=true
#
# Cost:    $0  (Pollinations is free, no auth, no quota docs — IP rate-limited)
# Privacy: prompts visible in URL; no PII implications for genre prompts.
# Models:  default `flux` (Flux Schnell), also `flux-realism`, `flux-anime`,
#          `flux-3d`, `turbo`.  See https://image.pollinations.ai/models.
#
# Cache: ~/.cache/music-video-ai-stills/<sha1(prompt+seed+model)>.jpg
# Same prompt + seed = same image (deterministic, intentional for consistency).

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
# shellcheck disable=SC1091
source "$PWD/agents/lib/env.sh" 2>/dev/null || true

PROMPT="${1:-}"
DST="${2:-}"

if [[ -z "$PROMPT" || -z "$DST" ]]; then
  echo "usage: $0 <prompt> <output.jpg> [--width=N] [--height=N] [--seed=N] [--model=NAME]" >&2
  exit 64
fi
shift 2 2>/dev/null || true

WIDTH=1080
HEIGHT=1920
SEED=42
MODEL=flux

for arg in "$@"; do
  case "$arg" in
    --width=*)  WIDTH="${arg#*=}"  ;;
    --height=*) HEIGHT="${arg#*=}" ;;
    --seed=*)   SEED="${arg#*=}"   ;;
    --model=*)  MODEL="${arg#*=}"  ;;
    *) echo "❌ unknown flag: $arg" >&2; exit 64 ;;
  esac
done

CACHE_DIR="$HOME/.cache/music-video-ai-stills"
mkdir -p "$CACHE_DIR"

# Cache key: sha1(prompt + seed + model + dimensions)
KEY_INPUT="${PROMPT}|${SEED}|${MODEL}|${WIDTH}x${HEIGHT}"
if command -v sha1sum >/dev/null 2>&1; then
  KEY=$(printf '%s' "$KEY_INPUT" | sha1sum | awk '{print $1}')
else
  KEY=$(printf '%s' "$KEY_INPUT" | shasum | awk '{print $1}')
fi
CACHE="$CACHE_DIR/${KEY}.jpg"

if [[ -f "$CACHE" && -s "$CACHE" ]]; then
  echo "→ cache hit for prompt ($KEY) → $CACHE"
  cp "$CACHE" "$DST"
  echo "✓ ai-still: $DST  (cached)"
  exit 0
fi

# URL-encode the prompt
ENC=$(printf '%s' "$PROMPT" | jq -sRr @uri)
URL="https://image.pollinations.ai/prompt/${ENC}?width=${WIDTH}&height=${HEIGHT}&seed=${SEED}&model=${MODEL}&nologo=true&private=true"

echo "→ fetching from Pollinations.ai (model=$MODEL, ${WIDTH}x${HEIGHT}, seed=$SEED)"
echo "  prompt: $PROMPT"

# Pollinations can take 5-15s to generate. Allow 60s timeout.
if ! curl -sSL --max-time 60 "$URL" -o "$CACHE"; then
  echo "❌ fetch failed (URL: $URL)" >&2
  rm -f "$CACHE"
  exit 1
fi
if [[ ! -s "$CACHE" ]]; then
  echo "❌ empty response from pollinations.ai" >&2
  rm -f "$CACHE"
  exit 1
fi
# Verify it's actually an image
if ! file "$CACHE" | grep -qiE "image|jpeg|png"; then
  echo "❌ response is not an image: $(file "$CACHE" | head -c 200)" >&2
  rm -f "$CACHE"
  exit 1
fi

cp "$CACHE" "$DST"

# Attribution sidecar
cat > "${CACHE%.jpg}.meta.json" <<EOF
{
  "prompt": $(printf '%s' "$PROMPT" | jq -Rs .),
  "source": "pollinations.ai",
  "model": "$MODEL",
  "seed": $SEED,
  "dimensions": "${WIDTH}x${HEIGHT}",
  "license": "CC0 (Pollinations: 'all images are free to use without attribution')",
  "attribution_string": "AI-generated via Pollinations.ai ($MODEL)"
}
EOF

echo "✓ ai-still: $DST  (attribution at ${CACHE%.jpg}.meta.json)"
