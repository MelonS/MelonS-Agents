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

- **Phase 1~5 완료** — 커밋 `3bdc96c`, 브랜치 `langgraph-migration`
- **쇼츠 라인이 끝에서 끝까지 코드로 이어졌다:**

```
스틸 → 🚪문1 → 🧑승인 → 영상화 → 🚪문2 → 조립 → ⚖️법률 → 출시
```

- `graph/` 패키지 ~1,800줄. 상세는 [`graph/README.md`](../graph/README.md)
- 남은 것: Phase 6(게임 라인), 진입 마법사

```bash
# 배선 확인 (모델 호출 0)
.venv/Scripts/python -m graph.shorts_graph run --spec graph/examples/shots.example.json --mock --thread demo

# 실물 — 승인 지점에서 멈춘다 (exit 3)
.venv/Scripts/python -m graph.shorts_graph run \
    --spec graph/examples/shots.one.json --judge cli --legal-judge cli --thread ep12

# 검수 시트를 보고 결정
.venv/Scripts/python -m graph.shorts_graph resume --thread ep12 --approve
.venv/Scripts/python -m graph.shorts_graph resume --thread ep12 --regen i03,i07
.venv/Scripts/python -m graph.shorts_graph resume --thread ep12 --reject

.venv/Scripts/python -m graph.shorts_graph diagram      # 구조도 3종
```

**종료 코드:** `0` 완주 · `2` 게이트 차단 · `3` 사람 승인 대기 · `1` 오류.
`3`을 따로 둔 이유 — 배치 스크립트가 "막힘"과 "사람 기다리는 중"을 구분해야 한다.

**주요 옵션**

| 옵션 | 뜻 |
|---|---|
| `--mock` | ComfyUI 없이 배선만 (모델 호출 0) |
| `--judge mock\|cli` | 스틸·컷 심사 (이미지를 본다) |
| `--legal-judge mock\|cli` | 법률 판단 (대본을 본다 — `--mock`과 조합 가능) |
| `--stills-only` | 문 1에서 멈춤 |
| `--autonomy` | 승인에서 기다리지 않고 블로커 기록 후 halt |
| `--frames N` | 컷당 심사 프레임 (기본 3) — 실행 토큰의 최대 변수 |
| `--profile info\|news\|idol` | 법률 게이트의 `required_checks` 선택 |

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
| ✓ | ~~Phase 2·3~~ | — | `54c863e`, 실물 완주 |
| ✓ | ~~Phase 4 — 사람 승인~~ | — | `aa75e0b`, 경로 4가지 |
| ✓ | ~~Phase 5 — 법률 루프~~ | — | `3bdc96c`, PASS + fail-closed |
| 1 | Phase 6 — 게임 라인 | ~1시간 | 새 도메인 — **조사 있음** |
| 2 | 진입 마법사 최소판 | ~20분 | 승인 불필요 |
| 3 | 게임 플러그인 등록 · 프로필 로더 | 제품화 때 | 로더는 §5 승인 대상 |

### Phase 로드맵

| # | 내용 | 상태 | 개발 토큰 |
|---|---|---|---|
| 01 | 스틸 게이트 (생성→채점→재시도→문1) | **완료** | ~250K |
| 02 | 실물 연결 — ComfyUI + `claude` CLI 채점 | **완료** | ~25K |
| 03 | 영상화 fan-out + cut-judge + 문2 | **완료** | ~35K |
| 04 | 사람 승인 (`interrupt()`) + 자율 halt | **완료** | ~30K |
| 05 | 조립 → 법률 게이트 → 출시 | **완료** | ~45K |
| 06 | 게임 라인 (뮤텍스 + 상태 병합) | 다음 | ~70K |

### Phase 5에서 게이트가 막은 것 — 우회하지 않고 고쳤다

돌리는 중 `legal-gate.sh`가 네 번 막았고 전부 정당했다. 통과시키려고 게이트를
손대는 대신 원인을 고쳤다:

| 막힌 것 | 왜 | 고친 방법 |
|---|---|---|
| `fact-accuracy` / `unverifiable` | 판단 심사를 안 돌림 → fail-closed | **실제로 심사위원을 붙였다** (대본 기반) |
| `required-disclaimer` | `info` 프로필이 Pexels 문구를 요구 | 프로필 YAML에서 **그대로 읽어온다** |
| `media-license` | `apache-2.0`이 allowlist에 없음 | `owner-self` — 제3자 권리 소재 없음, 실제로 그렇다 |

