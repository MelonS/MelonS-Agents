#!/usr/bin/env bash
# Validate that bootstrap.sh prints the right OS-specific install
# hints when CLI tools are missing.  Simulates "stranger with bare
# minimum PATH" by running bootstrap under `env -i` with PATH set
# to /usr/bin:/bin only — env.sh's `command -v` discovery comes up
# empty for whisper-cli / ollama / yt-dlp (which live in
# /opt/homebrew/bin, not on minimal PATH).
#
# What this test does NOT verify:
#   * ffmpeg / ffprobe — env.sh has a keg-path fallback to
#     /opt/homebrew/opt/ffmpeg-full/bin/ffmpeg that bypasses PATH.
#     That's a feature, not a bug.  Those binaries get found even
#     here, so the hint isn't exercised.
#   * The actual install commands working — only that the hints
#     are *printed* with the right wording.
#
# Appends one variant=bootstrap-hints line to fresh-clone-log.txt.
# Usage:  scripts/test-bootstrap-hints.sh

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REMOTE="${BOOTSTRAP_HINTS_REMOTE:-https://github.com/MelonS/MelonS-Agents.git}"
STAMP="$(date '+%Y%m%d-%H%M%S')"
WORK="${TMPDIR:-/tmp}/bootstrap-hints-${STAMP}"
LOG_DIR="${REPO_ROOT}/docs/onboarding"
LOG="${LOG_DIR}/fresh-clone-log.txt"

mkdir -p "$LOG_DIR"

cleanup_and_log() {
  local verdict="$1"
  local pass_count="$2"
  local total="$3"
  local reason="${4:-}"
  local ts
  ts="$(date '+%Y-%m-%d %H:%M:%S %z')"
  if [[ "$verdict" == "PASS" ]]; then
    printf '%s  PASS  variant=bootstrap-hints  asserts=%d/%d\n' \
      "$ts" "$pass_count" "$total" >> "$LOG"
  else
    printf '%s  FAIL  variant=bootstrap-hints  asserts=%d/%d  reason="%s"\n' \
      "$ts" "$pass_count" "$total" "$reason" >> "$LOG"
  fi
  rm -rf "$WORK"
}

echo "=== bootstrap-hints simulator ==="
echo "remote : $REMOTE"
echo "workdir: $WORK"
echo

if ! git clone --depth=1 "$REMOTE" "$WORK" 2>&1 | tail -3; then
  cleanup_and_log FAIL 0 0 "git clone failed"
  echo "❌ git clone failed"
  exit 1
fi
cd "$WORK"

echo "[STEP 1/2] run bootstrap with minimal PATH (env -i)"
# Capture output; bootstrap will exit non-zero because tools are
# missing.  That is the expected path — we want the *hints*.
output="$(env -i \
  PATH="/usr/bin:/bin" \
  HOME="$HOME" \
  TMPDIR="${TMPDIR:-/tmp}" \
  TERM="${TERM:-xterm}" \
  bash scripts/bootstrap.sh 2>&1 || true)"

echo "[STEP 2/2] assert hints + missing markers"
echo

pass=0
total=0

check() {
  local label="$1"
  local pattern="$2"
  total=$((total + 1))
  if grep -qF -- "$pattern" <<< "$output"; then
    pass=$((pass + 1))
    echo "  ✅ $label"
  else
    echo "  ❌ $label    (pattern: $pattern)"
  fi
}

echo "tools flagged missing:"
check "whisper-cli missing"     "❌ whisper-cli —"
check "ollama missing"          "❌ ollama —"
check "yt-dlp missing"          "❌ yt-dlp —"

echo
echo "macOS install hints present:"
check "whisper-cli hint"        "brew install whisper-cpp"
check "ollama hint"             "brew install ollama"
check "yt-dlp hint"             "brew install yt-dlp"

echo
echo "non-tool sections still printed:"
check "records dir section"     "=== records dir ==="
check "autonomy mode section"   "=== autonomy mode ==="

verdict="PASS"
[[ $pass -lt $total ]] && verdict="FAIL"

echo
echo "$verdict — $pass/$total asserts"
cleanup_and_log "$verdict" "$pass" "$total" "$(( total - pass )) asserts failed"
[[ "$verdict" == "PASS" ]]
