# Autonomous decisions log

## 2026-06-10 밤 ~ 06-11 새벽 야간 자율 세션 — 단독 결정 기록

운영자 위임: "10시간 스스로 계획 세워 작업" + "기능 최소화 상태로 게임이 되어야 함.
그런 방향으로 할일 계속 추가" + "절대 물어보지 마".  멀티에이전트 격차 분석(4갈래)으로
자율 큐를 만들고 UI 배치 2~5 + 게임필 배치 1~5 를 게이트(repro_all 12/12)마다 커밋.

1. **기능동결 해석**: 운영자가 그래픽/사운드/BGM/폴리싱을 명시 위임 → game-feel 자산과
   기존 시스템 배선·튜닝은 허용, 새 게임 시스템은 계속 금지.  전멸 화면·자동 일시정지·
   BGM 멜로디·아트 스트림 재오픈은 운영자 결정 대기로 분리 (check-round-2 ⑥).
2. **첫 습격 day 11→2 (grace 9→2, interval 5→3)**: '세션 안에 위협 0'이 게임 아님의
   1순위 원인.  1마리 습격은 3림 감당 가능(5/31 wipe 는 5마리×3일 조합이 원인).
   난이도 체감은 check-round-2 ③ 으로 운영자 재조정 루프.
3. **거짓 이벤트 4종 풀 제거 + 2종 실효 배선**: '카드는 항상 실제 사건' 신뢰 회복이
   목적 — 효과 없는 텍스트를 남기는 것보다 제거가 낫다고 판단.  되살릴 땐 배선 필수.
4. **기본 줌 3.5→5.5**: '림 너무 작음'(5/27)과 '첫 화면이 게임으로 안 보임'(격차 분석)
   사이 절충.  수치는 운영자 선호로 재조정.
5. **게이트 중 소스 편집 금지 룰 자가 발견**: repro_run 이 시나리오마다 에디터 단계를
   타므로 게이트 도중 편집이 컴파일 깨짐→연쇄 FAIL 을 만들었다(6 FAIL 사건).  이후
   전 배치를 '편집→동결→게이트→커밋' 사이클로 운영.
6. **raw Input 전수 감사**: 22파일 102개소 확인 (G:/ai/_raw_input_audit.txt).  야간
   일괄 전환은 입력 전면 리스크라 ESC 계열 5곳만 선별 전환(설정 ESC 하네스 미동작
   관찰의 해소).  나머지는 카테고리화해 후속 — 월드클릭 소비 경로는 이미 SimInput.
7. **튜토리얼 배너 투명 차단 발견**: 게이트 플레이키 추적 중 실플레이 버그 확정
   (첫 18초 상단 클릭 무음 무효) — 계측 로그(SimInput overUI 차단자 식별)로 잡음.

## 2026-06-10 저녁 #38 근본수정 세션 — 단독 결정 기록

1. **마키 우클릭의 나무/광맥 move-order 제거는 동작 변화지만 즉시 적용**: 운영자 실증상
   ("선택 림은 걸어가서 멍, 다른 림이 벌목")의 절반이 이 가로채기였음.  나무/광맥 우클릭의
   소유권을 ClickSelector 지정+직접배정 경로로 단일화 — 빈 땅/적 우클릭의 마키 동작은 불변.
2. **MarqueeSelector raw Input → SimInput 전환**: WORKFLOW-V2 규칙 2(검증은 운영자와 같은
   레이어) 위반이던 하네스 사각지대 해소.  평시 패스스루라 실플레이 동작 변화 0.
3. **SimInput 월드좌표 주입 모드 신설**: screen 좌표를 소비 프레임에 도출 — 카메라 포커스
   팬으로 인한 무음 클릭 미스(designations=0, 3회 관측)의 근본 해결.  실경로(ScreenToWorld
   Point→PickEntityAt) 보장은 유지.
4. **하네스 클릭 전 화면밖/UI밴드 타깃 자동 FocusOn**: 실유저 행동(보이게 만들고 클릭) 미러.
   가짜 PASS 방지가 아니라 가짜 FAIL(overUI 게이트 무음 무효화) 방지.
