#!/usr/bin/env bash
# product-cf mission runner — assemble a CF-style product short from ONE
# product photo + liquid/mood B-roll + music, beat-synced.
#
# The product hero shots (cutout + 2.5D + label sweep, label NEVER morphed)
# are produced by skills/product-cf/scripts/product-hero.sh.  Motion that
# implies shaking/pouring/drinking comes from liquid B-roll INTERCUT with the
# hero shots — the real product is never touched by a hand or liquid.
#
# Pipeline:
#   1. product-hero.sh → one hero clip; slice 3 push-in sub-beats from it
#   2. Pexels video search per liquid keyword → portrait B-roll clips
#   3. aubiotrack on music → beat grid → segment boundaries (~2s, snapped)
#   4. Timeline: B-roll teasers → hero reveals interleaved → final hero hold
#   5. Normalize every segment (1080x1920/30fps/h264), concat, mux music,
#      light grain + vignette to match the music-video look
#
# Usage:
#   agents/missions/product-cf/run.sh <short_id> <product_image> [keywords_csv] [music_file]
#
# Env knobs (taste):
#   PCF_TARGET_SEC   total length target (default 20)
#   PCF_SEG_BEATS    beats per segment (default 4; lower = faster cuts)
#   PCF_PATTERN      segment type cycle (default "b b h b h b h"; b=broll h=hero)
set -euo pipefail

SHORT_ID="${1:?usage: run.sh <short_id> <product_image> [keywords_csv] [music_file]}"
PRODUCT_IMG="${2:?missing product_image}"
KEYWORDS_CSV="${3:-water splash,ice cubes,citrus fruit slice,liquid pour,condensation droplets}"
MUSIC="${4:-assets/music/house-disco-mirrorball.mp3}"

cd "$(git rev-parse --show-toplevel 2>/dev/null || echo .)"
[[ -f .env ]] && set -a && . ./.env && set +a || true
FFMPEG_BIN="${FFMPEG_BIN:-ffmpeg}"
RECORDS_DIR="${RECORDS_DIR:-./records}"
TARGET_SEC="${PCF_TARGET_SEC:-20}"
SEG_BEATS="${PCF_SEG_BEATS:-4}"
PATTERN="${PCF_PATTERN:-b b h b h b h}"
W=1080; H=1920; FPS=30

DATE="$(date +%Y-%m-%d)"
MDIR="$RECORDS_DIR/missions/$DATE/$SHORT_ID"
RES="$MDIR/resources"; SEG="$MDIR/segments"
mkdir -p "$RES" "$SEG" "$MDIR/outputs"
log(){ printf '[pcf] %s\n' "$*" >&2; }

# ── 1. hero clip + 3 push-in sub-beats ──────────────────────────────
log "1/5  hero clip (product cutout + 2.5D + label sweep)"
HERO="$RES/hero.mp4"
if [[ "${PCF_REUSE_HERO:-0}" == "1" && -f "$HERO" ]]; then
  log "  reuse cached hero ($HERO)"
else
  HERO_ZOOM="${HERO_ZOOM:-1.16}" skills/product-cf/scripts/product-hero.sh "$PRODUCT_IMG" "$HERO" 6
fi
# three progressively-zoomed slices → the product "grows" across reveals
HERO_SUB=(); i=0
for ss in 0.0 2.0 3.8; do
  out="$SEG/hero-sub-$i.mp4"
  "$FFMPEG_BIN" -y -loglevel error -ss "$ss" -i "$HERO" -t 2.2 \
    -vf "scale=${W}:${H}:force_original_aspect_ratio=increase,crop=${W}:${H},fps=${FPS},format=yuv420p" \
    -an -c:v libx264 -crf 18 "$out"
  HERO_SUB+=("$out"); i=$((i+1))
done

# ── 2. liquid B-roll from Pexels (video) ────────────────────────────
log "2/5  fetch liquid B-roll"
BROLL=(); idx=0
IFS=',' read -ra KW <<< "$KEYWORDS_CSV"
for kw in "${KW[@]}"; do
  kw_trim="$(echo "$kw" | sed 's/^ *//;s/ *$//')"
  q="$(python3 -c "import urllib.parse,sys;print(urllib.parse.quote(sys.argv[1]))" "$kw_trim")"
  J=$(curl -fsS -H "Authorization: ${PEXELS_API_KEY:-}" \
      "https://api.pexels.com/videos/search?query=${q}&orientation=portrait&per_page=3&size=medium" 2>/dev/null || echo '{}')
  # pick an HD-ish portrait file link
  LINK=$(echo "$J" | jq -r '[.videos[].video_files[] | select(.width<=1280 and .height>=1080)] | sort_by(.height) | last | .link // empty' 2>/dev/null)
  [[ -z "$LINK" ]] && LINK=$(echo "$J" | jq -r '.videos[0].video_files[0].link // empty' 2>/dev/null)
  [[ -z "$LINK" ]] && { log "  no B-roll for '$kw_trim' — skip"; continue; }
  raw="$RES/broll-raw-$idx.mp4"
  curl -fsS "$LINK" -o "$raw" || { log "  download failed '$kw_trim'"; continue; }
  norm="$SEG/broll-$idx.mp4"
  "$FFMPEG_BIN" -y -loglevel error -i "$raw" -t 3 \
    -vf "scale=${W}:${H}:force_original_aspect_ratio=increase,crop=${W}:${H},fps=${FPS},format=yuv420p" \
    -an -c:v libx264 -crf 20 "$norm"
  BROLL+=("$norm"); idx=$((idx+1))
