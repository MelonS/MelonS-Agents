#!/usr/bin/env python3
"""check-sprite-ppu.py — 베이크 블록이 등록한 PPU 와 실제 .meta 의 PPU 를 대조.

왜 필요한가: `SceneSetup.Game.Entities.cs` 의 임포트 블록은 **씬 베이크를 돌릴 때만**
실행된다.  일반 빌드는 커밋된 `.meta` 를 쓴다.  둘이 어긋나면 스프라이트가 의도와
다른 크기로 게임에 들어가는데, 코드도 게이트도 아무 말을 안 한다.

실측 사고 (2026-07-27 운영자 "베리인거 같은데 왜케 커?"):
  flora32_bush_berry.png = 128px, 베이크 등록 PPU 128 (→ 1칸)
  그런데 커밋된 .meta 는 PPU 32 → **4×4칸**.  나무(2칸)의 두 배 크기로 렌더됐다.

usage: python skills/game-prototype/scripts/check-sprite-ppu.py
exit 0 = 불일치 없음.
"""
from __future__ import annotations
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

REPO = Path(__file__).resolve().parents[3]
UP = REPO / "skills" / "game-prototype" / "unity-project"
EDITOR = UP / "Assets" / "Editor"

# ("Assets/....png", 128f) 형태의 등록을 전부 긁는다.
REG_RE = re.compile(r'\("(Assets/[^"]+\.png)"\s*,\s*([0-9]+(?:\.[0-9]+)?)f?\)')
PPU_RE = re.compile(r"spritePixelsToUnits:\s*([0-9]+(?:\.[0-9]+)?)")


def main() -> int:
    if not EDITOR.is_dir():
        print(f"Editor 폴더 없음: {EDITOR}")
        return 0

    regs: dict[str, tuple[float, str]] = {}
    for cs in sorted(EDITOR.rglob("*.cs")):
        text = cs.read_text(encoding="utf-8", errors="replace")
        for m in REG_RE.finditer(text):
            rel, ppu = m.group(1), float(m.group(2))
            regs.setdefault(rel, (ppu, cs.name))

    if not regs:
        print("PPU 등록을 못 찾음 — 베이크 블록 형식이 바뀌었는지 확인할 것.")
        return 0

    print(f"베이크 등록 {len(regs)}건 대조")
    bad: list[str] = []
    missing: list[str] = []
    for rel, (want, src) in sorted(regs.items()):
        png = UP / rel
        meta = UP / (rel + ".meta")
        if not png.is_file():
            continue                      # 미생성 절차 에셋 — 대조 대상 아님
        if not meta.is_file():
            missing.append(f"  ? {rel} — .meta 없음 (임포트 전)")
            continue
        m = PPU_RE.search(meta.read_text(encoding="utf-8", errors="replace"))
        if not m:
            missing.append(f"  ? {rel} — .meta 에 spritePixelsToUnits 없음")
            continue
        have = float(m.group(1))
        if abs(have - want) > 0.01:
            try:
                from PIL import Image
                w, h = Image.open(png).size
                cells_have = f"{w/have:.2f}×{h/have:.2f}칸"
                cells_want = f"{w/want:.2f}×{h/want:.2f}칸"
                detail = f"  실제 {cells_have} / 의도 {cells_want}"
            except Exception:
                detail = ""
            bad.append(f"  ✗ {rel}\n      .meta PPU {have:g} ≠ 등록 {want:g} ({src}){detail}")

    for s in missing:
        print(s)
    if bad:
        print("\n── PPU 불일치")
        for s in bad:
            print(s)
        print(f"\nFAIL — {len(bad)}건.  .meta 를 등록값에 맞추거나, 등록값이 틀렸다면 그쪽을 고칠 것.")
        print("       (베이크 블록은 씬 베이크 때만 돌고, 일반 빌드는 .meta 를 쓴다.)")
        return 1

    print("PASS — 등록 PPU 와 .meta 가 모두 일치.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
