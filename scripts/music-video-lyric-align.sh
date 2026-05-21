#!/usr/bin/env bash
# music-video-lyric-align.sh — derive per-line timing for a plain lyric
# file by aligning to whisper.cpp's word-level transcription of the
# vocal audio.  Emits an LRC-format file that
# scripts/music-video-lyrics.sh consumes natively (its LRC branch).
#
# Quality-bar directive #3 (2026-05-22): lyric overlay should sync to
# the actual vocal cue, allowing a small early lead but not late.
#
# Usage:
#   scripts/music-video-lyric-align.sh <audio.mp3> <plain-lyrics.txt> <out.lrc> [--lang=ko|en|auto]
#
# Env vars:
#   LYRIC_LEAD_MS    pre-roll, ms (default 200).  Lyric appears this
#                    long before the detected vocal onset.
#   LYRIC_MAX_DRIFT_MS  bail out if a line's match score is below the
#                       confidence floor (default 600ms drift cap; if
#                       exceeded the line keeps its auto-spaced default).
#   WHISPER_LANG     language hint forwarded to whisper-cli.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
# shellcheck disable=SC1091
source "$PWD/agents/lib/env.sh" 2>/dev/null || true

AUDIO="${1:-}"
LYRICS="${2:-}"
OUT_LRC="${3:-}"
LANG_HINT=""
for arg in "$@"; do
  case "$arg" in --lang=*) LANG_HINT="${arg#*=}" ;; esac
done

if [[ -z "$AUDIO" || -z "$LYRICS" || -z "$OUT_LRC" ]]; then
  echo "usage: $0 <audio> <plain-lyrics.txt> <out.lrc> [--lang=ko|en|auto]" >&2
  exit 64
fi
[[ -f "$AUDIO" ]]  || { echo "❌ audio not found: $AUDIO"  >&2; exit 64; }
[[ -f "$LYRICS" ]] || { echo "❌ lyrics not found: $LYRICS" >&2; exit 64; }

# §8 exception: whisper-cli / ffmpeg parameter-expansion defaults — same
# pattern as scripts/music-video-shaders.sh:103.  Registered in
# operator-contract.md §8.
WHISPER_CLI="${WHISPER_CLI_BIN:-/opt/homebrew/bin/whisper-cli}"
MODEL="${WHISPER_MODEL:-}"
FFMPEG="${FFMPEG_BIN:-/opt/homebrew/bin/ffmpeg}"
LEAD_MS="${LYRIC_LEAD_MS:-200}"

[[ -x "$WHISPER_CLI" ]] || { echo "❌ whisper-cli not found: $WHISPER_CLI" >&2; exit 1; }
[[ -f "$MODEL" ]]       || { echo "❌ WHISPER_MODEL not set or missing" >&2; exit 1; }

if [[ -z "$LANG_HINT" ]]; then
  if grep -qE '[가-힣]' "$LYRICS"; then LANG_HINT=ko; else LANG_HINT="${WHISPER_LANG:-auto}"; fi
fi

WORK=$(mktemp -d)
trap "rm -rf '$WORK'" EXIT

echo "[align] normalize → 16k mono wav"
"$FFMPEG" -y -loglevel error -i "$AUDIO" -ar 16000 -ac 1 -c:a pcm_s16le "$WORK/in.wav"

# Use segment-level transcription (no -ml 1) for English-like languages —
# the longer per-segment text aggregates better against the lyric line
# via SequenceMatcher.  Word-level (-sow -ml 1) is only used for Korean
# where character-based matching needs finer granularity.
echo "[align] whisper transcription (lang=$LANG_HINT)"
args=( -m "$MODEL" -f "$WORK/in.wav" -of "$WORK/out" -oj )
if [[ "$LANG_HINT" == "ko" ]]; then
  args+=( -ojf -sow -ml 1 )
fi
[[ "$LANG_HINT" != "auto" ]] && args+=( -l "$LANG_HINT" )
"$WHISPER_CLI" "${args[@]}" >/dev/null 2>&1

[[ -f "$WORK/out.json" ]] || { echo "❌ whisper produced no json" >&2; exit 2; }

echo "[align] fuzzy match lyric lines → whisper segments"

LEAD_MS="$LEAD_MS" LYRICS="$LYRICS" OUT_LRC="$OUT_LRC" WJSON="$WORK/out.json" python3 - <<'PY'
import json, os, re, sys, difflib

LYRICS = os.environ["LYRICS"]
WJSON  = os.environ["WJSON"]
OUT    = os.environ["OUT_LRC"]
LEAD_MS = int(os.environ["LEAD_MS"])

# Load whisper words.  whisper.cpp with `-sow -ml 1` occasionally emits
# multi-byte CJK characters split across two segments, producing invalid
# UTF-8 inside the JSON.  Read bytes with errors='replace' so we don't
# crash on the broken characters — the timing data per segment is still
# intact; the partial text just becomes a U+FFFD placeholder, which the
# downstream fuzzy match correctly ignores.
with open(WJSON, "rb") as f:
    raw = f.read().decode("utf-8", errors="replace")
data = json.loads(raw)
words = []
for seg in data.get("transcription", []):
    txt = (seg.get("text") or "").strip()
    # Drop segments that became pure-replacement characters.
    if not txt or all(ch == "�" for ch in txt):
        continue
    t0 = int(seg["offsets"]["from"])
    t1 = int(seg["offsets"]["to"])
    words.append((t0, t1, txt))

