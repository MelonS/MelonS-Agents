#!/usr/bin/env bash
# TTS wrapper — synthesize a text file into a WAV using one of several
# backends.  Backends are tried in priority order; first available wins.
# Source after env.sh + log.sh.
#
# Backends (priority order):
#   1. Kokoro-ONNX (via .venv/bin/python + scripts/tts-kokoro.py)
#      — Apache 2.0 license, commercial use allowed.  ~325 MB ONNX model,
#        ~26 MB voice bank.  Fast on CPU; no torch/spacy compile required.
#      — voice_hint = built-in voice name (e.g., am_michael, af_heart).
#   2. macOS `say` (built-in Apple TTS)
#      — ~60% market voice quality, instant, AIFF output.
#      — voice_hint = system voice (e.g., Daniel, Samantha).
#
# Usage:
#   tts_synthesize <input_text_file> <output_wav_path> [voice_hint]
#
# Returns 0 on success, non-zero if no backend could synthesize.

# Voice routing:
#   - Kokoro voice names follow the pattern <lang><gender>_<name>
#     where lang is one of [abjzefhip] (a=American EN, b=British EN, j=JA,
#     z=ZH, e=ES, f=FR, h=HI, i=IT, p=PT-BR).  Korean is NOT in Kokoro v1.0,
#     so any voice_hint that doesn't match that pattern is routed
#     straight to macOS `say` (which supports Korean via Yuna, Japanese
#     via Kyoko, etc.).
#   - Examples that route to Kokoro: am_michael, af_heart, bf_emma.
#   - Examples that route to say: Yuna, Kyoko, Daniel, Samantha.
_is_kokoro_voice() {
  [[ "$1" =~ ^[abjzefhip][fm]_[a-zA-Z]+ ]]
}

tts_synthesize() {
  local text_file="$1" out_wav="$2" voice_hint="${3:-}"

  if [[ ! -f "$text_file" ]]; then
    echo "❌ tts_synthesize: input text file not found: $text_file" >&2
    return 1
  fi
  mkdir -p "$(dirname "$out_wav")"

  # Decide backend by voice_hint shape.  Default (empty hint) = Kokoro
  # am_michael, English documentary tone.  Explicit non-Kokoro hint
  # (e.g., Yuna for Korean) skips Kokoro entirely.
  local use_kokoro=1
  if [[ -n "$voice_hint" ]] && ! _is_kokoro_voice "$voice_hint"; then
    use_kokoro=0
  fi

  # Try Kokoro-ONNX via venv python first (unless caller explicitly
  # picked a non-Kokoro voice).
  if (( use_kokoro == 1 )); then
    local kokoro_py="$REPO_ROOT/scripts/tts-kokoro.py"
    local model_path="$HOME/.local/share/kokoro/kokoro-v1.0.onnx"
    local voices_path="$HOME/.local/share/kokoro/voices-v1.0.bin"
    if [[ -x "$REPO_ROOT/.venv/bin/python" && -f "$kokoro_py" \
          && -f "$model_path" && -f "$voices_path" ]]; then
      if "$REPO_ROOT/.venv/bin/python" -c "from kokoro_onnx import Kokoro" >/dev/null 2>&1; then
        log_info "  tts backend: Kokoro-ONNX (voice=${voice_hint:-am_michael})"
        # Kokoro emits WAV at native sample rate (24 kHz typically).  Transcode
        # to 44.1 kHz stereo so it joins the AAC pipeline cleanly downstream.
        local tmp_wav="${out_wav%.wav}.kokoro.wav"
        if "$REPO_ROOT/.venv/bin/python" "$kokoro_py" \
              --text-file "$text_file" \
              --output "$tmp_wav" \
              ${voice_hint:+--voice "$voice_hint"}; then
          "$FFMPEG_BIN" -y -loglevel error -i "$tmp_wav" -ar 44100 -ac 2 "$out_wav"
          rm -f "$tmp_wav"
          return 0
        fi
        log_warn "  Kokoro failed; falling back to say"
      fi
    fi
  fi

  # Fallback (and primary path for non-Kokoro voices): macOS say.
  if command -v say >/dev/null 2>&1; then
    local say_voice="${voice_hint:-Daniel}"
    local tmp_aiff="${out_wav%.wav}.tmp.aiff"
    log_info "  tts backend: macOS say (voice=$say_voice)"
    say -v "$say_voice" -r 175 -f "$text_file" -o "$tmp_aiff"
    "$FFMPEG_BIN" -y -loglevel error -i "$tmp_aiff" -ar 44100 -ac 2 "$out_wav"
    rm -f "$tmp_aiff"
    return 0
  fi

  echo "❌ tts_synthesize: no TTS backend available (neither Kokoro nor say)" >&2
  return 1
}
