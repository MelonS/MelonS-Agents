#!/usr/bin/env bash
# Audio-reactive grading: modulate saturation/exposure based on the
# music's actual RMS envelope (not a fixed sin wave like saturation_pulse).
#
# Format #8 from docs/research/2026-05-21-music-shorts-formats-landscape.md
# — "Single-take long-shot with audio-reactive grading".
#
# How it works:
#   1. ffmpeg astats on the music file → per-frame RMS_level dB values
#      written to text file
#   2. Parse text → generate sendcmd .txt with timed `eq saturation`
#      commands (1.0 baseline + 0.5 * normalized RMS)
#   3. Apply to video via filter_complex `sendcmd=f=cmd.txt,eq=...`
#
# Usage:
#   scripts/music-video-audio-reactive.sh <input.mp4> <music.mp3> <output.mp4>
#
# The input mp4 typically has the music already baked in.  The separate
# music.mp3 is needed because we can't extract the music back out at
# enough fidelity for clean RMS analysis (mp4's AAC re-encode loses
# transient detail).
#
# Best for genres in research's "Format #8" target:
#   - shoegaze (audio-reactive grading instead of cuts)
#   - drone (slow grading roll matches drone evolution)
#   - house (saturation pulses on the kick)
#
# WARNING: This is a v1 scaffold.  Tuning the response curve (how
# aggressively saturation reacts to RMS) is per-genre work.  Default
# values are conservative.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
# shellcheck disable=SC1091
source "$PWD/agents/lib/env.sh" 2>/dev/null || true

SRC="${1:-}"
MUSIC="${2:-}"
DST="${3:-}"

if [[ -z "$SRC" || -z "$MUSIC" || -z "$DST" ]]; then
  echo "usage: $0 <input.mp4> <music.mp3> <output.mp4>" >&2
  exit 64
fi
[[ -f "$SRC" ]]   || { echo "❌ input not found: $SRC" >&2; exit 64; }
[[ -f "$MUSIC" ]] || { echo "❌ music not found: $MUSIC" >&2; exit 64; }
# §8 exception: ffmpeg parameter-expansion default — same pattern as
# scripts/music-video-shaders.sh:103.  Registered in operator-contract.md §8.
FFMPEG="${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}"

TMPDIR_LOCAL=$(mktemp -d -t mv-areactive-XXXX)
trap "rm -rf '$TMPDIR_LOCAL'" EXIT

# 1. Extract per-frame RMS dB from music
#    astats with metadata=1:reset=0.04 → reports every 40ms (~25 Hz)
RMS_TXT="$TMPDIR_LOCAL/rms.txt"
"$FFMPEG" -y -loglevel error -i "$MUSIC" \
  -af "astats=metadata=1:reset=0.04,ametadata=mode=print:key=lavfi.astats.Overall.RMS_level:file=$RMS_TXT" \
  -f null - 2>/dev/null

if [[ ! -s "$RMS_TXT" ]]; then
  echo "❌ failed to extract RMS — astats output empty" >&2
  exit 1
fi

# 2. Parse RMS dB → normalized 0-1 amplitude, generate sendcmd file
#    astats output format:
#      frame:0    pts:0       pts_time:0
#      lavfi.astats.Overall.RMS_level=-23.45
#      ...
#    RMS_level is in dB; typical range -50 (silent) to -5 (peak).
#    Map [-40, -5] → [0, 1] for modulation factor.
CMD_TXT="$TMPDIR_LOCAL/cmd.txt"
python3 <<PY > "$CMD_TXT"
import re
rms_db = []
ts = []
with open("$RMS_TXT") as f:
    t = None
    for line in f:
        m = re.match(r'frame:\d+\s+pts:\S+\s+pts_time:([\d.]+)', line)
        if m:
            t = float(m.group(1))
            continue
        m = re.match(r'lavfi\.astats\.Overall\.RMS_level=(-?[\d.]+|nan|-?inf)', line)
        if m and t is not None:
            v = m.group(1)
            if v in ('nan', '-inf', 'inf'):
                continue
            rms_db.append(float(v))
            ts.append(t)

# Normalize -40dB..-5dB → 0..1
def norm(db):
    x = (db - (-40)) / 35.0
    if x < 0: x = 0
    if x > 1: x = 1
    return x

# Emit eq saturation commands.  Baseline 1.0, modulate by ±0.4 * norm.
# Use exposure / brightness too for richer reactivity? Conservative v1: sat only.
# sendcmd line format: `TIME [FLAGS] TARGET COMMAND ARG;`  (semicolon terminator)
# Downsample to ~10 Hz for compactness (RMS at 25 Hz is overkill for video)
for i, (t, db) in enumerate(zip(ts, rms_db)):
    if i % 3 != 0:  # keep every 3rd sample → ~8 Hz
        continue
    sat = 0.8 + 0.6 * norm(db)
    print(f"{t:.3f} eq saturation {sat:.3f};")
PY

if [[ ! -s "$CMD_TXT" ]]; then
  echo "❌ failed to build sendcmd file" >&2
  exit 1
fi

CMD_COUNT=$(wc -l < "$CMD_TXT" | tr -d ' ')
echo "→ extracted $CMD_COUNT RMS-driven saturation commands"

# 3. Apply to video via sendcmd + eq filter
#    sendcmd reads cmd file; eq has saturation init=1.0
"$FFMPEG" -y -loglevel warning -stats \
  -i "$SRC" \
  -filter_complex "[0:v]sendcmd=f=${CMD_TXT},eq=saturation=1.0[vout]" \
  -map "[vout]" -map 0:a \
  -c:v libx264 -preset medium -crf 22 -c:a copy "$DST"

dur=$("${FFPROBE_BIN:-/opt/homebrew/bin/ffprobe}" -v error -show_entries format=duration -of csv=p=0 "$DST" 2>/dev/null | awk '{printf "%.1f", $1}')
size=$(du -h "$DST" 2>/dev/null | awk '{print $1}')
echo "✓ audio-reactive: $DST (${dur}s, $size, $CMD_COUNT modulation commands)"
