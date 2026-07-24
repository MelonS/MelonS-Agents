"""★ 그래프 정의 = 구조도.

이 파일 하나가 "쇼츠 스틸 단계가 어떻게 도는가"의 정본이다. 손으로 그린 다이어그램이
아니라 실행되는 코드이므로 문서처럼 낡지 않는다. 그림이 필요하면:

    python -m graph.shorts_graph diagram

실행:

    python -m graph.shorts_graph run --spec graph/examples/shots.example.json --mock

종료 코드: 0 = 문 통과(영상화 진입 가능) · 2 = 문에서 차단 · 1 = 오류
"""

from __future__ import annotations

import argparse
import json
import pathlib
import sqlite3
import sys
import time
from typing import Any

from langgraph.checkpoint.sqlite import SqliteSaver
from langgraph.graph import END, START, StateGraph
from langgraph.types import Send

from . import nodes, tools
from .state import ShortsState, ShotState

# ─────────────────────────────────────────────────────────────────────────────
# 서브그래프: 샷 1개 — 생성 → 채점 → (미달이면) 재생성
#
#   문서(`docs/generative-shorts-pipeline.md` §4.5)에 "75 미만은 prompt_fix를
#   반영해 그 샷만 자동 재생성, 최대 3라운드"라고 적혀 있던 바로 그 루프.
#   지금까지 실행 코드가 없어서 사람이 손으로 돌리고 있었다.
# ─────────────────────────────────────────────────────────────────────────────


def build_shot_graph():
    g = StateGraph(ShotState)
    g.add_node("still", nodes.render_still)
    g.add_node("judge", nodes.judge_still)
    g.add_node("bump_round", nodes.bump_round)
    g.add_node("finalize", nodes.finalize_shot)

    g.add_edge(START, "still")
    g.add_edge("still", "judge")
    g.add_conditional_edges(
        "judge",
        nodes.after_judge,
        {
            "retry": "bump_round",   # 75 미달 & 회차 남음 → 그 샷만 다시 (9초)
            "give_up": "finalize",   # 상한 소진 → FAILED 확정
            "done": "finalize",      # 통과
        },
    )
    g.add_edge("bump_round", "still")
    g.add_edge("finalize", END)
    return g.compile()


_SHOT_GRAPH = None


def shot_graph():
    global _SHOT_GRAPH
    if _SHOT_GRAPH is None:
        _SHOT_GRAPH = build_shot_graph()
    return _SHOT_GRAPH


# ─────────────────────────────────────────────────────────────────────────────
# 메인 그래프
# ─────────────────────────────────────────────────────────────────────────────


def fan_out(state: ShortsState) -> list[Send]:
    """샷 N개를 병렬로 흘려보낸다.

    스틸은 9초짜리라 병렬 이득이 크진 않지만, 구조를 여기서 잡아두면 Phase 2에서
    같은 모양으로 I2V(7분/샷)를 붙일 수 있다.
    """
    return [
        Send(
            "render_shot",
            {
                "shot": shot,
                "out_dir": state["out_dir"],
                "threshold": state.get("threshold", 75),
                "max_rounds": state.get("max_rounds", 3),
                "style_lock": state.get("style_lock", ""),
                "character_lock": state.get("character_lock", ""),
                "judge_backend": state.get("judge_backend", "mock"),
                "mock": state.get("mock", False),
            },
        )
        for shot in state["shots"].values()
    ]


def render_shot(payload: ShotState) -> dict[str, Any]:
    """샷 서브그래프를 돌리고 결과를 메인 상태에 합류시킨다."""
    result = shot_graph().invoke(payload, {"recursion_limit": 50})
    shot = result["shot"]
    return {"shots": {shot["id"]: shot}, "trace": result.get("trace", [])}


def build_shorts_graph(checkpointer=None):
    g = StateGraph(ShortsState)
    g.add_node("plan", nodes.plan_shots)
    g.add_node("render_shot", render_shot)
    g.add_node("gate", nodes.gate)
    g.add_node("ready_for_video", nodes.ready_for_video)
    g.add_node("blocked", nodes.blocked)

    g.add_edge(START, "plan")
    g.add_conditional_edges("plan", fan_out, ["render_shot"])
    g.add_edge("render_shot", "gate")        # 전 샷이 끝나야 gate가 돈다
    g.add_conditional_edges(
        "gate",
        nodes.after_gate,
        {"ready_for_video": "ready_for_video", "blocked": "blocked"},
    )
    g.add_edge("ready_for_video", END)
    g.add_edge("blocked", END)
    return g.compile(checkpointer=checkpointer)


