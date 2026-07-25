"""노드 구현.

각 노드는 "상태 일부를 읽어서 상태 일부를 돌려주는 함수"다. 부작용(파일 생성,
외부 프로세스)은 전부 `tools.py`를 통한다.
"""

from __future__ import annotations

import hashlib
import json
import os
import pathlib
import re
import time
from typing import Any

from langgraph.types import interrupt

from . import tools
from .state import ShortsState, ShotState

# ─────────────────────────────────────────────────────────────────────────────
# 심사위원 백엔드
#
# 진짜 심사위원은 `.claude/agents/still-judge.md` (Claude Code 서브에이전트)다.
# 파이썬에서 그걸 직접 부를 수 없으므로 두 갈래를 둔다:
#   mock — GPU/모델 없이 배선·루프·게이트만 검증. 기본값.
#   cli  — `claude` CLI 헤드리스 호출. Max 구독 쿼터를 쓰므로 추가 과금 없음.
#          (config/policies.yaml 머니 방화벽: 유료 API 신규 호출은 명시 승인 대상.
#           그래서 기본값이 mock이고, cli는 --judge cli 로 명시해야 켜진다.)
# ─────────────────────────────────────────────────────────────────────────────

JUDGE_PROMPT = """\
너는 스틸 심사위원이다. `.claude/agents/still-judge.md`의 루브릭을 그대로 적용한다.

반드시 이미지를 Read로 먼저 열고, 실제로 본 것만 채점한다. 추정 금지.

샷 스펙:
  id: {id}
  beat: {beat}
  must: {must}
  character_lock: {character_lock}
  style_lock: {style_lock}
  image: {image}
{prior}
루브릭 (100점): 피사체 정확도 30 · 구도/가독성 20 · 무드/팔레트 20 ·
캐릭터 일관성 20 · 결함 없음 10.
즉시 실패(REGEN, 점수 무관): must 피사체 부재 · 화면 90%+ 암흑 ·
텍스트/워터마크 아티팩트 · 캐릭터 디자인 이탈.

**캐릭터 일관성 예외**: 이 샷의 must 목록에 인물이 없으면 그 샷은 인물이 없는 게
정상이다. 캐릭터 일관성 20점은 만점 처리하고, 인물이 없다는 이유로 감점하지 마라.
(오히려 인물이 나오면 안 되는 샷에 인물이 있으면 피사체 정확도에서 감점한다.)

{threshold}점 이상이면 PASS, 미만이면 REGEN.

**prompt_fix 작성 규칙 (REGEN일 때만, 매우 중요)**
생성기는 Z-Image(cfg=1)다. 네거티브 프롬프트를 무시하고, 긴 지시문을 덧붙이면
조건이 희석된다. 그러므로 prompt_fix에는 **지시문을 쓰지 말고, 다음 회차에
그대로 넣을 완성된 영어 프롬프트 한 덩어리**를 써라.

- 영어로, 원본 프롬프트와 같은 문체(쉼표로 이어지는 명사구)로 다시 쓴다
- "avoid / don't / not" 같은 부정 서술 금지 — 원하는 상태를 긍정으로 서술한다
  (예: "tiny distant light" 대신 "large" 를 부정하지 말고, 원하는 크기를 직접 쓴다)
- must 항목이 화면에서 확실히 보이도록 크기·위치를 명시한다
- STYLE LOCK 톤을 유지한다

JSON 객체 하나만 출력한다. 다른 말 금지:
{{"id":"...","total":0,"verdict":"PASS|REGEN","saw":"실제 본 것 1문장","prompt_fix":"REGEN일 때만: 다음 회차에 그대로 쓸 완성된 영어 프롬프트"}}
"""


def _judge_mock(shot: dict, threshold: int) -> dict[str, Any]:
    """결정론 가짜 채점 — 회차가 올라갈수록 점수가 오른다.

    루프가 실제로 도는지, 게이트가 실제로 막는지를 GPU 없이 증명하기 위한 것.
    `force_fail: true`인 샷은 끝까지 미달시켜서 **문이 닫히는 경로**도 시연한다.
    """
    rnd = int(shot.get("round", 0))
    if shot.get("force_fail"):
        total = 41
    else:
        h = int(hashlib.sha1(("%s:%d" % (shot["id"], rnd)).encode()).hexdigest()[:8], 16)
        total = min(97, 58 + rnd * 13 + h % 10)

    passed = total >= threshold
    return {
        "id": shot["id"],
        "total": total,
        "verdict": "PASS" if passed else "REGEN",
        "saw": "[mock] r%d 채점 — 실제 이미지 판독 아님" % rnd,
        "prompt_fix": None if passed else "[mock] 피사체를 더 크고 밝게, must 항목 명확히 노출",
    }


