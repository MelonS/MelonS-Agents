#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""gen-sfx-elevenlabs.py — 액션 SFX 를 ElevenLabs Sound Effects 로 세대교체.

승인: 운영자 2026-07-24 "사운드 일레븐랩스가 있으니 그걸로 바꿔보자 퀄리티업이
      된다면" (EL 유료 크레딧 사용 승인, lookfeel-round L8).

설계 원칙 — 앞선 사고에서 배운 것을 그대로 적용한다:

  1. **파일명 동일 드롭인.**  AudioBank 슬롯을 건드리지 않으므로 코드 변경 0.

  2. **기존 파일의 RMS 에 맞춘다.**  이게 제일 중요하다.  2026-07-27 에 앰비언트를
     피크 정규화로 교체했다가 BGM 보다 +8.5 dB 로 튀어 운영자가 "bgm 자체가
     없어진거야?" 라고 물은 사고가 있었다.  믹스 밸런스는 이미 튜닝돼 있으므로
     **새 소리를 옛 소리의 음량에 맞추는** 방향이 안전하다.

  3. **한 SFX 당 1회 생성.**  크레딧 절약 (lookfeel L8 절차).  실패분만 재생성.

  4. **원본 백업 + 원클릭 롤백.**  최종 청취 판정은 운영자만 할 수 있다
     (에이전트는 소리를 못 듣는다).  --rollback 으로 즉시 되돌린다.

  5. **앰비언트·BGM 은 대상 아님.**  ambient/ambient_night/rain/danger 는 길이가
     길고 RMS 가 BGM 대비로 정밀 튜닝돼 있다.  bgm_ambient 는 이미 EL Music 산출물.
     건드리면 위 1번 사고를 반복한다 — 액션 SFX 만 바꾼다.

사용:
  python skills/game-prototype/scripts/gen-sfx-elevenlabs.py --dry-run   # 계획만
  python skills/game-prototype/scripts/gen-sfx-elevenlabs.py             # 생성
  python skills/game-prototype/scripts/gen-sfx-elevenlabs.py --only chop
  python skills/game-prototype/scripts/gen-sfx-elevenlabs.py --rollback  # 원복
