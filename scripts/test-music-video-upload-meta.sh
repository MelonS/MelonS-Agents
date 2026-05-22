#!/usr/bin/env bash
# Regression test for scripts/music-video-upload-meta.sh.
#
# Asserts:
#   1. no-args → exit 64
#   2. with --genre=kpop_ballad → emits upload-metadata.md with KR hashtags
#   3. without --genre, short_id matches a pattern → genre inferred
#   4. lyric language detected from korean_*.mp4 raw clips

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/music-video-upload-meta.sh"
[[ -x "$SCRIPT" ]] || { echo "ERROR: $SCRIPT not executable"; exit 2; }

SBX=$(mktemp -d -t test-upload-meta.XXXX)
trap "rm -rf '$SBX'" EXIT

make_mission() {
  local dir="$1"; shift
  mkdir -p "$dir/outputs" "$dir/resources/clips"
  /opt/homebrew/bin/ffmpeg -y -loglevel error \
    -f lavfi -i color=c=black:s=320x568:d=2 \
    -f lavfi -i sine=frequency=400:duration=2 \
    -map 0:v -map 1:a -c:v libx264 -preset ultrafast -t 2 -shortest \
    "$dir/outputs/short.mp4" 2>/dev/null
  for kw in "$@"; do
    touch "$dir/resources/clips/raw-${kw}.mp4"
  done
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

# Test 2: KR genre + KR hashtags emitted
M1="$SBX/music-video-test-mission-123456"
make_mission "$M1" "korean_woman_cafe" "seoul_street"
"$SCRIPT" "$M1" --genre=kpop_ballad >/dev/null 2>&1
if grep -q '#kpopballad' "$M1/outputs/upload-metadata.md" 2>/dev/null; then
  echo "PASS [kr-hashtags]"
  PASS=$((PASS+1))
else
  echo "FAIL [kr-hashtags]: #kpopballad missing"
  FAIL=$((FAIL+1))
fi

# Test 3: genre inferred from short_id (no --genre)
M2="$SBX/music-video-uspop-fancy-123456"
make_mission "$M2" "new_york_street"
"$SCRIPT" "$M2" >/dev/null 2>&1
if grep -q "Genre | uspop" "$M2/outputs/upload-metadata.md" 2>/dev/null; then
  echo "PASS [genre-inferred]"
  PASS=$((PASS+1))
else
  echo "FAIL [genre-inferred]: expected 'uspop' from short_id pattern"
  FAIL=$((FAIL+1))
fi

# Test 4: ko language detected from korean_* clip
if grep -q "Lyric language | ko" "$M1/outputs/upload-metadata.md" 2>/dev/null; then
  echo "PASS [lang-ko-detected]"
  PASS=$((PASS+1))
else
  echo "FAIL [lang-ko-detected]"
  FAIL=$((FAIL+1))
fi

echo
echo "=== upload-meta tests: $PASS passed, $FAIL failed ==="
[[ "$FAIL" -gt 0 ]] && exit 1
exit 0
