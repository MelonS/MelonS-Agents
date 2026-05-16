#!/usr/bin/env bash
# Source-able env loader. Validates required vars; fails loud and early.
# Usage: source agents/lib/env.sh

set -eo pipefail

# Robust REPO_ROOT detection across bash and zsh sourcing contexts.
__env_self="${BASH_SOURCE[0]:-${(%):-%x}}"
REPO_ROOT="$(cd "$(dirname "${__env_self}")/../.." && pwd)"
unset __env_self

# Load .env if present (export every assignment)
if [[ -f "$REPO_ROOT/.env" ]]; then
  set -a
  # shellcheck disable=SC1091
  source "$REPO_ROOT/.env"
  set +a
fi

# Defaults for vars that don't need to be in .env
: "${RECORDS_DIR:=$REPO_ROOT/records}"
: "${OLLAMA_HOST:=http://127.0.0.1:11434}"
: "${OLLAMA_MODEL_HIGHLIGHT:=llama3.2:3b}"
: "${WHISPER_MODEL:=$HOME/.local/share/whisper-models/ggml-small.bin}"
# ffmpeg / ffprobe discovery — prefer a libass-enabled build, since
# the caption burn-in step requires it.  On macOS, Homebrew split the
# formula into `ffmpeg` (light, no libass) and `ffmpeg-full` (keg-only,
# includes libass).  We try PATH first, then the ffmpeg-full keg paths.
__resolve_ffmpeg() {
  local cand
  # Honour an explicit FFMPEG_BIN if it points somewhere usable.
  if [[ -n "${FFMPEG_BIN:-}" && -x "${FFMPEG_BIN:-}" ]]; then
    return
  fi
  # First pass: pick the first candidate that has libass.
  for cand in \
      "$(command -v ffmpeg 2>/dev/null || true)" \
      "/opt/homebrew/opt/ffmpeg-full/bin/ffmpeg" \
      "/usr/local/opt/ffmpeg-full/bin/ffmpeg"
  do
    if [[ -n "$cand" && -x "$cand" ]] && "$cand" -version 2>/dev/null | grep -q -- "libass"; then
      FFMPEG_BIN="$cand"
      return
    fi
  done
  # Fallback: first existing ffmpeg even without libass (will be
  # diagnosed by the bootstrap check, with an actionable hint).
  for cand in \
      "$(command -v ffmpeg 2>/dev/null || true)" \
      "/opt/homebrew/opt/ffmpeg-full/bin/ffmpeg" \
      "/usr/local/opt/ffmpeg-full/bin/ffmpeg"
  do
    if [[ -n "$cand" && -x "$cand" ]]; then
      FFMPEG_BIN="$cand"
      return
    fi
  done
}
__resolve_ffmpeg
unset -f __resolve_ffmpeg

# ffprobe sits next to ffmpeg in every supported install.
if [[ -z "${FFPROBE_BIN:-}" && -n "${FFMPEG_BIN:-}" ]]; then
  __ffprobe_guess="${FFMPEG_BIN%/ffmpeg}/ffprobe"
  if [[ -x "$__ffprobe_guess" ]]; then
    FFPROBE_BIN="$__ffprobe_guess"
  else
    FFPROBE_BIN="$(command -v ffprobe || true)"
  fi
  unset __ffprobe_guess
fi
: "${WHISPER_CLI_BIN:=$(command -v whisper-cli || true)}"
: "${YT_DLP_BIN:=$(command -v yt-dlp || true)}"
: "${OLLAMA_BIN:=$(command -v ollama || true)}"

require_env() {
  local missing=()
  for v in "$@"; do
    if [[ -z "${!v:-}" ]]; then
      missing+=("$v")
    fi
  done
  if (( ${#missing[@]} > 0 )); then
    echo "❌ missing env: ${missing[*]}" >&2
    return 1
  fi
}

require_bin() {
  local missing=()
  for b in "$@"; do
    if [[ ! -x "$b" && ! "$(command -v "$b" || true)" ]]; then
      missing+=("$b")
    fi
  done
  if (( ${#missing[@]} > 0 )); then
    echo "❌ binary not found / not executable: ${missing[*]}" >&2
    return 1
  fi
}

export REPO_ROOT RECORDS_DIR OLLAMA_HOST OLLAMA_MODEL_HIGHLIGHT WHISPER_MODEL \
       FFMPEG_BIN FFPROBE_BIN WHISPER_CLI_BIN YT_DLP_BIN OLLAMA_BIN
