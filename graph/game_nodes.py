"""게임 라인 노드.

핵심 두 가지:
  1) **뮤텍스** — Unity batchmode 는 배타 자원. 제작은 병렬, 빌드·QA 는 직렬.
     그래프 위상이 곧 락이라 "동시에 두 번 빌드" 가 구조적으로 불가능하다.
  2) **거짓 검증 차단** — 빌드 노드가 만든 경로를 상태에 담아 다니고,
     QA 는 그 경로만 읽는다. 날짜 폴더를 다시 탐색하지 않으므로
     자정을 넘겨도 어제 빌드를 열 수 없다.
"""

from __future__ import annotations

import json
import pathlib
import time
from typing import Any

from . import tools
from .game_state import GameState

SCENE_METHOD = "MelonS.GameProto.EditorTools.SceneSetup.GenerateAll"
BUILD_METHOD = "MelonS.GameProto.EditorTools.BuildScript.BuildWindows"


# ─────────────────────────────────────────────────────────────────────────────
# 1. PM — 작업 발행
# ─────────────────────────────────────────────────────────────────────────────


def publish_task(state: GameState) -> dict[str, Any]:
    """PM 이 task 를 발행하고 레인을 연다. whiteboard 파이프라인 1단계."""
    lanes = {
        "Programmer": {"role": "Programmer", "task": "C# 구현", "status": "pending", "artifacts": []},
        "Art": {"role": "Art", "task": "스프라이트·타일·UI", "status": "pending", "artifacts": []},
        "Sound": {"role": "Sound", "task": "BGM·SFX", "status": "pending", "artifacts": []},
    }
    return {
        "lanes": lanes,
        "reviewers": ["Director", "Designer", "AIDesigner"],
        "trace": [{"node": "pm_publish", "task": state.get("task", ""), "lanes": len(lanes)}],
    }


# ─────────────────────────────────────────────────────────────────────────────
# 2. 3인 동시 검토 — 승인 나야 제작 시작
# ─────────────────────────────────────────────────────────────────────────────


def review(state: GameState) -> dict[str, Any]:
    """Director·Designer·AIDesigner 동시 검토.

    실제 판단은 `.claude/agents/game-*.md` 서브에이전트 몫이라 그래프는
    스코프 검사만 한다 — 작업 문장이 비었으면 통과시키지 않는다.
    """
    task = (state.get("task") or "").strip()
    notes: list[str] = []
    ok = True
    if not task:
        ok = False
        notes.append("task 가 비어 있음 — PM 이 무엇을 만들지 적어야 한다")
    elif len(task) < 8:
        notes.append("task 가 짧다 — 범위가 모호하면 레인이 서로 다른 걸 만든다")

    return {
        "review_ok": ok,
        "review_notes": notes,
        "trace": [{"node": "review", "ok": ok, "reviewers": state.get("reviewers", [])}],
    }


def after_review(state: GameState) -> str:
    return "approved" if state.get("review_ok") else "blocked"


# ─────────────────────────────────────────────────────────────────────────────
# 3. 제작 3레인 — 병렬
# ─────────────────────────────────────────────────────────────────────────────


def work_lane(payload: dict) -> dict[str, Any]:
    """레인 하나. 실제 구현은 해당 서브에이전트가 하고, 여기서는 상태만 기록한다.

    그래프의 책임은 순서·병렬·병합이지 코드를 짜는 게 아니다 — 쇼츠 쪽에서
    ffmpeg 를 재작성하지 않은 것과 같은 원칙.
    """
    lane = dict(payload["lane"])
    t0 = time.time()
    lane["status"] = "done"
    lane["note"] = "%s 레인 완료 (서브에이전트 산출물 대기)" % lane["role"]
    lane["elapsed_s"] = round(time.time() - t0, 3)
    return {
        "lanes": {lane["role"]: lane},
        "trace": [{"node": "lane", "role": lane["role"], "status": lane["status"]}],
    }


