"""README용 구조도 — 위상은 실행 중인 그래프에서, 라벨·모양·색은 여기서.

`get_graph().draw_mermaid()` 는 정확하지만 README 에 붙이기엔 나쁘다: 엣지가
알파벳순이라 흐름대로 읽히지 않고, `__start__`/`<p>`/`&nbsp;` 노이즈가 그대로
나오고, 어느 노드가 문(gate)이고 어디서 사람이 멈추는지 구분되지 않는다.

그래서 이 모듈은 **위상(노드·엣지)을 실행 중인 그래프에서 그대로 가져오고**
(엣지를 손으로 적지 않는다) 라벨·모양·색만 입힌다.  코드에 노드가 하나 늘고
배치되지 않으면 `RuntimeError` 로 생성이 실패한다 — 구조도가 조용히 낡는 경로를
닫아 둔 장치다.

    python -m graph.shorts_graph diagram --compact            # 한국어
    python -m graph.shorts_graph diagram --compact --lang en
    python scripts/sync-readme-graph.py                       # README 에 주입
"""

from __future__ import annotations

from dataclasses import dataclass, field

# 노드 종류 — 의미가 다르면 모양과 색이 달라야 한다.
STEP = "step"      # 평범한 작업 노드
GATE = "gate"      # 문 · 심사 — 여기서 뒤 단계 비용이 잘린다
MUTEX = "mutex"    # 배타 자원 구간 — 병렬이 여기서 직렬로 합류한다
HUMAN = "human"    # 사람이 멈춰 서는 지점 (interrupt)
RETRY = "retry"    # 회차 카운터 — 되돌아가는 화살표의 출발점
DONE = "done"      # 정상 종료
STOP = "stop"      # 차단 종료

_SHAPES = {
    STEP: '{nid}["{label}"]',
    GATE: '{nid}{{{{"{label}"}}}}',
    MUTEX: '{nid}{{{{"{label}"}}}}',
    HUMAN: '{nid}[/"{label}"/]',
    RETRY: '{nid}("{label}")',
    DONE: '{nid}(["{label}"])',
    STOP: '{nid}[["{label}"]]',
}

# 채움은 밝게, 글자색은 명시적으로 어둡게 고정한다 — GitHub 다크 테마는 mermaid
# 기본 글자색을 밝게 바꾸므로 color 를 안 주면 밝은 채움 위 흰 글자가 되어 안 보인다.
_CLASSDEFS = [
    "classDef step fill:#EDF1F5,stroke:#C3CEDA,stroke-width:1px,color:#16202B",
    "classDef gate fill:#F6EBD6,stroke:#96671A,stroke-width:2px,color:#5B3F11",
    "classDef mutex fill:#E3EBF4,stroke:#2F5F94,stroke-width:2px,color:#16202B",
    "classDef human fill:#E3EBF4,stroke:#2F5F94,stroke-width:2px,color:#16202B",
    "classDef retry fill:#EDF1F5,stroke:#6B7C8D,stroke-width:1px,stroke-dasharray:4 3,color:#3D4C5C",
    "classDef done fill:#DFEFE5,stroke:#2E7D53,stroke-width:2px,color:#14532D",
    "classDef stop fill:#F6E2E0,stroke:#A93A31,stroke-width:2px,color:#7F1D1D",
]


@dataclass(frozen=True)
class Node:
    ko: str
    en: str
    kind: str = STEP

    def label(self, lang: str) -> str:
        return self.ko if lang == "ko" else self.en


@dataclass(frozen=True)
class Lane:
    """읽는 순서용 묶음.  subgraph 로 감싸지 않는다 (render 주석 참고)."""

    ko: str
    en: str
    nodes: tuple[str, ...]


@dataclass(frozen=True)
class Layout:
    nodes: dict[str, Node]
    lanes: tuple[Lane, ...]
    collapse: frozenset[str] = frozenset()
    edge_labels: dict[tuple[str, str], tuple[str, str]] = field(default_factory=dict)


