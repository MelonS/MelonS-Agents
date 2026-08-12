#!/usr/bin/env python3
"""render-stats-card.py — README 최상단 "숫자로 보기" 카드를 다시 뽑는다.

이 카드는 저장소에서 가장 먼저 보이는 자산인데, 숫자가 픽셀에 박혀 있어서 조용히
낡는다.  2026-08-01 점검에서 세 개가 틀려 있었다: 서브에이전트 23(실제 27),
커밋 게이트 15시나리오(실제 22), 미션 타입 6종(실제 7).  본문 수치만 고치면
alt 텍스트가 이미지를 잘못 설명하게 되므로 이미지와 같이 갱신해야 한다.

숫자는 하드코딩하지 않고 **저장소에서 직접 센다** — 그래야 다음에 또 어긋나지
않는다.  셀 수 없는 것(출시 편수, 쉐이더 수)만 문서에서 읽거나 상수로 둔다.

    python scripts/render-stats-card.py          # docs/visuals/01-hero-stats{,-ko}.png
    python scripts/render-stats-card.py --print  # 렌더 없이 측정치만 출력
"""
from __future__ import annotations

import argparse
import pathlib
import re
import subprocess
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT = ROOT / "docs" / "visuals"
WIDTH = 1240

for _s in (sys.stdout, sys.stderr):
    if hasattr(_s, "reconfigure"):
        try:
            _s.reconfigure(encoding="utf-8")
        except (ValueError, OSError):
            pass


def measure() -> dict[str, str]:
    """저장소에서 직접 센 값.  못 세는 것만 문서에서 읽는다."""
    agents = len(list((ROOT / ".claude" / "agents").glob("*.md")))
    core = 6
    game = len(list((ROOT / ".claude" / "agents").glob("game-*.md"))) + 1  # + ta
    judges = len(list((ROOT / ".claude" / "agents").glob("*-judge.md")))
    content = agents - core - game - judges
    scen = len([p for p in (ROOT / "games" / "pawnsim" / "repro-scenarios").glob("*.json")
                if not p.name.startswith("_")])
    missions = len([p for p in (ROOT / "agents" / "missions").iterdir() if p.is_dir()])
    ref = (ROOT / "docs" / "music-video-pipeline-reference.md").read_text(encoding="utf-8")
    shaders = re.search(r"Shader catalog \((\d+) effects\)", ref)
    version = subprocess.run(["git", "tag", "--sort=-v:refname"], capture_output=True,
                             text=True, encoding="utf-8", errors="replace",
                             cwd=ROOT).stdout.splitlines()
    return {
        "agents": str(agents), "core": str(core), "game": str(game),
        "content": str(content), "judges": str(judges),
        "scenarios": str(scen), "missions": str(missions),
        "shaders": shaders.group(1) if shaders else "23",
        "version": version[0] if version else "v0.4.0",
    }


CSS = """
* { box-sizing: border-box; margin: 0; padding: 0; }
body { width: %(w)spx; background: #0B0E13; color: #E8EDF4;
  font-family: ui-sans-serif, -apple-system, "Segoe UI", Roboto, Arial, sans-serif;
  -webkit-font-smoothing: antialiased; }
.card { background: radial-gradient(120%% 100%% at 0%% 0%%, #151A22 0%%, #0D1117 55%%, #0B0E13 100%%);
  border: 1px solid #1E2632; border-radius: 18px; padding: 38px 42px 30px; margin: 20px; }
.top { display: flex; justify-content: space-between; align-items: flex-start; gap: 24px; }
.eyebrow { font-family: ui-monospace, Consolas, monospace; font-size: 11.5px; letter-spacing: .18em;
  text-transform: uppercase; color: #7C8B9E; display: flex; align-items: center; gap: 12px; }
.eyebrow::before { content: ""; width: 22px; height: 2px; background: #D97757; }
h1 { font-size: 44px; letter-spacing: -.025em; font-weight: 700; margin: 12px 0 0; }
h1 em { font-style: normal; color: #E9825C; }
.lede { margin-top: 10px; max-width: 66ch; font-size: 14.5px; line-height: 1.55; color: #A9B6C6; }
.lede b { color: #E8EDF4; font-weight: 600; }
.pills { display: flex; flex-direction: column; gap: 8px; flex: none; }
.pill { font-size: 12.5px; font-weight: 600; padding: 5px 12px; border-radius: 999px;
  border: 1px solid #2A3442; color: #C9D6E4; white-space: nowrap; display: flex; align-items: center; gap: 7px; }
.pill::before { content: ""; width: 6px; height: 6px; border-radius: 50%%; background: currentColor; }
.pill.a { color: #E9825C; border-color: #6B3E30; }
.pill.b { color: #B6ACFB; border-color: #493F86; }
.pill.c { color: #6FD3A0; border-color: #2C6249; }
.tiles { margin-top: 26px; display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; }
.tile { background: #111722; border: 1px solid #202936; border-radius: 12px; padding: 16px 16px 14px; }
.tile .v { font-size: 30px; font-weight: 700; letter-spacing: -.02em; line-height: 1.05; }
.tile .v small { font-size: 15px; font-weight: 600; color: #8D9CAF; }
.tile .k { margin-top: 7px; font-size: 13px; color: #C3D0DF; }
.tile .s { margin-top: 4px; font-family: ui-monospace, Consolas, monospace; font-size: 10.5px;
  color: #6F7F93; line-height: 1.5; }
.tile.orange .v { color: #E9825C; } .tile.green .v { color: #6FD3A0; }
.tile.violet .v { color: #B6ACFB; } .tile.plain .v { color: #E8EDF4; }
footer { margin-top: 24px; padding-top: 15px; border-top: 1px solid #1B2330; display: flex;
  justify-content: space-between; font-family: ui-monospace, Consolas, monospace;
  font-size: 11px; color: #67768A; }
footer b { color: #E9825C; }
"""


