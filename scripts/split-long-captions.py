"""Enforce single-line caption rendering by splitting long SRT cues.

Two-line captions in faceless-short renders are visually noisy on
mobile: with BorderStyle=3 (opaque box per line), the second line's
box can graze the first line's box and the result reads as overlap.

This pass takes a corrected SRT and, for any cue whose text exceeds
CHAR_MAX (default 28 characters), splits the cue into sub-cues at
natural break points:

  1. Punctuation-first: if a cue has internal commas, semicolons,
     em-dashes, or periods, split there.  These match the natural
     speech pauses, so the boundary doesn't read as awkward.
  2. Fallback: if punctuation-split still leaves a chunk longer than
     CHAR_MAX, greedy word-split.  Avoids orphaning single trailing
     connectors by carrying them into the next chunk.

Sub-cues shorter than MIN_DURATION_S are merged into the previous
sub-cue (extending its end) to avoid sub-second blips.

CHAR_MAX is a RENDERED-WIDTH budget in half-glyph units, not a code-
point count: East-Asian full-width glyphs (Hangul/CJK) count as 2,
Latin as 1 (see visual_width).  Tuned for libass Fontsize=44 in a
1080-wide frame with MarginH=100 (~880 px usable).  At ~50 px per
Korean glyph and ~22 px per Latin char, 28 width-units ≈ 28 Latin
chars OR ~14 Hangul glyphs — both ~one line.  Counting code points
here was the original bug: 28 Hangul glyphs are ~1400 px, wrapping to
two lines whose BorderStyle=3 boxes graze into the reported overlap.

Usage:
  python3 scripts/split-long-captions.py \
    --srt-in <corrected.srt> \
    --srt-out <single-line.srt> \
    [--char-max 28] [--min-duration 1.0]
"""

import argparse
import re
import sys
import unicodedata


_TS_RE = re.compile(r"^(\d{2}):(\d{2}):(\d{2}),(\d{3})$")

# Punctuation we'll prefer to break on (split point goes AFTER the punct
# so the leading chunk ends with the natural pause marker).
_PUNCT_SPLIT_RE = re.compile(r"([,;—–:\.\?!])\s+")


def visual_width(text: str) -> int:
    """Rendered width in half-glyph units: East-Asian Wide/Fullwidth
    characters (CJK, incl. Hangul) count as 2, everything else as 1.

    Counting code points (``len``) treats a Korean glyph the same as a
    Latin one, but a Hangul block renders ~2x wider.  That let a Korean
    cue whose text is visually two lines wide slip under a code-point
    ``char_max`` and wrap — the exact 2-line box-overlap this splitter
    exists to prevent.  So ``char_max`` is a WIDTH budget, not a char
    count: 28 units == 28 Latin chars OR ~14 Hangul glyphs, each about
    one line inside the ~880 px caption safe-zone."""
    return sum(2 if unicodedata.east_asian_width(ch) in ("W", "F") else 1 for ch in text)


def ts_to_sec(ts: str) -> float:
    m = _TS_RE.match(ts.strip())
    if not m:
        return 0.0
    h, mn, s, ms = map(int, m.groups())
    return h * 3600 + mn * 60 + s + ms / 1000.0