# ─────────────────────────────────────────────────────────────────────────────
# 쇼츠 라인 — 병목은 시간(영상화 412초/컷).  fan-out 뒤에 **문**이 선다.
# ─────────────────────────────────────────────────────────────────────────────
SHORTS = Layout(
    nodes={
        "plan": Node("plan<br/>샷 스펙 로드", "plan<br/>load shot spec"),
        "render_shot": Node("render_shot ×N<br/>생성 9초 → 채점 → 재시도",
                            "render_shot ×N<br/>gen 9s → judge → retry"),
        "gate": Node("gate · 🚪 문 1<br/>전 샷 75점 이상",
                     "gate · 🚪 Gate 1<br/>every still ≥ 75", GATE),
        "ready_for_video": Node("ready_for_video", "ready_for_video"),
        "storyboard": Node("storyboard<br/>검수 시트 작성", "storyboard<br/>build review sheet"),
        "approval": Node("approval · 🧑 interrupt<br/>자율 모드면 블로커 기록 후 halt",
                         "approval · 🧑 interrupt<br/>autonomous run logs a blocker, halts", HUMAN),
        "mark_regen": Node("mark_regen<br/>지목한 샷만", "mark_regen<br/>only the marked shots", RETRY),
        "video_stage": Node("video_stage<br/>되돌릴 수 없는 지점", "video_stage<br/>point of no return"),
        "render_clip": Node("render_clip ×N<br/>영상화 412초 → 컷심사 → 시드 리롤",
                            "render_clip ×N<br/>i2v 412s → judge → seed reroll"),
        "clip_gate": Node("clip_gate · 🚪 문 2<br/>REGEN 컷 없어야",
                          "clip_gate · 🚪 Gate 2<br/>no cut left at REGEN", GATE),
        "ready_for_assembly": Node("ready_for_assembly", "ready_for_assembly"),
        "assemble": Node("assemble<br/>concat + SOURCES + 고지",
                         "assemble<br/>concat + SOURCES + disclosure"),
        "legal": Node("legal · legal-gate.sh<br/>결정론 + 판단 병합<br/>미실행 = fail-closed",
                      "legal · legal-gate.sh<br/>deterministic + judgment<br/>not run = fail-closed", GATE),
        "bump_legal": Node("bump_legal<br/>최대 2회", "bump_legal<br/>max 2 rounds", RETRY),
        "release": Node("release<br/>출시 패키지", "release<br/>release package", DONE),
        "blocked": Node("blocked<br/>179분 안 씀", "blocked<br/>179 min not spent", STOP),
    },
    lanes=(
        Lane("스틸", "Stills", ("plan", "render_shot", "gate", "ready_for_video")),
        Lane("사람", "Human", ("storyboard", "approval", "mark_regen", "video_stage")),
        Lane("영상화", "Video", ("render_clip", "clip_gate", "ready_for_assembly")),
        Lane("마감", "Finish", ("assemble", "legal", "bump_legal", "release")),
        Lane("", "", ("blocked",)),
    ),
    edge_labels={
        ("plan", "render_shot"): ("fan-out 샷별", "fan-out per shot"),
        ("gate", "ready_for_video"): ("통과", "pass"),
        ("gate", "blocked"): ("미달 → 3시간 안 씀", "below bar → 3h not spent"),
        ("approval", "video_stage"): ("승인", "approved"),
        ("approval", "mark_regen"): ("재생성 i03,i07", "regen i03,i07"),
        ("approval", "blocked"): ("취소", "reject"),
        ("mark_regen", "render_shot"): ("그 샷만", "those shots only"),
        ("video_stage", "render_clip"): ("fan-out 컷별", "fan-out per cut"),
        ("clip_gate", "ready_for_assembly"): ("통과", "pass"),
        ("clip_gate", "blocked"): ("미달", "below bar"),
        ("legal", "release"): ("PASS", "PASS"),
        ("legal", "bump_legal"): ("REVISE", "REVISE"),
        ("legal", "blocked"): ("BLOCK · 상한 소진", "BLOCK · rounds spent"),
    },
)