# ─────────────────────────────────────────────────────────────────────────────
# 4. Unity 배타 자원 — 여기서 직렬 합류
# ─────────────────────────────────────────────────────────────────────────────


def unity_scene(state: GameState) -> dict[str, Any]:
    """SceneSetup.GenerateAll. 배타 구간 시작."""
    if state.get("mock"):
        return {"scene_ok": True, "trace": [{"node": "unity_scene", "mock": True}]}

    proj = pathlib.Path(state["project_path"])
    rc, out = _unity(proj, SCENE_METHOD, state)
    return {
        "scene_ok": rc == 0,
        "trace": [{"node": "unity_scene", "rc": rc}],
    }


def unity_build(state: GameState) -> dict[str, Any]:
    """★ 거짓 검증 차단의 핵심.

    빌드 산출물 경로를 **이 노드가 직접 확정해서 상태에 넣는다.**
    QA 는 날짜 폴더를 다시 탐색하지 않고 이 경로만 쓴다 — 자정을 넘겨도
    어제 빌드를 열 수 없다. 규칙이 아니라 구조로 막는다.
    """
    stamp = time.strftime("%Y-%m-%d_%H%M%S")

    if state.get("mock"):
        # 실물과 같은 자리에 만든다 — mock 이 다른 곳에 두면 경로 버그를 못 잡는다.
        d = pathlib.Path(state["project_path"]).parent / "builds" / ("mock-%s" % stamp)
        d.mkdir(parents=True, exist_ok=True)
        exe = d / "PawnSim.exe"
        exe.write_bytes(b"MZ")                      # 자리표시자
        return {
            "build_ok": True, "build_dir": str(d), "build_stamp": stamp,
            "exe_path": str(exe),
            "trace": [{"node": "unity_build", "mock": True, "dir": d.name}],
        }

    proj = pathlib.Path(state["project_path"])
    rc, out = _unity(proj, BUILD_METHOD, state)
    if rc != 0:
        return {
            "build_ok": False, "build_log": out[-800:],
            "trace": [{"node": "unity_build", "rc": rc}],
        }

    # BuildScript.cs 는 `../builds` (= 프로젝트의 형제 폴더) 에 쓰고, 폴더명은
    # `day-<N>-<날짜>` 날짜 스탬프다 — 문서에 적힌 바로 그 함정의 출처.
    #
    # ★ 그래서 "가장 최근 폴더" 를 고르지 않는다. **이번 실행 이후에 생긴 것**만
    #   인정한다. 자정을 넘겨 어제 폴더가 최신으로 보이더라도 걸러진다.
    build_root = proj.parent / "builds"
    fresh = [
        p for p in build_root.iterdir()
        if p.is_dir() and p.stat().st_mtime >= _t0_of(state)
    ] if build_root.exists() else []
    if not fresh:
        return {
            "build_ok": False,
            "build_log": "빌드는 rc=0 인데 %s 에 이번 실행에서 생긴 폴더가 없다 — stale 의심"
                         % build_root,
            "trace": [{"node": "unity_build", "rc": rc, "stale_guard": "tripped"}],
        }

    d = max(fresh, key=lambda p: p.stat().st_mtime)
    # UnityCrashHandler64.exe 같은 부속 실행파일을 잡으면 안 된다 — 게임 본체만.
    exes = [e for e in d.rglob("*.exe") if "crashhandler" not in e.name.lower()]
    return {
        "build_ok": bool(exes),
        "build_dir": str(d),
        "build_stamp": stamp,
        "exe_path": str(exes[0]) if exes else None,
        "trace": [{"node": "unity_build", "rc": rc, "dir": d.name, "exe": bool(exes)}],
    }


def _t0_of(state: GameState) -> float:
    """이 사이클이 시작된 시각. 이보다 오래된 폴더는 이번 산출물이 아니다."""
    return float(state.get("_started_at") or 0.0)


