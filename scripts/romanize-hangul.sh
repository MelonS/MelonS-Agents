#!/usr/bin/env bash
# romanize-hangul.sh — Revised Romanization of Hangul text using
# stdlib unicodedata (no external library required).
#
# Each Hangul syllable's Unicode name embeds its official Revised
# Romanization (e.g. 변 = HANGUL SYLLABLE BYEON).  This script
# extracts those names character-by-character and joins them as
# space-separated syllables within Hangul words.
#
# Per docs/research/2026-05-22-music-video-pro-practices.md §8:
# KR canonical lyric-video sites (InkiStyle) universally stack
# Hangul above Romanized.  This script generates the romanized
# companion so music-video-lyrics.sh can render the stack.
#
# Usage:
#   scripts/romanize-hangul.sh <input.txt> [<output.txt>]
#   scripts/romanize-hangul.sh - < text > romanized
#
# Default <output.txt>: <input>.romanized.txt next to source.

set -uo pipefail

SRC="${1:-}"
OUT="${2:-}"

if [[ -z "$SRC" ]]; then
  echo "usage: $0 <input.txt> [output.txt]" >&2
  echo "       $0 - < text > romanized" >&2
  exit 64
fi

if [[ "$SRC" == "-" ]]; then
  # stdin mode
  python3 -c '
import sys, unicodedata, re
def romanize_char(ch):
    try:
        name = unicodedata.name(ch)
        if name.startswith("HANGUL SYLLABLE "):
            return name[len("HANGUL SYLLABLE "):].lower()
    except ValueError:
        pass
    return ch

for line in sys.stdin:
    out_chars = []
    for ch in line.rstrip("\n"):
        if "가" <= ch <= "힣":  # Hangul syllable range
            out_chars.append(romanize_char(ch) + "·")
        else:
            out_chars.append(ch)
    s = "".join(out_chars)
    # Join syllables within Hangul words (drop · within word, keep word-space)
    s = re.sub(r"·(\S)", r" \1", s)
    s = s.replace("·", "")
    print(s)
'
  exit 0
fi

[[ -f "$SRC" ]] || { echo "input not found: $SRC" >&2; exit 64; }
[[ -z "$OUT" ]] && OUT="${SRC%.txt}.romanized.txt"

python3 - "$SRC" "$OUT" <<'PY'
import sys, unicodedata, re

src, out_path = sys.argv[1], sys.argv[2]

def romanize_char(ch):
    try:
        name = unicodedata.name(ch)
        if name.startswith("HANGUL SYLLABLE "):
            return name[len("HANGUL SYLLABLE "):].lower()
    except ValueError:
        pass
    return ch

lines_out = []
with open(src, encoding="utf-8") as f:
    for line in f:
        ln = line.rstrip("\n")
        if not ln.strip() or ln.startswith("#") or ln.startswith("["):
            lines_out.append(ln)
            continue
        out_chars = []
        for ch in ln:
            if "가" <= ch <= "힣":
                out_chars.append(romanize_char(ch) + "·")
            else:
                out_chars.append(ch)
        s = "".join(out_chars)
        s = re.sub(r"·(\S)", r" \1", s)  # syllable boundary within word
        s = s.replace("·", "")             # trailing · at word end
        lines_out.append(s)

with open(out_path, "w", encoding="utf-8") as f:
    if not any(l.startswith("#") for l in lines_out[:2]):
        f.write("# Auto-romanized from " + src + " by romanize-hangul.sh\n")
    for ln in lines_out:
        f.write(ln + "\n")

print(f"[romanize] wrote {out_path}")
PY
