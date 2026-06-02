# Goal — game-prototype (PawnSim) 의 outcome layer

**이 파일은 outcome layer.** 작업 큐는 `ROADMAP_40H.md` + TaskList 가 담당.
세션 시작 시 이 파일을 **먼저** 읽고 "Done when" criteria 가 충족되었는지
확인한다. 충족 안 됐으면, 작업의 목표는 이 goal 을 advance 하는 것이지
큐를 비우는 것이 아님.

> 본 repo 루트의 [`/docs/goal.md`](../../../docs/goal.md) 는 멀티-skill 프레임워크
> 차원의 goal (music-video → Skill #1 등). 이 파일은 **Skill #3 game-prototype
> 자체의 goal** 만 다룬다.

---

## Active goal — 2026-05-27 promoted

### "바닐라 콜로니심급 수준의 prototype + 정직한 작동 검증"

운영자 발화 (2026-05-27, 자율 작업 10h+ 지시 직전):
- "그냥 대충 만들고 했다고 하고 있어. 내가 원하는 수준은 그게 아님"
- "리팩토링하면서 검증 + 문제 안생겨야지"
- "10시간 이상 자율 작업"

이게 의미하는 것:

1. **바닐라 콜로니심 핵심 시스템 90%+ 커버리지** (현재 추정 85%, 향후 검증으로 깎일 수 있음).
2. **모든 피처 "코드 있음" → "실제 작동 검증됨"** 으로 격상.
   현재 audit (Step-81 직후) 50 컴포넌트 중 ~10개만 실측 검증.
3. **아키텍처 부채 청산** — data 외부화, partial class 분할, Strategy pattern,
   ServiceLocator, PlayMode 자동 테스트.
4. **시각/오디오 퀄리티** — 운영자가 screenshot 보고 "공처럼 생긴 게 뭐냐" 같은
   질문 안 나오는 수준.

---

## Done when

각 항목이 verifiable + binary 한 기준이어야 함.

### Architecture (R 시리즈)
- [x] **R1** — `refactor_check.py` 5단계 자동 검증 harness (PASS)
- [x] **R2** — `PawnStats` SO (combat/move 외부화)
- [x] **R3** — `HealthPartsConfig` SO (6 body parts 외부화)
- [x] **R4** — SceneSetup 1,484L → 1,171L + 4 partial files
- [ ] **R5** — PawnUtilityAI Strategy pattern (IPawnAction + 6+ action class)
- [ ] **R6** — ServiceLocator (Singleton 5개 → testable lookup)
- [ ] **R7** — PlayMode 자동 검증 (drafted/wolf/research/arrow/crop 5 시나리오 PASS)

### Feature 작동 검증 (V 시리즈 — R7 활용)
- [ ] **V1** — Drafted state: R 키 toggle → cyan tint 1초 내 확인
- [ ] **V2** — Wolf detection: pawn 을 wolf 5 unit 안에 spawn → wolf chase 시작
- [ ] **V3** — Research progress: bench radius 안 pawn → 1초 후 currentPoints > 0
- [ ] **V4** — Arrow hit: research 강제 완료 → drafted → 적 spawn → 화살 발사 + hit
- [ ] **V5** — Crop harvest: ripe 작물 클릭 → +5 food + growth=0
- [ ] **V6** — Body parts: 적이 pawn 공격 → 특정 부위 HP 감소 + 출혈 tick
- [ ] **V7** — DirectorMode tier: day 7 도달 → tier 1 이상 이벤트 발화 확인
- [ ] **V8** — Map obstacle: pawn 호수/바위 경계에서 정지
- [ ] **V9** — Mood break: mood < 20 → IsBreaking = true, 1분 후 행동 중단

### Visual polish (P 시리즈)
- [x] **P1** — 사슴 4족 사슴 외형 (Step 81)
- [x] **P2** — pawn 맵 밖 안 나감 (Step 81)
- [x] **P3** — 호수/바위 통과 안 됨 (Step 81)
- [x] **P4** — Day 용어 통일 (ROADMAP "Step", 게임 "1일차" — Step 81)
- [ ] **P5** — pawn 32x32 디테일 sprite (얼굴/머리/옷 픽셀)
- [ ] **P6** — 야간 실제 어두워짐 시연 screenshot (밤 22시 캡처)
- [ ] **P7** — Combat 시연 시퀀스 (4-5 screenshot: idle → draft → 적 spawn → 화살 → 사망)

### Stretch (운영자 추가 지시 시)
- [ ] Power grid (발전기 + 배터리 + 전선)
- [ ] Trading caravan + buy/sell UI
- [ ] Animal taming (offer food → 30% success)
- [ ] Stockpile filter/priority logic
- [ ] Bills queue at workbench

---

## NOT Done when (스코프 밖)

- 완전한 레퍼런스 콜로니심 동등 (불가능 — 레퍼런스 콜로니심는 11년 개발됨)
- 다국어 (한글만)
- 멀티플레이어
- 모바일 빌드
- Steam 출시 준비 (당분간)

---

## 검증 방식

매 commit 마다 `python skills/game-dev-agent/scripts/refactor_check.py --tag NN`
실행. **5단계 모두 PASS** 한 commit 만 main 에 누적.

V 시리즈 (PlayMode 자동 검증) 는 R7 완료 후 활성화. 각 시나리오는 헤드리스
build + 짧은 시뮬 + Player.log assertion 으로 자동 PASS/FAIL.

운영자가 직접 .exe 열어서 키 누르는 수동 검증은 R7 이후엔 backup용.