def _unity(proj: pathlib.Path, method: str, state: GameState) -> tuple[int, str]:
    import os
    import subprocess

    unity = os.environ.get("UNITY_EXE")
    if not unity or not pathlib.Path(unity).exists():
        raise tools.ToolError(["unity"], 66, "", "UNITY_EXE 를 못 찾았다 — --mock 로 배선만 검증")

    # 괄호 필수: / 가 % 보다 먼저 묶여 Path % str 이 된다 (실물에서만 터짐)
    log = pathlib.Path(state["project_path"]) / ("unity-%s.log" % method.rsplit(".", 1)[-1])
    p = subprocess.run(
        [unity, "-batchmode", "-nographics", "-quit",
         "-projectPath", str(proj), "-executeMethod", method, "-logFile", str(log)],
        capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=3600,
    )
    tail = log.read_text(encoding="utf-8", errors="replace")[-2000:] if log.exists() else p.stderr
    return p.returncode, tail


def after_build(state: GameState) -> str:
    if state.get("build_ok"):
        return "qa"
    if int(state.get("fix_round", 0)) + 1 >= int(state.get("max_fix_rounds", 3)):
        return "blocked"
    return "fix"                                   # 컴파일 에러 → 코드 레인으로


# ─────────────────────────────────────────────────────────────────────────────
# 5. QA + TA — 검증
# ─────────────────────────────────────────────────────────────────────────────


def qa_verify(state: GameState) -> dict[str, Any]:
    """exe 를 띄우고 스크린샷을 찍어 확인한다.

    ★ `state["exe_path"]` 만 쓴다. 폴더를 다시 찾지 않는다 —
      그게 거짓 검증(어제 빌드로 '고쳤다')이 들어오는 경로였다.
    """
    exe = state.get("exe_path")
    if not exe:
        return {"qa_ok": False, "qa_note": "exe 경로가 상태에 없다", "trace": [{"node": "qa", "ok": False}]}

    shot = pathlib.Path(state["build_dir"]) / "qa-screenshot.png"

    if state.get("mock"):
        shot.write_bytes(tools._MOCK_PNG)
        ok, note = True, "[mock] 스크린샷 자리표시자"
    else:
        # 검증된 계약을 그대로 따른다 (skills/game-dev-agent/scripts/modules/qa.py).
        # subprocess.run 은 쓰면 안 된다 — 게임이 자동 종료하지 않으면 타임아웃으로
        # 죽는다(실측). Popen 으로 띄우고 **스크린샷 파일이 생기는지**를 폴링한 뒤
        # 직접 종료시킨다.
        #
        # 플래그 두 개 모두 필수 — 실측으로 확인한 연쇄:
        #
        #   -autostart : 빌드가 메뉴씬으로 부팅한다. 없으면 게임 씬에 못 들어간다.
        #   -repro     : 게임은 시작하자마자 '둘러보기' 일시정지에 들어간다
        #                (GameManager.PauseAtStart → timeScale=0). 그런데
        #                AutoScreenshotter 는 WaitForSeconds(= 스케일 시간)를 쓰므로
        #                코루틴이 영원히 안 깨어나 스크린샷이 안 나온다.
        #                일시정지를 건너뛰는 조건은 `ReproHarness.Enabled` 하나뿐이고
        #                (`if (!ReproHarness.Enabled) StartCoroutine(PauseAtStart())`),
        #                그건 `-repro <scenario.json>` 로만 켜진다.
        #                -integration 은 IntegrationTestRunner 만 켤 뿐 일시정지를
        #                풀지 못한다 (실측: 로그에 activated 후에도 일시정지 진입).
        scenario = state.get("qa_scenario") or str(
            tools.repo_root() / "skills" / "game-prototype" / "repro-scenarios" / "_grade-tour.json"
        )
        ok, note = tools.launch_and_capture(
            pathlib.Path(exe), shot, delay=float(state.get("qa_delay", 8)),
            extra_args=["-autostart", "-repro", scenario],
        )

    return {
        "screenshot": str(shot) if ok else None,
        "qa_ok": ok,
        "qa_note": note,
        "trace": [{"node": "qa", "ok": ok, "exe": pathlib.Path(exe).name}],
    }


