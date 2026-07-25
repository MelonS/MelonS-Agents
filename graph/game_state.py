"""게임 라인 상태.

쇼츠와 병목이 다르다:
  쇼츠 — 영상화 3시간. 잘못된 절차로 시간을 태우는 게 문제 → **문**
  게임 — 빌드는 몇 분. **거짓 검증**이 문제 → **산출물 신원(identity)**

`docs/` 에 기록된 함정: 빌드 폴더가 날짜 스탬프(day-tag-YYYY-MM-DD)라
자정을 넘기면 어제 빌드를 열고 "고쳤다"가 된다. 규칙으로 막는 대신
**그래프가 자기가 방금 만든 산출물의 경로를 들고 다니게** 해서
구조적으로 stale 을 못 읽게 한다.
"""

from __future__ import annotations

import operator
from typing import Annotated, Any, Literal, TypedDict

LaneStatus = Literal["pending", "done", "failed"]


def merge_lanes(left: dict, right: dict) -> dict:
    """제작 3레인(코드·아트·사운드) fan-in 리듀서.

    `.claude/whiteboard.json` 의 병렬 규칙 — "병렬 에이전트는 공유 파일을 직접
    쓰지 않고 각자 wb/<ROLE>.json 에 기록, PM 이 단일 writer 로 병합" — 을
    타입으로 강제한 것. 손으로 병합하다 JSON 이 깨진 게 실제로 일어났다(6d8d7e9).
    """
    merged = dict(left or {})
    merged.update(right or {})
    return merged


class Lane(TypedDict, total=False):
    """제작 레인 하나 (Programmer / Art / Sound)."""

    role: str
    task: str
    status: LaneStatus
    artifacts: list[str]
    note: str
    elapsed_s: float


class GameState(TypedDict, total=False):
    # ── 식별 ─────────────────────────────────────────────────
    run_id: str
    cycle_id: str                  # C-020 같은 사이클 번호
    task: str                      # PM 이 발행한 작업 한 줄
    project_path: str              # Unity 프로젝트 루트

    # ── 승인 (Director + Designer + AIDesigner 동시 검토) ────
    reviewers: list[str]
    review_ok: bool
    review_notes: list[str]

    # ── 제작 3레인 (병렬 — 리듀서 필수) ──────────────────────
    lanes: Annotated[dict[str, Lane], merge_lanes]

    # ── Unity 배타 자원 (여기서 직렬 합류) ───────────────────
    # batchmode 는 프로젝트당 하나만 돌 수 있다. 병렬로 부르면 락 충돌로
    # 둘 다 깨진다. 그래프 위상 자체가 뮤텍스 역할을 한다.
    scene_ok: bool
    build_ok: bool
    build_dir: str | None          # ★ 이번 실행이 만든 빌드. stale 방지의 핵심
    build_stamp: str | None        # 빌드 시각 — 날짜 폴더와 대조해 신원 확인
    exe_path: str | None
    build_log: str | None

    # ── 검증 ─────────────────────────────────────────────────
    screenshot: str | None
    qa_ok: bool
    qa_note: str
    ta_ok: bool
    ta_note: str

    # ── 재시도 ───────────────────────────────────────────────
    fix_round: int
    max_fix_rounds: int

    # ── 병합·종료 ────────────────────────────────────────────
    merged_wb: str | None          # PM 이 병합한 whiteboard 경로
    blocker_path: str | None
    mock: bool

    trace: Annotated[list[dict[str, Any]], operator.add]
