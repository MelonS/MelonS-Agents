"""Generate procedural SFX for PawnSim (colony-sim-style colony sim).
Redesigned 2026-05-30 — action-appropriate sounds for each event.

Produces (same filenames, drop-in replacement):
  chop.wav      — axe thunk into wood: dull woody percussive impact
  harvest.wav   — crisp plant rustle + gather
  hit.wav       — combat arrow/melee impact thud
  select.wav    — soft UI blip (subtle, per-click)
  wolf_howl.wav — low atmospheric wolf howl
  bgm_ambient.wav — calm frontier ambient bed (loopable, 30s)

All output: 44100Hz, 1ch (mono), 16-bit PCM.
Uses stdlib only (math, struct, wave, random).
"""
import math
import random
import struct
import wave
from pathlib import Path

SR = 44100  # unified to 44100Hz for all files


def write_wav(path, samples, sr=SR):
    """Write mono 16-bit PCM WAV."""
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        data = b"".join(
            struct.pack("<h", int(max(-32767, min(32767, s * 32767)))
        ) for s in samples)
        w.writeframes(data)


def normalize(samples, peak=0.88):
    """Normalize to peak amplitude."""
    mx = max(abs(s) for s in samples) if samples else 1.0
    if mx < 1e-9:
        return samples
    scale = peak / mx
    return [s * scale for s in samples]


# ---------------------------------------------------------------------------
# chop.wav — axe thunk into wood
# Design: woody percussive thunk. Low-mid body (~120-180Hz) + wood-crack
# transient (filtered noise burst with sharp attack). ~0.22s total.
# Character: dull, dense, satisfying — not a metallic click, not white noise.
# ---------------------------------------------------------------------------
def chop_sound():
    random.seed(42)
    dur = 0.22
    n = int(SR * dur)
    out = []

    for i in range(n):
        t = i / SR

        # --- Wood body resonance: two low-mid tones that fade fast
        # Oak/wood fundamental ~130Hz + harmonic ~220Hz
        body_env = math.exp(-t * 22)
        body = (
            math.sin(2 * math.pi * 130 * t) * 0.55 +
            math.sin(2 * math.pi * 220 * t) * 0.25 +
            math.sin(2 * math.pi * 85  * t) * 0.20   # low thump
        ) * body_env

        # --- Transient crack: short noise burst at the strike instant
        # Sharp attack, very fast decay — the "thwack" edge
        crack_env = math.exp(-t * 80)  # almost instantaneous
        crack = (random.random() * 2 - 1) * crack_env * 0.45

        # --- Wood grain flutter: mid-frequency noise (800-2000Hz feel)
        # Slow-decaying texture after the impact
        flutter_env = math.exp(-t * 35)
        # Bandpass-ish: combine two offset sinusoids modulated by noise
        grain_noise = (random.random() * 2 - 1)
        flutter = grain_noise * math.sin(2 * math.pi * 950 * t) * flutter_env * 0.18

        sample = body + crack + flutter
        out.append(sample)

    # Soft fade-out last 10ms to avoid click
    fade_n = int(SR * 0.01)
    for i in range(fade_n):
        out[-(i + 1)] *= i / fade_n

    return normalize(out, peak=0.85)


# ---------------------------------------------------------------------------
# harvest.wav — plant rustle + gather
# Design: a crisp rustling swish then a soft "thup" (item collected).
# Character: organic, airy, quick. Two-phase: swish (0.0-0.12s) + thup (0.12-0.22s).
# ---------------------------------------------------------------------------
def harvest_sound():
    random.seed(17)
    dur = 0.22
    n = int(SR * dur)
    out = []

    for i in range(n):
        t = i / SR

        # Phase 1: plant rustle (0-0.12s) — high-freq noise sweep
        if t < 0.12:
            # Rising then falling swish envelope
            rt = t / 0.12
            rustle_env = rt * (1.0 - rt) * 4.0  # peaks at midpoint
            # High freq noise (leaf-like)
            rustle = (random.random() * 2 - 1) * rustle_env * 0.55
            # Add a soft plant-body tone ~300Hz
            plant = math.sin(2 * math.pi * 320 * t) * rustle_env * 0.15
            sample = rustle + plant
        else:
            # Phase 2: soft gather "thup" (0.12-0.22s)
            tt = t - 0.12
            thup_env = math.exp(-tt * 45)
            # Soft low-mid pop ~200Hz
            thup = math.sin(2 * math.pi * 210 * tt) * thup_env * 0.50
            # Small noise component for texture
            tex = (random.random() * 2 - 1) * math.exp(-tt * 60) * 0.20
            sample = thup + tex

        out.append(sample)

    # Fade last 8ms
    fade_n = int(SR * 0.008)
    for i in range(fade_n):
        out[-(i + 1)] *= i / fade_n

    return normalize(out, peak=0.80)


