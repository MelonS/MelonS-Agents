# LangGraph 도입 계획

> **다음 세션은 이 파일만 읽으면 됩니다.** 대화를 되짚지 마세요 — 그게 토큰 절약의 핵심입니다.
> 대시보드(그림 포함): https://claude.ai/code/artifact/1401f730-b7b2-48b4-827f-ffcc433b7ad5

## 왜 하는가

한 편에 3시간. 그중 **2시간 55분이 마지막 한 단계**(영상화 7분/컷 × 26컷)이고,
그 앞은 다 합쳐 5분이다. 즉 **5분짜리로 전부 검증한 뒤 3시간을 쓸 수 있다.**

`docs/generative-shorts-pipeline.md` §4.5에 이미 그렇게 하라고 적혀 있다
("싼 단계에서 실패시켜라", "75 미만은 그 샷만 최대 3라운드", "전 샷 승인 후에만 영상화").
문제는 그게 **문서**라서 사람이 매번 기억해 지켜야 했고, 한 번 건너뛰면 3시간이 날아갔다는 것.

**LangGraph는 그 규칙을 코드로 만든다.** 부수 효과로 그래프 정의가 곧 구조도가 되어
(`draw_mermaid()`) `docs/architecture.md`처럼 낡지 않는다.

## 현재 상태 (2026-07-25)

- **Phase 1 완료** — 커밋 `43d841a`, 브랜치 `langgraph-migration`
- `graph/` 패키지 9파일. 상세는 [`graph/README.md`](../graph/README.md)
- mock 검증 4/4: 무인 완주 · 문 차단(exit 2) · 재시도 상한 · 체크포인트 재개
- **미검증**: 실제 ComfyUI 연결, `--judge cli` 실물 채점

```bash
# 배선 확인 (모델 호출 0)
.venv/Scripts/python -m graph.shorts_graph run --spec graph/examples/shots.example.json --mock --thread demo
.venv/Scripts/python -m graph.shorts_graph diagram
```

## 작업 순서

| | 할 일 | 시간 | 비고 |
|---|---|---|---|
| 1 | **whiteboard 복구** | 10분 | 지금 깨져 있음 (아래 참조) |
| 2 | **Phase 2 — 실물 한 회차** | 2~3시간 | 이 계획 전체의 분기점 |
| 3 | 진입 마법사 최소판 | 1~2시간 | 승인 불필요 |
| 4 | 게임 플러그인 등록 | 제품화 때 | |
| 5 | 프로필 로더 | 제품화 때 | §5 승인 대상 |

### Phase 로드맵

| # | 내용 | 상태 | 개발 토큰 |
|---|---|---|---|
| 01 | 스틸 게이트 (생성→채점→재시도→문) | **완료** | ~250K |
| 02 | 실물 연결 — ComfyUI + `claude` CLI 채점 | 다음 | ~60K |
| 03 | 영상화 fan-out + cut-judge | 대기 | ~90K |
| 04 | 사람 승인 (`interrupt()`) 2지점 | 대기 | ~70K |
| 05 | 법률 루프 (`legal-gate.sh` 조건부 엣지) | 대기 | ~90K |
| 06 | 게임 라인 (뮤텍스 + 상태 병합) | 대기 | ~110K |

**Phase 2 완료 조건:** 실제 스틸이 ComfyUI에서 나오고, 심사위원이 이미지를 실제로 보고
점수를 매기고, 미달 샷이 처방을 반영해 다시 생성되어 점수가 오른다.

```bash
.venv/Scripts/python -m graph.shorts_graph run \
    --spec graph/examples/shots.example.json --judge cli --thread real-01
```

## 두 라인은 모양이 다르다

| | 쇼츠 | 게임 |
|---|---|---|
| 병목 | 영상화 175분 | Unity 배타 자원 |
| 실패 비용 | **3시간** | 빌드 몇 분 |
| 그래프 모양 | fan-out → **문** | fan-out → **뮤텍스** |
| 가장 큰 위험 | 3시간 소각 | **거짓 검증** (날짜 스탬프 빌드 폴더) |
| 우선순위 | 1순위 | 2순위 |

## 토큰 — 아껴야 할 건 개발이 아니라 실행

| | 1회 | 30편 |
|---|---:|---:|
| 개발 (남은 5단계) | ~420K | ~420K |
| **실행 (편당)** | **~375K** | **~11M** |

30편 기준 개발은 전체의 3%. **관리 대상은 편당 실행 비용이다.**

실행 내역: still-judge 52회 ~155K · **cut-judge 26회 ~200K** · 법률/기획 ~20K.
가장 큰 덩어리는 cut-judge (컷당 프레임 5장을 다 봄).

**절감 레버 3개**
1. cut-judge 프레임 5장 → 3장 (편당 ~80K)
2. 심사위원을 Sonnet으로 (창 소모가 가벼움)
3. mock으로 배선 먼저 (그 회차 모델 호출 0)

**개발 토큰 절감**
- **한 세션에 한 Phase.** 대화가 길수록 매 턴 전체를 다시 보낸다.
- 새 세션은 이 파일 + `graph/README.md`만 읽는다. 이전 대화를 되짚지 않는다.

## 안 건드리는 것

`scripts/zimage-still.py` · `scripts/legal-gate.sh` · `agents/missions/**` · `agents/lib/**` ·
`.claude/agents/**` (operator-contract §5) · 기존 5-에이전트 미션 흐름(루프가 없어 이득 없음).
그래프는 순서·재시도·게이트만 책임지고, 외부 호출은 전부 `graph/tools.py`를 지난다.

## 열린 이슈

- **`.claude/whiteboard.json` 깨짐** — 161.6KB/161줄, 129번 줄 이스케이프 안 된 따옴표로
  JSON 파싱 실패. `.claude/wb/*.json` **94개는 전부 정상** — 손으로 병합한 결과물만 손상.
  "작업 시작 전 whiteboard 읽기" 프로토콜이 현재 동작하지 않음. wb 94개에서 재병합하면 복구.
- **Windows `python3` 함정** — Store 스텁이라 조용히 아무것도 안 함. 기존 `run.sh`들이
  Windows에서 실패 중일 가능성. `graph/`는 `sys.executable`로 회피했으나 나머지는 미확인.
- **게임 플러그인 0개** — `marketplace.json`에 쇼츠 5개만. README는 콜로니심을 앞세우는데
  방문자가 설치할 수 없음.
- **`goal.md` ↔ `roadmap.md` 불일치** — 서로 다른 프로젝트를 가리킨 지 한 달 이상.

## 진입 구조 (제품화 때)

프로필 3개로 묶으면 진입·토큰·파악이 한 번에 풀린다.

| 프로필 | 대상 | 담는 것 |
|---|---|---|
| `shorts` | 영상 만들러 온 사람 | music-video · info/news/idol · 법률 게이트 · judge 3종 |
| `gamedev` | README GIF 보고 온 사람 | 게임 에이전트 12 + TA + game-dev-agent |
| `core` | 자기 프로젝트에 쓸 사람 | orchestrator · planner · qa · auditor 뼈대만 |

최소판은 `scripts/first-touch.sh` 맨 앞에 질문 하나("뭘 하러 오셨나요")를 넣고
라인별 문서로 분기 — 에이전트 정의를 안 건드리므로 승인 불필요.
