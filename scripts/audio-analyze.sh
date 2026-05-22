#!/usr/bin/env bash
# audio-analyze.sh — per-second RMS energy analysis of an audio track,
# emitting two artifacts:
#
#   <out_prefix>.rms.csv   — t_seconds,rms_db per second
#   <out_prefix>.drops.txt — timestamps of detected "drops" (top 10%
#                             RMS windows of ≥ 2s sustained energy)
#
# Per docs/research/2026-05-22-music-video-pro-practices.md §6:
# "drop" events drive zoom-on-drop / camera-shake / color-burst
# accents in pro music videos.  Currently the pipeline's shader
# events fire on every drum onset; this gives a sparser "loudest
# moments" set that the shader chain can use as a different gate
# mode (`MUSIC_VIDEO_SHADER_GATE=drops` — future commit).
#
# Implementation: ffmpeg astats filter emits per-frame RMS to
# stderr; we sample at 1-second windows via ametadata + reset.
# librosa would give cleaner spectral analysis but is not
# installed; pure ffmpeg keeps this portable.
#
# Usage:
#   scripts/audio-analyze.sh <audio.mp3> <out_prefix>

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh" 2>/dev/null || true

SRC="${1:-}"
OUT_PREFIX="${2:-}"

if [[ -z "$SRC" || -z "$OUT_PREFIX" ]]; then
  echo "usage: $0 <audio> <out_prefix>" >&2
  exit 64
fi
[[ -f "$SRC" ]] || { echo "audio not found: $SRC" >&2; exit 64; }

RMS_CSV="${OUT_PREFIX}.rms.csv"
DROPS_TXT="${OUT_PREFIX}.drops.txt"

# Step 1: emit per-frame RMS via astats with reset=1 (per-frame
# statistics).  Capture stderr because astats prints to stderr.
RAW=$(mktemp)
trap "rm -f '$RAW'" EXIT

"${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}" -hide_banner -nostats -i "$SRC" \
  -af "aresample=22050,asetnsamples=n=22050:p=0,astats=metadata=1:reset=1,ametadata=print:key=lavfi.astats.Overall.RMS_level:file=-" \
  -f null - 2>/dev/null > "$RAW"

# astats output format:
#   frame:0    pts:0       pts_time:0
#   lavfi.astats.Overall.RMS_level=-23.456
#   frame:1    pts:22050   pts_time:1
#   lavfi.astats.Overall.RMS_level=-22.123
#   ...

python3 - "$RAW" "$RMS_CSV" "$DROPS_TXT" <<'PY'
import sys, re

raw_path, rms_csv, drops_txt = sys.argv[1], sys.argv[2], sys.argv[3]

samples = []
t = None
with open(raw_path) as f:
    for line in f:
        line = line.strip()
        m = re.match(r"frame:\d+\s+pts:\d+\s+pts_time:([\d.]+)", line)
        if m:
            t = float(m.group(1))
            continue
        m = re.match(r"lavfi\.astats\.Overall\.RMS_level=(-?[\d.]+|inf|-inf|nan)", line)
        if m and t is not None:
            val = m.group(1)
            if val in ("inf", "-inf", "nan"):
                continue
            samples.append((t, float(val)))
            t = None

if not samples:
    print("[audio-analyze] no RMS samples extracted", file=sys.stderr)
    open(rms_csv, "w").close()
    open(drops_txt, "w").close()
    sys.exit(0)

# Write csv
with open(rms_csv, "w") as f:
    f.write("t_seconds,rms_db\n")
    for t, db in samples:
        f.write(f"{t:.2f},{db:.2f}\n")

# Drop detection: top-10% RMS samples, then cluster consecutive ones
# into sustained windows ≥ 2s.  Emit window-start timestamps.
sorted_db = sorted(s[1] for s in samples)
n_top = max(1, len(sorted_db) // 10)
threshold = sorted_db[-n_top]
hot = [t for t, db in samples if db >= threshold]

# Cluster: group hot timestamps that are within 1.5s of each other.
drops = []
if hot:
    window_start = hot[0]
    last = hot[0]
    for t in hot[1:]:
        if t - last <= 1.5:
            last = t
        else:
            if last - window_start >= 2.0:
                drops.append(window_start)
            window_start = t
            last = t
    if last - window_start >= 2.0:
        drops.append(window_start)

with open(drops_txt, "w") as f:
    for t in drops:
        f.write(f"{t:.2f}\n")

print(f"[audio-analyze] {len(samples)} RMS samples → {len(drops)} drops", file=sys.stderr)
print(f"  threshold RMS={threshold:.1f} dB", file=sys.stderr)
print(f"  drops at: {' '.join(f'{t:.1f}s' for t in drops)}", file=sys.stderr)
PY

echo "[audio-analyze] rms → $RMS_CSV"
echo "[audio-analyze] drops → $DROPS_TXT"