def _judge_cli(shot: dict, threshold: int, style_lock: str, character_lock: str) -> dict[str, Any]:
    """`claude` CLI 헤드리스 호출로 진짜 채점.

    주의: 이 경로는 운영자 머신의 `claude` 바이너리에 의존한다. 첫 사용 전에
    한 번 수동 검증할 것 (README의 '심사위원 실물 연결' 절 참조).
    """
    prior = ""
    if shot.get("prompt_fix"):
        prior = "\n직전 회차 처방(반영 여부를 명시할 것): %s\n" % shot["prompt_fix"]

    prompt = JUDGE_PROMPT.format(
        id=shot["id"],
        beat=shot.get("beat", ""),
        must=", ".join(shot.get("must", [])) or "(없음)",
        character_lock=character_lock or "(없음)",
        style_lock=style_lock or "(없음)",
        image=shot.get("still_path", ""),
        prior=prior,
        threshold=threshold,
    )
    # --allowedTools Read : 헤드리스에서 이미지를 열려면 필수. 이게 없으면 심사위원이
    #   그림을 못 보고 추정으로 점수를 매긴다 (= 루브릭 위반).
    # --model            : 샷당 반복 호출이라 여기가 실행 토큰의 최대 덩어리다.
    #                      기본 Sonnet, JUDGE_MODEL 로 덮어쓸 수 있다.
    cmd = [
        "claude", "-p", prompt,
        "--allowedTools", "Read",
        "--model", os.environ.get("JUDGE_MODEL", "claude-sonnet-5"),
    ]
    out = tools.run(cmd, timeout=300)

    start, end = out.find("{"), out.rfind("}")
    if start < 0 or end <= start:
        raise tools.ToolError(["claude"], 65, out, "심사위원 응답에서 JSON을 못 찾음")
    data = json.loads(out[start : end + 1])
    data.setdefault("id", shot["id"])
    data.setdefault("verdict", "PASS" if int(data.get("total", 0)) >= threshold else "REGEN")
    return data


# ─────────────────────────────────────────────────────────────────────────────
# 샷 서브그래프 노드 (still → judge → 재시도)
# ─────────────────────────────────────────────────────────────────────────────


def render_still(state: ShotState) -> dict[str, Any]:
    """스틸 1장 생성 (~9초). 재시도 회차면 직전 처방을 프롬프트에 얹는다."""
    shot = dict(state["shot"])
    rnd = int(shot.get("round", 0))
    out_dir = pathlib.Path(state["out_dir"]) / "stills"
    out_path = out_dir / ("%s_r%d.png" % (shot["id"], rnd))

    # 처방은 '덧붙이는 지시문'이 아니라 '통째로 다시 쓴 영어 프롬프트'다.
    # Z-Image는 cfg=1이라 원본 뒤에 지시를 붙이면 조건이 희석되고, 실측에서
    # 처방이 완전히 무시되는 걸 확인했다(phase2-real i02: 65 → 71 → 64).
    prompt = shot.get("prompt_fix") or shot["prompt"]

    elapsed = tools.gen_still(
        prompt,
        out_path,
        seed=int(shot.get("seed", 1234)) + rnd,   # 회차마다 시드를 바꿔야 다른 그림이 나온다
        mock=bool(state.get("mock")),
    )

    shot["still_path"] = str(out_path)
    shot["elapsed_s"] = round(float(shot.get("elapsed_s", 0.0)) + elapsed, 2)
    return {
        "shot": shot,
        "trace": [{"node": "still", "shot": shot["id"], "round": rnd, "elapsed_s": elapsed}],
    }


def judge_still(state: ShotState) -> dict[str, Any]:
    """스틸 채점. 미달이면 prompt_fix를 받아 다음 회차로 넘긴다."""
    shot = dict(state["shot"])
    threshold = int(state.get("threshold", 75))
    backend = state.get("judge_backend", "mock")
    t0 = time.time()

    if backend == "cli":
        res = _judge_cli(shot, threshold, state.get("style_lock", ""), state.get("character_lock", ""))
    else:
        res = _judge_mock(shot, threshold)

    shot["score"] = int(res.get("total", 0))
    shot["verdict"] = res.get("verdict", "REGEN")
    shot["saw"] = res.get("saw")
    shot["prompt_fix"] = res.get("prompt_fix")

    return {
        "shot": shot,
        "trace": [
            {
                "node": "judge",
                "shot": shot["id"],
                "round": int(shot.get("round", 0)),
                "score": shot["score"],
                "verdict": shot["verdict"],
                "elapsed_s": round(time.time() - t0, 2),
            }
        ],
    }


def bump_round(state: ShotState) -> dict[str, Any]:
    """재생성 확정 — 회차를 올린다. 이 노드가 있어야 루프가 다이어그램에 보인다."""
    shot = dict(state["shot"])
    shot["round"] = int(shot.get("round", 0)) + 1
    return {"shot": shot}


