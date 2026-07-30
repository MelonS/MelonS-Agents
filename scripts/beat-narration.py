#!/usr/bin/env python3
"""beat-narration.py — beat-aligned narration + word-timed captions.

Each beat in the spec carries its own `vo`. Instead of generating one long
narration and hoping it lines up with the visuals, this synthesizes ONE clip PER
BEAT and lays each at its beat's exact start time. Alignment is arithmetic, not
ASR — nothing to drift.

A beat's `vo` may also be a LIST of parts, each with its own voice:

    "vo": [
      {"text": "Her bandmate shouts a cheer for her hometown."},
      {"text": "거제~ 야홍~", "voice": "B8rl62CpT9zOQ7RC3Mdl"}
    ]

which is how a foreign-language catchphrase gets said by a native voice instead
of being mangled by the narrator's TTS.

With `--engine=elevenlabs` the /with-timestamps endpoint is used, so we get
character-level alignment for free and emit REAL word timings to
`<out>.captions.json` — no ASR, no guessing.

Usage:
  python scripts/beat-narration.py <spec.json> <out.wav> [--fps 30]
      [--engine=elevenlabs] [--voice=<id|edge-voice>]
"""

import base64
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

FPS_DEFAULT = 30
VOICE_DEFAULT = "ko-KR-SunHiNeural"
RATE_STEPS = ["+0%", "+8%", "+16%", "+25%", "+35%"]
EL_SPEEDS = [1.0, 1.06, 1.12, 1.2]
EL_API = "https://api.elevenlabs.io/v1/text-to-speech"
EL_MODELS = ["eleven_v3", "eleven_multilingual_v2"]
HEAD_PAD_S = 0.25
TAIL_SLACK_S = 0.15
PART_GAP_S = 0.12


def ffbin(name):
    import os
    return os.environ.get(f"{name.upper()}_BIN") or shutil.which(name) or name


def duration_s(path):
    out = subprocess.run(
        [ffbin("ffprobe"), "-v", "error", "-show_entries", "format=duration",
         "-of", "default=nw=1:nk=1", str(path)],
        capture_output=True, text=True)
    try:
        return float(out.stdout.strip())
    except ValueError:
        return 0.0


def el_key():
    import os
    k = os.environ.get("ELEVENLABS_API_KEY")
    if k:
        return k.strip()
    for p in (os.environ.get("ELEVENLABS_KEY_FILE"), "/g/config/elevenlabs/api.key",
              "G:/config/elevenlabs/api.key",
              os.path.expanduser("~/.config/elevenlabs/api.key")):
        if p and Path(p).is_file():
            return Path(p).read_text(encoding="utf-8").strip()
    sys.exit("[beat-narration] no ElevenLabs API key")


_el_model = None


