"""gen_sfx.py — procedural SFX for Suika via Python stdlib wave module.

No external audio libs.  Generates 3 short PCM 16-bit mono WAVs at
44.1 kHz: drop.wav (tap), merge.wav (rising chime), gameover.wav
(falling tone).
"""
from __future__ import annotations

import math
import struct
import wave
from pathlib import Path

SR = 44100


def write_wav(path: Path, samples):
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        # Clamp + convert
        out = bytearray()
        for s in samples:
            v = int(max(-1.0, min(1.0, s)) * 32760)
            out += struct.pack("<h", v)
        w.writeframes(bytes(out))


def env_adsr(n: int, a: int, d: int, s_level: float, r: int):
    """Simple ADSR envelope in samples."""
    for i in range(n):
        if i < a:
            yield i / max(a, 1)
        elif i < a + d:
            t = (i - a) / max(d, 1)
            yield 1.0 + t * (s_level - 1.0)
        elif i < n - r:
            yield s_level
        else:
            t = (i - (n - r)) / max(r, 1)
            yield s_level * (1.0 - t)


def gen_drop():
    """Short percussive tap."""
    n = int(SR * 0.08)
    out = []
    for i, e in enumerate(env_adsr(n, int(SR * 0.005), int(SR * 0.02), 0.3, int(SR * 0.05))):
        freq = 220.0 * math.exp(-i / (SR * 0.05))
        s = math.sin(2 * math.pi * freq * i / SR) * 0.5
        # Add a click
        if i < 80:
            s += (0.3 if i % 2 == 0 else -0.3)
        out.append(s * e * 0.7)
    return out


def gen_merge():
    """Rising chime — two frequencies, second up an octave."""
    n = int(SR * 0.35)
    out = []
    for i, e in enumerate(env_adsr(n, int(SR * 0.01), int(SR * 0.05), 0.6, int(SR * 0.25))):
        t = i / SR
        freq = 440.0 * (1.0 + 1.5 * (i / n))  # 440 -> 1100 Hz
        s1 = math.sin(2 * math.pi * freq * t) * 0.4
        s2 = math.sin(2 * math.pi * freq * 1.5 * t) * 0.2
        out.append((s1 + s2) * e)
    return out


def gen_gameover():
    """Descending tone."""
    n = int(SR * 0.9)
    out = []
    for i, e in enumerate(env_adsr(n, int(SR * 0.02), int(SR * 0.1), 0.5, int(SR * 0.6))):
        t = i / SR
        # 440 -> 110 Hz over 0.9s
        freq = 440.0 * math.exp(-t * 1.5)
        s = math.sin(2 * math.pi * freq * t) * 0.6
        out.append(s * e)
    return out


def main():
    out_dir = Path(__file__).resolve().parent.parent / "unity-project" / "Assets" / "Audio"
    out_dir.mkdir(parents=True, exist_ok=True)
    write_wav(out_dir / "drop.wav", gen_drop());     print(f"wrote {out_dir / 'drop.wav'}")
    write_wav(out_dir / "merge.wav", gen_merge());   print(f"wrote {out_dir / 'merge.wav'}")
    write_wav(out_dir / "gameover.wav", gen_gameover()); print(f"wrote {out_dir / 'gameover.wav'}")


if __name__ == "__main__":
    main()