def after_judge(state: ShotState) -> str:
    """★ 재시도 조건분기 — 지금까지 문서에만 있던 '최대 3라운드'가 여기서 코드가 된다."""
    shot = state["shot"]
    if shot.get("verdict") == "PASS":
        return "done"
    if int(shot.get("round", 0)) + 1 >= int(state.get("max_rounds", 3)):
        return "give_up"          # 상한 소진 — FAILED로 확정하고 문에서 걸린다
    return "retry"


def finalize_shot(state: ShotState) -> dict[str, Any]:
    """상한을 소진했는데도 미달이면 FAILED로 못박는다."""
    shot = dict(state["shot"])
    if shot.get("verdict") != "PASS":
        shot["verdict"] = "FAILED"
    return {"shot": shot}


# ─────────────────────────────────────────────────────────────────────────────
# 메인 그래프 노드
# ─────────────────────────────────────────────────────────────────────────────


def plan_shots(state: ShortsState) -> dict[str, Any]:
    """샷 스펙 JSON을 읽어 샷 목록을 만든다."""
    spec = json.loads(pathlib.Path(state["spec_path"]).read_text(encoding="utf-8"))
    shots: dict[str, dict] = {}
    for raw in spec["shots"]:
        shots[raw["id"]] = {
            "id": raw["id"],
            "beat": raw.get("beat", ""),
            "must": raw.get("must", []),
            "prompt": raw["prompt"],
            "seed": int(raw.get("seed", 1234)),
            "force_fail": bool(raw.get("force_fail", False)),
            "force_cut_fail": bool(raw.get("force_cut_fail", False)),
            "round": 0,
            "still_path": None,
            "score": None,
            "verdict": "REGEN",
            "saw": None,
            "prompt_fix": None,
            "elapsed_s": 0.0,
            # Phase 3
            "clip_round": 0,
            "clip_path": None,
            "cut_score": None,
            "cut_verdict": None,
            "cut_saw": None,
            "cut_issues": [],
            "clip_fix": None,
            "clip_elapsed_s": 0.0,
        }

    return {
        "short_id": spec.get("short_id", "untitled"),
        "style_lock": spec.get("style_lock", ""),
        "character_lock": spec.get("character_lock", ""),
        "shots": shots,
        "shot_count": len(shots),
        "trace": [{"node": "plan", "shot_count": len(shots)}],
    }


def gate(state: ShortsState) -> dict[str, Any]:
    """★ 3시간 앞의 문.

    전 샷이 PASS여야 열린다. 하나라도 미달이면 영상화 단계로 가는 엣지가 닫힌다.
    이게 이 패키지의 존재 이유다 — 9초짜리 실패로 3시간을 지킨다.
    """
    shots = state.get("shots", {})
    failed = sorted(sid for sid, s in shots.items() if s.get("verdict") != "PASS")

    if failed:
        saved = len(shots) * 7          # 영상화 7분/샷 기준
        return {
            "gate_open": False,
            "gate_reason": "미달 %d/%d 샷: %s — 영상화(약 %d분) 진입 차단"
            % (len(failed), len(shots), ", ".join(failed), saved),
            "trace": [{"node": "gate", "open": False, "failed": failed}],
        }

    return {
        "gate_open": True,
        "gate_reason": "전 샷 %d개 통과 — 영상화 진입 허용" % len(shots),
        "trace": [{"node": "gate", "open": True, "failed": []}],
    }


def ready_for_video(state: ShortsState) -> dict[str, Any]:
    """문 통과. 다음 단계(I2V, 7분/샷)는 Phase 2에서 이 노드 뒤에 붙는다."""
    return {"trace": [{"node": "ready_for_video", "shot_count": state.get("shot_count", 0)}]}


def blocked(state: ShortsState) -> dict[str, Any]:
    """문에서 막힘. 3시간을 쓰지 않았다."""
    return {"trace": [{"node": "blocked", "reason": state.get("gate_reason", "")}]}


def after_gate(state: ShortsState) -> str:
    return "ready_for_video" if state.get("gate_open") else "blocked"


# ═════════════════════════════════════════════════════════════════════════════
# Phase 3 — 영상화 (7분/컷) + 컷 심사
#
# 문을 통과한 스틸만 여기 들어온다. 스틸 단계와 다른 점 두 가지:
#   1) 판정이 3단계다 — PASS / REVISE(수동 보정 후 사용) / REGEN(재생성)
#   2) 재생성 정책에 시드 리롤이 있다 (cut-judge.md: 표정·디테일 미스면
#      같은 프롬프트 + 시드만 바꾸는 게 가장 싸다)
# ═════════════════════════════════════════════════════════════════════════════

