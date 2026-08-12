"""Generate reward-moment SFX for PawnSim — #게임필 배치2 (2026-06-10 자율).

격차 분석 갈래4: 게임의 '보상 순간'이 전부 무음 — 나무 쓰러짐, 바위 붕괴, 식사,
연구 완료.  _gen_sfx.py 의 절차 합성 idiom 그대로 (stdlib only, 44100Hz mono 16-bit).

Produces (Assets/Audio/ 에 드롭):
  treefall.wav  — 저역 우드 크래시 + 낙하 럼블 (~0.6s)
  rockbreak.wav — 자갈 무너짐/돌 부서짐 (~0.45s)
  eat.wav       — 부드러운 머치 2회 (~0.32s)
  research.wav  — 2음 상승 징글 (~0.55s)

Run:  python _tmp_gen_reward_sfx.py <output-dir>
"""
import math
import random
import struct
import sys
import wave
from pathlib import Path

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

SR = 44100


def write_wav(path, samples, sr=SR):
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        data = b"".join(
            struct.pack("<h", int(max(-32767, min(32767, s * 32767))))
            for s in samples)
        w.writeframes(data)


def normalize(samples, peak=0.88):
    mx = max(abs(s) for s in samples) if samples else 1.0
    if mx < 1e-9:
        return samples
    scale = peak / mx
    return [s * scale for s in samples]


def fade_tail(out, ms=15):
    fade_n = min(len(out), int(SR * ms / 1000.0))
    for i in range(fade_n):
        out[-(i + 1)] *= i / fade_n
    return out


# ---------------------------------------------------------------------------
# treefall.wav — 나무 쓰러짐: 삐걱(crack) → 휘익(whoosh) → 쿵(impact rumble)
# ---------------------------------------------------------------------------
def treefall_sound():
    random.seed(101)
    dur = 0.62
    n = int(SR * dur)
    out = []
    for i in range(n):
        t = i / SR
        s = 0.0
        # phase 1 (0~0.12s): 섬유 찢어지는 crack 연타 — 짧은 노이즈 버스트 3개
        for k, t0 in enumerate((0.0, 0.045, 0.09)):
            if t >= t0:
                e = math.exp(-(t - t0) * 90)
                s += (random.random() * 2 - 1) * e * (0.32 - k * 0.07)
        # phase 2 (0.05~0.4s): 낙하 whoosh — 하강 필터 노이즈 (느린 envelope)
        if t >= 0.05:
            tw = t - 0.05
            we = math.exp(-((tw - 0.16) ** 2) / 0.012)   # 0.21s 부근 피크
            s += (random.random() * 2 - 1) * math.sin(2 * math.pi * (700 - 900 * tw) * tw) * we * 0.22
        # phase 3 (0.34s~): 지면 impact — 저역 thump + 럼블 감쇠
        if t >= 0.34:
            ti = t - 0.34
            ie = math.exp(-ti * 14)
            s += (math.sin(2 * math.pi * 55 * ti) * 0.8 +
                  math.sin(2 * math.pi * 90 * ti) * 0.4 +
                  math.sin(2 * math.pi * 38 * ti) * 0.5) * ie
            s += (random.random() * 2 - 1) * math.exp(-ti * 40) * 0.25  # 흙 튐
        out.append(s)
    return normalize(fade_tail(out), peak=0.87)


# ---------------------------------------------------------------------------
# rockbreak.wav — 광맥 붕괴: 둔탁한 균열 + 자갈 흩어짐
# ---------------------------------------------------------------------------
def rockbreak_sound():
    random.seed(202)
    dur = 0.45
    n = int(SR * dur)
    out = []
    for i in range(n):
        t = i / SR
        s = 0.0
        # 균열 임팩트: 저중역 돌 울림
        e0 = math.exp(-t * 26)
        s += (math.sin(2 * math.pi * 160 * t) * 0.5 +
              math.sin(2 * math.pi * 240 * t) * 0.3 +
              math.sin(2 * math.pi * 95 * t) * 0.35) * e0
        # 자갈 흩어짐: 0.06s 부터 짧은 노이즈 틱 다발 (감쇠)
        if t >= 0.06:
            tg = t - 0.06
            density = max(0.0, 1.0 - tg * 3.2)
            if random.random() < 0.22 * density:
                s += (random.random() * 2 - 1) * 0.55 * density
            s += (random.random() * 2 - 1) * math.exp(-tg * 18) * 0.12
        out.append(s)
    return normalize(fade_tail(out), peak=0.85)


# ---------------------------------------------------------------------------
# eat.wav — 식사: 부드러운 머치 2회 (만족스러운, 코믹하지 않게 낮은 톤)
# ---------------------------------------------------------------------------
def eat_sound():
    random.seed(303)
    dur = 0.32
    n = int(SR * dur)
    out = []
    for i in range(n):
        t = i / SR
        s = 0.0
        for t0 in (0.02, 0.17):
            if t >= t0:
                tm = t - t0
                e = math.exp(-tm * 45)
                # 입 안 머치: 저역 톤 + 부드러운 노이즈, 살짝 다른 피치
                pitch = 210 if t0 < 0.1 else 180
                s += (math.sin(2 * math.pi * pitch * tm) * 0.4 +
                      (random.random() * 2 - 1) * 0.3) * e * (1.0 - tm * 1.5)
        out.append(s)
    return normalize(fade_tail(out), peak=0.7)   # 식사는 잔잔하게


# ---------------------------------------------------------------------------
# research.wav — 연구 완료: 2음 상승 징글 (보상감, 따뜻한 사인+5th 화음)
# ---------------------------------------------------------------------------
def research_sound():
    dur = 0.55
    n = int(SR * dur)
    out = []
    for i in range(n):
        t = i / SR
        s = 0.0
        # 1음: C5 (523Hz), 0~0.3s
        if t < 0.32:
            e = math.exp(-t * 7) * min(1.0, t * 60)
            s += (math.sin(2 * math.pi * 523.25 * t) * 0.5 +
                  math.sin(2 * math.pi * 784.0 * t) * 0.18) * e
        # 2음: G5 (784Hz), 0.18s~ — 상승 해결
        if t >= 0.18:
            t2 = t - 0.18
            e2 = math.exp(-t2 * 6) * min(1.0, t2 * 60)
            s += (math.sin(2 * math.pi * 784.0 * t2) * 0.45 +
                  math.sin(2 * math.pi * 1046.5 * t2) * 0.22 +
                  math.sin(2 * math.pi * 1568.0 * t2) * 0.08) * e2
        out.append(s)
    return normalize(fade_tail(out, ms=40), peak=0.78)


def main():
    outdir = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(".")
    outdir.mkdir(parents=True, exist_ok=True)
    for name, fn in (("treefall", treefall_sound), ("rockbreak", rockbreak_sound),
                     ("eat", eat_sound), ("research", research_sound)):
        samples = fn()
        p = outdir / f"{name}.wav"
        write_wav(p, samples)
        print(f"{p}  ({len(samples)/SR:.2f}s, peak-normalized)")


if __name__ == "__main__":
    main()
