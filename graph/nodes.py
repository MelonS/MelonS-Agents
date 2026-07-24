"""노드 구현.

각 노드는 "상태 일부를 읽어서 상태 일부를 돌려주는 함수"다. 부작용(파일 생성,
외부 프로세스)은 전부 `tools.py`를 통한다.
"""

from __future__ import annotations

import hashlib
import json
import os
import pathlib
import time
from typing import Any

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


def after_clip_gate(state: ShortsState) -> str:
    return "ready_for_assembly" if state.get("clip_gate_open") else "blocked"
