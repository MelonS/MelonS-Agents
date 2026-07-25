#!/usr/bin/env python3
"""render-graph-art.py — 실행 그래프를 저장소 하우스 스타일 카드로 렌더한다.

왜 mermaid 가 아닌가: mermaid 는 위상은 정확히 그리지만 미적 통제권이 없다.
`docs/visuals/*.png` 는 전부 다크 에디토리얼 카드(모노 eyebrow · 오렌지 액센트 ·
라운드 패널)인데 README 안에서 mermaid 블록만 흰 배경 파스텔 상자로 튀었다
(운영자 지적, 2026-07-26).  그래서 레이아웃을 dagre 에 맡기지 않고 직접 잡는다.

낡지 않게 하는 방법은 그대로다: **단계 구성은 `graph/diagram.py` 의 레이아웃에서,
검증은 실행 중인 그래프에서** 온다.  코드에 노드가 늘고 배치되지 않으면
`_check_drift` 가 예외를 던져 렌더가 실패한다.

    python scripts/render-graph-art.py             # PNG 2종(ko/en) × 라인 2개
    python scripts/render-graph-art.py --open      # 렌더 후 결과 폴더 열기(로컬)

산출물: docs/visuals/15-graph-shorts{,-ko}.png · docs/visuals/16-graph-game{,-ko}.png
"""
from __future__ import annotations

import argparse
import html
import os
import pathlib
import shutil
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "docs" / "visuals"
WIDTH = 1240  # CSS px.  README 본문 폭(약 1000px)에서 축소율 ~80%면 카드 안 글씨가 읽힌다.
              # 1840px 로 넓게 뽑았더니 54%로 줄어 첫눈에 판독이 안 됐다(2026-07-26 실물 확인).

