#!/usr/bin/env bash
# Build full-length Pollinations music video — replaces ALL B-roll with AI generated.
# Hard-coded for 작은 손 (folk-small-hand) demo. Other songs would need their own prompt lists.
set -euo pipefail
cd "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"  # repo root (was hardcoded /Users/melons/ai; §8)
source .env 2>/dev/null
FFMPEG="${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}"
WORK=/tmp/full-pollinations
mkdir -p "$WORK/stills" "$WORK/clips"

# Each entry: timestamp_start|duration|prompt|lyric_text
# Spans match LRC, lyric shows on screen during its full duration
SEGS=(
  "0|8|narrow foreign cobblestone street at dusk, soft warm sunset light, traveler perspective, melancholic atmosphere, sepia tones, fine film grain, 9:16 vertical|낯선 길 위에서"
  "8|5|rough handmade clay cup on stone surface, steam rising, warm low light, intimate atmosphere, melancholic mood, sepia warm tones, vertical composition|투박한 잔 하나에"
  "13|17|warm tea steam rising from clay cup, soft golden light filtering through, dreamy bokeh, melancholic moment, cinematic 9:16 vertical|따뜻한 차 한 모금"
  "30|5|silhouette of person holding cup against sunset window, melancholic atmosphere, warm orange backlit, vertical composition|잠깐의 위로였어"
  "35|9|small child silent hand reaching out in foreign street, blurred background of passersby, melancholic atmosphere, sepia tones, vertical|말이 없는 작은 손"
  "44|8|small hand stopped mid-air reaching toward sky in shadow, sunset bokeh background, melancholic emotional weight, cinematic vertical|허공에 멈춘 손"
  "53|4|crowd of adults walking past dismissively, soft warm sunset, blurred motion, melancholic atmosphere, vertical composition|어른들은 손사래 쳤고"
  "57|5|footsteps walking past on dusty street, side view low angle, warm sunset light, melancholic atmosphere, fine film grain, vertical|내 걸음도 지나갔어"
  "62|10|adult hand opening palm holding single old coin under warm sunset, blurred street market, melancholic contemplation, sepia tones, vertical|주려는 내 마음이"
  "73|7|backlit silhouette of child hand reaching toward glowing window light, sepia warm bokeh, melancholic atmosphere, vertical composition|너를 가두는 일이 될까"
  "80|5|adult silhouette walking past in blurred crowd hand restrained at side, soft warm sunset, melancholic mood, shallow depth of field, vertical|주지 않은 그 손이"
  "85|7|empty rough clay cup on stone with godrays of light from above, contemplative atmosphere, melancholic warm tones, vertical|너를 더 사랑한 걸까"
  "92|7|elder traveler walking together with younger person on foreign street, side view, warm sunset light, contemplative atmosphere, vertical|함께 걷던 어른이 말했어"
  "99|10|profile close-up of weathered face of elder in soft warm light, wisdom and contemplation, cinematic shallow depth, vertical|오래 본 사람이 말했지"
  "108|4|contemplative child face in soft warm sunset light, melancholic emotional weight, intimate close-up, vertical|가난이 너를 만든 게 아니라"
  "112|8|silhouettes of crowd walking past in city street at sunset, atmospheric blurred motion, melancholic mood, vertical|우리가 너를 만든 거라고"
  "120|10|person looking down in deep contemplation, soft warm window light, melancholic understanding, vertical composition|그 말이 무거웠고"
  "130|8|person looking up slowly with dawning realization, soft warm light bathing face, contemplative atmosphere, vertical|나는 그제야 알았어"
  "138|5|adult hand extending forward in soft warm light, blurred background of street, melancholic atmosphere, vertical|내가 건네던 손이"
  "143|8|silhouette of hand reaching against warm bokeh background, contemplative atmosphere, melancholic mood, vertical composition|누굴 위한 손이었을까"
  "151|6|adult hand opening palm with coin reflecting sunset light, blurred crowd background, melancholic introspection, vertical|주려는 내 마음이"
  "157|9|reflection of person face in window glass with city lights bokeh, melancholic contemplation, warm tones, vertical|누구를 위한 거였을까"
  "166|5|small child empty hand close-up resting on warm stone, soft golden light from above, intimate atmosphere, vertical|그 작은 손 위에"
  "171|7|adult hand slowly closing into fist in warm sunset light, melancholic realization, soft bokeh background, vertical|내 욕심이 있었던 걸까"
  "178|6|warm tea steam rising from clay cup in dim warm light, melancholic intimate atmosphere, vertical composition|잠깐의 위로가"
  "184|11|silhouette walking away into warm sunset golden light, narrow street, melancholic farewell atmosphere, sepia tones, vertical|내 안에 남았어"
  "195|5|small empty hand close-up in soft warm light, fading into shadows, melancholic atmosphere, vertical|말이 없던 그 손도"
  "200|22|warm sunset slowly fading on empty cobblestone street, contemplative melancholic farewell, very slow gentle camera motion, sepia warm tones, vertical|내 안에 남았어"
)

