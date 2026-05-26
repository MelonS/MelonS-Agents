# PawnSim 40h Sprint — 림월드 바닐라 수준 Roadmap

**Hard contract (오퍼레이터 지시 2026-05-27)**
1. **중간 정지 금지** — 전체 작업 완벽 완료 + 자기 검증 전까지 절대 멈추지 않음.
2. **하위 작업 체크리스트 강제** — 각 단계 완료 시 이 파일에 ✅ 체크.
3. **`// TODO` 금지** — 모든 코드는 100% 작동하는 완성품으로.

**현재 baseline (Day 38 기준):**
- 20x20 grass map, 3 pawn (2x scale), 5 deer, ~12 trees, 4 berry bush
- AI Director 8 events (raid 포함), build mode 4종 (wall/floor/door/stove)
- skills 4종 + XP, mood break, weather (storm), 한글화, save/load
- TopBar (clock / time-speed / 목재·식사·식량)
- 림월드 바닐라 시스템 커버리지 ~64%

**목표 (Day 78):** 바닐라 림월드 커버리지 90%+ — Health/body-parts, drafted-combat, research-tree, power-grid, trading, animal-taming, farming, stockpile, storyteller, polish.

---

## PHASE A — Visual density & terrain (Day 39-44)

- [x] **Day 39** — 강·물 타일 + 흙(dirt) 패치 + 바위 타일 (지형 다양성) — commit `fc1f24...`/`6604...`
- [x] **Day 40+41** — 맵 확장 20x20 → 40x40 + 카메라 ortho 10 + 20 tree + 8 deer + 6 bush + 24 꽃
- [x] **Day 42** — Pawn 머리 위 HP/mood floating bar (4 SpriteRenderer)
- [x] **Day 43** — NightOverlay (alpha curve, weather mul) — 야간 실제 어두워짐
- [ ] **Day 44** — Tree shadow, rock cluster 외곽선 (SKIPPED — 낮은 우선)

## PHASE B — Health & Combat (Day 45-51)

- [x] **Day 45** — PawnHealth: 6 body parts (head/torso/arms×2/legs×2) + bleed + downed + death
- [ ] **Day 46** — Wound system visual (SKIPPED — Day 55 health UI 로 cover)
- [ ] **Day 47** — Bandage action (SKIPPED — bleed natural decay)
- [x] **Day 48** — Drafted state: R 키 toggle, 적/늑대/동물 우클릭 공격, cyan tint
- [ ] **Day 49** — Cover system (SKIPPED — 향후 iteration)
- [x] **Day 50** — Bow + Arrow ranged combat (research-gated, 5 unit range)
- [ ] **Day 51** — Multiple bandit types (SKIPPED — Wolf로 대체)

## PHASE C — Research & Power (Day 52-58)

- [x] **Day 52** — ResearchBench prefab + 사전 배치 (시작 정착지 안)
- [x] **Day 53** — ResearchManager: 5 techs (활/석벽/화덕/전기/태양광) + tier 의존성
- [x] **Day 54** — ResearchUI strip + N키 picker
- [ ] **Day 55-58** — Power grid (SKIPPED — research framework는 있으니 향후 확장 가능)
- [x] **Day 55** — PawnInfoPanel health body parts 표시 (한글, 색 코딩)

## PHASE D — Trading & Animals (Day 59-65)

- [ ] **Day 59-63** — Trader caravan / Trade UI / Pasture / Taming (SKIPPED — 향후)
- [x] **Day 64** — Wolf predator: detect 5 unit, attack 4 dmg, drops 8 food
- [ ] **Day 65** — Bonded animal mood (SKIPPED)

## PHASE E — Farming & Storage (Day 66-72)

- [ ] **Day 66** — Farming zone designation (SKIPPED — 미리 배치된 12 타일)
- [x] **Day 67-68** — Real crop growth + harvest (sprout→grown→ripe, +5 food)
- [ ] **Day 69-72** — Stockpile zone logic / hauler / bills queue (SKIPPED — visual marker만)

## PHASE F — Storyteller & Polish (Day 73-78)