# 하우스 팔레트 — docs/visuals/01-hero-stats.png · 14-verification-loop.png 에서 추출
CSS = """
* { box-sizing: border-box; margin: 0; padding: 0; }
body {
  width: %(width)spx; background: #0B0E13;
  font-family: ui-sans-serif, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  color: #E8EDF4; -webkit-font-smoothing: antialiased;
}
.card {
  background: radial-gradient(120%% 100%% at 0%% 0%%, #151A22 0%%, #0D1117 55%%, #0B0E13 100%%);
  border: 1px solid #1E2632; border-radius: 18px; padding: 40px 44px 34px;
  margin: 22px; box-shadow: 0 30px 60px -40px #000;
}
.eyebrow {
  font-family: ui-monospace, "Cascadia Mono", Consolas, monospace;
  font-size: 12px; letter-spacing: .18em; text-transform: uppercase; color: #7C8B9E;
  display: flex; align-items: center; gap: 12px;
}
.eyebrow::before { content: ""; width: 22px; height: 2px; background: #D97757; }
h1 { font-size: 40px; line-height: 1.12; letter-spacing: -.02em; font-weight: 700; margin: 14px 0 0; }
h1 em { font-style: normal; color: #E9825C; }
.lede { margin-top: 12px; max-width: 78ch; font-size: 16.5px; line-height: 1.55; color: #A9B6C6; }
.lede b { color: #E8EDF4; font-weight: 600; }

/* 비용 막대 — 왜 문이 여기 서는지 한 눈에 */
.bar-wrap { margin-top: 26px; }
.bar { display: flex; height: 42px; border-radius: 8px; overflow: hidden; border: 1px solid #232C39; }
.bar > div { display: flex; align-items: center; justify-content: center;
  font-family: ui-monospace, Consolas, monospace; font-size: 12px; color: #0B0E13; font-weight: 600; }
.s-plan  { flex: 3;   background: #2C4257; color: #9FB4C9 !important; }
.s-still { flex: 4;   background: #3E6C8F; color: #DCEAF6 !important; }
.s-gate  { flex: 0 0 4px; background: #E9825C; position: relative; overflow: visible; }
.s-gate::after { content: attr(data-label); position: absolute; bottom: calc(100%% + 6px); left: 50%%;
  transform: translateX(-50%%); font-family: ui-monospace, Consolas, monospace; font-size: 11px;
  letter-spacing: .08em; color: #E9825C; white-space: nowrap; }
.bar { overflow: visible; }
.s-i2v   { flex: 179; background: linear-gradient(90deg, #8C3A32, #B4483C); color: #FBE3DE !important; }
.s-asm   { flex: 7;   background: #6B5A2A; color: #F2E4C4 !important; }
.bar-legend { display: flex; gap: 26px; margin-top: 12px; flex-wrap: wrap;
  font-size: 13px; color: #8D9CAF; }
.bar-legend b { color: #C9D6E4; font-weight: 600; }
.gate-tip { margin-top: 14px; padding: 12px 16px; border-left: 3px solid #E9825C;
  background: #17141400; background: rgba(233,119,92,.08); border-radius: 0 8px 8px 0;
  font-size: 14.5px; color: #C7B0A6; }
.gate-tip b { color: #F4C4B2; }

/* 흐름 */
.flow { display: flex; align-items: stretch; gap: 0; margin-top: 22px; }
.flow + .flow { margin-top: 12px; }
.flow .wrapmark { flex: 0 0 34px; display: flex; align-items: center; justify-content: center;
  color: #E9825C; font-size: 15px; }
.step { flex: 1 1 0; min-width: 0; padding: 16px 14px 14px; border-radius: 12px;
  background: #111722; border: 1px solid #202936; }
.step .nid { font-family: ui-monospace, Consolas, monospace; font-size: 12px;
  color: #6F7F93; letter-spacing: .04em; }
.step .nm { margin-top: 6px; font-size: 16.5px; font-weight: 650; letter-spacing: -.01em; }
.step .sub { margin-top: 5px; font-size: 13.5px; line-height: 1.45; color: #93A2B5; }
.step.gate { background: rgba(233,119,92,.10); border-color: #7A4433; }
.step.gate .nm { color: #F0A385; }
.step.human { background: rgba(139,124,246,.10); border-color: #493F86; }
.step.human .nm { color: #B6ACFB; }
.step.done { background: rgba(52,168,110,.10); border-color: #2C6249; }
.step.done .nm { color: #6FD3A0; }
.arrow { flex: 0 0 34px; display: flex; align-items: center; justify-content: center;
  color: #46566B; font-size: 17px; }
.badge { display: inline-block; margin-top: 8px; padding: 2px 7px; border-radius: 5px;
  font-family: ui-monospace, Consolas, monospace; font-size: 10.5px; letter-spacing: .06em;
  border: 1px solid #2A3442; color: #94A4B8; }
.step.gate .badge { border-color: #7A4433; color: #F0A385; }

/* 되돌아가는 엣지 · 차단 레일 */
.rails { margin-top: 18px; display: grid; grid-template-columns: repeat(auto-fit, minmax(230px, 1fr)); gap: 12px; }
.rail { padding: 12px 14px; border-radius: 10px; background: #10151E; border: 1px solid #1F2836;
  font-size: 13px; color: #9BAABD; display: flex; gap: 10px; align-items: flex-start; }
.rail .tag { font-family: ui-monospace, Consolas, monospace; font-size: 10px; letter-spacing: .08em;
  padding: 2px 6px; border-radius: 4px; flex: none; margin-top: 1px; font-weight: 700; }
.rail.retry .tag { background: rgba(148,163,184,.16); color: #C3D0DF; }
.rail.stop  .tag { background: rgba(219,89,72,.16);  color: #F0A79A; }
.rail b { color: #D6E1EE; font-weight: 600; }
.rail code { font-family: ui-monospace, Consolas, monospace; font-size: 12px; color: #C7D3E1; }

footer { margin-top: 26px; padding-top: 16px; border-top: 1px solid #1B2330;
  display: flex; justify-content: space-between; align-items: baseline;
  font-family: ui-monospace, Consolas, monospace; font-size: 11.5px; color: #67768A; }
footer .ids { color: #55647A; }
footer .brand b { color: #E9825C; }
"""


