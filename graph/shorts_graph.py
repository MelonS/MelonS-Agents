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
import os
import pathlib
import sqlite3
import sys
import time
from typing import Any

from langgraph.checkpoint.sqlite import SqliteSaver
from langgraph.graph import END, START, StateGraph
from langgraph.types import Command, Send

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


def build_clip_graph():
    """컷 1개 — 영상화 → 컷 심사 → (REGEN이면) 시드 리롤 재생성."""
    g = StateGraph(ShotState)
    g.add_node("i2v", nodes.render_clip)
    g.add_node("cut_judge", nodes.judge_cut)
    g.add_node("bump_clip_round", nodes.bump_clip_round)
    g.add_node("finalize_clip", nodes.finalize_clip)

    g.add_edge(START, "i2v")
    g.add_edge("i2v", "cut_judge")
    g.add_conditional_edges(
        "cut_judge",
        nodes.after_cut_judge,
        {
            "retry": "bump_clip_round",   # REGEN — 7분을 다시 태운다
            "give_up": "finalize_clip",
            "done": "finalize_clip",      # PASS 또는 REVISE(수동 보정 등급)
        },
    )
    g.add_edge("bump_clip_round", "i2v")
    g.add_edge("finalize_clip", END)
    return g.compile()


_CLIP_GRAPH = None


def clip_graph():
    global _CLIP_GRAPH
    if _CLIP_GRAPH is None:
        _CLIP_GRAPH = build_clip_graph()
    return _CLIP_GRAPH


def fan_out_clips(state: ShortsState) -> list[Send]:
    """문을 통과한 스틸만 영상화로 보낸다."""
    return [
        Send(
            "render_clip",
            {
                "shot": shot,
                "out_dir": state["out_dir"],
                "cut_threshold": state.get("cut_threshold", 75),
                "max_clip_rounds": state.get("max_clip_rounds", 3),
                "frame_count": state.get("frame_count", 3),
                "style_lock": state.get("style_lock", ""),
                "judge_backend": state.get("judge_backend", "mock"),
                "mock": state.get("mock", False),
            },
        )
        for shot in state["shots"].values()
    ]


def fan_out_regen(state: ShortsState) -> list[Send]:
    """운영자가 지목한 샷만 스틸 단계로 되돌린다 (샷당 10초)."""
    shots = state["shots"]
    targets = [sid for sid in shots if shots[sid].get("verdict") != "PASS"]
    return [
        Send(
            "render_shot",
            {
                "shot": shots[sid],
                "out_dir": state["out_dir"],
                "threshold": state.get("threshold", 75),
                "max_rounds": state.get("max_rounds", 3),
                "style_lock": state.get("style_lock", ""),
                "character_lock": state.get("character_lock", ""),
                "judge_backend": state.get("judge_backend", "mock"),
                "mock": state.get("mock", False),
            },
        )
        for sid in targets
    ]


def render_clip(payload: ShotState) -> dict[str, Any]:
    result = clip_graph().invoke(payload, {"recursion_limit": 50})
    shot = result["shot"]
    return {"shots": {shot["id"]: shot}, "trace": result.get("trace", [])}


def build_shorts_graph(checkpointer=None, *, with_video: bool = True):
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
    g.add_edge("blocked", END)

    if not with_video:
        g.add_edge("ready_for_video", END)
        return g.compile(checkpointer=checkpointer)

    # ── Phase 4 — 3시간 앞의 사람 승인 ────────────────────────────────
    g.add_node("storyboard", nodes.build_storyboard)
    g.add_node("approval", nodes.request_approval)
    g.add_node("mark_regen", nodes.mark_regen)

    g.add_edge("ready_for_video", "storyboard")
    g.add_edge("storyboard", "approval")
    g.add_conditional_edges(
        "approval",
        nodes.after_approval,
        {"approved": "video_stage", "regen": "mark_regen", "blocked": "blocked"},
    )
    # 운영자가 지목한 샷만 스틸부터 다시 → 문 1을 다시 지난다
    g.add_conditional_edges("mark_regen", fan_out_regen, ["render_shot"])

    # ── Phase 3 — 승인 뒤에 붙는다. 앞은 건드리지 않는다 ──────────────
    g.add_node("video_stage", nodes.enter_video_stage)
    g.add_node("render_clip", render_clip)
    g.add_node("clip_gate", nodes.clip_gate)
    g.add_node("ready_for_assembly", nodes.ready_for_assembly)

    g.add_conditional_edges("video_stage", fan_out_clips, ["render_clip"])
    g.add_edge("render_clip", "clip_gate")
    g.add_conditional_edges(
        "clip_gate",
        nodes.after_clip_gate,
        {"ready_for_assembly": "ready_for_assembly", "blocked": "blocked"},
    )
    g.add_edge("ready_for_assembly", END)
    return g.compile(checkpointer=checkpointer)


