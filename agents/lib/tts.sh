#!/usr/bin/env bash
# TTS wrapper — synthesize a text file into a WAV using one of several
# backends.  Backends are tried in priority order; first available wins.
# Source after env.sh + log.sh.
#
# Backends (priority order):
#   1. Coqui XTTS v2 (via .venv/bin/python + scripts/tts-xtts.py)
#      — multilingual, ~85% market voice quality, ~2 GB model, slow first run.
#   2. macOS `say` (built-in Apple TTS)
#      — ~60% market voice quality, instant, AIFF output.
#
# Usage:
#   tts_synthesize <input_text_file> <output_wav_path> [voice_hint]
#
# voice_hint is backend-specific:
#   - XTTS: speaker name (default: built-in English voice)
#   - say:  voice name (default: Daniel — British English documentary tone)
#
# Returns 0 on success, non-zero if no backend could synthesize.

tts_synthesize() {
  local text_file="$1" out_wav="$2" voice_hint="${3:-}"

  if [[ ! -f "$text_file" ]]; then
    echo "❌ tts_synthesize: input text file not found: $text_file" >&2
    return 1
  fi
  mkdir -p "$(dirname "$out_wav")"

  # Try Coqui XTTS via venv python first.
  local xtts_py="$REPO_ROOT/scripts/tts-xtts.py"
  if [[ -x "$REPO_ROOT/.venv/bin/python" && -f "$xtts_py" ]]; then
    if "$REPO_ROOT/.venv/bin/python" -c "from TTS.api import TTS" >/dev/null 2>&1; then
      log_info "  tts backend: Coqui XTTS"
      if "$REPO_ROOT/.venv/bin/python" "$xtts_py" \
            --text-file "$text_file" \
            --output "$out_wav" \
            ${voice_hint:+--voice "$voice_hint"}; then
        return 0
      fi
      log_warn "  XTTS failed; falling back to say"
    fi
  fi

  # Fallback: macOS say.
  if command -v say >/dev/null 2>&1; then
    local say_voice="${voice_hint:-Daniel}"
    local tmp_aiff="${out_wav%.wav}.tmp.aiff"
    log_info "  tts backend: macOS say (voice=$say_voice)"
    say -v "$say_voice" -r 175 -f "$text_file" -o "$tmp_aiff"
    "$FFMPEG_BIN" -y -loglevel error -i "$tmp_aiff" -ar 44100 -ac 2 "$out_wav"
    rm -f "$tmp_aiff"
    return 0
  fi

  echo "❌ tts_synthesize: no TTS backend available (neither XTTS nor say)" >&2
  return 1
}