5. **selectedChopAssigned 인과 probe**: 결과 라벨 감시(race 운에 좌우)만으론 #38 부류를
   증명 못 함 — '클릭 1s 내 선택 림이 task 보유'(동기 직접명령 vs 1.5s 자율 틱)로 인과를
   판정.  멀티에이전트 감사 제안 채택.
6. **f29b10f '#38 fix' 사후 평가**: 그 수정은 이미 retire 된 DispatchToIdleChoppers 내부의
   죽은 코드가 됐고, 06-03 단일화는 sim 경로만 '선택 림 전용'으로 바꿔 test==real 이 정반대로
   깨진 상태였다 — "하네스 PASS = 고침" 선언이 가짜였던 구조적 원인으로 기록.

## 2026-06-10 UI 재검토 — 단독 결정 기록 (a685582)

1. **UI 작업은 기능동결 예외로 해석**: 운영자 직접 지시("ui/ux 개선 및 정리, 전체 재검토").
   신규 게임 시스템은 계속 금지 — 기존 UI 의 개선/정리만.
2. **ThreatAlertUI 삭제 대신 부트 제거**: 감사 제안은 파일 삭제였으나 #232 선례(운영자
   '되돌리기 쉽게 보존')에 따라 GameManager 호출만 끊고 파일 보존.
3. **게이트 레드 상태에서 UI 배치 커밋**: repro_all 9/10 — 유일 FAIL(p1-chop-selected-only)
   은 UI 와 무관한 별건 간헐 재현(우클릭 무동작, #38 계열)로 판단(증거: UI 변경 파일에 AI/
   디스패치 코드 없음 + 동일 시나리오가 UI 변경 전에도 1회 FAIL).  커밋 메시지에 명기.