# ─────────────────────────────────────────────────────────────────────────────
# CLI
# ─────────────────────────────────────────────────────────────────────────────


def _open_checkpointer() -> SqliteSaver:
    conn = sqlite3.connect(str(tools.checkpoint_path()), check_same_thread=False)
    return SqliteSaver(conn)


def _report_interrupt(final: dict, out_dir: pathlib.Path, elapsed: float, run_id: str) -> bool:
    """승인 대기 상태면 안내하고 True. 자율 모드 halt도 여기서 처리한다."""
    if final.get("human_decision") == "halt":
        print("⏸  자율 모드 — 승인이 필요해 멈췄습니다 (%.1fs)" % elapsed)
        print("   블로커 : %s" % final.get("blocker_path"))
        print("   재개   : python -m graph.shorts_graph resume --thread %s --approve" % run_id)
        return True

    intr = final.get("__interrupt__")
    if not intr:
        return False

    payload = getattr(intr[0], "value", {}) if isinstance(intr, (list, tuple)) else {}
    print("⏸  승인 대기 — 스토리보드 (%.1fs)" % elapsed)
    print("   검수 시트 : %s" % payload.get("sheet", out_dir / "approvals" / "storyboard.md"))
    print("   영상화 예상: 약 %s분 (되돌릴 수 없음)" % payload.get("est_video_min", "?"))
    print()
    print("   승인      : python -m graph.shorts_graph resume --thread %s --approve" % run_id)
    print("   일부 재생성: python -m graph.shorts_graph resume --thread %s --regen i03,i07" % run_id)
    print("   취소      : python -m graph.shorts_graph resume --thread %s --reject" % run_id)
    return True


def cmd_resume(args) -> int:
    """승인 결정을 넣고 이어서 돈다. 운영자가 만지는 유일한 명령."""
    checkpointer = _open_checkpointer()
    app = build_shorts_graph(checkpointer)
    config = {"configurable": {"thread_id": args.thread}, "recursion_limit": 100}

    snap = app.get_state(config)
    if not snap.values:
        print("그런 run_id가 없습니다: %s" % args.thread, file=sys.stderr)
        return 1

    if args.regen:
        decision = {"decision": "regen", "shots": [s.strip() for s in args.regen.split(",") if s.strip()]}
        print("결정: 재생성 %s" % ", ".join(decision["shots"]))
    elif args.reject:
        decision = {"decision": "reject"}
        print("결정: 취소")
    else:
        decision = {"decision": "approve"}
        n = len(snap.values.get("shots", {}))
        print("결정: 승인 — 영상화 시작 (컷 %d개, 약 %d분)" % (n, round(n * 412 / 60)))

    out_dir = pathlib.Path(snap.values.get("out_dir", "."))
    t0 = time.time()
    final = app.invoke(Command(resume=decision), config)
    elapsed = time.time() - t0

    if _report_interrupt(final, out_dir, elapsed, args.thread):
        return 3
    return _report_final(final, out_dir, elapsed, stills_only=False)


