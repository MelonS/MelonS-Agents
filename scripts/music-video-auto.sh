#!/usr/bin/env bash
# All-in-one music-video producer: detect genre → resolve preset →
# generate short → apply matching post-shader → optionally produce a
# Canvas 8s variant + kinetic typography overlay.  Zero arguments
# beyond the music file.
#
# Usage:
#   scripts/music-video-auto.sh <music_file>
#   scripts/music-video-auto.sh <music_file> [--with-canvas] [--with-typography "phrase1,phrase2,..."]
#   scripts/music-video-auto.sh <music_file> --short-id=<id>
#   scripts/music-video-auto.sh <music_file> --image=<path>   # forced for stillzoom genres
#
# Examples:
#   scripts/music-video-auto.sh assets/music/track3-arcade.mp3
#     → detects synthwave, fetches synthwave keyword pool, runs pipeline,
#       applies scanline shader.  Final mp4 under records/missions/...
#
#   scripts/music-video-auto.sh assets/music/track5-linen.mp3 --image=assets/stills/linen.jpg
#     → detects ambient, requires stillzoom (image), applies halation.
#
# Compose with --with-canvas / --with-typography for the full output bundle.
#
# Reads MUSIC_VIDEO_GENRE env var as override (skips detection).

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh" 2>/dev/null || true

DETECT_SH="$REPO_ROOT/scripts/music-video-genre-detect.sh"
GENRE_SH="$REPO_ROOT/scripts/music-video-genre.sh"
CANVAS_SH="$REPO_ROOT/scripts/music-video-canvas.sh"
TYPO_SH="$REPO_ROOT/scripts/music-video-typography.sh"
AREACTIVE_SH="$REPO_ROOT/scripts/music-video-audio-reactive.sh"
LYRICS_SH="$REPO_ROOT/scripts/music-video-lyrics.sh"

# Parse flags
WITH_CANVAS=0
WITH_TYPO_FLAG=0     # --with-typography given (use genre pool)
WITH_TYPO=""         # explicit phrases CSV (overrides pool)
WITH_AREACTIVE=0     # --with-audio-reactive
USE_AI_STILL=0       # --ai-still: Pollinations.ai instead of Pexels for stillzoom
FULL_LENGTH=0        # --full-length: use music's actual duration (default 60s)
DURATION=""          # --duration=N: explicit duration override in seconds
LYRICS_FILE=""       # --with-lyrics=PATH plain-text or LRC lyric file
LYRICS_ALIGN=1       # 1 → run whisper-based alignment; 0 → auto-space
STILL_IMG=""
SHORT_ID=""
ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --with-canvas) WITH_CANVAS=1; shift ;;
    --with-audio-reactive) WITH_AREACTIVE=1; shift ;;
    --ai-still) USE_AI_STILL=1; shift ;;
    --full-length) FULL_LENGTH=1; shift ;;
    --duration=*) DURATION="${1#*=}"; shift ;;
    --with-typography)
      WITH_TYPO_FLAG=1
      # Peek at next arg — if non-flag, treat as phrases CSV; else use pool
      if [[ "${2:-}" != "" && "${2:-}" != --* ]]; then
        WITH_TYPO="$2"; shift 2
      else
        shift
      fi
      ;;
    --with-typography=*) WITH_TYPO_FLAG=1; WITH_TYPO="${1#*=}"; shift ;;
    --with-lyrics=*) LYRICS_FILE="${1#*=}"; shift ;;
    --lyrics-no-align) LYRICS_ALIGN=0; shift ;;
    --image=*) STILL_IMG="${1#*=}"; shift ;;
    --short-id=*) SHORT_ID="${1#*=}"; shift ;;
    --help)
      sed -n '1,/^set -uo/p' "$0" | grep '^#' | sed 's/^# \?//'
      exit 0
      ;;
    -*) echo "❌ unknown flag: $1" >&2; exit 64 ;;
    *) ARGS+=("$1"); shift ;;
  esac
done

if [[ "${#ARGS[@]}" -eq 0 ]]; then
  echo "usage: $0 <music_file> [--with-canvas] [--with-typography \"...\"] [--image=PATH] [--short-id=ID]" >&2
  exit 64
fi

MUSIC="${ARGS[0]}"
[[ -f "$MUSIC" ]] || { echo "❌ music not found: $MUSIC" >&2; exit 64; }

# Generate short ID from filename if not supplied
if [[ -z "$SHORT_ID" ]]; then
  SHORT_ID=$(basename "$MUSIC" | sed 's/\.[^.]*$//' | tr 'A-Z ' 'a-z_' | tr -cd 'a-z0-9_-' | cut -c1-32)
  [[ -z "$SHORT_ID" ]] && SHORT_ID="auto"
fi

