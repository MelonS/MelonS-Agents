> 출처: UI 겹침 전수 감사 워크플로 wf_80af6af8 (28 에이전트, 확정 16쌍+1).

# UI 겹침 전수 적발 백로그 (2026-06-12, 검증 통과분 합성)

## 1) 평결

검증을 통과한 겹침은 **고유 16쌍 + 부수발견 1건**(렌즈 중복 3쌍 병합)이며, **P0(상시·조작 차단)은 0건, P1 9건, P2 7건**이다. 근본 원인은 4개 클러스터로 수렴한다: ① **좌하단 상태조합 누락** — F2의 PawnInfoPanel 시프트(wantX=272)가 카테고리 본체(252px)만 피하고 아이콘 셸프(x266~, y8–100)·SkillPanel(400,58 고정)을 전혀 모름 (5쌍), ② **상단 stale 좌표** — ModeBanner y=-56이 ColonistBar 의 y=84 이동(#238b)을 못 따라가 TopBar/ColonistBar 를 동시에 깎음 (2쌍, 수정 1줄), ③ **중앙 모달 상호배타 미가입** — ResearchPicker 의 CloseSiblingPanels 가 SettingsMenu/HotkeyCheatSheet 를 누락 + raw 핫키(N, 1–5)가 backdrop 을 관통 (2쌍), ④ **월드 라벨 sortingOrder·오프셋 충돌** — zZ/⛏/청사진 라벨/status/냠 팝업/이름끼리 (6쌍). 유일한 실조작 차단은 **셸프→징집 버튼**(71/76px 덮임, 셀 틈 5px만 클릭 가능)이며 z순서상 QA 경로에서도 셸프가 위라 상시 재현된다. 전 항목 Effort S(ColonistBar 줄바꿈만 M)로 1차 배치 수정이 가능하나, **아래 3)의 좌표 커플링 5건을 안 지키면 픽스끼리 새 겹침을 만든다.**

---

## 2) 우선순위 목록

### P0 — 없음

(상시+조작 차단 조합 없음. 최악인 셸프→징집도 건축 메뉴 열림 상태 한정이라 P1.)

### P1

| # | 겹침 요소 | 조건 | 수정 (구체 수치) | Effort |
|---|---|---|---|---|
| P1-1 | **ModeBanner ↔ TopBar(20px) + ColonistBar(2px 테두리)** — 겹침 2쌍, 수정 1줄 | 아무 지정/건설 모드 활성(핵심 루프, 사실상 매판). 배너(780,56)–(1140,86)이 TopBar 하단 보더(y74–76)와 ColonistBar 상단 보더(y84–86)를 동시 클립. 시각 전용 | `ModeBanner.cs:82` anchoredPosition **(0,-56) → (0,-146)** (76+8+54+8 산식, 주석 의도대로 콜로니스트 바 아래로). stale 주석 갱신 | S |
| P1-2 | **Architect 셸프 ↔ 징집 버튼** (유일한 조작 차단) | 건축 메뉴 + Zone(7셀)/Structure(6셀) 선택. 셸프 셀6·7이 징집 버튼(692,32)–(768,88)의 71/76px 를 덮고 raycast 도 가로챔. z순서는 QA·Open() 양 경로 모두 셸프가 위 | `ArchitectMenu.cs` RefreshShelf(584–635)에 **5셀 줄바꿈**: 5셀마다 x=0, y+=95 (1행 우단 626 < 탭바 좌단 680) | S |
| P1-3 | **셸프 ↔ 시프트된 PawnInfoPanel 하단 40px 띠** | 폰/엔티티 **선택 중**(무선택 시 패널 숨김, fb#3) + 건축 열림 + 카테고리 활성. 교차 y58–98, 기분/수면 바 시각 가림(밴드 내 raycastTarget=false라 차단은 아님) | `PawnInfoPanel.cs:407` 시프트 블록에 **wantY = IsOpen ? 108 : 58** 추가 (EnsurePlacement 가 매 프레임 (12,58) 리셋하므로 반드시 407 블록에서, 가드는 x+y 모두 비교). **3)-B 커플링 주의** | S |
| P1-4 | **SkillPanel ↔ 시프트된 PawnInfoPanel** (방향 정정: 늦은 sibling 인 SkillPanel(400,58)–(580,218)이 시프트 인포패널(272,58)–(652,306) 콘텐츠 밴드 위에 뜸) | 폰 선택 + 건축 열림 (F8 한 번이면 도달). 인포패널 본문 x400–580/y58–218 가림 | `SkillUI.cs` Update 에 시프트 미러: ArchitectMenu.IsOpen 시 **(400,58) → (660,108)** (= (660,108)–(840,268), 셸프 top 100·탭바 y96·인포패널 우단 652 모두 클리어). 대안: 건축 열림 중 SetActive(false). **(272,314) 슬롯은 금지 — 3)-A** | S |
| P1-5 | **ResearchPicker ↔ SettingsMenu 이중 모달 + 숨은 핫키** | 방향A: 설정 열림 중 N(raw Input 이라 backdrop 관통). 방향B: 픽커 열림 중 ⚙ 클릭. 픽커가 backdrop 아래 깔린 채 **1–5 키로 안 보이는 연구 시작 가능** | ① `SettingsMenu.cs` 에 `public bool IsOpen` + static 접근자 **신설**(현재 private) ② `ResearchUI.cs:134-138` CloseSiblingPanels 에 SettingsMenu 추가 ③ SettingsMenu.Open() 에서 ClosePicker()+WorkTab/Schedule Close ④ `ResearchUI.cs:47`(N)·**:59-71(Alpha1–5)** 양쪽에 SettingsMenu 열림 가드 | S |
| P1-6 | **SleepZz 'zZ' ↔ 본인 이름 라벨** | 수면 중 전 림 × 기본 줌(매일 밤 전원). zZ(order 31)가 이름(30) 끝 글자 위에 ~7x12px 덮임 | `PawnNeeds.cs:608` localPosition **(0.35, 0.8) → (0.35, 1.25)** — 단 **P2-5 적용 시 1.32 (3)-D** | S |
| P1-7 | **MineMark ⛏ ↔ 남쪽 셀 림 이름** | 마킹 광맥 남쪽에서 채굴/통행(표준 자세) — order 30==30 타이로 드로우 순서 미정의, 골드 이름+흰 ⛏ 판독 불가 | `MineDesignation.cs:466` sortingOrder **30 → 28** (name shadow 29/name 30 아래 고정). y너지(0,0.15)는 단독 무효 — 생략 가능 | S |
| P1-8 | **BlueprintStatus 라벨 ↔ NightOverlay** (order 25==25 타이) | 밤 + 미완 청사진. 타이 패배 세션에선 '자재 n/m·건설 %'가 alpha 0.72–0.82 어둠에 감광돼 판독 불가(비결정적 발현) | `BlueprintEntity.cs:107` sortingOrder **25 → 26**. 후속: `InspectHighlight.cs:54`도 25 동급 위험 — 별도 티켓 | S |
| P1-9 | **[부수·비겹침] 3x/6x 속도 버튼 하이라이트 사망** | fb#13 으로 SetScale(3f/6f) 전환 후 하이라이트 판정만 2f/4f 잔존 → 3x/6x 시 무점등 + 레거시 4f 경로에선 6x 오점등 (매 세션 재현이라 P1 격상) | `GuiControlBar.cs:344-345` **Approximately(s,2f)/(s,4f) → (s,3f)/(s,6f)** | S |

### P2

| # | 겹침 요소 | 조건 | 수정 | Effort |
|---|---|---|---|---|
| P2-1 | **셸프 ↔ SkillPanel 하단 40px** (교차 (400,58)–(580,98), 전투 행 y72–96 전부) | 폰 선택 + 건축 열림 + ≥2셀 카테고리. Structure 우단 699/Zone 772 라 x660 이동만으론 미해소 | **P1-4 의 (660,108) 이동으로 자동 해소** — 별도 코드 0줄 | S |
| P2-2 | **TutorialBg ↔ 시프트된 PawnInfoPanel** 12x64px 슬리버 | 폰 선택 + 건축 열림 + 팁 표시(실시간 1–25초, 온보딩 흐름상 현실적) | `SceneSetup.Game.Tutorial.cs:29` 폭 **640 → 560** (좌단 ~680, 인포패널 우단 652와 28px 갭). **P1-3 의 y108 로는 해소 안 됨 — 별도 필수 (3)-E** | S |
| P2-3 | **ColonistBar 무제한 가로 성장 ↔ ResourceRow** | n≥10 한계(긴 목재 readout), n≥11 견고, n≥14 무조건. entry border raycast=true 라 오클릭 시 콜로니스트 선택+카메라 점프 | `ColonistBar.cs:160-163` RebuildEntries 에 **9개/행 줄바꿈** (maxW≈1000, 2행째 y-=58, root 높이 행수 연동). **ModeBanner 위치 행수 연동 — 3)-C** | M |
| P2-4 | **ResearchPicker ↔ HotkeyCheatSheet(H)** 중앙 포개짐 (치트시트 실높이 854px — 교차 420x280) | H+N 동시 — 치트시트는 상호배타 미가입, SetVisible private 라 외부에서 닫을 수단 자체가 없음 | `HotkeyCheatSheet.cs` 에 **public Close()/Instance 신설** 후 양방향 상호배타 등록. 대안: 좌측 정렬(x12, anchor(0,0.5))로 중앙 슬롯 탈거 | S |
| P2-5 | **Status 줄 ↔ HP바 프레임** (가용 밴드 0.155wu < status 박스 0.187wu — 구조적) | 림 선택 + HP바 가시(징집 전투가 전형) — '[징집]' 브래킷 디센더가 프레임에 ~2px 클립 + order 31==31 | `PawnNameLabel.cs:23-24` offset.y **1.06 → 1.12**, statusGap **0.26 → 0.27** (여유 원하면 0.28). **P1-6 연동 — 3)-D** | S |
| P2-6 | **'냠' 팝업 ↔ 이름 라벨** (이중 y오프셋: 콜사이트 +0.6 + Spawn 내부 +0.3 = 실스폰 0.9) | 식사 중 1.1s 주기 재스폰, 수명의 64%가 이름 밴드 통과 — 끝 글자 ~7px 클립 반복 | `PawnNeeds.cs:663` 등 +0.6 콜사이트 **+0.6 → +1.0**, 냠 x **0.3 → 0.55**. 근본수정(FloatingText.cs:140 내부 +0.3 제거)은 콜사이트 전수 보정 동반 시에만 | S |
| P2-7 | **이름 라벨 ↔ 이름 라벨** (동일 셀 수렴 시 100% 합동, order 30==30) | 식사 러시/좁은 문/치료 등 서브셀 수렴 — 양쪽 다 판독 불가 | `PawnNameLabel.cs` Awake 결정적 스태거: **static counter 방식 `offset.y += (s_labelTier++ & 1) * 0.18f`** — GetInstanceID()&1 금지 (전부 짝수 가능, 3)-F) | S |