# ─────────────────────────────────────────────────────────────────────────────
# 게임 라인 — 병목은 시간이 아니라 배타 자원과 거짓 검증.
# 같은 fan-out 이지만 문이 아니라 **뮤텍스**로 합류한다.
# ─────────────────────────────────────────────────────────────────────────────
GAME = Layout(
    nodes={
        "pm_publish": Node("pm_publish<br/>task 발행 · 레인 3개 오픈",
                           "pm_publish<br/>publish task · open 3 lanes"),
        "review": Node("review<br/>Director · Designer · AI Designer",
                       "review<br/>Director · Designer · AI Designer", GATE),
        "work_lane": Node("work_lane ×3<br/>Programmer · Art · Sound",
                          "work_lane ×3<br/>Programmer · Art · Sound"),
        "unity_scene": Node("unity_scene<br/>🔒 Unity 배타 구간 시작",
                            "unity_scene<br/>🔒 Unity critical section", MUTEX),
        "unity_build": Node("unity_build<br/>산출물 경로를 상태에 확정<br/>+ stale guard",
                            "unity_build<br/>pins artifact paths into state<br/>+ stale guard"),
        "qa": Node("qa<br/>exe 실행 · 스크린샷<br/>★ 상태의 경로만 읽음",
                   "qa<br/>launch exe · screenshot<br/>★ reads only the pinned paths"),
        "ta": Node("ta<br/>아트 품질 채점", "ta<br/>art-quality score", GATE),
        "fix": Node("fix<br/>최대 3회", "fix<br/>max 3 rounds", RETRY),
        "pm_merge": Node("pm_merge<br/>상태 병합 (리듀서)", "pm_merge<br/>state merge (reducer)", DONE),
        "blocked": Node("blocked<br/>블로커 기록", "blocked<br/>blocker logged", STOP),
    },
    lanes=(
        Lane("발행", "Publish", ("pm_publish", "review")),
        Lane("제작", "Production", ("work_lane",)),
        Lane("Unity", "Unity", ("unity_scene", "unity_build")),
        Lane("검증", "Verify", ("qa", "ta", "fix")),
        Lane("", "", ("pm_merge", "blocked")),
    ),
    edge_labels={
        ("review", "work_lane"): ("fan-out 레인별", "fan-out per lane"),
        ("review", "blocked"): ("반려", "rejected"),
        ("unity_build", "qa"): ("빌드 성공", "build ok"),
        ("unity_build", "fix"): ("빌드 실패", "build failed"),
        ("unity_build", "blocked"): ("상한 소진", "rounds spent"),
        ("ta", "pm_merge"): ("통과", "pass"),
        ("ta", "fix"): ("미달", "below bar"),
        ("ta", "blocked"): ("상한 소진", "rounds spent"),
        ("fix", "unity_scene"): ("재빌드", "rebuild"),
    },
)


def _real_nodes(graph) -> set[str]:
    return {n for n in graph.nodes if not n.startswith("__")}


def _check_drift(graph, layout: Layout) -> None:
    """레이아웃이 실행 중인 그래프와 어긋나면 즉시 실패시킨다.

    구조도가 낡는 유일한 경로는 "코드에 노드가 늘었는데 그림은 그대로"다.
    조용히 빠뜨리는 대신 여기서 죽는다.
    """
    live = _real_nodes(graph)
    described = set(layout.nodes) | set(layout.collapse)
    missing, extra = sorted(live - described), sorted(described - live)
    if missing or extra:
        raise RuntimeError(
            "구조도 레이아웃이 그래프와 어긋났다 (graph/diagram.py 를 고쳐라)\n"
            + (f"  그래프에만 있는 노드: {missing}\n" if missing else "")
            + (f"  레이아웃에만 있는 노드: {extra}\n" if extra else "")
        )
    laid_out = [n for lane in layout.lanes for n in lane.nodes]
    dupes = sorted({n for n in laid_out if laid_out.count(n) > 1})
    unplaced = sorted(set(layout.nodes) - set(laid_out))
    if dupes or unplaced:
        raise RuntimeError(
            "레인 배치가 잘못됐다 (graph/diagram.py 의 lanes)\n"
            + (f"  두 번 이상 배치된 노드: {dupes}\n" if dupes else "")
            + (f"  어느 레인에도 없는 노드: {unplaced}\n" if unplaced else "")
        )


