"""게임 라인 그래프 — 제작 병렬 → Unity 뮤텍스 → 검증 → 병합.

쇼츠 그래프와 모양이 다르다:
  쇼츠 : fan-out → **문**      (3시간을 태우기 전에 막는다)
  게임 : fan-out → **뮤텍스**  (배타 자원 앞에서 직렬로 합류한다)

실행:
    python -m graph.game_graph run --task "B5 autodoor" --mock
    python -m graph.game_graph diagram
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

from . import game_nodes as gn
from . import tools
from .game_state import GameState


def route_after_review(state: GameState):
    """승인 판정 + 제작 3레인 fan-out.

    승인이 나야 레인이 열린다. whiteboard 규칙 그대로 코드·아트·사운드는
    동시에 돌고, 다음 회차(fix)에서는 열린 레인만 다시 돈다.
    """
    if not state.get("review_ok"):
        return "blocked"
    pending = [l for l in state["lanes"].values() if l.get("status") != "done"]
    return [Send("work_lane", {"lane": lane}) for lane in (pending or state["lanes"].values())]


def build_game_graph(checkpointer=None):
    g = StateGraph(GameState)

    g.add_node("pm_publish", gn.publish_task)
    g.add_node("review", gn.review)
    g.add_node("work_lane", gn.work_lane)

    # ── Unity 배타 구간 ─ 아래 세 노드는 그래프 위상상 직렬이다 ────────
    g.add_node("unity_scene", gn.unity_scene)
    g.add_node("unity_build", gn.unity_build)
    g.add_node("qa", gn.qa_verify)
    g.add_node("ta", gn.ta_review)

    g.add_node("fix", gn.bump_fix)
    g.add_node("pm_merge", gn.pm_merge)
    g.add_node("blocked", gn.blocked)

    g.add_edge(START, "pm_publish")
    g.add_edge("pm_publish", "review")
    # 한 노드에 조건부 엣지를 두 번 걸면 서로 덮어써서 Send 페이로드가 깨진다.
    # 승인 판정과 fan-out 을 한 함수로 합친다.
    g.add_conditional_edges("review", route_after_review, ["work_lane", "blocked"])
    # ...전 레인이 끝나야 여기로 모인다 = 뮤텍스
    g.add_edge("work_lane", "unity_scene")
    g.add_edge("unity_scene", "unity_build")

    g.add_conditional_edges("unity_build", gn.after_build,
                            {"qa": "qa", "fix": "fix", "blocked": "blocked"})
    g.add_edge("qa", "ta")
    g.add_conditional_edges("ta", gn.after_verify,
                            {"merge": "pm_merge", "fix": "fix", "blocked": "blocked"})
    g.add_edge("fix", "unity_scene")          # 수정 → 다시 빌드 (배타 구간 재진입)

    g.add_edge("pm_merge", END)
    g.add_edge("blocked", END)
    return g.compile(checkpointer=checkpointer)


# ── CLI ──────────────────────────────────────────────────────────────────────


def cmd_run(args) -> int:
    run_id = args.thread or ("game-%s" % time.strftime("%Y%m%d-%H%M%S"))
    proj = pathlib.Path(args.project or (tools.repo_root() / "skills" / "game-prototype" / "unity-project"))

    initial: GameState = {
        "run_id": run_id,
        "cycle_id": args.cycle,
        "task": args.task,
        "project_path": str(proj),
        "fix_round": 0,
        "max_fix_rounds": args.max_fix_rounds,
        "mock": args.mock,
        "_started_at": time.time(),
    }

    conn = sqlite3.connect(str(tools.checkpoint_path()), check_same_thread=False)
    app = build_game_graph(SqliteSaver(conn))
    config = {"configurable": {"thread_id": run_id}, "recursion_limit": 60}

    print("run_id  : %s" % run_id)
    print("project : %s" % proj)
    print("mode    : %s / 수정 상한 %d회\n" % ("mock" if args.mock else "real", args.max_fix_rounds))

    t0 = time.time()
    final = app.invoke(initial, config)
    elapsed = time.time() - t0

    print("─" * 62)
    for role, lane in sorted((final.get("lanes") or {}).items()):
        print("  %-11s %s" % (role, lane.get("status")))
    print("─" * 62)
    print("  Unity scene : %s" % ("OK" if final.get("scene_ok") else "-"))
    print("  Unity build : %s" % ("OK" if final.get("build_ok") else "FAIL"))
    if final.get("build_dir"):
        print("  산출물      : %s" % pathlib.Path(final["build_dir"]).name)
        print("                (이 경로만 QA 가 읽는다 — 날짜 폴더 재탐색 없음)")
    print("  QA / TA     : %s / %s" % (final.get("qa_note", "-"), final.get("ta_note", "-")))
    print("─" * 62)

    if final.get("merged_wb"):
        print("✅ 사이클 완료 (%.1fs)" % elapsed)
        print("   whiteboard : %s" % final["merged_wb"])
        return 0

    print("⛔ 중단 (%.1fs) — %s" % (elapsed, final.get("blocker_path", "")))
    return 2


def cmd_diagram(args) -> int:
    if getattr(args, "compact", False):
        from . import diagram

        print(diagram.game_compact(args.lang))
        return 0
    print("## 게임 라인 — 제작 병렬 → Unity 뮤텍스 → 검증 → 병합\n")
    print("```mermaid")
    print(build_game_graph().get_graph().draw_mermaid().strip())
    print("```")
    return 0


def main(argv=None) -> int:
    tools.force_utf8_stdout()
    p = argparse.ArgumentParser(prog="graph.game_graph", description="게임 프로토타입 사이클")
    sub = p.add_subparsers(dest="cmd", required=True)

    r = sub.add_parser("run", help="한 사이클 실행")
    r.add_argument("--task", required=True, help="PM 이 발행할 작업 한 줄")
    r.add_argument("--cycle", default="C-000")
    r.add_argument("--project", help="Unity 프로젝트 경로")
    r.add_argument("--thread", help="run_id — 같은 값이면 이어서 진행")
    r.add_argument("--mock", action="store_true", help="Unity 없이 배선만 검증")
    r.add_argument("--max-fix-rounds", type=int, default=3)
    r.set_defaults(fn=cmd_run)

    d = sub.add_parser("diagram", help="구조도(mermaid)")
    d.add_argument("--compact", action="store_true",
                   help="README용 컴팩트 1장 (가로·단계 묶음·게이트 강조)")
    d.add_argument("--lang", choices=("ko", "en"), default="ko", help="--compact 라벨 언어")
    d.set_defaults(fn=cmd_diagram)

    args = p.parse_args(argv)
    return args.fn(args)


if __name__ == "__main__":
    sys.exit(main())