def cmd_diagram(args) -> int:
    for title, g in (
        ("메인 — 계획 → 스틸 병렬 → 문1 → 영상화 병렬 → 문2", build_shorts_graph().get_graph()),
        ("스틸 1개 — 생성(9초) → 채점 → 재시도", build_shot_graph().get_graph()),
        ("컷 1개 — 영상화(7분) → 컷심사 → 시드리롤 재생성", build_clip_graph().get_graph()),
    ):
        print("## %s\n" % title)
        print("```mermaid")
        print(g.draw_mermaid().strip())
        print("```\n")
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
        "cut_threshold": args.cut_threshold,
        "max_clip_rounds": args.max_clip_rounds,
        "frame_count": args.frames,
        "judge_backend": args.judge,
        "mock": args.mock,
        "autonomy_mode": args.autonomy or os.environ.get("AUTONOMY_MODE", "").lower() == "true",
    }

    checkpointer = _open_checkpointer()
    app = build_shorts_graph(checkpointer, with_video=not args.stills_only)
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

    if _report_interrupt(final, out_dir, elapsed, run_id):
        return 3

    return _report_final(final, out_dir, elapsed, stills_only=args.stills_only)


def _report_final(final: dict, out_dir: pathlib.Path, elapsed: float, *, stills_only: bool) -> int:
    print("─" * 62)
    for sid in sorted(final.get("shots", {})):
        s = final["shots"][sid]
        mark = "OK  " if s.get("verdict") == "PASS" else "FAIL"
        line = "  %s %-6s 스틸 %3s점 r%d" % (mark, sid, s.get("score"), int(s.get("round", 0)) + 1)
        if s.get("cut_verdict"):
            cv = s["cut_verdict"]
            cm = {"PASS": "OK", "REVISE": "보정", "FAILED": "FAIL"}.get(cv, cv)
            line += "   │ 컷 %3s점 r%d %s" % (s.get("cut_score"), int(s.get("clip_round", 0)) + 1, cm)
        print(line)
    print("─" * 62)

    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "trace.json").write_text(
        json.dumps(final.get("trace", []), ensure_ascii=False, indent=2), encoding="utf-8"
    )

    if final.get("human_decision") == "reject":
        print("✋ 운영자 취소 — 영상화 진입 안 함 (%.1fs)" % elapsed)
        return 2

    if not final.get("gate_open"):
        print("🚪 문 1 닫힘 — %s" % final.get("gate_reason"))
        print("   (%.1fs) 스펙/프롬프트 고치고 같은 run_id로 다시 돌리면 이어서 진행" % elapsed)
        return 2

    print("🚪 문 1 열림 — %s" % final.get("gate_reason"))
    if stills_only:
        print("   (%.1fs, trace: %s)" % (elapsed, out_dir / "trace.json"))
        return 0

    if final.get("clip_gate_open"):
        print("🚪 문 2 열림 — %s" % final.get("clip_gate_reason"))
        print("   (%.1fs, trace: %s)" % (elapsed, out_dir / "trace.json"))
        return 0

    print("🚪 문 2 닫힘 — %s" % final.get("clip_gate_reason"))
    print("   (%.1fs) 해당 컷만 다시 돌리면 됩니다" % elapsed)
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
    r.add_argument("--threshold", type=int, default=75, help="스틸 임계")
    r.add_argument("--max-rounds", type=int, default=3, help="스틸 재생성 상한")
    r.add_argument("--stills-only", action="store_true", help="문 1에서 멈춤 (영상화 안 함)")
    r.add_argument("--cut-threshold", type=int, default=75, help="컷 임계")
    r.add_argument("--max-clip-rounds", type=int, default=3, help="컷 재생성 상한 (7분/회)")
    r.add_argument("--frames", type=int, default=3, help="컷당 심사 프레임 수 — 실행 토큰의 최대 변수")
    r.add_argument("--autonomy", action="store_true",
                   help="자율 모드 — 승인에서 기다리지 않고 블로커 기록 후 종료 (AUTONOMY_MODE=true 와 동일)")
    r.set_defaults(fn=cmd_run)

    rs = sub.add_parser("resume", help="승인 결정을 넣고 이어서 진행")
    rs.add_argument("--thread", required=True, help="run_id")
    rs.add_argument("--approve", action="store_true", help="승인 — 영상화 진행")
    rs.add_argument("--regen", help="일부 샷만 스틸부터 재생성 (쉼표 구분, 예: i03,i07)")
    rs.add_argument("--reject", action="store_true", help="취소")
    rs.set_defaults(fn=cmd_resume)

    d = sub.add_parser("diagram", help="구조도(mermaid) 출력")
    d.set_defaults(fn=cmd_diagram)

    args = p.parse_args(argv)
    return args.fn(args)


if __name__ == "__main__":
    sys.exit(main())
