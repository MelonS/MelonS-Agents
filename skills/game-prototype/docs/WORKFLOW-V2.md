# 작업방식 v2 — 재현-우선 (2026-06-10 운영자 승인)

운영자: "버그를 잡으라고 해도 잡지도 못하고 뭐가 문제인지도 모르고 내가 잡아줘도 못 고침"
→ 진단: 버그를 못 잡는 게 아니라 **못 보는** 상태. 검증이 API 레이어에서만 돌았고
(TestRunner V1~V9 전부 `crop.Harvest()` 식 직접 호출), 운영자가 쓰는 실제 입력 경로
(마우스 클릭 → UI 메뉴 → 명령)는 자동 검증 0건. 그 위에 증상 패치 누적 + 스킬 환류 0.

이 문서가 기존 자율 룰(갭 분석 우선)을 **대체**한다. 충돌 시 이 문서가 이긴다.

---

## 규칙 (전부 강제)

### 1. 재현 없으면 수정 없음
운영자 버그 리포트는 **실제 입력 경로로 재현하는 스크립트부터** 만든다
(스크린 좌표 클릭/우클릭 시뮬, EventSystem 경유 — 직접 메서드 호출 금지).
- 재현 성공 → 그 스크립트가 곧 회귀 테스트. 수정 후 같은 스크립트로 PASS 확인.
- 재현 실패 → "재현 불가 + 시도한 조건" 보고하고 운영자에게 추가 정보 요청.
  **추측 수정 절대 금지.** 재현 안 된 버그에 "fix" 커밋 금지.

### 2. 검증은 운영자와 같은 레이어
"작동한다"의 증거는 운영자가 보는 것과 같은 화면·같은 입력에서 나와야 한다.
- 클릭 버그는 클릭 시뮬로, UI 버그는 스크린샷 픽셀로, 이동 버그는 위치 추적으로.
- API 호출 테스트(기존 V 시리즈)는 보조 수단으로만 유지. 단독으로 "검증됨" 선언 불가.

### 3. 수정 선언 = 증거 첨부 (작성자 ≠ 검증자)
fix 를 쓴 흐름과 별개로 **qa 서브에이전트**가 빌드에서 재현 스크립트 재실행 +
스크린샷/로그 첨부해야 "fixed". 증거 없는 fix 커밋 금지.
커밋 메시지에 증거 파일 경로 명시 (예: `검증: playtest-logs/repro-38.md`).

### 4. 기능 동결
PLAYTEST-TODO 의 P0/P1 이 **운영자 인게임 OK** 될 때까지 신규 기능/시스템 추가 금지.
갭 분석 자율 task 발굴 룰(feedback-pawnsim-autonomy)은 안정화 완료까지 일시 중단.
자율 모드에서 큐가 비면: 갭 분석이 아니라 **미재현 버그 재현 시도**가 다음 작업.

### 5. 소량 배치
운영자 확인 라운드 1회당 fix 3~5건만. 20건 쌓아두고 "다 고쳤어요" 금지.
각 라운드는 "이 빌드에서 이 3가지를 이렇게 조작해보면 됨" 형태로 전달.

### 6. 스킬 환류 강제
매 사이클 종료 시 "이번에 배운 것 중 다음 게임에도 적용되는 것"을
스킬 playbook 에 기록한다 (없으면 "없음"이라고라도 기록 — 빈 사이클 가시화).
스킬의 성공 지표 = **2번째 게임(suika)이 같은 품질에 도달하는 속도**.

---

## 실행 순서

1. **입력 경로 playtest harness** — 기존 AutoQA/AutoScreenshotter/TestRunner 를
   스크린 좌표 입력 시뮬 기반으로 확장. 모든 후속 작업의 전제.
2. **P0 3건 재현** — 림 기본 이동 / 원거리 벌목 / 벌목 서브메뉴 없음.
   harness 로 재현 → 원인 → 수정 → 같은 스크립트 PASS → 운영자 확인 라운드 1.
3. **P1 순회** — 같은 루프로 3~5건씩.
4. **스킬/산출물 분리** — skills/game-prototype = playbook + 재사용 스크립트만,
   게임 프로젝트는 산출물 위치로. SKILL.md 를 실제 배운 것 기반으로 재작성.
5. **suika 로 스킬 검증** — playbook 만 보고 2번째 게임 진행, 막히는 지점이
   곧 playbook 의 구멍.

## 재현 harness 사용법 (2026-06-10 구축)

- **SimInput** (`Tests/SimInput.cs`) — 게임플레이 입력 추상화. 평시 Input 패스스루,
  `-repro` 시 주입. ClickSelector 가 1호 적용 (MarqueeSelector/BuildManager 는 추후).
- **ReproHarness** (`Tests/ReproHarness.cs`) — `-repro <scenario.json>` 로 활성화.
  시나리오 = 클릭 시퀀스 + 화면 기준 assert (형식은 파일 헤더 주석).
- **러너**: `python skills/game-dev-agent/scripts/repro_run.py <scenario> [--fresh-build]`
  exit 0 = PASS. FAIL 스텝 출력 + `G:/ai/_repro_shots/` 스크린샷이 곧 재현 증거.
  타임아웃은 시나리오 JSON 의 `timeoutSec` (없으면 240).
- **스위트(커밋 게이트)**: `python skills/game-dev-agent/scripts/repro_all.py [--fresh-build]`
  — 전 시나리오 직렬 실행(리포트 파일 전역 공유라 병렬 금지), exit 0 = 전체 PASS → 커밋 가능.
- **시나리오 보존**: `skills/game-prototype/repro-scenarios/*.json` — PASS 한 시나리오는
  영구 보존 = 그 버그의 회귀 테스트. 운영자 버그 1건 = 시나리오 파일 1개 원칙.
- **`_` 접두 시나리오 = 게이트 비포함** (2026-06-11): 휴먼라이크 플레이테스트/소크 등
  길고 판정이 사후 리뷰형인 시나리오. repro_run 직접 호출로 실행.
- **시나리오 작성 함정 5종** (자가조절 바닥값 금지 / 세팅-적용 레이스 / 임계값 기하 도출 /
  기준값 시점 / Destroy 오탐) — `docs/playbook.md` 사이클 2 참조.
- **P1 계열 ops/probes** (2026-06-10): `setNeed`/`speed`/`setWeather`(스캐폴딩, 검증 대상 아님),
  `needBelow`/`needDrops`/`needDropsAtMost`(폭주 페널티 상한 가드)/`hasThought`/
  `pileDurabilityDrops`(검증). 형식은 ReproHarness.cs 헤더.

## 멀티에이전트 운용 (현실적 최소선)

- 톱레벨 = orchestrator + editor 겸임 (Unity 반복작업은 컨텍스트 공유가 무거움).
- **qa 서브에이전트는 의무** — 규칙 3. 이게 유일한 하드 게이트.
- planner 는 재설계급 작업(작업배정 단일화 같은)에만 투입.
- auditor 기존 유지.