4. **SkillUI 는 부활 선택**: 죽은 UI 의 대안(삭제 vs 부활) 중 부활 — 운영자가 스킬 시스템을
   이미 승인·구현했고(#120, Day 21) 표시만 죽어 있었음.  info 탭 흡수안은 백로그 P2 로 보류.

## 2026-06-10 재현 사이클 3 — 단독 결정 기록 (d29e49b)

1. **폭풍 fix 방향 결정**: 직접 드레인 리스케일(-3→-0.072/s) 대신 **thought 일원화** 선택 —
   운영자 승인(2026-06-05) "decay+thought 합산" 모델에 정합 + 이중 페널티 우회로 제거.
   페널티 체감(-6)과 피난처 판정(바닥타일만)은 check-round-1 질문으로 상정.
2. **stand-cell fix 위치 결정**: 호출부 14곳 대신 판정 함수(AtStandCell) 1곳 보강 — 같은 결함을
   공유하는 벌목/채광/건설/요리/채집/수확/운반/재배 전부에 한 번에 적용.  중심 근접 0.3 은
   arriveDistance(0.05) 여유 + 최대 작업거리 1.71(<2셀) 근거.
3. **fix 3건에서 라운드 1 마감**: WORKFLOW-V2 규칙 5(라운드당 3~5건) — thrash·폭풍·stand-cell
   로 소량 배치 충족.  이후 자율 작업은 fix 가 아닌 재현/시나리오 확장만.

## 2026-06-10 재현 사이클 2 — 단독 결정 기록 (081c850)

1. **시나리오 3건 재작성 결정**: repro_all 첫 가동의 FAIL 3건이 전부 게임 버그가 아닌 시나리오
   결함으로 판명(스크린샷·로그·코드 교차 검증: 원거리 벌목은 실제로 나무가 쓰러졌음) → 도달 가능
   조건으로 재작성.  함정 5종은 playbook.md 사이클 2 참조.
2. **통나무 41x 가속 미수정 결정**: WoodPileEntity 가 "24초=1게임일" 낡은 가정으로 내구도 계산
   (실측 하루 ~1,000게임초 → 옥외 더미 수명 반나절, 의도 10게임일의 1/41).  기능동결 + 속도는
   게임필 사안이라 코드 미수정, check-round-1 ③ 운영자 결정 질문으로 상정.
3. **mood 하강 페이스 관찰만**: 자연 플레이 첫날 저녁 mood 36 도달(정신붕괴 임계 20) — 회복수단
   (침대/실내) 확보 전 하강 일변도 구조.  수정 아닌 check-round-1 ④ 역방향 질문으로 상정.
4. **qa 게이트 적용 범위 해석**: 이번 커밋은 게임코드 fix 없는 test-infra/문서 — 규칙 3의 의무
   게이트 대상은 아니나, 증거 검수용 qa 서브에이전트를 자발 투입(VERDICT: VERIFIED) 후 커밋.

## 2026-06-04 멀티에이전트 버그헌트 2사이클 (운영자 "할거없어?" 재나ज)

신규 7차원(경로탐색·예약/작업배정·save/load견고성·동물/길들이기·연구/스킬·일정/징집·렌더상태)
병렬 감사 → 발견 22 → 적대적 2-검증 → **확정 10 / 기각 12**.  적대적 검증이 Unity fake-null
의미론 오해 기반 false-positive(트리 중복증식·예약 leak·stand-cell 등 다수)를 정확히 기각.

**수정 완료 8건**(전부 ISO83/INT46, 일부 LongPlay):
- 징집/수동제어 중 자동공격 정지(후퇴 무시·이중타격 fix) · 출혈사망 corpse 회색조(ForceSyncDead
  ApplyVisual) · 부상폰 로드 Hp 동기화 · Hunter.HasTask 순수 null(킬직후 hitch) · 길들인 동물
  자동사냥 제외 · 연구 mul manipulation² → manipulation · 징집 중 자율취침 금지 · **연구 진행도
  save/load**(재시작 unlock 소실 fix, I46)

**✅ 해결 — 🔴 CRITICAL #2 구조물 재시작 persist+reconstruct (운영자 "지금 바로 구현" 지시로 착수)**:
StructureTag(빌드 Mode 스탬프) + BuildManager.SpawnFinished(빌드완료·로드재구성 단일 경로) +
SaveData.structures + OnLoad 재구성(구조물/작물/스톡파일 파괴 후 재생성).  ISO83/INT47(I47/I35)·
LongPlay survived.  잔여 폴리시: 스톡파일 allowed-kinds 필터 + 재시작 마커 스프라이트(기능은 동작).
아래는 착수 전 분석 기록(참고):

**(원본 분석) #2 구조물 재시작 persist+reconstruct**:
GameSaveButtons.OnLoad 가 pawn+tree 만 Instantiate 하고 벽/침대/스톡파일/작물은 재생성 안 함
(ApplyLoadedSubStates 는 기존 엔티티 서브상태만 위치매칭 덮기).  → 게임 **재시작 후** 로드 시
플레이어 건축물 전부 소실(같은 세션 F9 는 엔티티 안 파괴라 우연히 동작 → 버그 은폐).  진짜 fix 는
큰 save-system 피처: (a) SaveData 에 doors/stoves/lamps/fences/barricade/autodoor 등 **모든**
플레이어 구조물 저장(현재 walls/beds/stockpiles/crops 만), (b) OnLoad 가 BuildManager.PrefabFor
(private→공개 SpawnFinished 필요) + GrowZone 으로 전부 reconstruct + 등록(RegisterWallCell 등)
+ 멀티셀(침대 1x2) footprint, (c) 재시작 검증(자동 테스트는 same-session 만 커버 → 인게임 필요).
말단 세션에서 급조하면 미등록/깨진 재구성 위험이라 미착수.  우선 ApplyLoadedSubStates 에 소실
가시화 경고로그만 추가([[verify-the-real-path]] — 거짓 "복원됨"보다 가시화가 정직).

**보류 2 — #5 길들이기 walk-to(feature)**: 현재 동물 우클릭 즉시 TryTame(텔레포트, 거리 무시).
RimWorld 정합하려면 PawnTamer(또는 hunter 재사용)로 걸어가 인접 시 시도 — feature 라 운영자
방향 확인 후.

## 2026-06-04 멀티에이전트 버그헌트 1사이클 (운영자 "멀티에이전트 버그헌트 한 사이클" 선택)

7차원 병렬 감사(needs-decay/mood·운반저장·AI생존·건축·전투건강·시간날씨·UI) → 발견 34건
→ 발견별 적대적 2-검증(반박 시도) → **확정 14 / 반박기각 20**.  적대적 검증이 운영자 보고
#35/#36(needs decay·mood)을 정확히 **기각**: "thought 기반 mood가 의도된 설계, decay 매 프레임
정상 작동, 충분히 먹고 자면 mood 안 떨어지는 게 정상" — 작년 over-confirm 재발 방지.

확정 14건 중 **모델-독립적 명백 correctness 버그 7건 수정**(전부 회귀 가드 + ISO82/INT45):
- 🔴 폭풍 지속 회귀(내 2026-06-03 Time.time→GameSeconds 변경 시 상수 60 미보정 → 1x 0.7실초만
  지속) → 5184 게임초(≈60실초@1x) 환산 (V79)
- 해체 환불 품질별 정합: 수면자리(비용0) +4 복제 익스플로잇 / 고급침대 과소환불 / 자동문 1→3 (V82)
- 바리케이드 해체 불가(영구 봉쇄) → IsDeconstructable 추가(OnDestroy가 셀 해제 확인)
- 운반 중 pawn 사망 시 자원 영구 소실 → OnDisable에서 발밑 드롭(scene.isLoaded 가드)
- 다운(의식불명) pawn이 계속 일/이동/자동공격 → PawnUtilityAI + PawnEntity 게이트
- 출혈 사망이 PawnEntity.Hp 미동기화 → 적이 시체 헛공격 → ForceSyncDead (V81)
- 의사 치료가 영구 bandaged → 영구 출혈면역 → 새 상처 시 bandaged 해제 (V80)

**보류(운영자 결정 필요 — 자원 모델 단일화)**: 확정 #1/#2/#3/#5 는 모두 "ResourceManager
카운터 vs 물리 더미"의 이중 표현 불일치라는 **단일 뿌리**에서 나온다 (주석은 'derived'라는데
동작은 authoritative).  증상: (1) blueprint용 InStockpile 더미 pickup 시 카운터 미차감 →
카운터 영구 과대, (2) stockpile 식량 소비 시 물리 더미 미파괴, (3) blueprint 카운터결제+물리운반
이중 funding, (5) blueprint 취소 시 collected 자재 미환불(카운터 환불은 물리-haul 경우 복제 위험).
**"다 림월드식"** 방향(RimWorld는 추상 카운터 없이 물리 stack 합이 곧 재고)으로 단일화하는 게
정공법이나, 이는 feel/scope 설계 변경이라 운영자 판단 후 진행.  piecemeal 수정은 회계를 더
악화시킬 수 있어 의도적으로 미착수([[verify-the-real-path]] / [[no-sloppy-shortcuts]]).

---

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

### 트레잇 결정성 — 보류→완료 (2026-06-03, d47b688)
보류했던 #1(트레잇 결정성: 전원 동일 트레잇)을 base-HP 멱등 리팩터로 안전하게 해결.
PawnHealth.baseMaxHp + ApplyMaxHpMul(멱등) → PawnTraits.ReRollFromName(이름 시드) 을
GameManager 스폰/GameSaveButtons 로드에서 호출.  콜로니스트마다 다른 트레잇 + save/load
결정성.  Awake 기본 roll 유지로 V14/V43 테스트 무변경.  검증: 76/76 + 44/44 + LongPlay
survived(변종 트레잇 하 생존).  → 남은 보류: save-load 완성·behavior-medium·대형피처·전투
절대값 rescale (운영자 플레이테스트/결정 필요).