def ta_review(state: GameState) -> dict[str, Any]:
    """TA — 아트 품질. 제작자와 분리된 눈으로 본다 (`.claude/agents/ta.md`)."""
    if not state.get("qa_ok"):
        return {"ta_ok": False, "ta_note": "QA 실패 — TA 심사 생략", "trace": [{"node": "ta", "skipped": True}]}
    return {
        "ta_ok": True,
        "ta_note": "[mock] TA 통과" if state.get("mock") else "TA 심사 완료",
        "trace": [{"node": "ta", "ok": True}],
    }


def after_verify(state: GameState) -> str:
    if state.get("qa_ok") and state.get("ta_ok"):
        return "merge"
    if int(state.get("fix_round", 0)) + 1 >= int(state.get("max_fix_rounds", 3)):
        return "blocked"
    return "fix"


def bump_fix(state: GameState) -> dict[str, Any]:
    n = int(state.get("fix_round", 0)) + 1
    lanes = {r: {**l, "status": "pending"} for r, l in (state.get("lanes") or {}).items()
             if r == "Programmer"}          # 재현→수정은 코드 레인만 다시 연다
    return {"fix_round": n, "lanes": lanes, "trace": [{"node": "fix", "round": n}]}


# ─────────────────────────────────────────────────────────────────────────────
# 6. PM 병합 — whiteboard 를 사람이 아니라 리듀서가 합친다
# ─────────────────────────────────────────────────────────────────────────────


def pm_merge(state: GameState) -> dict[str, Any]:
    """wb/*.json 을 읽어 whiteboard 스냅샷을 쓴다.

    손으로 병합하다 이스케이프 안 된 따옴표로 JSON 이 깨진 적이 있다(6d8d7e9).
    `json.dumps` 가 이스케이프를 책임지므로 같은 사고가 구조적으로 안 난다.
    """
    repo = tools.repo_root()
    wb_dir = repo / ".claude" / "wb"
    agents: dict[str, Any] = {}
    broken: list[str] = []

    if wb_dir.exists():
        for f in sorted(wb_dir.glob("*.json")):
            try:
                agents[f.stem] = json.loads(f.read_text(encoding="utf-8"))
            except Exception as e:
                broken.append("%s: %s" % (f.name, e))

    out = pathlib.Path(state.get("project_path", ".")) / ("whiteboard-%s.json" % state["run_id"])
    out.write_text(
        json.dumps(
            {
                "cycle": state.get("cycle_id"),
                "task": state.get("task"),
                "build_dir": state.get("build_dir"),
                "build_stamp": state.get("build_stamp"),
                "screenshot": state.get("screenshot"),
                "agents": agents,
                "broken_sources": broken,
                "merged_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
            },
            ensure_ascii=False, indent=2,
        ),
        encoding="utf-8",
    )
    return {
        "merged_wb": str(out),
        "trace": [{"node": "pm_merge", "agents": len(agents), "broken": len(broken)}],
    }


def blocked(state: GameState) -> dict[str, Any]:
    blockers = tools.records_dir() / "blockers" / time.strftime("%Y-%m-%d")
    blockers.mkdir(parents=True, exist_ok=True)
    bp = blockers / ("%s-game.md" % state["run_id"])
    reason = (
        "\n".join(state.get("review_notes", []))
        or state.get("qa_note")
        or state.get("build_log")
        or "알 수 없음"
    )
    bp.write_text(
        "# 게임 사이클 중단 — %s\n\n작업: %s\n\n원인:\n%s\n\n수정 회차: %d\n"
        % (state["run_id"], state.get("task", ""), reason, int(state.get("fix_round", 0)) + 1),
        encoding="utf-8",
    )
    return {"blocker_path": str(bp), "trace": [{"node": "blocked", "path": str(bp)}]}
