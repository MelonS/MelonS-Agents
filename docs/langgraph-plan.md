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

- **Phase 1·2·3 완료** — 커밋 `54c863e`, 브랜치 `langgraph-migration`
- `graph/` 패키지. 상세는 [`graph/README.md`](../graph/README.md)
- **실물 end-to-end 완주 확인** — 스틸 → 심사 → 문1 → 영상화 → 컷심사 → 문2, exit 0
- 남은 미검증: 조립·법률·출시 단계 연결 (Phase 5)

```bash
# 배선 확인 (모델 호출 0)
.venv/Scripts/python -m graph.shorts_graph run --spec graph/examples/shots.example.json --mock --thread demo

# 실물 (ComfyUI 필요)
.venv/Scripts/python -m graph.shorts_graph run --spec graph/examples/shots.one.json --judge cli --thread real-01
.venv/Scripts/python -m graph.shorts_graph run ... --stills-only    # 문 1에서 멈춤
.venv/Scripts/python -m graph.shorts_graph diagram
```

### 실측치 (RTX 4070 Ti SUPER 16GB, 1샷 완주 507초)

| 단계 | 실측 | 비중 |
|---|---:|---:|
| 스틸 생성 (Z-Image) | 10.2s | 2% |
| 스틸 심사 (Sonnet, 이미지 1장) | 22.8s | 4% |
| **영상화 (Wan A14B)** | **412.3s** | **81%** |
| 컷 심사 (Sonnet, 프레임 3장) | 61.9s | 12% |

**412초 × 26컷 = 2시간 58분.** 문서의 "3시간"이 추정이 아니라 측정값으로 확인됐다.
스틸 1장(10초) 대 영상 1컷(412초) = **1:40** — 문을 스틸 뒤에 세운 근거.

## 작업 순서

| | 할 일 | 시간 | 비고 |
|---|---|---|---|
| ✓ | ~~whiteboard 복구~~ | — | `6d8d7e9` |
| ✓ | ~~Phase 2·3~~ | — | `54c863e`, 실물 완주 확인 |
| 1 | **Phase 4 — 사람 승인 지점** | 2~3시간 | `interrupt()` 2곳 |
| 2 | Phase 5 — 법률 루프 | 3~4시간 | `legal-gate.sh` 조건부 엣지 |
| 3 | 진입 마법사 최소판 | 1~2시간 | 승인 불필요 |
| 4 | Phase 6 — 게임 라인 | 4~5시간 | 뮤텍스 + 상태 병합 |
| 5 | 게임 플러그인 등록 · 프로필 로더 | 제품화 때 | 로더는 §5 승인 대상 |

### Phase 로드맵

| # | 내용 | 상태 | 개발 토큰 |
|---|---|---|---|
| 01 | 스틸 게이트 (생성→채점→재시도→문1) | **완료** | ~250K |
| 02 | 실물 연결 — ComfyUI + `claude` CLI 채점 | **완료** | ~60K |
| 03 | 영상화 fan-out + cut-judge + 문2 | **완료** | ~90K |
| 04 | 사람 승인 (`interrupt()`) 2지점 | 다음 | ~70K |
| 05 | 법률 루프 (`legal-gate.sh` 조건부 엣지) | 대기 | ~90K |
| 06 | 게임 라인 (뮤텍스 + 상태 병합) | 대기 | ~110K |

### Phase 2·3에서 실물로만 잡힌 것 (재발 방지)

1. **처방이 완전히 무시되던 문제.** 한국어 처방을 영어 프롬프트 뒤에 덧붙였는데,
   Z-Image는 cfg=1이라 지시가 희석된다. 실측 i02 `65→71→64` 발산.
   → 심사위원이 **완성된 영어 프롬프트**를 돌려주고 **교체**하도록 수정. `73→69→88` 수렴.
2. **인물 없는 샷에서 character_lock 감점.** must에 인물이 없으면 캐릭터 일관성 만점 처리.
3. **LangGraph: State에 선언 안 한 키는 조용히 버려진다.** `clip_gate_open`을 빠뜨려
   문 2가 항상 닫혀 있었다. 에러도 경고도 없다. **스키마 선언이 곧 계약.**
4. **Windows `python3`는 Store 스텁.** `graph/`는 `sys.executable`로 회피.
5. **심사위원 CLI는 `--allowedTools Read` 필수.** 없으면 그림을 못 보고 추정으로 채점한다.

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
| 개발 (남은 3단계) | ~270K | ~270K |
| **실행 (편당)** | **~300K** | **~9M** |

30편 기준 개발은 전체의 3%. **관리 대상은 편당 실행 비용이다.**

실행 내역(실측 기반): still-judge 26~52회 · cut-judge 26회(프레임 3장) · 법률/기획 소량.
`judge-frames.py` 기본값이 3장이라 앞선 추정(5장)보다 싸다.

**절감 레버 3개**
1. **심사위원 Sonnet 라우팅** — 이미 적용됨 (`JUDGE_MODEL`로 변경 가능)
2. `--frames`로 컷당 프레임 수 조절 — 실행 토큰의 최대 변수
3. `--mock`으로 배선 먼저 (그 회차 모델 호출 0)

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
