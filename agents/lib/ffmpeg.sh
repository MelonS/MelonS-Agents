#!/usr/bin/env bash
# ffmpeg helpers for 9:16 short-form output. Source after env.sh + log.sh.
#
# --- Layout engine constants (env-overridable) -------------------------------
# Subtitle placement follows a "safe zone" model so burned captions never
# collide with platform UI (TikTok/Shorts/Reels action rail + caption strip)
# and never sit on the central subject. The geometry is fixed; only the model-
# selected window content varies. Override via .env if a platform changes its
# safe area.
: "${LAYOUT_SAFE_MARGIN_V:=280}"     # px from bottom — keeps caption above bottom UI strip
: "${LAYOUT_SAFE_MARGIN_H:=100}"     # px left/right margin — keeps caption inside horizontal safe zone
: "${LAYOUT_FONT_SIZE:=44}"          # body caption size, tuned for 1080x1920
: "${LAYOUT_FONT_NAME:=Helvetica}"   # libass picks via fontconfig
: "${LAYOUT_BOX_OPACITY_HEX:=80}"    # libass alpha for caption box: 00=opaque, FF=fully transparent; 80≈50%
: "${LAYOUT_DRAWTEXT_FONTFILE:=/System/Library/Fonts/Helvetica.ttc}"  # ffmpeg drawtext needs a real font file

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
#
# Non-speech filter:
#   STRIP_NONSPEECH=true (default) skips any segment whose text is just a
#   bracketed marker like [MUSIC], [APPLAUSE], [Laughter] — whisper emits
#   these for accessibility, but in a 30-60s short the caption box gets
#   filled with [MUSIC] when no dialogue runs, which adds noise without
#   information.  STRIP_NONSPEECH=false preserves whisper's behavior.
ffmpeg_segments_to_srt() {
  local segments="$1" win_start="$2" win_end="$3" out="$4"
  local strip="${STRIP_NONSPEECH:-true}"
  jq -r --argjson ws "$win_start" --argjson we "$win_end" --arg strip "$strip" '
    [ .[] | select(.start >= $ws and .end <= $we)
          | select($strip != "true" or (.text | test("^\\s*\\[[^\\]]*\\]\\s*$") | not)) ] |
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
# Convert SRT to ASS with an explicit PlayResX/Y matching the 1080x1920 short
# output. Without this, libass interprets Fontsize against its default 384x288
# script canvas and scales fonts up ~6.67× when rendered at 1920 height — the
# captions become enormous and overflow the frame. Setting PlayRes here makes
# Fontsize map 1:1 to output pixels, so the layout-engine constants behave
# the way they read.
ffmpeg_srt_to_ass() {
  local srt="$1" ass="$2"
  python3 - "$srt" "$ass" \
    "$LAYOUT_FONT_NAME" "$LAYOUT_FONT_SIZE" \
    "$LAYOUT_SAFE_MARGIN_V" "$LAYOUT_SAFE_MARGIN_H" \
    "$LAYOUT_BOX_OPACITY_HEX" <<'PY'
import sys, re
srt_path, ass_path, font, size, mv, mh, box_alpha = sys.argv[1:8]

def srt_to_ass_ts(t):
    # SRT: HH:MM:SS,mmm  →  ASS: H:MM:SS.cc
    h, m, rest = t.split(":")
    s, ms = rest.split(",")
    cs = int(ms) // 10
    return f"{int(h)}:{int(m):02d}:{int(s):02d}.{cs:02d}"

def escape_ass(text):
    # ASS dialogue: braces are override blocks, comma is field separator.
    return (text.replace("\\", "\\\\")
                .replace("{", "(").replace("}", ")")
                .replace(",", "\\,")
                .replace("\n", "\\N"))

events = []
with open(srt_path) as f:
    block = []
    for line in f:
        line = line.rstrip("\n")
        if line.strip() == "":
            if block:
                events.append(block)
                block = []
        else:
            block.append(line)
    if block:
        events.append(block)

dialogues = []
for blk in events:
    # Block layout: [index, "start --> end", text...].  Skip malformed.
    if len(blk) < 2:
        continue
    time_line = blk[1] if "-->" in blk[1] else (blk[0] if "-->" in blk[0] else None)
    if time_line is None:
        continue
    m = re.match(r"^([\d:,]+)\s*-->\s*([\d:,]+)", time_line)
    if not m:
        continue
    start, end = srt_to_ass_ts(m.group(1)), srt_to_ass_ts(m.group(2))
    text_lines = blk[2:] if "-->" in blk[1] else blk[1:]
    text = escape_ass("\n".join(t for t in text_lines if t))
    dialogues.append(f"Dialogue: 0,{start},{end},Default,,0,0,0,,{text}")

# Style fields (V4+): Name,Fontname,Fontsize,PrimaryColour,SecondaryColour,
# OutlineColour,BackColour,Bold,Italic,Underline,StrikeOut,ScaleX,ScaleY,
# Spacing,Angle,BorderStyle,Outline,Shadow,Alignment,MarginL,MarginR,
# MarginV,Encoding.  BorderStyle=3 = opaque box behind text using OutlineColour
# as the box fill.  OutlineColour with non-zero alpha = semi-transparent box.
header = f"""[Script Info]
ScriptType: v4.00+
PlayResX: 1080
PlayResY: 1920
WrapStyle: 0
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,{font},{size},&H00FFFFFF,&H00FFFFFF,&H{box_alpha}000000,&H{box_alpha}000000,1,0,0,0,100,100,0,0,3,10,0,2,{mh},{mh},{mv},1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""

with open(ass_path, "w") as f:
    f.write(header + "\n".join(dialogues) + "\n")
PY
}

# Single-pass render: cut [start,end), 9:16 letterbox-with-blur, burn captions
# from an inline-generated ASS file with the project's standard layout (safe
# zone + semi-transparent bounding box + optional source-attribution
# watermark).  Avoids the 3x re-encode of the cut→crop→burn chain, and uses
# the ass= filter (with an explicit PlayResX/Y) instead of subtitles=+
# force_style — the latter's font sizing is unstable across reference
# resolutions.
#
# Usage: ffmpeg_render_short <input> <start> <end> <srt> <output> [source_attribution]
#
# Layout contract (do not free-form override at call sites — change the env
# vars at the top of this file instead so every mission stays consistent):
#   - foreground video letterboxed into 9:16 with a blurred-fill background
#   - captions: bottom-center fixed (Alignment=2), inside safe zone via
#     MarginL/R/V, rendered with a semi-transparent box (BorderStyle=3 +
#     OutlineColour alpha) so they remain readable over any background
#   - source attribution (if provided): small drawtext box, top-left, low
#     opacity — survives reposts and credits the original creator
ffmpeg_render_short() {
  local input="$1" start="$2" end="$3" srt="$4" output="$5"
  local source_attribution="${6:-}"

  local duration; duration=$(awk -v s="$start" -v e="$end" 'BEGIN { printf "%.3f", e - s }')
  local srt_dir srt_base ass_path ass_base abs_input abs_output
  srt_dir="$(cd "$(dirname "$srt")" && pwd)"
  srt_base="$(basename "$srt")"
  ass_path="${srt%.srt}.ass"
  ass_base="$(basename "$ass_path")"
  abs_input="$(cd "$(dirname "$input")" && pwd)/$(basename "$input")"
  abs_output="$(cd "$(dirname "$output")" && pwd)/$(basename "$output")"

  # Generate styled ASS sidecar with the layout-engine constants baked in.
  ffmpeg_srt_to_ass "$srt" "$ass_path"

  # Stack: blurred-fill bg + centered fg + burned captions via ass= filter.
  local filter_chain="[0:v]scale=1080:1920:force_original_aspect_ratio=increase,gblur=sigma=15,crop=1080:1920[bg];[0:v]scale=1080:1920:force_original_aspect_ratio=decrease[fg];[bg][fg]overlay=(W-w)/2:(H-h)/2,setsar=1,ass=${ass_base}"

  # Source attribution overlay — only if caller supplied a string AND a usable
  # font file exists. Drawtext can't render without a fontfile, so we degrade
  # gracefully on hosts without the macOS system font.
  if [[ -n "$source_attribution" ]]; then
    if [[ -f "$LAYOUT_DRAWTEXT_FONTFILE" ]]; then
      local attr_file="$srt_dir/source-attribution.txt"
      printf '%s' "$source_attribution" > "$attr_file"
      filter_chain="${filter_chain},drawtext=fontfile=${LAYOUT_DRAWTEXT_FONTFILE}:textfile=${attr_file}:fontsize=22:fontcolor=white@0.75:box=1:boxcolor=black@0.45:boxborderw=10:x=40:y=40"
    else
      echo "⚠ LAYOUT_DRAWTEXT_FONTFILE not found ($LAYOUT_DRAWTEXT_FONTFILE) — skipping source-attribution overlay" >&2
    fi
  fi

  (
    cd "$srt_dir"
    "$FFMPEG_BIN" -y -loglevel error -ss "$start" -i "$abs_input" -t "$duration" \
      -filter_complex "$filter_chain" \
      -c:v h264_videotoolbox -b:v 2500k -maxrate 3000k -bufsize 4M -realtime 0 -allow_sw 1 \
      -c:a aac -b:a 128k -movflags +faststart \
      "$abs_output"
  )
}