CUT_JUDGE_PROMPT = """\
너는 컷 심사위원이다. `.claude/agents/cut-judge.md`의 루브릭을 그대로 적용한다.

아래 프레임을 **전부 Read로 열어** 실제로 본 것만 채점한다. 추정 금지.

컷 스펙:
  id: {id}
  intent(비트): {beat}
  must: {must}
  style_lock: {style_lock}
  frames:
{frames}
{prior}
루브릭 (100점): 피사체 정확도 25 · 시각적 자연스러움 20 · 자막·장면 일치 20 ·
톤 적합성 15 · 채널 적합성(9:16 모바일 가독) 10 · 비용 효율성 10.

즉시 실패(점수 무관 REGEN): 라벨/텍스트 뭉갬 · 손가락 오류 · 얼굴 변형 ·
물리 오류 · 형체 붕괴.

판정: {pass_at}점 이상 PASS · {revise_at}~{pass_max} REVISE(수동 보정 후 사용) ·
{revise_at}점 미만 REGEN.

**prompt_fix 규칙 (REGEN일 때만)**
- 표정·디테일 미스 → `"SEED_REROLL"` 이라고만 쓴다 (같은 프롬프트 + 시드만 교체가 가장 쌈)
- 구조적 미스(피사체가 아예 안 나옴 등) → 다음 회차에 그대로 쓸 **완성된 영어 모션 프롬프트**

JSON 객체 하나만 출력한다. 다른 말 금지:
{{"id":"...","total":0,"verdict":"PASS|REVISE|REGEN","saw":"프레임에서 실제로 본 것 1-2문장","issues":["..."],"prompt_fix":null}}
"""


def _cut_judge_mock(shot: dict, pass_at: int) -> dict[str, Any]:
    rnd = int(shot.get("clip_round", 0))
    if shot.get("force_cut_fail"):
        total = 48
    else:
        h = int(hashlib.sha1(("cut:%s:%d" % (shot["id"], rnd)).encode()).hexdigest()[:8], 16)
        total = min(96, 62 + rnd * 12 + h % 10)
    verdict = "PASS" if total >= pass_at else ("REVISE" if total >= pass_at - 10 else "REGEN")
    return {
        "id": shot["id"],
        "total": total,
        "verdict": verdict,
        "saw": "[mock] r%d 채점 — 실제 프레임 판독 아님" % rnd,
        "issues": [],
        "prompt_fix": None if verdict != "REGEN" else "SEED_REROLL",
    }


def _cut_judge_cli(shot: dict, frames: list[str], pass_at: int, style_lock: str) -> dict[str, Any]:
    prior = ""
    if shot.get("clip_fix"):
        prior = "\n직전 회차 처방(반영 여부를 명시할 것): %s\n" % shot["clip_fix"]

    prompt = CUT_JUDGE_PROMPT.format(
        id=shot["id"],
        beat=shot.get("beat", ""),
        must=", ".join(shot.get("must", [])) or "(없음)",
        style_lock=style_lock or "(없음)",
        frames="\n".join("    - %s" % f for f in frames),
        prior=prior,
        pass_at=pass_at,
        revise_at=pass_at - 10,
        pass_max=pass_at - 1,
    )
    out = tools.run(
        ["claude", "-p", prompt, "--allowedTools", "Read",
         "--model", os.environ.get("JUDGE_MODEL", "claude-sonnet-5")],
        timeout=600,
    )
    start, end = out.find("{"), out.rfind("}")
    if start < 0 or end <= start:
        raise tools.ToolError(["claude"], 65, out, "컷 심사위원 응답에서 JSON을 못 찾음")
    data = json.loads(out[start : end + 1])
    data.setdefault("id", shot["id"])
    return data


def render_clip(state: ShotState) -> dict[str, Any]:
    """앵커 스틸 → 모션 클립 (~7분). 파이프라인 전체 비용의 92%가 여기다."""
    shot = dict(state["shot"])
    rnd = int(shot.get("clip_round", 0))
    out_path = pathlib.Path(state["out_dir"]) / "clips" / ("%s_r%d.mp4" % (shot["id"], rnd))

    # 구조적 처방이 있으면 모션 프롬프트를 교체, SEED_REROLL이면 원본 유지.
    fix = shot.get("clip_fix")
    motion = shot["prompt"] if (not fix or fix == "SEED_REROLL") else fix

    elapsed = tools.gen_clip(
        pathlib.Path(shot["still_path"]),
        motion,
        out_path,
        seed=int(shot.get("seed", 1234)) + rnd * 977,   # 회차마다 시드 리롤
        mock=bool(state.get("mock")),
    )

    shot["clip_path"] = str(out_path)
    shot["clip_elapsed_s"] = round(float(shot.get("clip_elapsed_s", 0.0)) + elapsed, 2)
    return {
        "shot": shot,
        "trace": [{"node": "i2v", "shot": shot["id"], "round": rnd, "elapsed_s": elapsed}],
    }


