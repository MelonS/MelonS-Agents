#!/usr/bin/env bash
# Fresh-clone simulator — verifies the clone-and-go path actually
# works end-to-end on a clean tree.
#
# What it does:
#   1. Clones the public repo (or a local override via FRESH_CLONE_REMOTE)
#      into a temp dir.
#   2. Runs scripts/bootstrap.sh inside the clone (auto-creates .env,
#      checks tools, auto-fetches whisper + ollama models, generates
#      synthetic fixtures on macOS).
#   3. Runs one highlight mission against the Sintel trailer
#      (CC-BY-3.0, Blender Foundation).
#   4. Asserts a short.mp4 was produced with non-trivial size.
#   5. Appends a PASS / FAIL line to docs/onboarding/fresh-clone-log.txt
#      so the proof persists across machine swaps.
#
# Run from the repo root:
#   scripts/test-fresh-clone.sh
#
# Optional overrides:
#   FRESH_CLONE_REMOTE   git URL or local path (default GitHub HTTPS)
#   FRESH_CLONE_KEEP     set to 1 to leave the temp clone in place
#                        after the run (default: cleaned up)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REMOTE="${FRESH_CLONE_REMOTE:-https://github.com/MelonS/MelonS-Agents.git}"
KEEP="${FRESH_CLONE_KEEP:-0}"
FORCE_MODEL_DOWNLOAD="${FRESH_CLONE_FORCE_MODEL_DOWNLOAD:-0}"
STAMP="$(date '+%Y%m%d-%H%M%S')"
WORK="${TMPDIR:-/tmp}/fresh-clone-${STAMP}"
LOG_DIR="${REPO_ROOT}/docs/onboarding"
LOG="${LOG_DIR}/fresh-clone-log.txt"
VARIANT="basic"
if [[ "$FORCE_MODEL_DOWNLOAD" == "1" ]]; then
  VARIANT="force-model-download"
fi

mkdir -p "$LOG_DIR"

VERDICT="FAIL"
SHORT_PATH=""
SHORT_SIZE_MB=""
FAIL_REASON=""
MODEL_DOWNLOADED_MB=""

write_log_entry() {
  local ts; ts="$(date '+%Y-%m-%d %H:%M:%S %z')"
  {
    if [[ "$VERDICT" == "PASS" ]]; then
      if [[ "$VARIANT" == "force-model-download" ]]; then
        printf '%s  PASS  variant=%s  remote=%s  short=%s  size=%sMB  model_download=%sMB\n' \
          "$ts" "$VARIANT" "$REMOTE" "${SHORT_PATH#$WORK/}" "$SHORT_SIZE_MB" "$MODEL_DOWNLOADED_MB"
      else
        printf '%s  PASS  variant=%s  remote=%s  short=%s  size=%sMB\n' \
          "$ts" "$VARIANT" "$REMOTE" "${SHORT_PATH#$WORK/}" "$SHORT_SIZE_MB"
      fi
    else
      printf '%s  FAIL  variant=%s  remote=%s  reason="%s"\n' \
        "$ts" "$VARIANT" "$REMOTE" "$FAIL_REASON"
    fi
  } >> "$LOG"
}

on_exit() {
  local rc="$?"
  if [[ "$VERDICT" != "PASS" && -z "$FAIL_REASON" ]]; then
    FAIL_REASON="exit code ${rc}"
  fi
  write_log_entry
  if [[ "$VERDICT" == "PASS" ]]; then
    echo
    echo "✅ fresh-clone PASS — log appended to ${LOG#$REPO_ROOT/}"
  else
    echo
    echo "❌ fresh-clone FAIL (${FAIL_REASON}) — log appended to ${LOG#$REPO_ROOT/}"
  fi
  if [[ "$KEEP" != "1" ]]; then
    rm -rf "$WORK"
  else
    echo "ℹ  temp workdir kept at: $WORK"
  fi
}
trap on_exit EXIT

echo "=== fresh-clone simulator ==="
echo "remote : $REMOTE"
echo "workdir: $WORK"
echo

# --- step 1: clone -----------------------------------------------------
echo "[STEP 1/4] clone"
if ! git clone --depth=1 "$REMOTE" "$WORK"; then
  FAIL_REASON="git clone failed"
  exit 1
fi

cd "$WORK"
echo

# Optional: force the bootstrap to download the whisper model rather
# than reuse whatever lives in the host's $WHISPER_MODEL.  Exercises
# the actual fetch path (≈ 466 MB for ggml-small.bin).
if [[ "$FORCE_MODEL_DOWNLOAD" == "1" ]]; then
  cp .env.example .env
  FRESH_MODEL_PATH="${WORK}/whisper-models/ggml-small.bin"
  sed -i.bak "s|^WHISPER_MODEL=.*|WHISPER_MODEL=${FRESH_MODEL_PATH}|" .env
  rm -f .env.bak
  echo "  → forced WHISPER_MODEL=${FRESH_MODEL_PATH} (not present; download will run)"
  echo
fi

# --- step 2: bootstrap -------------------------------------------------
echo "[STEP 2/4] bootstrap"
if ! ./scripts/bootstrap.sh; then
  FAIL_REASON="bootstrap.sh exited non-zero"
  exit 1
fi

# Validate the forced download actually produced a model file.
if [[ "$FORCE_MODEL_DOWNLOAD" == "1" ]]; then
  if [[ ! -f "$FRESH_MODEL_PATH" ]]; then
    FAIL_REASON="forced WHISPER_MODEL path missing after bootstrap"
    exit 1
  fi
  local_bytes="$(wc -c < "$FRESH_MODEL_PATH" | tr -d ' ')"
  MODEL_DOWNLOADED_MB=$(( local_bytes / 1024 / 1024 ))
  if (( local_bytes < 100000000 )); then
    FAIL_REASON="forced model download too small: ${local_bytes} bytes"
    exit 1
  fi
  echo "  ✅ model downloaded: ${MODEL_DOWNLOADED_MB} MB at ${FRESH_MODEL_PATH}"
fi
echo

# --- step 3: highlight mission ----------------------------------------
echo "[STEP 3/4] highlight mission (Sintel trailer)"
SINTEL_URL="https://download.blender.org/durian/trailer/sintel_trailer-1080p.mp4"
if ! ./agents/missions/highlight/run.sh "$SINTEL_URL"; then
  FAIL_REASON="highlight mission exited non-zero"
  exit 1
fi
echo

# --- step 4: assert short.mp4 exists ----------------------------------
echo "[STEP 4/4] assert outputs"
SHORT_PATH="$(find "$WORK/records/missions" -name 'short.mp4' -path '*/highlight-*' 2>/dev/null | sort | tail -1)"
if [[ -z "$SHORT_PATH" ]]; then
  FAIL_REASON="no short.mp4 under records/missions/.../highlight-*"
  exit 1
fi

SIZE_BYTES="$(wc -c < "$SHORT_PATH" | tr -d ' ')"
SHORT_SIZE_MB="$(( SIZE_BYTES / 1024 / 1024 ))"
if (( SIZE_BYTES < 1000000 )); then
  FAIL_REASON="short.mp4 only ${SIZE_BYTES} bytes (looks like an empty render)"
  exit 1
fi

echo "✅ short.mp4 produced: $SHORT_PATH"
echo "   size: ${SHORT_SIZE_MB} MB"

VERDICT="PASS"
exit 0
