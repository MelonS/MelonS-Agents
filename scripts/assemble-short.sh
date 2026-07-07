#!/usr/bin/env bash
# assemble-short.sh — reusable vertical-short assembler.
# Pipeline (field-proven on the black-hole & Ronaldo tribute shorts, 2026-07):
#   concat cNN.mp4 cuts -> mix BGM sidechain-ducked under narration (+ BGM tail)
#   -> loudnorm -14 LUFS (YouTube) -> scale/crop 1080x1920 + burn ASS captions -> h264_nvenc.
#
# Usage:
#   scripts/assemble-short.sh <clips_dir> <narration.wav> <bgm.(mp3|wav)> <captions.ass|-> <fontsdir|-> <out.mp4> [bgm_vol]
#   - clips_dir must contain cNN.mp4 (c01,c02,...) at a uniform resolution.
#   - captions.ass / fontsdir may be "-" to skip caption burn-in.
#   - bgm_vol defaults to 0.55 (pre-duck BGM level).
# Notes: concat uses paths relative to clips_dir (Windows ffmpeg can't read MSYS /g/ paths).
set +e
CLIPS_DIR="$1"; NAR="$2"; BGM="$3"; ASS="$4"; FONTS="$5"; OUT="$6"; BGMVOL="${7:-0.55}"
if [ -z "$OUT" ]; then
  echo "usage: assemble-short.sh <clips_dir> <narration.wav> <bgm> <captions.ass|-> <fontsdir|-> <out.mp4> [bgm_vol]" >&2
  exit 1
fi
OUTDIR=$(dirname "$OUT"); mkdir -p "$OUTDIR"

# 1) concat (relative paths inside clips_dir)
( cd "$CLIPS_DIR" || exit 1
  : > _concat.txt
  for f in c[0-9][0-9].mp4; do [ -f "$f" ] && echo "file '$f'" >> _concat.txt; done
  ffmpeg -y -hide_banner -loglevel error -f concat -safe 0 -i _concat.txt -c copy _video.mp4 )
VIDEO="$CLIPS_DIR/_video.mp4"
VD=$(ffprobe -v error -show_entries format=duration -of default=nk=1:nw=1 "$VIDEO")
echo "concat: ${VD}s"

# 2) audio mix — BGM ducked under narration; apad keeps BGM tail after narration ends
ffmpeg -y -hide_banner -loglevel error -i "$BGM" -i "$NAR" \
  -filter_complex "[1:a]aformat=channel_layouts=stereo,apad,asplit=2[nar][key];[0:a]aformat=channel_layouts=stereo,volume=${BGMVOL}[bg];[bg][key]sidechaincompress=threshold=0.035:ratio=8:attack=15:release=450[bgd];[bgd][nar]amix=inputs=2:normalize=0:duration=first,alimiter=limit=0.97[m]" \
  -map "[m]" -t "$VD" -ar 44100 -ac 2 "$OUTDIR/_mix.wav"

# 3) loudnorm -14 LUFS (YouTube reference)
ffmpeg -y -hide_banner -loglevel error -i "$OUTDIR/_mix.wav" -af "loudnorm=I=-14:TP=-1.0:LRA=11" -ar 44100 -ac 2 "$OUTDIR/_audio.wav"

# 4) render 1080x1920 (+ optional caption burn-in)
VF="scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920"
if [ -n "$ASS" ] && [ "$ASS" != "-" ] && [ -f "$ASS" ]; then
  VF="$VF,ass=$ASS"
  [ -n "$FONTS" ] && [ "$FONTS" != "-" ] && VF="$VF:fontsdir=$FONTS"
fi
ffmpeg -y -hide_banner -loglevel error -i "$VIDEO" -i "$OUTDIR/_audio.wav" \
  -vf "$VF" -map 0:v -map 1:a -c:v h264_nvenc -cq 20 -preset p5 -pix_fmt yuv420p -c:a aac -b:a 192k -shortest "$OUT"

rm -f "$CLIPS_DIR/_video.mp4" "$CLIPS_DIR/_concat.txt" "$OUTDIR/_mix.wav" "$OUTDIR/_audio.wav"
if [ -f "$OUT" ]; then
  echo "OK $OUT ($(ffprobe -v error -show_entries format=duration -of default=nk=1:nw=1 "$OUT")s)"
  echo "loudness: $(ffmpeg -hide_banner -i "$OUT" -af ebur128 -f null - 2>&1 | grep -A4 Summary | awk '/I:/{print $2, $3}')"
else
  echo "FAIL: render"
fi
