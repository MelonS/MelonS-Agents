#!/usr/bin/env bash
# Ollama helpers — call local LLM via HTTP API. Source after env.sh + log.sh.

# Generate completion. JSON-escapes the prompt internally.
# Usage: ollama_generate <model> <prompt> [json_format=true|false]
ollama_generate() {
  local model="$1" prompt="$2" json_format="${3:-false}"
  local payload
  payload=$(jq -n --arg m "$model" --arg p "$prompt" --argjson j "$json_format" '
    if $j then
      { model: $m, prompt: $p, stream: false, format: "json", options: { temperature: 0.2 } }
    else
      { model: $m, prompt: $p, stream: false, options: { temperature: 0.2 } }
    end
  ')
  curl -fsS -X POST "$OLLAMA_HOST/api/generate" \
    -H 'Content-Type: application/json' \
    -d "$payload" \
    | jq -r '.response'
}

# Ensure a model is pulled. Returns 0 if available (existing or pulled now).
ollama_ensure_model() {
  local model="$1"
  if ollama list 2>/dev/null | awk '{print $1}' | grep -qx "$model"; then
    return 0
  fi
  log_info "pulling ollama model: $model"
  ollama pull "$model" >&2
}