def _esc(s: str) -> str:
    return html.escape(s, quote=False)


def _step(nid: str, name: str, sub: str, kind: str = "", badge: str = "") -> str:
    cls = f"step {kind}".strip()
    badge_html = f'<span class="badge">{_esc(badge)}</span>' if badge else ""
    return (f'<div class="{cls}"><div class="nid">{_esc(nid)}</div>'
            f'<div class="nm">{_esc(name)}</div>'
            f'<div class="sub">{_esc(sub)}</div>{badge_html}</div>')


ARROW = '<div class="arrow">→</div>'


def _rows(steps: list[str], per_row: int = 4) -> str:
    """단계를 per_row 개씩 나눠 여러 .flow 행으로 — 한 줄로 늘이면 카드가 넓어져
    README 폭에서 축소되고 글씨가 작아진다.  행 끝에는 이어짐 표시를 둔다."""
    out = []
    for i in range(0, len(steps), per_row):
        chunk = steps[i:i + per_row]
        row = ARROW.join(chunk)
        if i + per_row < len(steps):
            row += '<div class="wrapmark">↴</div>'
        out.append(f'<div class="flow">{row}</div>')
    return "".join(out)


def shorts_html(lang: str) -> str:
    ko = lang == "ko"
    steps = [
        _step("plan", "샷 스펙" if ko else "Shot spec", "기획 → 컷 목록" if ko else "brief → cut list"),
        _step("render_shot ×N", "스틸 라운드" if ko else "Still round",
              "생성 → 심사 → 재생성" if ko else "generate → judge → regen", badge="9.0s / 장" if ko else "9.0s each"),
        _step("gate", "문 1" if ko else "Gate 1",
              "전 샷 75점 이상" if ko else "every still ≥ 75", kind="gate",
              badge="3시간 차단" if ko else "cuts 3 hours"),
        _step("storyboard · approval", "사람 승인" if ko else "Human approval",
              "검수 시트 → 승인·재생성·취소" if ko else "sheet → approve / regen / reject",
              kind="human", badge="interrupt()"),
        _step("render_clip ×N", "컷 라운드" if ko else "Clip round",
              "영상화 → 심사 → 시드 리롤" if ko else "i2v → judge → reroll",
              badge="412.3s / 컷" if ko else "412.3s each"),
        _step("clip_gate", "문 2" if ko else "Gate 2",
              "REGEN 컷 0" if ko else "no cut at REGEN", kind="gate"),
        _step("assemble · legal", "조립 · 법률" if ko else "Assemble · legal",
              "concat + 고지 → 라이선스 게이트" if ko else "concat + disclosure → license gate"),
        _step("release", "출시 패키지" if ko else "Release package",
              "제목·태그·썸네일·귀속" if ko else "titles · tags · thumbnail", kind="done"),
    ]
    flow = _rows(steps, 4)
    if ko:
        eyebrow = "실행 그래프 · 쇼츠 라인 · LANGGRAPH"
        title = 'GPU 3시간 앞에 <em>문</em>을 세운다'
        lede = ("한 편 실측 507초 중 <b>영상화가 412.3초(81%)</b>다. 스틸 한 장은 10초. "
                "그래서 싼 단계가 심사와 재시도를 안고, <b>문 1</b>이 기준 미달 스틸에 영상화 시간을 "
                "쓰지 못하게 막는다. 한 번 막을 때마다 <b>179분</b>이 남는다.")
        legend = [("기획·대본", "3분"), ("스틸 26장", "4분"), ("영상화 26컷", "179분"), ("조립·자막", "7분")]
        tip = ("<b>문의 자리는 스틸 직후.</b> 스틸 1장 10초 대 컷 1개 412초 — 비용비 1:40. "
               "문서에 적어 두면 잊히지만, 엣지가 열리지 않으면 잊을 수가 없다.")
        rails = [("retry", "되돌아가는 엣지 4개",
                  "스틸 재시도(최대 3) · 승인에서 지목한 재생성 · 시드 리롤 · 법률 REVISE(최대 2)"),
                 ("stop", "차단 = 절약",
                  "어느 문에서 막히든 <code>exit 2</code>. 영상화 전에 막히면 179분을 아직 안 쓴 상태다"),
                 ("retry", "이어서 하기",
                  "<code>resume --approve</code> 는 체크포인트에서 재개 — 26컷 중 19컷에서 죽어도 남은 7컷만")]
        foot_l = "plan · render_shot · gate · storyboard · approval · mark_regen · video_stage · render_clip · clip_gate · assemble · legal · bump_legal · release · blocked"
        foot_r = "graph/shorts_graph.py — 이 그림은 코드에서 생성된다"
    else:
        eyebrow = "EXECUTION GRAPH · SHORTS LINE · LANGGRAPH"
        title = 'A <em>gate</em> in front of three GPU hours'
        lede = ("Measured over a full run of 507 s, <b>video is 412.3 s of it (81%)</b>; a still costs 10 s. "
                "So the cheap stage carries the judging and the retries, and <b>Gate 1</b> refuses to spend "
                "video time on stills that never cleared the bar. Each block leaves <b>179 minutes</b> unspent.")
        legend = [("brief & script", "3 min"), ("26 stills", "4 min"), ("26 cuts rendered", "179 min"), ("assembly", "7 min")]
        tip = ("<b>The gate belongs right after the stills.</b> One still 10 s versus one cut 412 s — a 1:40 ratio. "
               "A rule in prose gets skipped; an edge that will not open cannot be.")
        rails = [("retry", "Four backward edges",
                  "still retry (max 3) · operator-marked regen · seed reroll · legal REVISE (max 2)"),
                 ("stop", "Blocking is the saving",
                  "any gate can end the run with <code>exit 2</code> — blocked before video means the 179 minutes were never spent"),
                 ("retry", "Resume",
                  "<code>resume --approve</code> continues from the checkpoint — dying on cut 19 of 26 costs the remaining seven")]
        foot_l = "plan · render_shot · gate · storyboard · approval · mark_regen · video_stage · render_clip · clip_gate · assemble · legal · bump_legal · release · blocked"
        foot_r = "graph/shorts_graph.py — this figure is generated from the code"

    gate_label = "문" if ko else "gate"
    legend_html = "".join(
        f'<div><b>{_esc(v)}</b> &nbsp;{_esc(k)}</div>' for k, v in legend)
    rails_html = "".join(
        f'<div class="rail {c}"><span class="tag">{"RETRY" if c == "retry" else "STOP"}</span>'
        f'<span><b>{_esc(t)}</b><br/>{d}</span></div>' for c, t, d in rails)
    return f"""<div class="card">
  <div class="eyebrow">{_esc(eyebrow)}</div>
  <h1>{title}</h1>
  <p class="lede">{lede}</p>
  <div class="bar-wrap">
    <div class="bar">
      <div class="s-plan"></div><div class="s-still"></div><div class="s-gate" data-label="{gate_label}"></div>
      <div class="s-i2v">{'영상화 179분 · 전체의 81%' if ko else 'video 179 min · 81% of the run'}</div>
      <div class="s-asm"></div>
    </div>
    <div class="bar-legend">{legend_html}</div>
    <div class="gate-tip">{tip}</div>
  </div>
  {flow}
  <div class="rails">{rails_html}</div>
  <footer><span class="ids">{_esc(foot_l)}</span></footer>
  <footer style="border:0;padding-top:6px"><span class="brand"><b>◆</b> MelonS-Agents</span><span>{_esc(foot_r)}</span></footer>
</div>"""


