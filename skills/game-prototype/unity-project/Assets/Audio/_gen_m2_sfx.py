"""Generate M2 procedural SFX for PawnSim — wave W-M2-01 sound coverage.

Produces:
  build.wav   — hammer/clink wall construction finish (wiki #1)
  alert.wav   — alert siren single beep (wiki #2, tier-scaled repeat in AudioBank)
  ambient.wav — looping outdoor wind/birds ambient bed (wiki #4), ~20s

All: 44100Hz, mono, 16-bit PCM. Uses stdlib only.
"""
import math
import random
import struct
import wave
from pathlib import Path

SR = 44100


def write_wav(path, samples, sr=SR):
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        data = b"".join(
            struct.pack("<h", int(max(-32767, min(32767, s * 32767))))
            for s in samples
        )
        w.writeframes(data)


def normalize(samples, peak=0.85):
    mx = max(abs(s) for s in samples) if samples else 1.0
    if mx < 1e-9:
        return samples
    scale = peak / mx
    return [s * scale for s in samples]


# ---------------------------------------------------------------------------
# build.wav — hammer/clink construction finish
# Design: two-part: short metallic clink (tool-on-stone/wood, ~0.05s) then
# a brief woody resonant body (~0.25s).  Total ~0.30s.
# Character: satisfying "task complete" thunk — distinct from axe chop
# (chop is lower, woodier; build has brighter metallic leading edge).
# Throttled 0.25s in AudioBank.PlayBuild().
# ---------------------------------------------------------------------------
def build_sound():
    random.seed(77)
    dur = 0.30
    n = int(SR * dur)
    out = []

    for i in range(n):
        t = i / SR

        # --- Metallic clink transient (0-0.05s)
        clink_env = math.exp(-t * 90)
        clink = (
            math.sin(2 * math.pi * 1400 * t) * 0.45 +
            math.sin(2 * math.pi * 2100 * t) * 0.25 +
            math.sin(2 * math.pi * 980  * t) * 0.15
        ) * clink_env

        # --- Woody body resonance (peaks ~0.04s, decays by 0.25s)
        body_delay = max(0.0, t - 0.02)
        body_env = math.exp(-body_delay * 18)
        body = (
            math.sin(2 * math.pi * 150 * t) * 0.50 +
            math.sin(2 * math.pi * 240 * t) * 0.22 +
            math.sin(2 * math.pi * 90  * t) * 0.18
        ) * body_env * 0.8

        # --- Noise transient texture
        crack_env = math.exp(-t * 70)
        crack = (random.random() * 2 - 1) * crack_env * 0.30

        sample = clink + body + crack
        out.append(sample)

    # Fade last 12ms
    fade_n = int(SR * 0.012)
    for i in range(fade_n):
        out[-(i + 1)] *= i / fade_n

    return normalize(out, peak=0.82)


# ---------------------------------------------------------------------------
# alert.wav — alert siren single beep
# Design: a rising two-tone siren pulse (~0.28s).  AudioBank.PlayAlert(tier)
# fires this clip N times (2/3/4) with pitch offsets via coroutine.
# Character: urgent, electronic — not a shotgun blast, not a phone ring.
# Two tones sweep upward: 600Hz→900Hz (like a classic civil-defense beep).
# ---------------------------------------------------------------------------
def alert_sound():
    random.seed(33)
    dur = 0.28
    n = int(SR * dur)
    out = []

    for i in range(n):
        t = i / SR
        progress = t / dur

        # Overall envelope: quick attack (0.01s), sustain, decay last 0.06s
        if t < 0.01:
            env = t / 0.01
        elif t > dur - 0.06:
            env = (dur - t) / 0.06
        else:
            env = 1.0

        # Sweeping frequency: 600Hz → 920Hz (linear pitch rise)
        f1 = 600 + (920 - 600) * progress
        f2 = f1 * 1.50   # perfect fifth above — siren character

        # Two-tone siren
        siren = (
            math.sin(2 * math.pi * f1 * t) * 0.55 +
            math.sin(2 * math.pi * f2 * t) * 0.35
        ) * env

        # Slight electronic buzz for urgency
        buzz_env = env * 0.10
        buzz = (random.random() * 2 - 1) * buzz_env

        sample = siren + buzz
        out.append(sample)

    # Fade last 10ms
    fade_n = int(SR * 0.010)
    for i in range(fade_n):
        out[-(i + 1)] *= i / fade_n

    return normalize(out, peak=0.88)


