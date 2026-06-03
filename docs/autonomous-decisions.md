# Autonomous decisions log

운영자 부재(2026-05-31, 6~10시간) 자율 작업.  "순서대로 전부"(코어검증→장기생존→폴리시)
지시.  gated 신기능(#4)은 승인 사안이라 미착수.  매 항목 관찰→수정→검증(isolated/integration
+LongPlay)→독립 커밋.  [[observe-dont-speculate]] + [[no-sloppy-shortcuts]] 적용.

## 2026-05-31 자율 세션 (라이브 피드백 직후 이어서)

먼저 라이브 세션에서 운영자가 직접 지적해 고친 것 (재현·로그 기반, 추측 아님):
- #216 창 설정: productName PawnSim + 1280x720 윈도우 + 리사이즈 + runInBackground
- #217 새게임 씬 2중 로드 fix (시간멈춤/목재0 근본원인 — 버튼 리스너 중복→싱글톤 중복)
- #218 목재 순간이동 진짜 fix — TryReserveWorkStandPos fast-path 가 타깃변경 시 옛 stand
  cell 재사용 → AtStandCell 즉시 true → 안 걷고 저장고 드롭.  WOODTRACE 로그로 확정.
- #219 시간속도(Game.unity 직렬화 60→6, 하루 4분) / 작물→고기(농작물 식량더미로) /
  우클릭 강제지정(작물·광맥·나무 림이 가서 작업)

이후 자율 QA 로 로그 샅샅이 훑어 발견·수정:
- #220 석재 광맥 12→20 (운영자 '맵에 석재 없음' — 광맥은 재생 안 됨)
- #221 채광 무한 포기 루프 (로그 give-up vein 358회) — 도달불가 광맥 쿨다운(20→60s),
  MineStoneAction 이 쿨다운 광맥 스킵
- #222 휴식 도달 stuck (LongPlay '민지 휴식이동 no-move 60s') — restTarget 경로에
  도달 timeout 15s 추가 (자율취침엔 이미 있었음)

- #221b 채광 포기 쿨다운 20→60s (재시도 churn ↓: give-up 254→160/run)
- #223 식량 더미 종류별 수명 + 베리 라벨 — #219 로 작물/베리가 물리 더미가 됐는데
  수명이 raw고기 90s 라 운반 전 상함.  고기 90s/농작물 600s/산딸기 300s, 베리 '고기→산딸기'.

검증 상태: isolated 76/76, integration 42/42.  LongPlay 250~350s 3명 전원 생존,
불변식 위반 0 / STUCK 0 / 예외 0.  자원 물리운반(텔레포트 없음).  시각 QA(실플레이 캡처)
정상 — 요리/낮밤/저장/UI 건강, 새 이상 없음.  창=PawnSim 1280x720.

## 남은 것 / 운영자 결정거리
- ★식량 균형 얇음: meals 3~5 / food 2 에서 안정(굶진 않음, 3명 Day5 생존)이나 버퍼가
  안 쌓임.  부패 아님(요리·운반 발생) = 생산/분배 균형.  작물 수확량(8)·농장(4x3=12)
  상향 = 튜닝거리(주관적이라 운영자 부재 중 blind 변경 보류).  원하면 올려줌.
- give-up vein 아직 ~0.8/s (광맥이 rock 에 둘러싸여 도달불가 스폰).  쿨다운으로 완화,
  근본은 스폰 시 reachability 보장 — 추후.
