"""Generate M6 action SFX for PawnSim — wave W-M6-03 B9 (Sound Designer lane).

Produces:
  door.wav   — soft door creak/thud, ~0.18s.
               Wiki B9: opening a door plays a distinct sound.
               Character: low woody creak (180-400Hz) + brief door-frame thud
               transient (100Hz body). Soft attack, short sustain.
               AudioBank.PlayDoor() throttled 0.15s. Volume 0.75.

  cook.wav   — sizzle/clatter cooking sound, ~0.30s.
               Wiki B9: cooking at a stove plays a distinct sound.
               Character: high-freq sizzle (bandpass noise 1500-4000Hz) +
               a light metallic clatter tap (800Hz + 1200Hz transient).
               AudioBank.PlayCook() throttled 0.5s. Volume 0.70.

  shoot.wav  — bow-twang arrow-launch, ~0.15s.
               Wiki B9: firing an arrow plays a distinct sound.
               Character: sharp pluck/twang (120-180Hz fundamental + 400Hz
               harmonic, fast decay) + brief string-tension noise burst.
               AudioBank.PlayShoot() throttled 0.0s (per-fire, each distinct).
               Volume 0.80.

All: 44100Hz, mono, 16-bit PCM. Uses stdlib only.

Throttle rationale:
  door   0.15s — door is opened once per pass-through; 0.15s prevents a
                 pawn entering and exiting in a tight frame burst from
                 double-triggering (OpenHintDuration=0.3s in DoorEntity).
  cook   0.50s — cookInterval in PawnCook is 1.5s; 0.5s bank-side guard
                 allows distinct cook sounds if two pawns share a stove but
                 blocks any sub-frame duplicates.
  shoot  0.00s — each arrow is a deliberate, distinct user/AI event;
                 no throttle needed (ArrowProjectile.SpawnArrow fires once
                 per bow-draw cycle, not a tight loop).
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
# door.wav — soft door creak/thud, ~0.18s
# Design: two layers
#   1. Door creak (180-400Hz woody resonance): slow-attack sine cluster,
#      simulates hinges + wood flex — peaks at ~0.04s then decays.
#   2. Frame/latch thud (80-120Hz): short impact transient at onset,
#      the door meets its frame. Very fast decay (~0.03s).
# ---------------------------------------------------------------------------
def door_sound():
    random.seed(17)
    dur = 0.18
    n = int(SR * dur)
    out = []

    for i in range(n):
        t = i / SR

        # Layer 1: woody creak (180-400Hz), soft attack then decay
        # Attack envelope: rises to peak over 0.04s, then decays
        attack = min(t / 0.04, 1.0)
        decay = math.exp(-max(0.0, t - 0.04) * 22.0)
        creak_env = attack * decay

        creak = (
            math.sin(2 * math.pi * 185.0 * t) * 0.50 +
            math.sin(2 * math.pi * 260.0 * t) * 0.30 +
            math.sin(2 * math.pi * 380.0 * t) * 0.15 +
            math.sin(2 * math.pi * 320.0 * t) * 0.10
        ) * creak_env

        # Layer 2: frame thud (80-130Hz), very short at onset
        thud_env = math.exp(-t * 95.0)
        thud = (
            math.sin(2 * math.pi * 95.0  * t) * 0.60 +
            math.sin(2 * math.pi * 130.0 * t) * 0.25
        ) * thud_env * 0.55

        # Small noise burst for wood texture
        noise_env = math.exp(-t * 110.0) * 0.18
        noise = (random.random() * 2.0 - 1.0) * noise_env

        sample = creak + thud + noise
        out.append(sample)

    # Fade last 8ms
    fade_n = int(SR * 0.008)
    for i in range(fade_n):
        out[-(i + 1)] *= i / fade_n

    return normalize(out, peak=0.78)


# ---------------------------------------------------------------------------
# cook.wav — sizzle/clatter cooking sound, ~0.30s
# Design: two layers
#   1. Sizzle (bandpass noise 1500-4000Hz): cooking fat/liquid on hot surface.
#      Sustained, quiet, fast-decaying amplitude from initial onset.
#   2. Metallic clatter (800Hz + 1200Hz transient): spoon/pan knock at onset.
#      Sharp attack, very fast decay (~0.02s).
# ---------------------------------------------------------------------------
def cook_sound():
    random.seed(53)
    dur = 0.30
    n = int(SR * dur)

    # Build sizzle noise: LP at 4000Hz minus LP at 1200Hz = bandpass
    raw_noise = [random.random() * 2.0 - 1.0 for _ in range(n)]

    # Single-pole LP at 4000Hz
    alpha_hi = math.exp(-2 * math.pi * 4000.0 / SR)
    lp4k = [0.0] * n
    prev = 0.0
    for i in range(n):
        prev = alpha_hi * prev + (1.0 - alpha_hi) * raw_noise[i]
        lp4k[i] = prev

    # Single-pole LP at 1200Hz (subtract to approximate bandpass)
    alpha_lo = math.exp(-2 * math.pi * 1200.0 / SR)
    lp1k2 = [0.0] * n
    prev2 = 0.0
    for i in range(n):
        prev2 = alpha_lo * prev2 + (1.0 - alpha_lo) * raw_noise[i]
        lp1k2[i] = prev2

    sizzle_band = [lp4k[i] - lp1k2[i] for i in range(n)]

    out = []
    for i in range(n):
        t = i / SR

        # Sizzle envelope: strong onset then quick settle to low sustained level
        sizzle_env = 0.15 + 0.85 * math.exp(-t * 18.0)
        sizzle = sizzle_band[i] * sizzle_env * 0.55

        # Metallic clatter transient at onset (800Hz + 1200Hz)
        clatter_env = math.exp(-t * 160.0)
        clatter = (
            math.sin(2 * math.pi * 820.0  * t) * 0.55 +
            math.sin(2 * math.pi * 1250.0 * t) * 0.30
        ) * clatter_env * 0.70

        sample = sizzle + clatter
        out.append(sample)

    # Fade last 15ms
    fade_n = int(SR * 0.015)
    for i in range(fade_n):
        out[-(i + 1)] *= i / fade_n

    return normalize(out, peak=0.72)


# ---------------------------------------------------------------------------
# shoot.wav — bow-twang arrow-launch, ~0.15s
# Design: two layers
#   1. Bow-string pluck/twang (120-180Hz fundamental + harmonics):
#      simulates a taut bowstring released. Fast-attack (string snap),
#      decaying ring (~0.12s). Pitch slightly descending (string tension
#      drops as arrow leaves) — modeled as slow FM detuning.
#   2. Arrow flight whoosh onset (narrow noise burst, 600-1200Hz):
#      very brief (~0.04s) to suggest the arrow leaving.
# ---------------------------------------------------------------------------
def shoot_sound():
    random.seed(91)
    dur = 0.15
    n = int(SR * dur)
    out = []

    for i in range(n):
        t = i / SR

        # Layer 1: bow-string twang
        # Fast attack (0-0.008s), then exponential decay
        attack = min(t / 0.008, 1.0)
        decay = math.exp(-max(0.0, t - 0.008) * 30.0)
        twang_env = attack * decay

        # Slight pitch descend: fundamental starts at 145Hz, drops ~8Hz
        f0 = 145.0 - 8.0 * (t / dur)
        twang = (
            math.sin(2 * math.pi * f0       * t) * 0.60 +
            math.sin(2 * math.pi * f0 * 2.0 * t) * 0.25 +
            math.sin(2 * math.pi * f0 * 3.0 * t) * 0.10 +
            math.sin(2 * math.pi * 420.0    * t) * 0.12
        ) * twang_env

        # Layer 2: arrow whoosh onset (very brief, 600-1200Hz noise burst)
        whoosh_env = math.exp(-t * 140.0) * 0.30
        raw_noise = random.random() * 2.0 - 1.0
        whoosh = raw_noise * whoosh_env

        sample = twang + whoosh
        out.append(sample)

    # Fade last 10ms
    fade_n = int(SR * 0.010)
    for i in range(fade_n):
        out[-(i + 1)] *= i / fade_n

    return normalize(out, peak=0.82)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    out_dir = Path(__file__).parent

    print("Generating M6 action SFX (door / cook / shoot)...")

    print("  door.wav...", end=" ", flush=True)
    samples_d = door_sound()
    write_wav(out_dir / "door.wav", samples_d)
    with wave.open(str(out_dir / "door.wav"), "rb") as wf:
        dur_d = wf.getnframes() / wf.getframerate()
    print(f"OK  {SR}Hz mono 16bit {dur_d:.3f}s peak={max(abs(s) for s in samples_d):.3f}")

    print("  cook.wav...", end=" ", flush=True)
    samples_c = cook_sound()
    write_wav(out_dir / "cook.wav", samples_c)
    with wave.open(str(out_dir / "cook.wav"), "rb") as wf:
        dur_c = wf.getnframes() / wf.getframerate()
    print(f"OK  {SR}Hz mono 16bit {dur_c:.3f}s peak={max(abs(s) for s in samples_c):.3f}")

    print("  shoot.wav...", end=" ", flush=True)
    samples_s = shoot_sound()
    write_wav(out_dir / "shoot.wav", samples_s)
    with wave.open(str(out_dir / "shoot.wav"), "rb") as wf:
        dur_s = wf.getnframes() / wf.getframerate()
    print(f"OK  {SR}Hz mono 16bit {dur_s:.3f}s peak={max(abs(s) for s in samples_s):.3f}")

    print()
    print("Wiring notes for QA (Lane B9 — door / cook / shoot):")
    print()
    print("  door.wav   — AudioBank.sfxDoor (public AudioClip)")
    print("               REQUIRES scene-side Inspector assignment on AudioBank GO.")
    print("               DoorEntity.cs NotifyPassing() calls AudioBank.Instance?.PlayDoor().")
    print("               PlayDoor() throttled 0.15s (DoorInterval).")
    print()
    print("  cook.wav   — AudioBank.sfxCook (public AudioClip)")
    print("               REQUIRES scene-side Inspector assignment on AudioBank GO.")
    print("               StoveEntity.cs CookOne() calls AudioBank.Instance?.PlayCook().")
    print("               PlayCook() throttled 0.5s (CookInterval); cookInterval=1.5s is outer gate.")
    print()
    print("  shoot.wav  — AudioBank.sfxShoot (public AudioClip)")
    print("               REQUIRES scene-side Inspector assignment on AudioBank GO.")
    print("               ArrowProjectile.SpawnArrow() calls AudioBank.Instance?.PlayShoot().")
    print("               PlayShoot() no throttle (ShootInterval=0.0): each arrow-launch is distinct.")
    print()
    print("  Wiki B9 acceptance (binary):")
    print("    Opening a door -> door.wav plays (soft creak/thud).")
    print("    Cooking at a stove -> cook.wav plays (sizzle/clatter).")
    print("    Firing an arrow -> shoot.wav plays (bow-twang).")
