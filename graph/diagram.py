"""컴팩트 구조도 — 코드에서 뽑되, 사람이 읽기 좋은 모양으로.

`get_graph().draw_mermaid()` 는 정확하지만 README 에 붙이기엔 나쁘다:

  * `graph TD` 라서 18노드가 세로로 한 줄 — 스크롤 없이 안 보인다.
  * 엣지 목록이 알파벳순이라 흐름 순서로 읽히지 않는다.
  * `__start__` / `<p>` / `&nbsp;` 같은 구현 노이즈가 그대로 나온다.
  * 단계 구분이 없다 — 스틸·승인·영상화·마감이 한 덩어리로 보인다.

그래서 이 모듈은 **위상은 실행 중인 그래프에서 그대로 가져오고**(엣지를 손으로
적지 않는다) 레이아웃·라벨·모양만 입힌다.  코드에 노드가 하나 추가되면
`RuntimeError` 로 생성이 실패한다 — 구조도가 조용히 낡는 경로를 막는다.

    python -m graph.shorts_graph diagram --compact          # 한국어
    python -m graph.shorts_graph diagram --compact --lang en
"""

from __future__ import annotations

from dataclasses import dataclass, field

# 노드 모양 — 의미가 다르면 모양이 달라야 한다.
STEP = "step"      # 평범한 작업 노드
GATE = "gate"      # 자동 심사/문 — 여기서 뒤 단계 비용이 잘린다
HUMAN = "human"    # 사람이 멈춰 서는 지점
RETRY = "retry"    # 회차 증가용 보조 노드 (되돌아가는 화살표의 출발점)
DONE = "done"      # 정상 종료
STOP = "stop"      # 차단 종료

_SHAPES = {
    STEP: '{nid}["{label}"]',
    GATE: '{nid}{{{{"{label}"}}}}',
    HUMAN: '{nid}[/"{label}"/]',
    RETRY: '{nid}("{label}")',
    DONE: '{nid}(["{label}"])',
    STOP: '{nid}(["{label}"])',
}

