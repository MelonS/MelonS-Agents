"""Generate M3 procedural SFX for PawnSim — wave W-M3-01 Lane D.

Produces:
  mine.wav  — pick-on-stone impact (wiki #7: mining plays a distinct sound,
              not the chop thunk; pick-on-stone character, sharper high-freq
              transient, stone resonance body, distinct from axe-on-wood chop).

All: 44100Hz, mono, 16-bit PCM. Uses stdlib only.

Design rationale:
  chop.wav has a woody, low resonance (150-240Hz body, 1400Hz clink top).
  mine.wav must be clearly distinct:
    - Higher primary transient: 2400-3200Hz (metal pick tip on stone)
    - Stone-cavity resonance body: 400-600Hz (rock chamber ring, NOT woody 150Hz)
    - Shorter decay (~0.18s total vs chop ~0.30s) — stone is denser, less sustain
    - More impulse-noise (chip/grit texture) vs wood's fibrous crack
  AudioBank.PlayMine() throttled at 0.25s (MineInterval).
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
# mine.wav — pick-on-stone impact
# Design: three-layer:
#   1. Metal-pick transient (0-0.025s): high-frequency clink of tool tip
#      meeting rock — 2400Hz + 3100Hz + 1800Hz, very fast decay.
#   2. Stone resonance body (peaks ~0.015s, decays by 0.15s): cavity ring
#      of struck rock — 420Hz + 580Hz + 280Hz, slower decay than wood.
#   3. Grit/chip noise burst (0-0.04s): stone fragments, rough impulse noise,
#      band-passed to 800-2000Hz range for "stone chip" texture.
# Total duration: ~0.18s. Shorter than chop.wav (0.30s) for denser feel.
# ---------------------------------------------------------------------------
def mine_sound():
    random.seed(42)
    dur = 0.18
    n = int(SR * dur)
    out = []

    for i in range(n):
        t = i / SR

        # --- Layer 1: Metal-pick transient (high-frequency, very fast decay)
        pick_env = math.exp(-t * 180)  # ~5.5ms half-life — sharper than chop clink
        pick = (
            math.sin(2 * math.pi * 2400 * t) * 0.50 +
            math.sin(2 * math.pi * 3100 * t) * 0.30 +
            math.sin(2 * math.pi * 1800 * t) * 0.20
        ) * pick_env

        # --- Layer 2: Stone resonance body (mid-freq ring, slower decay)
        stone_delay = max(0.0, t - 0.008)  # slight onset delay after pick contact
        stone_env = math.exp(-stone_delay * 28)  # ~35ms half-life — stone ring sustain
        stone = (
            math.sin(2 * math.pi * 420 * t) * 0.55 +
            math.sin(2 * math.pi * 580 * t) * 0.28 +
            math.sin(2 * math.pi * 280 * t) * 0.22
        ) * stone_env * 0.75

        # --- Layer 3: Grit/chip noise burst (stone fragment texture)
        grit_env = math.exp(-t * 120)  # ~8ms decay — brief impulse
        raw_noise = random.random() * 2 - 1
        # Crude band-pass approximation: mix raw noise with a high-freq sine
        # to push spectral content toward 800-2000Hz stone-chip range
        grit_tone = math.sin(2 * math.pi * 1200 * t) * 0.4 + raw_noise * 0.6
        grit = grit_tone * grit_env * 0.35

        sample = pick + stone + grit
        out.append(sample)

    # Fade last 10ms for clean loop/repeat
    fade_n = int(SR * 0.010)
    for i in range(fade_n):
        out[-(i + 1)] *= i / fade_n

    return normalize(out, peak=0.84)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    out_dir = Path(__file__).parent

    print("Generating M3 mine SFX (pick-on-stone)...")
    print("  mine.wav...", end=" ", flush=True)
    samples = mine_sound()
    write_wav(out_dir / "mine.wav", samples)
    with wave.open(str(out_dir / "mine.wav"), "rb") as wf:
        dur_s = wf.getnframes() / wf.getframerate()
    peak = max(abs(s) for s in samples)
    print(f"OK  {SR}Hz mono 16bit {dur_s:.3f}s peak={peak:.3f}")

    print()
    print("Wiring notes for QA (Lane D — sfxMine):")
    print("  mine.wav    — AudioBank.sfxMine (public AudioClip) REQUIRES scene-side")
    print("                SerializeField assignment on the AudioBank GameObject")
    print("                (same workflow as sfxBuild/sfxAlert/sfxAmbient last wave).")
    print("  PlayMine()  — throttled 0.25s (MineInterval), own _lastMineTime guard,")
    print("                graceful null no-op if sfxMine or sfxSource unassigned.")
    print("  StoneVeinEntity.TakeMineDamage() — AudioBank.Instance?.PlayMine()")
    print("                (was PlayChop(); entity-level 0.6s SfxInterval guard kept).")
    print("  Wiki acceptance #7: 'Mining ... plays a distinct sound' (pick-on-stone,")
    print("                not the chop thunk).")