# ---------------------------------------------------------------------------
# hit.wav — combat arrow/melee impact thud
# Design: sharp transient + low-mid thud. Arrow hitting flesh/armor has
# punch and a brief resonant decay. ~0.18s.
# Character: punchy, clear. Distinct from chop — higher transient freq,
# shorter body decay, tighter.
# ---------------------------------------------------------------------------
def hit_sound():
    random.seed(99)
    dur = 0.18
    n = int(SR * dur)
    out = []

    for i in range(n):
        t = i / SR

        # Sharp impact transient — metallic/flesh punch
        trans_env = math.exp(-t * 55)
        trans = (random.random() * 2 - 1) * trans_env * 0.40

        # Low-mid impact body ~160Hz (flesh thud)
        body_env = math.exp(-t * 30)
        body = math.sin(2 * math.pi * 160 * t) * body_env * 0.55

        # Bright click at moment of impact (arrow tip)
        click_env = math.exp(-t * 120)
        click = math.sin(2 * math.pi * 1800 * t) * click_env * 0.20

        # Brief hiss (arrow flight cut-off)
        hiss_env = math.exp(-t * 40)
        hiss = (random.random() * 2 - 1) * hiss_env * 0.15

        sample = trans + body + click + hiss
        out.append(sample)

    # Fade last 8ms
    fade_n = int(SR * 0.008)
    for i in range(fade_n):
        out[-(i + 1)] *= i / fade_n

    return normalize(out, peak=0.82)


# ---------------------------------------------------------------------------
# select.wav — soft UI blip for pawn selection
# Design: gentle rising tone, very short, non-annoying. Fires on every
# pawn click so must not be harsh or repetitive-sounding. ~0.08s.
# Character: soft, rounded, brief. Warmer than a typical UI beep.
# ---------------------------------------------------------------------------
def select_sound():
    dur = 0.08
    n = int(SR * dur)
    out = []

    for i in range(n):
        t = i / SR

        # Attack-decay envelope with soft knee
        env = math.exp(-t * 28) * (1.0 - math.exp(-t * 350))

        # Warm tone: fundamental 520Hz + soft harmonic 780Hz
        tone = (
            math.sin(2 * math.pi * 520 * t) * 0.65 +
            math.sin(2 * math.pi * 780 * t) * 0.20 +
            math.sin(2 * math.pi * 1040 * t) * 0.08  # gentle brightness
        ) * env * 0.7

        out.append(tone)

    # Fade last 10ms
    fade_n = int(SR * 0.01)
    for i in range(fade_n):
        out[-(i + 1)] *= i / fade_n

    return normalize(out, peak=0.70)


# ---------------------------------------------------------------------------
# wolf_howl.wav — atmospheric wolf howl
# Design: low growl + rising-then-falling tone. Frontier wilderness feel.
# ~1.2s. Plays once on wolf pack event.
# Character: ominous, low, long — not a cartoon "awooo" but a low
# timberwolf threat indicator.
# ---------------------------------------------------------------------------
def wolf_howl_sound():
    random.seed(55)
    dur = 1.2
    n = int(SR * dur)
    out = []

    for i in range(n):
        t = i / SR
        progress = t / dur  # 0..1

        # Overall howl envelope: fade in 0.08s, sustain, fade out 0.2s
        if t < 0.08:
            env = t / 0.08
        elif t > dur - 0.20:
            env = (dur - t) / 0.20
        else:
            env = 1.0
        # Slight tremolo
        tremolo = 1.0 + 0.06 * math.sin(2 * math.pi * 5.5 * t)
        env *= tremolo

        # Howl pitch: starts ~260Hz, rises to ~380Hz at 40%, falls to ~180Hz
        if progress < 0.40:
            f = 260 + (380 - 260) * (progress / 0.40)
        else:
            f = 380 - (380 - 180) * ((progress - 0.40) / 0.60)

        # Fundamental + harmonics for a wolf-like timbre
        howl = (
            math.sin(2 * math.pi * f * t) * 0.55 +
            math.sin(2 * math.pi * f * 2 * t) * 0.22 +
            math.sin(2 * math.pi * f * 3 * t) * 0.10 +
            math.sin(2 * math.pi * f * 0.5 * t) * 0.15  # sub-harmonic depth
        ) * env

        # Slight breathiness
        breath = (random.random() * 2 - 1) * env * 0.07

        out.append(howl + breath)

    return normalize(out, peak=0.78)


