"""Script-aware caption correction for faceless-short pilots.

Problem:
  When a faceless short is built from a synthesized narration, the source
  script IS the ground truth.  Whisper.cpp transcribes the synthetic
  audio for timing, but its small-model text drifts on proper nouns
  ("Hattusa" → "Hadusa", "Anatolia" → "Anatoria", etc.).  Those drifts
  burn into the captions and become visible defects.

Approach:
  Use whisper segments for TIMING only.  Replace the text of each segment
  with the corresponding span of the original script via token alignment.

Algorithm:
  1. Tokenize script + SRT (whitespace + simple punctuation handling).
  2. Use difflib.SequenceMatcher (case-folded, punctuation-stripped) to
     align whisper tokens against script tokens.  This gives us, for
     each whisper token position, a matching script token position (or
     a deletion if whisper invented words that aren't in the script).
  3. For each SRT cue, compute its [start_tok, end_tok) span over the
     whisper token stream, then emit the matching script token span as
     the corrected text — preserving the script's original punctuation
     and capitalization.

Usage:
  python3 scripts/correct-captions.py \
    --script <narration.txt> \
    --srt-in <whisper.srt> \
    --srt-out <corrected.srt>

  Optionally writes a diff to stderr if --verbose.
"""

import argparse
import difflib
import re
import sys


_PUNCT_STRIP = re.compile(r"[^\w']+", re.UNICODE)


def normalize(tok: str) -> str:
    """Lowercase + strip surrounding punctuation for matching only.
    The original token is kept for output."""
    return _PUNCT_STRIP.sub("", tok).lower()


def tokenize(text: str):
    """Split text into whitespace-separated tokens, preserving punctuation
    on each token (e.g., 'Hittites,' is one token).  Empty tokens are dropped."""
    return [t for t in re.split(r"\s+", text.strip()) if t]


def parse_srt(srt_text: str):
    """Parse SRT into a list of dicts: {index, start, end, text, tokens}.
    `tokens` is the whitespace-split token list of the cue text."""
    cues = []
    blocks = re.split(r"\n\s*\n", srt_text.strip())
    ts_re = re.compile(r"^(\d{2}:\d{2}:\d{2},\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2},\d{3})")
    for blk in blocks:
        lines = [ln for ln in blk.splitlines() if ln.strip()]
        if len(lines) < 2:
            continue
        idx_line = lines[0].strip()
        ts_line = lines[1].strip() if len(lines) > 1 else ""
        m = ts_re.match(ts_line)
        if not m:
            continue
        text = " ".join(lines[2:]).strip()
        cues.append({
            "index": int(idx_line) if idx_line.isdigit() else len(cues) + 1,
            "start": m.group(1),
            "end": m.group(2),
            "text": text,
            "tokens": tokenize(text),
        })
    return cues


def align_token_streams(script_toks, whisper_toks):
    """Return a mapping: whisper_index -> script_index_span (start_inclusive,
    end_exclusive).  Whisper tokens that don't map (insertions) get
    span (None, None)."""
    sn = [normalize(t) for t in script_toks]
    wn = [normalize(t) for t in whisper_toks]
    matcher = difflib.SequenceMatcher(a=wn, b=sn, autojunk=False)
    mapping = [(None, None)] * len(whisper_toks)
    for tag, i1, i2, j1, j2 in matcher.get_opcodes():
        if tag == "equal":
            for k in range(i2 - i1):
                mapping[i1 + k] = (j1 + k, j1 + k + 1)
        elif tag == "replace":
            # Distribute the script span [j1, j2) proportionally across
            # whisper tokens [i1, i2).  This handles cases like a single
            # whisper token "Hadusa" replaced by "Hattusa," — one-to-one,
            # easy — and the looser "two whisper tokens map to one script
            # token" case where we collapse them onto the same script tok.
            w_n = i2 - i1
            s_n = j2 - j1
            if w_n == 0:
                continue
            for k in range(w_n):
                # Compute proportional script range for this whisper token.
                s_start = j1 + (k * s_n) // w_n
                s_end = j1 + ((k + 1) * s_n) // w_n
                if s_end <= s_start:
                    s_end = s_start + 1 if s_start < j2 else s_start
                mapping[i1 + k] = (s_start, min(s_end, j2))
        elif tag == "delete":
            # Whisper invented tokens that aren't in the script — leave
            # them unmapped; they'll be dropped from output.
            pass
        elif tag == "insert":
            # Script has tokens whisper missed.  We'll redistribute these
            # at output time by extending each cue's span to cover them.
            pass
    return mapping


def correct_cues(cues, script_toks, mapping):
    """For each whisper cue, replace its text with the matching script span.
    Whisper tokens are addressed by their global stream index — we
    re-derive each cue's [w_start, w_end) range as we walk."""
    out_lines = []
    w_pos = 0
    for c in cues:
        n = len(c["tokens"])
        w_start, w_end = w_pos, w_pos + n
        w_pos = w_end

        # Collect script indices for this cue's whisper span.
        script_indices = []
        for w in range(w_start, w_end):
            s_lo, s_hi = mapping[w]
            if s_lo is None:
                continue
            for s in range(s_lo, s_hi):
                if s not in script_indices:
                    script_indices.append(s)

        # If we picked up nothing, fall back to whisper text.
        if not script_indices:
            corrected_text = c["text"]
        else:
            s_min, s_max = min(script_indices), max(script_indices) + 1
            corrected_text = " ".join(script_toks[s_min:s_max])

        out_lines.append(f"{c['index']}")
        out_lines.append(f"{c['start']} --> {c['end']}")
        out_lines.append(corrected_text)
        out_lines.append("")
    return "\n".join(out_lines).rstrip() + "\n"


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--script", required=True, help="Path to source narration script (.txt)")
    p.add_argument("--srt-in", required=True, help="Path to whisper-generated SRT")
    p.add_argument("--srt-out", required=True, help="Path to write corrected SRT")
    p.add_argument("--verbose", action="store_true", help="Print per-cue diff to stderr")
    args = p.parse_args()

    with open(args.script, encoding="utf-8") as f:
        script_text = f.read()
    with open(args.srt_in, encoding="utf-8") as f:
        srt_text = f.read()

    script_toks = tokenize(script_text)
    cues = parse_srt(srt_text)
    whisper_toks = [t for c in cues for t in c["tokens"]]

    mapping = align_token_streams(script_toks, whisper_toks)
    corrected = correct_cues(cues, script_toks, mapping)

    with open(args.srt_out, "w", encoding="utf-8") as f:
        f.write(corrected)

    if args.verbose:
        old_cues = parse_srt(srt_text)
        new_cues = parse_srt(corrected)
        changed = 0
        for o, n in zip(old_cues, new_cues):
            if o["text"] != n["text"]:
                changed += 1
                print(f"  cue {o['index']}: {o['text']!r} → {n['text']!r}", file=sys.stderr)
        print(f"corrected {changed}/{len(old_cues)} cues", file=sys.stderr)

    return 0


if __name__ == "__main__":
    sys.exit(main())