def game_html(lang: str) -> str:
    ko = lang == "ko"
    steps = [
        _step("pm_publish", "작업 발행" if ko else "Publish task", "레인 3개 오픈" if ko else "open three lanes"),
        _step("review", "검토" if ko else "Review",
              "Director · Designer · AI Designer", kind="gate"),
        _step("work_lane ×3", "제작 병렬" if ko else "Parallel lanes",
              "코드 · 아트 · 사운드" if ko else "code · art · sound", badge="fan-out"),
        _step("unity_scene · unity_build", "Unity 배타 구간" if ko else "Unity critical section",
              "씬 생성 → 빌드, 산출물 경로를 상태에 확정" if ko else "scene → build, paths pinned into state",
              kind="human", badge="🔒 mutex"),
        _step("qa", "실물 검증" if ko else "Verify on the build",
              "exe 실행 · 스크린샷 — 상태의 경로만 읽는다" if ko else "launch exe · screenshot — pinned paths only"),
        _step("ta", "아트 심사" if ko else "Art review",
              "TA 가 품질을 채점하고 수정 지시" if ko else "TA scores quality, returns a fix list", kind="gate"),
        _step("pm_merge", "병합" if ko else "Merge",
              "리듀서가 상태를 합친다" if ko else "reducer merges state", kind="done"),
    ]
    flow = _rows(steps, 4)
    if ko:
        eyebrow = "실행 그래프 · 게임 라인 · LANGGRAPH"
        title = '같은 fan-out, 문이 아니라 <em>뮤텍스</em>'
        lede = ("게임 라인의 병목은 시간이 아니라 <b>배타 자원</b>과 <b>거짓 검증</b>이다. "
                "Unity 는 두 레인이 동시에 몰 수 없어서, 병렬 제작 레인이 문이 아니라 뮤텍스에서 합류한다.")
        rails = [("stop", "거짓 검증을 구조로 막는다",
                  "빌드 폴더가 날짜 스탬프라 자정을 넘기면 어제 빌드를 열고 \"고쳤다\"가 된다. "
                  "<code>unity_build</code> 가 경로를 상태에 확정하고 <code>qa</code> 는 그 경로만 읽는다"),
                 ("retry", "되돌아가는 엣지",
                  "빌드 실패·TA 미달 → <code>fix</code>(최대 3회) → 배타 구간 재진입"),
                 ("stop", "상한 소진",
                  "회차를 다 쓰면 <code>blocked</code> 로 끝나고 블로커가 기록된다")]
        foot_r = "graph/game_graph.py — 이 그림은 코드에서 생성된다"
    else:
        eyebrow = "EXECUTION GRAPH · GAME LINE · LANGGRAPH"
        title = 'Same fan-out, a <em>mutex</em> instead of a gate'
        lede = ("Here the bottleneck is not time but an <b>exclusive resource</b> and <b>false verification</b>. "
                "Unity cannot be driven by two lanes at once, so the parallel lanes converge on a mutex.")
        rails = [("stop", "False verification, closed structurally",
                  "build folders are date-stamped, so past midnight you open yesterday's build and call it fixed. "
                  "<code>unity_build</code> pins the paths into state and <code>qa</code> reads only those"),
                 ("retry", "Backward edges",
                  "build failure or TA below bar → <code>fix</code> (max 3) → back into the critical section"),
                 ("stop", "Rounds spent",
                  "when the retries run out the run ends at <code>blocked</code> with a logged blocker")]
        foot_r = "graph/game_graph.py — this figure is generated from the code"
    foot_l = "pm_publish · review · work_lane · unity_scene · unity_build · qa · ta · fix · pm_merge · blocked"
    rails_html = "".join(
        f'<div class="rail {c}"><span class="tag">{"RETRY" if c == "retry" else "STOP"}</span>'
        f'<span><b>{_esc(t)}</b><br/>{d}</span></div>' for c, t, d in rails)
    return f"""<div class="card">
  <div class="eyebrow">{_esc(eyebrow)}</div>
  <h1>{title}</h1>
  <p class="lede">{lede}</p>
  {flow}
  <div class="rails">{rails_html}</div>
  <footer><span class="ids">{_esc(foot_l)}</span></footer>
  <footer style="border:0;padding-top:6px"><span class="brand"><b>◆</b> MelonS-Agents</span><span>{_esc(foot_r)}</span></footer>
</div>"""


