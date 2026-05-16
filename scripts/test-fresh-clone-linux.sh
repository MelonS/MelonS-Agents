#!/usr/bin/env bash
# Validate the Linux side of the Platform-support claim by running
# scripts/bootstrap.sh inside an ubuntu:24.04 Docker container.
#
# Scope:
#   * apt-installs ffmpeg / yt-dlp / git / curl / python3 / ca-certs
#     — the realistic "stranger on Linux who already has the basics"
#     starting state.
#   * Does NOT install ollama or whisper-cli (both need extra work
#     on Linux — ollama via curl|sh, whisper-cli via building from
#     source).  Those should be flagged missing with the LINUX
#     install hint, not the macOS one.
#   * Therefore the mission step is NOT attempted.  The test only
#     validates that the Linux code paths in bootstrap.sh fire
#     correctly.
#
# What "PASS" means here:
#   * uname -s inside container = Linux  → bootstrap takes the
#     Linux branch.
#   * apt-installed ffmpeg detected, libass check passes.
#   * Missing tools (whisper-cli, ollama) flagged with the Linux
#     install commands, NOT the macOS ones.
#
# Appends variant=linux-docker line to fresh-clone-log.txt.

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REMOTE="${LINUX_FRESH_CLONE_REMOTE:-https://github.com/MelonS/MelonS-Agents.git}"
LOG_DIR="${REPO_ROOT}/docs/onboarding"
LOG="${LOG_DIR}/fresh-clone-log.txt"

mkdir -p "$LOG_DIR"

write_log() {
  local verdict="$1"
  local pass_count="$2"
  local total="$3"
  local reason="${4:-}"
  local ts
  ts="$(date '+%Y-%m-%d %H:%M:%S %z')"
  if [[ "$verdict" == "PASS" ]]; then
    printf '%s  PASS  variant=linux-docker  asserts=%d/%d  base=ubuntu:24.04\n' \
      "$ts" "$pass_count" "$total" >> "$LOG"
  else
    printf '%s  FAIL  variant=linux-docker  asserts=%d/%d  base=ubuntu:24.04  reason="%s"\n' \
      "$ts" "$pass_count" "$total" "$reason" >> "$LOG"
  fi
}

echo "=== linux-docker simulator ==="
echo "remote : $REMOTE"
echo "base   : ubuntu:24.04"
echo

if ! docker info >/dev/null 2>&1; then
  echo "❌ Docker daemon not reachable — start Docker Desktop first."
  write_log FAIL 0 0 "docker daemon unreachable"
  exit 1
fi

echo "[STEP 1/2] running bootstrap inside ubuntu:24.04 container"

output="$(docker run --rm \
  -e DEBIAN_FRONTEND=noninteractive \
  -e LINUX_FRESH_CLONE_REMOTE="$REMOTE" \
  ubuntu:24.04 bash -c '
    set -e
    apt-get update -qq >/dev/null
    apt-get install -y --no-install-recommends \
      ffmpeg yt-dlp git curl python3 ca-certificates >/dev/null 2>&1
    cd /tmp
    git clone --depth=1 "${LINUX_FRESH_CLONE_REMOTE}" repo >/dev/null 2>&1
    cd repo
    bash scripts/bootstrap.sh || true
  ' 2>&1)"

echo "[STEP 2/2] assert Linux platform-support claim"
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

# apt-installed tools should be found
echo "tools detected from apt install:"
check "✅ ffmpeg (apt build)"      "✅ ffmpeg       "
check "✅ ffprobe (apt build)"     "✅ ffprobe      "
check "✅ yt-dlp (apt build)"      "✅ yt-dlp       "

echo
# Tools NOT installed by apt should be flagged
echo "tools correctly flagged missing:"
check "❌ whisper-cli missing"     "❌ whisper-cli —"
check "❌ ollama missing"          "❌ ollama —"

echo
# Linux-specific hints (not macOS)
echo "Linux install hints (NOT macOS hints):"
check "whisper-cli linux hint"     "build from source: https://github.com/ggerganov/whisper.cpp"
check "ollama linux hint"          "curl -fsSL https://ollama.com/install.sh"

echo
# Negative: macOS-specific phrases should NOT appear
echo "macOS-only phrases absent:"
total=$((total + 1))
if ! grep -qF "brew install whisper-cpp" <<< "$output"; then
  pass=$((pass + 1))
  echo "  ✅ no 'brew install whisper-cpp' on Linux output"
else
  echo "  ❌ macOS hint leaked into Linux output"
fi
total=$((total + 1))
if ! grep -qF "brew install ollama" <<< "$output"; then
  pass=$((pass + 1))
  echo "  ✅ no 'brew install ollama' on Linux output"
else
  echo "  ❌ macOS hint leaked into Linux output"
fi

verdict="PASS"
[[ $pass -lt $total ]] && verdict="FAIL"

echo
echo "$verdict — $pass/$total asserts"
write_log "$verdict" "$pass" "$total" "$(( total - pass )) asserts failed"
[[ "$verdict" == "PASS" ]]
