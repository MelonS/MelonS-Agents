#!/usr/bin/env bash
# Download the whisper.cpp model named by $WHISPER_MODEL (or the small
# multilingual default) into the configured path.  Idempotent: if the
# target file already exists and is non-trivial, skips the download.
#
# Usage:
#   scripts/fetch-whisper-model.sh              # uses $WHISPER_MODEL from .env
#   scripts/fetch-whisper-model.sh ggml-base    # override variant
#
# Source: ggerganov/whisper.cpp models on Hugging Face
#   https://huggingface.co/ggerganov/whisper.cpp
#
# Models distributed there:
#   ggml-tiny.bin    ~75 MB
#   ggml-base.bin   ~142 MB
#   ggml-small.bin  ~466 MB
#   ggml-medium.bin ~1.5 GB
#   ggml-large-v3.bin ~3.1 GB
#
# Notes:
#   - "small" is the project default — multilingual, decent quality.
#   - The .bin files at huggingface.co/ggerganov/whisper.cpp are
#     distributed by the upstream maintainer; they are MIT-licensed
#     model weights (see the repo's LICENSE).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# Load .env if present so $WHISPER_MODEL is in scope.
if [[ -f .env ]]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
fi

# Determine which variant we are fetching.  Caller arg wins, then the
# tail of $WHISPER_MODEL, then default "small".
variant_arg="${1:-}"
if [[ -n "$variant_arg" ]]; then
  variant="$variant_arg"
elif [[ -n "${WHISPER_MODEL:-}" ]]; then
  base=$(basename "$WHISPER_MODEL" .bin)
  # ggml-small  → small ; ggml-base → base ; etc.
  variant="${base#ggml-}"
else
  variant="small"
fi

# Resolve target path.  Honour $WHISPER_MODEL if it already points to the
# requested variant; otherwise fall back to the conventional location.
default_dir="${HOME}/.local/share/whisper-models"
default_file="${default_dir}/ggml-${variant}.bin"
target="${WHISPER_MODEL:-$default_file}"

if [[ -z "$variant_arg" && "$target" != *"ggml-${variant}.bin" ]]; then
  echo "ℹ \$WHISPER_MODEL points to '$target' but variant '$variant' was inferred from it; using that path." >&2
fi

mkdir -p "$(dirname "$target")"

# Skip if already present (with a sanity-check size — >10 MB).
if [[ -f "$target" ]]; then
  size_bytes=$(wc -c < "$target" 2>/dev/null | tr -d ' ')
  if (( size_bytes > 10000000 )); then
    size_mb=$(( size_bytes / 1024 / 1024 ))
    echo "✅ already present: $target (${size_mb} MB)"
    exit 0
  else
    echo "⚠ existing file is suspiciously small (${size_bytes} bytes); refetching" >&2
    rm -f "$target"
  fi
fi

url="https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-${variant}.bin"
echo "↓ fetching ggml-${variant}.bin"
echo "  from $url"
echo "  to   $target"

# Pick the best available downloader.
if command -v curl >/dev/null 2>&1; then
  curl --fail --location --progress-bar --output "$target" "$url"
elif command -v wget >/dev/null 2>&1; then
  wget --show-progress --progress=bar:force --output-document "$target" "$url"
else
  echo "❌ neither curl nor wget is installed; can't download" >&2
  exit 1
fi

size_bytes=$(wc -c < "$target" | tr -d ' ')
size_mb=$(( size_bytes / 1024 / 1024 ))
if (( size_bytes < 10000000 )); then
  echo "❌ downloaded file is only ${size_bytes} bytes — looks like an error page, not a model." >&2
  echo "  Check the URL and try again." >&2
  exit 1
fi
echo "✅ downloaded ${size_mb} MB to $target"
