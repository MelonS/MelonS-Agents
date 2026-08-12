# Plan #198 — 건축 fix + 디자인 polish + QA (game-pm 계획)

세션 directive (운영자, 2026-05-29): **QA / 건축 fix / 디자인 / 파이프라인**
(game-pm → programmer → game-qa).  기조: **"기능추가는 보수적으로, 게임이
되는게 먼저."**

마지막 shipped: #197.  현재 QA baseline run 진행 중.

---

## 0. Daily-shippable 불변식 (절대 규칙)

이 increment 가 끝나면 **반드시** launchable `PawnSim.exe` 가 나오고,
`refactor_check.py` 전 단계 PASS 해야 함.  기능 미완이라도 빌드는 켜지고
메뉴 선택되고 뭔가 보여야 함.  중간에 빌드가 안 켜지는 상태로 commit 금지.

각 task 는 **단독으로 daily-shippable** 을 보존한다 (stub-then-polish 패턴).
빌드 안 켜지는 중간 상태가 필요하면 task 를 더 쪼갠다.

---

## 1. Scope decision — vision-impact 순 랭킹

알려진 미해결 결함 + 디자인 항목을 vision-impact (바닐라 콜로니심급 +
정직한 검증) 로 랭킹.  이번 increment 는 **상위 2개만** 가져간다 (보수적).