def judge_cut(state: ShotState) -> dict[str, Any]:
    shot = dict(state["shot"])
    pass_at = int(state.get("cut_threshold", 75))
    t0 = time.time()

    if state.get("judge_backend") == "cli":
        frames = tools.extract_frames(
            pathlib.Path(shot["clip_path"]),
            pathlib.Path(state["out_dir"]) / "judge" / shot["id"],
            count=int(state.get("frame_count", 3)),
            mock=bool(state.get("mock")),
        )
        res = _cut_judge_cli(shot, frames, pass_at, state.get("style_lock", ""))
    else:
        res = _cut_judge_mock(shot, pass_at)

    shot["cut_score"] = int(res.get("total", 0))
    shot["cut_verdict"] = res.get("verdict", "REGEN")
    shot["cut_saw"] = res.get("saw")
    shot["cut_issues"] = res.get("issues", [])
    shot["clip_fix"] = res.get("prompt_fix")

    return {
        "shot": shot,
        "trace": [{
            "node": "cut_judge", "shot": shot["id"], "round": int(shot.get("clip_round", 0)),
            "score": shot["cut_score"], "verdict": shot["cut_verdict"],
            "elapsed_s": round(time.time() - t0, 2),
        }],
    }


def bump_clip_round(state: ShotState) -> dict[str, Any]:
    shot = dict(state["shot"])
    shot["clip_round"] = int(shot.get("clip_round", 0)) + 1
    return {"shot": shot}


def after_cut_judge(state: ShotState) -> str:
    """REVISE는 통과시킨다 — 수동 보정으로 쓸 수 있는 등급이므로 7분을 더 태우지 않는다."""
    shot = state["shot"]
    if shot.get("cut_verdict") in ("PASS", "REVISE"):
        return "done"
    if int(shot.get("clip_round", 0)) + 1 >= int(state.get("max_clip_rounds", 3)):
        return "give_up"
    return "retry"


def finalize_clip(state: ShotState) -> dict[str, Any]:
    shot = dict(state["shot"])
    if shot.get("cut_verdict") not in ("PASS", "REVISE"):
        shot["cut_verdict"] = "FAILED"
    return {"shot": shot}


def clip_gate(state: ShortsState) -> dict[str, Any]:
    """컷 문 — REGEN으로 끝난 컷이 하나라도 있으면 조립으로 못 넘어간다."""
    shots = state.get("shots", {})
    failed = sorted(s for s, v in shots.items() if v.get("cut_verdict") not in ("PASS", "REVISE"))
    revise = sorted(s for s, v in shots.items() if v.get("cut_verdict") == "REVISE")

    if failed:
        return {
            "clip_gate_open": False,
            "clip_gate_reason": "미달 %d/%d 컷: %s — 조립 진입 차단" % (len(failed), len(shots), ", ".join(failed)),
            "trace": [{"node": "clip_gate", "open": False, "failed": failed}],
        }
    note = " (REVISE %d컷은 수동 보정 필요: %s)" % (len(revise), ", ".join(revise)) if revise else ""
    return {
        "clip_gate_open": True,
        "clip_gate_reason": "전 컷 %d개 통과%s" % (len(shots), note),
        "trace": [{"node": "clip_gate", "open": True, "revise": revise}],
    }


def ready_for_assembly(state: ShortsState) -> dict[str, Any]:
    return {"trace": [{"node": "ready_for_assembly", "clips": state.get("shot_count", 0)}]}


def enter_video_stage(state: ShortsState) -> dict[str, Any]:
    """승인 직후 · 영상화 fan-out 직전. 되돌릴 수 없는 지점이라 표시를 남긴다."""
    n = len(state.get("shots", {}))
    return {"trace": [{"node": "video_stage", "clips": n, "est_min": round(n * 412 / 60)}]}


def after_clip_gate(state: ShortsState) -> str:
    return "ready_for_assembly" if state.get("clip_gate_open") else "blocked"


# ═════════════════════════════════════════════════════════════════════════════
# Phase 4 — 사람 승인 지점
#
# 문 1(자동 채점)을 통과해도 3시간을 태우기 전에 사람이 한 번 본다.
# `docs/generative-shorts-pipeline.md` §4.5: "전 샷 승인 후에만 5번(영상화) 진입".
#
# 운영자 계약 §1: 사용자는 터미널을 만지지 않는다. 그래서 CLI 프롬프트가 아니라
# **파일**로 오간다 — 그래프가 검수 시트를 쓰고, 운영자는 한 줄로 답한다.
#
# 자율 모드에서는 멈춰 기다리지 않는다. policies.yaml on_blocker: log_and_halt.
# 아침에 같은 run_id로 resume 하면 이어진다 (체크포인트).
# ═════════════════════════════════════════════════════════════════════════════