def _check_live_graphs() -> None:
    """그림이 코드와 어긋나면 렌더 자체를 실패시킨다 (graph/diagram.py 와 같은 가드)."""
    sys.path.insert(0, str(ROOT))
    from graph import diagram
    from graph.game_graph import build_game_graph
    from graph.shorts_graph import build_shorts_graph

    diagram._check_drift(build_shorts_graph().get_graph(), diagram.SHORTS)
    diagram._check_drift(build_game_graph().get_graph(), diagram.GAME)


def _chrome() -> str:
    env = os.environ.get("CHROME_HEADLESS_SHELL")
    if env and pathlib.Path(env).exists():
        return env
    for pat in ("chrome-headless-shell", "chrome", "chromium", "msedge"):
        found = shutil.which(pat)
        if found:
            return found
    base = pathlib.Path.home() / "AppData/Local/ms-playwright"
    if base.exists():
        for cand in sorted(base.glob("chromium_headless_shell-*/chrome-headless-shell-*/chrome-headless-shell.exe")):
            return str(cand)
        for cand in sorted(base.glob("chromium-*/chrome-win/chrome.exe")):
            return str(cand)
    raise SystemExit("헤드리스 크로미움을 찾지 못했다 — CHROME_HEADLESS_SHELL 로 경로를 주거나 "
                     "playwright chromium 을 설치하라")


