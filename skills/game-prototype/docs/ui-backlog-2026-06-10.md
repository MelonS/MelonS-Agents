# UI/UX 전면 재검토 백로그 — 2026-06-10 (운영자 지시)

생성: 8-도메인 병렬 감사 워크플로 (현행 코드 + ui-tour 스크린샷 + rimworldwiki.com 교차).
발견 82건 = P0 5 / P1 20 / P2 33 / P3 24.  ui-audit.md(5/30)는 §2 P1/P3/P4 해결 확인됨 —
본 문서가 현행 기준.  처리 시 각 항목에 ✅(커밋해시) 표기.

도메인: bottom-bar(하단바·기즈모·지정토글) / left-resources(좌상단 자원) / top-bar(초상화바·시계)
/ inspect(선택·info탭·라벨·툴팁) / architect(건축메뉴·토스트) / tabs(직업·일정·연구·스킬)
/ system-menus(설정·저장·메인메뉴) / feedback(알림·이벤트·컨텍스트메뉴)

## 적용 현황 — 배치 1 (2026-06-10 저녁)

P0 5건 전부 + 빠른 P1/P2 5건 적용, ui-tour 스크린샷으로 시각 검증:
- ✅ `#2.0` 날짜 클리핑 — Overflow+폭380, "봄 1일, 5500년" 풀표시 확인
- ✅ `#3.0` 폰 이름 타이틀 가림 — 탭 스트립 아래 전용 밴드로 정규화, "민지 · 떠도는중 (특성)" 가독 확인
- ✅ `#5.0` SkillUI 죽은 UI — 상시활성 호스트 분리 + (400,58) 이동, Lv 표시 부활 확인 (테두리/톤은 5.7 잔여)
- ✅ `#5.1` 연구 스트립 가림 — 우하단 시계 왼쪽 (x-198, y120) 밴드로 이동 (1차 y112 는 시계와 겹쳐 재보정)
- ✅ `#7.0` 우상단 5중 겹침 — 밴드 계약: 카드3장(-88~-232)>EventLog(-244)>Toast(-416)>LowAlert(-462), ui-audit §3.5 테이블화
- ✅ `#7.2` ThreatAlertUI 이중 표시 — 부트 제거(파일 보존, #232 선례)
- ✅ `#0.2` ControlHint 잘린 띠 — 실제 숨김 (TutorialOverlay 가 안내 담당)
- ✅ `#0.3` 탭바 폭 off-by-one — 5*GroupGap, ⚙설정 테두리 침범 해소
- ✅ `#0.4`/`#6.0` 설정 "(ESC)" 거짓 힌트 제거
- ✅ `#0.5` ui-audit.md SSOT 현행화 (P1/P3/P4 RESOLVED, §3.1/3.2/3.3/3.5 재기술)
- ➕ ui-tour.json 신설 — 전 패널 열어 캡처 (UI 존재 회귀가드 겸용)
- ➕ (진단) PawnChopper 로그에 PawnName 병기 — "Pawn(Clone)" 주체 식별 불가 해소
- 관찰: 설정 메뉴가 harness ESC 주입으로 안 닫힘 — SimInput 의 key 주입이 SettingsMenu 의
  직접 Input 읽기에 미도달 (게임 버그인지 harness 한계인지 추후 판별, 6.4 모달과 함께)
- 관찰: `p1-chop-selected-only` 가 재생성 씬에서 1회 FAIL (지훈 '벌목' 1.0s 합류) — #38 간헐
  재현 가능성.  Chopper 로그 이름 병기 후 반복 실행으로 추적 예정 (UI 와 별건)
  ↳ 해소(2026-06-10 저녁): 간헐이 아니라 **상시 버그**였음 — 우클릭이 지정만 만들고 선택
  림 직접명령이 사문화된 #233 회귀 + 마키 move-order 가로채기.  근본수정 + 재현가드 3종,
  repro_all 12/12.  상세: PLAYTEST-TODO #38 항목.

## 적용 현황 — 배치 2·3 (2026-06-10 밤, 야간 자율 세션)

배치 2 (`809edaf`) 알림/피드백:
- ✅ `#7.1` EventLog 45s 만료 / ✅ `#7.3` 카드 팬 WolfEnemy 포함 / ✅ `#7.4` tier2+ 카드 영속
- ✅ `#7.6`/`#1.3` LowAlert 펄스 매 프레임 / ✅ `#4.1` BuildClickToast 하단 중앙 이동
- ✅ `#7.7` 일부 (ResourceLowAlert·BuildClickToast 폰트 사본 제거 — ArchitectMenu 등 잔여)
- `#1.2` 기하 충돌은 배치1 밴드 계약으로 해소 — AlertStack 통합(1안)은 보류 항목으로 유지

배치 3 (`ecc3c18`) 명령 피드백/하단바:
- ✅ `#0.0` 징집 버튼 다중선택 / ✅ `#0.1` 지정모드 6종 [건축] 하이라이트
- ✅ `#4.2` Open 시 RefreshContent / ✅ `#4.6` ESC 빌드 취소 / ✅ `#4.7` UI 클릭 토스트 스팸 제거

부수 발견 (`37bf170`): 튜토리얼 배너가 alpha 0 에서도 상단 ~230px 밴드 클릭을 투명 차단
— 첫 18초 무음 클릭 무효의 실플레이 버그.  raycast 비차단화로 해소.

## 적용 현황 — 배치 4·5 (2026-06-11 새벽, 야간 자율 세션)

배치 4 inspect/tabs:
- ✅ `#3.1` 라벨 겹침(간격 0.26+plate bounds 실측) / ✅ `#3.6` 전 탭 폰이름 타이틀
- ✅ `#5.2` 시체 행 제거 / ✅ `#5.3` 팝업 3종 상호배타+ESC / ✅ `#2.2` mood<30% 빨강
- ✅ `#3.5` 동물 SpeciesKr (hover/인스펙트 일치)

배치 5 left-resources/system-menus:
- ✅ `#1.0` 칩 폭 자동 확장 / ✅ `#1.1` 식사 칩 +고급N 병기 (+ ≈N일치, 게임필4)
- ✅ `#6.1` (S)/(L)→(F5)/(F9) / ✅ `#6.2` 저장/불러오기 토스트 / ✅ `#6.4` 설정 모달 백드롭
- ✅ `#6.6` 불러오기 2-클릭 암

신규 등재 (게임성 검증 관찰):
- `#1.8` [left-resources] wood=0 카운터 의미 혼란 — '저장구역 내 더미만 카운트' 불변식이
  신규 플레이어에게 '벌목했는데 0' 으로 읽힘.  칩 hover 툴팁(#1.7)에 설명 포함하거나
  '바닥 N' 보조 표기 검토 (P2).
- `#9.0` [system] vignette 런타임 미로드 — VignetteOverlay 가 'Assets/Sprites/vignette.png'
  경로 로드 = 플레이어 빌드에서 항상 실패 (에디터 전용 경로).  Resources/ 이전 또는
  SceneSetup 베이크 시 sprite ref 주입 (S, 부트 경고도 제거됨).

## P0

### [feedback] 우상단 5개 알림 시스템 상호 겹침 — EventLog 패널 위에 AlertStack 카드/토스트/자원경보가 그대로 포개짐  `#7.0`
- **증거**: 좌표 계산: EventLogPanel -72~-232 (SceneSetup.Game.EventLog.cs:27, 240px 폭) vs AlertStack 카드1~3 -88~-232 (AlertStackUI.cs:68,143: -(76+12) 시작, 카드 44+gap 6, 230px 폭) — 같은 사각형을 완전히 공유. 카드 4장 이상이면 BuildClickToast(-148~-186), ResourceLowAlert(-196~-260), ThreatAlertUI(-240~-304)까지 순차 충돌하고, ResourceLowAlert 와 ThreatAlertUI 는 둘만 떠도 -240~-260 20px 겹침. 육안 확인: G:/ai/_longplay/50_600s.png 우상단 — '감기 유행' AlertStack 카드가 EventLog 텍스트('보급품 더미를 발견했다…') 위에 포개져 있고, EventLog 첫 줄은 TopBar 하단(-76)과 카드 상단(-88) 사이 12px 틈에 끼어 글자가 잘린 채 보임. 각 파일 주석(#275 '우상단 계층')은 AlertStack 을 카드 1장으로 가정해 계층을 짰고, EventLogPanel 은 그 체인에 아예 빠져 있음(EventLog.cs:25 주석은 TopBar 60px 이라는 낡은 가정이라 TopBar 76 과도 4px 겹침). 부가(추정): ThreatAlert/ResourceLow/Toast 는 FindFirstObjectByType<Canvas> 첫 캔버스에 붙어 어느 캔버스(scene vs AlertStack sort 200)에 들어가는지 비결정적.
- **수정안**: 우상단 컬럼 밴드 계약을 한 번에 재정렬: (1) AlertStackUI.cs:60 maxCards 6→3 으로 카드 예약 밴드를 -88~-232 로 고정; (2) SceneSetup.Game.EventLog.cs:27 anchoredPosition (-12,-72) → (-12,-244) (카드 예약 밴드 아래, 점유 -244~-404); (3) BuildClickToast.cs:47 (-16,-148) → (-12,-416); (4) ResourceLowAlert.cs:37 (-12,-196) → (-12,-462); (5) ThreatAlertUI 는 아래 finding 대로 제거(존치 시 -540). 동시에 ui-audit.md §3.5 에 이 밴드 테이블을 추가해 우상단도 §3.2 처럼 좌표 계약화.
- **RimWorld 근거**: User_interface: 'Current alerts and suggestions — Top right corner' 단일 readout. Events: envelope 아이콘이 'on the right side of the screen' 에 하나의 스택으로 쌓임 — 독립 박스 5개가 같은 모서리를 나눠 쓰는 구조가 아님.

### [inspect] 상태 탭에서 폰 이름 타이틀이 탭 스트립 버튼 뒤에 깔려 가려짐 (이름 판독 불가)  `#3.0`
- **증거**: G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Editor/SceneSetup.Game.PawnInfo.cs L38-43: titleRt가 패널 상단 밴드(anchor top, pos (10,−8), height 26 → top −8..−34)에 배치. G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Scripts/PawnInfoPanel.cs L195-199: 런타임 EnsureTabs가 tabStrip을 같은 상단 밴드(offsetMin −(12+22)=−34, offsetMax −12 → top −12..−34)에 핀. 런타임 생성 스트립이 늦은 sibling이라 불투명 탭 Image(BtnInactiveBg)가 타이틀 위에 그려짐. 스크린샷 확증: G:/ai/_repro_shots/p0-pawn-move/01_selected.png 좌하단 패널(확대본 G:/ai/_repro_shots/_crop_ghost.png) — 폰 이름은 '상태' 활성 버튼에 완전히 가려지고, 타이틀 rich-text 조각('…는중'(활동 #e8b560), '기치다, 게'(특성 #c0b090), ')')이 탭 버튼 사이 4px 간격으로만 새어 보임. e7d229f/#obj-audit 정규화(L532-547)는 healthText 본문만 옮겼고 타이틀은 누락.
- **수정안**: PawnInfoPanel.cs — healthRectNormalized 블록(L532-547) 패턴 그대로 타이틀 1회 정규화 추가: titleText의 RectTransform을 anchorMin/Max (0,1)/(1,1), pivot (0,1), anchoredPosition (PadOuter, −(PadOuter+TabStripH+TabStripGap)), sizeDelta (−2*PadOuter, 22)로 탭 스트립 '아래' 밴드로 이동, fontSize 22→16(긴 이름+활동+특성 한 줄 수용), verticalOverflow=Truncate 유지 확인. 상태 탭 need 바(bottom y=105/65/25)와는 충돌 없음(타이틀 새 위치는 top 40..62 = bottom 186..208). 추가 상수 TitleBandH=22f+gap을 TabStripH 옆에 선언해 매직넘버 재발 방지.
- **RimWorld 근거**: rimworldwiki.com/wiki/User_interface: "The inspect pane appears whenever the player selects a pawn ... and shows details about that object" — 선택 대상의 라벨이 인스펙트 패널의 1차 정보인데 현행은 그 라벨이 가려져 접근 불가.

### [tabs] SkillUI(스킬 패널)는 영구히 표시 불가능한 죽은 UI — 비활성 GO 위의 Update 가 자기 자신을 켜는 구조  `#5.0`
- **증거**: SceneSetup.Game.SkillPanel.cs:52-61 — SkillUI 컴포넌트를 skillPanelGo 자신에 AddComponent 하고 container 프로퍼티도 skillPanelGo 로 배선한 뒤 마지막에 skillPanelGo.SetActive(false). Unity 는 비활성 GameObject 의 Update() 를 호출하지 않으므로 SkillUI.Update (SkillUI.cs:18-24, container.SetActive(any)) 는 영원히 실행되지 않음. 다른 활성화 경로 전무(grep 'SkillPanel|SkillUI' 전체 0건) + Game.unity:104748-104753 에 m_IsActive: 0 으로 베이크 확인. pawn 을 선택해도 채집/벌목/건축/전투 Lv 표시가 절대 나타나지 않는다. 추가로 되살리기만 하면 위치 (260,60) 180×180 이 PawnInfoPanel (12,58) 380×248 (PawnInfoPanel.cs:319-323 EnsurePlacement 가 런타임 강제) 과 x 260..392 × y 60..240 영역에서 겹치는 잠복 충돌이 있음.
- **수정안**: SceneSetup.Game.SkillPanel.cs 수정 2가지: (1) ResearchUIHost 패턴(SceneSetup.Game.Research.cs:102-106) 미러 — 'SkillUIHost' 별도 GO(항상 활성)에 SkillUI 를 AddComponent 하고 container 만 skillPanelGo 로 배선. (2) skRt.anchoredPosition 을 (260,60) → (400,58) 로 이동(PawnInfoPanel 우측변 392 + 8px 갭; ArchitectMenu 좌측 x=12..292 와도 무충돌). 이후 SceneSetup 재실행으로 씬 재생성. 대안(더 RimWorld 다움): SkillPanel 을 삭제하고 PawnInfoPanel 탭스트립(상태/건강/기분/장비)에 '스킬' 탭으로 흡수.
- **RimWorld 근거**: https://rimworldwiki.com/wiki/Work — "hovering over a skill box displays the colonist's skill level": RimWorld 는 스킬 정보를 별도 떠다니는 패널이 아니라 선택 pawn 인스펙터(Bio)와 Work 탭 셀에 통합해 보여준다.

### [tabs] 연구 상태 스트립(이름+진행바)이 GuiControlBar 탭바에 100% 가려져 연구 진행을 볼 수단이 없음  `#5.1`
- **증거**: SceneSetup.Game.Research.cs:20-24 — ResearchStrip anchor(0.5,0), pivot(0.5,0), 420×36, y=40 → 화면 footprint x±210, y 40..76. GuiControlBar.cs:138-140 — 탭바(ui-audit §3.2 Band A) anchor(0.5,0) y=24, 폭 6*76+4*16+padX*2≈552(±276), 높이 56+16=72 → y 24..96. 스트립이 탭바 footprint 에 완전 포함되고, GuiControlBar 는 런타임 EnsureInScene(GameManager.cs:130)으로 캔버스 마지막 자식이 되어 위에 그려짐(PanelBg alpha 0.94 ≈ 불투명). 스크린샷 01_natural_play.png 검증: '연구:' 텍스트가 화면 어디에도 안 보임. ResearchUI.cs:7 헤더 주석 'bottom-right' 도 실제 배치(bottom-center)와 불일치한 사실(stale comment). Day 75 부터 첫 tech 자동 활성(ResearchManager.cs:63-64)이라 진행 중인 연구가 항상 존재하는데 그 표시가 통째로 사라진 상태.
- **수정안**: SceneSetup.Game.Research.cs:20-24 의 resStripRt 를 우하단 속도 패널 위로 재배치: anchorMin=anchorMax=(1,0), pivot=(1,0), anchoredPosition=(-16,112) (속도 패널 y 24..96 + 16px 갭), 크기 420×36 유지. ResearchUI.cs:7 주석을 실제 위치로 정정. SceneSetup 재실행으로 씬 재생성. (picker 는 중앙 유지.)
- **RimWorld 근거**: https://rimworldwiki.com/wiki/Research — "Once a project is selected, a researcher will work at the bench to generate research points... Only one research project can be studied at a time": 진행 중 프로젝트와 포인트 진행도는 항상 확인 가능한 정보 구조여야 함.

### [top-bar] 좌상단 날짜 readout 연도 클리핑 — "봄 1일," 로 문장이 끊겨 항상 깨져 보임  `#2.0`
- **증거**: SceneSetup.Game.TopBar.cs:57-62 — 날짜 Text 의 sizeDelta=(220,0), fontSize 32 Bold 인데 horizontalOverflow 미설정(기본 Wrap) + verticalOverflow 기본 Truncate. ClockUI.cs:149 가 "봄 1일, 5500년" 을 쓰면 32px bold 한글이 220px 를 넘어 "5500년" 이 둘째 줄로 래핑되고, 76px 바 높이에서 둘째 줄이 수직 잘림 → 연도가 영구히 안 보임. 두 스크린샷 모두에서 확인: 01_natural_play.png "봄 1일," / 50_600s.png "봄 3일," — 쉼표 뒤가 빈 채로 끊긴 문자열이 화면 최상단 가장 눈에 띄는 슬롯에 상시 노출.
- **수정안**: SceneSetup.Game.TopBar.cs GenerateTopBar 의 clockText 에 `clockText.horizontalOverflow = HorizontalWrapMode.Overflow;` 한 줄 추가(자원 칩 Text 는 이미 같은 처리, 248행). 보수적으로 가려면 clockRt.sizeDelta 를 (220,0)→(380,0) 으로도 확대. 씬 재생성 없이 임시로 막으려면 ClockUI.cs:149 를 연도 생략형 `$"{season} {dayInQuadrum}일"` 로 — 단 권장은 overflow 플래그.


## P1

### [architect] 드래그 취소 시 mouse-DOWN 앵커 청사진 1개가 잔류 — '취소했는데 지어짐' 혼란  `#4.0`
- **증거**: BuildManager.cs:207-210 — 빌드 모드 중 GetMouseButtonDown(0) 즉시 단일 셀 배치. BlueprintDragDesignation.cs:27-37 주석이 '앵커 셀은 mouse-DOWN 에 BuildManager 가 이미 배치' 를 명시하고, CancelDrag(266-271)는 고스트만 숨길 뿐 앵커 청사진을 제거하지 않음. 따라서 드래그 중 우클릭/ESC 취소(147-151)해도 누른 지점 청사진 1개가 남는다. 같은 파일 59-63 QA 주석의 '(3) mid-drag cancel → NO blueprints placed' 는 실제 동작과 모순(문서 거짓).
- **수정안**: BlueprintDragDesignation.CancelDrag 에서 dragging==true 였을 때 앵커 셀 정리: pressStartWorld 를 FloorToInt 한 셀 중심에서 Physics2D.OverlapBox 로 BlueprintEntity 를 찾아 press 시점 이후 생성분이면 Destroy (press 시각 기록 필드 추가). 또는 더 근본적으로 BuildManager.Update 의 단일 배치를 GetMouseButtonUp(0)+드래그 미승격 조건으로 이연(HandleLeftClickAt 시그니처 유지, QA 의 SimulateMapClick 경로는 불변). 둘 중 하나 적용 후 59-63 QA 주석을 실동작에 맞게 수정.

### [architect] BuildClickToast(우상단 y=-148)가 알림 카드 2장 이상일 때 AlertStack 과 겹침  `#4.1`
- **증거**: BuildClickToast.cs:46-47 — 우상단 고정 (-16,-148), 360x38. AlertStackUI.cs:61-68,143,256-257 — 첫 카드 y=-88, 카드 44px+gap 6 → 2번째 카드 -138~-182, 폭 230 우측정렬. 토스트 밴드 -148~-186 과 x∈[-16,-242] 구간에서 겹침. BuildClickToast.cs:42 주석 '#275 AlertStack 카드군(-88~) 아래에 둬 겹침 제거' 는 카드 1장 가정의 매직오프셋 — 식량부족+위협 등 동시 알림이 흔해 빌드 클릭 시 재현 가능. (조건부·일시적 겹침이라 P0 아닌 P1.)
- **수정안**: BuildClickToast 를 알림 전용 구역에서 빼낸다: 앵커를 하단 중앙 탭바 위(예: anchor(0.5,0), y=110, GuiControlBar 밴드 위)로 이동 — 클릭 지점과 시선이 가까워 피드백 인지도 상승. 우상단 유지가 필수라면 AlertStackUI 에 public float CurrentStackBottomY 를 노출해 Show() 시점에 rt.anchoredPosition.y = min(-148, bottom-8) 로 동적 회피.
- **RimWorld 근거**: User_interface 페이지: 'Current alerts and suggestions — Top right corner' — 우상단은 알림 스택 전용 구역이라는 컨벤션

### [bottom-bar] 하단바 [징집] 버튼이 마퀴 다중선택에서 무동작 — R 핫키와 GUI 동작 불일치  `#0.0`
- **증거**: GuiControlBar.cs:294-304 ToggleDraft() 는 cs.CurrentSelection(단일 선택)만 읽고 null 이면 로그만 남기고 return. 마퀴 다중선택 시 MarqueeSelector 가 ClickSelector 단일 선택을 비움(MarqueeSelector.cs:350-358 ClearClickSelectorSingle) → 버튼 클릭이 no-op. 반면 R 핫키는 ClickSelector.cs:153-156 → ToggleDraftOnSelection()(399-422행, 다중선택 전원 일괄 토글, 운영자 fb 반영 완료)을 탐. 다중선택 징집을 처리하던 SelectionGizmoBar 는 #232 로 비활성(SelectionGizmoBar.cs:103-106). 또한 Update 의 징집 하이라이트(GuiControlBar.cs:337-343)도 CurrentSelection 만 봐서 다중선택 전원 징집 상태가 버튼에 반영 안 됨. GuiControlBar 의 존재 이유가 '키보드 의존 제거'(파일 헤더 7-9행)인데 핵심 명령이 핫키 전용으로 회귀한 상태.
- **수정안**: ClickSelector.cs:399 ToggleDraftOnSelection() 을 private→public 으로 변경. GuiControlBar.cs ToggleDraft() 본문을 'if (cachedCs == null) cachedCs = Object.FindFirstObjectByType<ClickSelector>(); if (cachedCs != null) cachedCs.ToggleDraftOnSelection();' 로 교체(중복 징집 로직 제거 + per-click Find 제거 겸사). Update 의 draft 하이라이트는 cached MarqueeSelector.HasMultiSelection 일 때 첫 생존 림의 IsDrafted 를 읽도록 분기 추가. 신규 로직 없음 — 기존 공개 경로 재사용.
- **RimWorld 근거**: rimworldwiki.com/wiki/Controls: "LEFT MOUSE BUTTON (Click and drag) — Draw square to select multiple items" — 박스 다중선택은 기본 조작이며, 선택 대상 전원에 명령이 가는 것이 컨벤션.

### [bottom-bar] 지정 모드(채광/해체/경작 등) 진입 후 화면에 활성 모드 표시가 전혀 없음 — 보이지 않는 모드  `#0.1`
- **증거**: ArchitectMenu.cs:500-506 — Orders/Zone 항목 클릭 시 SetMode(true) 직후 Close() 로 메뉴가 닫힘. 메뉴 안에서는 ▣ + BtnActiveBg 로 활성 표시(494-499행)하지만 닫힌 뒤에는 어떤 indicator 도 없음. GuiControlBar.cs:322-327 의 [건축] 버튼 하이라이트는 BuildManager.BuildModeActive 만 검사 — Mine/Deconstruct/GrowZone/TreeChop/Roof/Stockpile 의 ModeActive 는 미포함. 핫키(M/X/P) 진입 시도 동일. 즉 채광 모드 활성 중 화면은 평시와 100% 동일해서, 좌클릭이 광맥을 지정해버리거나 우클릭이 모드만 취소되는 이유를 운영자가 알 수 없음 ('현재 모드' 류 배너 grep 결과 0건).
- **수정안**: 기존 하이라이트 메커니즘 확장(신규 UI 없음): GuiControlBar.cs Update 의 RefreshBuildHighlight(architectBtn, ...) 인자를 'BuildManager.Instance.BuildModeActive || (MineDesignation.Instance != null && MineDesignation.Instance.ModeActive) || (DeconstructDesignation.Instance != null && ...) || GrowZone/TreeChop/Roof/Stockpile 동일 패턴' OR 체인으로 교체 — 지정 모드 동안 [건축] 버튼이 노랗게 유지되어 '모드 안'임이 보임. 전부 기존 public ModeActive 프로퍼티 read-only 폴링이라 기능동결 위반 없음.
- **RimWorld 근거**: rimworldwiki.com/wiki/User_interface: "Architect menu — Bottom left corner — Use the Architect menu to access submenus which allow you to build your colony base, place or change objects" — 지정 도구는 Architect 소속이며, 원작에서 선택된 designator 가 패널/커서에 활성 표시되는 것은 게임 내 컨벤션(위키 stub 에는 명시 문구 없음).

### [bottom-bar] ControlHint 텍스트가 탭바 뒤에 잘린 채 렌더 — '가리기' 함수가 실제로는 안 가림  `#0.2`
- **증거**: SceneSetup.Game.SaveHint.cs:48-53 — ControlHint 는 anchor(0.5,0), y=14, 640x20, fontSize 18. GuiControlBar 탭바 패널은 y=24..96, 폭 544 중앙(BuildLayout, GuiControlBar.cs:139-140). HideOldHintIfPresent(GuiControlBar.cs:86-96)는 이름과 달리 hide 하지 않고 텍스트를 "🖱 좌클릭=선택 · 우클릭=이동/작업 · ESC=빌드취소" 로 교체만 함 → y=14..34 중 24..34 가 탭바에 가려져 글자 아랫부분 10px 띠만 바 밑으로 비져나옴. 01_natural_play.png 하단 중앙에서 탭바 뒤로 잘린 텍스트 잔재 육안 확인. 같은 안내는 TutorialOverlay(01_selected.png 상단 '나무·작물 우클릭 = 작업...')와 HotkeyCheatSheet 가 이미 담당 — 중복 표시.
- **수정안**: GuiControlBar.cs HideOldHintIfPresent 에서 텍스트 교체 대신 hint.SetActive(false) 호출(이름값 하게). 마우스 힌트를 살리고 싶으면 대안으로 hintRt.anchoredPosition = new Vector2(0, 100) (Band A 패널 상단 96 위)으로 이동 — 단 TutorialOverlay 와 중복이므로 비활성화 권장.

### [feedback] EventLog 항목이 영구 잔존 — 마지막 3개 이벤트가 우상단 240x160 을 영원히 가림  `#7.1`
- **증거**: EventLogUI.cs:31-36 HandleEvent 는 Enqueue 후 maxEntries(3) 초과분만 Dequeue — 시간 기반 만료가 전혀 없어 첫 이벤트 이후 패널이 영구 표시됨. G:/ai/_longplay/99_1188s.png (1188초 시점) 우상단에 한참 지난 '사기 저하'/'정직한 나무꾼' 텍스트가 여전히 떠 있음. 오래된 이벤트가 최신처럼 보여 혼란 + 우상단 상시 가림('이벤트 텍스트 패널이 화면을 너무 가리지 않는가' 관점 직접 해당).
- **수정안**: EventLogUI.cs: Queue<string> entries 를 Queue<(string text, float time)> 로 바꾸고 HandleEvent 에서 Time.unscaledTime 기록, Update 에서 45초 경과 항목 Dequeue 후 Refresh. 빈 상태 bg 숨김(42-44행)이 이미 있어 만료가 끝나면 패널이 자연 소멸. 영속이 필요한 위협은 이미 AlertStack 카드가 담당.
- **RimWorld 근거**: Events: 사소한 알림은 'only a minor message'(일시 표시)이고, 지속되는 것은 작은 envelope 아이콘 — 텍스트 패널이 상시 점유하는 컨벤션 없음.

### [feedback] 위협 알림 이중화 — ThreatAlertUI 가 AlertStackUI 와 같은 위협을 중복 표시 (구식 스톱갭 잔존)  `#7.2`
- **증거**: ThreatAlertUI.cs 헤더 주석 자체가 'EventLog 텍스트만 보임' 시절의 보완책임을 명시하는데, 이후 AlertStackUI(wiki #22)가 랜딩되어 threatTier>=1 이벤트마다 영속 카드를 띄움(AlertStackUI.cs:180-191). 현재 늑대/산적 접근 시 ① AlertStack 카드 ② ThreatAlertUI 의 32px 빨강 '⚠ 늑대 위협!' 플래시(-240, ResourceLowAlert 밴드와 20px 겹침)가 동시에 뜨는 이중 표시. GameManager.cs:150 이 여전히 ThreatAlertUI.EnsureInScene() 호출. 매 0.5s FindObjectsByType 전수 폴링 비용도 중복.
- **수정안**: GameManager.cs:150 의 ThreatAlertUI.EnsureInScene() 호출 제거 + ThreatAlertUI.cs 삭제(죽은 시스템 정리). 근접 감지(거리<8)의 즉시성이 가치 있다고 판단되면 별도 대형 텍스트 대신 AlertStackUI 에 카드 1장 푸시하는 형태로 위임 — 신규 시스템 아님, 기존 카드 재사용.
- **RimWorld 근거**: User_interface: 위협 알림은 우측 letter 스택 + 'Current alerts and suggestions' readout 단일 채널 — 동일 위협에 대형 텍스트 플래시를 중복으로 띄우는 컨벤션 없음.

### [feedback] AlertStack 카드 클릭 팬이 늑대 이벤트에서 엉뚱한 곳으로 이동 — WolfEnemy 미탐색  `#7.3`
- **증거**: AlertStackUI.cs:277-310 ResolveThreatTarget 는 BanditEnemy 만 검색(331-338행 FindBandits)하고 WolfEnemy 는 전혀 보지 않음. wolf_pack 카드를 클릭하면 ① 맵에 산적이 있으면 산적에게 팬(완전히 다른 위협), ② 없으면 콜로니 중심으로 팬(늑대 무시). 클래스 doc 의 QA FLAG 가 휴리스틱임을 자인하지만 늑대 누락은 그 안에서도 명백한 구멍 — ThreatAlertUI.CheckThreats(96행)는 WolfEnemy 를 검색하므로 타입은 존재.
- **수정안**: AlertStackUI.ResolveThreatTarget 에 WolfEnemy 검색 추가: FindObjectsByType<WolfEnemy> 를 FindBandits 와 동일 패턴으로 넣고 '콜로니에 가장 가까운 적(산적∪늑대)' 으로 타겟 선정. 근본 해결(이벤트별 sourcePos)은 GameEvent 필드 추가가 필요하므로 별도 wave 로 유지(코드 내 QA FLAG 의 기존 제안 그대로).
- **RimWorld 근거**: Events: 'Clicking the envelope icon offers the option to jump to the center point' — letter 클릭은 해당 이벤트 발생 지점으로 점프하는 것이 컨벤션.

### [feedback] 위협 카드가 30초 뒤 무조건 자동 소멸 — 진행 중인 레이드 알림이 사라짐 (자체 acceptance 'persistent' 와 모순)  `#7.4`
- **증거**: AlertStackUI.cs:59 cardLifetimeSec=30, ExpireCards(233-246행)가 tier 무관 일괄 만료. 클래스 doc 의 수용 기준은 'A raid creates a persistent top-right card' 인데 산적이 살아있는 동안에도 카드가 30초면 사라져 운영자가 미대응 위협을 다시 놓침 — 이 스택을 만든 원래 목적(loop-legibility gap) 회귀.
- **수정안**: AlertCard 에 int tier 필드 추가(HandleEvent 에서 전달), ExpireCards 에서 tier>=2 카드는 시간 만료 제외(클릭 dismiss 만, 기존 OnCardClicked 경로 그대로), tier1 만 30s 유지. maxCards 초과 시 eviction(204-209행)은 그대로 둬 오버플로 안전 유지.
- **RimWorld 근거**: Events: 'red denotes direct threats' — 직접 위협 letter 는 플레이어가 처리할 때까지 우측 스택에 남는 것이 레퍼런스 동작.

### [inspect] 머리위 이름/활동 라벨 두 줄이 서로 겹치고, 네임플레이트가 이름 줄을 덮지 못함  `#3.1`
- **증거**: G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Scripts/PawnNameLabel.cs — L14 name y=0.98, L96 status y=0.98−0.15=0.83. 줄 높이 계산: name fontSize 64 × characterSize 0.05 → 글리프 높이 ≈0.32wu(0.82..1.14), status 44×0.0425 ≈0.19wu(0.74..0.92) → 중심 간격 0.15 < 반높이 합 0.25, 약 0.10wu 겹침. 두 MeshRenderer 모두 sortingOrder 30(L90, L106)이라 어느 쪽이 위인지도 비결정. 플레이트: L71 center 0.905 + L148 높이 공식 characterSize*3.4+0.05=0.22 → 0.795..1.015만 커버, 이름 위쪽 ~0.12wu가 플레이트 밖. ResizePlate(L137-150)는 폭만 bounds 실측, 높이는 매직 공식. 스크린샷 확증: G:/ai/_repro_shots/p1-mood-negative/01_natural_play.png(확대 _crop_jihoon.png) — '지훈'/'민지' 모두 '떠도는중' 상단 절반이 이름 글리프에 가려 판독 곤란, 금색 이름 상단이 플레이트 밖 잔디 위에 노출. 헤더 주석(L12 'status(0.80)')과 코드(0.83)도 불일치.
- **수정안**: PawnNameLabel.cs — (1) status 줄 간격 0.15→0.26 (L96: offset.y−0.26), name offset 0.98→1.06으로 동반 상향해 HP 바(0.68 밴드)와 간격 유지. (2) ResizePlate에서 높이도 TextWorldWidth와 같은 bounds 패턴으로 실측: 두 TextMesh MeshRenderer.bounds의 min/max Y union + characterSize*0.6 패딩으로 plate localScale.y와 localPosition.y를 함께 갱신. (3) statusMr sortingOrder 30→31로 올려 겹침 잔존 시에도 활동 라벨이 위에 오게. (4) L12 주석의 0.80을 실제 값과 동기화.

### [left-resources] 4자리 자원값이 배경 패널 밖 맵 위로 넘침 (고정 칩폭 184 + Overflow)  `#1.0`
- **증거**: SceneSetup.Game.TopBar.cs:106 kChipW=184 고정, :117 패널폭 = kChipW+12=196(우측 끝 x≈204), :132-134 ContentSizeFitter 가 verticalFit 만 PreferredSize(horizontalFit=Unconstrained), :248 horizontalOverflow=Overflow, :251 txtLe.flexibleWidth=1 이라 칩이 텍스트 따라 못 늘어남. 실측: G:/ai/_longplay/99_1188s.png (day 4 일반 플레이) '목재: 1,013' 의 ',013' 과 '석재: 292' 끝자리가 어두운 배경 우측 경계를 넘어 맵 타일 위에 직접 렌더됨 (크롭 확인). 어두운 맵에선 간신히 읽히지만 밝은 지형(모래/눈) 위에선 배경 없는 숫자가 대비를 잃음. N0 포맷(#17) 으로 콤마까지 붙어 4자리 도달이 빠름.
- **수정안**: SceneSetup.Game.TopBar.cs: (1) resFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize 로 변경, (2) MakeResChip 에서 chipLe.preferredWidth=chipW 를 제거하고 chipLe.minWidth=chipW 만 유지(최소폭 보장), (3) txtLe.flexibleWidth=1 제거 → Text 의 preferredWidth 가 칩 폭을 결정하게 해 자릿수가 늘면 배경 패널이 함께 넓어지게 한다. 4칩 중 최대 텍스트폭으로 전체 패널이 늘어나는 것은 VerticalLayoutGroup.childControlWidth=true + childForceExpandWidth=false 조합이라 칩별 폭이 달라질 수 있으니, 균일폭을 원하면 childForceExpandWidth=true 로 패널폭=최장칩으로 통일. 씬 재생성 필요 (Text 명 FoodText/MealsText/WoodText/StoneText, ResIcon_<key> 명 보존 — ResourceCounterUI 와이어링은 참조 바인딩이라 무영향).
- **RimWorld 근거**: RimWorld 자원 readout 은 좌상단 목록 전체가 자체 배경 위에 그려지고 숫자가 배경 밖으로 새지 않음 (rimworldwiki.com/wiki/User_interface 'resource list' — 직접 fetch 는 403 차단, 검색 발췌로 확인)

### [left-resources] fineMeals(고급식사 #131)가 HUD 어디에도 표시되지 않아 운영자가 식량 상태를 오판  `#1.1`
- **증거**: ResourceManager.cs:22 public int fineMeals 존재. StoveEntity.cs:22-29 CookOne(fineMeal:true) 가 AddFineMeals(+1) (Build skill 5+ 폰 조리 시 meals 대신 fineMeals 증가), PawnNeeds.cs:585 가 fineMeals 우선 소비, ResourceLowAlert.cs:78 totalFood = food + meals*3 + fineMeals*5 로 경고 판정에 포함. 그러나 ResourceCounterUI.cs 는 wood/food/meals/stone 4종만 표시하고 grep 결과 fineMeals 를 그리는 UI 가 전무 (ResourceMonitorLogger 디버그 로그와 테스트 러너만 참조). 결과: 숙련 요리사가 조리해도 '식사:' 숫자가 안 늘어 #130 이 해소하려던 '값이 안 늘어' 혼란이 그대로 재발하고, 화면엔 '식량: 0 / 식사: 0' 인데 fineMeals=1 이면 totalFood=5 라 부족 경고도 안 떠 표시값과 경고가 모순된다.
- **수정안**: 표시만 추가 (기능동결 준수, 신규 시스템 아님): SceneSetup.Game.TopBar.cs GenerateTopBar 에 5번째 칩 추가 — Text fineMealsText = MakeResChip(resRowGo, "FineMealsText", "고급식사: 0", "meal"(기존 아이콘 재사용), uiFont, UITheme.AccentGold 계열, kChipW) 를 식사 칩 바로 아래 순서로 삽입하고 rcSo.FindProperty("fineMealsText") 와이어링 추가. ResourceCounterUI.cs 에 [SerializeField] Text fineMealsText + lastFineMeals + meals 블록과 동일한 갱신/flash 블록 추가. 대안(더 작은 diff): mealsText 갱신을 $"식사: {rm.meals:N0} (+고급 {rm.fineMeals:N0})" 로 — 단 fineMeals=0 일 땐 접미 생략.
- **RimWorld 근거**: RimWorld ResourceReadout 은 simple meal / fine meal 을 별개 항목으로 각각 아이콘+수량 표시 (categorized mode 에선 Foods 카테고리로 묶임 — Steam 토론 'Categorized mode... collapsible format, sorted according to category' 발췌 확인)

### [left-resources] ResourceLowAlert 매직 오프셋(-196)이 동적 AlertStack 과 기하 충돌 — 위협 카드 3장부터 부족 경고가 가려짐  `#1.2`
- **증거**: ResourceLowAlert.cs:37 anchoredPosition (-12,-196), 크기 280x64 → 점유 대역 y -196..-260. AlertStackUI.cs:143 카드 시작 y=-88, 카드 44px + 간격 6px, maxCards=6 → 3번째 카드가 -188..-232 로 ResourceLowAlert 와 겹치고 최대 -388 까지 내려옴. AlertStack 은 자체 캔버스 sortingOrder=200(:127), ResourceLowAlert 는 FindFirstObjectByType<Canvas>() (:22) 가 잡히는 임의 캔버스(통상 scene 캔버스, sort 더 낮음)에 붙어 위협 카드가 부족 경고를 덮는다. 식량 부족과 위협 다발(습격+늑대+멘붕)은 동시 발생하기 쉬운 시나리오 — 99_1188s.png 에서 이미 평시 day 4 에 카드 2장('사기 저하' x2)이 떠 있어 3장 도달은 현실적. 주석 '#275 TopBar+AlertStack+Toast 아래'는 스택이 고정 높이라는 잘못된 가정의 손계산. 또한 우상단에 손으로 쌓은 popup 이 3계열(AlertStack -88~, BuildClickToast -148, ResourceLowAlert -196) 공존하는 중복 패턴.
- **수정안**: 1안(권장, 정리 효과 최대): ResourceLowAlert 의 자체 popup 을 삭제하고 AlertStackUI 에 합류 — AlertStackUI 에 public static void PushPersistent(string title, Color c) 또는 조건형 카드 API 를 추가해 wood<5 / totalFood<5 동안 카드 1장을 유지(조건 해소 시 제거), ResourceLowAlert.cs 는 조건 판정 + 카드 lifecycle 만 남긴다. RimWorld 도 low food 를 별도 popup 이 아닌 우측 알림 스택의 빨간 alert 로 처리. 2안(최소 diff): ResourceLowAlert.cs:37 의 y 를 AlertStack 최대 신장(-88 - 6*50 = -388) 아래인 -400 으로 내리거나, 좌상단 자원 목록 바로 아래(정보 인접 — 부족 경고가 해당 수치 옆)로 이동: anchorMin/Max (0,1), anchoredPosition (8, 자원패널 하단-8).
- **RimWorld 근거**: RimWorld 의 'Low food' 는 우측 가장자리 단일 알림 스택의 빨간 alert (built-in red 'low food' alert at the right side of the screen — Steam/GitHub MoreAlerts 'more Alerts in the righthand sidebar' 발췌 확인); 병렬 popup 시스템을 두지 않음

### [system-menus] 설정 버튼의 "(ESC)" 핫키 힌트가 거짓 — ESC 로 메뉴를 열 수 없음  `#6.0`
- **증거**: GuiControlBar.cs:195 `MakeBtn("설정", "(ESC)", x, () => SettingsMenu.ToggleStatic(), ...)` 로 힌트만 표기. 전 Scripts grep 결과 Escape 바인딩은 designation 취소 7곳(Mine/Grow/Deconstruct/Stockpile/Roof/TreeChop/BlueprintDrag), HotkeyCheatSheet:179·TutorialOverlay:57 닫기, SettingsMenu.cs:243(열려 있을 때 닫기)뿐 — 여는 코드 없음. 게다가 SettingsMenu.Close() 가 gameObject.SetActive(false)(line 234) 라 닫힌 상태에선 Update 자체가 안 돌아 자가 처리 불가. 스크린샷(01_natural_play.png) 하단바에서 (Space)/(F1)/(F8) 등 다른 힌트는 전부 실동작 핫키인데 (ESC) 만 미작동 — 운영자가 ESC 를 눌러도 무반응(또는 지정모드 취소만).
- **수정안**: GuiControlBar.cs Update(line 320)에 ESC-open 추가: `if (Input.GetKeyDown(KeyCode.Escape) && !SettingsMenu.IsOpen && Time.frameCount != SettingsMenu.LastEscFrame && 빌드/지정 모드 비활성) SettingsMenu.Open();`. SettingsMenu.cs 에 `public static bool IsOpen`, `public static int LastEscFrame` 추가(Close/OpenInternal 에서 Time.frameCount 기록)해 같은 프레임 닫기→재열림 레이스 차단. 지정모드 활성 시엔 기존 취소 동작 우선(RimWorld 도 ESC 는 열린 다이얼로그 닫기가 우선).
- **RimWorld 근거**: rimworldwiki.com/wiki/Controls — "Esc: Closes any open dialogue or opens the Game Menu"

### [system-menus] 저장(S)/불러오기(L) 괄호 힌트가 죽은 핫키이며 실제 키 바인딩(카메라 S·램프 L)과 충돌  `#6.1`
- **증거**: SettingsMenu.cs:183 "💾 저장(S)", :191 "📂 불러오기(L)". 실제 저장/불러오기 핫키는 F5/F9 (GameSaveButtons.cs:58-59). 한편 KeyCode.S 는 카메라 아래 이동(CameraController.cs:109), KeyCode.L 은 램프 건설 모드 토글(BuildManager.cs:192) — 하단바 힌트 컨벤션((Space)/(F1)/(ESC))과 동일 표기라 운영자가 핫키로 오인해 L 을 누르면 설정 패널 뒤에서 램프 건설 모드에 진입한다. (S)/(L) 은 #245 로 제거된 구 코너 버튼 글리프의 잔재.
- **수정안**: SettingsMenu.cs:183/191 버튼 라벨을 "💾 저장(F5)" / "📂 불러오기(F9)" 로 교체 (실핫키와 일치). GameObject 이름 SettingsSaveBtn/SettingsLoadBtn 은 유지(테스트 이름보존 규칙).

### [system-menus] 저장/불러오기 화면 피드백 전무 — 성공/실패/세이브없음 모두 Debug.Log 뿐  `#6.2`
- **증거**: SaveLoadManager.cs:288(저장 성공 Debug.Log), :298(세이브 없음 LogWarning+null), :310/:315(손상 LogError). GameSaveButtons.OnLoad 는 data==null 이면 조용히 return(line 72). SettingsMenu.OnSaveClicked/OnLoadClicked 는 PlayClickBlip(공용 클릭음)만 — 운영자는 저장이 됐는지, 세이브가 없어서 로드가 무시됐는지 화면에서 알 수 없음. 세이브 없는 첫 세션에서 불러오기 클릭 시 아무 일도 안 일어나 보임(헤맴 확정).
- **수정안**: 기존 토스트 재사용(신규 시스템 아님): GameSaveButtons.OnSave 끝에 `BuildClickToast.EnsureInScene(); BuildClickToast.Instance?.ShowSuccess("💾 저장 완료");`, OnLoad 에서 data==null 이면 `ShowFail("✗ 불러올 세이브 없음/손상")` 후 return, 성공 경로 끝에 `ShowSuccess("📂 불러오기 완료")` (BuildClickToast.cs:81-82 API). 설정행·F5/F9 모두 이 캐논 경로를 타므로 한 곳 수정으로 전 경로 커버.

### [tabs] 죽은 pawn(시체)이 직업/일정 탭에 계속 행으로 나타나고 우선순위 편집까지 가능  `#5.2`
- **증거**: WorkTabUI.cs:135-144 / ScheduleUI.cs:113-121 — FindObjectsByType<PawnEntity> 결과를 IsDead 필터 없이 그대로 행으로 생성(null/컴포넌트 체크만 있음). PawnHealth.cs:53 주석 'Dead → 회색조 corpse + 90° 쓰러짐' — 사망 후에도 GameObject(와 PawnWorkSettings/PawnSchedule)가 시체로 존속하므로 두 탭 모두 시체 행을 표시하고 클릭 시 우선순위/슬롯도 바뀐다(아무 효과 없음 → 운영자 혼란). 같은 파일군의 SkillUI.cs:22 는 이미 !pawn.IsDead 필터를 쓰고 있어 패턴 불일치이기도 함.
- **수정안**: WorkTabUI.cs:142 'if (p == null) continue;' → 'if (p == null || p.IsDead) continue;' / ScheduleUI.cs:119 동일 수정. (탭이 열린 채 사망 시 다음 RefreshGrid 에서 자연 제거됨.)
- **RimWorld 근거**: https://rimworldwiki.com/wiki/Work — "The Work Menu allows the player to set the types of work you want your colonists to perform": Work/Schedule 탭의 행은 살아있는 콜로니스트만 대상이며 사망 pawn 은 목록에서 제거된다.

### [tabs] 중앙 팝업 3종(직업/일정/연구 picker)이 상호 배타가 아니고 ESC 로도 닫히지 않아 겹쳐 쌓임  `#5.3`
- **증거**: WorkTabUI(중앙앵커 638px), ScheduleUI(중앙앵커 652px), ResearchPicker(중앙앵커 680×280, SceneSetup.Game.Research.cs:78-83) 모두 화면 정중앙. GuiControlBar.cs:182-183, 314-318 의 토글은 서로를 닫지 않으며 각 Open() 은 SetAsLastSibling 만 호출(WorkTabUI.cs:303, ScheduleUI.cs:196) → F1+F4+N 을 누르면 3장이 겹쳐 아래 패널 가장자리가 비져나오고 클릭도 일부 통과. ESC 처리도 전무: KeyCode.Escape grep 결과 designation 모드들과 SettingsMenu(자기 닫기, SettingsMenu.cs:243)만 처리하고 WorkTabUI/ScheduleUI/ResearchUI 는 없음 — 게임 전반의 'ESC=취소' 관례(DeconstructDesignation.cs:189 등 7곳)와 불일치.
- **수정안**: (1) WorkTabUI.Open() 에 'if (ScheduleUI.Instance?.IsOpen == true) ScheduleUI.Instance.Close();' + ResearchUI picker 닫기, ScheduleUI.Open() 에 대칭 코드, ResearchUI.TogglePicker/N키 열기 시 두 탭 Close — ResearchUI 에 public bool PickerOpen => pickerOpen 과 public void ClosePicker() 추가. (2) 세 클래스 Update 에 'if (열림 && Input.GetKeyDown(KeyCode.Escape)) Close/ClosePicker();' 추가(SettingsMenu 는 isOpen 일 때만 ESC 를 소비하므로 충돌 없음).
- **RimWorld 근거**: https://rimworldwiki.com/wiki/Schedule (Menus) — "The following menus are found along the bottom edge of the screen. The first eight menus are bound to hotkeys F1 through F8": RimWorld 하단 메뉴 탭은 한 번에 하나만 열리는 배타 구조.

### [top-bar] ColonistBar 0.5초마다 무조건 전체 파괴-재생성 — 클릭 간헐 유실 + hover 플리커 + 매 프레임 배열 alloc  `#2.1`
- **증거**: ColonistBar.cs:115 `if (count != lastColonistCount || Time.unscaledTime >= nextRebuildTime)` — 둘째 조건이 무조건 참이 되어 로스터 변화가 없어도 0.5s 마다 RebuildEntries() 가 모든 entry GameObject 를 Destroy+재생성(139행). Unity Button 은 pointer-down 과 pointer-up 사이에 파괴되면 onClick 이 발화하지 않음 → ~100ms 클릭 기준 약 1/5 확률로 바 클릭이 먹히지 않는 간헐 오동작(코드 구조상 결정적 귀결이나 영상 재현은 안 함 — 이 부분은 추정), hover 하이라이트도 0.5s 마다 리셋. 추가로 CountColonists()(129행)가 매 프레임 FindObjectsByType 호출로 배열을 할당 — 클래스 헤더 주석 "매 frame ... 재구축 X, alloc X"(22행)와 모순.
- **수정안**: ColonistBar.Update 재구성: CountColonists/로스터 스캔을 `Time.unscaledTime >= nextRebuildTime` 블록 안으로 이동(0.5s 캐던스), RebuildEntries 는 멤버십이 실제로 바뀐 때만(count 변화 또는 캐시된 e.pawn 이 null/IsDead) 호출. 이름 갱신이 필요하면 Entry 에 nameText ref 를 저장해 0.5s 캐던스에서 텍스트만 갱신 — GameObject 파괴 금지. RefreshFills 는 현행 유지.

### [top-bar] mood 미니바에 저기분 색 경고 없음 — HP 는 30% 미만 빨강인데 mood 는 0% 여도 파랑  `#2.2`
- **증거**: ColonistBar.cs:347-348 HP fill 은 `ratio < 0.30f ? HpLowCol : HpFillCol` 로 저체력 빨강 전환, 반면 351-357행 mood fill 은 어떤 값에서도 MoodFillCol(파랑) 고정. 검토 스크린샷 폴더명이 바로 p1-mood-negative — mood 위기 재현 중인데 전담 인디케이터가 5px 두께 바의 '길이' 외에 아무 신호도 주지 않아 운영자가 위기를 놓침. HP/mood 두 바가 같은 위치·같은 두께인데 경고 규칙만 비대칭 → 일관성도 깨짐.
- **수정안**: ColonistBar.cs UpdateEntryFill 의 mood 분기에 HP 와 동일 패턴 한 줄: `if (e.moodFill != null) e.moodFill.color = ratio < 0.30f ? HpLowCol : MoodFillCol;` (기존 HpLowCol 상수 재사용, 신규 시스템 없음). 원하면 0.30~0.45 구간 amber 중간 단계는 후속 폴리시.
- **RimWorld 근거**: 위키 'Colonist bar — Top — Shows the colonists' names, appearance, and status icons' (페이지가 stub 이라 mood 표시 세부는 미기재 — 본 위키 페이지에서 직접 확인한 근거는 status 표시가 콜로니스트 바의 책무라는 것까지)


## P2

### [architect] Open() 이 RefreshContent 를 호출하지 않아 재오픈 시 ▣ 활성표시/자재 dim 이 낡은 상태  `#4.2`
- **증거**: ArchitectMenu.cs:725 Open() = SetActive+SetAsLastSibling 만. RefreshContent 호출처는 Awake(BuildMenu), 카테고리 헤더 클릭, Update 의 자원변화 감지(745-754)뿐. 시나리오: 메뉴에서 '벌목(C)' 클릭 → invoke+Close → 자원 변화 없이 F8 재오픈 → Orders 카테고리는 펼쳐진 채(activeCategory 유지)인데 벌목 행에 ▣/BtnActiveBg 가 없음(렌더 시점엔 모드 off 였으므로). 반대로 우클릭으로 모드 해제 후 재오픈하면 ▣ 가 남아 보이는 역방향 stale 도 동일 경로.
- **수정안**: ArchitectMenu.cs Open() 을 { isOpen=true; gameObject.SetActive(true); transform.SetAsLastSibling(); RefreshContent(); } 로 변경. RefreshContent 는 이미 idempotent(전체 재구성)이고 열 때 1회라 비용 무시 가능.

### [architect] buildable 행은 활성 빌드모드 표시(▣/ActiveBg)가 없음 — Orders/Zone 행과 피드백 비대칭  `#4.3`
- **증거**: ArchitectMenu.cs:494-499 — Orders/Zone 행은 isActive() 로 '▣ ' 접두 + BtnActiveBg 반영. 반면 537-577 buildable 행은 CurrentMode 비교 없이 항상 BtnInactiveBg/일반 라벨. onClick(569-575)은 CurrentMode==bcap 이면 Off 로 토글하는데, 시각적으로는 어느 행이 켜져 있는지 알 수 없어 '같은 걸 또 누르면 꺼진다' 는 동작이 발견 불가.
- **수정안**: RefreshContent 의 buildable 루프에서 bool on = BuildManager.Instance != null && BuildManager.Instance.CurrentMode == mode; 를 계산해 Orders 행과 동일하게 label 앞 "▣ " + rowBg 를 UITheme.BtnActiveBg 로 교체(affordable dim 보다 우선). 위 Open() 리프레시 수정과 결합하면 재오픈 시 현재 모드가 즉시 보임.

### [architect] 석재 벽 아이콘 회귀: SpriteForMode 에 WallStone 케이스 부재 → '회색 틴트 목재 스프라이트' 머디브라운 버그 재발 + IconKey 수정이 죽은 코드화  `#4.4`
- **증거**: BuildManager.cs:265-282 SpriteForMode 는 WallStone 케이스가 없어 `_ => wallSprite`(목재 벽)로 폴백. ArchitectMenu.cs:551-557 은 BuildManager.Instance 존재 시(게임 중 항상) SpriteForMode 를 우선 사용하고 석재 모드는 회색 틴트(0.72,0.72,0.78) — 이는 ArchitectMenu.cs:268-271 주석이 '갈색 wall_wood 회색 tint = muddy brown, 목재와 구분 불가' 라며 stone_floor 로 고쳤다고 기록한 바로 그 증상의 재발(#255 가 경로를 바꾸면서 회귀). IconKey/LoadIcon(265-358)의 stone_floor 수정은 Instance==null 폴백에서만 살아있는 사실상 죽은 경로. 부수 피해: SetMode(258)·DoTryPlaceAt(583, SpriteForCurrentMode 635-652 역시 WallStone 부재)이라 석재 벽 모드의 커서 고스트/청사진 스프라이트도 목재 벽 갈색으로 표시. SpriteForCurrentMode 는 SpriteForMode(CurrentMode)와 완전 중복 switch(정리 대상).
- **수정안**: BuildManager.cs SpriteForMode 에 `Mode.WallStone => EnsureFloorStoneSprite()`(회색 석재) 추가 — 메뉴 아이콘·고스트·청사진이 한 번에 회색화. SpriteForCurrentMode() 본문을 `=> SpriteForMode(CurrentMode);` 로 치환해 중복 switch 제거. ArchitectMenu.cs:556-557 의 석재 회색 틴트를 Color.white 로 변경(이중 회색화 방지 — 268-271 주석의 원래 의도 복원). LoadIcon/IconKey/_iconCache/MaterialColor 는 Instance==null 폴백용으로 유지하되 주석에 '폴백 전용' 명시.

### [architect] 비용 이중 정의: 메뉴 라벨/affordability 의 하드코딩 비용이 BuildManager 의 SerializeField 비용과 드리프트 가능  `#4.5`
- **증거**: ArchitectMenu.cs:145-234 — (mode,label,cost) 튜플에 비용을 라벨 문자열("벽 (목재 5)")과 int 로 이중 하드코딩. 실제 차감/고스트 빨강 판정은 BuildManager.cs:70-158 의 SerializeField(wallCost=5 등) + private CostFor(284-302). 현재는 값이 일치하지만 Inspector 에서 비용 튜닝 시 메뉴 라벨·#241 자재부족 dim·툴팁(BuildableTooltip 309-314)이 전부 구값을 표시 — 비용 표시 UI 전체가 침묵 드리프트.
- **수정안**: BuildManager.CostFor(Mode) 를 public 으로 승격. ArchitectMenu.RefreshContent 에서 int liveCost = BuildManager.Instance != null ? BuildManager.Instance.CostFor(mode) : cost; 로 치환하고 라벨을 정적 문자열 대신 $"{명사} ({자재} {liveCost})" 합성(ThingKr/PaysWithStone 재사용). ArchitectClickAutoQA 는 라벨 substring("벽" 등) 매칭(ArchitectClickAutoQA.cs:201-212)이라 명사부 유지 시 안전 — 튜플엔 명사만 남기고 cost 필드 삭제.

### [architect] ESC 가 빌드 모드를 취소하지 않음 — 7개 designation 모드와 비대칭, 타 파일 주석은 존재하지 않는 바인딩을 가정  `#4.6`
- **증거**: BuildManager.cs:202 — 우클릭만 SetMode(Off); 파일 내 KeyCode.Escape 부재(grep 확인). 반면 TreeChop/Mine/Deconstruct/GrowZone/Stockpile/Roof/BlueprintDrag 7개는 전부 우클릭+ESC 취소(예: MineDesignation.cs:179). SettingsMenu.cs:240-242 주석은 'ESC=build-cancel binding (owned by BuildManager)' 를 보존한다고 적었으나 그 바인딩은 실제로 없음 — ESC 를 눌러도 빌드 모드·고스트가 유지된다(드래그만 BlueprintDragDesignation 이 취소).
- **수정안**: BuildManager.cs Update 의 202행 우클릭 취소 옆에 `if (BuildModeActive && Input.GetKeyDown(KeyCode.Escape)) { SetMode(Mode.Off); return; }` 추가. SettingsMenu 가 열려 있을 때의 ESC 는 SettingsMenu 가 isOpen 일 때만 소비하므로(243-244) 충돌 없음.

### [architect] 빌드 모드 중 정상적인 UI 버튼 클릭마다 '✗ UI 위 클릭' 실패 토스트 스팸 — #190 진단 도구의 잔재  `#4.7`
- **증거**: BuildManager.cs:227-236 — 빌드 모드 활성 중 IsPointerOverGameObject 면 무조건 ShowFail("✗ UI 위 클릭 - 맵에 직접 클릭하세요"). 속도 버튼·탭바·메뉴 재오픈 등 의도적 UI 조작도 전부 빨간 실패 토스트를 띄운다. 원래 목적은 #190 '보이지 않는 raycast 차단자' 진단이었고(파일 헤더 7-17), 차단 버그는 ArchitectMenu.cs:616-626 근본수정으로 해소된 상태라 지금은 거짓 경보만 남음.
- **수정안**: BuildManager.HandleLeftClickAt 의 overUI 분기에서 EventSystem.RaycastAll 결과를 검사해 부모 체인에 Selectable(버튼 등 상호작용 요소)이 있으면 토스트 생략(Debug.Log 만 유지), 순수 차단 그래픽에 막혔을 때만 ShowFail — 진단 가치는 보존하고 정상 조작 스팸 제거. 간단 대안: 토스트 자체를 제거하고 로그만 유지.

### [bottom-bar] 탭바 폭 계산 off-by-one(GroupGap 4개 vs 실제 5개) — ⚙설정 버튼이 패널 우측 테두리를 4px 침범, 좌우 비대칭  `#0.3`
- **증거**: GuiControlBar.cs:128 'float totalW = 6 * BtnW + 4 * GroupGap;' = 520. 그러나 BuildLayout 의 실제 배치 루프(178-195행)는 버튼 사이 갭을 5번 소비(징집|직업, 직업|일정, 일정|건축, 건축|연구, 연구|설정) → 실제 콘텐츠 폭 6*76+5*16=536. 버튼 행은 x[-260..+276], 패널(폭 544)은 x[-272..+272] → 설정 버튼 우측이 패널 경계 밖으로 4px 돌출(2px 테두리 덮음), 좌측 패딩 12px vs 우측 -4px 비대칭. 01_natural_play.png 에서 ⚙설정 이 우측 테두리에 들러붙은 모습과 일치.
- **수정안**: GuiControlBar.cs:128 을 'float totalW = 6 * BtnW + 5 * GroupGap;' 로 수정 (x = -totalW*0.5 시작점 로직은 그대로). 버튼 GO 이름/계층(Btn_* root 직속, IntegrationTestRunner.cs:312-326 depth-1 Find) 불변이라 테스트 영향 없음.

### [bottom-bar] [⚙설정] 버튼 힌트 "(ESC)" 가 거짓 — ESC 로 설정이 열리지 않음  `#0.4`
- **증거**: GuiControlBar.cs:195 MakeBtn("설정", "(ESC)", ...). 그러나 SettingsMenu.cs:240-244 는 'isOpen && Escape → Close()' 만 — ESC 로 여는 코드는 전 스크립트 grep 결과 없음(ToggleStatic 호출처는 이 버튼·메인메뉴·AutoScreenshotter 뿐). 실제 ESC 는 build/designation 취소(BuildManager, Mine/Decon/Grow 등 8곳) 와 패널 닫기에 소비됨. 나머지 탭 힌트(F1/F4/F8/N)는 전부 실제 바인딩과 일치 확인(WorkTabUI.cs:310, ScheduleUI.cs:202, ArchitectMenu.cs:743, ResearchUI.cs:47) — 설정만 거짓.
- **수정안**: 최소 수정: GuiControlBar.cs:195 힌트 문자열 "(ESC)" → "" (또는 "(메뉴)"). 선택 확장(원작 관례 정합): SettingsMenu.Update 에 '!isOpen && Escape && 어떤 build/designation 모드도 비활성 && 다른 패널 닫힘 → Open()' 가드 추가 — 단 ESC 소비 우선순위 조정이 필요하므로 라벨 수정이 안전한 바닥선.
- **RimWorld 근거**: rimworldwiki.com/wiki/Controls: "Esc — Closes any open dialogue or opens the Game Menu" — 원작은 '닫을 게 없으면 ESC=게임 메뉴'. 현행은 라벨만 있고 동작이 없음.

### [bottom-bar] ui-audit.md(자칭 SSOT)의 §3.2/§3.3 스펙이 현행 레이아웃과 불일치 — 후속 lane 이 제거된 UI 를 되살릴 위험  `#0.5`
- **증거**: docs/ui-audit.md §3.2 Band A 는 'Pause/1x/2x/4x | 징집...' 이 하단 중앙 한 바에 있다고 기술하지만 현행은 속도 클러스터가 우하단 분리(GuiControlBar.cs:145-172, RimWorld 관례 일치). §3.2 Band B/§3.3 은 해체/채광/경작 좌하단 strip(x0=16, y=24) 배치를 '계약'으로 명시하지만 세 토글은 2026-05-31 운영자 fb 로 제거되어 ArchitectMenu 로 이동(세 파일 모두 주석으로 명기). Band C 기즈모바(y=112, sort 150)도 #232 로 통째 비활성. 문서 머리말이 '모든 UI fix 서브태스크는 이 문서를 읽고 따라야 한다' 고 강제하므로, 문서대로 따르면 제거된 standalone 토글·기즈모바를 재생성하게 됨.
- **수정안**: docs/ui-audit.md 갱신(코드 변경 없음): §2 P1/P3/P4 에 'RESOLVED (날짜, 방식: P1=멈춤 복구, P3=ArchitectMenu 이동, P4=#232 비활성)' 추기, §3.2 Band A 를 '탭바 중앙 y=24 + 속도/시계 우하단(x=-16, y=24)' 로 재기술, §3.3 strip 스펙을 'ArchitectMenu Orders/Zone 소속(standalone 버튼 금지)' 로 교체, Band C 는 'disabled #232 — 재활성 시 y=112/sort150 준수' 주석으로 강등.
- **RimWorld 근거**: rimworldwiki.com/wiki/User_interface: "Time speed control — Bottom right corner", "Current time — Bottom right corner" — 현행 코드가 원작 관례와 일치하고 문서가 뒤처짐.

### [feedback] ContextMenuUI 가 무테두리 raw 패널 — 전 패널 공통 bordered-panel 계약 위반  `#7.5`
- **증거**: ContextMenuUI.cs:47-48 은 bg=UITheme.PanelBg 단색 Image 만 추가하고 UITheme.MakeBorderedPanel 미사용 — ui-audit.md §3 계약('All panels use UITheme.MakeBorderedPanel … No script invents its own colors/fonts/padding') 위반으로, 어두운 지형 위에서 메뉴 경계가 흐릿함. 라벨 색도 (0.95,0.92,0.85) 하드코딩(105행, UITheme.TextPrimary 미사용). hover/클릭/z-order 등 과거 감사 지적은 모두 해결 확인됨(86-96, 128-132, 149-159행).
- **수정안**: ContextMenuUI.Awake 에서 패널에 UITheme.MakeBorderedPanel(또는 최소 1-2px Divider 색 Outline) 적용, 라벨 색을 UITheme.TextPrimary 로 교체. 항목 컨테이너만 content 안으로 옮기면 Open/Close 로직은 무변경.

### [feedback] ResourceLowAlert 펄스가 1초 스로틀 안에 갇혀 1fps 로 깜빡임 — 감사 rank7 '또렷한 맥동' fix 무효화  `#7.6`
- **증거**: ResourceLowAlert.cs:72 'if (Time.unscaledTime - lastCheck < 1.0f) return;' 이 함수 전체를 게이트하는데, rank7 펄스 계산(86-89행 Mathf.Sin(t*3f))이 그 게이트 안에 있어 색이 초당 1번만 갱신됨 — 부드러운 맥동이 아니라 1초마다 임의 밝기로 점프하는 스텝. 또한 bg 색 (0.45,0.10,0.10) / (0.55,0.13,0.13) 하드코딩으로 UITheme(TextDanger 등) 미사용.
- **수정안**: ResourceLowAlert.Update 재구성: 자원 체크만 1s 스로틀로 남기고, gameObject.activeSelf 일 때 펄스 색 계산은 매 프레임 실행하도록 86-89행을 게이트 밖으로 이동. 색상은 UITheme 상수 기반으로 교체.

### [feedback] LoadKoreanFont 사본 4중 복제 — UITheme.LoadKoreanFont 존재에도 각자 복붙  `#7.7`
- **증거**: UITheme.cs:48 에 공용 LoadKoreanFont(size) 가 있고 AlertStackUI.cs:116 은 이를 사용하는데, ThreatAlertUI.cs:56-65, ResourceLowAlert.cs:57-66, BuildClickToast.cs:70-79, ContextMenuUI.cs:52-61 은 동일한 후보 배열('Malgun Gothic'…)을 각자 사설 메서드로 복붙 — 폰트 후보 변경 시 5곳 수정 필요한 죽은 중복.
- **수정안**: 4개 파일의 사설 LoadKoreanFont 삭제하고 MelonS.GameProto.Core.UITheme.LoadKoreanFont(16/18/15/32) 호출로 교체. 동작 변화 없는 순수 정리.

### [inspect] 같은 HP/기분 수치가 콜로니스트 바와 머리위 바에서 정반대 색 언어 — 풀피 림이 '꽉 찬 빨간 바'로 표시  `#3.2`
- **증거**: G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Scripts/PawnFloatingBars.cs L165-170 ColorForHp: 전 구간 빨강 계열(>70%도 bright red 0.95,0.30,0.25), 기분은 노랑(L172-177). ColonistBar.cs L50-53: 같은 HP를 초록(0.45,0.82,0.40), 기분을 파랑(0.46,0.70,0.95)으로 표시. 스크린샷 확증: 01_natural_play.png — 게임 시작 직후 무피해 림 둘 다 머리 위에 꽉 찬 빨간 바(+노란 바), 같은 화면 상단 콜로니스트 바 초상 아래엔 초록+파랑 미니바. 빨강=위험 관습상 건강한 림이 항상 '경고'로 읽히고, 화면 두 곳의 동일 수치 색이 충돌.
- **수정안**: PawnFloatingBars.cs ColorForHp를 ColonistBar 램프와 정렬: >70% 초록(ColonistBar.HpFillCol과 동일 0.45,0.82,0.40), 30-70% 주황(기존 0.95,0.40,0.20 유지), <30% 빨강(기존 0.55,0.10,0.10). 출혈 깜빡임(L133-138)은 그대로(빨강 펄스가 초록 베이스와 대비돼 오히려 더 잘 띔). 기분 바 노랑↔파랑 통일은 노랑 쪽이 잔디 위 가독성이 좋으므로 ColonistBar.MoodFillCol을 노랑으로 맞추는 방향 권장(ColonistBar 담당 영역과 협의 필요 — 본 영역 단독으로는 HP 램프만 수정).
- **RimWorld 근거**: rimworldwiki.com/wiki/User_interface: "Colonist bar — Shows the colonists' names, appearance, and status icons" — RimWorld는 상시 머리위 HP 바 없이 콜로니스트 바가 상태 표시를 담당. PawnSim은 머리위 바를 채택했으므로 최소한 두 표시의 색 의미는 일치해야 함.

### [inspect] 엔티티 인스펙터 본문이 dev 노트 톤 — 영어 용어/밸런스 수치/이모지 혼입, 폰 패널의 운영자용 한국어 톤과 불일치  `#3.3`
- **증거**: G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Scripts/EntityInspectorPanel.cs Describe(): L69-71 '🔨 건설중'/'⏳ 자재 부족, hauler 운반 중'/'✓ 자재 완비, pawn 대기', L105 '충돌 collider (pawn 통과 X)\n(wood 100 / stone 280 / steel 300)', L107 '목재 3, trigger collider', L116 'radius 1.5 안 pawn 시 연구 진행\n2 pt/sec/pawn', L118 'HP 18, dmg 4\ndetect 5u, chase speed 2.5'. 같은 패널의 폰 탭(PawnInfoPanel L549-639)은 완전 한국어 운영자 톤(상태/출혈/붕대/사망)이라 한 패널 안에서 두 톤이 충돌. 이모지 🔨⏳는 UITheme.LoadKoreanFont 경로의 레거시 Text에서 글리프 누락(빈 네모) 가능성 있음 — 런타임 미확인, 추정.
- **수정안**: EntityInspectorPanel.Describe()의 문자열만 재작성(로직 불변, I25 테스트는 reflection으로 메서드 존재만 검증하므로 안전 — 단 문자열 단언 여부 1회 확인): 'pawn 대기'→'콜로니스트 대기', 'hauler 운반 중'→'운반 대기 중', 'trigger collider' 등 엔진 용어 삭제, 'dmg 4/detect 5u'→'공격력 4 · 감지 5칸', '2 pt/sec/pawn'→'초당 2pt (1인당)', 괄호 밸런스 표는 유지하되 한국어 단위. 이모지는 ✓⚔처럼 이미 다른 UI(HoverTooltip L199)에서 쓰는 글리프만 남기고 🔨⏳ 제거.

### [inspect] 죽은 UI: V7 styled empty-state(빈 패널 힌트+▸글리프) ~60 LOC가 2026-05-31 '선택 없으면 패널 숨김' 변경 이후 정상 플레이에서 도달 불가  `#3.4`
- **증거**: G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Scripts/PawnInfoPanel.cs — L428-444 nothingSelected(pawn==null && inspect==null) 분기가 패널 전체를 숨기고 조기 return. inspect가 비-pawn이면 entity 분기(L378-417)가 항상 잡힘(Describe 폴백 L126 ("오브젝트", go.name)로 entTitle이 null일 수 없음). 따라서 emptyText '선택된 오브젝트 없음' + emptyGlyph '▸'(EnsureEmptyState L93-135, SetEmptyStateVisible L137-140, L471-472)가 보이는 경로는 'CurrentInspect가 폰인데 CurrentSelection이 null' 또는 'EntityInspectorPanel이 씬에 없음'이라는 엣지뿐. 게다가 L473-477 주석 'keep the bordered frame drawn in the empty state too … Frame is always on'은 nothingSelected 전체 숨김과 정면 모순(스테일). EnsureEmptyState는 매 프레임 호출돼 죽은 상태를 유지·연출.
- **수정안**: PawnInfoPanel.cs에서 EnsureEmptyState/emptyGlyph/SetEmptyStateVisible 및 L471-472 호출 제거, emptyText는 SerializeField 호환을 위해 ref만 남기고 항상 비활성(또는 엣지 경로용 한 줄 fallback으로 축소). L473-477 스테일 주석을 '선택 없으면 패널 전체 숨김(2026-05-31 운영자 fb #3)'으로 교체. ui-audit P5의 empty-copy 통일 지적은 해결로 기록하되 '해결 방식이 empty-state 자체 제거'임을 명시.
- **RimWorld 근거**: rimworldwiki.com/wiki/User_interface: inspect pane은 선택 시에만 나타남("appears whenever the player selects") — 현행 숨김 동작이 컨벤션에 맞고, 따라서 empty-state는 존재 이유가 없음.

### [inspect] 호버 툴팁이 모든 동물을 '사슴'으로 하드코딩 — 같은 엔티티가 hover와 클릭-인스펙트에서 다른 이름  `#3.5`
- **증거**: G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Scripts/HoverTooltip.cs L196-201: AnimalEntity hover 시 종 무관 '⚔ 사슴'/'사슴 (드래프트 후 우클릭=사냥…)' 고정 문자열. AnimalEntity.cs L27-30에 SpeciesKr(사슴/멧돼지/닭)가 이미 있고 EntityInspectorPanel.cs L121-123은 이를 사용 → 멧돼지를 hover하면 '사슴', 클릭하면 '멧돼지'로 표기가 갈림.
- **수정안**: HoverTooltip.DescribeHit L196-201을 SpeciesKr 사용으로 교체: draftedReady 시 $"⚔ {animal.SpeciesKr}  (우클릭=공격/사냥)", 아니면 $"{animal.SpeciesKr}  (드래프트 후 우클릭=사냥 / 우클릭=길들이기)". 길들여진 개체는 EntityInspectorPanel처럼 IsTamed 반영(예: '✓ 길들여짐' suffix)하면 hover/인스펙트 정보 구조도 일치.

### [inspect] 건강/기분/장비 탭에서 폰 이름이 어디에도 표시되지 않음 — 탭 전환 시 '누구를 보고 있는지' 컨텍스트 상실  `#3.6`
- **증거**: G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Scripts/PawnInfoPanel.cs L456: titleText.SetActive(any && tabStatus) — 타이틀(이름+활동+특성)이 상태 탭에서만 활성. 건강 탭 본문은 '상태:' 헤더(L556), 기분은 '기분:'(L583), 장비는 '장비:'(L608)로 시작할 뿐 폰 이름이 없음. 림 3명 운영 중 탭을 바꾸면 패널만으로는 대상 식별 불가(월드 선택 하이라이트로만 추정 가능).
- **수정안**: P0 타이틀 정규화(finding 1)와 함께 처리: titleText.SetActive(any)로 전 탭 상시 표시, MakeBodyText(L258)와 healthRect 정규화(L541)의 top offset을 −(pad+TabStripH+TabStripGap)→−(pad+TabStripH+TabStripGap+TitleBandH)로 한 줄 내려 타이틀 밴드 확보. 본문 가용 높이 감소(~22px)는 verticalOverflow=Overflow라 클리핑 없음.
- **RimWorld 근거**: rimworldwiki.com/wiki/User_interface: inspect pane은 선택 대상의 세부를 보여주는 단일 패널 — RimWorld에서 선택 라벨은 탭과 무관하게 패널에 유지됨(위키 스텁이라 탭별 명세는 없음, 갤러리 'Inspect pane with a pawn selected' 항목 기준).

### [left-resources] ResourceLowAlert 펄스 애니메이션이 1초 폴링 게이트 안에 있어 1Hz 로 끊겨 '맥동' 의도 불발  `#1.3`
- **증거**: ResourceLowAlert.cs:72 'if (Time.unscaledTime - lastCheck < 1.0f) return;' 이 메서드 최상단에서 조기 반환하므로 :87-89 의 sin 펄스(bg.color 갱신)도 초당 1회만 실행됨. sin 주기 2π/3≈2.09s 를 1Hz 로 샘플링하면 에일리어싱으로 두 값을 번갈아 점프하는 깜빡임이 되어, :86 주석(감사 rank7)이 명시한 '전체 밝기+알파 동반 펄스(또렷한 맥동)' 가 실제로는 연출되지 않는다.
- **수정안**: ResourceLowAlert.cs Update 를 분리: 조건 판정(msg 계산, SetActive)만 1s 게이트 안에 두고, 펄스 적용 블록(:87-89)은 게이트 밖으로 빼서 if (gameObject.activeSelf) 일 때 매 프레임 실행. 3줄 이동으로 끝남.
- **RimWorld 근거**: RimWorld 의 critical alert (저식량/아사 등) 는 부드럽게 점멸하는 연속 애니메이션 — 본 프로젝트가 rank7 에서 모사하려 한 연출

### [left-resources] 세로 목록에 가로 바 시절의 ResSep 세로 구분선이 잔존 — 죽은 UI 요소  `#1.4`
- **증거**: SceneSetup.Game.TopBar.cs:197-206 MakeResChip 이 칩마다 2x44px 'ResSep_<key>' Image 를 생성. 주석 자체가 'separates this chip from the one to its LEFT' — 가로 HorizontalLayoutGroup 시절 칩 간 경계선 설계인데 #41 세로 이전 후 칩의 왼쪽엔 이웃 칩이 없음. 실측: 두 스크린샷 크롭(01_natural_play.png, 99_1188s.png) 모두에서 패널 왼쪽 가장자리에 끊어진 밝은 세로 선 토막들로 렌더되어 의미 없는 틱 마크로 보임. grep 결과 ResSep 참조는 생성 코드와 씬 파일뿐, 테스트 미참조라 제거 안전.
- **수정안**: SceneSetup.Game.TopBar.cs MakeResChip 에서 divGo 블록(:197-206) 과 kDividerW 상수(:174) 삭제 후 씬 재생성. (가로 재사용 가능성을 남기려면 bool vertical 파라미터로 게이트해도 되지만 호출처가 세로 1곳뿐이라 그냥 삭제 권장.)
- **RimWorld 근거**: RimWorld ResourceReadout 세로 목록은 행간 구분선 없이 아이콘+수량 행만 쌓음

### [system-menus] 인게임 메뉴에 '메인 메뉴로 돌아가기 / 게임 종료' 행 부재 — RimWorld Menu 탭 표준 구성 미달  `#6.3`
- **증거**: 전 Scripts grep: 게임플레이 경로의 SceneManager.LoadScene 은 MainMenuController.cs:103(메뉴→게임 단방향)뿐, Application.Quit 도 MainMenuController.cs:119 외엔 전부 QA/테스트 하니스. 즉 인게임 진입 후 메인 메뉴 복귀·정상 종료 UI 가 없음(Alt+F4/창닫기만 가능). 설정 패널이 save/load/options 를 통합한 'Menu 탭' 역할인데 핵심 두 행이 빠짐.
- **수정안**: SettingsMenu.BuildPanel 의 저장행 아래(게임 씬에서만, saveLoadRow 와 동일하게 SyncSaveLoadAvailability 로 표시 제어)에 한 행 추가: MakeButton 재사용 "🏠 메인 메뉴" → `UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu")`, "종료" → `Application.Quit()` (에디터 가드는 MainMenuController.OnQuitClicked 패턴 복사). PanelH 를 행 높이만큼 가산. 신규 게임 시스템 아님 — 기존 메뉴 패널에 기존 API 2 호출 배선.
- **RimWorld 근거**: rimworldwiki.com/wiki/User_interface — "The last 'Menu' tab lets you review the current scenario, configure options, save the game in progress, load a saved game, return to the main screen, or exit the game."

### [system-menus] 설정 패널이 모달이 아님 — 패널 바깥 클릭이 게임 월드로 통과해 폰 명령/청사진 배치 발생  `#6.4`
- **증거**: SettingsMenu.Awake(100-115): 360×286 중앙 rect 만 생성, 풀스크린 백드롭 없음. 열려 있는 동안 패널 밖 클릭은 GraphicRaycaster 를 안 거치고 월드로 — ClickSelector/BuildManager 가 그대로 반응(코드 구조상 확실; 실클릭 재현은 미수행 — 추정 아님, 차단 레이어 부재는 코드 사실). RimWorld ESC 메뉴는 모달 오버레이로 뒤 화면 조작 차단.
- **수정안**: SettingsMenu.Awake 에서 캔버스 자식으로 "SettingsBackdrop" GO 생성: 풀스트레치 앵커, Image=UITheme.ShadowBg(0,0,0,0.45), raycastTarget=true, Button onClick→Close(바깥 클릭 닫기 = 표준 모달 UX). OpenInternal 에서 backdrop.SetActive(true)+backdrop.SetAsLastSibling() 직후 transform.SetAsLastSibling()(패널이 백드롭 위), Close 에서 backdrop.SetActive(false). MainMenu 씬에서도 동일 동작이라 추가 분기 불필요.

### [system-menus] 메인 메뉴 씬이 인게임 UITheme 와 다른 디자인 언어 — 파란 플랫 버튼+네이비 배경+영문 라벨  `#6.5`
- **증거**: SceneSetup.Menu.cs:24 카메라 bg 차가운 네이비(0.05,0.05,0.08), :111-118 플랫 파랑 버튼(0.2,0.5,0.8 계열), :53/:132 LegacyRuntime.ttf+흰색, 라벨 "Start Game"/"Options / 설정"/"Quit" 영문 혼용, 보더/패널 시스템 미사용. 인게임은 UITheme 웜브라운(#2A1F18)+골드+한글+MakeBorderedPanel 통일이고 UITheme.cs:10-13 이 "모든 panel/button 이 이 색을 사용해야" 명시. 메뉴에서 Options 클릭 시 웜브라운 설정 패널이 파란 메뉴 위에 떠 두 디자인 언어가 한 화면에 공존.
- **수정안**: SceneSetup.Menu.cs 리스타일 후 MainMenu.unity 리베이크: cam.backgroundColor → 다크 웜브라운(예 0.08,0.06,0.045), CreateMenuButton 을 UITheme.MakeBorderedPanel(2px Divider 보더)+BtnInactiveBg 필+LoadKoreanFont+TextPrimary 로 교체(SettingsMenu.MakeButton 과 동형), highlighted/pressed 도 UITheme.BtnHover 계열, 라벨 "게임 시작"/"설정"/"종료" 한글 통일, 타이틀도 LoadKoreanFont+AccentGold. GO 이름 StartButton/OptionsButton/QuitButton 과 persistent listener 베이크(87-93)는 유지.

### [system-menus] 불러오기 즉시 실행(확인 없음) — 클릭 1회로 진행 중 콜로니 전체 파괴·교체  `#6.6`
- **증거**: SettingsMenu.OnLoadClicked(292-300) → gsb.OnLoad() 직행. GameSaveButtons.OnLoad(69-103)는 확인 없이 현재 폰/나무/구조물/작물/스톡파일 전부 Destroy 후 세이브로 재구성. F9 도 동일. 모달 안에서 저장 버튼 바로 옆이라 오클릭 시 미저장 진행분 즉시 소실. (RimWorld 의 로드 확인 다이얼로그 존재 여부는 위키 stub 에서 미확인 — ref 비움.)
- **수정안**: 신규 다이얼로그 없이 2-클릭 암: SettingsMenu.OnLoadClicked 에 `float loadArmUntil` 추가 — 첫 클릭 시 loadBtn 텍스트를 "정말 불러오기? 재클릭" 으로 바꾸고 loadArmUntil=Time.unscaledTime+3f, 3초 내 재클릭에만 gsb.OnLoad() 실행 후 라벨 원복. Update 에서 시간 경과 시 라벨 원복.

### [system-menus] 설정 패널 행 배치가 하드코딩 매직 Y 체인 + saveLoadRow 폭이 콘텐츠보다 4px 오버행  `#6.7`
- **증거**: SettingsMenu.cs:200 sfxY=-54, :207 musicY=-116, :167 저장행 y=-176, :58-60 PanelH=286/210 수기 합산 — ui-audit.md §1 루트코즈가 지목한 "HARD-CODED pixel offset" 패턴 그대로(행 하나 추가 시 4곳 수동 재계산). 또 :166 saveLoadRow 폭 = PanelW-PadOuter*2 = 336 인데 panelContent 실폭은 PanelW-2*(BorderPx+PadOuter) = 332 (UITheme.MakeBorderedPanel 91-106: 보더 2 인셋 + 패드 12 인셋) — 행이 좌우 2px 씩 넓어 양끝 버튼이 패딩 영역 침범(보더 안쪽이라 시각적 깨짐은 경미).
- **수정안**: BuildPanel 을 running-cursor 방식으로: `float y=-40-14;` 에서 시작해 라벨/슬라이더/RowGap 을 누적하며 배치하고 최종 cursor 로 PanelH/PanelHNoSave 산출(상수 삭제). saveLoadRow 는 anchorMin(0,1)/anchorMax(1,1)/sizeDelta(0,56) 스트레치로 바꿔 폭 하드코딩 제거(부모 폭 자동 추종).

### [tabs] 직업 탭 열 순서가 RimWorld '왼쪽=중요' 관례를 무시 — 의료가 맨 오른쪽, 연구가 중간  `#5.4`
- **증거**: WorkTabUI 열 순서 = enum 순서(PawnWorkSettings.cs:17,26: 벌목,채집,사냥,요리,연구,건축,채광,운반,의료 — 저장 호환 위해 enum 은 append-only). RimWorld 바닐라는 Doctor 가 좌측 3번째(최중요), Haul/Clean/Research 가 맨 우측. 현행은 의료(Doctor)가 9번째(맨끝), 연구가 5번째(중간)라 정보 구조가 반대 신호를 줌. WorkTabUI.cs:9 헤더 주석도 '열 = 벌목/채집/사냥/요리/연구(5종)' 로 9종 확장 이전 상태의 stale comment.
- **수정안**: WorkTabUI.cs 에 표시 전용 재배열 배열 추가(enum/저장 불변): 'private static readonly int[] ColOrder = {8,3,2,5,1,6,0,7,4}; // 의료,요리,사냥,건축,채집,채광,벌목,운반,연구' — RefreshGrid 의 헤더 루프(127-132)와 셀 루프(151-167)에서 AllKinds[ColOrder[c]] / KoreanNames[ColOrder[c]] 로 인덱싱. 헤더 주석(9줄)도 9종으로 갱신.
- **RimWorld 근거**: https://rimworldwiki.com/wiki/Work — "Tasks on the left rank more important than tasks on the right" / "For tasks with the same priority, tasks on the left will be performed first".

### [tabs] 직업 탭 셀에 스킬 신호가 전혀 없음 — 누구를 요리사로 둘지 판단 근거가 화면에 없음  `#5.5`
- **증거**: WorkTabUI.MakePriorityCell(251-297)은 우선순위 숫자+색만 그림. PawnSkills(채집/벌목/건축/전투 4종, SkillUI.cs:27-30 에서 사용)가 이미 존재하는데 Work 탭과 미연결 — 게다가 그 4종을 보여줄 유일한 UI(SkillUI)는 P0-1 로 죽어 있어, 현재 빌드에서 운영자는 스킬 레벨을 볼 방법이 0개다. RimWorld 는 셀 외곽선 밝기(빨강=최악→흰→밝은노랑=최고)와 호버 툴팁으로 스킬을 Work 탭 안에서 보여준다.
- **수정안**: WorkTabUI.MakePriorityCell 에 스킬 외곽선 추가: 셀 root Image 를 외곽선 색(스킬 0..10 을 빨강→흰→노랑 Color.Lerp)으로 칠하고 2px 인셋 fill 자식에 기존 priority 색을 칠함(UITheme.MakeBorderedPanel 과 동일 이디엄, 신규 시스템 아님 — 기존 PawnSkills 데이터 재사용). 매핑 {Chop→SkillKind.Chop, Gather→Gather, Build→Build, Hunt→Combat}, 매핑 없는 열(요리/연구/채광/운반/의료)은 외곽선 생략. MakePriorityCell 시그니처에 PawnSkills 파라미터 1개 추가.
- **RimWorld 근거**: https://rimworldwiki.com/wiki/Work — "each box has a visible outline. The higher the colonist's skill, the brighter the outline, ranging from red (worst), to white, to bright yellow (best). Also, hovering over a skill box displays the colonist's skill level."

### [tabs] 일정 탭은 고정 높이 360px — WorkTabUI 에 랜딩한 동적 높이 fix(#obj-audit #0)가 미이식, pawn 11+ 시 행 잘림  `#5.6`
- **증거**: ScheduleUI.cs:44-45 h=360 고정, RefreshGrid(100-137)에 크기 재계산 없음. grid inset -60(96행) → 가용 300px, 행 26px: 헤더+10행=286 OK, 11행째부터 패널 밖으로 overflow. 동일 결함이 WorkTabUI 에서는 이미 수정됨(WorkTabUI.cs:187-193 — 행수×RowHeight 로 needed 계산 + Screen.height clamp). 또한 WorkTabUI 의 '(pawn 없음)' 빈 상태(172-185)도 ScheduleUI 에는 없어 pawn 전멸 시 헤더만 남은 빈 그리드가 뜸. 현재 콜로니 3-4명이라 잠복 상태.
- **수정안**: WorkTabUI.cs:187-193 블록을 ScheduleUI.RefreshGrid 끝에 이식: 'int rows = pawns.Length + 1; float needed = rows*RowHeight + 60f + 16f; rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Clamp(needed, 160f, Screen.height - 80f));' + '(pawn 없음)' 빈 상태 블록도 동일 이식.

### [tabs] 1240d43 warm 톤이 씬-베이크 요소(연구 스트립/picker, 스킬 패널)에 미적용 — picker 는 차가운 남색  `#5.7`
- **증거**: SceneSetup.cs:305 colPanel=(0.10,0.094,0.078,0.85) — UITheme.PanelBg(#2A1F18, a0.94)보다 어둡고 투명한 구세대 색이 ResearchStrip(SceneSetup.Game.Research.cs:18)과 SkillPanel(SceneSetup.Game.SkillPanel.cs:21)에 그대로 사용. picker 배경은 (0.08,0.10,0.13,0.95) 청회색(cold)으로 warm 팔레트 정면 위반(SceneSetup.Game.Research.cs:76), 진행바 fill (0.45,0.85,0.50) 임의 녹색(:55), 세 요소 모두 UITheme.MakeBorderedPanel 미사용(테두리 없음). 런타임 패널들(WorkTab/Schedule/GuiControlBar)은 warm 적용 완료라 베이크/런타임 간 톤 단절.
- **수정안**: SceneSetup.Game.Research.cs: resStripBg.color / pickerBg.color → MelonS.GameProto.Core.UITheme.PanelBg, resProg.color → UITheme.AccentOrange, picker 제목줄은 AccentGold. SceneSetup.Game.SkillPanel.cs: skillBg.color → UITheme.PanelBg (Editor 스크립트에서 런타임 UITheme 참조 가능 — SceneSetup.Game.PawnInfo 가 이미 동일 패턴). 가능하면 세 패널 모두 UITheme.MakeBorderedPanel 로 감싸기. 이후 씬 재생성.

### [tabs] WorkTabUI/ScheduleUI 가 공용 패널 시스템을 우회 — 테두리 없는 맨 Image + LoadKoreanFont 사본 2벌  `#5.8`
- **증거**: WorkTabUI.cs:56-57, ScheduleUI.cs:47-48 — bg = AddComponent<Image>(PanelBg) 만 사용, ui-audit §3 계약('All panels use UITheme.MakeBorderedPanel')의 2px Divider 테두리 없음 → GuiControlBar/PawnInfoPanel 의 bordered 스타일과 불일치. LoadKoreanFont 가 양 파일에 사적으로 중복(WorkTabUI.cs:63-72, ScheduleUI.cs:53-62)되어 있고 후보 목록(5종)이 UITheme.KoreanFontCandidates(7종, UITheme.cs:43-46)보다 짧은 drift 상태 — UITheme.LoadKoreanFont(48-56)가 이미 공용으로 존재. 빈 상태 텍스트 색도 하드코딩 (0.7,0.7,0.7)(WorkTabUI.cs:180)으로 UITheme.TextSecondary 미사용.
- **수정안**: 두 파일 Awake 에서 'var content = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(rt);' 호출 후 BuildShell 의 Title/Hint(Legend)/Grid 를 content 하위로 parenting; 사적 LoadKoreanFont 2벌 삭제 → 'font = UITheme.LoadKoreanFont(18);'; WorkTabUI.cs:180 색을 UITheme.TextSecondary 로 교체.

### [tabs] 일정 범례가 SlotColors 를 하드코딩 hex 로 중복(이미 1색 drift) + '자유' 만 색 견본 없음  `#5.9`
- **증거**: ScheduleUI.cs:82 — richtext hex #4d66d9/#4cc070/#f29940 하드코딩. 정본 PawnSchedule.SlotColors(PawnSchedule.cs:19-24)의 Work (0.30,0.75,0.45) = #4DC073 인데 범례는 #4cc070 으로 이미 미세 desync(향후 색 변경 시 통째로 어긋남). SlotLabels(:18) 문자열도 중복 표기. 첫 항목 '자유'(Anytime grey #8C8C99)는 색 태그 자체가 없어 어느 색 셀이 자유인지 범례에서 알 수 없음 — 그리드의 회색 셀과 매칭 불가.
- **수정안**: ScheduleUI.BuildShell 범례를 루프 생성으로 교체: for (int i=0;i<4;i++) sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(PawnSchedule.SlotColors[i])}>{PawnSchedule.SlotLabels[i]}</color>") + 구분자 ' | ' — 자유 포함 4종 전부 정본 색·라벨에서 파생되어 desync 원천 차단.
- **RimWorld 근거**: https://rimworldwiki.com/wiki/Schedule (Menus §Schedule) — Anything/Work/Recreation/Sleep 4종 모두 고유 색으로 구분 표시되는 것이 관례.

### [top-bar] 속도 상태 이중 표시 — 우상단 TimeUI 텍스트가 우하단 속도 버튼 하이라이트와 중복, RimWorld 는 우하단 단일  `#2.3`
- **증거**: TimeUI.cs 가 TopBar 우측 슬롯(SceneSetup.Game.TopBar.cs:68-83)에 "▶ 1x"/"▶▶▶ 3x" 를 그리고, GuiControlBar.cs:145-171+333-335 가 우하단 멈춤/1x/2x/4x 클러스터에 활성 버튼 하이라이트를 이미 표시. 두 스크린샷 모두 이중 표시 확인(01: 우상단 "▶ 1x"+우하단 gold "1x" 버튼, 50_600s: 우상단 "▶▶▶ 3x"). 같은 상태를 화면 대각 양끝에 두 번 — ClockUI 헤더 주석 스스로 "둘이면 테스트 빌드처럼 보임" 이라 한 바로 그 패턴. 단 주의: 50_600s 의 3x 는 버튼(1/2/4x)으로 표현 불가한 스케일이라 TimeUI 가 유일한 정확 readout — 단순 삭제 금지.
- **수정안**: TimeUI readout 을 우하단 시계 클러스터로 합류: SceneSetup.Game.TopBar.cs 에서 TimeUI GO 생성(68-83행) 제거, ClockUI.BuildBottomRightCluster 에 `var speedTxt = MakeLine(content, "ClockSpeed", font, 20, FontStyle.Bold, UITheme.TextPrimary); speedTxt.gameObject.AddComponent<TimeUI>();` 추가, panelH 52→80. TimeUI 는 [RequireComponent(typeof(Text))] 라 그대로 동작. TopBar 우측 빈 슬롯은 비워둠(추후 알림 영역과 간섭 없음).
- **RimWorld 근거**: 위키: 'Time speed control — Bottom right corner', 'Current alerts and suggestions — Top right corner' — 속도 상태는 우하단 한 곳, 우상단은 알림 전용 코너

### [top-bar] 날짜(좌상단)·시각(우하단) 분리 배치 — RimWorld 는 날짜+시각이 우하단 속도 컨트롤 옆 한 덩어리  `#2.4`
- **증거**: ClockUI.cs:38-39+98-99+149 — 의도적 분리: topBarDate(좌상단 gold)가 달력, 우하단 ClockCluster 는 "3:30 PM" 단일 라인 전담. '지금 언제인가' 를 읽으려면 화면 대각 양끝을 오가야 함. 분리는 운영자 피드백("탑바 좌측 빈 슬롯 채움" 주석, 51행)에 따른 것이므로 위반이 아니라 판단 사안 — 단 RimWorld 컨벤션과 어긋나고, F(연도 클리핑) 결함이 보여주듯 좌상단 슬롯은 날짜 전체를 담기에 폭도 부족.
- **수정안**: 우하단 클러스터를 자급화: ClockUI.BuildBottomRightCluster 에 날짜 라인 추가 `dateText = MakeLine(content, "ClockDate", font, 18, FontStyle.Normal, UITheme.TextPrimary);` + Update 에서 시각과 함께 갱신, panelH 80→104(속도 라인 합류 시). 좌상단 topBarDate 는 운영자 의향 확인 후 (a) 보조 readout 으로 유지(중복 1개 허용) 또는 (b) 비우기 중 택일 — RimWorld 정합 기준 권장은 (b).
- **RimWorld 근거**: 위키: 'Current time — Bottom right corner — Days and hours passed since you have landed.' — 날짜+시각이 우하단 단일 readout, 속도 컨트롤과 같은 코너

### [top-bar] 우하단 시계 박스가 속도 패널 위 24px 에 따로 부유 — 폭·간격 불일치 + 형제 지오메트리 하드코딩 3곳  `#2.5`
- **증거**: ClockUI.cs:72-84 — panelW=170, marginBottom=120, 주석 "#275 속도패널(높이 72, y24~96) 위 안전 간격" 으로 GuiControlBar 지오메트리(GuiControlBar.cs:104-157: SpRight=16, SpBottom=24, 높이 56+2*8=72, 폭 4*76+3*4+2*PadX≈348)를 손으로 복사. 결과(두 스크린샷 확인): 170px 시계 박스가 ~348px 속도 패널 위에 24px 떠서 우변만 맞고 폭·간격은 무관 — 한 클러스터가 아니라 떠도는 별개 박스 2개로 읽힘. 같은 안티패턴이 ColonistBar.cs:37 `TopBarH = 76f` 주석 "(변경 시 같이 갱신)" 에도 — ui-audit 이 루트코즈로 지목한 '형제 크기를 측정 않는 매직 오프셋' 이 fix 후에도 3개 파일에서 재발.
- **수정안**: (1) 시계 패널을 속도 패널에 도킹: ClockUI 의 panelW 를 속도 패널과 동일 폭으로, marginBottom 을 '속도 패널 top + 4' 로. (2) 근본 정리: GuiControlBar 의 SpRight/SpBottom/패널 높이와 TopBar 높이 76 을 UITheme 공유 상수(예: UITheme.TopBarH, UITheme.SpeedPanelRect)로 승격하고 ClockUI.cs:74, ColonistBar.cs:37 이 이를 참조 — 세 파일의 사본 주석("변경 시 같이 갱신") 제거.
- **RimWorld 근거**: 위키: 'Time speed control — Bottom right corner' + 'Current time — Bottom right corner' — 같은 코너의 한 클러스터


## P3

### [architect] 메뉴 타이틀의 비-BMP 이모지 '🏛' 는 레거시 uGUI Text 에서 tofu(□) 가능성 — 추정  `#4.8`
- **증거**: ArchitectMenu.cs:440 t.text = "🏛 건축 (F8)". U+1F3DB 는 SMP 영역으로 Malgun Gothic 등 OS 폰트에 글리프가 없어 레거시 Text 에선 빈칸/□ 로 그려질 공산이 큼. 스크린샷에서 BMP 기호 ⏸(멈춤)·⚙(설정)은 정상 렌더 확인됨; 메뉴 열린 스크린샷이 없어 🏛 실렌더는 미확인 — 추정.
- **수정안**: 타이틀을 "건축 (F8)" 로 단순화하거나 BMP 기호 "⚒ 건축 (F8)"(U+2692, ⏸/⚙ 와 동일 폰트 커버리지 계열)로 교체. -open-architect 캡처 1장으로 검증 후 확정.

### [architect] Orders/Zone 카테고리는 Dictionary 삽입 순서에 의존 — #63 이 build 카테고리에 도입한 명시 정렬 패턴과 불일치  `#4.9`
- **증거**: ArchitectMenu.cs:478 `foreach (var kv in OrderCategories)` — Dictionary<string,…> 순회 순서는 언어 스펙상 비보장(현 구현에선 우연히 삽입 순서). 반면 build 카테고리는 #63(238-246)에서 'dict 선언 순서와 표시 순서 분리' 를 명시적으로 채택한 CategoryOrder 배열로 순회(518).
- **수정안**: `private static readonly string[] OrderCategoryOrder = { "Orders (지시)", "Zone (구역)" };` 추가 후 478행 foreach 를 CategoryOrder 패턴과 동일하게 OrderCategoryOrder + TryGetValue 순회로 교체 — 한 파일 내 두 가지 순서 결정 방식 통일.
- **RimWorld 근거**: Architect 페이지 탭 순서: Orders → Zone → Structure → Production → … (Orders/Zone 선행 자체는 현행이 컨벤션과 일치)

### [architect] 패널 높이 clamp 가 Screen.height(디바이스 px)를 캔버스 단위 sizeDelta 와 직접 비교 — ScaleWithScreenSize 에서 단위 불일치  `#4.10`
- **증거**: ArchitectMenu.cs:592-594 — `float maxH = Screen.height > 0 ? Screen.height - 80f : 900f; rt.sizeDelta = …Clamp(desired, 200f, maxH)`. ui-audit §3 의 캔버스 규약은 1920x1080 reference + match 0.5 의 ScaleWithScreenSize 라 sizeDelta 는 캔버스 단위, Screen.height 는 실제 px — 1080p 외 해상도(720p 창모드, 4K)에서 clamp 기준이 scaleFactor 배만큼 틀어짐(현 콘텐츠 최대 568 단위라 실해는 잠재적).
- **수정안**: `float maxH = ((RectTransform)transform.parent).rect.height - 80f;`(캔버스 단위) 로 교체 — 같은 좌표계끼리 비교. parent 가 Canvas 루트가 아닐 가능성 대비 GetComponentInParent<Canvas>().GetComponent<RectTransform>().rect.height 사용도 가.

### [architect] BuildClickToast 톤 불일치 + 사문화된 문서: 자체 폰트 로더 중복, 무보더 평면 패널, 헤더가 약속한 메시지 2종은 실제로 출력 안 됨  `#4.11`
- **증거**: BuildClickToast.cs:70-79 — UITheme.LoadKoreanFont 가 있는데(ArchitectMenu.cs:373 사용) 동일 후보 배열을 사본으로 보유(U9 'per-script font fallback drift 금지' 위반 잔존). 48-50 — bg 가 PanelBg 단색 Image 로 MakeBorderedPanel 보더 규약(ui-audit §3 'All panels use MakeBorderedPanel') 미적용. 13-17 헤더 주석의 "✗ 쿨다운 중 (0.07s)"·"✗ 카메라 null" 은 BuildManager.cs:221-226(쿨다운: 로그만)·239(카메라: 로그만)에서 토스트를 쏘지 않아 사문서.
- **수정안**: ① label.font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(15) 로 교체하고 LoadKoreanFont 사본 삭제. ② Awake 에서 bg 단색 대신 UITheme.MakeBorderedPanel(rt, BorderPx, PanelBg) 적용해 다른 패널과 동일 보더 톤. ③ 헤더 주석에서 미출력 메시지 2줄 삭제(또는 쿨다운에 토스트 추가하지 말고 문서만 정리 — 쿨다운 무음은 #182 의도).

### [architect] 드래그 지정 폴리시 2건: 400셀 캡 도달이 플레이어에게 무음 + 외곽선이 항상 '유효' 파란색(ghostValidColor 라는 이름만 남은 미구현 invalid 상태)  `#4.12`
- **증거**: BlueprintDragDesignation.cs:243-247 — maxCellsPerDrag(400) 초과 시 Debug.LogWarning 후 조용히 부분 배치(화면 피드백 없음 → '드래그한 만큼 안 깔렸다' 혼란). 78행 필드명 ghostValidColor 는 invalid 색 존재를 전제한 명명이나 303행에서 무조건 단일색 적용 — 물/점유 영역을 가로질러도 외곽선은 항상 파랑(단일 셀 고스트만 빨강 전환, BuildManager.cs:396-400).
- **수정안**: ① 캡 분기에서 BuildClickToast.Instance?.ShowFail($"✗ 드래그 {maxCellsPerDrag}칸 초과 - 일부만 배치됨") 1회 호출. ② 저비용 개선: UpdateGhost 에서 커서 현재 셀의 ValidatePlacement 결과만 샘플링해 외곽선 색을 ghostValidColor / 빨강(1,0.4,0.4)으로 토글(전 셀 검증은 비용상 비채택) — 필드명 의도 복원. 신규 시스템 아님: 기존 외곽선 색상만 동적화.

### [bottom-bar] 죽은 코드·낡은 주석 정리: 비활성 SelectionGizmoBar 432줄, GuiControlBar 헤더의 구버전 버튼 라인업, 미사용 using  `#0.6`
- **증거**: (a) SelectionGizmoBar.cs — Bootstrap 첫 줄 return(103-106행)으로 파일 전체(빌드캔버스/버튼/폴링 ~330줄)가 도달 불가, #pragma warning disable CS0162 로 경고만 묻음. 운영자 fb #232 '되돌리기 쉽게 보존' 의도는 주석에 있으나 보존 기한/조건 없음. (b) GuiControlBar.cs:11-14 헤더 주석이 제거된 지 오래인 구 라인업 '[⏸멈춤][1x][2x][4x][징집(R)][벽(B)][바닥(F)][문(G)][화덕(T)][연구(N)]' + '각 버튼 60x56'(실제 76x56)을 기술, 322행 주석 '5개 비교'도 build 버튼 5개 시절 잔재. (c) MineDesignation.cs/GrowZoneDesignation.cs/DeconstructDesignation.cs 는 토글 UI 제거 후에도 'using UnityEngine.UI;' 유지(세 파일 모두 4행), MineDesignation.DispatchToIdleMiners(340-377행)는 호출부가 주석 처리된 retired 코드.
- **수정안**: (a) SelectionGizmoBar.cs Bootstrap 의 return 위에 '#232 비활성 (2026-06-XX 기준 유지; 재활성 조건: 운영자 요청)' 1줄 명시 또는 'const bool DISABLED_BY_232 = true; if (DISABLED_BY_232) return;' 패턴으로 CS0162 pragma 제거. (b) GuiControlBar.cs 헤더를 현행 라인업([징집|직업|일정|건축|연구|설정] 중앙 + [멈춤|1x|2x|4x] 우하단, 76x56)으로 갱신, 322행 주석 수정. (c) 세 designation 파일의 미사용 using UnityEngine.UI 제거, retired DispatchToIdleMiners 삭제(이미 단일 경로 MineStoneAction 으로 대체 명시됨).

### [bottom-bar] 탭 핫키가 RimWorld 표준 배치와 다름(일정 F4, 건축 F8, 연구 N) — 의도 결정 기록 필요  `#0.7`
- **증거**: 현행 바인딩: 직업 F1(WorkTabUI.cs:310, 원작 Work F1 과 일치), 일정 F4(ScheduleUI.cs:202), 건축 F8(ArchitectMenu.cs:743), 연구 N(ResearchUI.cs:47). 원작은 Schedule F2 / Architect Tab / Research F6 이며 F4=Assign, F8=World 탭. 버튼 힌트는 현행 바인딩과 정확히 일치하므로 운영상 즉시 문제는 없음(스크린샷 라벨 확인). 멈춤 Space, 속도 1/2/3 은 원작과 일치.
- **수정안**: 기능동결 하 재바인딩은 권하지 않음(테스트·HotkeyCheatSheet·문서 동시 수정 비용). docs/ui-audit.md 에 '핫키 매핑 의도 차이' 표 1개 추가해 (PawnSim키 ↔ RimWorld키) 를 명시, 향후 키 추가 시 충돌 참조표로 사용. 코드 무변경.
- **RimWorld 근거**: rimworldwiki.com/wiki/Controls: "SPACE — Pauses and unpauses game / Tab — Toggle Architect tab / F1 — Toggle Work tab / F2 — Toggle Schedule tab / F6 — Toggle Research tab / 1 — Normal speed (1x), 2 — Fast speed (3x), 3 — Super fast speed (6x)"

### [feedback] 낡은 위치 주석/문서 — 'bottom-center EventLog', '60px top bar' 가 현행과 불일치해 다음 wave 오배치 유발  `#7.8`
- **증거**: EventLogUI.cs:8 'Bottom-center event log' / AlertStackUI.cs:15-16 'bottom-center scrolling EventLog' — 실제 EventLog 는 우상단(SceneSetup.Game.EventLog.cs:21-27). AlertStackUI.cs:142 주석 'drop below the 60px top resource bar' — 실제 topBarHeight 76(같은 파일 65-68행 #275 가 직접 교정한 값). 이런 낡은 가정이 이미 한 번 겹침 사고를 냈음(EventLog.cs:25 의 60px 가정 → TopBar 4px 겹침).
- **수정안**: 주석 3곳 현행화(우상단/76px) + ui-audit.md 에 §3.5 확장으로 우상단 컬럼 밴드 테이블(P0 finding 의 좌표 계약)을 기록해 단일 진실원 유지.

### [inspect] 기분 탭에서 moodBar 행(하단 고정)과 moodDetailText(본문 전체 채움)가 같은 영역 공유 — 생각 목록이 길면 겹침  `#3.7`
- **증거**: G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Scripts/PawnInfoPanel.cs L466: moodBar 행은 기분 탭에서도 활성(에디터 위치 bottom y=25, 행 높이 28 → 패널 top 기준 195..223). moodDetailText는 MakeBodyText 렉트(top 40..bottom 12)를 전부 채우는 UpperLeft 텍스트(L258-262) → fontSize 13 기준 약 10줄째부터 텍스트가 moodBar 행 영역(top 195)에 진입. PawnThoughts.active가 9개 이상이 되는지는 미확인(추정) — 기하 계산상 잠재 겹침이며 스크린샷엔 해당 상황 없음.
- **수정안**: PawnInfoPanel.cs — moodDetailText 렉트만 기분 탭 전용으로 분리: MakeBodyText로 만든 뒤 offsetMin.y를 pad→(pad+행높이 28+RowGap 6)=46으로 올려 moodBar 행 위에서 끝나게 1회 조정(MoodDetailBody 생성 직후 한 줄). 또는 verticalOverflow=Truncate로 바꿔 바 침범만 차단.

### [inspect] PawnFloatingBars 죽은 코드 — no-op 메서드, 무의미 문장, Update가 즉시 덮어쓰는 중복 초기화  `#3.8`
- **증거**: G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Scripts/PawnFloatingBars.cs L115-119: ReanchorFillPivot이 주석뿐인 no-op인데 L93-94에서 두 번 호출. L86 'hpFill.GetComponent<SpriteRenderer>(); // ensure' 결과 미사용 문장. L84-85/L89-90의 fill localPosition/localScale 초기화는 UpdateFill(L152-163)이 매 프레임 재계산해 사실상 중복. L87-88 주석('custom 1-unit-wide sprite + adjusted pivot')도 실제 구현(센터 피벗+위치 보정)과 어긋난 스테일.
- **수정안**: ReanchorFillPivot 메서드와 두 호출, L86 문장, L87-88 스테일 주석 삭제; BuildBars의 fill 초기화는 UpdateFill(hpFill, 1f, …) 1회 호출로 대체. 동작 변화 0인 순수 정리.

### [inspect] PawnInfoPanel 잔여 매직넘버·중복·로그 스팸 — 매 프레임 rect 강제, 에디터 빌더 값 사문화, 본문 폰트 크기 3종 혼재  `#3.9`
- **증거**: G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Scripts/PawnInfoPanel.cs — (1) L315-324 EnsurePlacement가 가드 없이 매 프레임 anchor/size/pos를 380x248/(12,58)로 강제, SceneSetup.Game.PawnInfo.cs L26-27의 380x200/(12,64)는 사문화된 중복 정의. (2) L343 엔티티 본문 top 여백 '34f'는 에디터 타이틀 높이 추정 매직넘버(실제 타이틀 밴드는 −8..−34). (3) 본문 폰트 크기 혼재: MakeBodyText 13(L261) vs entityBodyText 14(L346) vs healthText 13(에디터, SceneSetup.Game.PawnInfo.cs L71 — 우연히 일치할 뿐 정규화 블록 L532-547은 폰트 크기를 안 만짐). (4) L365 selector null 시 매 프레임 Debug.LogWarning. (5) 본문 인라인 hex 색 12종(#ddc28a, #9adb86, #e8b454, #e85454, #ff6464, #a0c8ff, #c0392b, #e8b560, #c0b090, #dddddd, #888888, #e88c54)이 UITheme 밖에 산재.
- **수정안**: (1) bool placed 가드로 EnsurePlacement 1회화 + SceneSetup 빌더 값을 런타임 값과 일치시키고 '런타임 EnsurePlacement가 소유' 주석 명시. (2) 34f → P0 fix의 TitleBandH 상수로 통일. (3) entityBodyText 14→13. (4) LogWarning을 1회 플래그로. (5) 자주 쓰는 4색(섹션 헤더 #ddc28a, 양호 #9adb86, 주의 #e8b454/#e88c54, 위험 #e85454)만 UITheme 상수+RichHex 헬퍼로 승격, 나머지는 그 4종으로 수렴.

### [inspect] HoverTooltip — hover 프레임마다 FindFirstObjectByType<ClickSelector> 호출 + 레거시 죽은 필드  `#3.10`
- **증거**: G:/ai/MelonS-Agents/skills/game-prototype/unity-project/Assets/Scripts/HoverTooltip.cs L171: DescribeHit가 collider hover 중 매 프레임 Object.FindFirstObjectByType<ClickSelector>() (씬 전수 스캔). L21/L86: bg 필드는 'kept for legacy ref' 주석 그대로 이후 사용처 없음. 그 외 동작은 양호: UI 위 숨김(L120-121), 가장자리 clamp(L150-157), ArchitectMenu override 경로(L34-60), 자동문>문 상속 순서 가드(L224-229) 모두 정상 확인.
- **수정안**: ClickSelector를 private 필드로 lazy-cache(파일 내 ArchitectMenu override가 이미 쓰는 'caller polls Instance' 패턴과 동일하게 null일 때만 재탐색), bg 필드와 legacy 주석 삭제.
- **RimWorld 근거**: rimworldwiki.com/wiki/User_interface: "Cell info — Bottom left corner — whatever is at the cursor's current location is listed" — RimWorld는 커서 아래 대상 정보를 제공하는 컨벤션이 있고 PawnSim의 hover 툴팁이 그 역할을 수행 중(구조 자체는 적합).

### [left-resources] 변화 flash 가 증가/감소를 구분하지 않고, 식사 칩에선 기본색과 flash 색이 비슷해 안 보임  `#1.5`
- **증거**: ResourceCounterUI.cs:39-62 각 블록이 '값이 다르면' 무조건 flash 예약 — #130 주석의 의도는 '값이 안 늘어 느낌 해소'(증가 피드백)인데 소비로 줄 때도 동일한 노란 flash 가 떠 운영자가 증감을 오독할 수 있음. 또 flash 색 (1, 0.95, 0.35)(:64) 과 식사 칩 기본색 amber(0.98, 0.66, 0.24)(TopBar.cs:140) 가 유사 계열이라 식사 행의 flash 는 거의 식별 불가 (스크린샷으론 flash 순간 미포착 — 색 거리 비교에 근거한 판단이며 실기 flash 순간 캡처는 못 함, 이 부분은 추정).
- **수정안**: ResourceCounterUI.cs 각 블록의 flash 예약 조건을 증가 시로 한정: if (rm.wood > lastWood && lastWood >= 0) woodFlashUntil = ... (4곳 동일). flash 색은 어느 기본색과도 거리가 먼 흰색 계열 (1f, 1f, 1f) 또는 밝은 시안 톤으로 교체해 4칩 모두에서 식별되게.

### [left-resources] 주석·네이밍 드리프트 3건 — 코드가 말하는 위치/조건이 현행과 불일치  `#1.6`
- **증거**: (1) ResourceCounterUI.cs:6 'Top-right resource counter UI' — #41 이후 좌상단. (2) SceneSetup.Game.TopBar.cs:85-106 ui-audit §3.1 'RIGHT resource chips... HorizontalLayoutGroup' 서사 블록이 세로 이전 코드 바로 위에 그대로 남아 다음 작업자가 가로 설계로 오독할 수 있고, GameObject 명 'ResourceRow'(:111) 도 이제 row 가 아닌 column. (3) ResourceLowAlert.cs:8 'wood < 5, food + meals < 5, stone < 0 (0 이면 OK) 등 조건마다 텍스트' — 코드(:78-80)엔 stone 조건이 아예 없고 food 식도 meals*3 + fineMeals*5 가중치로 바뀜.
- **수정안**: 주석 3곳 현행화: ResourceCounterUI.cs:6 → '좌상단 자원 readout (#41 RimWorld ResourceReadout 정합)', TopBar.cs:85-106 의 §3.1 가로 서사를 '#41 로 세로 이전, §3.1 의 칩 단위 격리 원칙만 계승' 한 단락으로 압축, ResourceLowAlert.cs:8 → 실제 조건(wood<5, food+meals*3+fineMeals*5<5) 명기. ResourceRow→ResourceReadout 개명은 씬 재생성 시 함께 (코드 Find() 참조 없음 확인됨).

### [left-resources] 자원 칩에 hover tooltip 부재 — 식량/식사 의미 구분 보조 수단 없음  `#1.7`
- **증거**: MakeResChip 의 아이콘·구분선은 raycastTarget=false, 칩에 tooltip 컴포넌트 없음 (TopBar.cs:202,231). 식량(원재료) vs 식사(조리됨 1끼) 구분이 아이콘+색으로는 읽히지만 '식사 1 = 식량 3 어치' 같은 환산 정보(ResourceLowAlert 의 *3 가중치)는 화면 어디에도 설명이 없다. 코드베이스에 HoverTooltip.cs 가 이미 존재하나 UI Rect 대상 부착 가능 여부는 미검증 (추정).
- **수정안**: 기존 HoverTooltip 시스템이 UI 요소를 지원하면 ResChip 루트에 부착해 한 줄 설명('식량: 조리 전 원재료', '식사: 조리된 끼니 — 식량 3 소비로 1개 생산') 표시. 미지원이면 보류 (신규 tooltip 시스템 제작은 기능동결 위반이므로 하지 않음).
- **RimWorld 근거**: RimWorld readout 항목은 hover 시 tooltip 으로 이름/상세 제공 (Steam 토론 'read the tooltips for them' — categorized/display 토글 tooltip 관행 발췌)

### [system-menus] 사문 주석 3곳 + 영구 null 죽은 직렬화 필드 — 차기 수정자 오도 위험  `#6.8`
- **증거**: (1) SettingsMenu.cs:178-182 주석이 "SaveHint lane 의 SaveBtn/LoadBtn ... invokes their wired onClick (see InvokeExistingButton)" 라 하나 InvokeExistingButton 메서드는 코드베이스에 없음(#44 에서 gsb.OnSave 직접 호출로 교체)이고 시각 SaveBtn/LoadBtn 도 #245 로 미생성. (2) SettingsMenu.cs:220-225 OpenInternal 주석 "Hide the redundant floating S/L corner buttons ... we only hide its visual GO" — 숨기는 코드 없음(숨길 버튼 자체가 없음). (3) GameSaveButtons.cs:7-11 클래스 주석 "in-game Save / Load buttons (top-left)" — 현재는 시각 버튼 0개 로직 호스트. (4) GameSaveButtons.cs:14-15 saveButton/loadButton 필드와 Awake 배선(43-53)은 SceneSetup.Game.SaveHint.cs:27 이 의도적으로 null 로 두므로 영구 죽은 코드.
- **수정안**: 주석 3곳을 현행 구조(#44 직접 호출/#245 시각버튼 제거)로 재작성, GameSaveButtons 의 saveButton/loadButton [SerializeField] 필드 + Awake 의 onClick 배선 블록 삭제(SceneSetup.Game.SaveHint.cs 의 SerializedObject 는 pawnPrefab/treeSprite 만 세팅하므로 안전).

### [system-menus] 볼륨 슬라이더 현재값 미표시 + SFX 조절 시 즉각 청각 피드백 없음  `#6.9`
- **증거**: MakeSlider(436-516)는 트랙/필/핸들만 생성, 수치 Text 없음 — 운영자는 현재 볼륨이 몇 % 인지, 두 슬라이더가 같은 값인지 눈으로 비교 불가. OnSfxChanged(306-311)는 볼륨만 세팅하고 샘플음을 재생하지 않아 다음 효과음이 날 때까지 변경 체감 불가. (#39 의 트랙 대비 fix 는 코드 확인 — 트랙 0.10,0.10,0.13 vs PanelBg 0.165,0.122,0.094 로 해결로 기록.)
- **수정안**: 각 슬라이더 라벨 행 우측에 % Text 추가(MakeLabel 재사용, TextSecondary, TextAnchor.MiddleRight): SyncSlidersFromAudio/OnSfxChanged/OnMusicChanged 에서 `Mathf.RoundToInt(v*100)+"%"` 갱신. OnSfxChanged 에 0.15s 스로틀 걸고 bank.PlaySelect() 호출해 드래그 중 즉각 청각 프리뷰 제공.

### [tabs] 일정 셀이 클릭-순환 전용 — RimWorld 식 드래그 페인팅 부재로 야간 시프트 설정에 행당 수십 클릭  `#5.10`
- **증거**: ScheduleUI.MakeSlotCell(181-193) — Button 1개, 좌클릭 4단 순환(Anytime→Sleep→Work→Joy)만 가능. 한 pawn 의 8시간 수면대를 옮기려면 셀당 최대 3클릭×시간 수 = 수십 클릭. WorkTabUI 는 우클릭=즉시 0 단축이라도 있는데(317-327 WorkTabCellClick) ScheduleUI 는 그것조차 없어 탭 간 조작 문법도 불일치. RimWorld 는 타입 선택 후 드래그로 칠하는 페인팅 방식.
- **수정안**: ScheduleUI.MakeSlotCell 의 Button 을 WorkTabCellClick 류 핸들러로 교체 + 드래그 페인팅: IPointerDownHandler 에서 '다음 슬롯값' 계산해 paintValue 로 저장·적용, IPointerEnterHandler 에서 Input.GetMouseButton(0) 이면 paintValue 를 그대로 적용(기존 SetSlot 재사용, 신규 시스템 아님). 우클릭=Anytime 리셋도 함께.
- **RimWorld 근거**: https://rimworldwiki.com/wiki/Schedule (Menus §Schedule) — "The Schedule screen allows the player to schedule an activity for each colonist, for each hour of a 24-hour day": 바닐라는 시간대를 드래그로 칠하는 조작.

### [tabs] 탭 제목의 이모지(📋/📅)가 legacy Text + OS 한글 폰트에서 tofu 로 깨질 가능성  `#5.11`
- **증거**: WorkTabUI.cs:80 '📋 직업 우선순위 (F1)', ScheduleUI.cs:69 '📅 일정 (F4)...'. Unity legacy UI Text 는 컬러 이모지를 렌더링하지 못하고 Malgun Gothic dynamic font 에 해당 글리프가 없어 빈 칸/□ 로 표시될 공산이 큼 — 추정: 두 탭이 열린 스크린샷이 없어 육안 확인은 못 함(01_natural_play.png 은 탭 닫힘 상태). 같은 코드베이스의 GuiControlBar 버튼/ResearchUI 텍스트는 이모지 미사용.
- **수정안**: WorkTabUI.cs:80 / ScheduleUI.cs:69 에서 이모지 프리픽스 제거('직업 우선순위 (F1)', '일정 (F4) - ...') — 제목은 이미 AccentGold+Bold 라 시각적 손실 없음.

### [tabs] 탭 행 정렬이 GetInstanceID 기준 — 상단 콜로니스트 바 순서와 무관한 임의 순서  `#5.12`
- **증거**: WorkTabUI.cs:137 / ScheduleUI.cs:114 — System.Array.Sort(pawns, InstanceID 비교). 두 탭끼리는 일치하지만 InstanceID 는 세션마다 임의라, 스크린샷 상단 콜로니스트 바(서연/민지/지훈 순)와 탭 행 순서가 일치한다는 보장이 없음(추정: ColonistBar 의 정렬 기준은 본 감사 범위 밖이라 미확인 — 코드상 두 시스템이 정렬 기준을 공유하지 않는 것은 확인).
- **수정안**: 두 파일의 Sort 비교자를 이름 기준으로 통일: 'System.Array.Sort(pawns, (a,b) => string.CompareOrdinal(a.name, b.name));' — 또는 ColonistBar 가 노출하는 로스터 순서가 있으면 그것을 공용 정렬 키로 사용.
- **RimWorld 근거**: https://rimworldwiki.com/wiki/Work — Work 탭 행 순서는 콜로니스트 바 순서와 일치하는 것이 바닐라 관례.

### [top-bar] ColonistBar 정렬 키가 GetInstanceID — 세이브/로드 후 바 순서가 뒤바뀔 수 있음  `#2.6`
- **증거**: ColonistBar.cs:143-148 — "결정적 순서" 주석으로 instanceID 정렬. 세션 내에서는 안정적이나 Unity instanceID 는 씬 리로드/로드게임 간 보존되지 않으므로 같은 3인이 로드 후 다른 순서로 나타날 수 있음 (세이브/로드 사이클 실측은 안 함 — Unity instanceID 의미론에 근거한 추정).
- **수정안**: RebuildEntries 의 비교자를 PawnName 1차(string.CompareOrdinal) + instanceID 2차 타이브레이크로 교체 — 한 줄 변경, 로드 간 순서 안정.

### [top-bar] TimeUI 일시정지 표기 "❚❚ PAUSED" — 전한글 HUD 속 영문 + 폰트 미보유 글리프 위험  `#2.7`
- **증거**: TimeUI.cs:36 `txt.text = "❚❚ PAUSED"` — 우하단 버튼은 "멈춤 (Space)"(GuiControlBar, 스크린샷 확인)인데 같은 상태의 텍스트 표기는 영문. 톤 불일치. 또한 ❚(U+275A)가 한국어 폰트에 없으면 tofu 박스 가능 (일시정지 상태 스크린샷이 없어 미검증 — 추정).
- **수정안**: TimeUI.cs Refresh 의 문자열을 "❚❚ 멈춤" 또는 "일시정지" 로 교체해 버튼 캡션과 통일, ❚ 글리프는 LoadKoreanFont 결과로 1회 렌더 확인 후 불안하면 "II" 로. F(속도 이중표시) 합류안 적용 시 클러스터 쪽 문자열로 함께 정리.

### [top-bar] ClockUI 죽은 스캐폴딩 — "3줄" 주석·클래스 독과 1줄 실체 불일치, 단일 자식용 VerticalLayoutGroup  `#2.8`
- **증거**: ClockUI.cs:19 클래스 독 "3줄을 그린다", 89행 주석 "3줄을 위→아래... VerticalLayoutGroup 으로 간단 배치" — 실제 생성 라인은 timeText 하나(99행). 1개 고정 라인에 VLG+LayoutElement 기계 장치가 남아 있고 문서가 실체와 어긋나 다음 작업자의 오독을 유발.
- **수정안**: F(속도 합류)+F(날짜 합류) 채택 시 클러스터가 실제 3줄(날짜/시각/속도)이 되어 VLG 가 정당화되므로 주석만 현행화. 미채택 시 ClockUI.cs:89-96 VLG 제거하고 timeText 를 content 에 직접 anchor, 19행·89행 주석 1줄 표기로 수정.

### [top-bar] ColonistBar 폭 무제한 — 인원 증가 시 화면 양끝 오버플로 (현 3인이라 잠재)  `#2.9`
- **증거**: ColonistBar.cs:157-158 totalW = n*104+(n-1)*6 무제한 → 1920 기준 17인 초과 시 양끝 화면 밖, 그 전에 우상단 알림 스택 영역과 근접. 현 콜로니 3인이라 실발생 안 함 (추정 — 미재현, 산술 근거). 기능동결 하에서도 클램프는 기존 UI 보강에 해당.
- **수정안**: RebuildEntries 끝에 `float maxW = 1400f; rootRt.localScale = Vector3.one * Mathf.Min(1f, maxW / Mathf.Max(1f, totalW));` 한 줄 — RimWorld 식 축소 동작의 최소 구현(스케일 다운). 행 줄바꿈 같은 신규 시스템은 보류.
- **RimWorld 근거**: 위키 colonist bar 항목은 'The bar can be turned off and on via toggle' 까지만 기재 (다인원 축소 동작은 본 페이지 stub 에 미기재)

