#!/usr/bin/env bash
# Lightweight smoke test for the music-video skill.
#
# Per Layer 4 sketch in docs/ideas.md "Main-protection v2":
# a short-lived check that doesn't render but verifies the
# invariants the full pipeline depends on.  Designed to be
# invoked periodically (launchd / cron) to catch silent decay
# while the operator is heads-down on other skills.
#
# What this proves on PASS:
#   - All required external binaries are present and runnable.
#   - aubiotrack + aubioonset can extract events from a real audio file.
#   - ffmpeg has libass (the mission's caption-burn precondition,
#     even though music-video doesn't burn captions today — keeps
#     parity with the broader pipeline).
#   - The mission script's bash syntax is valid (catches a typo in
#     run.sh before the next operator invocation).
#   - The mission's `classify_kw` heuristic returns expected shape
#     for known and default keywords.
#
# What this does NOT prove (intentionally — that's Layer 3's job):
#   - End-to-end render succeeds.  Use scripts/test-demo-mode.sh
#     for the functional gate.
#   - Pexels / Suno integration works on a key the operator might
#     not have configured.
#
# Usage:
#   skills/music-video/tests/smoke.sh
#
# Exit codes:
#   0 — all checks PASS
#   1 — one or more invariants broken (see stderr for which)

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"

# Source env.sh the same way the mission script does so $FFMPEG_BIN /
# $FFPROBE_BIN resolve to the same binary the mission actually uses.
# Without this the smoke test could check a system ffmpeg without
# libass while the mission uses a static build that has it.
# shellcheck disable=SC1091
[[ -f "$REPO_ROOT/agents/lib/env.sh" ]] && source "$REPO_ROOT/agents/lib/env.sh"
FFMPEG_BIN="${FFMPEG_BIN:-ffmpeg}"
FFPROBE_BIN="${FFPROBE_BIN:-ffprobe}"

PASS=0
FAIL=0
FAILED=()

check() {
  local name="$1"; shift
  if "$@" >/dev/null 2>&1; then
    PASS=$((PASS + 1))
    printf "  [PASS] %s\n" "$name"
  else
    FAIL=$((FAIL + 1))
    FAILED+=("$name")
    printf "  [FAIL] %s\n" "$name" >&2
  fi
}

echo "=== music-video skill smoke ==="

# 1. Binary presence
echo "[1/5] required binaries on PATH"
check "aubiotrack on PATH"  command -v aubiotrack
check "aubioonset on PATH"  command -v aubioonset
check "ffmpeg on PATH"      command -v ffmpeg
check "ffprobe on PATH"     command -v ffprobe
check "jq on PATH"          command -v jq
check "curl on PATH"        command -v curl

# 2. ffmpeg libass presence — the wider pipeline relies on it; if it
# breaks here, faceless-short would also break.  Keeping the check
# in this skill smoke is cheap insurance against the Homebrew
# ffmpeg-vs-ffmpeg-full packaging gotcha.
echo "[2/5] ffmpeg has libass"
# Check the env-resolved ffmpeg (same one the mission uses) by parsing
# the `-filters` output and looking for the `ass` filter as a single
# token in column 2.  A bare `grep "ass"` would false-positive on
# `allpass`, `bass`, etc.
check "FFMPEG_BIN -filters lists 'ass' (libass enabled)" \
  bash -c '"$0" -hide_banner -filters 2>/dev/null | awk "\$2==\"ass\"{found=1} END{exit !found}"' "$FFMPEG_BIN"

# 3. Mission script syntactic validity
echo "[3/5] mission script syntax"
check "agents/missions/music-video/run.sh bash -n" \
  bash -n agents/missions/music-video/run.sh

# 4. Generate a 2-second sine-tone wav and verify aubio binaries
#    actually return events.  This is the smallest practical end-to-
#    end probe of "can audio analysis still happen on this machine."
#    macOS-friendly: use ffmpeg's lavfi to synthesize so we don't
#    depend on the operator's music cache being populated.
echo "[4/5] aubio responds on a synthetic tone"
TMPWAV="$(mktemp -t mv-smoke-XXXXXX).wav"
trap 'rm -f "$TMPWAV"' EXIT

# 2-second 220 Hz tone amplitude-pulsed at 4 Hz so aubiotrack
# has something rhythmic to detect.
if "$FFMPEG_BIN" -y -hide_banner -loglevel error \
     -f lavfi -i 'sine=frequency=220:beep_factor=4:duration=2' \
     -ar 22050 -ac 1 "$TMPWAV" 2>/dev/null; then
  beats=$(aubiotrack -i "$TMPWAV" 2>/dev/null | wc -l | tr -d ' ')
  onsets=$(aubioonset -i "$TMPWAV" 2>/dev/null | wc -l | tr -d ' ')
  check "aubiotrack emits ≥1 beat on synthetic tone" \
    bash -c "test $beats -ge 1"
  check "aubioonset emits ≥1 onset on synthetic tone" \
    bash -c "test $onsets -ge 1"
else
  FAIL=$((FAIL + 2))
  FAILED+=("aubiotrack smoke (ffmpeg synth failed)")
  FAILED+=("aubioonset smoke (ffmpeg synth failed)")
  echo "  [FAIL] ffmpeg lavfi sine synthesis failed — cannot probe aubio" >&2
fi

# 5. The mission's keyword classifier is a pure bash function — extract
#    + dot-source it in a subshell to assert it still produces the
#    expected (speed, cam) tuple for known keywords.
echo "[5/5] keyword classifier returns expected shape"
classifier_out=$(
  bash -c '
    # Extract classify_kw from the mission script into a temp file,
    # source it, and exercise.  Stops at the closing brace.
    awk "/^classify_kw\(\) \{/,/^\}/" agents/missions/music-video/run.sh > /tmp/mv-classify.sh
    source /tmp/mv-classify.sh
    rm -f /tmp/mv-classify.sh
    classify_kw "vintage turntable"
    classify_kw "rooftop city night"
    classify_kw "cozy reading room"
    classify_kw "totally unknown keyword 12345"
  ' 2>&1
)
expected_lines=("1.00 static" "0.80 motion" "0.55 static" "0.80 motion")
ok=1
i=0
while IFS= read -r line; do
  if [[ "$line" != "${expected_lines[$i]}" ]]; then
    ok=0
    echo "  classifier mismatch line $i: got '$line' expected '${expected_lines[$i]}'" >&2
  fi
  i=$((i + 1))
done <<< "$classifier_out"
check "classify_kw on 4 known/default keywords" bash -c "test $ok -eq 1"

# Summary
echo
TOTAL=$((PASS + FAIL))
echo "=== ${PASS}/${TOTAL} PASS ==="
if (( FAIL > 0 )); then
  echo "failed checks:" >&2
  for name in "${FAILED[@]}"; do
    echo "  - $name" >&2
  done
  exit 1
fi
exit 0
