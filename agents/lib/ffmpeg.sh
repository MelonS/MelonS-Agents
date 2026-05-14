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
    -c:v libx264 -preset veryfast -crf 20 -c:a aac -b:a 128k -movflags +faststart \
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
    -c:v libx264 -preset veryfast -crf 20 -c:a copy -movflags +faststart \
    "$output"
}

# NOTE: This bottle of ffmpeg (homebrew, 8.1.1) was built without libass
# and without drawtext (no libfreetype/fontconfig). Burning captions
# requires a richer build; see docs/known-limitations.md.
# For now we just stream-copy the video unchanged so the pipeline still
# produces a deliverable. The SRT file is shipped alongside.
# Usage: ffmpeg_burn_srt <input> <srt> <output>
ffmpeg_burn_srt() {
  local input="$1" srt="$2" output="$3"
  "$FFMPEG_BIN" -y -loglevel error -i "$input" -c copy -movflags +faststart "$output"
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