# ---------------------------------------------------------------------------
# ambient.wav — looping outdoor wind/birds ambient bed (~20s)
# Design: open outdoor soundscape: low wind drone + occasional bird chirp
# texture + gentle rustling noise. Loops seamlessly (fade in/out at edges).
# Character: calm, pastoral. Clearly distinct from bgm_ambient (which is a
# musical drone chord).  This is more "literal outdoor" — noise-based.
# Volume set at 0.18 in AudioBank — well below BGM 0.25 and SFX 0.7.
# ---------------------------------------------------------------------------
def ambient_sound():
    random.seed(888)
    dur = 20.0
    n = int(SR * dur)
    out = []

    # Pre-generate noise
    noise = [random.random() * 2 - 1 for _ in range(n)]

    # Simple low-pass: running average (approximation)
    lp_noise = [0.0] * n
    alpha = 0.02  # heavy smoothing — low-freq wind feel
    acc = 0.0
    for i in range(n):
        acc = acc * (1 - alpha) + noise[i] * alpha
        lp_noise[i] = acc

    # Bird chirp schedule: sparse random events
    random.seed(999)
    chirp_times = sorted(random.uniform(1.0, dur - 1.0) for _ in range(18))
    chirp_set = set(int(ct * SR) for ct in chirp_times)

    # Pre-build chirp envelope array (vectorised)
    chirp_buf = [0.0] * n
    chirp_dur_samples = int(SR * 0.06)  # 60ms chirp
    for ci in chirp_set:
        f_chirp = random.uniform(2200, 3800)  # bird frequency range
        vol_chirp = random.uniform(0.12, 0.22)
        for k in range(chirp_dur_samples):
            idx = ci + k
            if idx >= n:
                break
            tc = k / SR
            chirp_env = math.sin(math.pi * tc / 0.06)  # half-sine shape
            chirp_buf[idx] += math.sin(2 * math.pi * f_chirp * tc) * chirp_env * vol_chirp

    phase_wind = 0.0
    for i in range(n):
        t = i / SR

        # Loop envelope: 1.5s fade in/out
        if t < 1.5:
            loop_env = t / 1.5
        elif t > dur - 1.5:
            loop_env = (dur - t) / 1.5
        else:
            loop_env = 1.0

        # Wind: slow LFO-modulated noise band
        lfo = 0.75 + 0.25 * math.sin(2 * math.pi * 0.07 * t)
        wind = lp_noise[i] * lfo * 0.55 * loop_env

        # Very low sub-rumble for "outdoor air" body
        phase_wind += 2 * math.pi * 55 * (1 + 0.02 * math.sin(2 * math.pi * 0.04 * t)) / SR
        sub = math.sin(phase_wind) * 0.08 * loop_env

        # Bird chirps
        bird = chirp_buf[i] * loop_env

        sample = wind + sub + bird
        out.append(sample)

    return normalize(out, peak=0.60)  # kept low — AudioBank vol=0.18 further attenuates


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    out_dir = Path(__file__).parent

    sounds = {
        "build.wav":   build_sound,
        "alert.wav":   alert_sound,
        "ambient.wav": ambient_sound,
    }

    print("Generating M2 SFX (build / alert / ambient)...")
    for fname, gen_fn in sounds.items():
        print(f"  {fname}...", end=" ", flush=True)
        samples = gen_fn()
        write_wav(out_dir / fname, samples)
        with wave.open(str(out_dir / fname), "rb") as wf:
            dur = wf.getnframes() / wf.getframerate()
        peak = max(abs(s) for s in samples)
        print(f"OK  {SR}Hz mono 16bit {dur:.3f}s peak={peak:.3f}")

    print()
    print("Wiring notes for QA:")
    print("  build.wav   — AudioBank.sfxBuild (public AudioClip) needs scene-side assignment")
    print("                BlueprintEntity.Complete() calls AudioBank.Instance?.PlayBuild()")
    print("                Wiki #1: wall finishing plays a construct sound once (throttled 0.25s)")
    print("  alert.wav   — AudioBank.sfxAlert needs scene-side assignment")
    print("                AIDirector calls AudioBank.Instance?.PlayAlert(tier) on raid events")
    print("                Wiki #2: tier-3 produces 4 beeps, tier-1 produces 2 (siren burst)")
    print("  ambient.wav — AudioBank.sfxAmbient needs scene-side assignment")
    print("                AudioBank.Start() auto-starts loop on ambientSource (vol=0.18)")
    print("                Wiki #4: continuous ambient layer distinct from bgm")
