"""Generate M3 procedural SFX for PawnSim — wave W-M3-02 Lane D.

Produces:
  rain.wav  — soft continuous filtered-noise rain bed, loopable.
              Wiki acceptance #9: a storm plays a rain loop; clear weather is
              silent of rain.

Design rationale:
  rain.wav must be:
    - Clearly a rain bed: broadband white/pink noise filtered to a continuous
      soft hiss — the classic "rain on a tin roof" spectral shape, but gentler
      (indoor-adjacent, not a downpour).
    - Loopable with zero crossings at both ends — AudioSource.loop=true will
      click at the loop boundary if the start/end amplitudes differ.
    - About 10–15s to keep the file lightweight while making the loop period
      inaudible at normal listening distances.
    - Low peak (0.65) — AudioBank plays this at vol 0.20 on a dedicated
      rainSource (same quiet-bed discipline as ambientSource vol=0.18).

Spectral shape:
  - Broadband noise (white) low-pass filtered at ~4kHz to remove harshness.
  - Gentle LFO (0.04Hz) on amplitude → slight "gusting" feel without obvious
    periodicity.
  - A very soft sub-layer at 70Hz acts as the "rain hitting the ground" low
    body, barely audible, gives the bed physical weight.
  - No chirps, no wind tones — this is pure rain, not ambient.

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
# rain.wav — soft loopable rain bed
# ---------------------------------------------------------------------------
def rain_sound():
    random.seed(314)
    dur = 12.0           # 12s — loop period inaudible at normal attention
    n = int(SR * dur)

    # --- 1. Generate white noise
    noise = [random.random() * 2.0 - 1.0 for _ in range(n)]

    # --- 2. Low-pass filter (simple IIR single-pole, cutoff ~4kHz)
    #   alpha = exp(-2*pi*fc/SR); y[i] = alpha*y[i-1] + (1-alpha)*x[i]
    fc = 4000.0
    alpha_lp = math.exp(-2 * math.pi * fc / SR)
    lp = [0.0] * n
    prev = 0.0
    for i in range(n):
        prev = alpha_lp * prev + (1.0 - alpha_lp) * noise[i]
        lp[i] = prev

    # --- 3. High-pass to remove DC and sub-rumble below ~80Hz
    #   Simple IIR high-pass: y[i] = alpha_hp*(y[i-1]+x[i]-x[i-1])
    fc_hp = 80.0
    alpha_hp = math.exp(-2 * math.pi * fc_hp / SR)
    hp = [0.0] * n
    prev_x = 0.0
    prev_y = 0.0
    for i in range(n):
        hp[i] = alpha_hp * (prev_y + lp[i] - prev_x)
        prev_x = lp[i]
        prev_y = hp[i]

    # --- 4. Sub-body layer: very soft 70Hz sine (ground-impact weight)
    sub_amp = 0.04
    sub = [math.sin(2 * math.pi * 70.0 * i / SR) * sub_amp for i in range(n)]

    # --- 5. Slow LFO modulation (0.04Hz "gusting") — makes the bed feel alive
    #   LFO range: [0.80, 1.00] — gentle, not obvious periodicity
    lfo_hz = 0.04
    lfo = [0.90 + 0.10 * math.sin(2 * math.pi * lfo_hz * i / SR) for i in range(n)]

    # --- 6. Combine
    out = [hp[i] * lfo[i] + sub[i] for i in range(n)]

    # --- 7. Loop-safe fade: 0.5s fade-in and fade-out to zero at both ends
    #   This ensures the loop boundary is click-free regardless of AudioSource.loop
    fade_samples = int(SR * 0.50)
    for i in range(fade_samples):
        frac = i / fade_samples
        out[i] *= frac            # fade in
        out[n - 1 - i] *= frac   # fade out

    return normalize(out, peak=0.65)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    out_dir = Path(__file__).parent

    print("Generating M3 rain SFX (rain bed loop)...")
    print("  rain.wav...", end=" ", flush=True)
    samples = rain_sound()
    write_wav(out_dir / "rain.wav", samples)
    with wave.open(str(out_dir / "rain.wav"), "rb") as wf:
        dur_s = wf.getnframes() / wf.getframerate()
    peak = max(abs(s) for s in samples)
    print(f"OK  {SR}Hz mono 16bit {dur_s:.3f}s peak={peak:.3f}")

    print()
    print("Wiring notes for QA (Lane D — rainLoop / rain.wav):")
    print("  rain.wav     — AudioBank.rainLoop (public AudioClip) REQUIRES scene-side")
    print("                 SerializeField assignment on the AudioBank GameObject")
    print("                 (same workflow as sfxBuild/sfxAlert/sfxAmbient/sfxMine).")
    print("  PlayRain()   — starts looping rainSource; null no-op if rainLoop unassigned.")
    print("  StopRain()   — stops rainSource; null no-op if rainSource not playing.")
    print("  RainSoundDriver polls WeatherController.Instance.Current:")
    print("    WeatherKind.Storm  -> AudioBank.Instance?.PlayRain()")
    print("    WeatherKind.Clear  -> AudioBank.Instance?.StopRain()")
    print("  Wiki acceptance #9: 'A storm plays a rain loop; clear weather is silent of rain.'")
