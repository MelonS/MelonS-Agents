# graph/ — 쇼츠 파이프라인 실행 엔진 (LangGraph)

> **한 줄:** 3시간짜리 영상화 단계 앞에 9초짜리 문을 세운다.

## 왜 이게 있는가

한 편을 만드는 데 3시간이 걸린다. 그런데 시간이 어디로 가는지 보면:

| 단계 | 비용 | 누적 |
|---|---:|---:|
| 대본·기획 | 몇 분 | — |
| **스틸 26장** | **9초 × 26 ≈ 4분** | 5분 |
| 스토리보드 | 즉시 | 5분 |
| **영상화 26컷** | **7분 × 26 ≈ 3시간** | **3시간** |
| 조립·자막 | 몇 분 | 3시간 |

**3시간 중 2시간 55분이 마지막 한 단계**이고, 그 앞은 다 합쳐 5분이다.
즉 **5분짜리로 전부 검증한 뒤에 3시간을 쓸 수 있다.**

[`docs/generative-shorts-pipeline.md`](../docs/generative-shorts-pipeline.md) §4.5는 이미
그렇게 하라고 적어두고 있다:

> **"싼 단계에서 실패시켜라: REGEN은 스틸(9초)에서, 영상(7분)에서 하지 않는다"**
> **"75 미만은 prompt_fix를 반영해 그 샷만 자동 재생성 (최대 3라운드)"**
> **"전 샷 승인 후에만 5번(영상화) 진입"**

정답이 다 적혀 있었다. 문제는 그게 **문서**라서 사람이 매번 기억해 손으로 지켜야
했다는 것이고, 한 번 건너뛰면 3시간이 날아갔다. 이 패키지는 그 규칙을 **코드**로
만든다. 전 샷이 임계 점수를 넘지 못하면 영상화로 가는 엣지 자체가 열리지 않는다.

## 구조

아래 그림은 손으로 그린 게 아니라 그래프에서 뽑은 것이다 —
`python -m graph.shorts_graph diagram` 을 돌리면 언제든 현재 코드 기준으로 다시 나온다.
**그래서 낡지 않는다.**

### 메인 — 계획 → 샷 병렬 → 문

```mermaid
graph TD;
	__start__([__start__]):::first
	plan(plan)
	render_shot(render_shot)
	gate(gate)
	ready_for_video(ready_for_video)
	blocked(blocked)
	__end__([__end__]):::last
	__start__ --> plan;
	plan -.fan-out 샷별.-> render_shot;
	render_shot --> gate;
	gate -.전 샷 PASS.-> ready_for_video;
	gate -.하나라도 미달.-> blocked;
	ready_for_video --> __end__;
	blocked --> __end__;
	classDef default fill:#f2f0ff,line-height:1.2
	classDef first fill-opacity:0
	classDef last fill:#bfb6fc
```

### 샷 하나 — 생성 → 채점 → 재시도

```mermaid
graph TD;
	__start__([__start__]):::first
	still(still)
	judge(judge)
	bump_round(bump_round)
	finalize(finalize)
	__end__([__end__]):::last
	__start__ --> still;
	still --> judge;
	judge -. retry (75 미달, 회차 남음) .-> bump_round;
	judge -. done / give_up .-> finalize;
	bump_round --> still;
	finalize --> __end__;
	classDef default fill:#f2f0ff,line-height:1.2
	classDef first fill-opacity:0
	classDef last fill:#bfb6fc
```

## 쓰는 법

```bash
# 설치 (한 번)
python -m venv .venv
.venv/Scripts/python -m pip install -r graph/requirements.txt   # Windows
.venv/bin/python     -m pip install -r graph/requirements.txt   # macOS/Linux

# 배선 검증 — GPU·ComfyUI 없이 그래프만 돈다
.venv/Scripts/python -m graph.shorts_graph run \
    --spec graph/examples/shots.example.json --mock --thread demo

# 실제 실행 — ComfyUI가 떠 있어야 한다 (COMFYUI_URL, 기본 127.0.0.1:8188)
.venv/Scripts/python -m graph.shorts_graph run \
    --spec my-shots.json --judge cli --thread ep12

# 구조도
.venv/Scripts/python -m graph.shorts_graph diagram
```

**종료 코드:** `0` 문 통과(영상화 가능) · `2` 문에서 차단 · `1` 오류.
CI나 배치 스크립트에서 `|| exit` 로 그대로 물릴 수 있다.

