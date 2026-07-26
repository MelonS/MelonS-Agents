"""Generate ambient.wav — 주간 야외 앰비언트 베드 (바람 + 간헐 새소리), 12s 루프.

계기 (2026-07-27 운영자): "아직도 삐 하는 소리가 들리네."

진단: 기존 ambient.wav 는 **200.0 Hz 사인 + 400.0 Hz 2배음**에 전체 에너지의 약 45%가
몰린 순음 드론이었다.  이게 상시 루프로 깔리니 계속 "삐~" 로 들린다.
`bgm_ambient.wav`(2026-05-30 _gen_sfx.py 재작업본)는 260/347/439 Hz 로 분산된 화음
패드라 정상이었다 — 즉 BGM 은 고쳐졌는데 **그 옆의 주간 앰비언트가 누락**돼 있었다.
AudioBank.cs 주석은 이 파일을 이미 "wind/birds bed" 로 기술하고 있었으므로,
코드 기대와 에셋 실물이 어긋난 상태이기도 했다.

설계 — **순음 금지**가 유일한 하드 규칙. 전부 노이즈 기반으로 만든다:
  1. 바람 베드: 화이트노이즈 → 저역(원폴 LP ~420Hz) + 중고역 밴드(1.2~3kHz "공기감").
     느린 LFO 2개(주기가 길이를 정수분할 → 루프 이음매 없음)로 돌풍 진폭 변조.
  2. 새소리: 짧은 처프(2.4~4.2kHz FM 스윕)를 드물게 배치.  루프 경계 0.8s 는
     비워서 크로스페이드가 처프를 반토막 내지 않게 한다.
  3. 심리스 루프: 꼬리 0.6s 를 머리와 등파워 크로스페이드.

출력: 44100Hz, 1ch, 16-bit PCM, 12.00s.  **RMS 기준** 정규화(TARGET_RMS) — 피크가 아니다.
AudioBank vol=0.18 (ambientSource) — 기존과 동일하므로 배선 변경 불필요.
stdlib 만 사용 (math, random, struct, wave) — 기존 _gen_*.py 관례.
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
DUR = 12.0
XFADE = 0.6          # 루프 이음매 크로스페이드 길이
BIRD_GUARD = 0.8     # 루프 경계에서 새소리를 배제할 여유
SEED = 20260727

# 목표 RMS — 믹스에서 BGM 아래에 깔리도록 실측으로 잡은 값.
#   ambientSource vol 0.18 · bgmSource vol 0.25 (AudioBank 상수)
#   bgm_ambient.wav 파일 RMS 0.0304 → 실효 0.0076
#   여기 0.024 → 실효 0.0043 = BGM 대비 약 -5 dB (음악이 위, 바람이 뒤)
# 참고: 사고 전 사인 드론은 파일 RMS 0.0471(실효 0.0085)로 BGM 과 거의 같은 크기였다.
TARGET_RMS = 0.024


def write_wav(path, samples, sr=SR):
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(b"".join(
            struct.pack("<h", max(-32768, min(32767, int(s * 32767)))) for s in samples))


def normalize_rms(samples, target_rms=TARGET_RMS, peak_ceiling=0.85):
    """**피크가 아니라 RMS 로** 맞춘다.

    2026-07-27 운영자 "bgm 자체가 없어진거야?" 의 원인이 여기였다.
    1차 버전은 피크 0.60 으로 맞췄는데, 광대역 노이즈는 사인 드론보다 크레스트
    팩터가 작아서 같은 피크라도 RMS 가 훨씬 크게 나온다.  그 결과 새 앰비언트가
    기존 대비 +7.6 dB, **BGM 보다 +8.5 dB** 커져 음악을 덮어버렸다.

    지속음 베드의 체감 크기를 결정하는 건 피크가 아니라 RMS 이므로 RMS 를 기준으로
    잡고, 피크는 클리핑 방지용 상한으로만 쓴다.
    """
    n = len(samples) or 1
    cur = math.sqrt(sum(s * s for s in samples) / n) or 1e-9
    g = target_rms / cur
    peak = (max(abs(s) for s in samples) or 1.0) * g
    if peak > peak_ceiling:                 # 클리핑 방지 (RMS 목표보다 우선)
        g *= peak_ceiling / peak
    return [s * g for s in samples]


def day_ambient():
    rnd = random.Random(SEED)
    n_total = int(SR * (DUR + XFADE))   # 꼬리 여분까지 생성 후 접는다
    out = [0.0] * n_total

    # --- 1. 바람 --------------------------------------------------------
    # 원폴 저역통과 2단 = 부드러운 저역 러mble.  대역통과는 (LP_hi - LP_lo) 근사.
    lp_low = 0.0      # ~420Hz
    lp_air1 = 0.0     # ~3kHz
    lp_air2 = 0.0     # ~1.2kHz  (air = lp_air1 - lp_air2 → 1.2~3kHz 밴드)
    a_low = math.exp(-2.0 * math.pi * 420.0 / SR)
    a_air1 = math.exp(-2.0 * math.pi * 3000.0 / SR)
    a_air2 = math.exp(-2.0 * math.pi * 1200.0 / SR)

    # 돌풍 LFO — 주기가 DUR 을 정수분할해야 루프에서 위상이 튀지 않는다.
    f_g1 = 2.0 / DUR   # 12초에 2주기
    f_g2 = 3.0 / DUR   # 12초에 3주기

    for i in range(n_total):
        t = i / SR
        white = rnd.uniform(-1.0, 1.0)
        lp_low = a_low * lp_low + (1.0 - a_low) * white
        lp_air1 = a_air1 * lp_air1 + (1.0 - a_air1) * white
        lp_air2 = a_air2 * lp_air2 + (1.0 - a_air2) * white
        air = lp_air1 - lp_air2

        gust = (0.62
                + 0.26 * math.sin(2 * math.pi * f_g1 * t)
                + 0.12 * math.sin(2 * math.pi * f_g2 * t + 1.7))
        out[i] = (lp_low * 2.6 + air * 0.9) * gust

    # --- 2. 새소리 ------------------------------------------------------
    # 처프 = 주파수가 위로 훑고 내려오는 짧은 정현 + 약간의 흔들림.  단독으로는
    # 톤이지만 60~180ms 로 짧고 드물어서 "삐" 로 인지되지 않는다 (지속음이 아님).
    n_birds = 9
    for _ in range(n_birds):
        start_t = rnd.uniform(BIRD_GUARD, DUR - BIRD_GUARD)
        dur = rnd.uniform(0.06, 0.18)
        f0 = rnd.uniform(2400.0, 3400.0)
        f1 = f0 * rnd.uniform(1.15, 1.55)
        amp = rnd.uniform(0.05, 0.11)
        warble_f = rnd.uniform(18.0, 34.0)
        n_c = int(dur * SR)
        s0 = int(start_t * SR)
        phase = 0.0
        for k in range(n_c):
            u = k / n_c
            # 위로 훑고 되돌아오는 아치형 스윕
            f = f0 + (f1 - f0) * math.sin(math.pi * u)
            f *= 1.0 + 0.02 * math.sin(2 * math.pi * warble_f * k / SR)
            phase += 2 * math.pi * f / SR
            env = math.sin(math.pi * u) ** 1.4
            idx = s0 + k
            if 0 <= idx < n_total:
                out[idx] += math.sin(phase) * env * amp

    # --- 3. 심리스 루프 -------------------------------------------------
    n = int(SR * DUR)
    nx = int(SR * XFADE)
    for k in range(nx):
        w = k / nx
        # 등파워 크로스페이드 (진폭 선형이면 겹침 구간이 죽는다)
        a = math.cos(0.5 * math.pi * w)
        b = math.sin(0.5 * math.pi * w)
        out[k] = out[k] * b + out[n + k] * a
    return normalize_rms(out[:n])


def main():
    out_dir = Path(__file__).resolve().parent
    print("Generating day ambient (wind + birds)...")
    s = day_ambient()
    p = out_dir / "ambient.wav"
    write_wav(p, s)
    with wave.open(str(p), "rb") as wf:
        print(f"  ambient.wav  {wf.getnframes()/wf.getframerate():.2f}s "
              f"{wf.getframerate()}Hz {wf.getnchannels()}ch — OK")
    print()
    print("검증: find_beeps 지표로 tonality 가 순음 임계(>60) 아래로 떨어지는지 확인할 것.")
    print("배선 변경 없음 — AudioBank.sfxAmbient (ambientSource, vol=0.18) 그대로.")


if __name__ == "__main__":
    main()
