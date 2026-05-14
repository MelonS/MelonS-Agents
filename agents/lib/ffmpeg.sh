#!/usr/bin/env bash
# ffmpeg helpers for 9:16 short-form output. Source after env.sh + log.sh.

# Probe duration in seconds.
ffmpeg_duration() {
  "$FFPROBE_BIN" -v error -show_entries format=duration -of default=nw=1:nk=1 "$1"
}

# Probe display aspect ratio (e.g. "16:9", "9:16").
ffmpeg_aspect() {
  "$FFPROBE_BIN" -v error -select_streams v:0 -show_entries stream=width,height \
    -of csv=p=0 "$1" | awk -F, '{ printf "%d:%d", $1, $2 }'
}

# Cut a segment [start, end) without re-encoding when possible.
# Usage: ffmpeg_cut <input> <start> <end> <output>
ffmpeg_cut() {
  local input="$1" start="$2" end="$3" output="$4"
  local duration
  duration=$(awk -v s="$start" -v e="$end" 'BEGIN { printf "%.3f", e - s }')
  "$FFMPEG_BIN" -y -loglevel error -ss "$start" -i "$input" -t "$duration" \
    -c:v h264_videotoolbox -b:v 2500k -maxrate 3000k -bufsize 4M -realtime 0 -allow_sw 1 -c:a aac -b:a 128k -movflags +faststart \
    "$output"
}

# Crop/scale to 9:16 (1080x1920) — fits content centered, blurs background.
# Usage: ffmpeg_crop_9_16 <input> <output>
ffmpeg_crop_9_16() {
  local input="$1" output="$2"
  "$FFMPEG_BIN" -y -loglevel error -i "$input" -filter_complex "\
[0:v]scale=1080:1920:force_original_aspect_ratio=increase,boxblur=20:5,crop=1080:1920[bg]; \
[0:v]scale=1080:1920:force_original_aspect_ratio=decrease[fg]; \
[bg][fg]overlay=(W-w)/2:(H-h)/2,setsar=1" \
    -c:v h264_videotoolbox -b:v 2500k -maxrate 3000k -bufsize 4M -realtime 0 -allow_sw 1 -c:a copy -movflags +faststart \
    "$output"
}

# Burn SRT subtitles into video using the subtitles filter (requires libass).
# chdir + relative basename avoids the absolute-path-with-colon parser issue.
# Usage: ffmpeg_burn_srt <input> <srt> <output>
ffmpeg_burn_srt() {
  local input="$1" srt="$2" output="$3"
  local srt_dir srt_base abs_input abs_output
  srt_dir="$(cd "$(dirname "$srt")" && pwd)"
  srt_base="$(basename "$srt")"
  abs_input="$(cd "$(dirname "$input")" && pwd)/$(basename "$input")"
  abs_output="$(cd "$(dirname "$output")" && pwd)/$(basename "$output")"
  (
    cd "$srt_dir"
    "$FFMPEG_BIN" -y -loglevel error -i "$abs_input" \
      -vf "subtitles=${srt_base}:force_style=Fontname=Helvetica\,Fontsize=22\,PrimaryColour=&H00FFFFFF\,OutlineColour=&H80000000\,BorderStyle=3\,Outline=2\,MarginV=80" \
      -c:v h264_videotoolbox -b:v 2500k -maxrate 3000k -bufsize 4M -realtime 0 -allow_sw 1 -c:a copy -movflags +faststart \
      "$abs_output"
  )
}


# Convert whisper segments JSON → SRT in the given time window.
# Usage: ffmpeg_segments_to_srt <segments_json> <window_start> <window_end> <out.srt>
ffmpeg_segments_to_srt() {
  local segments="$1" win_start="$2" win_end="$3" out="$4"
  jq -r --argjson ws "$win_start" --argjson we "$win_end" '
    [ .[] | select(.start >= $ws and .end <= $we) ] |
    to_entries[] |
    "\(.key + 1)\n" +
    ( (.value.start - $ws) as $s | (.value.end - $ws) as $e |
      ( ($s | floor / 3600 | floor) as $h |
        (($s - $h*3600) / 60 | floor) as $m |
        (($s - $h*3600 - $m*60)) as $sec |
        ($sec | floor) as $si |
        (($sec - $si) * 1000 | floor) as $ms |
        "\($h | tostring | (if length < 2 then "0" + . else . end)):\($m | tostring | (if length < 2 then "0" + . else . end)):\($si | tostring | (if length < 2 then "0" + . else . end)),\($ms | tostring | (if length < 3 then ("000"[0:(3 - length)]) + . else . end))" ) +
      " --> " +
      ( ($e | floor / 3600 | floor) as $h2 |
        (($e - $h2*3600) / 60 | floor) as $m2 |
        (($e - $h2*3600 - $m2*60)) as $sec2 |
        ($sec2 | floor) as $si2 |
        (($sec2 - $si2) * 1000 | floor) as $ms2 |
        "\($h2 | tostring | (if length < 2 then "0" + . else . end)):\($m2 | tostring | (if length < 2 then "0" + . else . end)):\($si2 | tostring | (if length < 2 then "0" + . else . end)),\($ms2 | tostring | (if length < 3 then ("000"[0:(3 - length)]) + . else . end))" )
    ) + "\n" + .value.text + "\n"
  ' "$segments" > "$out"
}
# Single-pass render: cut [start,end), 9:16 letterbox-with-blur, burn SRT.
# Avoids the 3x re-encode of the cut→crop→burn chain.
# Usage: ffmpeg_render_short <input> <start> <end> <srt> <output>
ffmpeg_render_short() {
  local input="$1" start="$2" end="$3" srt="$4" output="$5"
  local duration; duration=$(awk -v s="$start" -v e="$end" 'BEGIN { printf "%.3f", e - s }')
  local srt_dir srt_base abs_input abs_output
  srt_dir="$(cd "$(dirname "$srt")" && pwd)"
  srt_base="$(basename "$srt")"
  abs_input="$(cd "$(dirname "$input")" && pwd)/$(basename "$input")"
  abs_output="$(cd "$(dirname "$output")" && pwd)/$(basename "$output")"
  (
    cd "$srt_dir"
    "$FFMPEG_BIN" -y -loglevel error -ss "$start" -i "$abs_input" -t "$duration" \
      -filter_complex "[0:v]scale=1080:1920:force_original_aspect_ratio=increase,gblur=sigma=15,crop=1080:1920[bg]; [0:v]scale=1080:1920:force_original_aspect_ratio=decrease[fg]; [bg][fg]overlay=(W-w)/2:(H-h)/2,setsar=1,subtitles=${srt_base}" \
      -c:v h264_videotoolbox -b:v 2500k -maxrate 3000k -bufsize 4M -realtime 0 -allow_sw 1 \
      -c:a aac -b:a 128k -movflags +faststart \
      "$abs_output"
  )
}