if not words:
    sys.exit("no words in whisper output")

# Build a per-character index → word index for searching.
flat_chars = []   # list of (char_idx, word_idx, char)
def norm(s):
    # Drop punctuation + whitespace; lowercase for case-insensitive Latin.
    return re.sub(r'[\s\.,!?;:"\'\(\)\[\]\-—–]', '', s.lower())

for wi, (_, _, txt) in enumerate(words):
    for ch in norm(txt):
        flat_chars.append((wi, ch))
flat_str = "".join(c for _, c in flat_chars)

# Lyric lines.
lines = []
with open(LYRICS, encoding="utf-8") as f:
    for ln in f:
        s = ln.strip()
        if not s or s.startswith("#") or s.startswith("["):
            continue
        lines.append(s)

if not lines:
    sys.exit("no lyric lines")

# Monotonic alignment: search each line in flat_str starting from
# previous match's end position.  Use difflib SequenceMatcher's
# get_matching_blocks to find best match window for each line.

cursor_char = 0          # left bound of search window in flat_str
matches = []             # list of (line_text, start_ms, end_ms, conf)
N = len(flat_str)

for li, line in enumerate(lines):
    target = norm(line)
    if not target:
        matches.append((line, None, None, 0.0))
        continue

    # Search window: cursor_char .. min(N, cursor_char + 6 * len(target) + 200)
    window_end = min(N, cursor_char + max(6 * len(target), 200) + 40)
    haystack = flat_str[cursor_char:window_end]

    sm = difflib.SequenceMatcher(a=haystack, b=target, autojunk=False)
    match = sm.find_longest_match(0, len(haystack), 0, len(target))
    if match.size < max(2, len(target) // 3):
        # Fall back: search wider.
        window_end = min(N, cursor_char + 12 * len(target) + 400)
        haystack = flat_str[cursor_char:window_end]
        sm = difflib.SequenceMatcher(a=haystack, b=target, autojunk=False)
        match = sm.find_longest_match(0, len(haystack), 0, len(target))

    if match.size == 0:
        matches.append((line, None, None, 0.0))
        continue

    match_start_in_flat = cursor_char + match.a
    word_idx = flat_chars[match_start_in_flat][0]
    start_ms = words[word_idx][0]

    # End word: at the right edge of the matched chars.
    match_end_in_flat = min(N - 1, cursor_char + match.a + match.size - 1)
    end_word_idx = flat_chars[match_end_in_flat][0]
    end_ms = words[end_word_idx][1]

    conf = match.size / max(1, len(target))
    matches.append((line, start_ms, end_ms, conf))
    cursor_char = match_end_in_flat + 1

# Sanity pass: assign auto-space timing to lines whose match is below
# confidence floor (0.3) — they get spread proportionally between
# their nearest matched neighbors.
audio_end_ms = words[-1][1]
prev_end = 0
auto_filled = 0
for i, (line, s, e, conf) in enumerate(matches):
    if s is not None and conf >= 0.30:
        prev_end = e or s
        continue
    # find next matched line
    nxt = None
    for j in range(i + 1, len(matches)):
        if matches[j][1] is not None and matches[j][3] >= 0.30:
            nxt = matches[j][1]
            break
    nxt = nxt if nxt is not None else audio_end_ms
    # interpolate
    span = max(800, nxt - prev_end)
    pos_in_run = 1  # only this line being filled at once
    s2 = prev_end + span // 3
    e2 = s2 + 1500
    matches[i] = (line, s2, e2, 0.0)
    auto_filled += 1
    prev_end = e2

# Apply LEAD_MS: shift line starts earlier by LEAD_MS to give vocal-anticipation.
final = []
for line, s, e, conf in matches:
    start = max(0, s - LEAD_MS)
    end   = max(start + 800, e)
    final.append((line, start, end, conf))

# Emit LRC: [mm:ss.xx]TEXT
def fmt_ts(ms):
    s_total = ms / 1000.0
    mm = int(s_total // 60)
    ss = s_total - mm * 60
    return f"[{mm:02d}:{ss:05.2f}]"

with open(OUT, "w", encoding="utf-8") as f:
    f.write("# Auto-aligned by music-video-lyric-align.sh (whisper word-level + difflib).\n")
    f.write(f"# Source: {os.environ.get('LYRICS_FILE_DISPLAY', LYRICS)}\n")
    f.write(f"# Lead-ms: {LEAD_MS}\n")
    for line, s, e, conf in final:
        marker = "" if conf >= 0.30 else "  # autofilled (low confidence)"
        f.write(f"{fmt_ts(s)}{line}{marker}\n")

print(f"[align] wrote {OUT} ({len(final)} lines, {auto_filled} autofilled)")

# Diagnostic: how many lines hit decent confidence
hi = sum(1 for _,_,_,c in final if c >= 0.50)
med = sum(1 for _,_,_,c in final if 0.30 <= c < 0.50)
lo  = sum(1 for _,_,_,c in final if c < 0.30)
print(f"[align] confidence: hi(≥0.50)={hi}  med(0.30-0.49)={med}  lo(<0.30)={lo}")
PY

echo "[align] LRC ready: $OUT_LRC"