| 순위 | 항목 | vision-impact | 이번 #198? | 근거 |
|------|------|---------------|-----------|------|
| **1** | 건축 end-to-end 정직 재검증 (defect #3) + bed sprite 런타임 binding (defect #1) | **최상** — "게임이 되는가"의 핵심. 운영자가 #189~197 내내 "건축 안 됨" 불만 | **YES → task #3** | 게임의 코어 루프. 작동/검증 안 되면 나머지 무의미 |
| **2** | 디자인 polish — bed/wall sprite 시각 정합 + UI 일관성 (defect #4) | **상** — 운영자 "디자인 구리고 프로토타입 수준도 안됨" 상시 불만 | **YES → task #4** | task #3 와 같은 entity(bed/wall) 를 건드리므로 한 increment 에 묶는 게 효율적. magenta error quad / 빈 sprite 제거가 1차 목표 |
| 3 | SaveLoad fidelity (defect #2 — BedQuality/StockpilePriority/TreeSpecies/WallMaterial 직렬화) | 중 | **NO → 다음 increment** | 독립적 작업 단위(SaveLoadManager + V-scenario). 건축 binding 이 먼저 확정돼야 무엇을 직렬화할지 명확. #199 후보 |
| 4 | P6 야간 시연 screenshot | 중하 | **NO** | -starthour CLI 이미 있음(P6 인프라 done). screenshot 산출은 QA 부산물로 자동 첨부 가능하나 별도 작업 아님 |
| 5 | P7 combat 시퀀스 screenshot | 하 | **NO** | 순수 산출물 작업. 코어 루프 확정 후 |
| 6 | Stretch (power/trading/taming/bills) | 보류 | **NO** | 운영자 "기능추가 보수적" 명시. 손대지 않음 |

**결론: 이번 increment = task #3 (건축 fix) + task #4 (디자인) + task #5 (QA gate).**
defect #2 (SaveLoad) 는 #199 로 명시 push.  daily-shippable 보존 + 보수적 스코프.

---

## 2. Concrete deliverables + binary 합격 기준

### Task #3 — 건축 fix (bed sprite 런타임 binding + end-to-end 정직 재검증)

**문제 정의:**
- defect #1: `bm.BedSpriteRef` 가 런타임에 null → BuildAutoQA Phase 2 가 bed
  를 시각 확인 못 함. V56 unit test 는 BedQuality API 만 보증, in-game
  sprite binding 은 미검증.
- defect #3: #189~197 동안 "청사진까지는 되는데 실제 건축이 안됨" 을 반복
  fix. 지금 진짜 end-to-end (청사진 → 자재/작업 → 완성 entity + sprite 표시)
  가 되는지 **정직하게** 재확인 필요.

**Deliverable D3-1 — bed sprite 런타임 binding:**
- `bm.BedSpriteRef` (또는 동등 참조) 가 런타임에 non-null 이고, 완성된 bed
  entity 의 SpriteRenderer.sprite 가 실제 bed_wood/bed_fine sprite.
- **Binary 합격 (game-qa):** Build Click QA 의 bed phase 가 완성 후 screenshot
  을 캡처 → game-qa 가 PNG 를 Read → **침대 형태의 sprite 가 보임**
  (magenta error quad / 빈 사각형 / null sprite 아님). 추가로 런타임 로그에
  `[BuildClickQA] bed: SpriteRef non-null, renderer.sprite=bed_*` 형태 라인.

**Deliverable D3-2 — 건축 end-to-end 정직 재검증:**
- 청사진 배치 → hauler 자재 운반 → builder 작업 → blueprint 가 실제 entity
  로 치환 + sprite 표시, 6 모드(wall/floor/door/stove/bed_wood/bed_fine) 전부.
- **Binary 합격 (game-qa):** `-build-click-qa` 실행 →
  `[BuildClickQA] OVERALL: PASS` 1회 이상 + 6 case 모두 `: PASS`.
  case fail 0. (이미 harness step 4.6 에 있음 — 회귀 0 + bed case 신규 강화.)

**제약:** 아키텍처 변경 금지(programmer 가 BuildManager 내부에서 해결). PM 은
"BedSpriteRef 가 null 인 root cause 를 stub 으로 우회하지 말고 실제 binding
하라" 만 요구 — TODO/가짜 PASS 금지(roadmap hard contract).

---

### Task #4 — 디자인 polish (bed/wall sprite 정합 + UI 일관성)

**문제 정의:** 운영자 상시 불만 "디자인 구리고 프로토타입 수준도 안됨".
이번엔 task #3 가 건드리는 entity (bed/wall) 의 **시각 정합**에 한정 — 스코프
폭발 방지. 새 sprite redraw 큰 작업은 안 함.

**Deliverable D4-1 — bed sprite 시각 품질:**
- bed_wood / bed_fine 두 sprite 가 구분 가능(quality 별 색/디테일 차이) +
  침대 형태로 인지됨(매트/프레임 보임). 1x2 multi-cell footprint(#193) 와
  정렬 일치 — sprite 가 cell 경계 안에 들어옴, 떠 있거나 잘리지 않음.
- **Binary 합격 (game-qa):** 완성 bed screenshot PNG Read →
  (a) bed_wood vs bed_fine 가 시각적으로 구분됨, (b) sprite 가 2-cell 영역에
  정렬(중심 오프셋이 cell 경계 밖으로 안 나감). 둘 다 충족.

**Deliverable D4-2 — wall 시각 피드백 정합 확인:**
- #158 wall damage tint (hpRatio 별 어두워짐) 가 건축 완성 직후 풀-HP 상태
  에서 **올바른 base material tint** 로 시작하는지(데미지 없는데 어둡게
  시작하는 회귀 없음).
- **Binary 합격:** V60 (wall damage tint preserved) GREEN 유지 + 완성 직후
  wall screenshot 의 tint 가 base material 색(과도하게 어둡지 않음).

**Deliverable D4-3 — UI 일관성 (가벼운 정리):**
- Architect/Furniture 메뉴의 bed 버튼 3종(SleepingSpot/Wood/Fine) label /
  아이콘이 일관(폰트·정렬·한글 표기 통일). 신규 위젯 추가 아님 — 기존 정리.
- **Binary 합격:** 메뉴 screenshot Read → 3 버튼 label 한글 정상 표기 +
  정렬 어긋남 없음. (소프트 기준 — game-qa 가 육안 확인 후 PASS/FAIL 코멘트.)

**제약:** sprite 생성은 기존 PIL Kenney-style 파이프라인(#194) 재사용. 새
스타일 도입 금지. visual diff threshold 5% 안에 들도록 — 의도적 시각 변경이면
`--accept-visual` 로 baseline 갱신 + 운영자에게 before/after 보고.

---

## 3. Dependency order

```
  task #3 (건축 fix)  ──선행──▶  task #4 (디자인)  ──선행──▶  task #5 (QA gate)
   D3-1 sprite binding         D4-1 sprite 품질        전 단계 회귀 0
   D3-2 e2e 재검증             D4-2 wall tint
                              D4-3 UI 일관성
```

**왜 이 순서:**
1. **#3 먼저** — bed sprite 가 런타임에 binding 돼야(D3-1) 디자인 작업(D4-1)
   이 화면에 나타나 검증 가능. binding 없이 sprite 만 예쁘게 그려도 게임에
   안 보임. "게임이 되는게 먼저" 원칙과 일치.
2. **#4 다음** — binding 확정 후 sprite 품질/정렬/UI 정리.
3. **#5 마지막** — 두 task 가 daily-shippable 을 깨지 않았는지 full harness.

**중간 daily-shippable 체크포인트 (각 task 끝에서):**
- task #3 끝: programmer 가 `refactor_check.py --skip-scenes` 빠른 사이클로
  빌드 켜짐 + Build Click QA PASS 확인 후 commit. (디자인 미적용이어도 ship 가능.)
- task #4 끝: 동일하게 빌드 켜짐 확인 후 commit.
- task #5: full gate (아래 §4).

QA fail 시: fix-in-place(해당 task 연장) 또는 직전 last-good commit 으로 roll
back. 절대 fail 상태로 다음 task 진행 금지.

---

## 4. QA gate — 정확한 invocation + 회귀 기준

### 최종 게이트 명령 (task #5, full)

```powershell
cd G:\ai\MelonS-Agents
python skills/game-dev-agent/scripts/refactor_check.py --tag 198
```

이 명령은 다음을 순서대로 강제하고 **전부 PASS 해야 #198 commit 가능:**

| 단계 | 검증 | #198 합격선 |
|------|------|------------|
| 1 scenes regen | compile error 0 (`error CS` 없음) | PASS |
| 2 build verify | build compile error 0 | PASS |
| 3 QA screenshot | PawnSim.exe 켜짐 + 캡처 | PASS |
| 4 log scan | Player.log Exception/NullRef 0 | PASS |
| 4.5 REAL QA (30s) | 최신 day-N build, wood 증가 > 0 | PASS (회귀 검출) |
| **4.6 Build Click QA** | `OVERALL: PASS` + **6 case 전부 `: PASS`** | **D3-2 핵심. bed case fail 0** |
| 6 isolated V | **64/64 PASS** (현재 baseline) + 신규 V61 | **64 → 65, 회귀 0** |
| 7 integration I | **36/36 PASS** (현재 baseline) | **회귀 0** |
| 5 visual diff | baseline 대비 < 5% (또는 의도적이면 `--accept-visual`) | PASS |

### 회귀 0 으로 GREEN 유지해야 하는 핵심 V/I 시나리오

건축·디자인을 건드리므로 아래는 **반드시 GREEN 유지** (programmer 가 깨면 안 됨):
- **V56** bed quality rest/mood mul (0.80/1.00/1.40) — bed binding 변경이
  quality API 안 깨야 함.
- **V60** wall damage tint preserved — D4-2 가 직접 관련.
- **I36** BedFine regression (2s in-process) — bed phase 핵심 회귀 가드.
- Build Click QA 6 case (wall/floor/door/stove/bed_wood/bed_fine) — D3-2.

### programmer 가 신규 추가할 V 시나리오

- **V61 (신규) — bed sprite runtime binding:** 게임 내 bed entity 완성 후
  `SpriteRenderer.sprite != null && sprite.name` 가 `bed_wood`/`bed_fine` 중
  하나임을 단언. defect #1 의 정직한 검증 — `BedSpriteRef` null 회귀를 영구
  가드. (isolated testmode 에 추가 → 64 → 65.)
  - **Binary:** `-testmode` report 에 `V61: OK ... sprite=bed_*` + totalFailed 0.

### game-qa 가 추가로 할 시각 검증 (harness 밖 육안)

harness 의 픽셀 diff 는 형태를 못 본다. game-qa 는 아래 PNG 들을 **Read** 해서
binary 코멘트:
- Build Click QA bed screenshot → 침대 형태 sprite (D3-1, D4-1).
- 완성 wall screenshot → base material tint 정상 (D4-2).
- Architect/Furniture 메뉴 screenshot → bed 3버튼 label 정합 (D4-3).
- (부산물) `-starthour 22` 야간 screenshot 1장 첨부 — P6 데모 겸 시각 회귀 참고.

---

## 5. 산출물 / commit 계획

- task #3 → commit `#198a 건축 bed sprite runtime binding + e2e 재검증 + V61`
- task #4 → commit `#198b bed/wall sprite 정합 + Architect 메뉴 UI 일관성`
- (필요 시 visual baseline 의도 갱신은 별도 `--accept-visual` + 보고)
- 각 commit 전 daily-shippable 확인. full gate 는 task #5 에서.

## 6. 다음 increment 로 명시 push (스코프 밖)

- **#199 후보:** SaveLoadManager fidelity (defect #2) — BedQuality /
  StockpilePriority / TreeSpecies / WallMaterial 직렬화 + V-scenario
  (save → load → bed still Fine quality 단언). 이번 binding 확정 후 진행.
- P7 combat 시퀀스 screenshot.
- Stretch (power/trading/taming/bills) — 운영자 명시 지시 전까지 보류.
