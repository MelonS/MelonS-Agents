"""Kokoro-TTS wrapper — synthesize a text file into a WAV.

Backend: kokoro-onnx (Apache 2.0 license, commercial use allowed).
Model files expected at $HOME/.local/share/kokoro/:
  - kokoro-v1.0.onnx          (~325 MB, model weights)
  - voices-v1.0.bin           (~26 MB, speaker embeddings)

If missing, run:
  scripts/setup-kokoro.sh   # downloads both files

Usage:
  .venv/bin/python scripts/tts-kokoro.py \
      --text-file <input.txt> \
      --output <output.wav> \
      [--voice <voice_name>]   # default: af_heart (warm female, English)

Built-in English voices that work well for documentary tone:
  af_heart    — warm female, conversational
  af_bella    — clear female, news anchor
  am_michael  — calm male, documentary
  am_adam     — confident male, narration
  bf_emma     — British female, calm
  bm_george   — British male, authoritative
"""

import argparse
import pathlib
import sys


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--text-file", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--voice", default="am_michael")
    parser.add_argument("--lang", default="en-us")
    parser.add_argument("--speed", type=float, default=1.0)
    args = parser.parse_args()

    text_path = pathlib.Path(args.text_file)
    if not text_path.exists():
        print(f"❌ text file not found: {text_path}", file=sys.stderr)
        return 1
    text = text_path.read_text(encoding="utf-8").strip()
    if not text:
        print("❌ text file is empty", file=sys.stderr)
        return 1

    try:
        from kokoro_onnx import Kokoro
    except ImportError as e:
        print(f"❌ kokoro-onnx not installed: {e}", file=sys.stderr)
        return 1

    import soundfile as sf

    home = pathlib.Path.home()
    model_path = home / ".local/share/kokoro/kokoro-v1.0.onnx"
    voices_path = home / ".local/share/kokoro/voices-v1.0.bin"

    if not model_path.exists() or not voices_path.exists():
        print(
            f"❌ kokoro model files missing.  Expected:\n"
            f"     {model_path}\n"
            f"     {voices_path}\n"
            f"  Run:  scripts/setup-kokoro.sh",
            file=sys.stderr,
        )
        return 1

    print(f"  loading kokoro model...", file=sys.stderr)
    kokoro = Kokoro(str(model_path), str(voices_path))

    print(f"  synthesizing ({len(text)} chars, voice={args.voice}, speed={args.speed}x)...", file=sys.stderr)
    samples, sample_rate = kokoro.create(
        text=text, voice=args.voice, speed=args.speed, lang=args.lang
    )

    out = pathlib.Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    sf.write(out, samples, sample_rate)

    duration = len(samples) / sample_rate
    print(f"✅ wrote {out} ({duration:.1f}s @ {sample_rate} Hz)", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