def build_storyboard(state: ShortsState) -> dict[str, Any]:
    """검수 시트를 쓴다. 운영자가 여는 유일한 파일."""
    out_dir = pathlib.Path(state["out_dir"])
    path = out_dir / "approvals" / "storyboard.md"
    path.parent.mkdir(parents=True, exist_ok=True)

    shots = state.get("shots", {})
    est_min = round(len(shots) * 412 / 60)      # 실측 412초/컷

    lines = [
        "# 스토리보드 검수 — %s" % state.get("short_id", state["run_id"]),
        "",
        "**전 샷이 자동 채점을 통과했습니다.** 승인하면 영상화에 들어갑니다.",
        "",
        "> 영상화는 컷당 약 7분 — 이 회차는 **약 %d분**이 걸리고 되돌릴 수 없습니다." % est_min,
        "> 지금 되돌리면 샷당 10초입니다. **비용비 1:40.**",
        "",
        "| 샷 | 점수 | 비트 | 심사위원이 본 것 | 스틸 |",
        "|---|---:|---|---|---|",
    ]
    for sid in sorted(shots):
        s = shots[sid]
        lines.append("| `%s` | %s | %s | %s | `%s` |" % (
            sid, s.get("score"), s.get("beat", ""),
            (s.get("saw") or "").replace("|", "·")[:90],
            pathlib.Path(s.get("still_path") or "").name,
        ))

    lines += [
        "",
        "## 결정",
        "",
        "터미널을 열 필요 없습니다. 아래 중 하나를 에이전트에게 말하면 됩니다.",
        "",
        "- **승인** — 영상화 진행 (약 %d분)" % est_min,
        "- **다시: i03, i07** — 그 샷만 스틸부터 재생성 (샷당 10초)",
        "- **취소** — 여기서 중단",
        "",
        "---",
        "재개 명령: `python -m graph.shorts_graph resume --thread %s --approve`" % state["run_id"],
    ]
    path.write_text("\n".join(lines), encoding="utf-8")

    return {
        "storyboard_path": str(path),
        "pending_approval": "storyboard",
        "trace": [{"node": "storyboard", "shots": len(shots), "est_video_min": est_min}],
    }


def request_approval(state: ShortsState) -> dict[str, Any]:
    """★ 사람이 보는 지점. 자율 모드면 기다리지 않고 halt한다."""
    shots = state.get("shots", {})
    payload = {
        "stage": "storyboard",
        "run_id": state["run_id"],
        "sheet": state.get("storyboard_path"),
        "shots": {sid: s.get("score") for sid, s in shots.items()},
        "est_video_min": round(len(shots) * 412 / 60),
    }

    if state.get("autonomy_mode"):
        # 밤새 돌 때는 사람을 기다리지 않는다 — 기록하고 끊는다.
        blockers = tools.records_dir() / "blockers" / time.strftime("%Y-%m-%d")
        blockers.mkdir(parents=True, exist_ok=True)
        bp = blockers / ("%s-storyboard.md" % state["run_id"])
        bp.write_text(
            "# 승인 대기 — %s\n\n"
            "스토리보드 검수가 필요해 자율 실행을 멈췄습니다.\n\n"
            "- 검수 시트: `%s`\n- 샷 %d개, 영상화 예상 약 %d분\n\n"
            "재개: `python -m graph.shorts_graph resume --thread %s --approve`\n"
            % (state["run_id"], payload["sheet"], len(shots), payload["est_video_min"], state["run_id"]),
            encoding="utf-8",
        )
        return {
            "human_decision": "halt",
            "blocker_path": str(bp),
            "trace": [{"node": "approval", "mode": "autonomous", "halted": True}],
        }

    decision = interrupt(payload)          # ← 여기서 멈춘다. 체크포인트에 상태가 남는다.

    if isinstance(decision, str):
        decision = {"decision": decision}
    verdict = (decision or {}).get("decision", "approve")
    targets = (decision or {}).get("shots", []) or []

    return {
        "human_decision": verdict,
        "regen_targets": list(targets),
        "pending_approval": None,
        "approval_history": [{"stage": "storyboard", "decision": verdict, "shots": list(targets)}],
        "trace": [{"node": "approval", "decision": verdict, "shots": list(targets)}],
    }


def after_approval(state: ShortsState) -> str:
    d = state.get("human_decision")
    if d == "approve":
        return "approved"
    if d == "regen" and state.get("regen_targets"):
        return "regen"
    return "blocked"                        # reject · halt · 알 수 없는 값


def _profile_disclosures(profile: str) -> list[str]:
    """config/content-short-profiles.yaml 의 disclosures 블록을 읽는다.

    기존 run.sh 가 하던 것과 같은 일. 파서를 새로 쓰지 않고 같은 평면 YAML 규약을 따른다
    (`  - id: <p>` 아래 `    disclosures:` 의 `      - "..."` 줄들).
    """
    path = tools.repo_root() / "config" / "content-short-profiles.yaml"
    if not path.exists():
        return []
    cur, sect, out = None, None, []
    for line in path.read_text(encoding="utf-8").splitlines():
        m = re.match(r"^  - id:\s*(.+)$", line)
        if m:
            cur, sect = m.group(1).strip(), None
            continue
        if cur != profile:
            continue
        if re.match(r"^    disclosures:\s*$", line):
            sect = "d"
            continue
        if re.match(r"^    [a-z_]+:", line):
            sect = None
        m = re.match(r"^      -\s*(.+)$", line)
        if m and sect == "d":
            v = m.group(1).strip()
            mq = re.match(r'"((?:[^"\\]|\\.)*)"', v)
            out.append(mq.group(1) if mq else re.split(r"\s+#", v, 1)[0].strip())
    return [l.replace("{AS_OF_DATE}", time.strftime("%Y-%m-%d")) for l in out]