def _edges(graph, layout: Layout) -> list[tuple[str, str, bool]]:
    """(source, target, conditional) — START/END 를 떼고 흐름순으로 정렬."""
    edges = [
        (e.source, e.target, bool(e.conditional))
        for e in graph.edges
        if not e.source.startswith("__") and not e.target.startswith("__")
    ]
    for c in layout.collapse:
        ins = [e for e in edges if e[1] == c]
        outs = [e for e in edges if e[0] == c]
        edges = [e for e in edges if c not in (e[0], e[1])]
        for src, _, cond_in in ins:
            for _, dst, cond_out in outs:
                edges.append((src, dst, cond_in or cond_out))
    order = {n: i for i, n in enumerate(n for lane in layout.lanes for n in lane.nodes)}
    edges.sort(key=lambda e: (order.get(e[0], 99), order.get(e[1], 99)))
    seen: set[tuple[str, str, bool]] = set()
    return [e for e in edges if not (e in seen or seen.add(e))]


def _arrow(text: str | None, conditional: bool) -> str:
    if text is None:
        return "-.->" if conditional else "-->"
    return f'-. "{text}" .->' if conditional else f'-- "{text}" -->'


def render(graph, layout: Layout, lang: str = "ko", *, flow: str = "TD") -> str:
    """컴팩트 mermaid.  위상은 graph 에서, 라벨·모양·색은 layout 에서.

    기본값이 세로(TD)이고 subgraph 를 쓰지 않는 이유는 실측이다
    (2026-07-26, mermaid 11 + GitHub 실물 렌더):

      * subgraph 로 단계를 감싸면 dagre 가 되돌아오는 엣지(재시도·차단) 때문에
        박스 순서를 뒤집어, 스틸 단계가 승인 단계 오른쪽 아래로 밀려났다.
      * 가로(LR) 한 장은 폭 1,700px 을 넘겨 README 본문 폭에서 48% 로 축소되고
        글자가 7px 이 된다.  GitHub 의 확대·축소 컨트롤이 오른쪽 끝 노드 위에
        겹쳐 라벨이 잘리기도 했다.
      * 세로는 폭이 본문 안에 들어가 글자가 원래 크기로 읽히고, 오른쪽 여백이
        남아 컨트롤과 겹치지 않는다.
    """
    _check_drift(graph, layout)

    out: list[str] = [f"flowchart {flow}"]
    for nid in (n for lane in layout.lanes for n in lane.nodes):
        node = layout.nodes[nid]
        out.append("  " + _SHAPES[node.kind].format(nid=nid, label=node.label(lang)))

    out.append("")
    for src, dst, conditional in _edges(graph, layout):
        label = layout.edge_labels.get((src, dst))
        text = None if label is None else (label[0] if lang == "ko" else label[1])
        out.append(f"  {src} {_arrow(text, conditional)} {dst}")

    out.append("")
    out.extend("  " + c for c in _CLASSDEFS)
    for kind in (STEP, GATE, MUTEX, HUMAN, RETRY, DONE, STOP):
        members = sorted(nid for nid, n in layout.nodes.items() if n.kind == kind)
        if members:
            out.append(f"  class {','.join(members)} {kind}")
    return "\n".join(out)


def shorts_compact(lang: str = "ko") -> str:
    from .shorts_graph import build_shorts_graph

    return render(build_shorts_graph().get_graph(), SHORTS, lang)


def game_compact(lang: str = "ko") -> str:
    from .game_graph import build_game_graph

    return render(build_game_graph().get_graph(), GAME, lang)