def synth_elevenlabs(text, voice_id, speed, out_path):
    """Returns word timings [(start, end, word)] relative to the clip."""
    global _el_model
    import urllib.error
    import urllib.request
    body = {"text": text,
            "voice_settings": {"stability": 0.45, "similarity_boost": 0.8,
                               "style": 0.3, "speed": round(speed, 2)}}
    last = None
    for model in ([_el_model] if _el_model else EL_MODELS):
        body["model_id"] = model
        req = urllib.request.Request(
            f"{EL_API}/{voice_id}/with-timestamps?output_format=mp3_44100_128",
            data=json.dumps(body).encode("utf-8"),
            headers={"xi-api-key": el_key(), "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=180) as r:
                payload = json.loads(r.read())
            Path(out_path).write_bytes(base64.b64decode(payload["audio_base64"]))
            _el_model = model
            return words_from_alignment(payload.get("alignment") or {})
        except urllib.error.HTTPError as e:
            last = f"{e.code} {e.read()[:300]!r}"
        except Exception as e:  # noqa: BLE001
            last = str(e)
    sys.exit(f"[beat-narration] ElevenLabs failed for {text[:30]!r}: {last}")


def words_from_alignment(al):
    chars = al.get("characters") or []
    starts = al.get("character_start_times_seconds") or []
    ends = al.get("character_end_times_seconds") or []
    words, cur, w0 = [], "", None
    for ch, s, e in zip(chars, starts, ends):
        if ch.isspace():
            if cur:
                words.append((w0, e, cur))
                cur, w0 = "", None
            continue
        if not cur:
            w0 = s
        cur += ch
        last_e = e
    if cur:
        words.append((w0, last_e, cur))
    return words


def synth_edge(text, voice, rate, out_path):
    cmd = [sys.executable, "-m", "edge_tts", "--voice", voice,
           "--text", text, "--write-media", str(out_path)]
    if rate != "+0%":
        cmd += ["--rate", rate]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0 or not Path(out_path).exists():
        sys.exit(f"[beat-narration] edge-tts failed for {text[:24]!r}\n{r.stderr[-500:]}")
    return []


def as_parts(vo):
    if isinstance(vo, list):
        return [p for p in vo if str(p.get("text", "")).strip() or p.get("audio")]
    vo = (vo or "").strip()
    return [{"text": vo}] if vo else []


def group_captions(words, max_chars=30):
    """Group word timings into short readable phrases — a caption is a phrase,
    never a single word flashing, never a paragraph."""
    out, buf, t0, prev_e = [], [], None, 0.0

    def flush():
        nonlocal buf, t0
        if buf:
            out.append({"start": round(t0, 3), "end": round(prev_e, 3),
                        "text": " ".join(buf)})
        buf, t0 = [], None

    for s, e, w, solo in words:
        if solo:
            flush()
            out.append({"start": round(s, 3), "end": round(e, 3), "text": w})
            prev_e = e
            continue
        if t0 is None:
            t0 = s
        if buf and len(" ".join(buf + [w])) > max_chars:
            flush()
            buf, t0 = [w], s
        else:
            buf.append(w)
        prev_e = e
    flush()
    return out


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    flags = {a.split("=", 1)[0]: (a.split("=", 1)[1] if "=" in a else True)
             for a in sys.argv[1:] if a.startswith("--")}
    if len(args) < 2:
        sys.exit(__doc__)
    spec_path, out_path = Path(args[0]), Path(args[1])
    fps = int(flags.get("--fps", FPS_DEFAULT))
    voice = flags.get("--voice", VOICE_DEFAULT)
    engine = flags.get("--engine", "edge")
    steps = EL_SPEEDS if engine == "elevenlabs" else RATE_STEPS

    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    beats = spec["beats"]
    tmp = Path(tempfile.mkdtemp(prefix="beatvo-"))

    clips, captions, start_f, overruns, n = [], [], 0, [], 0
    for i, b in enumerate(beats):
        beat_s = b["frames"] / fps
        budget = beat_s - HEAD_PAD_S + TAIL_SLACK_S
        cursor = start_f / fps + HEAD_PAD_S
        parts = as_parts(b.get("vo"))
        spoken = 0.0
        for part in parts:
            # A part may be a REAL audio file instead of TTS — that is how the
            # subject's own voice (a catchphrase, a quote) gets into the mix
            # without a synthetic imitation of it.
            if part.get("audio"):
                clip = Path(part["audio"])
                if not clip.is_file():
                    sys.exit(f"[beat-narration] audio part not found: {clip}")
                dur = duration_s(clip)
                if part.get("caption"):
                    captions.append((cursor, cursor + dur, part["caption"], True))
                clips.append((clip, cursor))
                cursor += dur + PART_GAP_S
                spoken += dur + PART_GAP_S
                continue
            pv = part.get("voice") or voice
            clip = tmp / f"{n:03d}.mp3"
            n += 1
            words, chosen, dur = [], None, 0.0
            for rate in steps:
                if engine == "elevenlabs":
                    words = synth_elevenlabs(part["text"], pv, rate, clip)
                else:
                    words = synth_edge(part["text"], pv, rate, clip)
                dur, chosen = duration_s(clip), rate
                if spoken + dur <= budget or len(parts) > 1:
                    break
            for (ws, we, w) in words:
                captions.append((cursor + ws, cursor + we, w, False))
            clips.append((clip, cursor))
            cursor += dur + PART_GAP_S
            spoken += dur + PART_GAP_S
        spoken = max(spoken - PART_GAP_S, 0.0)
        flag = "" if spoken <= budget else "  ← OVERRUN"
        if spoken > budget:
            overruns.append((b["scene"], round(spoken, 2), round(budget, 2)))
        print(f"  · {b['scene']:9s} beat {beat_s:5.2f}s  vo {spoken:5.2f}s  "
              f"parts {len(parts)}{flag}")
        start_f += b["frames"]

    total_s = start_f / fps
    inputs, filters, labels = [], [], []
    for k, (clip, start_s) in enumerate(clips):
        inputs += ["-i", str(clip)]
        ms = int(start_s * 1000)
        filters.append(f"[{k}:a]aresample=48000,adelay={ms}|{ms}[a{k}]")
        labels.append(f"[a{k}]")
    filter_complex = ";".join(filters) + ";" + "".join(labels) + \
        f"amix=inputs={len(clips)}:normalize=0:dropout_transition=0,"\
        f"alimiter=limit=0.95,apad,atrim=0:{total_s:.3f}[out]"

    out_path.parent.mkdir(parents=True, exist_ok=True)
    r = subprocess.run([ffbin("ffmpeg"), "-y", "-loglevel", "error", *inputs,
                        "-filter_complex", filter_complex, "-map", "[out]",
                        "-ac", "1", "-ar", "48000", str(out_path)],
                       capture_output=True, text=True)
    if r.returncode != 0:
        sys.exit(f"[beat-narration] mux failed\n{r.stderr[-800:]}")

    cap_path = out_path.with_suffix(".captions.json")
    cap_path.write_text(json.dumps(group_captions(captions), ensure_ascii=False,
                                   indent=1), encoding="utf-8")

    print(f"[beat-narration] {len(clips)} clips → {out_path} "
          f"({duration_s(out_path):.2f}s) · captions → {cap_path.name}")
    if overruns:
        print("[beat-narration] WARNING - lines that overrun their beat:")
        for scene, d, b in overruns:
            print(f"    {scene}: {d}s vo in a {b}s slot")
    shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    main()