done
[[ "${#BROLL[@]}" -eq 0 ]] && { log "FATAL: no B-roll fetched"; exit 65; }

# ── 3. beat grid → segment boundaries ───────────────────────────────
log "3/5  beat detection (aubiotrack)"
BEATS="$RES/beats.txt"
aubiotrack -i "$MUSIC" 2>/dev/null | awk 'NF' > "$BEATS" || true
# bash 3.2 (macOS default) has no mapfile — read the beat grid manually
BARR=(); while IFS= read -r _bl; do [[ -n "$_bl" ]] && BARR+=("$_bl"); done < "$BEATS"
# boundaries every SEG_BEATS beats until TARGET_SEC
BOUNDS=(); k=0
if [[ "${#BARR[@]}" -gt "$SEG_BEATS" ]]; then
  while [[ "$k" -lt "${#BARR[@]}" ]]; do
    b="${BARR[$k]}"
    awk -v b="$b" -v t="$TARGET_SEC" 'BEGIN{exit !(b<=t)}' || break
    BOUNDS+=("$b"); k=$((k+SEG_BEATS))
  done
fi
# fallback: fixed 2s grid if beats too sparse
if [[ "${#BOUNDS[@]}" -lt 3 ]]; then
  log "  beats sparse — fixed 2s grid"
  BOUNDS=(); t=0
  while awk -v t="$t" -v T="$TARGET_SEC" 'BEGIN{exit !(t<T)}'; do BOUNDS+=("$t"); t=$(awk -v t="$t" 'BEGIN{print t+2}'); done
fi
BOUNDS+=("$TARGET_SEC")  # close final segment
log "  ${#BOUNDS[@]} boundaries over ${TARGET_SEC}s"

# ── 4. timeline assembly ────────────────────────────────────────────
log "4/5  build timeline"
read -ra PAT <<< "$PATTERN"
CONCAT="$MDIR/concat.txt"; : > "$CONCAT"
bi=0; hi=0; pi=0; s=0
NSEG=$(( ${#BOUNDS[@]} - 1 ))
for ((s=0; s<NSEG; s++)); do
  start="${BOUNDS[$s]}"; end="${BOUNDS[$((s+1))]}"
  dur=$(awk -v a="$start" -v b="$end" 'BEGIN{printf "%.3f", b-a}')
  awk -v d="$dur" 'BEGIN{exit !(d>0.3)}' || continue
  # last segment is always a hero hold (product is the payoff)
  if [[ "$s" -eq "$((NSEG-1))" ]]; then typ="h"; else typ="${PAT[$((pi % ${#PAT[@]}))]}"; pi=$((pi+1)); fi
  segout="$SEG/timeline-$(printf '%02d' "$s").mp4"
  if [[ "$typ" == "h" ]]; then
    src="${HERO_SUB[$((hi % ${#HERO_SUB[@]}))]}"; hi=$((hi+1))
  else
    src="${BROLL[$((bi % ${#BROLL[@]}))]}"; bi=$((bi+1))
  fi
  # trim/loop src to exactly dur (loop short clips), normalize
  "$FFMPEG_BIN" -y -loglevel error -stream_loop 3 -i "$src" -t "$dur" \
    -vf "scale=${W}:${H}:force_original_aspect_ratio=increase,crop=${W}:${H},fps=${FPS},setpts=PTS-STARTPTS,format=yuv420p" \
    -an -c:v libx264 -crf 19 "$segout"
  echo "file '$(realpath "$segout")'" >> "$CONCAT"
done

# ── 5. concat + music + match-look treatment ────────────────────────
log "5/5  concat + music + grain/vignette"
SILENT="$RES/concat-silent.mp4"
"$FFMPEG_BIN" -y -loglevel error -f concat -safe 0 -i "$CONCAT" -c copy "$SILENT"
DUR=$("$FFMPEG_BIN" -y -loglevel error -i "$SILENT" 2>&1 | true; ffprobe -v error -show_entries format=duration -of csv=p=0 "$SILENT")
OUT="$MDIR/outputs/short.mp4"
"$FFMPEG_BIN" -y -loglevel error -i "$SILENT" -i "$MUSIC" -filter_complex "
  [0:v]vignette=PI/5,noise=alls=6:allf=t+u[v];
  [1:a]afade=t=out:st=$(awk -v d="$DUR" 'BEGIN{printf "%.2f", d-1}'):d=1,aresample=44100[a]
" -map "[v]" -map "[a]" -t "$DUR" -c:v libx264 -crf 18 -c:a aac -b:a 192k -shortest "$OUT"

log "done → $OUT  (${DUR}s)"
echo "$OUT"
