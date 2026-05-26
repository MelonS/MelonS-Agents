"""sfx_proc — procedural SFX generation (stdlib `wave` only).

Sound Designer agent's fast-path: when the asset is a generic
game-feel SFX (drop / click / merge / gameover / win / hit) the
Sound Designer generates locally in <100ms.  No external deps.

Sound kinds (Day-1 catalog — extend as new prototypes need new feels):
  drop     : short percussive tap, 80ms
  click    : crisp UI click, 40ms
  merge    : rising chime, 350ms (2-octave sweep)
  win      : rising arpeggio, 600ms
  gameover : descending tone, 900ms
  hit      : impact thud, 120ms
  pickup   : short upward blip, 100ms

Future:
  - operator-tunable interval/scale via CLI
  - LLM-generated parameter packs (kind=hit-soft / hit-metal / hit-wood)
"""
from __future__ import annotations

import math
import struct
import wave
from pathlib import Path

SR = 44100  # 44.1 kHz mono


def _write_wav(path: Path, samples):
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        buf = bytearray()
        for s in samples:
            v = int(max(-1.0, min(1.0, s)) * 32760)
            buf += struct.pack("<h", v)
        w.writeframes(bytes(buf))


def _env(n: int, a: int, d: int, s_level: float, r: int):
    """ADSR envelope (Attack, Decay, Sustain-level, Release) in samples."""
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


# ---- Generators (one per kind) -------------------------------------

def gen_drop():
    n = int(SR * 0.08)
    out = []
    for i, e in enumerate(_env(n, int(SR * 0.005), int(SR * 0.02), 0.3, int(SR * 0.05))):
        freq = 220.0 * math.exp(-i / (SR * 0.05))
        s = math.sin(2 * math.pi * freq * i / SR) * 0.5
        if i < 80:
            s += (0.3 if i % 2 == 0 else -0.3)
        out.append(s * e * 0.7)
    return out


def gen_click():
    n = int(SR * 0.04)
    out = []
    for i, e in enumerate(_env(n, int(SR * 0.001), int(SR * 0.01), 0.2, int(SR * 0.025))):
        # White noise burst + 1500Hz tone
        import random
        noise = (random.random() - 0.5) * 0.5
        tone = math.sin(2 * math.pi * 1500 * i / SR) * 0.3
        out.append((noise + tone) * e * 0.8)
    return out


def gen_merge():
    n = int(SR * 0.35)
    out = []
    for i, e in enumerate(_env(n, int(SR * 0.01), int(SR * 0.05), 0.6, int(SR * 0.25))):
        t = i / SR
        freq = 440.0 * (1.0 + 1.5 * (i / n))  # 440 -> 1100 Hz sweep
        s1 = math.sin(2 * math.pi * freq * t) * 0.4
        s2 = math.sin(2 * math.pi * freq * 1.5 * t) * 0.2
        out.append((s1 + s2) * e)
    return out


def gen_win():
    n = int(SR * 0.6)
    out = []
    # 3-note rising arpeggio: 440, 554, 659 (A, C#, E)
    notes = [440.0, 554.37, 659.25]
    note_len = n // len(notes)
    for ni, freq in enumerate(notes):
        for i, e in enumerate(_env(note_len, int(SR * 0.005), int(SR * 0.05), 0.6, int(SR * 0.1))):
            t = i / SR
            s = math.sin(2 * math.pi * freq * t) * 0.5
            out.append(s * e)
    return out


def gen_gameover():
    n = int(SR * 0.9)
    out = []
    for i, e in enumerate(_env(n, int(SR * 0.02), int(SR * 0.1), 0.5, int(SR * 0.6))):
        t = i / SR
        freq = 440.0 * math.exp(-t * 1.5)  # 440 -> ~110 Hz descend
        s = math.sin(2 * math.pi * freq * t) * 0.6
        out.append(s * e)
    return out


def gen_hit():
    import random
    n = int(SR * 0.12)
    out = []
    for i, e in enumerate(_env(n, int(SR * 0.002), int(SR * 0.04), 0.4, int(SR * 0.08))):
        noise = (random.random() - 0.5) * 0.7
        body = math.sin(2 * math.pi * (180 - i * 0.5) * i / SR) * 0.3
        out.append((noise * 0.6 + body) * e)
    return out


def gen_pickup():
    n = int(SR * 0.10)
    out = []
    for i, e in enumerate(_env(n, int(SR * 0.005), int(SR * 0.02), 0.5, int(SR * 0.07))):
        t = i / SR
        freq = 660 + 1200 * (i / n)  # quick upward chirp
        s = math.sin(2 * math.pi * freq * t) * 0.45
        out.append(s * e)
    return out


def gen_ambient_pad():
    """30s sustained pad — slow harmonic chord, very low volume.
    Loops seamlessly (ends where it began on a single sine pass).
    Use as game BGM."""
    n = int(SR * 30.0)  # 30 seconds
    out = []
    # Three layered sine voices forming a minor chord (A2, C3, E3).
    voices = [(110.0, 0.18), (130.81, 0.13), (164.81, 0.10)]
    # Slow LFO 0.05 Hz for breathiness
    for i in range(n):
        t = i / SR
        s = 0.0
        for f, amp in voices:
            lfo = 1.0 + 0.05 * math.sin(2 * math.pi * 0.05 * t + f * 0.001)
            s += math.sin(2 * math.pi * f * t * lfo) * amp
        # Long fade-in over first 2s, fade-out last 2s — loop-safe.
        env = 1.0
        if t < 2.0: env = t / 2.0
        elif t > 28.0: env = (30.0 - t) / 2.0
        out.append(s * env * 0.5)
    return out


KIND_REGISTRY = {
    "drop":         gen_drop,
    "click":        gen_click,
    "merge":        gen_merge,
    "win":          gen_win,
    "gameover":     gen_gameover,
    "hit":          gen_hit,
    "pickup":       gen_pickup,
    "ambient_pad":  gen_ambient_pad,
}


def generate(kind: str, out: Path) -> Path:
    if kind not in KIND_REGISTRY:
        raise ValueError(f"unknown sfx kind: {kind!r}. supported: {list(KIND_REGISTRY)}")
    samples = KIND_REGISTRY[kind]()
    _write_wav(out, samples)
    print(f"[sfx_proc] {kind} -> {out}")
    return out