def card(m: dict[str, str], lang: str) -> str:
    ko = lang == "ko"
    tiles = [
        ("orange", "100", "+", "내놓은 미션 산출물" if ko else "mission outputs shipped",
         f"미션 타입 {m['missions']}종" if ko else f"across {m['missions']} mission types"),
        ("plain", "1", "+1+4", "프로덕션 스킬 · 메타 스킬 · 프로토타입" if ko else "production skill · meta-skill · prototypes",
         "music-video · content-shorts · game-dev-agent"),
        ("plain", m["shaders"], "", "ffmpeg 쉐이더" if ko else "ffmpeg shaders",
         "3 stages · genre-routed"),
        ("green", "0", "", "런타임 API 토큰" if ko else "runtime API tokens",
         "local: ffmpeg · whisper · ollama"),
        ("plain", m["scenarios"], "", "시나리오 커밋 게이트" if ko else "scenario commit gate",
         "input-level repro + assertions"),
        ("violet", m["agents"], "", "서브에이전트 정의" if ko else "subagent definitions",
         f"코어 {m['core']} + 게임 {m['game']} + 콘텐츠 {m['content']} + 심사위원 {m['judges']}" if ko
         else f"{m['core']} core + {m['game']} game + {m['content']} content + {m['judges']} judges"),
        ("plain", "3", "", "감사 트리거 레이어" if ko else "audit trigger layers",
         "commit · anomaly · schedule"),
        ("plain", "MIT", "", "라이선스 · EN + KO 이중 트랙" if ko else "license · EN + KO dual-track",
         f"{m['version']} · public"),
    ]
    tile_html = "".join(
        f'<div class="tile {c}"><div class="v">{v}<small>{sfx}</small></div>'
        f'<div class="k">{k}</div><div class="s">{s}</div></div>'
        for c, v, sfx, k, s in tiles)
    lede = ("<b>혼자서 Claude Code 로 만든 멀티 에이전트 시스템</b> — 음악을 숏폼 영상으로 뽑아내고, "
            "콜로니 심 게임을 만들어 직접 플레이하며 스스로 검증합니다. 런타임 API 비용은 0."
            if ko else
            "<b>A multi-agent system built solo with Claude Code</b> — it turns music into short-form "
            "video, and builds a colony-sim game it plays to verify itself, at zero runtime API cost.")
    pills = (("자율 실행", "스스로 검증", "런타임 비용 0") if ko
             else ("Autonomous", "Self-verifying", "Zero runtime cost"))
    foot = ("기계적인 일은 로컬로, 창의적인 일은 Claude 로" if ko
            else "local for the mechanical, Claude for the creative")
    return f"""<div class="card">
  <div class="top">
    <div>
      <div class="eyebrow">MULTI-AGENT SYSTEM · AGENTSKILLS.IO</div>
      <h1>MelonS<em>-Agents</em></h1>
      <p class="lede">{lede}</p>
    </div>
    <div class="pills"><span class="pill a">{pills[0]}</span>
      <span class="pill b">{pills[1]}</span><span class="pill c">{pills[2]}</span></div>
  </div>
  <div class="tiles">{tile_html}</div>
  <footer><span><b>◆</b> MelonS-Agents — {foot}</span><span>github.com/MelonS/MelonS-Agents</span></footer>
</div>"""


def chrome() -> str:
    base = pathlib.Path.home() / "AppData/Local/ms-playwright"
    for pat in ("chromium_headless_shell-*/chrome-headless-shell-*/chrome-headless-shell.exe",
                "chromium-*/chrome-win/chrome.exe"):
        for c in sorted(base.glob(pat)):
            return str(c)
    raise SystemExit("헤드리스 크로미움을 찾지 못했다")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--print", action="store_true", help="렌더 없이 측정치만 출력")
    args = ap.parse_args()
    m = measure()
    print("측정: " + " · ".join(f"{k}={v}" for k, v in m.items()))
    if args.print:
        return 0
    exe = chrome()
    for lang, name in (("en", "01-hero-stats.png"), ("ko", "01-hero-stats-ko.png")):
        tmp = OUT / f".{name}.html"
        tmp.write_text("<!doctype html><html><head><meta charset='utf-8'><style>"
                       + (CSS % {"w": WIDTH}) + "</style></head><body>" + card(m, lang) + "</body></html>",
                       encoding="utf-8")
        subprocess.run([exe, "--headless", "--disable-gpu", "--hide-scrollbars",
                        "--force-device-scale-factor=2", f"--window-size={WIDTH},900",
                        "--virtual-time-budget=4000", f"--screenshot={OUT/name}", tmp.as_uri()],
                       check=True, capture_output=True)
        tmp.unlink(missing_ok=True)
        try:
            from PIL import Image
            im = Image.open(OUT / name).convert("RGB")
            w, h = im.size
            bg = im.getpixel((w - 3, h - 3))
            last = h - 1
            while last > 0 and im.crop((0, last, w, last + 1)).getcolors(maxcolors=w) == [(w, bg)]:
                last -= 1
            im.crop((0, 0, w, min(h, last + 22))).save(OUT / name)
        except ModuleNotFoundError:
            pass
        print(f"  {name}  {(OUT/name).stat().st_size // 1024} KB")
    return 0


if __name__ == "__main__":
    sys.exit(main())