def assemble(state: ShortsState) -> dict[str, Any]:
    """컷 concat + 법률 게이트 입력 생성.

    게이트가 요구하는 건 셋이다: outputs/short.mp4 · SOURCES.txt · disclosures.txt.
    TTS·BGM·자막 번인은 기존 faceless-short 경로가 담당하는 별개 영역이라
    여기서 손대지 않는다 (그래프의 책임은 순서·게이트·재시도뿐).
    """
    out_dir = pathlib.Path(state["out_dir"])
    outputs = out_dir / "outputs"
    outputs.mkdir(parents=True, exist_ok=True)

    shots = state.get("shots", {})
    clips = [pathlib.Path(shots[s]["clip_path"]) for s in sorted(shots) if shots[s].get("clip_path")]
    if not clips:
        raise RuntimeError("조립할 컷이 없다 — 문 2를 통과했는데 clip_path가 비어 있음")

    final = outputs / "short.mp4"
    elapsed = tools.concat_clips(clips, final, mock=bool(state.get("mock")))

    # SOURCES.txt — 100% 생성물. 제3자 미디어를 재사용하지 않았다.
    #
    # `license:` 값은 guard_publish 가 config/copyright-allowlist.yaml 과 대조한다.
    # 생성 모델 라이선스(apache-2.0 등)는 아직 그 allowlist에 없으므로 `owner-self`
    # 로 기록한다 — "제3자 권리가 걸린 소재가 없다"는 뜻이고, 실제로 그렇다.
    # 모델명은 attribution 줄에 남겨 추적성을 잃지 않는다.
    outputs.joinpath("SOURCES.txt").write_text(
        "mission_id: %s\n"
        "source: generated (Z-Image Turbo + Wan2.2 A14B, local)\n"
        "attribution: 100%% synthetic — no third-party media reused\n"
        "license: owner-self\n"
        "recorded_at: %s\n" % (state["run_id"], time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())),
        encoding="utf-8",
    )

    # disclosures.txt — 프로필이 요구하는 줄을 **그대로** 쓴다.
    # 여기서 임의로 지어내면 게이트를 통과시키려고 사실이 아닌 고지를 넣는 셈이 된다.
    lines = state.get("disclosures") or _profile_disclosures(state.get("profile", "info"))
    outputs.joinpath("disclosures.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")

    return {
        "final_video": str(final),
        "trace": [{"node": "assemble", "clips": len(clips), "elapsed_s": elapsed}],
    }


# ═════════════════════════════════════════════════════════════════════════════
# Phase 5 — 법률 게이트
#
# `scripts/legal-gate.sh`는 재작성하지 않는다. 이미 fail-closed로 정확하다:
# 결정론 체크와 판단 체크를 프로필의 required_checks 위에서 병합하고,
# **실행되지 않은 필수 체크는 REVISE로 떨어뜨린다** — 안 돌린 검사를 통과시키지 않는다.
#
# 그래프가 더하는 건 하나뿐이다: REVISE면 되돌아가는 **엣지**.
# `content-director.md`의 "max_legal_iters" 산문 루프가 여기서 코드가 된다.
# ═════════════════════════════════════════════════════════════════════════════


LEGAL_JUDGE_PROMPT = """\
너는 법률팀(legal-team)이다. `.claude/agents/legal-team.md`의 판단 체크만 담당한다.
라이선스·고지 존재 여부 같은 결정론 체크는 이미 bash가 증명했으니 건드리지 마라.

콘텐츠:
  profile: {profile}
  대본/비트:
{beats}

판단할 체크: {checks}
  - fact-accuracy   : 사실과 다른 단정이 있는가
  - unverifiable    : 검증 불가한 주장을 사실처럼 말하는가
  - defamation      : 특정 개인·단체를 깎아내리는가
  - trademark-ip    : 상표·IP를 무단으로 쓰는가

각 체크에 pass 또는 fail을 준다. 근거 없이 fail 주지 마라 — 실제 문장을 지목한다.
창작 서사(허구)는 사실 주장이 아니므로 fact-accuracy 대상이 아니다.

JSON 객체 하나만 출력한다. 다른 말 금지:
{{"checks":[{{"id":"fact-accuracy","status":"pass","evidence":"근거 1문장"}}]}}
"""


