#!/usr/bin/env bash
# whisper.cpp helpers. Source after env.sh + log.sh.

# Transcribe a media file. Produces <out_prefix>.json with segments.
# Usage: whisper_transcribe <input_media> <out_prefix> [language=$WHISPER_LANG or "auto"]
#
# Language precedence: explicit 3rd arg > WHISPER_LANG env var > "auto".
# Set WHISPER_LANG=ko when running on content the model mis-identifies on
# auto (e.g., accented or casual Korean speech sometimes comes back as
# [FOREIGN] under auto-detect).
whisper_transcribe() {
  local input="$1" out_prefix="$2" lang="${3:-${WHISPER_LANG:-auto}}"

  require_bin "$WHISPER_CLI_BIN" "$FFMPEG_BIN" || return 1
  [[ -f "$WHISPER_MODEL" ]] || { log_err "WHISPER_MODEL not found: $WHISPER_MODEL"; return 1; }

  # whisper-cli wants 16kHz mono WAV. Always normalize via ffmpeg first.
  local wav="${out_prefix}.16k.wav"
  log_info "normalizing audio → $wav"
  "$FFMPEG_BIN" -y -loglevel error -i "$input" -ar 16000 -ac 1 -c:a pcm_s16le "$wav"

  log_info "transcribing with whisper.cpp (model: $(basename "$WHISPER_MODEL"), lang: $lang)"
  local args=( -m "$WHISPER_MODEL" -f "$wav" -of "$out_prefix" -oj )
  [[ "$lang" != "auto" ]] && args+=( -l "$lang" )

  "$WHISPER_CLI_BIN" "${args[@]}" >/dev/null

  [[ -f "${out_prefix}.json" ]] || { log_err "transcript JSON not produced"; return 1; }
  echo "${out_prefix}.json"
}

# Extract a flat list of {start, end, text} segments from whisper.cpp JSON.
whisper_segments() {
  local json="$1"
  jq -c '[.transcription[]? | {start: (.offsets.from / 1000), end: (.offsets.to / 1000), text: (.text | gsub("^ +|  +$"; ""))}]' "$json"
}