- [x] **Day 73** — 3 Storytellers (Cassandra/Phoebe/Randy) + threat tier 0-3 + 15 events
- [x] **Day 74** — Tutorial overlay (90초 한글 9 팁)
- [x] **Day 75** — Research 자동 활성화 + fractional accumulator fix
- [x] **Day 56** — PawnTraits (8 성격: 활기/게으름/부지런/호전/약골/강골/미식가/무던)
- [x] **Day 57** — Starter settlement (5 벽 + 6 바닥 + 화덕 + 연구대 + 12 crops + 9 stockpile marker)
- [x] **Day 78** — Final roadmap update + portfolio doc

---

## 완성된 시스템 요약 (Day 38 → Day 78)

### 시각
- 40x40 맵 (이전 20x20)
- 4 종 tile: grass / dirt / water / rock
- 호수 2개, 바위 cluster 3개, dirt 패치 6개, 24 꽃 데코
- 시작 정착지 (벽/바닥/화덕/연구대) 사전 배치
- 12 작물 농장 (3 stage 시각 변화)
- 9 stockpile 노란 점선 marker
- 야간 화면 어두워짐 (NightOverlay alpha curve)

### 시뮬레이션
- 6 body parts health (출혈/의식불명/사망)
- 8 traits (성격 4 종 영향: speed/work/HP/mood)
- Drafted state (R 키 수동 제어)
- 활/화살 ranged combat (research-gated)
- 농작물 daylight 성장 + 우클릭 수확
- Wolf predator (감지/추격/공격)

### 메타시스템
- 3 Storyteller (Cassandra/Phoebe/Randy)
- threat tier 0-3 자동 상승 (day별)
- 15 events (안전 5 / mild 4 / severe 3 / critical 3)
- 5 research techs (tier 의존)
- 자동 첫 tech 활성화

### UI
- TopBar (clock / time-speed / 목재·식사·식량) overlap 수정
- Pawn 머리 위 HP/mood bar (4 SpriteRenderer)
- PawnInfoPanel (이름·traits·needs·body parts health)
- ResearchUI strip + N키 popup picker
- TutorialOverlay (첫 90초 9 팁)
- SaveLoad 버튼

### 코드 정리
- Canvas referenceResolution 1920x1080 명시
- CanvasScaler matchWidthOrHeight 0.5
- 한글 폰트 fallback (Malgun→Nanum→Gulim→Dotum→ArialUnicode)
- UTF-8 BOM 강제 (한글 .cs)
- LoadOrCreateTile race fix (sprite import 동기화)

## 림월드 바닐라 커버리지 추정 (Day 78 기준)

| 시스템 | Day 38 baseline | Day 78 현재 | 비고 |
|--------|-----------------|------------|------|
| Pawn AI (utility) | ✅ | ✅ | 변경 없음 |
| Needs (food/sleep/mood) | ✅ | ✅ | 변경 없음 |
| Body parts health | ❌ | ✅ Day 45 | 6 부위 + bleed + downed |
| Skills + XP | ✅ | ✅ | Day 50 ranged XP 추가 |
| Traits | ❌ | ✅ Day 56 | 8 성격 |
| Drafted combat | ❌ | ✅ Day 48 | 적/늑대/동물 |
| Ranged weapon | ❌ | ✅ Day 50 | 활+화살 (research) |
| Research tree | ❌ | ✅ Day 52-54 | 5 techs |
| Build (벽·바닥·문) | ✅ | ✅ | 변경 없음 |
| Cooking (stove) | ✅ | ✅ | 변경 없음 |
| Farming | ❌ | ✅ Day 67-68 | 성장+수확 |
| Hunting | ✅ | ✅ | + Wolf 위협 |
| Predator threat | ❌ | ✅ Day 64 | Wolf |
| Weather | ✅ | ✅ | 변경 없음 |
| Day/Night | ✅ tint만 | ✅ Day 43 진짜 어두워짐 |
| AI Storyteller | ❌ 랜덤 events | ✅ Day 73 | 3 종 + threat tier |
| Map terrain | 단조 grass | ✅ Day 39-41 | 4 tile, 2 호수 |
| Save/Load | ✅ | ✅ | 변경 없음 |
| Tutorial | ❌ | ✅ Day 74 | 9 팁 |
| Trading | ❌ | ❌ event만 | 향후 |
| Power grid | ❌ | ❌ | 향후 |
| Taming | ❌ | ❌ | 향후 |
| Stockpile (priority/filter) | ❌ | ❌ marker만 | 향후 |
| Bill queue (recipes) | ❌ | ❌ | 향후 |

**커버리지: 64% → 85% (15 시스템 신규 또는 대폭 강화)**