"""
from __future__ import annotations
import argparse
import math
import os
import shutil
import struct
import subprocess
import sys
import wave
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
AUDIO = ROOT / "unity-project" / "Assets" / "Audio"
BACKUP = AUDIO / "_procedural_backup"
TMP = Path(os.environ.get("TEMP", "/tmp")) / "pawnsim_sfx"
EL = REPO / "scripts" / "elevenlabs-sfx.py"
FFMPEG = os.environ.get("FFMPEG_BIN", "ffmpeg")

# 프롬프트는 **게임 안에서 무엇이 일어나는 소리인지**로 쓴다.  악기·장르어를 쓰면
#  음악처럼 나온다.  길이는 기존 파일과 맞춰 리듬(쿨다운·throttle)을 보존한다.
SFX = {
    "chop":      ("axe chopping into a tree trunk, single solid wood impact, short dry thud with splinter crack", 0.8),
    "treefall":  ("large tree falling and crashing to forest floor, wood snap then heavy leafy impact", 1.5),
    "mine":      ("pickaxe striking stone, sharp metallic chink with small rock debris", 0.7),
    "rockbreak": ("stone block breaking apart, rubble collapsing, dry crumbling rock", 1.0),
    "build":     ("hammering a wooden plank into place, two quick hammer strikes on wood", 0.8),
    "door":      ("small wooden door opening on old hinges, short creak and latch click", 0.8),
    "eat":       ("person taking a bite of food, soft chewing, single quick bite", 0.8),
    "cook":      ("food sizzling in a pan over a fire, short burst of sizzle", 1.0),
    "harvest":   ("pulling a plant from soil, leaves rustle and roots tear free", 0.7),
    "hit":       ("blunt melee impact on leather armor, dull heavy thud", 0.6),
    "shoot":     ("wooden bow releasing an arrow, string twang and arrow whoosh", 0.7),
    "research":  ("quill writing on parchment then a small satisfied chime, study desk", 1.0),
    "alert":     ("short warning horn call, two rising notes, medieval watchtower", 1.2),
    "wolf_howl": ("distant wolf howling at night in a forest, single mournful howl", 2.5),
}
# 제외 (위 원칙 5): ambient, ambient_night, rain, danger, bgm_ambient,
#                   footstep, select  — 아래 주석 참조.
#  footstep/select 는 **매우 자주** 재생된다(발소리는 걸음마다).  리치 샘플로 바꾸면
#  반복 피로가 급증하고, 과거 "per-frame buzz" 사고 계열로 돌아간다.  절차 생성의
#  짧고 건조한 소리가 이 용도엔 오히려 맞다 — 의도적으로 유지한다.


def wav_stats(p: Path):
    with wave.open(str(p)) as w:
        n, sw, ch, sr = w.getnframes(), w.getsampwidth(), w.getnchannels(), w.getframerate()
        raw = w.readframes(n)
    if sw != 2:
        return None
    cnt = len(raw) // 2
    vals = struct.unpack("<%dh" % cnt, raw[: cnt * 2])
    if not vals:
        return None
    rms = math.sqrt(sum(v * v for v in vals) / len(vals)) / 32768.0
    peak = max(abs(v) for v in vals) / 32768.0
    return dict(sec=n / sr, sr=sr, ch=ch, rms=rms, peak=peak)


def to_wav(src: Path, dst: Path, sr=44100, ch=1):
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-i", str(src),
                    "-ar", str(sr), "-ac", str(ch), "-c:a", "pcm_s16le", str(dst)],
                   check=True)


def normalize_to(p: Path, target_rms: float, peak_ceiling=0.95):
    """RMS 를 target 에 맞춘다.  피크가 천장을 넘으면 그만큼만 눌러 클리핑을 막는다."""
    with wave.open(str(p)) as w:
        params = w.getparams()
        raw = w.readframes(w.getnframes())
    cnt = len(raw) // 2
    vals = list(struct.unpack("<%dh" % cnt, raw[: cnt * 2]))
    if not vals:
        return
    cur = math.sqrt(sum(v * v for v in vals) / len(vals)) / 32768.0
    if cur <= 1e-9:
        return
    g = target_rms / cur
    pk = max(abs(v) for v in vals) / 32768.0
    if pk * g > peak_ceiling:
        g = peak_ceiling / pk
    out = [max(-32768, min(32767, int(v * g))) for v in vals]
    with wave.open(str(p), "wb") as w:
        w.setparams(params)
        w.writeframes(struct.pack("<%dh" % len(out), *out))


def rollback() -> int:
    if not BACKUP.is_dir():
        print("백업 폴더가 없다 — 되돌릴 것이 없음.")
        return 1
    n = 0
    for b in sorted(BACKUP.glob("*.wav")):
        shutil.copy2(b, AUDIO / b.name)
        n += 1
        print(f"  복원 {b.name}")
    print(f"\n{n}개 원복 완료.  씬 재생성/재빌드 필요.")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", help="이 이름 하나만")
    ap.add_argument("--dry-run", action="store_true", help="생성 없이 계획만 출력")
    ap.add_argument("--rollback", action="store_true")
    a = ap.parse_args()

    if a.rollback:
        return rollback()

    targets = {a.only: SFX[a.only]} if a.only else SFX
    if a.only and a.only not in SFX:
        print(f"모르는 SFX: {a.only}  (가능: {', '.join(sorted(SFX))})")
        return 2

    TMP.mkdir(parents=True, exist_ok=True)
    BACKUP.mkdir(parents=True, exist_ok=True)

    print(f"대상 {len(targets)}종 · 기존 RMS 에 맞춰 정규화 · 원본은 {BACKUP.name}/ 에 보존\n")
    ok, fail = 0, []
    for name, (prompt, dur) in sorted(targets.items()):
        dst = AUDIO / f"{name}.wav"
        if not dst.exists():
            print(f"  ✗ {name}: 원본 없음 — 건너뜀")
            continue
        before = wav_stats(dst)
        if a.dry_run:
            print(f"  · {name:10s} {before['sec']:.2f}s rms {before['rms']:.4f}"
                  f"  ← \"{prompt[:52]}…\"")
            continue

        if not (BACKUP / dst.name).exists():
            shutil.copy2(dst, BACKUP / dst.name)

        mp3 = TMP / f"{name}.mp3"
        r = subprocess.run([sys.executable, str(EL), prompt, str(dur), str(mp3)],
                           capture_output=True, text=True)
        if r.returncode != 0 or not mp3.exists():
            print(f"  ✗ {name}: 생성 실패 — {r.stdout.strip() or r.stderr.strip()[:120]}")
            fail.append(name)
            continue

        tmpwav = TMP / f"{name}.wav"
        try:
            to_wav(mp3, tmpwav, sr=before["sr"], ch=before["ch"])
        except subprocess.CalledProcessError as e:
            print(f"  ✗ {name}: 변환 실패 {e}")
            fail.append(name)
            continue

        shutil.copy2(tmpwav, dst)
        normalize_to(dst, before["rms"])
        after = wav_stats(dst)
        print(f"  ✔ {name:10s} {before['sec']:.2f}s→{after['sec']:.2f}s  "
              f"rms {before['rms']:.4f}→{after['rms']:.4f}  peak {after['peak']:.2f}")
        ok += 1

    if a.dry_run:
        print("\n--dry-run — 생성하지 않음.")
        return 0
    print(f"\n생성 {ok}종" + (f" · 실패 {len(fail)}종: {', '.join(fail)}" if fail else ""))
    print("최종 청취 판정은 운영자.  마음에 안 들면 --rollback 으로 즉시 원복.")
    return 1 if fail else 0


if __name__ == "__main__":
    raise SystemExit(main())