# 1. Detect genre
echo "═══════════════════════════════════════════════════════════════"
echo " music-video-auto: $(basename "$MUSIC")"
echo "═══════════════════════════════════════════════════════════════"
GENRE=$(bash "$DETECT_SH" "$MUSIC")
echo "→ detected genre: $GENRE"
echo "→ short_id: $SHORT_ID"
echo

# 2. For stillzoom genres without operator-supplied image, auto-fetch
#    a Pexels portrait photo from the genre's first keyword_pool entry.
PRESETS_YAML="$REPO_ROOT/skills/music-video/data/genre-presets.yaml"
CUT_MODE=$(yq -r ".genres.${GENRE}.cut_mode" "$PRESETS_YAML" 2>/dev/null)
LANG_ANCHOR=$(yq -r ".genres.${GENRE}.lang_anchor // \"neutral\"" "$PRESETS_YAML" 2>/dev/null)
if [[ "$CUT_MODE" == "stillzoom" && -z "$STILL_IMG" ]]; then
  FIRST_KW=$(yq -r ".genres.${GENRE}.keyword_pool[0]" "$PRESETS_YAML" 2>/dev/null)
  if [[ -n "$FIRST_KW" && "$FIRST_KW" != "null" ]]; then
    STILL_IMG="${TMPDIR:-/tmp}/auto-still-${SHORT_ID}.jpg"
    if [[ "$USE_AI_STILL" == 1 ]]; then
      # Pollinations.ai — richer prompt combining genre aesthetic + keyword.
      # Ethnicity anchor (quality-bar #5/#6) added for vocal-anchored genres.
      AI_PROMPT="${FIRST_KW}, ${GENRE} aesthetic, cinematic, atmospheric, no text, no people"
      case "$LANG_ANCHOR" in
        ko)    AI_PROMPT="${FIRST_KW}, Korean aesthetic, Seoul context, ${GENRE} mood, cinematic, atmospheric, no text" ;;
        en)    AI_PROMPT="${FIRST_KW}, Western context, ${GENRE} aesthetic, cinematic, atmospheric, no text" ;;
        mixed) AI_PROMPT="${FIRST_KW}, East Asian city aesthetic, ${GENRE} mood, cinematic, atmospheric, no text" ;;
      esac
      echo "→ stillzoom genre + --ai-still — generating via Pollinations.ai (lang_anchor=$LANG_ANCHOR)"
      if bash "$REPO_ROOT/scripts/music-video-fetch-ai-still.sh" "$AI_PROMPT" "$STILL_IMG"; then
        echo "  using $STILL_IMG (AI-generated)"
      else
        echo "⚠️  AI still fetch failed — falling back to Pexels" >&2
        bash "$REPO_ROOT/scripts/music-video-fetch-still.sh" "$FIRST_KW" "$STILL_IMG"
      fi
    else
      echo "→ stillzoom genre + no --image — auto-fetching Pexels still"
      if bash "$REPO_ROOT/scripts/music-video-fetch-still.sh" "$FIRST_KW" "$STILL_IMG"; then
        echo "  using $STILL_IMG (query: '$FIRST_KW')"
      else
        echo "⚠️  still fetch failed — operator must pass --image=PATH" >&2
        exit 1
      fi
    fi
  fi
fi

# 3. Resolve duration — --duration=N takes precedence over --full-length,
#    --full-length uses music file's actual length, otherwise default 60s.
if [[ -n "$DURATION" ]]; then
  export MUSIC_VIDEO_DURATION="$DURATION"
  echo "→ explicit duration: ${DURATION}s"
elif [[ "$FULL_LENGTH" == 1 ]]; then
  MUSIC_DUR=$(${FFPROBE_BIN:-ffprobe} -v error -show_entries format=duration -of csv=p=0 "$MUSIC" | awk '{printf "%d", $1}')
  if [[ -n "$MUSIC_DUR" && "$MUSIC_DUR" -gt 30 ]]; then
    export MUSIC_VIDEO_DURATION="$MUSIC_DUR"
    echo "→ full-length: ${MUSIC_DUR}s (music's actual duration)"
  fi
fi

# 4. Run genre wrapper
EXTRA_FLAGS=()
[[ -n "$STILL_IMG" ]] && EXTRA_FLAGS+=(--image="$STILL_IMG")

# bash 3.2 + set -u: empty-array expansion errors without the +"..." guard
bash "$GENRE_SH" ${EXTRA_FLAGS[@]+"${EXTRA_FLAGS[@]}"} "$GENRE" "$SHORT_ID" "$MUSIC"
RC=$?
[[ $RC -ne 0 ]] && { echo "❌ genre wrapper exit $RC" >&2; exit $RC; }

# 3. Locate final mp4
LATEST=$(ls -1t "${RECORDS_DIR:-$REPO_ROOT/records}/missions/$(date +%Y-%m-%d)/music-video-${SHORT_ID}-"*/outputs/short*.mp4 2>/dev/null | head -1)
if [[ -z "$LATEST" ]]; then
  echo "⚠️  could not locate output mp4 — skipping --with-* steps" >&2
  exit 0