# ---------------------------------------------------------------------------
# bgm_ambient.wav — calm frontier colony ambient bed (30s loopable)
# Design: low drone + slow harmonic movement + subtle nature texture.
# Stays below BGM volume 0.25 (already set in AudioBank).
# Loop point: beginning = end (both fade to near-zero at t=0 and t=30).
# Character: open, calm, vaguely pastoral. Not oppressive or tense.
# Uses layered sine drones with slow LFO + gentle noise bed.
# ---------------------------------------------------------------------------
def bgm_ambient_sound():
    random.seed(1234)
    dur = 30.0
    n = int(SR * dur)
    out = []

    # Drone root: D2 = 73.4Hz (frontier/open feel)
    root = 73.4
    # Chord tones: root, fifth (110.1), octave (146.8), minor seventh (128.7)
    chord = [root, root * 1.5, root * 2.0, root * 1.755]

    # Slow LFO for movement (0.03Hz = ~33s cycle — barely perceptible)
    lfo_rate = 0.03

    # Pre-generate noise array for efficiency
    noise_arr = [random.random() * 2 - 1 for _ in range(n)]

    # Phase accumulators for smooth frequency (avoid clicks at boundaries)
    phases = [0.0] * len(chord)

    for i in range(n):
        t = i / SR
        progress = t / dur

        # Loop envelope: fade in first 2s, fade out last 2s
        if t < 2.0:
            loop_env = t / 2.0
        elif t > dur - 2.0:
            loop_env = (dur - t) / 2.0
        else:
            loop_env = 1.0

        # Slow LFO modulates overall amplitude slightly
        lfo = 0.85 + 0.15 * math.sin(2 * math.pi * lfo_rate * t)

        # Drone tones — each with individual slow vibrato
        drone = 0.0
        weights = [0.40, 0.25, 0.18, 0.12]
        for j, (f, w) in enumerate(zip(chord, weights)):
            # Slight per-tone vibrato (different rates so they drift)
            vib = 1.0 + 0.003 * math.sin(2 * math.pi * (0.07 + j * 0.023) * t)
            phases[j] += 2 * math.pi * f * vib / SR
            drone += math.sin(phases[j]) * w

        drone *= loop_env * lfo

        # Gentle nature-like noise bed (low-passed approximation via averaging)
        # Use a slow-decaying filtered noise (simple: average adjacent samples)
        noise = noise_arr[i] * 0.05 * loop_env

        # Occasional soft harmonic shimmer (high partial, very quiet)
        shimmer_env = 0.5 + 0.5 * math.sin(2 * math.pi * 0.11 * t)
        shimmer = math.sin(2 * math.pi * root * 4 * t) * shimmer_env * 0.025 * loop_env

        sample = drone + noise + shimmer
        out.append(sample)

    return normalize(out, peak=0.55)  # BGM at lower peak — AudioBank sets vol=0.25


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    out_dir = Path(__file__).parent

    print("Generating action-appropriate SFX for PawnSim...")

    sounds = {
        "chop.wav":       chop_sound,
        "harvest.wav":    harvest_sound,
        "hit.wav":        hit_sound,
        "select.wav":     select_sound,
        "wolf_howl.wav":  wolf_howl_sound,
        "bgm_ambient.wav": bgm_ambient_sound,
    }

    for fname, gen_fn in sounds.items():
        print(f"  Generating {fname}...", end=" ", flush=True)
        samples = gen_fn()
        write_wav(out_dir / fname, samples)
        # Verify
        with wave.open(str(out_dir / fname), "rb") as wf:
            sr = wf.getframerate()
            nf = wf.getnframes()
            dur = nf / sr
            peak_val = max(abs(s) for s in samples)
        print(f"OK  {sr}Hz mono 16bit {dur:.3f}s peak={peak_val:.3f}")

    print("\nAll SFX written. Throttle/wiring notes:")
    print("  chop.wav     - throttled 0.45s in TreeEntity.TakeChopDamage (per-frame safe)")
    print("  harvest.wav  - throttled 0.15s in AudioBank.PlayHarvest (fine)")
    print("  hit.wav      - throttled 0.25s in AudioBank.PlayHit (fine for arrows)")
    print("  select.wav   - no throttle needed (per-click, distinct events)")
    print("  wolf_howl.wav- NOTE: PlayWolfHowl() has no caller in any script.")
    print("                 Programmer action: call AudioBank.Instance?.PlayWolfHowl()")
    print("                 in AIDirector when wolf_pack event fires.")
    print("  bgm_ambient  - 30s loopable ambient bed. AudioBank vol=0.25 (correct).")
    print()
    print("  WIRING GAPS FOUND (programmer action required):")
    print("  1. StoneVeinEntity.TakeMineDamage calls PlayChop() — now sounds like axe.")
    print("     Add sfxMine slot to AudioBank + PlayMine() + generate mine.wav,")
    print("     OR repurpose hit.wav: StoneVeinEntity call PlayHit() instead of PlayChop().")
    print("     Throttle already set: SfxInterval=0.6s (good).")
    print("  2. AnimalEntity.TakeDamage calls PlayChop() — animal hit = axe sound.")
    print("     Should call PlayHit() for combat-hit feel.")
    print("     Throttle: AudioBank.PlayHit() has 0.25s throttle (fine per-collision).")
    print("  3. PlayWolfHowl() never called — see above.")
