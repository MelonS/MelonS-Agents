"""Generate procedural SFX for Day 6.  Standalone Python (stdlib only).

Produces:
  chop.wav   — short noise burst with envelope (axe hit)
  select.wav — short click (UI confirm)
"""
import math
import struct
import wave
from pathlib import Path

SR = 22050

def write_wav(path, samples):
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        # 16-bit signed PCM
        data = b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32767)) for s in samples)
        w.writeframes(data)


def chop_sound():
    # ~0.18s burst: wood crack ish — pink-ish noise + low thump
    import random
    random.seed(7)
    n = int(SR * 0.18)
    out = []
    # low thump (50-80 Hz pulse)
    for i in range(n):
        t = i / SR
        env = math.exp(-t * 18)  # fast decay
        thump = math.sin(2 * math.pi * 70 * t) * env * 0.55
        # snapping noise
        noise = (random.random() * 2 - 1) * env * 0.4
        # higher click at start
        click = math.sin(2 * math.pi * 600 * t) * math.exp(-t * 40) * 0.3
        out.append(thump + noise + click)
    return out


def select_sound():
    # ~0.06s: sine pip
    n = int(SR * 0.06)
    out = []
    for i in range(n):
        t = i / SR
        env = math.exp(-t * 30) * (1 - math.exp(-t * 200))  # attack-decay
        # rising tone 700->1100
        f = 700 + (1100 - 700) * (t / 0.06)
        s = math.sin(2 * math.pi * f * t) * env * 0.6
        out.append(s)
    return out


if __name__ == "__main__":
    out_dir = Path(__file__).parent
    write_wav(out_dir / "chop.wav", chop_sound())
    write_wav(out_dir / "select.wav", select_sound())
    print(f"Wrote chop.wav + select.wav to {out_dir}")