fi
echo
echo "═══ base mp4: $LATEST"

OUTPUT_DIR="$(dirname "$LATEST")"

# 4. Audio-reactive grading variant (optional, applied to base BEFORE canvas/typo
#    so downstream variants inherit the reactive grading)
if [[ "$WITH_AREACTIVE" == 1 ]]; then
  AREACTIVE_OUT="${LATEST%.mp4}-audio-reactive.mp4"
  echo "→ audio-reactive grading"
  bash "$AREACTIVE_SH" "$LATEST" "$MUSIC" "$AREACTIVE_OUT"
  LATEST="$AREACTIVE_OUT"   # downstream uses the graded version
  echo "✓ audio-reactive: $AREACTIVE_OUT"
fi

# 5. Canvas variant (optional)
if [[ "$WITH_CANVAS" == 1 ]]; then
  CANVAS_OUT="$OUTPUT_DIR/canvas.mp4"
  echo "→ canvas 8s variant"
  bash "$CANVAS_SH" "$LATEST" "$CANVAS_OUT"
  echo "✓ canvas: $CANVAS_OUT"
fi

# 5a. Lyrics overlay (optional, applied before typography so typography
#     can paint on top if both are requested).
if [[ -n "$LYRICS_FILE" ]]; then
  if [[ ! -f "$LYRICS_FILE" ]]; then
    echo "⚠️  --with-lyrics file not found: $LYRICS_FILE — skipping" >&2
  else
    LYRICS_OUT="${LATEST%.mp4}-lyrics.mp4"
    LYRICS_ARGS=("$LATEST" "$LYRICS_OUT" "$LYRICS_FILE" "--genre=$GENRE")
    if [[ "$LYRICS_ALIGN" == 1 ]]; then
      LYRICS_ARGS+=("--align-to-audio=$MUSIC")
      echo "→ lyrics overlay (whisper-aligned)"
    else
      echo "→ lyrics overlay (auto-spaced; --lyrics-no-align)"
    fi
    bash "$LYRICS_SH" "${LYRICS_ARGS[@]}"
    LATEST="$LYRICS_OUT"
    echo "✓ lyrics: $LYRICS_OUT"
  fi
fi

# 5. Typography overlay (optional)
if [[ "$WITH_TYPO_FLAG" == 1 ]]; then
  # If no explicit phrases, pull from genre phrase_pool
  if [[ -z "$WITH_TYPO" ]]; then
    PHRASES_YAML="$REPO_ROOT/skills/music-video/data/genre-presets.yaml"
    WITH_TYPO=$(yq -r ".genres.${GENRE}.phrase_pool | join(\",\")" "$PHRASES_YAML" 2>/dev/null)
    if [[ -z "$WITH_TYPO" || "$WITH_TYPO" == "null" ]]; then
      echo "⚠️  no phrase_pool for $GENRE — skipping typography" >&2
      WITH_TYPO=""
    else
      echo "→ no phrases supplied — using $GENRE preset pool"
      echo "  ($WITH_TYPO)"
    fi
  fi
  if [[ -n "$WITH_TYPO" ]]; then
    TYPO_OUT="${LATEST%.mp4}-typography.mp4"
    echo "→ typography overlay"
    bash "$TYPO_SH" "$LATEST" "$TYPO_OUT" "$WITH_TYPO"
    echo "✓ typography: $TYPO_OUT"
  fi
fi

# 6. Thumbnail extraction — auto-generated for upload metadata.
#    Skip via MUSIC_VIDEO_NO_THUMB=1 if operator handles it externally.
if [[ "${MUSIC_VIDEO_NO_THUMB:-0}" != "1" ]]; then
  THUMB_OUT="${LATEST%.mp4}-thumb.jpg"
  echo "→ thumbnail extraction"
  bash "$REPO_ROOT/scripts/music-video-thumbnail.sh" "$LATEST" "$THUMB_OUT" >/dev/null 2>&1 \
    && echo "✓ thumbnail: $THUMB_OUT"
fi

# 7. Optional validation pass — set MUSIC_VIDEO_VALIDATE=1 to run the
#    combined gate (file-integrity + qa-anchor + lyric-drift +
#    broll-dedup) at the end of the render.  Exit code is ignored
#    here (operator inspects verdict in the printed report).
if [[ "${MUSIC_VIDEO_VALIDATE:-0}" == "1" ]]; then
  echo "→ post-render validation gate"
  bash "$REPO_ROOT/scripts/music-video-validate.sh" "$OUTPUT_DIR/.." --genre="$GENRE" || true
fi

echo
echo "═══════════════════════════════════════════════════════════════"
echo " Done.  Outputs in: $OUTPUT_DIR"
echo "═══════════════════════════════════════════════════════════════"
ls -la "$OUTPUT_DIR" | awk 'NR>1 {printf "  %-30s %s\n", $NF, $5}'
