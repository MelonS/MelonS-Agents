#!/usr/bin/env bash
# Detect a music-video genre from a music file's filename + (optional) mp3
# metadata.  Outputs ONE token from skills/music-video/data/genre-presets.yaml
# to stdout, suitable for piping into scripts/music-video-genre.sh.
#
# Detection strategy (priority order, first match wins):
#   1. mp3 ID3 genre tag (id3v2 -l or ffprobe -show_format)
#   2. filename keyword match against per-genre alias list (greedy longest match)
#   3. fallback = "lofi_hiphop" (safest mismatch — warm grain+halation rarely
#      offends across genres, vs. e.g. scanlines on a ballad which feels wrong)
#
# Usage:
#   scripts/music-video-genre-detect.sh <music_file>      # prints genre
#   scripts/music-video-genre-detect.sh --explain <music> # prints reasoning + genre
#
# Common naming → detection:
#   track1-coastline.mp3   → house          (coastline → tropical_house alias)
#   track2-noir.mp3        → jazz           (noir → jazz_noir alias)
#   track3-arcade.mp3      → synthwave      (arcade → synthwave alias)
#   track4-rain.mp3        → lofi_hiphop    (rain → lofi alias)
#   track5-linen.mp3       → ambient        (linen → minimal_ambient alias)
#   cyberpunk.mp3          → synthwave      (cyberpunk → retrowave alias)
#   Tokyo Neon.mp3         → synthwave      (neon → retrowave alias)
#   Rainy Bossa.mp3        → jazz           (bossa → jazz alias)
#   Velvet Turntable.mp3   → lofi_hiphop    (turntable → lofi alias)
#   Late Night Piano.mp3   → ambient OR jazz (depends on tempo — defaults to jazz)
#
# Override via env: MUSIC_VIDEO_GENRE=<name> short-circuits detection entirely.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

PRESETS="skills/music-video/data/genre-presets.yaml"

EXPLAIN=0
if [[ "${1:-}" == "--explain" ]]; then
  EXPLAIN=1
  shift
fi

SRC="${1:-}"
if [[ -z "$SRC" ]]; then
  echo "usage: $0 [--explain] <music_file>" >&2
  exit 64
fi
[[ -f "$SRC" ]] || { echo "❌ file not found: $SRC" >&2; exit 64; }

# 0. env override
if [[ -n "${MUSIC_VIDEO_GENRE:-}" ]]; then
  [[ "$EXPLAIN" == 1 ]] && echo "→ env override MUSIC_VIDEO_GENRE=${MUSIC_VIDEO_GENRE}" >&2
  echo "$MUSIC_VIDEO_GENRE"
  exit 0
fi

# 1. ID3 genre tag via ffprobe
if command -v "${FFPROBE_BIN:-ffprobe}" >/dev/null 2>&1; then
  id3_genre=$("${FFPROBE_BIN:-ffprobe}" -v error -show_format "$SRC" 2>/dev/null | \
              awk -F= '/^TAG:genre=/ {gsub(/^TAG:genre=/,""); print tolower($0); exit}')
  if [[ -n "$id3_genre" ]]; then
    [[ "$EXPLAIN" == 1 ]] && echo "→ ID3 genre tag: '$id3_genre'" >&2
    # Try to resolve via aliases
    norm=$(echo "$id3_genre" | tr ' ' '_' | tr -cd 'a-z_')
    match=$(yq -r --arg g "$norm" '
      .genres | to_entries | .[] |
      select(.key == $g or ((.value.aliases // []) | map(tostring | sub("[^a-z_]";"";"g")) | index($g))) |
      .key
    ' "$PRESETS" 2>/dev/null | head -1)
    if [[ -n "$match" ]]; then
      [[ "$EXPLAIN" == 1 ]] && echo "→ matched preset: $match" >&2
      echo "$match"
      exit 0
    fi
    [[ "$EXPLAIN" == 1 ]] && echo "→ ID3 genre '$id3_genre' had no preset match — falling through to filename" >&2
  fi
fi

# 2. Filename keyword match
fname=$(basename "$SRC" | tr 'A-Z' 'a-z' | tr -cs 'a-z' ' ')
[[ "$EXPLAIN" == 1 ]] && echo "→ filename tokens: '$fname'" >&2

# Per-genre filename keywords (richer than presets aliases; tuned to user's
# actual naming patterns and Suno generation prompts).
declare_genre_keywords() {
  cat <<'EOF'
ambient: ambient linen minimal drone soft pad meditation chill_relax wind ocean still
drone: drone doom ambient dark bass_drone funeral
shoegaze: shoegaze dreampop slowdive fuzz reverb
citypop: citypop city_pop kr_citypop late_train ballad korean_pop kpop_ballad
lofi_hiphop: lofi lo-fi chillhop turntable vinyl velvet rain bossa cozy cafe study chill
jazz: jazz bossa noir vibraphone trumpet sax piano latenight late_night night
house: house tropical coastline summer beach palm sunshine
techno: techno minimal_techno warehouse industrial
synthwave: synthwave retrowave outrun arcade cyberpunk neon tokyo grid drive midnight urban
vaporwave: vaporwave mall pastel chrome
phonk: phonk drift anime memphis trap brakeslide
hyperpop: hyperpop glitch breakcore gabber chiptune
cottagecore: cottagecore folk acoustic fireplace warm forest meadow autumn
classical: classical orchestral chamber piano_solo violin baroque
dreamcore: dreamcore liminal weirdcore backrooms void
EOF
}

best_match=""
best_score=0
while IFS=':' read -r genre kws; do
  score=0
  matched_kw=""
  for kw in $kws; do
    # word-boundary match against filename tokens
    if echo " $fname " | grep -qiE "[[:space:]]${kw//_/[ _-]}[[:space:]]"; then
      score=$((score + ${#kw}))
      matched_kw="${matched_kw:+$matched_kw }$kw"
    fi
  done
  if (( score > best_score )); then
    best_score=$score
    best_match=$genre
    best_matched_kw=$matched_kw
  fi
done < <(declare_genre_keywords)

if [[ -n "$best_match" ]]; then
  [[ "$EXPLAIN" == 1 ]] && echo "→ filename match: $best_match (matched: $best_matched_kw, score=$best_score)" >&2
  echo "$best_match"
  exit 0
fi

# 3. Fallback
[[ "$EXPLAIN" == 1 ]] && echo "→ no match — fallback to lofi_hiphop (safest mismatch)" >&2
echo "lofi_hiphop"
