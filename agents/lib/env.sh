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
: "${WHISPER_MODEL:=$HOME/.local/share/whisper-models/ggml-base.bin}"
: "${FFMPEG_BIN:=$(command -v ffmpeg || true)}"
: "${FFPROBE_BIN:=$(command -v ffprobe || true)}"
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
