#!/usr/bin/env bash
# Full Pollinations for 월요일이 또 와요 — KR comedy K-ballad
set -euo pipefail
cd /Users/melons/ai
source .env 2>/dev/null
FFMPEG="${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}"
WORK=/tmp/full-pollinations-monday
mkdir -p "$WORK/stills" "$WORK/clips"

# LRC times for monday (from earlier whisper):
# [00:02.26] 일요일 밤 열한 시 반
# [00:20.39] 내일을 미루고 싶어
# [00:28.19] 드라마는 끝났는데
# [00:33.80] 왜 시간은 안 멈추니
# [00:40.86] 월요일이 또 와요
# [00:44.56] 어쩌면 좋아요
# [00:48.50] 주말은 너무 짧고
# [00:50.95] 회의는 너무 길어요
# [00:54.94] 출근길 지하철 안
# [00:58.50] 모두가 같은 표정
# [01:00.95] 누구도 웃지 않아요
# [01:04.79] 오늘이 금요일이면 좋을 텐데
# [01:12.34] 한 주만 더 버티면
# [01:17.80] 또 다른 한 주가 와요
# [01:27.79] 월요일이 또 와요 (CHORUS REPEAT)
# [01:31.24] 누구도 못 막아요
# [01:36.04] 커피만이 친구예요
# [01:37.93] 어떻게든 살아남아요
# Audio ends ~132s

SEGS=(
  "0|18|Korean person lying in bed late Sunday night with phone screen glowing, soft warm bedroom light, melancholic atmosphere, cinematic film grain, 9:16 vertical|일요일 밤 열한 시 반"
  "20|8|Korean person sighing in bed scrolling phone reluctantly avoiding tomorrow, warm dim bedroom light, melancholic mood, vertical|내일을 미루고 싶어"
  "28|5|TV screen showing drama ending credits in dim Korean apartment, melancholic atmosphere, soft warm light, vertical|드라마는 끝났는데"
  "33|7|close-up of bedside clock showing late hour with blurred bedroom in background, soft warm light, melancholic atmosphere, vertical|왜 시간은 안 멈추니"
  "40|4|Korean office worker in suit standing in dim Seoul subway car at dawn, weary expression looking out window, cinematic vertical|월요일이 또 와요"
  "44|4|Korean salaryman face profile expressing weary sigh, soft morning office light, melancholic mood, vertical close-up|어쩌면 좋아요"
  "48|3|hand pressing phone alarm clock at dawn in bed, soft golden window light, melancholic Monday atmosphere, vertical|주말은 너무 짧고"
  "50|4|empty Seoul conference room interior with dim warm fluorescent light and wall clock, melancholic corporate atmosphere, vertical|회의는 너무 길어요"
  "54|4|Korean commuters standing silently in packed Seoul subway car at morning, all looking down at phones, cinematic mood, vertical|출근길 지하철 안"
  "58|3|close-up of weary tired Korean office workers faces in subway, all expressionless, soft cool lighting, vertical|모두가 같은 표정"
  "60|4|Korean office worker face in subway, no smile, contemplative weary expression, soft morning light, melancholic, vertical|누구도 웃지 않아요"
  "64|8|Korean salaryman looking longingly at calendar showing Friday, office desk with laptop, warm window light, melancholic atmosphere, vertical|오늘이 금요일이면 좋을 텐데"
  "72|5|Korean office worker at desk leaning head on hands, soft warm office light from window, melancholic atmosphere, vertical|한 주만 더 버티면"
  "77|10|Korean subway escalator with crowd of commuters moving up at dawn, motion blur, cinematic warm light, melancholic atmosphere, vertical|또 다른 한 주가 와요"
  "87|4|Korean office worker boarding subway again at next Monday dawn, repetitive cycle, soft warm light, melancholic vertical|월요일이 또 와요"
  "91|5|Korean office worker standing on rainy Seoul street at night with umbrella, soft city lights bokeh, melancholic atmosphere, vertical|누구도 못 막아요"
  "96|5|coffee cup sitting on Korean office desk with steam rising, soft warm window light, melancholic atmosphere, vertical close-up|커피만이 친구예요"
  "101|31|Korean office worker walking home at night through Seoul subway tunnel, lone figure backlit, soft warm lights, melancholic perseverance, vertical|어떻게든 살아남아요"
)

