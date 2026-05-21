#!/usr/bin/env bash
# first-touch.sh — single-command guided first-run wizard.
#
# The "press button get result" entry point for a stranger who just
# cloned the repo.  Walks the operator through one specific goal —
# producing a zero-account music-video demo — with no decision
# branches.  Target: ≤ 5 minutes from `git clone` to a playing mp4.
#
# Maps to docs/goal.md CRITICAL candidate goal "First-touch success
# rate 10-20% → 60%+".  Pre-built ahead of Build Day Seoul
# (2026-06-16) so the in-person 90 minute slot is design feedback,
# not framework discovery.
#
# Usage:
#   bash scripts/first-touch.sh             # run the wizard
#   bash scripts/first-touch.sh --check     # prereq check only, no render
#   bash scripts/first-touch.sh --help

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

CHECK_ONLY=0
case "${1:-}" in
  --check) CHECK_ONLY=1 ;;
  -h|--help)
    sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'
    exit 0
    ;;
  "") : ;;
  *)  echo "unknown arg: $1 (see --help)" >&2; exit 64 ;;
esac

if [[ -t 1 ]]; then
  G=$'\033[32m'; Y=$'\033[33m'; R=$'\033[31m'; B=$'\033[1m'; D=$'\033[0m'
else
  G=''; Y=''; R=''; B=''; D=''
fi

step() { printf "%s→%s %s\n" "$B" "$D" "$*"; }
ok()   { printf "%s✓%s %s\n" "$G" "$D" "$*"; }
warn() { printf "%s⚠%s %s\n" "$Y" "$D" "$*"; }
err()  { printf "%s✗%s %s\n" "$R" "$D" "$*"; }

echo
printf "%s== MelonS-Agents · first-touch wizard ==%s\n" "$B" "$D"
echo "zero-account music-video demo · ~3 minutes"
echo

step "detecting platform"
case "$(uname -s)" in
  Darwin)
    OS=macos
    OPEN_CMD="open"
    INSTALL_HINT="brew install ffmpeg aubio yq jq python3 whisper-cpp"
    ;;
  Linux)
    OS=linux
    OPEN_CMD="xdg-open"
    INSTALL_HINT="apt install ffmpeg aubio-tools jq python3 # + whisper.cpp build from source"
    ;;
  *)
    err "unsupported platform: $(uname -s) — macOS / Linux only"
    exit 1
    ;;
esac
ok "platform: $OS"

step "checking required tools"
MISSING=()
for bin in ffmpeg ffprobe aubiotrack aubioonset jq python3 curl; do
  if command -v "$bin" >/dev/null 2>&1; then
    ok "  $bin"
  else
    err "  $bin not found"
    MISSING+=("$bin")
  fi
done
for bin in yq whisper-cli; do
  if command -v "$bin" >/dev/null 2>&1; then
    ok "  $bin (optional)"
  else
    warn "  $bin missing (optional — lyrics + genre auto-detect won't work, but demo will)"
  fi
done

if [[ ${#MISSING[@]} -gt 0 ]]; then
  echo
  err "${#MISSING[@]} required tool(s) missing.  Install with:"
  echo "    $INSTALL_HINT"
  echo
  echo "Then re-run:  bash scripts/first-touch.sh"
  exit 1
fi

if ! ffmpeg -version 2>/dev/null | grep -q libass; then
  warn "ffmpeg lacks libass — lyric overlay path will be disabled in the demo"
  warn "to enable: brew install ffmpeg-full   (macOS) or build with --enable-libass (Linux)"
fi

if [[ "$CHECK_ONLY" -eq 1 ]]; then
  echo
  ok "prereq check complete — environment ready"
  echo "Run without --check to produce the demo render."
  exit 0
fi

echo
step "about to render a ~60-second demo music-video (zero-account)"
echo "    - CC-BY-3.0 Blender open-movie B-roll  (cached on first run)"
echo "    - CC-BY-4.0 Kevin MacLeod music track  (cached on first run)"
echo "    - output -> records/missions/<today>/music-video-demo-*/outputs/short.mp4"
echo "    - estimated time: ~3 min Apple Silicon, ~5 min older Intel"
echo
read -r -p "Press Enter to begin (or Ctrl-C to bail) " _
echo

step "running music-video pipeline in MUSIC_VIDEO_DEMO_MODE=1"
START_TS=$(date +%s)
SHORT_ID="first-touch-$(date +%H%M%S)"

LOG=/tmp/first-touch-$$.log
if MUSIC_VIDEO_DEMO_MODE=1 \
   bash "$REPO_ROOT/agents/missions/music-video/run.sh" \
        "$SHORT_ID" "" 2>&1 | tee "$LOG"; then
  :
else
  err "render failed.  See $LOG for diagnostics"
  exit 1
fi

END_TS=$(date +%s)
ELAPSED=$(( END_TS - START_TS ))
ok "render complete in ${ELAPSED}s"

step "locating output"
LATEST=$(ls -1t "${RECORDS_DIR:-$REPO_ROOT/records}/missions/$(date +%Y-%m-%d)/music-video-${SHORT_ID}-"*/outputs/short.mp4 2>/dev/null | head -1)

if [[ -z "$LATEST" || ! -f "$LATEST" ]]; then
  err "couldn't find the output mp4 — search records/missions/$(date +%Y-%m-%d)/ manually"
  exit 1
fi
ok "output: $LATEST"

step "opening for playback"
if command -v "$OPEN_CMD" >/dev/null 2>&1; then
  "$OPEN_CMD" "$LATEST" &
else
  warn "$OPEN_CMD not found — open manually: $LATEST"
fi

echo
printf "%s== Done. What's next? ==%s\n" "$B" "$D"
echo
echo "Your demo lives at:"
echo "  $LATEST"
echo
echo "To produce your own music-video from your own track:"
echo "  bash scripts/music-video-auto.sh PATH/TO/YOUR_TRACK.mp3"
echo
echo "(Requires PEXELS_API_KEY in .env for B-roll — free at"
echo " https://www.pexels.com/api/)"
echo
echo "For the full skill reference:"
echo "  docs/music-video-pipeline-reference.md"
echo

rm -f "$LOG"
exit 0
