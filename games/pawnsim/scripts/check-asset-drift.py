# -*- coding: utf-8 -*-
"""check-asset-drift.py — 같은 이름의 스프라이트 사본이 **갈라졌는지** 검사한다.

계기 (2026-08-02): 두 건이 동시에 터졌다.

  ① `Sprites/struct32_bed_spot.png` = 8/01 한국풍, `Resources/struct32/…` = 7/26 옛
     그림.  에디터에서는 새 그림이 보이고 **플레이어 빌드에서는 옛 그림이 보인다.**
     수면자리·전돌바닥·서안·돌담 4종이 이 상태였다 — 한국풍 리스킨의 일부가
     실제 게임에 들어가지 않은 채로 며칠 지나 있었다.
  ② `ts_wood_pile.png` 는 같은 파일인데 PPU 가 `Sprites/`=70, `Resources/`=160.
     런타임은 Resources 를 읽으므로 화면에서는 **0.31칸** — 운영자가 "목재는 왜케
     작게 표현되는거지 너무 작음" 이라고 지적한 것의 원인.

두 버그 모두 **에디터 스크린샷으로는 절대 안 보인다.**  사람 눈으로 잡을 수 없는
종류이므로 검사로 잡는다.

검출 항목:
  · STALE — 같은 이름인데 내용(md5)이 다르다.  한쪽만 갱신됐다는 뜻.
  · PPU   — 같은 이름인데 `.meta` 의 spritePixelsToUnits 가 다르다.  같은 그림이
            장소에 따라 다른 크기로 나온다는 뜻.

백업 폴더(`_pre_kenney_backup` 등)는 일부러 옛 그림을 보관하므로 제외한다.

usage: python check-asset-drift.py [--fix]
       --fix : 가장 최근에 수정된 사본으로 나머지를 덮는다 (PPU 는 손대지 않는다)
"""
from __future__ import annotations
import sys
import os
import re
import shutil
import hashlib
from collections import defaultdict

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.normpath(os.path.join(HERE, "..", "unity-project", "Assets"))
SKIP_DIRS = ("_pre_kenney_backup", "_backup", "_old", "_old_sdxl", "_stage", "_preview")


def ppu_of(png: str):
    meta = png + ".meta"
    if not os.path.exists(meta):
        return None
    t = open(meta, "r", encoding="utf-8", errors="replace").read()
    m = re.search(r"spritePixelsToUnits:\s*([0-9.]+)", t)
    return float(m.group(1)) if m else None


def collect():
    by_name = defaultdict(list)
    for root, dirs, files in os.walk(ASSETS):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for f in files:
            if f.endswith(".png"):
                by_name[f].append(os.path.join(root, f))
    return {k: v for k, v in by_name.items() if len(v) > 1}


def main() -> int:
    fix = "--fix" in sys.argv
    dupes = collect()
    stale, ppu_split = [], []

    for name, paths in sorted(dupes.items()):
        digests = {p: hashlib.md5(open(p, "rb").read()).hexdigest() for p in paths}
        if len(set(digests.values())) > 1:
            stale.append((name, paths))
        ppus = {p: ppu_of(p) for p in paths}
        vals = {v for v in ppus.values() if v is not None}
        if len(vals) > 1:
            ppu_split.append((name, ppus))

    def rel(p):
        return os.path.relpath(p, ASSETS)

    print(f"[asset-drift] 같은 이름 사본 {len(dupes)}종 검사")

    for name, paths in stale:
        newest = max(paths, key=os.path.getmtime)
        print(f"  STALE  {name}")
        for p in sorted(paths, key=os.path.getmtime, reverse=True):
            import datetime
            d = datetime.date.fromtimestamp(os.path.getmtime(p))
            mark = " ← 최신" if p == newest else ""
            print(f"           {d}  {rel(p)}{mark}")
        if fix:
            for p in paths:
                if p != newest:
                    shutil.copyfile(newest, p)
            print(f"           → 최신본으로 통일")

    for name, ppus in ppu_split:
        print(f"  PPU    {name}")
        for p, v in sorted(ppus.items()):
            print(f"           PPU={v}  {rel(p)}")
        print("           → 생성기의 월드 크기 표에서 한 값으로 정하고 "
              "_assetpaths.set_ppu 로 맞출 것")

    n = len(stale) + len(ppu_split)
    if n == 0:
        print("[asset-drift] OK — 갈라진 사본 없음")
        return 0
    print(f"[asset-drift] FAIL — 갈라짐 {n}건 "
          f"(내용 {len(stale)} / PPU {len(ppu_split)})")
    return 1


if __name__ == "__main__":
    sys.exit(main())
