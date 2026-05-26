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

- [ ] **Day 39** — 강·물 타일 + 흙(dirt) 패치 + 바위 타일 (지형 다양성)
- [ ] **Day 40** — 맵 확장 20x20 → 40x40 + 카메라 줌 범위 조정
- [ ] **Day 41** — 40그루 트리 + 8 berry bush + 8 사슴 + 야생화 데코
- [ ] **Day 42** — 32x32 더 디테일한 pawn 스프라이트 (얼굴/머리/옷)
- [ ] **Day 43** — Day/Night 실제 화면 어두워짐 (overlay alpha 0.4 야간)
- [ ] **Day 44** — Tree shadow, rock cluster 외곽선 — 시각 폴리시

## PHASE B — Health & Combat (Day 45-51)

- [ ] **Day 45** — PawnHealth: body-parts (head/torso/L-arm/R-arm/L-leg/R-leg) HP per-part
- [ ] **Day 46** — Wound system: cut/bruise/gunshot — visible bar per part
- [ ] **Day 47** — Blood loss tick + bandage action (rest-on-bed restore)
- [ ] **Day 48** — Drafted state: R 키 draft, 마우스 우클릭 명령 (이동/공격)
- [ ] **Day 49** — Cover system: pawn behind wall takes 50% less damage from ranged
- [ ] **Day 50** — Bow + arrow ranged weapon, pawn auto-equips bow when drafted+raid
- [ ] **Day 51** — Bandit 종류 2개 (knife-melee, club-bash) 다른 공격 패턴

## PHASE C — Research & Power (Day 52-58)

- [ ] **Day 52** — Research bench prefab + 배치 (B mode 추가)
- [ ] **Day 53** — ResearchTree.cs: 5 techs (Bow, BetterStove, StoneWall, Electricity, Solar)
- [ ] **Day 54** — Research UI 패널 (techs 목록, 진행률, 잠금)
- [ ] **Day 55** — WindTurbine + Battery + PowerNet 그래프 (연결된 노드 propagation)
- [ ] **Day 56** — Lamp/Cooler/ElectricStove — power consumer 컴포넌트
- [ ] **Day 57** — Lamp lights up 1.5 unit radius at night (스프라이트 + 컬러 오버레이)
- [ ] **Day 58** — Cooler keeps stockpile meals fresh 2x longer

## PHASE D — Trading & Animals (Day 59-65)

- [ ] **Day 59** — Trader caravan event: 5 silver + 3 traders arrive at map edge
- [ ] **Day 60** — Trade UI: buy/sell 목재/식량/식사 (silver currency)
- [ ] **Day 61** — Pasture zone (P key, drag to define)
- [ ] **Day 62** — Animal taming: offer food (1 food 소모/시도), 30% success per try
- [ ] **Day 63** — Tamed animal AI: follow owner, eat from pasture
- [ ] **Day 64** — Predator Wolf: 4hp damage, 5 unit detect range, attacks pawn
- [ ] **Day 65** — Bonded animal mood +5 if alive, -10 if killed

## PHASE E — Farming & Storage (Day 66-72)

- [ ] **Day 66** — Farming zone (F key in zone mode), tile 변경 to "tilled"
- [ ] **Day 67** — Rice sow action: pawn walks to tilled tile, plants seed
- [ ] **Day 68** — Growth: 0%→100% over 4 game days, harvest = 8 food
- [ ] **Day 69** — Stockpile zone (S key) with item filter (wood/food/meals/silver)
- [ ] **Day 70** — Hauler job: pawn picks loose item, drops in matching stockpile
- [ ] **Day 71** — Bills queue at stove: cook 1/cook 5/cook until 30 meals
- [ ] **Day 72** — Storage capacity = zone area * 4 stacks

## PHASE F — Storyteller & Polish (Day 73-78)

- [ ] **Day 73** — 3 storytellers: Cassandra(steady), Phoebe(chill), Randy(chaotic) — event freq curves
- [ ] **Day 74** — Tutorial overlay first 60s: arrow + Korean tooltip per step
- [ ] **Day 75** — Ambient SFX: rain/wind layer when storm event
- [ ] **Day 76** — 3 BGM phases (calm/tense/combat) — crossfade by threat level
- [ ] **Day 77** — Balance pass: pawn HP, attack damage, event frequencies
- [ ] **Day 78** — Final QA: 10-min playthrough screenshot sequence + portfolio doc

---

## 작업 사이클 (각 Day마다)

1. 코드 작성 (생략·TODO 금지, 풀 구현)
2. Scene regen: `python agent.py integrate --method scenes`
3. Build verify: `python agent.py integrate --method verify-build`
4. QA screenshot via `qa.launch_and_capture(delay=2.0)`
5. Player.log 검증 (에러 없는지)
6. Git commit (push 차단 시 로컬 누적)
7. 이 파일에 ✅ 체크 + 다음 Day 즉시 시작

**작업 중단 조건**: 78일 모두 ✅ 체크 끝났을 때만.