**"Pexels"라고 적으면 오귀속**이다 — 100% 생성물인데 스톡 라이선스를 표기하는 건
`generative-shorts-pipeline.md` 2026-07-06 항목에서 고친 바로 그 버그다.
게이트를 통과시키려고 사실이 아닌 고지를 넣지 않는다.

### ⚠️ 추정 방법 — 앞선 추정이 5~7배 틀렸다

Phase 2·3을 "2~3시간 / 3~4시간"으로 잡았는데 실제로는 **15분 / 25분**이었다.
Phase 4도 "2~3시간"으로 잡았는데 **~20분**. 원인 네 가지:

1. **사람 기준으로 잡았다.** 사람이 코드 쓰고 디버깅하는 시간이지 에이전트 시간이 아니다.
2. **Phase 1(250K)을 기준으로 스케일했다.** Phase 1은 비용의 대부분이 *조사*였다
   (레포 전수 읽기·외부 레포 리서치·아키텍처 보고서·대시보드). 이후 단계엔 그게 없다.
3. **GPU 대기를 작업시간으로 셌다.** I2V 412초는 토큰 0 — 서브프로세스가 도는 동안 다른 걸 한다.
4. **토큰 경고 후 방어적으로 부풀렸다.** 재계산이 아니라 안전마진이었다.

**진짜 예측 변수는 시간도 Phase 번호도 아니다:**

> **조사가 필요하면 비싸고, 인터페이스가 이미 있으면 싸다.**

Phase 2·3·4가 싼 이유는 `zimage-still.py`·`wan-a14b-i2v.py`·`cut-judge.md`·
`interrupt()`가 이미 있었기 때문이다. Phase 6이 여전히 비싼 이유는
Unity 배타 자원과 wb 병합을 **조사해야** 하기 때문이고, 그건 진짜다.

새 단계를 추정할 때는 이렇게 물어라 — **"읽어야 할 게 있나, 아니면 쓰기만 하면 되나?"**

### Phase 2·3에서 실물로만 잡힌 것 (재발 방지)

1. **처방이 완전히 무시되던 문제.** 한국어 처방을 영어 프롬프트 뒤에 덧붙였는데,
   Z-Image는 cfg=1이라 지시가 희석된다. 실측 i02 `65→71→64` 발산.
   → 심사위원이 **완성된 영어 프롬프트**를 돌려주고 **교체**하도록 수정. `73→69→88` 수렴.
2. **인물 없는 샷에서 character_lock 감점.** must에 인물이 없으면 캐릭터 일관성 만점 처리.
3. **LangGraph: State에 선언 안 한 키는 조용히 버려진다.** `clip_gate_open`을 빠뜨려
   문 2가 항상 닫혀 있었다. 에러도 경고도 없다. **스키마 선언이 곧 계약.**
4. **심사위원 CLI는 `--allowedTools Read` 필수.** 없으면 그림을 못 보고 추정으로 채점한다.
5. **Windows에는 스텁이 둘 있다.** 같은 종류의 함정이 두 번 나왔다:
   - `python3` → Microsoft Store 스텁. 조용히 아무것도 안 함.
   - `bash` → WSL 스텁. WSL 없으면 rc=1로 죽음.

   `graph/`는 `sys.executable`과 `bash_bin()`(Git Bash 직접 탐색)으로 회피했다.
   **기존 `run.sh`·`scripts/*.sh`가 Windows에서 조용히 실패 중일 가능성이 크다** —
   `docs/platform-windows.md`에 이 두 스텁 얘기가 없다.
6. **`--mock`(가짜 스틸)과 `--judge cli`(진짜 채점)는 모순 조합이다.** 1px 이미지를
   실제로 채점해 0점이 난다. 그래서 법률 판단만 `--legal-judge`로 분리했다 —
   법률은 이미지가 아니라 대본을 보므로 mock 스틸과 조합할 수 있어야 한다.

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
| 개발 (남은 2단계) | ~115K | ~115K |
| **실행 (편당)** | **~300K** | **~9M** |

30편 기준 개발은 전체의 1%. **관리 대상은 편당 실행 비용이다.**

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