echo "[STEP] generate ${#SEGS[@]} stills in parallel batches of 4"
JOBS=()
i=0
for spec in "${SEGS[@]}"; do
  i=$((i+1))
  IFS='|' read -r TS DUR PROMPT LYRIC <<< "$spec"
  IDX=$(printf '%02d' "$i")
  OUT="$WORK/stills/${IDX}.jpg"
  if [[ ! -f "$OUT" ]]; then
    ENCODED=$(python3 -c "import sys, urllib.parse; print(urllib.parse.quote(sys.argv[1]))" "$PROMPT")
    URL="https://image.pollinations.ai/prompt/${ENCODED}?width=1080&height=1920&seed=${i}88&nologo=true&model=flux"
    (curl -sSL -o "$OUT" "$URL" && echo "[STILL] $IDX done") &
    JOBS+=($!)
    if (( ${#JOBS[@]} >= 4 )); then
      for pid in "${JOBS[@]}"; do wait $pid 2>/dev/null; done
      JOBS=()
    fi
  fi
done
for pid in "${JOBS[@]}"; do wait $pid 2>/dev/null; done

echo "[STEP] make Ken Burns clips"
i=0
for spec in "${SEGS[@]}"; do
  i=$((i+1))
  IFS='|' read -r TS DUR PROMPT LYRIC <<< "$spec"
  IDX=$(printf '%02d' "$i")
  STILL="$WORK/stills/${IDX}.jpg"
  CLIP="$WORK/clips/${IDX}.mp4"
  [[ -f "$CLIP" ]] && continue
  [[ ! -s "$STILL" ]] && continue
  FPS=30; FRAMES=$((DUR * FPS))
  ZOOMEXPR=$((i % 2 == 0)) && ZOOMEXPR="zoom+0.0008" || ZOOMEXPR="zoom-0.0008"
  "$FFMPEG" -y -loglevel error \
    -loop 1 -i "$STILL" \
    -vf "scale=1620:2880:force_original_aspect_ratio=increase,crop=1620:2880,zoompan=z='${ZOOMEXPR}':d=${FRAMES}:s=1080x1920:fps=${FPS},format=yuv420p" \
    -t "$DUR" -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p -an "$CLIP"
done

echo "[STEP] concat + grade + shader + lyrics"
> "$WORK/concat.txt"
for c in "$WORK/clips/"*.mp4 ; do echo "file '$c'" >> "$WORK/concat.txt"; done
"$FFMPEG" -y -loglevel error -f concat -safe 0 -i "$WORK/concat.txt" -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p "$WORK/base.mp4"

"$FFMPEG" -y -loglevel error -i "$WORK/base.mp4" \
  -vf "eq=saturation=0.85:contrast=0.95:gamma=1.05,colorbalance=rs=0.05:gs=0.0:bs=-0.05,curves=preset=lighter" \
  -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p "$WORK/graded.mp4"

"$FFMPEG" -y -loglevel error -i "$WORK/graded.mp4" \
  -vf "gblur=sigma=20" -c:v libx264 -preset medium -crf 22 -pix_fmt yuv420p "$WORK/blur.mp4"
"$FFMPEG" -y -loglevel error -i "$WORK/graded.mp4" -i "$WORK/blur.mp4" \
  -filter_complex "[0:v][1:v]blend=all_mode=screen:all_opacity=0.35[v]" -map "[v]" \
  -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p "$WORK/shaded.mp4"

FONT=/System/Library/Fonts/AppleSDGothicNeo.ttc
DRAWTEXTS=""
for spec in "${SEGS[@]}"; do
  IFS='|' read -r TS DUR PROMPT LYRIC <<< "$spec"
  END=$((TS + DUR))
  ESC_LYRIC=$(echo "$LYRIC" | sed "s/'/\\\\'/g; s/:/\\\\:/g")
  if [[ -n "$DRAWTEXTS" ]]; then DRAWTEXTS+=","; fi
  DRAWTEXTS+="drawtext=fontfile='${FONT}':text='${ESC_LYRIC}':fontsize=88:fontcolor=white:x=(w-text_w)/2:y=h*0.42:shadowcolor=black:shadowx=4:shadowy=4:borderw=2:bordercolor=black:enable='between(t,${TS},${END})'"
done

"$FFMPEG" -y -loglevel error -i "$WORK/shaded.mp4" -vf "$DRAWTEXTS" \
  -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p -an "$WORK/with-lyrics.mp4"

mkdir -p outputs/publish/shorts-2026-05-23-batch/_hybrid-demo
"$FFMPEG" -y -loglevel error -i "$WORK/with-lyrics.mp4" -i assets/music/vocal-comedy-monday-v1.mp3 \
  -map 0:v -map 1:a -c:v copy -c:a aac -b:a 192k -shortest outputs/publish/shorts-2026-05-23-batch/_hybrid-demo/01-monday-v1-FULL-POLLINATIONS.mp4

echo "[DONE] $(ffprobe -v error -show_entries format=duration -of csv=p=0 outputs/publish/shorts-2026-05-23-batch/_hybrid-demo/01-monday-v1-FULL-POLLINATIONS.mp4 | awk '{printf "%.1f", $1}')s"
