#!/usr/bin/env bash
# music-video-doctor.sh — skill-specific health check for the
# music-video pipeline.  Complements scripts/doctor.sh (repo-wide)
# by verifying the music-video skill's deps + scripts + test suite.
#
# Reports:
#   - required bins (ffmpeg, ffprobe, aubiotrack, aubioonset, jq,
#     python3, curl, yq)
#   - optional bins (whisper-cli for lyrics, ollama if used)
#   - presence + executability of every scripts/music-video-*.sh
#   - presence + readability of skills/music-video/data/genre-presets.yaml
#   - count of genres + shader catalog
#   - quick-running test suite for the skill (test-shader-gates,
#     test-qa-anchor, test-music-video-{batch,validate,trim,upload-meta})
#
# Usage:
#   scripts/music-video-doctor.sh                  # full report
#   scripts/music-video-doctor.sh --quiet          # one-line summary
#   scripts/music-video-doctor.sh --skip-tests     # skip running tests

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

QUIET=0
SKIP_TESTS=0
for arg in "$@"; do
  case "$arg" in
    --quiet)      QUIET=1 ;;
    --skip-tests) SKIP_TESTS=1 ;;
    -h|--help)
      sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *) echo "unknown arg: $arg" >&2; exit 64 ;;
  esac
done

PASS=0
WARN=0
FAIL=0

check_bin() {
  local bin="$1" required="$2"
  if command -v "$bin" >/dev/null 2>&1; then
    [[ "$QUIET" -eq 0 ]] && echo "  ✓ $bin"
    PASS=$((PASS+1))
  elif [[ "$required" == "required" ]]; then
    [[ "$QUIET" -eq 0 ]] && echo "  ✗ $bin (required)"
    FAIL=$((FAIL+1))
  else
    [[ "$QUIET" -eq 0 ]] && echo "  ⚠ $bin (optional — feature subset disabled)"
    WARN=$((WARN+1))
  fi
}

check_script() {
  local s="$REPO_ROOT/scripts/$1"
  if [[ -x "$s" ]]; then
    [[ "$QUIET" -eq 0 ]] && echo "  ✓ scripts/$1"
    PASS=$((PASS+1))
  elif [[ -f "$s" ]]; then
    [[ "$QUIET" -eq 0 ]] && echo "  ⚠ scripts/$1 (not executable)"
    WARN=$((WARN+1))
  else
    [[ "$QUIET" -eq 0 ]] && echo "  ✗ scripts/$1 missing"
    FAIL=$((FAIL+1))
  fi
}

[[ "$QUIET" -eq 0 ]] && echo "=== music-video doctor ==="

[[ "$QUIET" -eq 0 ]] && echo
[[ "$QUIET" -eq 0 ]] && echo "required tools:"
check_bin ffmpeg     required
check_bin ffprobe    required
check_bin aubiotrack required
check_bin aubioonset required
check_bin jq         required
check_bin python3    required
check_bin curl       required

[[ "$QUIET" -eq 0 ]] && echo
[[ "$QUIET" -eq 0 ]] && echo "optional tools:"
check_bin yq          optional
check_bin whisper-cli optional
check_bin ollama      optional

[[ "$QUIET" -eq 0 ]] && echo
[[ "$QUIET" -eq 0 ]] && echo "scripts:"
for s in music-video-auto.sh music-video-genre.sh music-video-genre-detect.sh \
         music-video-shaders.sh music-video-lyrics.sh music-video-lyric-align.sh \
         music-video-batch.sh music-video-validate.sh music-video-qa-anchor.sh \
         music-video-thumbnail.sh music-video-trim.sh music-video-upload-meta.sh \
         music-video-canvas.sh music-video-typography.sh music-video-audio-reactive.sh \
         music-video-stillzoom.sh music-video-fetch-still.sh music-video-fetch-ai-still.sh \
         lyric-extract.sh broll-history-backfill.sh first-touch.sh; do
  check_script "$s"
done

[[ "$QUIET" -eq 0 ]] && echo
[[ "$QUIET" -eq 0 ]] && echo "data:"
PRESETS="$REPO_ROOT/skills/music-video/data/genre-presets.yaml"
if [[ -r "$PRESETS" ]]; then
  if command -v yq >/dev/null; then
    NGENRES=$(yq -r '.genres | keys | length' "$PRESETS" 2>/dev/null)
    [[ "$QUIET" -eq 0 ]] && echo "  ✓ $PRESETS ($NGENRES genres)"
    PASS=$((PASS+1))
  else
    [[ "$QUIET" -eq 0 ]] && echo "  ✓ $PRESETS (yq absent — skipping genre count)"
    PASS=$((PASS+1))
  fi
else
  [[ "$QUIET" -eq 0 ]] && echo "  ✗ $PRESETS missing"
  FAIL=$((FAIL+1))
fi

# Shader catalog count: shellcheck via grep for `^  <name>)` case blocks.
SHADER_CT=$(grep -cE '^  [a-z_]+\)' "$REPO_ROOT/scripts/music-video-shaders.sh" 2>/dev/null)
[[ "$QUIET" -eq 0 ]] && echo "  shader catalog: $SHADER_CT effects"

# Test suite
if [[ "$SKIP_TESTS" -eq 0 ]]; then
  [[ "$QUIET" -eq 0 ]] && echo
  [[ "$QUIET" -eq 0 ]] && echo "tests (skip with --skip-tests):"
  for t in test-shader-gates test-qa-anchor test-music-video-batch \
           test-music-video-validate test-music-video-trim \
           test-music-video-upload-meta; do
    if [[ -x "$REPO_ROOT/scripts/$t.sh" ]]; then
      if bash "$REPO_ROOT/scripts/$t.sh" >/dev/null 2>&1; then
        [[ "$QUIET" -eq 0 ]] && echo "  ✓ $t"
        PASS=$((PASS+1))
      else
        [[ "$QUIET" -eq 0 ]] && echo "  ✗ $t (FAIL)"
        FAIL=$((FAIL+1))
      fi
    fi
  done
fi

[[ "$QUIET" -eq 0 ]] && echo
echo "music-video-doctor: $PASS pass, $WARN warn, $FAIL fail"
[[ "$FAIL" -gt 0 ]] && exit 2
[[ "$WARN" -gt 0 ]] && exit 1
exit 0
