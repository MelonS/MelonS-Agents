# -*- coding: utf-8 -*-
"""fun-score.py — 한 세션의 '재미'를 100점으로 채점한다.

계기 (2026-08-02 운영자): "유저의 재미를 점수화 시켜서 재미에 대한 점수를 올려야
할 거 같아."

이 레포는 톤(`check-art-tone.py`)과 에셋 드리프트(`check-asset-drift.py`)를 이미
수치로 판정한다.  재미만 감상으로 남아 있었고, 그래서 **같은 문제를 세 번 다른
말로** 진단했다 — 7/30 "영상 2점", 7/31 "읽히지 않는다", 8/02 "10분간 숫자가 안
움직인다".  셋 다 같은 것이다.  잴 수 있으면 한 번에 보이고, 무엇을 고쳤을 때
올라갔는지도 보인다.

── 채점 대상은 '심사자의 10분' ────────────────────────────────────────────
게임잼·페스티벌 심사 통설은 엔트리당 5~15분이고 첫인상은 ~5분이다.  그래서 이
루브릭은 **장기 밸런스가 아니라 첫 10분**을 본다.  30시간 뒤에 재미있는 것은
여기서 0점이다 — 심사자는 그걸 볼 수 없다.

── 축과 배점 ──────────────────────────────────────────────────────────────
  진행감  30  마을이 눈에 띄게 자라는가 (목표·구조물·가치)
  사건    25  손대지 않아도 무슨 일이 일어나는가 (사건 수 / 최장 침묵)
  활력    20  주민이 놀고 있지 않은가 (유휴 비율 / 동시 활동 종류)
  긴장    15  위협이 오고 해소되는가
  다양성  10  서로 다른 활동이 몇 가지나 보였는가

배점 근거: 운영자가 실제로 지적한 순서다.  "숫자가 안 움직인다"(진행감)와
"찍을 사건이 존재하지 않았다"(사건)가 두 번의 탈락 판정 사유였고, "뭐 하는지
모르겠다"(활력·다양성)가 그 다음이었다.

usage:
  python fun-score.py <telemetry.jsonl> [--json]
"""
from __future__ import annotations
import sys
import json

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


# ── 목표선 ────────────────────────────────────────────────────────────────
# "이 정도면 만점" 을 명시한다.  근거 없는 곡선을 쓰면 점수가 올라도 왜 올랐는지
# 말할 수 없다.  값은 전부 10분 창 기준.
TARGETS = {
    "objectives_done": 3,      # 정착 목표 4개 중 3개
    "structures_gain": 6,      # 새 구조물 6개 (방 하나 증축 정도)
    "value_growth": 2.5,       # 가치 2.5배
    "events": 8,               # 사건 8건
    "max_silence_sec": 90,     # 최장 무사건 구간 90초 이하
    "idle_ratio": 0.15,        # 유휴 15% 이하
    "act_kinds_avg": 3.0,      # 동시에 3종류 이상이 벌어진다
    "threat_cycles": 1,        # 위협 발생→해소 1회
    "act_variety": 8,          # 세션 통틀어 8가지 활동
}

WEIGHTS = {"진행감": 30, "사건": 25, "활력": 20, "긴장": 15, "다양성": 10}


def clamp01(x):
    return max(0.0, min(1.0, x))


def load(path):
    rows = []
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError:
                pass          # 마지막 줄이 잘렸을 수 있다 (크래시 시)
    return rows


