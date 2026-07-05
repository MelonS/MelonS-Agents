#!/usr/bin/env python3
# typecast-tts.py — Typecast (Neosapience) TTS backend for the shorts pipeline.
#
# Synthesizes Korean narration with PER-SENTENCE EMOTION, chosen to fit the
# storyboard's mood.  Typecast applies one emotion per request, so a "voice
# plan" lists each sentence with its own emotion; this tool synthesizes each,
# stitches them with a small gap, and emits:
#   - <out>.wav   44100 Hz stereo (pipeline-native)
#   - <out>.srt   sentence-level captions with exact timings (zero ASR drift)
#
# The plan IS the creative decision (voice + emotion arc).  It is authored per
# short from the master note — see docs/typecast-tts-notes.md for the voice
# roster and the genre→voice/emotion palette.
#
# Usage:
#   python scripts/typecast-tts.py --plan plan.json --out narration.wav
#   python scripts/typecast-tts.py --voice tc_xxx --emotion whisper \
#          --text-file line.txt --out out.wav          # single-emotion shortcut
#
# plan.json:
#   {
#     "voice_id": "tc_694395d43f2c8d9d43e9a897",
#     "gap": 0.25,                       # seconds of silence between sentences
#     "segments": [
#       {"text": "그날 밤, 무언가가 나를 보고 있었다.", "emotion": "tonedown"},
#       {"text": "나는 눈을 감았다.",                   "emotion": "whisper"},
#       {"text": "그것이, 내 이름을 불렀다!",           "emotion": "toneup"}
#     ]
#   }
#
# Emotions (ssfm-v30): normal, happy, sad, angry, whisper, toneup, tonedown.
#
# API key resolution (never printed/committed):
#   $TYPECAST_API_KEY  →  $TYPECAST_KEY_FILE  →  /g/config/typecast/api.key
#   →  ~/.config/typecast/api.key

import argparse, json, os, subprocess, sys, tempfile, urllib.request, urllib.error

API_URL = "https://api.typecast.ai/v1/text-to-speech"
MODEL = "ssfm-v30"
VALID_EMOTIONS = {"normal", "happy", "sad", "angry", "whisper", "toneup", "tonedown"}
FFMPEG = os.environ.get("FFMPEG_BIN", "ffmpeg")
FFPROBE = os.environ.get("FFPROBE_BIN", "ffprobe")


def load_key():
    k = os.environ.get("TYPECAST_API_KEY")
    if k:
        return k.strip()
    candidates = [os.environ.get("TYPECAST_KEY_FILE"),
                  "/g/config/typecast/api.key",
                  "G:/config/typecast/api.key",  # native Windows python can't read POSIX /g/
                  os.path.expanduser("~/.config/typecast/api.key")]
    for p in candidates:
        if p and os.path.isfile(p):
            with open(p, encoding="utf-8") as f:
                return f.read().strip()
    sys.exit("ERROR: no Typecast API key ($TYPECAST_API_KEY or /g/config/typecast/api.key)")


def synth(key, voice_id, text, emotion, out_wav):
    if emotion not in VALID_EMOTIONS:
        sys.exit(f"ERROR: invalid emotion '{emotion}' (valid: {sorted(VALID_EMOTIONS)})")
    body = json.dumps({"voice_id": voice_id, "text": text,
                       "model": MODEL, "emotion": emotion}).encode("utf-8")
    req = urllib.request.Request(API_URL, data=body, method="POST",
        headers={"X-API-KEY": key, "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            data = r.read()
    except urllib.error.HTTPError as e:
        sys.exit(f"ERROR: Typecast HTTP {e.code}: {e.read()[:200]!r}")
    with open(out_wav, "wb") as f:
        f.write(data)


def dur(path):
    out = subprocess.run([FFPROBE, "-v", "error", "-show_entries",
        "format=duration", "-of", "default=nk=1:nw=1", path],
        capture_output=True, text=True)
    return float(out.stdout.strip())


def ts(sec):
    h = int(sec // 3600); m = int((sec % 3600) // 60)
    s = int(sec % 60); ms = int(round((sec - int(sec)) * 1000))
    if ms == 1000:
        s += 1; ms = 0
    return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan")
    ap.add_argument("--voice")
    ap.add_argument("--emotion", default="normal")
    ap.add_argument("--text-file")
    ap.add_argument("--out", required=True)
    ap.add_argument("--gap", type=float, default=0.25)
    args = ap.parse_args()

    key = load_key()

    if args.plan:
        with open(args.plan, encoding="utf-8") as f:
            plan = json.load(f)
        voice_id = plan["voice_id"]
        gap = float(plan.get("gap", args.gap))
        segments = plan["segments"]
    else:
        if not (args.voice and args.text_file):
            sys.exit("ERROR: provide --plan, or --voice + --text-file")
        with open(args.text_file, encoding="utf-8") as f:
            text = f.read().strip()
        voice_id = args.voice
        gap = args.gap
        segments = [{"text": text, "emotion": args.emotion}]

    out_wav = args.out
    out_srt = os.path.splitext(out_wav)[0] + ".srt"
    tmp = tempfile.mkdtemp(prefix="tcvoice_")

    # 1) synth each sentence, measure duration
    seg_files, durs = [], []
    for i, seg in enumerate(segments):
        w = os.path.join(tmp, f"seg{i:03d}.wav")
        synth(key, voice_id, seg["text"], seg.get("emotion", "normal"), w)
        d = dur(w)
        seg_files.append(w); durs.append(d)
        print(f"  seg{i} [{seg.get('emotion','normal')}] {d:.2f}s  {seg['text'][:30]}")

    # 2) silence gap clip
    sil = os.path.join(tmp, "sil.wav")
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-f", "lavfi",
        "-t", str(gap), "-i", "anullsrc=r=44100:cl=mono",
        "-c:a", "pcm_s16le", sil], check=True)

    # 3) concat list (seg, sil, seg, sil, ...), render 44100 stereo
    listf = os.path.join(tmp, "list.txt")
    with open(listf, "w", encoding="utf-8") as f:
        for j, w in enumerate(seg_files):
            if j > 0:
                f.write(f"file '{sil}'\n")
            f.write(f"file '{w}'\n")
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-f", "concat",
        "-safe", "0", "-i", listf, "-ar", "44100", "-ac", "2",
        out_wav], check=True)

    # 4) sentence-level SRT (exact timings from durations + gaps)
    cues, t = [], 0.0
    for i, (seg, d) in enumerate(zip(segments, durs)):
        start, end = t, t + d
        cues.append(f"{i+1}\n{ts(start)} --> {ts(end)}\n{seg['text']}\n")
        t = end + gap
    with open(out_srt, "w", encoding="utf-8") as f:
        f.write("\n".join(cues))

    total = dur(out_wav)
    print(f"OK: {out_wav} ({total:.1f}s, {len(segments)} sentences)  +  {out_srt}")


if __name__ == "__main__":
    main()
