"""Generate M3 procedural SFX for PawnSim — wave W-M3-03 Lane A.

Produces:
  danger.wav  — tense low-drone + slow percussive bed, loopable.
                Wiki acceptance #8: music swaps to the tension track during
                a raid (threatTier>=2) and back when clear.

Design rationale:
  danger.wav must be:
    - Clearly "danger/tension" in character: low ominous drone + heavy slow
      percussion hits — distinct from the calm bgm_ambient bed.
    - Loopable with zero-crossings at both ends (fade-in / fade-out so
      AudioSource.loop=true plays without clicks at the boundary).
    - ~16s loop period — long enough that the loop point is not noticed
      during a raid encounter, short enough to keep the file lightweight.
    - Low peak (0.70) — MusicDirector/AudioBank crossfade manages actual
      playback volume; the clip itself has headroom.

Spectral composition:
  Layer 1 — Low drone: 55Hz (A1) + 82.5Hz (E2) + 110Hz (A2) — minor 5th
             stack, ominous "dark pad" quality. Slow vibrato 0.15Hz.
  Layer 2 — Tension shimmer: filtered narrow-band noise centered ~900Hz,
             amplitude modulated at 1.8Hz — gives a sense of urgency
             without melodic content.
  Layer 3 — Slow percussive hits: kick-like transient at ~0.4Hz (one hit
             every ~2.5s), 60Hz fundamental + noise burst + 8ms pitch drop.
             Four hits spread across the 16s loop with slight timing variation
             so they don't feel metronomic.

Loop integrity: 0.8s cosine fade-in + 0.8s cosine fade-out at endpoints.
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


def loop_crossfade(samples, xf_sec=1.0, sr=SR):
    """#게임필5(2026-06-10 자율) — 꼬리→머리 equal-power 크로스페이드 (루프 절벽 제거)."""
    xf = int(sr * xf_sec)
    if len(samples) <= xf * 2:
        return samples
    body = samples[:-xf]
    tail = samples[-xf:]
    for i in range(xf):
        t = (i + 1) / xf
        body[i] = tail[i] * math.cos(t * math.pi / 2) + body[i] * math.sin(t * math.pi / 2)
    return body