def score(rows):
    if len(rows) < 2:
        return None, "표본이 2개 미만 — 세션이 너무 짧다"

    first, last = rows[0], rows[-1]
    dur = max(1e-3, last["t"] - first["t"])
    mins = dur / 60.0

    # ── 진행감 ────────────────────────────────────────────────────────────
    obj = last["objectives"] - first["objectives"]
    st = last["structures"] - first["structures"]
    v0 = max(1.0, first["value"])
    vg = last["value"] / v0
    p_obj = clamp01(obj / TARGETS["objectives_done"])
    p_st = clamp01(st / TARGETS["structures_gain"])
    p_v = clamp01((vg - 1.0) / (TARGETS["value_growth"] - 1.0))
    prog = (p_obj * 0.45 + p_st * 0.30 + p_v * 0.25)

    # ── 사건 ──────────────────────────────────────────────────────────────
    ev_times = [r["t"] for r in rows if r.get("events")]
    n_ev = sum(len(r.get("events", [])) for r in rows)
    # 최장 침묵 — 사건 사이 최대 간격 (시작·끝도 경계로 센다).
    marks = [first["t"]] + ev_times + [last["t"]]
    silence = max(b - a for a, b in zip(marks, marks[1:])) if len(marks) > 1 else dur
    p_n = clamp01((n_ev / max(mins, 1e-3)) / (TARGETS["events"] / 10.0))
    p_s = clamp01(TARGETS["max_silence_sec"] / max(silence, 1e-3))
    events = p_n * 0.6 + p_s * 0.4

    # ── 활력 ──────────────────────────────────────────────────────────────
    idle_r = sum(r["idle"] / max(1, r["pawns"]) for r in rows) / len(rows)
    kinds = sum(r["actKinds"] for r in rows) / len(rows)
    p_idle = clamp01((1.0 - idle_r) / (1.0 - TARGETS["idle_ratio"]))
    p_kinds = clamp01(kinds / TARGETS["act_kinds_avg"])
    live = p_idle * 0.6 + p_kinds * 0.4

    # ── 긴장 ──────────────────────────────────────────────────────────────
    cycles = sum(1 for r in rows if "threat_clear" in r.get("events", []))
    started = sum(1 for r in rows if "threat_start" in r.get("events", []))
    # 발생만 하고 해소가 없으면 절반만 준다 (해소 없는 위협은 스트레스지 재미가 아니다).
    tension = clamp01(cycles / TARGETS["threat_cycles"]) if cycles else \
              (0.5 * clamp01(started / TARGETS["threat_cycles"]))

    # ── 다양성 ────────────────────────────────────────────────────────────
    variety = clamp01(last["actEver"] / TARGETS["act_variety"])

    axes = {"진행감": prog, "사건": events, "활력": live,
            "긴장": tension, "다양성": variety}
    detail = {
        "진행감": f"목표 +{obj} / 구조물 +{st} / 가치 x{vg:.2f}",
        "사건": f"{n_ev}건 ({n_ev/max(mins,1e-3):.1f}/분), 최장 침묵 {silence:.0f}초",
        "활력": f"유휴 {idle_r*100:.0f}%, 동시 활동 {kinds:.1f}종",
        "긴장": f"위협 발생 {started} / 해소 {cycles}",
        "다양성": f"활동 {last['actEver']}가지",
    }
    total = sum(axes[k] * WEIGHTS[k] for k in WEIGHTS)
    return {"total": total, "axes": axes, "detail": detail,
            "minutes": mins, "samples": len(rows)}, None


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    path = sys.argv[1]
    rows = load(path)
    res, err = score(rows)
    if err:
        print(f"[fun] {err}")
        return 1

    if "--json" in sys.argv:
        print(json.dumps(res, ensure_ascii=False, indent=2))
        return 0

    print(f"[fun] {res['minutes']:.1f}분 / 표본 {res['samples']}개")
    print(f"[fun] ── 재미 점수  {res['total']:.1f} / 100 ──")
    for k, w in WEIGHTS.items():
        got = res["axes"][k] * w
        bar = "█" * int(round(got / w * 20)) + "·" * (20 - int(round(got / w * 20)))
        print(f"  {k:4s} {got:5.1f}/{w:<3d} {bar}  {res['detail'][k]}")
    worst = min(WEIGHTS, key=lambda k: res["axes"][k])
    lost = (1 - res["axes"][worst]) * WEIGHTS[worst]
    print(f"[fun] 가장 크게 잃는 축: **{worst}** (−{lost:.1f}점)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
