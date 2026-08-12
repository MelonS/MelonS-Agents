# -*- coding: utf-8 -*-
"""_assetpaths.py — 스프라이트를 **있는 자리 전부에** 쓰는 규칙.  공용.

계기 (2026-08-02): 한국풍으로 새로 그린 스프라이트 4종(수면자리·전돌 바닥·서안·
돌담)이 `Sprites/` 에만 반영되고 `Resources/struct32/` 에는 7/26 자 옛 그림이 그대로
남아 있었다.  에디터에서는 새 그림이 보이고 **운영자가 실제로 플레이하는 exe·WebGL
에서는 옛 그림이 보인다** — 아트를 고쳤는데 "안 바뀌었다" 는 보고가 나오는 전형적인
경로다.  같은 이유로 한국풍 등잔은 게임에 한 번도 들어간 적이 없었다.

원인: 이 레포는 같은 스프라이트를 최대 세 곳에 둔다.
  · `Assets/Sprites/`            — 에디터(AssetDatabase)가 읽는다
  · `Assets/Resources/Sprites/`  — 런타임 `Resources.Load("Sprites/…")`
  · `Assets/Resources/struct32/` — 런타임 `Resources.Load("struct32/…")`
생성기마다 목적지를 손으로 적어 왔고, 하나라도 빠지면 조용히 갈라진다.

규칙: **목적지를 적지 않는다.  찾는다.**  이름이 같은 png 를 `Assets/` 전체에서
찾아 전부 덮어쓴다.  새 사본이 생겨도 자동으로 따라온다.
"""
from __future__ import annotations
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.normpath(os.path.join(HERE, ".."))

# 백업·검수 폴더는 건드리지 않는다 — 옛 아트를 보관하는 곳이라 덮으면 되돌릴
#  기준이 사라진다 (실제로 `_pre_kenney_backup/` 을 덮었다가 git 으로 복구했다).
SKIP_DIRS = ("_pre_kenney_backup", "_backup", "_old", "_old_sdxl", "_stage", "_preview")


def mirrors(name: str):
    """`Assets/` 전체에서 `<name>.png` 를 전부 찾는다 (백업 폴더 제외)."""
    hits = []
    for root, dirs, files in os.walk(ASSETS):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        if name + ".png" in files:
            hits.append(os.path.join(root, name + ".png"))
    return hits


def save_everywhere(img, name: str, world_tiles: float | None = None,
                    create_in: str | None = None) -> list:
    """이미지를 같은 이름의 모든 사본 자리에 저장한다.

    `world_tiles` 를 주면 `.meta` 의 PPU 도 함께 맞춘다 — 그림 크기와 월드 크기가
    한 곳에서 같이 정해져야 '그림만 바꿨는데 크기가 변했다' 가 안 생긴다.
    사본이 하나도 없으면 `create_in` 에 새로 만든다 (없으면 건너뛴다)."""
    paths = mirrors(name)
    if not paths and create_in:
        os.makedirs(create_in, exist_ok=True)
        paths = [os.path.join(create_in, name + ".png")]
    for p in paths:
        img.save(p)
        if world_tiles:
            set_ppu(p, world_tiles)      # 내용 bbox 기준
    return paths


def set_ppu(png_path: str, world_tiles: float, px: int | None = None) -> bool:
    """`.meta` 의 `spritePixelsToUnits` 만 고쳐 **보이는 그림**을 `world_tiles` 칸으로.

    기준은 캔버스가 아니라 **내용(불투명 픽셀의 bbox) 폭**이다.  캔버스로 재면
    여백이 큰 스프라이트가 의도보다 작아진다 — 실제로 석재를 0.85칸으로 맞췄는데
    128px 캔버스 안 내용이 92px 이라 화면에서는 0.61칸이었다.  플레이어가 보는 것은
    여백이 아니라 그림이므로, 크기는 그림으로 재야 한다.

    `px` 를 직접 주면 그 값을 쓴다 (타일처럼 캔버스 전체가 내용인 경우).
    guid 등 나머지 필드는 건드리지 않는다.  meta 가 없으면(아직 임포트 전) 아무 것도
    하지 않는다 — Unity 가 만들 때 기본값으로 들어오고, 다음 실행에서 맞춰진다."""
    meta = png_path + ".meta"
    if not os.path.exists(meta):
        return False
    if px is None:
        from PIL import Image
        bb = Image.open(png_path).convert("RGBA").getbbox()
        px = (bb[2] - bb[0]) if bb else Image.open(png_path).width
    ppu = round(px / float(world_tiles), 2)
    t = open(meta, "r", encoding="utf-8", errors="replace").read()
    t2, n = re.subn(r"spritePixelsToUnits:\s*[0-9.]+", f"spritePixelsToUnits: {ppu}", t)
    if n:
        open(meta, "w", encoding="utf-8", newline="\n").write(t2)
    return bool(n)


def rel(paths) -> str:
    """보고용 — `Assets/` 기준 상대 폴더 목록."""
    return ", ".join(sorted({os.path.relpath(os.path.dirname(p), ASSETS) for p in paths}))