# ─────────────────────────────────────────────────────────────────────────────
# CLI
# ─────────────────────────────────────────────────────────────────────────────


def _open_checkpointer() -> SqliteSaver:
    conn = sqlite3.connect(str(tools.checkpoint_path()), check_same_thread=False)
    return SqliteSaver(conn)


def cmd_diagram(args) -> int:
    print("## 메인 그래프 — 계획 → 샷 병렬 → 문\n")
    print("```mermaid")
    print(build_shorts_graph().get_graph().draw_mermaid().strip())
    print("```\n")
    print("## 샷 서브그래프 — 생성 → 채점 → 재시도 루프\n")
    print("```mermaid")
    print(build_shot_graph().get_graph().draw_mermaid().strip())
    print("```")
    return 0


def cmd_run(args) -> int:
    spec_path = pathlib.Path(args.spec).resolve()
    if not spec_path.exists():
        print("스펙 파일 없음: %s" % spec_path, file=sys.stderr)
        return 1

    run_id = args.thread or ("still-%s" % time.strftime("%Y%m%d-%H%M%S"))
    out_dir = pathlib.Path(args.out) if args.out else (tools.records_dir() / "graph" / run_id)
    out_dir.mkdir(parents=True, exist_ok=True)

    initial: ShortsState = {
        "run_id": run_id,
        "spec_path": str(spec_path),
        "out_dir": str(out_dir),
        "threshold": args.threshold,
        "max_rounds": args.max_rounds,
        "judge_backend": args.judge,
        "mock": args.mock,
    }

    checkpointer = _open_checkpointer()
    app = build_shorts_graph(checkpointer)
    config = {"configurable": {"thread_id": run_id}, "recursion_limit": 100}

    print("run_id : %s   (같은 값으로 다시 돌리면 이어서 진행)" % run_id)
    print("out    : %s" % out_dir)
    print("mode   : %s / judge=%s / 임계 %d점 / 최대 %d회\n"
          % ("mock" if args.mock else "real", args.judge, args.threshold, args.max_rounds))

    t0 = time.time()
    # 이미 끝난 스레드를 같은 id로 다시 돌리면 입력 없이 재개된다.
    resuming = checkpointer.get(config) is not None
    final = app.invoke(None if resuming and not args.restart else initial, config)
    elapsed = time.time() - t0

    print("─" * 62)
    for sid in sorted(final.get("shots", {})):
        s = final["shots"][sid]
        mark = "OK  " if s.get("verdict") == "PASS" else "FAIL"
        print("  %s %-6s %3s점  r%d  %s"
              % (mark, sid, s.get("score"), int(s.get("round", 0)) + 1, s.get("still_path") or ""))
    print("─" * 62)

    (out_dir / "trace.json").write_text(
        json.dumps(final.get("trace", []), ensure_ascii=False, indent=2), encoding="utf-8"
    )

    if final.get("gate_open"):
        print("🚪 문 열림 — %s" % final.get("gate_reason"))
        print("   (%.1fs 소요, trace: %s)" % (elapsed, out_dir / "trace.json"))
        return 0

    print("🚪 문 닫힘 — %s" % final.get("gate_reason"))
    print("   (%.1fs 소요) 스펙/프롬프트 고치고 같은 run_id로 다시 돌리면 이어서 진행" % elapsed)
    return 2


def main(argv=None) -> int:
    p = argparse.ArgumentParser(prog="graph.shorts_graph", description="쇼츠 스틸 게이트 그래프")
    sub = p.add_subparsers(dest="cmd", required=True)

    r = sub.add_parser("run", help="스틸 생성 → 채점 → 문")
    r.add_argument("--spec", required=True, help="샷 스펙 JSON")
    r.add_argument("--out", help="출력 디렉터리 (기본 records/graph/<run_id>)")
    r.add_argument("--thread", help="run_id — 같은 값이면 이어서 진행")
    r.add_argument("--restart", action="store_true", help="체크포인트 무시하고 처음부터")
    r.add_argument("--mock", action="store_true", help="ComfyUI 없이 배선만 검증")
    r.add_argument("--judge", default="mock", choices=["mock", "cli"], help="심사위원 백엔드")
    r.add_argument("--threshold", type=int, default=75)
    r.add_argument("--max-rounds", type=int, default=3)
    r.set_defaults(fn=cmd_run)

    d = sub.add_parser("diagram", help="구조도(mermaid) 출력")
    d.set_defaults(fn=cmd_diagram)

    args = p.parse_args(argv)
    return args.fn(args)


if __name__ == "__main__":
    sys.exit(main())
