#!/usr/bin/env bash
# fetch-ai-anime-broll.sh — generate 8 cyberpunk-anime stills via
# pollinations.ai (free, no signup, Flux model) and ken-burns each
# into a 7-second 1080×1920 mp4.  Outputs to $FIXTURE_DIR/clips
# so the music-video mission can pick them up via
# MUSIC_VIDEO_BROLL_DIR=$FIXTURE_DIR/clips.
#
# Origin: 2026-05-19 ~17:10 KST friend-meeting.  The friend-test
# surfaced a four-way constraint Pexels/trailers can't satisfy at
# once: anime style + 2020s aesthetic + no embedded text + no
# copyrighted source.  AI image gen → ken-burns is the only path
# that hits all four inside a meeting timeframe.
#
# Pollinations.ai rate-limits concurrent requests per IP to 1
# (returns JSON x402 error instead of JPEG when over).  This
# script issues requests serially with retry-on-JSON.
#
# Usage:
#   FIXTURE_DIR=/tmp/anime-gen scripts/fetch-ai-anime-broll.sh
#   MUSIC_VIDEO_BROLL_DIR=$FIXTURE_DIR/clips \
#     ./agents/missions/music-video/run.sh <id> <music> <kw,csv>
set -euo pipefail
FIXTURE_DIR="${FIXTURE_DIR:-/tmp/anime-gen}"
DEST="$FIXTURE_DIR/clips"
FFMPEG="${FFMPEG_BIN:-ffmpeg}"
mkdir -p "$DEST"

PROMPTS=(
  "neon_rain_city_night|cinematic anime cyberpunk scene, rainy neon city street at night, blade runner inspired, no signage no text no letters, atmospheric purple pink lighting, studio trigger style, ultra detailed"
  "cyberpunk_skyline_neon|cinematic anime cyberpunk megacity skyline at night, neon towers, holographic reflections, no signage no text no letters, blade runner 2049 atmosphere, painterly anime style"
  "futuristic_megacity|cinematic anime futuristic megacity aerial view, dense skyscrapers, flying vehicles, no signage no text no letters, sunset cyberpunk colors, edgerunners style"
  "rainy_alley_neon|cinematic anime empty rainy alley with neon glow reflections on wet pavement, no text no signage no letters, atmospheric solitary mood, cyberpunk noir"
  "holographic_billboard|cinematic anime giant abstract holographic display over street, no readable text no letters no signage, glowing geometric patterns, blade runner 2049 atmosphere"
  "megacity_tower_lights|cinematic anime cyberpunk towers wrapped in fog at night, glowing windows, no signage no text no letters, layered depth, painterly"
  "noir_detective_city|cinematic anime cyberpunk silhouette of figure in trench coat in rainy neon street, no text no letters no signage, dramatic backlight, blade runner noir mood"
  "future_neon_street_walk|cinematic anime first person view walking through neon cyberpunk street, no signage no text no letters, motion blur, atmospheric"
)

# Step 1 - serial generation with retry
for entry in "${PROMPTS[@]}"; do
  IFS='|' read -r kw prompt <<< "$entry"
  out="$DEST/${kw}.jpg"
  # if existing valid JPEG, skip
  if [[ -s "$out" ]] && file "$out" | grep -q "JPEG image data"; then
    echo "  [skip] $kw"; continue
  fi
  rm -f "$out"
  enc=$(python3 -c "import urllib.parse,sys;print(urllib.parse.quote(sys.argv[1]))" "$prompt")
  seed=$(( RANDOM + 1 ))
  url="https://image.pollinations.ai/prompt/${enc}?width=1080&height=1920&model=flux&nologo=true&enhance=true&seed=${seed}"

  for attempt in 1 2 3; do
    echo "  [gen ] $kw (attempt $attempt)"
    if curl -sL --connect-timeout 30 --max-time 150 -o "$out" "$url" \
       && [[ -s "$out" ]] \
       && file "$out" | grep -q "JPEG image data"; then
      echo "  [ok  ] $kw ($(du -h "$out" | awk '{print $1}'))"
      break
    fi
    sleep 3
  done
done

# Step 2 - ken-burns each into 7s mp4
echo "[gen] ken-burns conversion..."
i=0
for kw_pair in "${PROMPTS[@]}"; do
  IFS='|' read -r kw _prompt <<< "$kw_pair"
  jpg="$DEST/${kw}.jpg"
  out="$DEST/raw-${kw}.mp4"
  [[ -s "$jpg" ]] || { echo "  [skip] $kw (no jpg)"; continue; }
  pan_dir=$((i % 4))
  case "$pan_dir" in
    0) x="iw/2-(iw/zoom/2)"; y="ih/2-(ih/zoom/2)+(1-on/210)*30" ;;
    1) x="iw/2-(iw/zoom/2)+(on/210)*30"; y="ih/2-(ih/zoom/2)" ;;
    2) x="iw/2-(iw/zoom/2)-(on/210)*30"; y="ih/2-(ih/zoom/2)" ;;
    *) x="iw/2-(iw/zoom/2)"; y="ih/2-(ih/zoom/2)-(on/210)*30" ;;
  esac
  i=$((i+1))
  "$FFMPEG" -y -loglevel error \
    -loop 1 -i "$jpg" \
    -vf "zoompan=z='min(zoom+0.001,1.2)':x='${x}':y='${y}':d=210:s=1080x1920:fps=30,format=yuv420p" \
    -t 7 -r 30 -c:v libx264 -preset medium -crf 22 "$out"
  [[ -s "$out" ]] && echo "  [mp4 ] $kw"
done
echo "[gen] done"
ls /tmp/anime-gen/clips/raw-*.mp4 2>&1