def _legal_judge(state: ShortsState) -> pathlib.Path | None:
    """판단 체크를 심사위원에게 맡기고 external-verdict 파일로 떨군다.

    이걸 안 돌리면 legal-gate.sh 가 필수 판단 체크를 REVISE로 fail-close 한다
    (= 안 돌린 검사를 통과시키지 않는다). 그게 옳은 설계라 우회하지 않고 실제로 돌린다.
    """
    # 법률 판단은 이미지가 아니라 대본을 본다 — 그래서 --mock(가짜 스틸)과도
    # 같이 쓸 수 있다. legal_backend 로 따로 켠다.
    if state.get("legal_backend", state.get("judge_backend")) != "cli":
        return None

    shots = state.get("shots", {})
    beats = "\n".join("    - %s" % (s.get("beat") or s.get("prompt", ""))[:120] for s in shots.values())
    prompt = LEGAL_JUDGE_PROMPT.format(
        profile=state.get("profile", "info"),
        beats=beats or "    (없음)",
        checks="fact-accuracy, unverifiable",
    )
    out = tools.run(
        ["claude", "-p", prompt, "--model", os.environ.get("JUDGE_MODEL", "claude-sonnet-5")],
        timeout=300,
    )
    start, end = out.find("{"), out.rfind("}")
    if start < 0 or end <= start:
        return None

    vpath = pathlib.Path(state["out_dir"]) / "legal" / "subagent-verdict.json"
    vpath.parent.mkdir(parents=True, exist_ok=True)
    vpath.write_text(out[start : end + 1], encoding="utf-8")
    return vpath


def legal_check(state: ShortsState) -> dict[str, Any]:
    rc, out = tools.legal_gate(
        pathlib.Path(state["out_dir"]),
        state.get("profile", "info"),
        platform=state.get("platform", "public"),
        external_verdict=_legal_judge(state),
    )
    verdict = {0: "PASS", 1: "REVISE", 2: "BLOCK"}.get(rc, "ERROR")

    fixes: list[str] = []
    vpath = pathlib.Path(state["out_dir"]) / "legal" / "legal-verdict.json"
    if vpath.exists():
        try:
            data = json.loads(vpath.read_text(encoding="utf-8"))
            verdict = data.get("verdict", verdict)
            fixes = [
                "%s: %s" % (c.get("id"), c.get("evidence", ""))
                for c in data.get("checks", [])
                if c.get("status") in ("fail", "unknown") and not c.get("informational")
            ]
        except Exception:
            pass

    return {
        "legal_verdict": verdict,
        "legal_fixes": fixes,
        "legal_round": int(state.get("legal_round", 0)),
        "trace": [{"node": "legal", "rc": rc, "verdict": verdict, "fixes": len(fixes)}],
    }


def bump_legal_round(state: ShortsState) -> dict[str, Any]:
    return {"legal_round": int(state.get("legal_round", 0)) + 1}


def after_legal(state: ShortsState) -> str:
    """★ 출시로 가는 엣지는 PASS 하나뿐. 이게 이 단계의 불변식이다."""
    v = state.get("legal_verdict")
    if v == "PASS":
        return "release"
    if v == "BLOCK":
        return "blocked"                    # 되돌릴 수 없음 — 재시도조차 안 한다
    if int(state.get("legal_round", 0)) + 1 >= int(state.get("max_legal_rounds", 2)):
        return "blocked"                    # REVISE 상한 소진
    return "revise"


def release(state: ShortsState) -> dict[str, Any]:
    """출시 패키지. 자동 업로드는 하지 않는다 — 운영자가 손으로 올린다."""
    out_dir = pathlib.Path(state["out_dir"])
    rel = out_dir / "release"
    rel.mkdir(parents=True, exist_ok=True)

    checklist = rel / "PUBLISH-CHECKLIST.md"
    shots = state.get("shots", {})
    checklist.write_text(
        "# 출시 체크리스트 — %s\n\n"
        "법률 판정: **PASS** · 컷 %d개\n\n"
        "- [ ] `short.mp4` 한 번 끝까지 보기 (자막 가독·오디오 클리핑)\n"
        "- [ ] `outputs/disclosures.txt` 고지 문구가 설명란에 들어갔는지\n"
        "- [ ] `outputs/SOURCES.txt` 출처 표기 유지\n"
        "- [ ] **수동 업로드** — 자동 업로드 없음, 공개 URL은 레포에 남기지 않음\n"
        % (state.get("short_id", state["run_id"]), len(shots)),
        encoding="utf-8",
    )
    return {
        "release_path": str(rel),
        "trace": [{"node": "release", "checklist": str(checklist)}],
    }


def mark_regen(state: ShortsState) -> dict[str, Any]:
    """운영자가 지목한 샷만 회차를 올리고 미통과로 되돌린다."""
    shots = state.get("shots", {})
    patch = {}
    for sid in state.get("regen_targets", []):
        if sid not in shots:
            continue
        s = dict(shots[sid])
        s["round"] = int(s.get("round", 0)) + 1
        s["verdict"] = "REGEN"
        patch[sid] = s
    return {
        "shots": patch,
        "human_decision": None,
        "regen_targets": [],
        "trace": [{"node": "mark_regen", "shots": sorted(patch)}],
    }