def danger_sound():
    random.seed(777)
    dur = 16.0
    n = int(SR * dur)
    out = [0.0] * n

    # -----------------------------------------------------------------------
    # Layer 1: Low drone — minor-5th stack (55 / 82.5 / 110 Hz)
    # Slow vibrato at 0.15Hz for organic tension feel.
    # -----------------------------------------------------------------------
    vib_hz = 0.15
    vib_depth = 0.008      # +/- 0.8% pitch wobble — subtle
    drone_amp = 0.38
    for i in range(n):
        t = i / SR
        vib = 1.0 + vib_depth * math.sin(2 * math.pi * vib_hz * t)
        # Slightly detuned octave doubling for thickness
        d = (
            math.sin(2 * math.pi * 55.0 * vib * t) * 0.50 +
            math.sin(2 * math.pi * 82.5 * vib * t) * 0.35 +
            math.sin(2 * math.pi * 110.0 * vib * t) * 0.25 +
            math.sin(2 * math.pi * 55.3 * vib * t) * 0.12   # slight detune for beating
        )
        out[i] += d * drone_amp

    # -----------------------------------------------------------------------
    # Layer 2: Tension shimmer — narrow-band noise ~900Hz, AM at 1.8Hz
    # Bandpass approximation: mix noise with 900Hz carrier; AM creates urgency.
    # -----------------------------------------------------------------------
    am_hz = 1.8
    shimmer_amp = 0.14
    prev_lp = 0.0
    alpha_lp = math.exp(-2 * math.pi * 1200.0 / SR)   # LP at 1.2kHz
    prev_hp = 0.0
    prev_hp_x = 0.0
    alpha_hp = math.exp(-2 * math.pi * 600.0 / SR)    # HP at 600Hz
    for i in range(n):
        t = i / SR
        noise = random.random() * 2.0 - 1.0
        # LP
        prev_lp = alpha_lp * prev_lp + (1.0 - alpha_lp) * noise
        # HP
        hp_y = alpha_hp * (prev_hp + prev_lp - prev_hp_x)
        prev_hp_x = prev_lp
        prev_hp = hp_y
        # AM envelope: pulsing urgency
        am = 0.6 + 0.4 * math.sin(2 * math.pi * am_hz * t)
        out[i] += hp_y * am * shimmer_amp

    # -----------------------------------------------------------------------
    # Layer 3: Slow percussive hits
    # Hit times (seconds into the 16s loop) — avoid 0.0 and 16.0 for clean loop
    # Four hits: ~0, ~4, ~8, ~12s with slight jitter but seeded for repeatability
    # -----------------------------------------------------------------------
    hit_base_times = [0.8, 4.3, 8.1, 12.5]
    hit_amp = 0.50
    hit_dur = 0.35       # each hit decays over 0.35s
    hit_hz = 58.0        # fundamental kick frequency

    for ht in hit_base_times:
        hit_start = int(ht * SR)
        hit_samples = int(hit_dur * SR)
        for j in range(hit_samples):
            if hit_start + j >= n:
                break
            t_h = j / SR
            env = math.exp(-t_h * 22.0)                    # ~45ms half-life
            # Pitch drop: start at 120Hz, drop to hit_hz over 40ms
            pitch_hz = hit_hz + (120.0 - hit_hz) * math.exp(-t_h * 80.0)
            tone = math.sin(2 * math.pi * pitch_hz * t_h)
            # Noise burst (stone/thud texture)
            noise_h = random.random() * 2.0 - 1.0
            noise_env = math.exp(-t_h * 150.0)             # 6ms noise burst
            sample = (tone * 0.70 + noise_h * noise_env * 0.30) * env * hit_amp
            out[hit_start + j] += sample

    # -----------------------------------------------------------------------
    # #게임필5 — 0.8s 양끝 cosine fade 는 루프마다 ~1.6s 무음 절벽(긴장 음악이
    #  주기적으로 꺼짐).  꼬리→머리 크로스페이드로 교체 — 경계에서도 풀 밀도.
    # -----------------------------------------------------------------------
    return normalize(loop_crossfade(out, xf_sec=1.0), peak=0.70)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    out_dir = Path(__file__).parent

    print("Generating M3 danger SFX (tense drone + percussive bed)...")
    print("  danger.wav...", end=" ", flush=True)
    samples = danger_sound()
    write_wav(out_dir / "danger.wav", samples)
    with wave.open(str(out_dir / "danger.wav"), "rb") as wf:
        dur_s = wf.getnframes() / wf.getframerate()
    peak = max(abs(s) for s in samples)
    print(f"OK  {SR}Hz mono 16bit {dur_s:.3f}s peak={peak:.3f}")

    print()
    print("Wiring notes for QA (Lane A — dangerBgm / danger.wav):")
    print("  danger.wav   — AudioBank.dangerBgm (public AudioClip) REQUIRES scene-side")
    print("                 SerializeField assignment on the AudioBank GameObject")
    print("                 (same workflow as sfxBuild/sfxAlert/sfxAmbient/sfxMine/rainLoop).")
    print("  PlayDangerMusic()  — crossfades dangerSource in, bgmSource out (~1s lerp).")
    print("                       Idempotent: no-op if danger is already playing.")
    print("  StopDangerMusic()  — crossfades bgmSource back in, dangerSource out (~1s lerp).")
    print("                       Idempotent: no-op if calm is already playing.")
    print("  MusicDirector polls AIDirector.Instance.CurrentThreatTier:")
    print("    tier >= 2  -> AudioBank.Instance?.PlayDangerMusic()")
    print("    tier <  2  -> AudioBank.Instance?.StopDangerMusic()")
    print("  Wiki acceptance #8: 'Music swaps to the tension track during a raid")
    print("                       (threatTier>=2) and back when clear.'")
