#!/usr/bin/env bash
# Regression test for scripts/music-video-trim.sh.
#
# Asserts:
#   1. no-args → exit 64
#   2. --short on a 10s source (< 60s) → copies unchanged
#   3. --duration=5 on a 10s source → trims to 5s
#   4. --shorts-max on a 200s source → trims to 180s
#   5. --start=2 --duration=3 → re-encodes 3s starting at t=2
#   6. unknown flag → exit 64

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/music-video-trim.sh"
[[ -x "$SCRIPT" ]] || { echo "ERROR: $SCRIPT not executable"; exit 2; }

SBX=$(mktemp -d -t test-trim.XXXX)
trap "rm -rf '$SBX'" EXIT

# Build a 10s source.
SHORT_SRC="$SBX/short.mp4"
/opt/homebrew/bin/ffmpeg -y -loglevel error \
  -f lavfi -i color=c=black:s=320x568:d=10 \
  -f lavfi -i sine=frequency=400:duration=10 \
  -map 0:v -map 1:a -c:v libx264 -preset ultrafast -t 10 -shortest \
  "$SHORT_SRC" 2>/dev/null

# Build a 200s source.
LONG_SRC="$SBX/long.mp4"
/opt/homebrew/bin/ffmpeg -y -loglevel error \
  -f lavfi -i color=c=black:s=320x568:d=200 \
  -f lavfi -i sine=frequency=400:duration=200 \
  -map 0:v -map 1:a -c:v libx264 -preset ultrafast -t 200 -shortest \
  "$LONG_SRC" 2>/dev/null

# Helper: probe duration as integer seconds.
probe_dur() {
  /opt/homebrew/bin/ffprobe -v error -show_entries format=duration -of csv=p=0 "$1" 2>/dev/null \
    | awk '{printf "%d", $1+0.5}'
}

PASS=0
FAIL=0

# Test 1: no-args
if "$SCRIPT" >/dev/null 2>&1; then
  echo "FAIL [no-args]"
  FAIL=$((FAIL+1))
else
  rc=$?
  if [[ "$rc" -eq 64 ]]; then echo "PASS [no-args]"; PASS=$((PASS+1));
  else echo "FAIL [no-args]: exit $rc"; FAIL=$((FAIL+1)); fi
fi

# Test 2: --short on 10s source → copies unchanged
"$SCRIPT" "$SHORT_SRC" "$SBX/out-2.mp4" --short >/dev/null 2>&1
d=$(probe_dur "$SBX/out-2.mp4")
if [[ "$d" -eq 10 ]]; then echo "PASS [short-on-short]: ${d}s"; PASS=$((PASS+1));
else echo "FAIL [short-on-short]: ${d}s, expected 10"; FAIL=$((FAIL+1)); fi

# Test 3: --duration=5 on 10s → 5s
"$SCRIPT" "$SHORT_SRC" "$SBX/out-3.mp4" --duration=5 >/dev/null 2>&1
d=$(probe_dur "$SBX/out-3.mp4")
if [[ "$d" -eq 5 ]]; then echo "PASS [trim-to-5s]: ${d}s"; PASS=$((PASS+1));
else echo "FAIL [trim-to-5s]: ${d}s, expected 5"; FAIL=$((FAIL+1)); fi

# Test 4: --shorts-max on 200s → 180s
"$SCRIPT" "$LONG_SRC" "$SBX/out-4.mp4" --shorts-max >/dev/null 2>&1
d=$(probe_dur "$SBX/out-4.mp4")
if [[ "$d" -ge 178 && "$d" -le 182 ]]; then echo "PASS [shorts-max]: ${d}s"; PASS=$((PASS+1));
else echo "FAIL [shorts-max]: ${d}s, expected ≈180"; FAIL=$((FAIL+1)); fi

# Test 5: --start=2 --duration=3 → re-encode 3s
"$SCRIPT" "$SHORT_SRC" "$SBX/out-5.mp4" --start=2 --duration=3 >/dev/null 2>&1
d=$(probe_dur "$SBX/out-5.mp4")
if [[ "$d" -ge 2 && "$d" -le 4 ]]; then echo "PASS [start-offset]: ${d}s"; PASS=$((PASS+1));
else echo "FAIL [start-offset]: ${d}s, expected ≈3"; FAIL=$((FAIL+1)); fi

# Test 6: unknown flag
if "$SCRIPT" "$SHORT_SRC" --bogus >/dev/null 2>&1; then
  echo "FAIL [unknown-flag]"
  FAIL=$((FAIL+1))
else
  rc=$?
  if [[ "$rc" -eq 64 ]]; then echo "PASS [unknown-flag]"; PASS=$((PASS+1));
  else echo "FAIL [unknown-flag]: exit $rc"; FAIL=$((FAIL+1)); fi
fi

echo
echo "=== trim tests: $PASS passed, $FAIL failed ==="
[[ "$FAIL" -gt 0 ]] && exit 1
exit 0