def render(name: str, body: str, out: pathlib.Path, chrome: str) -> None:
    tmp = out.parent / f".{out.stem}.html"
    tmp.write_text(
        "<!doctype html><html><head><meta charset='utf-8'><style>"
        + (CSS % {"width": WIDTH}) + "</style></head><body>" + body + "</body></html>",
        encoding="utf-8")
    try:
        subprocess.run(
            [chrome, "--headless", "--disable-gpu", "--hide-scrollbars",
             "--force-device-scale-factor=2", f"--window-size={WIDTH},1200",
             "--default-background-color=00000000", "--virtual-time-budget=4000",
             f"--screenshot={out}", tmp.as_uri()],
            check=True, capture_output=True)
    finally:
        tmp.unlink(missing_ok=True)
    _trim(out)
    print(f"  {out.relative_to(ROOT)}  {out.stat().st_size // 1024} KB")


def _trim(png: pathlib.Path) -> None:
    """카드 아래 빈 배경을 잘라낸다 — 창 높이를 미리 알 수 없어 넉넉히 찍고 자른다."""
    try:
        from PIL import Image
    except ModuleNotFoundError:
        return
    im = Image.open(png).convert("RGB")
    w, h = im.size
    bg = im.getpixel((w - 3, h - 3))
    last = h - 1
    while last > 0:
        row = im.crop((0, last, w, last + 1)).getcolors(maxcolors=w)
        if not (len(row) == 1 and row[0][1] == bg):
            break
        last -= 1
    if last < h - 8:
        im.crop((0, 0, w, min(h, last + 24))).save(png)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", choices=("shorts", "game"), help="한 라인만 렌더")
    args = ap.parse_args()

    _check_live_graphs()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    chrome = _chrome()
    jobs = []
    if args.only in (None, "shorts"):
        jobs += [("15-graph-shorts.png", shorts_html("en")), ("15-graph-shorts-ko.png", shorts_html("ko"))]
    if args.only in (None, "game"):
        jobs += [("16-graph-game.png", game_html("en")), ("16-graph-game-ko.png", game_html("ko"))]
    print("[graph-art] 렌더:")
    for fname, body in jobs:
        render(fname, body, OUT_DIR / fname, chrome)
    return 0


if __name__ == "__main__":
    sys.exit(main())