def sec_to_ts(sec: float) -> str:
    if sec < 0:
        sec = 0
    h = int(sec // 3600)
    sec -= h * 3600
    mn = int(sec // 60)
    sec -= mn * 60
    s = int(sec)
    ms = int(round((sec - s) * 1000))
    if ms >= 1000:
        ms = 999
    return f"{h:02d}:{mn:02d}:{s:02d},{ms:03d}"


def parse_srt(text: str):
    cues = []
    blocks = re.split(r"\n\s*\n", text.strip())
    arrow_re = re.compile(r"^([\d:,]+)\s*-->\s*([\d:,]+)")
    for blk in blocks:
        lines = [ln for ln in blk.splitlines() if ln.strip()]
        if len(lines) < 2:
            continue
        ts_line = lines[1] if "-->" in lines[1] else (lines[0] if "-->" in lines[0] else None)
        if ts_line is None:
            continue
        m = arrow_re.match(ts_line.strip())
        if not m:
            continue
        start = ts_to_sec(m.group(1))
        end = ts_to_sec(m.group(2))
        text_lines = lines[2:] if "-->" in lines[1] else lines[1:]
        cues.append((start, end, " ".join(t for t in text_lines if t)))
    return cues


def split_on_punct(text: str):
    """Return list of (chunk_text, ends_with_terminal) tuples.  The
    boolean is true when the chunk ends in '.', '?', or '!' — used by
    callers if they care about hard stops.  Empty for now but cheap to
    expose."""
    parts = _PUNCT_SPLIT_RE.split(text)
    # split returns [seg, punct, seg, punct, ..., last_seg]
    out = []
    buf = ""
    for i, p in enumerate(parts):
        if i % 2 == 0:
            buf = (buf + " " + p).strip() if buf else p.strip()
        else:
            buf += p
            out.append(buf)
            buf = ""
    if buf:
        out.append(buf)
    return [c for c in out if c]


def greedy_word_split(text: str, char_max: int):
    """Pack words greedily into <= char_max chunks.  When a chunk would
    end with a single trailing connector (1-char Korean particle or
    English connector word like 'and'/'the'/'a'), pull that token to
    the next chunk to avoid orphaning."""
    orphan_blocklist = {"a", "an", "the", "and", "or", "but", "of", "to", "is", "with", "by", "in", "on", "at", "as", "for"}
    words = text.split()
    if not words:
        return [text]
    chunks = []
    current = []
    cur_len = 0
    for w in words:
        added_len = visual_width(w) if not current else cur_len + 1 + visual_width(w)
        if current and added_len > char_max:
            # Check if the last word in `current` is an orphan; if so,
            # move it to start of the new chunk.  (len==1 stays a
            # code-point test — it detects single-token particles.)
            if current and (current[-1].lower() in orphan_blocklist or len(current[-1]) == 1):
                trailing = current.pop()
                cur_len -= visual_width(trailing) + 1
                if current:
                    chunks.append(" ".join(current))
                current = [trailing, w]
                cur_len = visual_width(trailing) + 1 + visual_width(w)
            else:
                chunks.append(" ".join(current))
                current = [w]
                cur_len = visual_width(w)
        else:
            current.append(w)
            cur_len = added_len
    if current:
        chunks.append(" ".join(current))
    return chunks


def split_text(text: str, char_max: int):
    """Two-pass split: punctuation first, then word-greedy on leftover
    long chunks."""
    if visual_width(text) <= char_max:
        return [text]

    # First pass: split on natural punctuation.
    primary = split_on_punct(text)
    if not primary:
        primary = [text]

    out = []
    for chunk in primary:
        if visual_width(chunk) <= char_max:
            out.append(chunk)
        else:
            out.extend(greedy_word_split(chunk, char_max))
    return out


def merge_short_neighbours(items, min_duration: float, char_max: int = 28):
    """items: list of (start, end, text).  Merge any item shorter than
    min_duration into its previous sibling by extending the previous
    item's end and concatenating texts — but ONLY when the merged text
    still fits one line (visual_width <= char_max).

    The width guard is load-bearing: without it, merging the short tail
    cues that split_text just produced (e.g. Korean "리브," / "메이,"
    fragments, each < 1 s) concatenates them straight back into the
    exact 2-line-wide cue the splitter existed to break up.  A sub-second
    single-line blip is the lesser evil than a 2-line box overlap, so
    width wins over min_duration."""
    if not items:
        return items
    merged = [items[0]]
    for s, e, t in items[1:]:
        ps, pe, pt = merged[-1]
        combined = (pt + " " + t).strip()
        if e - s < min_duration and visual_width(combined) <= char_max:
            merged[-1] = (ps, e, combined)
        else:
            merged.append((s, e, t))
    # Handle first item being too short by pulling start of next earlier.
    # (Skipped in this minimal pass — the script-aware corrector tends
    # not to produce sub-1s leading cues.)
    return merged


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--srt-in", required=True)
    p.add_argument("--srt-out", required=True)
    p.add_argument("--char-max", type=int, default=28,
                   help="max rendered width in half-glyph units "
                        "(CJK/Hangul=2, Latin=1); 28 ~= one caption line")
    p.add_argument("--min-duration", type=float, default=1.0)
    args = p.parse_args()

    with open(args.srt_in, encoding="utf-8") as f:
        cues = parse_srt(f.read())

    expanded = []
    split_count = 0
    for start, end, text in cues:
        chunks = split_text(text, args.char_max)
        if len(chunks) == 1:
            expanded.append((start, end, text))
            continue

        split_count += 1
        total_chars = sum(len(c) for c in chunks)
        cur_start = start
        total_dur = end - start
        for i, chunk in enumerate(chunks):
            if i == len(chunks) - 1:
                cue_end = end
            else:
                share = len(chunk) / total_chars
                cue_end = cur_start + total_dur * share
            expanded.append((cur_start, cue_end, chunk))
            cur_start = cue_end

    expanded = merge_short_neighbours(expanded, args.min_duration, args.char_max)

    out_lines = []
    for i, (s, e, t) in enumerate(expanded, 1):
        out_lines.append(f"{i}")
        out_lines.append(f"{sec_to_ts(s)} --> {sec_to_ts(e)}")
        out_lines.append(t)
        out_lines.append("")

    with open(args.srt_out, "w", encoding="utf-8") as f:
        f.write("\n".join(out_lines).rstrip() + "\n")

    print(f"split-long-captions: {split_count} cues split, {len(expanded)} cues out (char_max={args.char_max}, min_duration={args.min_duration}s)", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