**이어서 하기:** `--thread` 에 같은 값을 주면 체크포인트에서 재개한다.
26컷 중 19컷에서 죽어도 남은 7컷만 돈다. 처음부터 다시 하려면 `--restart`.

### 샷 스펙

[`examples/shots.example.json`](examples/shots.example.json) 참조. 필드는
`.claude/agents/still-judge.md` 의 입력 계약을 그대로 따른다.

```json
{
  "short_id": "ep12",
  "style_lock": "cinematic night, luminous not muddy, ...",
  "character_lock": "young traveler, dark robe #2B2E4A, ...",
  "shots": [
    { "id": "i01", "beat": "산길에 밤이 내린다",
      "must": ["좁은 산길", "안개"], "prompt": "...", "seed": 1101 }
  ]
}
```

## 심사위원 백엔드

| `--judge` | 동작 | 비용 |
|---|---|---|
| `mock` (기본) | 결정론 가짜 채점. 회차가 오를수록 점수 상승 | 없음 |
| `cli` | `claude` CLI 헤드리스 호출 → `still-judge` 루브릭 채점 | Max 쿼터 |

기본값이 `mock`인 이유는 [`config/policies.yaml`](../config/policies.yaml)의 머니
방화벽이다 — 모델 호출은 **명시적으로 켜야** 일어난다.

`cli` 백엔드는 운영자 머신의 `claude` 바이너리에 의존한다. 첫 사용 전 한 번
수동 확인할 것.

## 안 건드리는 것

그래프는 **아무것도 직접 만들지 않는다.** 순서·재시도·게이트만 책임진다.
외부 프로세스 호출은 전부 [`tools.py`](tools.py) 한 파일을 지나간다.

- `scripts/zimage-still.py` — 스틸 생성 (그대로 호출)
- `scripts/legal-gate.sh` — 법률 게이트 (Phase 3에서 그대로 호출)
- `agents/missions/*/run.sh`, `agents/lib/*.sh` — 무변경
- `.claude/agents/*.md` — 무변경 (operator-contract §5: 로직 변경은 명시 승인)

## 검증된 것

`--mock` 으로 확인한 항목:

| 확인 | 결과 |
|---|---|
| 스틸 6장 무인 완주 | ✅ 재시도 r2~r3 발생 후 전 샷 통과, 문 열림, exit 0 |
| 문이 실제로 막는가 | ✅ 1개 샷 미달 → `exit 2`, 영상화 진입 차단 |
| 재시도 상한 | ✅ 3회 소진 후 FAILED 확정 (무한루프 없음) |
| 체크포인트 재개 | ✅ 산출물 지우고 같은 `--thread` 재실행 → 0.0초, 재작업 0건 |
| 구조도 자동 생성 | ✅ `diagram` 이 현재 코드 기준 mermaid 출력 |

**아직 안 한 것:** 실제 ComfyUI 연결(`--mock` 없이), `--judge cli` 실물 채점.
둘 다 운영자 머신에서 한 번 돌려봐야 한다.

## 다음 (Phase 2~)

이 문 **뒤에** 붙는다. 앞은 안 건드린다.

1. **I2V** — `wan-a14b-i2v.py` 를 같은 fan-out 모양으로. 문을 통과한 샷만 들어온다.
2. **HITL** — 스토리보드 승인을 `interrupt()` 로. 지금은 자동 채점만 있고 사람 승인 지점이 없다.
3. **법률 루프** — `legal-gate.sh` PASS/REVISE/BLOCK 을 조건부 엣지로 (`content-director.md` 의 산문 루프 승격).

## 설계 메모

- **Windows 함정:** 레포의 bash 스크립트는 `python3` 를 쓰는데, Windows에서
  `python3` 는 Microsoft Store 스텁이라 조용히 아무 일도 안 한다.
  여기서는 항상 `sys.executable` 을 쓴다.
- **체크포인트 위치:** `$RECORDS_DIR/graph/checkpoints.sqlite`.
  체크포인트도 데이터이므로 코드/데이터 분리 규칙에 따라 gitignore 대상.
- **리듀서:** 병렬 노드가 동시에 쓸 수 있는 필드는 `Annotated[..., reducer]` 가
  붙은 것뿐이다 (`state.py`). `.claude/whiteboard.json` 의 "병렬 에이전트는 공유
  파일을 직접 쓰지 않는다" 규칙과 같은 얘기인데, 여기서는 타입으로 강제된다.