---

## 3) 일괄 수정 시 상호 충돌 주의점

- **(A) SkillPanel 이동 좌표 단일화 — (272,314) 금지.** 원안 (272,314)는 인포패널 top 306 기준 계산인데, P1-3 의 y108 시프트가 들어가면 top 이 356이 되어 **(272,314)–(452,474)와 42px 신규 겹침**이 생긴다. SkillPanel 은 반드시 **(660,108)** 또는 건축 열림 중 숨김으로 통일.
- **(B) 셸프 2행 wrap ↔ 인포패널 y108 충돌.** P1-2 의 줄바꿈이 Zone/Structure 에서 2행째(y103–193, x266–412)를 만들면 y108 시프트 인포패널(272,108~)과 다시 교차한다. **wantY 를 108 하드코딩 대신 셸프 실상단+10 으로 연동**(1행=108, 2행=203)하거나, 셸프 wrap 시 인포패널 시프트를 x뿐 아니라 셸프 행수에 반응시킬 것.
- **(C) ModeBanner -146 ↔ ColonistBar 2행 wrap.** P2-3 적용 시 바 높이가 54→112가 되어 -146 배너가 2행째 안으로 들어간다. 배너 y를 **바 실높이 연동**(76+8+barH+8)으로 계산하거나, P2-3 와 같은 커밋에서 함께 조정.
- **(D) 월드 라벨 수직 사다리 동시 확정.** P2-5(이름 offset 1.06→1.12) 적용 시 이름 top 이 1.28로 올라가므로 P1-6 의 zZ 는 1.25가 아니라 **1.32**여야 한다. 권장 최종 사다리: HP 0.68 / status 0.757–0.944 / 이름 0.96–1.28 / zZ 1.32 / 팝업 실스폰 1.3+.
- **(E) 탭바 좌단 680은 동적 값.** GuiControlBar 폭이 버튼 수 기반(totalW+padX*2)이라 버튼 추가 시 680이 무너진다. 680 가정 픽스 2건(셸프 5셀 한도, TutorialBg 560)은 여유 갭(현재 각 49px/28px)을 믿되, 탭바에 버튼을 추가하는 순간 재검 필요. 또한 TutorialBg 슬리버는 **y108 시프트로 해소되지 않으므로** 폭 축소를 생략하지 말 것.
- **(F) API 선행 작업 2건.** P1-5 는 `SettingsMenu.IsOpen` public 접근자 신설이, P2-4 는 `HotkeyCheatSheet.Close()` 신설이 선행돼야 컴파일된다. P2-7 은 GetInstanceID 홀짝이 아닌 static counter 필수.
- **(G) EnsurePlacement 리셋 함정.** PawnInfoPanel 의 y 시프트를 EnsurePlacement(L320–329) 쪽에 넣으면 매 프레임 (12,58) 강제와 충돌한다 — 반드시 L407 시프트 블록에서 x·y 동시 적용, 변경 가드도 둘 다 비교.
- **권장 배치 순서**: ① P1-1·P1-9·P1-7·P1-8 (독립 1줄 픽스 4건) → ② 좌하단 패키지 P1-2+P1-3+P1-4(+P2-1 자동)를 (A)(B) 반영해 한 커밋 → ③ 모달 패키지 P1-5+P2-4 → ④ 월드 라벨 패키지 P1-6+P2-5+P2-6+P2-7 을 (D) 사다리로 한 커밋 → ⑤ P2-2 → ⑥ P2-3+(C).

✅완료