- 목재 운반 우선순위(#2): 현재 haul>chop (부패방지).  '벌목>운반' 전환은 운영자 선택
  (나무 무한이라 아무도 안 나르는 부작용 가능 → 작업우선순위 그리드 필요).
- EntityInspector "1.5분 후 상함" 표기가 농작물(10분)엔 부정확 — 경미, 추후.
- gated 신기능(작업우선순위 그리드 / 기분 thought / 지형 이동비용): 승인 필요(미착수).

- #224 장기 식량 고갈 fix: 800s 에서 meals 16→0(Day11 굶주림 직전), 정상상태 food 가
  요리임계(15) 밑→요리중단.  작물 수확량 8→14 (검증서 meals 164 까지 축적 가능).
  + LongPlay stuck 감지기가 '요리'(제자리 작업) 오탐하던 것 제외.
- #225 사냥 stuck timeout: '서연 사냥 no-move 180s' — give-up 이 dist>attackRange 일
  때만 검사돼 사정거리 안 무한공격 stuck.  절대 사냥 timeout 45s.

최종 검증(650s): 위반0 STUCK0 예외0, Day9 3명 생존.  채광/휴식/사냥/요리오탐 stuck 전부 해소.

★식량 변동성(중요 결정거리): harvestFood 14 로 천장은 올랐으나(run 따라 meals 164) 바닥은
여전히 thin(run 따라 meals 3~8 로 감소).  원인 = AI 요리 분배 변동(어떤 run 은 한 림이
요리 전담→대량, 어떤 run 은 분산→소량).  근본 안정화엔 요리임계(현 15) 하향 OR 작업
우선순위 그리드(전담 요리사 지정)가 필요 — 밸런스 주관적이라 운영자 부재 중 blind 변경
보류.  현 상태로도 5분+ 생존 기준은 충족(Day9~11, 위반0).

## 작업 원칙 (이번 세션 확립)
운영자 부재 중엔 명백한 버그만 고치고(관찰·로그로 재현 후), 주관적 밸런스/대형기능은
보류해 결정거리로 남긴다.  cosmetic busywork 금지.  매 수정 isolated/integration+LongPlay
회귀 게이트 통과 후 독립 커밋.

## 2026-06-03 — 4h+ 자율 세션 (운영자 명시: "묻지 말고 그냥 일해")
운영자가 4시간+ 부재하며 "묻지 말고 자율로 일하라" + "다 림월드식으로" 지시.  이번 세션은
이전 보수 원칙보다 적극적: **RimWorld 정합으로 스스로 판단해 진행**(주관적 분기도 RimWorld
바닐라를 정답으로 채택, 묻지 않음).  단 가드는 유지 — 매 변경 **컴파일 클린 + isolated 76/76
+ integration 43/43 회귀 게이트** 통과 후에만 독립 커밋, false-fix/거짓검증 금지(verify-real-path),
파괴적/머니 작업 없음(해당 없음).  대형/feel 변경은 보류 대신 **좌표화해 일관 적용**하고 운영자
플레이테스트로 검증받도록 남긴다.

오늘 자율 결정(커밋):
- 림 작업배정 지정-구동 단일화(dc030f5) — 3중 중첩 제거, RimWorld 모델.
- 시작 식량 물리 드롭(fee2325) — 추상 식사50 카운터 폐기.
- 통나무 sprite 일관화 + info 탭 정렬(e7d229f).
- 팔=다리 HP 통일 20(d351614) + 근접 데미지 1→5(전투 지루함 #8) — 소스케일 내부정합 유지.
- 진행: 남은 운영자 플레이테스트 항목 + 멀티에이전트 감사로 RimWorld 정합/버그 계속.

### 4h 자율 세션 — 누적 결과 + 보류 항목 (2026-06-03, 운영자 부재 중)
**완료(전부 컴파일+isolated 76/76+integration 44/44 게이트 통과, LongPlay 생존 검증):**
작업종류 분리(건축/채광/운반/의료)·근접데미지 1→5·팔=다리 HP·hover 작업명·통나무 sprite·
info탭 정렬·물리 식량 경제·#34 좌클릭메뉴 회귀가드(I44)·CRITICAL 회귀(ChopTreeAction/
MineStone Decide 리스트 누락) 자가발견·수정+가드(I43)·cook task 수면중 미정리·
ClearAllWorkTasks miner/harvester·save/load 1:1 매칭(데이터손상).  멀티에이전트 7회 감사,
적대적 검증으로 과(過)확정 다수 기각(verify-real-path).

**보류(운영자 결정/플레이테스트 필요 — blind 수정 위험):**
1. **트레잇 결정성 버그(고가치)**: 모든 pawn 이 gameObject.name="Pawn(Clone)" 공유 →
   PawnTraits 가 동일 시드로 굴려 **전원 같은 트레잇**(RimWorld 변종성 결여).  fix 계획:
   PawnHealth 에 baseMaxHp 저장 + PawnTraits.Initialize(name) 추가(reset→name 시드 재roll→
   base 에서 HP 재적용) + GameManager/GameSaveButtons 가 이름 설정 후 호출 + V14/V43 테스트
   timing 갱신.  4~5파일·HP 얽힘·feel 변화라 운영자 OK 후 실행 권장.
2. **save/load 완성**: 부위 HP/지정상태/작물성장 미저장(로드 시 소실).  #1(트레잇 결정성)과
   부분 얽힘.  포맷 변경이라 scope 확인 필요.
3. **behavior-medium**: drafted 자동 근접교전, PawnHauler bpDropTarget 예약, 스케줄 하드게이트
   — 동작 변화라 플레이테스트로 feel 확인 필요.
4. **대형 RimWorld 피처**: 작업종류 추가(청소/소방), 연구 트리 확장(wireable 효과 필요),
   forbidden/allowed zone, 길들이기 progression, 조리 품질 variance, 환경 mood — 큰 scope.
5. **밸런스 절대값 rescale**: 전투 수치를 RimWorld full 스케일로(현재 소스케일 내부정합) —
   pawn/적/무기 좌표화 동시 조정 + feel 플레이테스트 필요.

### 신선-시스템 버그헌트 결과 (2026-06-03 자율, c63de9c)
멀티에이전트가 저감사 영역(카메라/날씨/UI패널/경보)에서 21 실버그 확정 → 고가치 4건 적용:
레이드 이벤트 중복발생(SpawnSingleBandit→SpawnRaid 1회), 카메라 줌-인식 경계(void 렌더
방지), 날씨 폭풍 Time.time→GameClock(배속/일시정지 존중), ResearchUI null 가드.  나머지
~15건은 paranoid null-가드(저가치)라 보류.  **각도 바꾼 감사가 매번 새 실버그를 잡음**
(회귀헌트→CRITICAL chop, 신선→camera/event/weather) → 새 영역 감사 계속이 생산적.
**테스트 노트**: I16-full-chop-cycle 이 라이브 씬 타이밍에 flaky(재실행 시 통과; chop 코드
무관).  추후 전용 pawn 셋업으로 deterministic 하게 hardening 필요(현재는 재실행으로 확인).
