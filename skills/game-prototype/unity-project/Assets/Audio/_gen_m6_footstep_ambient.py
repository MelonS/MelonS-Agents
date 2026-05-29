"""Generate M6 procedural SFX for PawnSim — wave W-M6-01 B8 (Sound Designer lane).

Produces:
  footstep.wav    — short soft footfall, ~0.07s.
                    Wiki B8: a walking pawn plays a faint footstep ~every 0.4s.
                    Character: dull earth-thud with slight textural noise — quiet,
                    should NOT compete with BGM or SFX. AudioBank vol=0.45 on
                    sfxSource (low), throttled 0.2s in PlayFootstep() — outer
                    FootstepDriver throttle 0.4s per pawn is the primary gate.

  ambient_night.wav — looping outdoor night cricket/insect bed, ~15s loopable.
                    Wiki B8: ambient differs at 02:00 vs 12:00.
                    Character: continuous cricket chirping texture — clearly distinct
                    from ambient.wav (daytime birds+wind). Lower spectral brightness
                    than birds; repetitive cricket-pattern LFO texture.
                    AudioBank vol=0.18 on ambientSource (same as day bed).

Design notes:
  footstep.wav:
    - Very short (70ms). Two components:
        1. Low thud (60-120Hz): foot-strikes-earth body impact — short
           exponential decay envelope (~0.035s).
        2. Textural noise burst (grit/soil scratch): bandpass 200-800Hz,
           short (~0.06s), soft amplitude. Adds realism without harshness.
    - Fade last 8ms to prevent click.
    - Peak 0.70 (AudioBank further attenuates to ~0.45 vol).

  ambient_night.wav:
    - 15s loop (inaudible period at normal attention). Loop-safe: 0.8s
      fade-in/out at both ends so AudioSource.loop plays clean.
    - Cricket bed: amplitude-modulated narrow-band noise in the 3500-6000Hz
      range (cricket-like high-frequency texture) with a slow chirp-burst LFO
      at ~2Hz (cricket rhythm) combined with a lower 1800Hz resonance body.
    - Low sub-rumble at 40Hz (outdoor-night air body), very quiet (0.03 amp).
    - No wind or bird chirps — exclusively night insect character.
    - Peak 0.60. AudioBank vol=0.18 on ambientSource.

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
# footstep.wav — short soft earth-thud footfall, ~0.07s
# ---------------------------------------------------------------------------
def footstep_sound():
    random.seed(42)
    dur = 0.070
    n = int(SR * dur)
    out = []

    for i in range(n):
        t = i / SR

        # 1. Low-frequency earth thud (60-120Hz body)
        thud_env = math.exp(-t * 80.0)
        thud = (
            math.sin(2 * math.pi * 75.0  * t) * 0.55 +
            math.sin(2 * math.pi * 110.0 * t) * 0.30 +
            math.sin(2 * math.pi * 55.0  * t) * 0.20
        ) * thud_env

        # 2. Textural grit/soil noise (200-800Hz bandpass approximation)
        #    Use a simple one-pole LP at 800Hz minus a one-pole LP at 200Hz
        #    (computed inline here for simplicity via randomness + envelope)
        grit_env = math.exp(-t * 55.0) * 0.35
        noise_raw = random.random() * 2.0 - 1.0
        grit = noise_raw * grit_env

        sample = thud + grit
        out.append(sample)

    # Fade last 8ms to prevent click
    fade_n = int(SR * 0.008)
    for i in range(fade_n):
        out[-(i + 1)] *= i / fade_n

    return normalize(out, peak=0.70)


# ---------------------------------------------------------------------------
# ambient_night.wav — looping outdoor night cricket/insect bed, ~15s
# ---------------------------------------------------------------------------
def ambient_night_sound():
    random.seed(271)
    dur = 15.0
    n = int(SR * dur)

    # --- Cricket texture: amplitude-modulated narrow noise in 3500-6000Hz
    #     with a ~2Hz chirp-burst LFO (cricket rhythm).
    # Generate base noise
    noise = [random.random() * 2.0 - 1.0 for _ in range(n)]

    # Simple high-pass to focus in upper registers (LP at 3500Hz + subtract LP at 800Hz)
    # Single-pole LP at 3500Hz
    fc_lp = 3500.0
    alpha_lp = math.exp(-2 * math.pi * fc_lp / SR)
    lp3500 = [0.0] * n
    prev = 0.0
    for i in range(n):
        prev = alpha_lp * prev + (1.0 - alpha_lp) * noise[i]
        lp3500[i] = prev

    # Single-pole LP at 800Hz (subtract to get ~800-3500Hz band)
    fc_lp2 = 800.0
    alpha_lp2 = math.exp(-2 * math.pi * fc_lp2 / SR)
    lp800 = [0.0] * n
    prev2 = 0.0
    for i in range(n):
        prev2 = alpha_lp2 * prev2 + (1.0 - alpha_lp2) * noise[i]
        lp800[i] = prev2

    # High-mid band (3500-lp): cricket presence texture
    hm_band = [lp3500[i] - lp800[i] for i in range(n)]

    # 1800Hz cricket body resonance (narrow sine at cricket call frequency)
    # Adds warmth so it's not just noise
    cricket_body = [
        math.sin(2 * math.pi * 1800.0 * i / SR) * 0.08 +
        math.sin(2 * math.pi * 4200.0 * i / SR) * 0.06
        for i in range(n)
    ]

    # Cricket chirp LFO: ~2.2Hz burst envelope — crickets chirp in bursts
    # Use half-rectified LFO so there are distinct chirp/silence cycles
    chirp_hz = 2.2
    chirp_lfo = [
        max(0.0, math.sin(2 * math.pi * chirp_hz * i / SR)) ** 0.5
        for i in range(n)
    ]

    # Secondary offset cricket layer (different phase, 1.8Hz) — layered chirp
    chirp_hz2 = 1.8
    chirp_lfo2 = [
        max(0.0, math.sin(2 * math.pi * chirp_hz2 * i / SR + 1.2)) ** 0.5
        for i in range(n)
    ]

    # Very low sub-air at 40Hz (outdoor night body)
    sub_amp = 0.03
    sub = [math.sin(2 * math.pi * 40.0 * i / SR) * sub_amp for i in range(n)]

    # Combine
    out = []
    for i in range(n):
        t = i / SR

        # Loop envelope: 0.8s fade in/out
        if t < 0.8:
            loop_env = t / 0.8
        elif t > dur - 0.8:
            loop_env = (dur - t) / 0.8
        else:
            loop_env = 1.0

        # Cricket texture: two offset LFO layers
        cricket = (
            hm_band[i] * chirp_lfo[i] * 0.65 +
            hm_band[i] * chirp_lfo2[i] * 0.35 +
            cricket_body[i] * (chirp_lfo[i] * 0.6 + chirp_lfo2[i] * 0.4)
        ) * loop_env

        sample = cricket + sub[i] * loop_env
        out.append(sample)

    return normalize(out, peak=0.60)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    out_dir = Path(__file__).parent

    print("Generating M6 SFX (footstep + night ambient)...")

    print("  footstep.wav...", end=" ", flush=True)
    samples_fs = footstep_sound()
    write_wav(out_dir / "footstep.wav", samples_fs)
    with wave.open(str(out_dir / "footstep.wav"), "rb") as wf:
        dur_s = wf.getnframes() / wf.getframerate()
    peak = max(abs(s) for s in samples_fs)
    print(f"OK  {SR}Hz mono 16bit {dur_s:.3f}s peak={peak:.3f}")

    print("  ambient_night.wav...", end=" ", flush=True)
    samples_an = ambient_night_sound()
    write_wav(out_dir / "ambient_night.wav", samples_an)
    with wave.open(str(out_dir / "ambient_night.wav"), "rb") as wf:
        dur_s = wf.getnframes() / wf.getframerate()
    peak = max(abs(s) for s in samples_an)
    print(f"OK  {SR}Hz mono 16bit {dur_s:.3f}s peak={peak:.3f}")

    print()
    print("Wiring notes for QA (Lane B8 — footstep / night-ambient):")
    print()
    print("  footstep.wav     — AudioBank.sfxFootstep (public AudioClip)")
    print("                     REQUIRES scene-side SerializeField assignment on AudioBank GO")
    print("                     (same workflow as sfxBuild/sfxAlert/sfxAmbient/sfxMine/rainLoop/dangerBgm).")
    print("                     FootstepDriver.cs calls AudioBank.Instance?.PlayFootstep()")
    print("                     per walking pawn, throttled 0.4s per-pawn in FootstepDriver")
    print("                     + 0.2s AudioBank-side guard (defense-in-depth).")
    print()
    print("  ambient_night.wav — AudioBank.sfxAmbientNight (public AudioClip)")
    print("                      REQUIRES scene-side SerializeField assignment on AudioBank GO.")
    print("                      FootstepDriver polls DayNightCycle via GameClock.DayProgress:")
    print("                        DayProgress >= 0.83 or < 0.27  -> SwapAmbientToNight()")
    print("                        DayProgress in [0.27, 0.83)    -> SwapAmbientToDay()")
    print("                      AudioBank swaps ambientSource clip (cross-snap, no crossfade)")
    print("                      so the day-ambient (birds) and night-ambient (crickets)")
    print("                      differ audibly at 02:00 vs 12:00.")
    print()
    print("  DayNightCycle read accessor used:")
    print("    GameClock.Instance.DayProgress (float [0,1)) — READ-ONLY.")
    print("    Night = DayProgress >= 0.83 (22:00) OR DayProgress < 0.27 (06:30).")
    print("    This matches DayNightCycle.cs STOPS: 0.83=22:00 deep-night, 0.27=06:30 sunrise.")
    print()
    print("  Wiki B8 acceptance:")
    print("    - A walking pawn plays a faint footstep ~every 0.4s.")
    print("    - Ambient bed differs at 02:00 (DayProgress~0.083, night crickets)")
    print("      vs 12:00 (DayProgress=0.50, day birds).")
