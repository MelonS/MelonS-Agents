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

# Edge-TTS (Microsoft Edge online neural TTS) voice names are BCP-47-ish:
#   <lang>-<REGION>-<Name>Neural   e.g. ko-KR-SunHiNeural, en-US-AriaNeural.
# A hint with that shape routes to edge-tts.  This is the path for languages
# Kokoro v1.0 does NOT cover — notably Korean — and the only neural option when
# macOS `say` is unavailable (Windows / Linux).  Free, no API key.
_is_edge_voice() {
  [[ "$1" == *Neural ]] || [[ "$1" =~ ^[a-z]{2}-[A-Z]{2}- ]]
}

# Resolve the project venv's python across platforms: POSIX puts it at
# .venv/bin/python, Windows at .venv/Scripts/python.exe.  Echoes the path,
# returns non-zero if no venv python exists.
_venv_python() {
  if [[ -x "$REPO_ROOT/.venv/Scripts/python.exe" ]]; then
    echo "$REPO_ROOT/.venv/Scripts/python.exe"
  elif [[ -x "$REPO_ROOT/.venv/bin/python" ]]; then
    echo "$REPO_ROOT/.venv/bin/python"
  else
    return 1
  fi
}

tts_synthesize() {
  local text_file="$1" out_wav="$2" voice_hint="${3:-}"

  if [[ ! -f "$text_file" ]]; then
    echo "❌ tts_synthesize: input text file not found: $text_file" >&2
    return 1
  fi
  mkdir -p "$(dirname "$out_wav")"

  # Emotion-directed TTS backend (ElevenLabs v3 / Typecast).  Opt-in: set
  # FACELESS_TTS_PLAN to a voice-plan JSON — the plan supersedes text_file/
  # voice_hint because the emotion arc IS the script.  The plan's "engine"
  # field (default "elevenlabs") selects the backend.  Both emit narration.wav
  # + a sentence-level SRT, renamed to .edge.srt so the caller's script-exact
  # caption path is unchanged.  Falls through to Kokoro/edge-tts on any
  # failure.  See docs/elevenlabs-tts-notes.md / typecast-tts-notes.md.
  if [[ -n "${FACELESS_TTS_PLAN:-}" && -f "${FACELESS_TTS_PLAN}" ]]; then
    local vpy; vpy="$(_venv_python)" || vpy=""
    if [[ -n "$vpy" ]]; then
      local engine
      engine="$("$vpy" -c "import json,sys;print(json.load(open(sys.argv[1],encoding='utf-8')).get('engine','elevenlabs'))" "$FACELESS_TTS_PLAN" 2>/dev/null)"
      local emo_srt="${out_wav%.wav}.srt"
      case "$engine" in
        typecast)
          local tc_key="${TYPECAST_API_KEY:-}"
          [[ -z "$tc_key" && -f /g/config/typecast/api.key ]] && tc_key="$(cat /g/config/typecast/api.key)"
          [[ -z "$tc_key" && -f "G:/config/typecast/api.key" ]] && tc_key="$(cat "G:/config/typecast/api.key")"
          if [[ -n "$tc_key" ]]; then
            log_info "  tts backend: Typecast (plan=$(basename "$FACELESS_TTS_PLAN"))"
            if TYPECAST_API_KEY="$tc_key" PYTHONUTF8=1 \
                 "$vpy" "$REPO_ROOT/scripts/typecast-tts.py" --plan "$FACELESS_TTS_PLAN" --out "$out_wav"; then
              [[ -f "$emo_srt" ]] && cp -f "$emo_srt" "${out_wav%.wav}.edge.srt"
              TTS_LABEL="Typecast ssfm-v30 (synthetic AI narration, emotion-directed)"
              return 0
            fi
          else
            log_warn "  Typecast key missing"
          fi
          ;;
        *)  # elevenlabs (default)
          local el_key="${ELEVENLABS_API_KEY:-}"
          [[ -z "$el_key" && -f /g/config/elevenlabs/api.key ]] && el_key="$(cat /g/config/elevenlabs/api.key)"
          [[ -z "$el_key" && -f "G:/config/elevenlabs/api.key" ]] && el_key="$(cat "G:/config/elevenlabs/api.key")"
          if [[ -n "$el_key" ]]; then
            log_info "  tts backend: ElevenLabs v3 (plan=$(basename "$FACELESS_TTS_PLAN"))"
            if ELEVENLABS_API_KEY="$el_key" PYTHONUTF8=1 \
                 "$vpy" "$REPO_ROOT/scripts/elevenlabs-tts.py" --plan "$FACELESS_TTS_PLAN" --out "$out_wav"; then
              [[ -f "$emo_srt" ]] && cp -f "$emo_srt" "${out_wav%.wav}.edge.srt"
              TTS_LABEL="ElevenLabs v3 (synthetic AI narration, emotion-directed)"
              return 0
            fi
          else
            log_warn "  ElevenLabs key missing"
          fi
          ;;
      esac
      log_warn "  emotion-TTS ($engine) failed; falling back to Kokoro/edge-tts"
    else
      log_warn "  FACELESS_TTS_PLAN set but venv python missing — skipping"
    fi
  fi

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
    local vpy; vpy="$(_venv_python)" || vpy=""
    if [[ -n "$vpy" && -f "$kokoro_py" \
          && -f "$model_path" && -f "$voices_path" ]]; then
      if "$vpy" -c "from kokoro_onnx import Kokoro" >/dev/null 2>&1; then
        log_info "  tts backend: Kokoro-ONNX (voice=${voice_hint:-am_michael})"
        # Kokoro emits WAV at native sample rate (24 kHz typically).  Transcode
        # to 44.1 kHz stereo so it joins the AAC pipeline cleanly downstream.
        local tmp_wav="${out_wav%.wav}.kokoro.wav"
        if "$vpy" "$kokoro_py" \
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

  # Edge-TTS backend (Microsoft Edge online neural TTS).  FREE, no API key.
  # Primary path for languages Kokoro doesn't cover (notably Korean,
  # ko-KR-*Neural) and the only neural voice off-macOS.  Emits MP3 + an aligned
  # SRT (word boundaries); transcode the MP3 to 44.1 kHz stereo for the AAC
  # pipeline.  The .edge.srt is left beside the WAV for callers that prefer
  # word-accurate captions over ASR.
  if [[ -n "$voice_hint" ]] && _is_edge_voice "$voice_hint"; then
    local vpy; vpy="$(_venv_python)" || vpy=""
    if [[ -n "$vpy" ]] && "$vpy" -c "import edge_tts" >/dev/null 2>&1; then
      log_info "  tts backend: edge-tts (voice=$voice_hint)"
      local tmp_mp3="${out_wav%.wav}.edge.mp3"
      local srt_out="${out_wav%.wav}.edge.srt"
      if "$vpy" -m edge_tts --voice "$voice_hint" \
            --file "$text_file" \
            --write-media "$tmp_mp3" \
            --write-subtitles "$srt_out"; then
        "$FFMPEG_BIN" -y -loglevel error -i "$tmp_mp3" -ar 44100 -ac 2 "$out_wav"
        rm -f "$tmp_mp3"
        TTS_LABEL="edge-tts neural (synthetic AI narration)"
        return 0
      fi
      log_warn "  edge-tts failed; falling back to say"
    fi
  fi

  # Fallback (and primary path for non-Kokoro voices on macOS): macOS say.
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