# 라이트/다크 양쪽에서 읽히도록 채움색은 밝게, 글자색은 명시적으로 어둡게 고정한다.
# (GitHub 은 다크 테마에서 mermaid 기본 글자색을 밝게 바꾸므로 color 를 안 주면
#  밝은 채움 위에 흰 글자가 올라가 안 보인다.)
_CLASSDEFS = [
    "classDef step fill:#eff6ff,stroke:#93c5fd,stroke-width:1px,color:#0f172a",
    "classDef gate fill:#fde68a,stroke:#b45309,stroke-width:1.5px,color:#1f2937",
    "classDef human fill:#ddd6fe,stroke:#6d28d9,stroke-width:1.5px,color:#1f2937",
    "classDef retry fill:#e5e7eb,stroke:#6b7280,stroke-dasharray:3 3,color:#1f2937",
    "classDef done fill:#bbf7d0,stroke:#15803d,stroke-width:1.5px,color:#14532d",
    "classDef stop fill:#fecaca,stroke:#b91c1c,stroke-width:1.5px,color:#7f1d1d",
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
    """한 단계 묶음.  ko/en 이 빈 문자열이면 subgraph 로 감싸지 않는다."""

    ko: str
    en: str
    nodes: tuple[str, ...]

    def title(self, lang: str) -> str:
        return self.ko if lang == "ko" else self.en


@dataclass(frozen=True)
class View:
    """구조도 한 장이 담을 레인 묶음.

    한 장에 다 넣으면 가로가 1,700px 을 넘어 README 폭(약 830px)에서 48% 로
    줄고 글자가 7px 이 된다(2026-07-26 측정).  세로로 세우면 1,210px 로 길어져
    이번에 고치려던 문제로 되돌아간다.  그래서 흐름을 단계로 잘라 각 장이
    폭 안에 들어오게 한다.  잘린 지점은 경계 스텁으로 남겨서 어디로 이어지는지
    보이게 한다.
    """

    ko: str
    en: str
    lanes: tuple[int, ...]

    def title(self, lang: str) -> str:
        return self.ko if lang == "ko" else self.en


@dataclass(frozen=True)
class Layout:
    nodes: dict[str, Node]
    lanes: tuple[Lane, ...]
    # 위상상 필요하지만 그림에선 화살표에 흡수시키는 표지 노드
    # (예: ready_for_video — "전 샷 통과" 상태를 표시하려고 존재하는 통과점).
    collapse: frozenset[str] = frozenset()
    edge_labels: dict[tuple[str, str], tuple[str, str]] = field(default_factory=dict)
    # 뷰가 비어 있으면 전체를 한 장에 그린다.  어느 뷰에도 속하지 않은 레인은
    # 공용으로 취급해 모든 장에 함께 나온다 (차단·병합 같은 종료 상태).
    views: tuple[View, ...] = ()


# ─────────────────────────────────────────────────────────────────────────────
# 쇼츠 라인
# ─────────────────────────────────────────────────────────────────────────────
SHORTS = Layout(
    nodes={
        "plan": Node("계획", "Plan"),
        "render_shot": Node("스틸 라운드<br/>9초/장", "Still round<br/>9s each"),
        "gate": Node("문 1<br/>스틸", "Gate 1<br/>stills", GATE),
        "storyboard": Node("검수 시트", "Review sheet"),
        "approval": Node("사람 승인", "Human approval", HUMAN),
        "mark_regen": Node("재생성 지정", "Mark regen", RETRY),
        "render_clip": Node("컷 라운드<br/>7분/컷", "Clip round<br/>7min each"),
        "clip_gate": Node("문 2<br/>컷", "Gate 2<br/>cuts", GATE),
        "assemble": Node("조립", "Assemble"),
        "legal": Node("법률 심사", "Legal review", GATE),
        "bump_legal": Node("수정 회차", "Revise round", RETRY),
        "release": Node("출시 패키지", "Release package", DONE),
        "blocked": Node("차단", "Blocked", STOP),
    },
    lanes=(
        Lane("① 스틸 · 9초/장", "1. Stills - 9s each", ("plan", "render_shot", "gate")),
        Lane("② 사람이 보는 지점", "2. Human checkpoint", ("storyboard", "approval", "mark_regen")),
        Lane("③ 영상화 · 7분/컷", "3. Video - 7min per cut", ("render_clip", "clip_gate")),
        Lane("④ 마감", "4. Finish", ("assemble", "legal", "bump_legal", "release")),
        Lane("", "", ("blocked",)),
    ),
    views=(
        View("① 스틸 → 문 1 → 사람 승인", "1. Stills -> Gate 1 -> human approval", (0, 1)),
        View("② 영상화 → 문 2 → 마감", "2. Video -> Gate 2 -> finish", (2, 3)),
    ),
    collapse=frozenset({"ready_for_video", "video_stage", "ready_for_assembly"}),
    edge_labels={
        ("plan", "render_shot"): ("샷별", "per shot"),
        ("gate", "storyboard"): ("PASS", "PASS"),
        ("gate", "blocked"): ("미달", "below bar"),
        ("approval", "render_clip"): ("승인", "approved"),
        ("approval", "mark_regen"): ("재생성", "regen"),
        ("approval", "blocked"): ("취소", "reject"),
        ("mark_regen", "render_shot"): ("지정분", "marked"),
        ("clip_gate", "assemble"): ("PASS", "PASS"),
        ("clip_gate", "blocked"): ("미달", "below bar"),
        ("legal", "release"): ("PASS", "PASS"),
        ("legal", "bump_legal"): ("수정", "revise"),
        ("legal", "blocked"): ("BLOCK", "BLOCK"),
    },
)

# ─────────────────────────────────────────────────────────────────────────────
# 게임 라인 — 같은 fan-out 이지만 문이 아니라 뮤텍스로 합류한다
# ─────────────────────────────────────────────────────────────────────────────
GAME = Layout(
    nodes={
        "pm_publish": Node("PM 작업 발행", "PM publishes task"),
        "review": Node("검토", "Review", GATE),
        "work_lane": Node("제작 레인 병렬<br/>코드·아트·사운드", "Work lanes<br/>code·art·sound"),
        "unity_scene": Node("씬 생성", "Scene gen"),
        "unity_build": Node("빌드", "Build"),
        "qa": Node("QA 실물 검증", "QA real build"),
        "ta": Node("TA 아트 심사", "TA art review", GATE),
        "fix": Node("수정 회차", "Fix round", RETRY),
        "pm_merge": Node("병합", "Merge", DONE),
        "blocked": Node("차단", "Blocked", STOP),
    },
    lanes=(
        Lane("① 발행 · 검토", "1. Publish & review", ("pm_publish", "review")),
        Lane("② 제작", "2. Production", ("work_lane",)),
        Lane("③ Unity 배타 구간 · 뮤텍스", "3. Unity critical section - mutex", ("unity_scene", "unity_build")),
        Lane("④ 검증", "4. Verification", ("qa", "ta", "fix")),
        Lane("", "", ("pm_merge", "blocked")),
    ),
    views=(
        View("① 발행 → 검토 → 제작 병렬", "1. Publish -> review -> parallel lanes", (0, 1)),
        View("② Unity 뮤텍스 → 검증 → 병합", "2. Unity mutex -> verify -> merge", (2, 3)),
    ),
    edge_labels={
        ("review", "work_lane"): ("통과", "approved"),
        ("review", "blocked"): ("반려", "rejected"),
        ("unity_build", "qa"): ("빌드 OK", "build OK"),
        ("unity_build", "fix"): ("컴파일 실패", "compile fail"),
        ("unity_build", "blocked"): ("회차 소진", "rounds spent"),
        ("ta", "pm_merge"): ("PASS", "PASS"),
        ("ta", "fix"): ("수정 지시", "fix list"),
        ("ta", "blocked"): ("회차 소진", "rounds spent"),
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
    missing = sorted(live - described)
    extra = sorted(described - live)
    if missing or extra:
        raise RuntimeError(
            "구조도 레이아웃이 그래프와 어긋났다 (graph/diagram.py 를 고쳐라)\n"
            + (f"  그래프에만 있는 노드: {missing}\n" if missing else "")
            + (f"  레이아웃에만 있는 노드: {extra}\n" if extra else "")
        )
    laid_out: list[str] = [n for lane in layout.lanes for n in lane.nodes]
    dupes = sorted({n for n in laid_out if laid_out.count(n) > 1})
    unplaced = sorted(set(layout.nodes) - set(laid_out))
    if dupes or unplaced:
        raise RuntimeError(
            "레인 배치가 잘못됐다 (graph/diagram.py 의 lanes)\n"
            + (f"  두 번 이상 배치된 노드: {dupes}\n" if dupes else "")
            + (f"  어느 레인에도 없는 노드: {unplaced}\n" if unplaced else "")
        )


def _edges(graph, layout: Layout) -> list[tuple[str, str, bool]]:
    """(source, target, conditional) 목록 — START/END 를 떼고 표지 노드를 흡수한다."""
    edges = [
        (e.source, e.target, bool(e.conditional))
        for e in graph.edges
        if not e.source.startswith("__") and not e.target.startswith("__")
    ]
    # 표지 노드 splice: a→c, c→b 를 a→b 로 접는다 (체인도 처리).
    for c in layout.collapse:
        ins = [e for e in edges if e[1] == c]
        outs = [e for e in edges if e[0] == c]
        edges = [e for e in edges if c not in (e[0], e[1])]
        for src, _, cond_in in ins:
            for _, dst, cond_out in outs:
                edges.append((src, dst, cond_in or cond_out))
    # 흐름 순서로 정렬: 레인 순서 → 레인 내 노드 순서.
    order = {n: i for i, n in enumerate(n for lane in layout.lanes for n in lane.nodes)}
    edges.sort(key=lambda e: (order.get(e[0], 99), order.get(e[1], 99)))
    seen: set[tuple[str, str, bool]] = set()
    return [e for e in edges if not (e in seen or seen.add(e))]


def _view_nodes(layout: Layout, view: int | None) -> list[str]:
    """이 장에 들어갈 노드 — 뷰가 가진 레인 + 어느 뷰에도 없는 공용 레인."""
    if view is None or not layout.views:
        return [n for lane in layout.lanes for n in lane.nodes]
    claimed = {i for v in layout.views for i in v.lanes}
    wanted = set(layout.views[view].lanes)
    return [
        n
        for i, lane in enumerate(layout.lanes)
        if i in wanted or i not in claimed
        for n in lane.nodes
    ]


def _edge_text(layout: Layout, src: str, dst: str, lang: str) -> str | None:
    label = layout.edge_labels.get((src, dst))
    if not label:
        return None
    return label[0] if lang == "ko" else label[1]


def _arrow(text: str | None, conditional: bool) -> str:
    if text is None:
        return "-.->" if conditional else "-->"
    return f"-. {text} .->" if conditional else f"-- {text} -->"


def render(graph, layout: Layout, lang: str = "ko", *, view: int | None = None,
           flow: str = "LR", lane_direction: str = "LR", subgraphs: bool = False) -> str:
    """컴팩트 mermaid 문자열.  위상은 graph 에서, 모양·라벨은 layout 에서.

    기본값(LR · subgraph 없음)은 실측으로 고른 조합이다(2026-07-26, mermaid 11):

      * subgraph 로 단계를 감싸면 dagre 가 되돌아오는 엣지(재시도·차단) 때문에
        박스 순서를 뒤집어서 ①스틸이 ②승인 오른쪽 아래로 밀려났다.  박스를
        걷어내면 흐름이 왼→오른쪽 한 줄로 정렬된다.
      * 세로(TD)는 475×1210 — 글자는 크지만 이번에 고치려던 "너무 길다"로
        되돌아간다.  가로(LR) 한 장은 1743×336 으로 폭이 넘쳐 48% 로 줄어든다.
        그래서 뷰로 잘라 각 장을 README 폭 안에 넣는다.
    """
    _check_drift(graph, layout)
    if view is not None and not layout.views:
        raise ValueError("이 레이아웃에는 view 가 정의돼 있지 않다")

    shown = _view_nodes(layout, view)
    shown_set = set(shown)

    out: list[str] = [f"flowchart {flow}"]
    for i, lane in enumerate(layout.lanes):
        lane_nodes = [n for n in lane.nodes if n in shown_set]
        if not lane_nodes:
            continue
        title = lane.title(lang) if subgraphs else ""
        indent = "    " if title else "  "
        if title:
            out.append(f'  subgraph lane{i}["{title}"]')
            out.append(f"    direction {lane_direction}")
        for nid in lane_nodes:
            node = layout.nodes[nid]
            out.append(indent + _SHAPES[node.kind].format(nid=nid, label=node.label(lang)))
        if title:
            out.append("  end")

    # 잘린 지점: 이 장 밖으로 나가거나 밖에서 들어오는 엣지는 스텁 한 개로 모은다.
    # 단, 모든 장에 함께 나오는 공용 노드(차단·병합)로 들어오는 엣지는 그 엣지가
    # 속한 장에서 이미 보이므로 여기선 그리지 않는다 — 안 그러면 다른 단계의
    # 화살표가 전부 이 장으로 새어 들어온다.
    claimed = {i for v in layout.views for i in v.lanes}
    shared = {n for i, lane in enumerate(layout.lanes) if i not in claimed for n in lane.nodes}
    stubs: dict[str, str] = {}
    boundary: list[str] = []
    for src, dst, conditional in _edges(graph, layout):
        inside_src, inside_dst = src in shown_set, dst in shown_set
        if inside_src == inside_dst:
            continue
        if (dst if inside_dst else src) in shared:
            continue
        other = dst if inside_src else src
        which = next((k for k, v in enumerate(layout.views)
                      for i in v.lanes
                      if other in layout.lanes[i].nodes), None)
        if which is None:
            continue
        sid = f"view{which}"
        stubs[sid] = layout.views[which].title(lang)
        text = _edge_text(layout, src, dst, lang)
        edge = (f"  {src} {_arrow(text, conditional)} {sid}" if inside_src
                else f"  {sid} {_arrow(text, conditional)} {dst}")
        if edge not in boundary:
            boundary.append(edge)
    out.extend(f'  {sid}>"{title}"]' for sid, title in stubs.items())

    out.append("")
    for src, dst, conditional in _edges(graph, layout):
        if src in shown_set and dst in shown_set:
            out.append(f"  {src} {_arrow(_edge_text(layout, src, dst, lang), conditional)} {dst}")
    out.extend(boundary)

    out.append("")
    out.extend("  " + c for c in _CLASSDEFS)
    for kind in (STEP, GATE, HUMAN, RETRY, DONE, STOP):
        members = sorted(nid for nid in shown if layout.nodes[nid].kind == kind)
        if members:
            out.append(f"  class {','.join(members)} {kind}")
    if stubs:
        out.append("  classDef stub fill:#f8fafc,stroke:#94a3b8,stroke-dasharray:4 3,color:#475569")
        out.append(f"  class {','.join(sorted(stubs))} stub")
    return "\n".join(out)


def shorts_compact(lang: str = "ko", view: int | None = None) -> str:
    from .shorts_graph import build_shorts_graph

    return render(build_shorts_graph().get_graph(), SHORTS, lang, view=view)


def game_compact(lang: str = "ko", view: int | None = None) -> str:
    from .game_graph import build_game_graph

    return render(build_game_graph().get_graph(), GAME, lang, view=view)