echo "[STEP] generate ${#SEGS[@]} Pollinations stills in parallel batches of 4"
JOBS=()
i=0
for spec in "${SEGS[@]}"; do
  i=$((i+1))
  IFS='|' read -r TS DUR PROMPT LYRIC <<< "$spec"
  IDX=$(printf '%02d' "$i")
  OUT="$WORK/stills/${IDX}.jpg"

  if [[ ! -f "$OUT" ]]; then
    ENCODED=$(python3 -c "import sys, urllib.parse; print(urllib.parse.quote(sys.argv[1]))" "$PROMPT")
    URL="https://image.pollinations.ai/prompt/${ENCODED}?width=1080&height=1920&seed=${i}77&nologo=true&model=flux"
    (curl -sSL -o "$OUT" "$URL" && echo "[STILL] $IDX done") &
    JOBS+=($!)
    if (( ${#JOBS[@]} >= 4 )); then
      for pid in "${JOBS[@]}"; do wait $pid 2>/dev/null; done
      JOBS=()
    fi
  fi
done
for pid in "${JOBS[@]}"; do wait $pid 2>/dev/null; done
echo "[STEP] all stills fetched"
ls "$WORK/stills/" | head -30

echo ""
echo "[STEP] convert each still to Ken Burns clip"
i=0
for spec in "${SEGS[@]}"; do
  i=$((i+1))
  IFS='|' read -r TS DUR PROMPT LYRIC <<< "$spec"
  IDX=$(printf '%02d' "$i")
  STILL="$WORK/stills/${IDX}.jpg"
  CLIP="$WORK/clips/${IDX}.mp4"
  [[ -f "$CLIP" ]] && continue
  [[ ! -s "$STILL" ]] && { echo "[SKIP] $IDX no still"; continue; }

  FPS=30
  FRAMES=$((DUR * FPS))
  # Alternating zoom-in (even i) vs zoom-out (odd i) for variety
  if (( i % 2 == 0 )); then
    ZOOMEXPR="zoom+0.0008"
  else
    ZOOMEXPR="zoom-0.0008"
  fi

  "$FFMPEG" -y -loglevel error \
    -loop 1 -i "$STILL" \
    -vf "scale=1620:2880:force_original_aspect_ratio=increase,crop=1620:2880,zoompan=z='${ZOOMEXPR}':d=${FRAMES}:s=1080x1920:fps=${FPS},format=yuv420p" \
    -t "$DUR" -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p -an "$CLIP"
done
echo "[STEP] all clips done"
ls "$WORK/clips/" | wc -l | awk '{print "  total clips: " $1}'

echo ""
echo "[STEP] concat all clips into base.mp4"
> "$WORK/concat.txt"
for c in "$WORK/clips/"*.mp4 ; do
  echo "file '$c'" >> "$WORK/concat.txt"
done
"$FFMPEG" -y -loglevel error -f concat -safe 0 -i "$WORK/concat.txt" -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p "$WORK/base.mp4"

echo ""
echo "[STEP] apply grade + halation shader"
"$FFMPEG" -y -loglevel error -i "$WORK/base.mp4" \
  -vf "eq=saturation=0.85:contrast=0.95:gamma=1.05,colorbalance=rs=0.05:gs=0.0:bs=-0.05,curves=preset=lighter" \
  -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p "$WORK/graded.mp4"

"$FFMPEG" -y -loglevel error -i "$WORK/graded.mp4" \
  -vf "gblur=sigma=20" -c:v libx264 -preset medium -crf 22 -pix_fmt yuv420p "$WORK/blur.mp4"
"$FFMPEG" -y -loglevel error -i "$WORK/graded.mp4" -i "$WORK/blur.mp4" \
  -filter_complex "[0:v][1:v]blend=all_mode=screen:all_opacity=0.35[v]" -map "[v]" \
  -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p "$WORK/shaded.mp4"

echo ""
echo "[STEP] add lyric overlay with proper timestamps"
FONT=/System/Library/Fonts/AppleSDGothicNeo.ttc
# Build drawtext filter from SEGS
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

echo ""
echo "[STEP] add original audio from base mp3"
mkdir -p outputs/publish/shorts-2026-05-23-batch/_hybrid-demo
"$FFMPEG" -y -loglevel error -i "$WORK/with-lyrics.mp4" -i assets/music/vocal-folk-small-hand-folk-v1.mp3 \
  -map 0:v -map 1:a -c:v copy -c:a aac -b:a 192k -shortest outputs/publish/shorts-2026-05-23-batch/_hybrid-demo/05-smallhand-folk-v1-FULL-POLLINATIONS.mp4

echo ""
echo "[DONE] FULL POLLINATIONS output:"
ffprobe -v error -show_entries stream=width,height,pix_fmt -show_entries format=duration -of default=noprint_wrappers=1 outputs/publish/shorts-2026-05-23-batch/_hybrid-demo/05-smallhand-folk-v1-FULL-POLLINATIONS.mp4
SZ=$(stat -f%z outputs/publish/shorts-2026-05-23-batch/_hybrid-demo/05-smallhand-folk-v1-FULL-POLLINATIONS.mp4 | awk '{printf "%.1fMB", $1/1024/1024}')
echo "size: $SZ"
