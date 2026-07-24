"""그래프가 들고 다니는 상태 정의.

리듀서가 붙은 필드(`Annotated[..., fn]`)만 병렬 노드가 동시에 쓸 수 있다.
`.claude/whiteboard.json`의 "병렬 에이전트는 공유 파일을 직접 쓰지 않는다" 규칙과
같은 얘기인데, 여기서는 타입으로 강제된다.
"""

from __future__ import annotations

import operator
from typing import Annotated, Any, Literal, TypedDict


def merge_shots(left: dict, right: dict) -> dict:
    """샷 단위 fan-in 리듀서.

    샷 26개가 병렬로 끝나면 각자 `{"shots": {"i07": {...}}}` 를 돌려준다.
    키가 샷 id라 서로 겹치지 않으므로 그냥 합치면 된다.
    """
    merged = dict(left or {})
    merged.update(right or {})
    return merged


Verdict = Literal["PASS", "REGEN", "FAILED"]

# 컷 심사는 3단계다 — REVISE는 "수동 보정 후 사용" 등급이라 7분을 더 태우지 않는다.
CutVerdict = Literal["PASS", "REVISE", "REGEN", "FAILED"]


class Shot(TypedDict, total=False):
    """샷 하나의 스펙 + 진행 상태."""

    # ── 스펙 (입력 JSON에서 옴) ──────────────────────────────
    id: str
    beat: str                 # 이 샷이 실어야 할 나레이션 비트
    must: list[str]           # 화면에 반드시 보여야 하는 것 (still-judge 루브릭 30점)
    prompt: str               # 스틸 생성 프롬프트
    seed: int
    force_fail: bool          # 테스트용 — mock 판정에서 항상 미달시킨다

    # ── 진행 상태 ────────────────────────────────────────────
    round: int                # 0부터. 재생성할 때마다 +1
    still_path: str | None
    score: int | None
    verdict: Verdict
    saw: str | None           # 심사위원이 "실제로 본 것" 1문장
    prompt_fix: str | None    # REGEN 판정일 때 다음 회차 프롬프트 (완성된 영어 프롬프트)
    elapsed_s: float

    # ── Phase 3 · 영상화 ─────────────────────────────────────
    force_cut_fail: bool      # 테스트용
    clip_round: int
    clip_path: str | None
    cut_score: int | None
    cut_verdict: CutVerdict
    cut_saw: str | None
    cut_issues: list[str]
    clip_fix: str | None      # "SEED_REROLL" 또는 완성된 영어 모션 프롬프트
    clip_elapsed_s: float


class ShotState(TypedDict, total=False):
    """샷 1개짜리 서브그래프의 상태 (still → judge → 재시도 루프)."""

    shot: Shot
    out_dir: str
    threshold: int
    max_rounds: int
    cut_threshold: int
    max_clip_rounds: int
    frame_count: int
    style_lock: str
    character_lock: str
    judge_backend: str
    mock: bool
    trace: Annotated[list[dict[str, Any]], operator.add]


class ShortsState(TypedDict, total=False):
    """메인 그래프 상태."""

    # ── 식별 ─────────────────────────────────────────────────
    run_id: str               # = thread_id. 체크포인트 키
    spec_path: str
    out_dir: str

    # ── 스펙 ─────────────────────────────────────────────────
    short_id: str
    style_lock: str
    character_lock: str

    # ── 샷 (병렬 fan-out / fan-in 대상) ──────────────────────
    shots: Annotated[dict[str, Shot], merge_shots]
    shot_count: int

    # ── 게이트 설정 ──────────────────────────────────────────
    threshold: int            # still-judge 임계 점수 (기본 75)
    max_rounds: int           # 샷당 재생성 횟수 상한 (기본 3)
    judge_backend: str        # mock | cli
    mock: bool                # 스틸 생성도 가짜로 (GPU 없이 배선 검증)

    # ── 문 1 · 스틸 ──────────────────────────────────────────
    gate_open: bool           # False면 영상화(3시간)로 못 넘어간다
    gate_reason: str

    # ── 문 2 · 컷 ────────────────────────────────────────────
    # 주의: 여기 선언하지 않은 키는 LangGraph가 조용히 버린다.
    #       (실측: clip_gate_open 미선언 → 문 2가 항상 닫힘)
    cut_threshold: int
    max_clip_rounds: int
    frame_count: int
    clip_gate_open: bool
    clip_gate_reason: str

    # ── 관측성 ───────────────────────────────────────────────
    trace: Annotated[list[dict[str, Any]], operator.add]
