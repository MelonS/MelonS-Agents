#!/usr/bin/env python3
# elevenlabs-tts.py — ElevenLabs v3 (emotion-tagged) TTS backend for the pipeline.
#
# ElevenLabs v3 carries the emotion arc via INLINE AUDIO TAGS ([whispers],
# [shouts], [sighs], [sad], [excited] ...) inside one text string — so the
# whole narration is a single call (no per-sentence splitting).  The emotion
# tagging IS the creative decision, authored per short from the master-note
# mood — see docs/elevenlabs-tts-notes.md.
#
# Uses the /with-timestamps endpoint so we get character-level alignment, then:
#   - decodes audio_base64 → <out>.wav  (44100 Hz stereo, pipeline-native)
#   - builds <out>.srt  (sentence-level, exact timings, TAGS STRIPPED from the
#     caption text — viewers never see "[whispers]")
#
# Usage:
#   python scripts/elevenlabs-tts.py --plan plan.json --out narration.wav
#   python scripts/elevenlabs-tts.py --voice tc_xxx --text-file line.txt --out out.wav
#
# plan.json:
#   {
#     "engine": "elevenlabs",
#     "voice_id": "JBFqnCBsd6RMkjVDRZzb",     # George — Warm Storyteller
#     "model_id": "eleven_v3",
#     "text": "천 년을 기다린 뱀... [whispers] 조용히 눈을 감으시겠습니까. [shouts] 뱀이다!",
#     "voice_settings": {"stability": 0.4, "similarity_boost": 0.8, "style": 0.5}
#   }
#
# Key: $ELEVENLABS_API_KEY → $ELEVENLABS_KEY_FILE → /g/config/elevenlabs/api.key
#      → G:/config/elevenlabs/api.key → ~/.config/elevenlabs/api.key

import argparse, base64, json, os, re, subprocess, sys, tempfile, urllib.request, urllib.error

API_BASE = "https://api.elevenlabs.io/v1/text-to-speech"
DEFAULT_MODEL = "eleven_v3"
FFMPEG = os.environ.get("FFMPEG_BIN", "ffmpeg")
TAG_RE = re.compile(r"\[[^\]]*\]")          # [whispers], [shouts], ...


def load_key():
    k = os.environ.get("ELEVENLABS_API_KEY")
    if k:
        return k.strip()
    for p in [os.environ.get("ELEVENLABS_KEY_FILE"),
              "/g/config/elevenlabs/api.key",
              "G:/config/elevenlabs/api.key",   # native Windows python can't read POSIX /g/
              os.path.expanduser("~/.config/elevenlabs/api.key")]:
        if p and os.path.isfile(p):
            with open(p, encoding="utf-8") as f:
                return f.read().strip()
    sys.exit("ERROR: no ElevenLabs API key ($ELEVENLABS_API_KEY or /g/config/elevenlabs/api.key)")


def ts(sec):
    h = int(sec // 3600); m = int((sec % 3600) // 60)
    s = int(sec % 60); ms = int(round((sec - int(sec)) * 1000))
    if ms == 1000:
        s += 1; ms = 0
    return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"


def tag_char_indices(chars):
    """Indices of characters that fall inside a [...] tag (brackets included)."""
    full = "".join(chars)
    bad = set()
    for m in TAG_RE.finditer(full):
        bad.update(range(m.start(), m.end()))
    return bad


def build_srt(alignment, out_srt):
    chars = alignment["characters"]
    starts = alignment["character_start_times_seconds"]
    ends = alignment["character_end_times_seconds"]
    bad = tag_char_indices(chars)

    cues, cur, cur_start = [], [], None
    for i, ch in enumerate(chars):
        if i in bad:
            continue
        if ch in "\n\r":
            ch = " "
        if not cur and ch.isspace():
            continue                       # skip leading whitespace of a cue
        if cur_start is None:
            cur_start = starts[i]
        cur.append((ch, ends[i]))
        if ch in ".!?…":                   # sentence boundary → close cue
            text = "".join(c for c, _ in cur).strip()
            text = TAG_RE.sub("", text).strip()
            if text:
                cues.append((cur_start, cur[-1][1], text))
            cur, cur_start = [], None
    if cur:                                # trailing fragment
        text = TAG_RE.sub("", "".join(c for c, _ in cur)).strip()
        if text:
            cues.append((cur_start, cur[-1][1], text))

    with open(out_srt, "w", encoding="utf-8") as f:
        for n, (a, b, text) in enumerate(cues, 1):
            f.write(f"{n}\n{ts(a)} --> {ts(b)}\n{text}\n\n")
    return len(cues)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan")
    ap.add_argument("--voice")
    ap.add_argument("--text-file")
    ap.add_argument("--model", default=DEFAULT_MODEL)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    key = load_key()

    if args.plan:
        with open(args.plan, encoding="utf-8") as f:
            plan = json.load(f)
        voice_id = plan["voice_id"]
        model = plan.get("model_id", args.model)
        text = plan["text"]
        vsettings = plan.get("voice_settings")
    else:
        if not (args.voice and args.text_file):
            sys.exit("ERROR: provide --plan, or --voice + --text-file")
        with open(args.text_file, encoding="utf-8") as f:
            text = f.read().strip()
        voice_id = args.voice
        model = args.model
        vsettings = None

    body = {"text": text, "model_id": model}
    if vsettings:
        body["voice_settings"] = vsettings

    url = f"{API_BASE}/{voice_id}/with-timestamps?output_format=mp3_44100_128"
    req = urllib.request.Request(url, data=json.dumps(body, ensure_ascii=False).encode("utf-8"),
        method="POST", headers={"xi-api-key": key, "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=180) as r:
            resp = json.load(r)
    except urllib.error.HTTPError as e:
        sys.exit(f"ERROR: ElevenLabs HTTP {e.code}: {e.read()[:200].decode('utf-8','replace')}")

    out_wav = args.out
    out_srt = os.path.splitext(out_wav)[0] + ".srt"
    tmp = tempfile.mkdtemp(prefix="eltts_")

    # audio: base64 → mp3 → 44100 stereo wav
    mp3 = os.path.join(tmp, "a.mp3")
    with open(mp3, "wb") as f:
        f.write(base64.b64decode(resp["audio_base64"]))
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-i", mp3,
        "-ar", "44100", "-ac", "2", out_wav], check=True)

    # captions from alignment (tags stripped)
    alignment = resp.get("alignment") or resp.get("normalized_alignment")
    n = build_srt(alignment, out_srt) if alignment else 0

    print(f"OK: {out_wav}  +  {out_srt} ({n} caption lines)")


if __name__ == "__main__":
    main()
