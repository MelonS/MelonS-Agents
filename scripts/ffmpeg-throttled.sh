#!/usr/bin/env bash
# CPU-throttled ffmpeg wrapper.
#
# Caps CPU usage at FFMPEG_CPU_LIMIT_PCT (default 80) percent of total
# system CPU.  Also nices to lowest priority and caps thread count so
# ffmpeg can't peg every core even momentarily.
#
# Why this exists:
#   ffmpeg + libx264 will saturate every core (100% × N).  Sustained
#   full-load encoding overheats laptops and triggers thermal throttle,
#   which is worse than running at a self-imposed lower cap.
#   Operator request 2026-05-21 02:44 KST: "맥북이 터질 것 같아".
#
# Usage as drop-in for ffmpeg:
#   scripts/ffmpeg-throttled.sh -i in.mp4 ... out.mp4
#
# Or via FFMPEG_BIN env var (existing music-video pipeline picks this
# up via agents/lib/env.sh):
#   export FFMPEG_BIN=$PWD/scripts/ffmpeg-throttled.sh
#   bash scripts/music-video-auto.sh assets/music/...
#
# Tunables (env vars):
#   FFMPEG_CPU_LIMIT_PCT   target CPU % of total system  (default 80)
#   FFMPEG_THREADS         max threads ffmpeg may use    (default = ncpu - 2)
#   FFMPEG_NICE            nice level                    (default 19, lowest)
#   FFMPEG_REAL_BIN        path to actual ffmpeg         (default discovered)

set -uo pipefail

# Discover real ffmpeg
REAL=${FFMPEG_REAL_BIN:-}
if [[ -z "$REAL" ]]; then
  for c in /opt/homebrew/bin/ffmpeg /usr/local/bin/ffmpeg /usr/bin/ffmpeg; do
    [[ -x "$c" ]] && { REAL="$c"; break; }
  done
fi
if [[ -z "$REAL" || ! -x "$REAL" ]]; then
  echo "ffmpeg-throttled: real ffmpeg not found (set FFMPEG_REAL_BIN)" >&2
  exit 1
fi

NCPU=$(sysctl -n hw.ncpu 2>/dev/null || nproc 2>/dev/null || echo 8)
LIMIT_PCT="${FFMPEG_CPU_LIMIT_PCT:-80}"
# cpulimit -l is percentage of ONE core × 100.  For 80% of N-core system,
# pass NCPU * LIMIT_PCT.  (E.g. 8 cores × 80 = 640.)
CPULIMIT_VAL=$(( NCPU * LIMIT_PCT ))

# Default threads = ncpu - 2 (leave headroom)
THREADS="${FFMPEG_THREADS:-$(( NCPU > 2 ? NCPU - 2 : 1 ))}"
NICE_LVL="${FFMPEG_NICE:-19}"

# Inject -threads if not already specified in args (don't override operator)
HAS_THREADS=0
for arg in "$@"; do
  [[ "$arg" == "-threads" ]] && HAS_THREADS=1
done

CMD=(nice -n "$NICE_LVL" "$REAL")
[[ $HAS_THREADS -eq 0 ]] && CMD+=("-threads" "$THREADS")
CMD+=("$@")

# Run under cpulimit if available; fall back to bare nice if not.
if command -v cpulimit >/dev/null 2>&1; then
  # cpulimit forks and watches; -i means include child PIDs
  exec cpulimit -l "$CPULIMIT_VAL" -i -- "${CMD[@]}"
else
  exec "${CMD[@]}"
fi
