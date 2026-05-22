#!/usr/bin/env bash
# lyric-extract.sh — transcribe a vocal track via whisper and emit a
# plain-text lyric file ready for `music-video-lyrics.sh
# --align-to-audio=...` to time against.
#
# Useful when:
#   - Operator has a Suno take they never wrote a prompt for (rare).
#   - Suno's actual output drifted enough from the prompt that the
#     align-script's drift gate refuses to render (Phase A.2 FAIL).
#     In that case, extracting THIS take's actual sung lyrics and
#     using THAT as the lyric file flips the alignment back to OK.
#
# Whisper transcribes mixed-music audio with non-trivial error rate
# (the percussion + instrumental layer add noise), so this script
# is best-effort.  Operator should review the output before using
# it as the canonical lyric file.
#
# Usage:
#   scripts/lyric-extract.sh <audio.mp3> <out.txt> [--lang=ko|en|auto]
#
# Env:
#   WHISPER_LANG     fallback language hint
#
# Output format: one line per detected lyrical phrase.  Whisper's
# default segmentation (~5-10s phrases) is usually adequate for
# music videos; long segments are split on em-dash / period.

set -uo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"
# shellcheck disable=SC1091
source "$REPO_ROOT/agents/lib/env.sh" 2>/dev/null || true

AUDIO="${1:-}"
OUT="${2:-}"
LANG_HINT=""
for arg in "$@"; do
  case "$arg" in --lang=*) LANG_HINT="${arg#*=}" ;; esac
done

if [[ -z "$AUDIO" || -z "$OUT" ]]; then
  echo "usage: $0 <audio> <out.txt> [--lang=ko|en|auto]" >&2
  exit 64
fi
[[ -f "$AUDIO" ]] || { echo "audio not found: $AUDIO" >&2; exit 64; }

WHISPER_CLI="${WHISPER_CLI_BIN:-/opt/homebrew/bin/whisper-cli}"
MODEL="${WHISPER_MODEL:-}"
FFMPEG="${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}"

[[ -x "$WHISPER_CLI" ]] || { echo "whisper-cli not found: $WHISPER_CLI" >&2; exit 1; }
[[ -f "$MODEL" ]] || { echo "WHISPER_MODEL not set / missing" >&2; exit 1; }

# Lang resolution.  No lyric file to peek at, so the hint is the only
# signal — fall back to WHISPER_LANG or auto.
if [[ -z "$LANG_HINT" ]]; then
  LANG_HINT="${WHISPER_LANG:-auto}"
fi

WORK=$(mktemp -d)
trap "rm -rf '$WORK'" EXIT

echo "[extract] normalize → 16k mono wav"
"$FFMPEG" -y -loglevel error -i "$AUDIO" -ar 16000 -ac 1 -c:a pcm_s16le "$WORK/in.wav"

echo "[extract] whisper transcription (lang=$LANG_HINT)"
args=( -m "$MODEL" -f "$WORK/in.wav" -of "$WORK/out" -oj )
[[ "$LANG_HINT" != "auto" ]] && args+=( -l "$LANG_HINT" )
"$WHISPER_CLI" "${args[@]}" >/dev/null 2>&1

[[ -f "$WORK/out.json" ]] || { echo "whisper produced no json" >&2; exit 2; }

# Emit one line per non-empty whisper segment.  Trim leading/trailing
# whitespace; drop bracketed annotations ([MUSIC] / [APPLAUSE]) and
# empty segments.  Read with errors='replace' for the same multi-byte
# split issue the align script handles.
WJSON="$WORK/out.json" OUT="$OUT" python3 - <<'PY'
import json, os, re, sys

with open(os.environ["WJSON"], "rb") as f:
    data = json.loads(f.read().decode("utf-8", errors="replace"))

lines = []
for seg in data.get("transcription", []):
    txt = (seg.get("text") or "").strip()
    # Drop whisper non-lexical markers.
    if not txt or re.match(r"^\[.*\]$", txt):
        continue
    # Collapse internal whitespace.
    txt = re.sub(r"\s+", " ", txt)
    # Replace U+FFFD (replacement char from multi-byte split) with nothing.
    txt = txt.replace("�", "")
    # Strip whisper's lyrical-content markers: leading / trailing ♪
    # (often paired) and parenthetical scene notes like "(upbeat music)".
    txt = re.sub(r"^♪\s*", "", txt)
    txt = re.sub(r"\s*♪$", "", txt)
    txt = re.sub(r"^\(.+\)$", "", txt)
    txt = txt.strip()
    if not txt:
        continue
    lines.append(txt)

# Drop exact duplicates introduced by whisper's overlap (~1-3 lines
# common at segment boundaries).
seen = set()
deduped = []
for ln in lines:
    key = ln.lower()
    if key in seen:
        continue
    seen.add(key)
    deduped.append(ln)

with open(os.environ["OUT"], "w", encoding="utf-8") as f:
    f.write("# Auto-extracted by scripts/lyric-extract.sh\n")
    f.write("# Whisper transcription; review for accuracy before using.\n")
    for ln in deduped:
        f.write(ln + "\n")

print(f"[extract] wrote {len(deduped)} lines → {os.environ['OUT']}")
PY
echo "[extract] done.  Review with: cat $OUT"
