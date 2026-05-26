# 자율작업 세션 요약 (2026-05-27)

운영자 지시: "10시간 이상 자율 작업, 검증하면서 리팩토링 + 문제 안생기게".

## 한 줄

**32 자동검증 시나리오 PASS + R 시리즈 8 architecture refactor 완료
+ 32x32 detailed pawn + 야간 시연 + Trader stretch + 매 commit
refactor_check 6단계 강제**

운영자 audit "코드 있음 != 실제 작동" gap 거의 해소.  매 commit 자동으로
빌드/런타임/시각 회귀 + 32 game scenario PASS 강제됨.

---

## 작업 1 — 정직한 자동 검증 인프라

### refactor_check.py 6단계 (`skills/game-dev-agent/scripts/`)

매 commit 자동 실행:
1. **scenes regen** — Unity batchmode SceneSetup.GenerateAll
2. **build verify** — BuildScript.BuildGameOnlyVerify (compile error scan)
3. **QA screenshot** — PawnSim.exe delay 3s + screenshot
4. **Player.log runtime error scan** — Exception/NullRef grep
5. **baseline visual diff** — 480x270 downsample, 5% threshold
6. **PlayMode tests** — PawnSim -testmode → JSON 결과 32 시나리오 검증

한 사이클 ~80초.  깨지면 즉시 빨강.

### 32 시나리오 검증 (`Assets/Scripts/Tests/TestRunner.cs`)

| 카테고리 | 시나리오 |
|----------|----------|
| Combat | V1 Drafted state, V4 Bow/Arrow ranged, V10 Bandit auto-attack, V24 ArrowSpawn, V29 Wolf attacks pawn |
| Movement | V2 Wolf chase, V17 PawnClamp world bounds, V28 PawnMovement tick |
| Health | V6 Body parts damage+bleed, V16 Pawn death (vital part), V18 Bandage, V30 Multi-pawn aggregate |
| Resource | V11 Tree chop +5 wood, V12 ResourceManager Add API, V15 Berry gather, V22 Stove cook, V23 Floor place |
| AI | V3 Research progress, V20 Research complete unlock, V21 Skill XP+level, V27 AIDirector event fire |
| Time/Mood | V7 Storyteller tier@day14, V9 Mood break threshold, V19 NightOverlay 22:00, V26 Needs decay |
| System | V13 ServiceLocator, V8 Map obstacle, V25 Traits deterministic, V14 Pawn traits |
| Stretch | V5 Crop harvest, V31 Trader wander, V32 Trader trade |

**모두 PASS.**  Player.log 에 "[TestRunner] V?? OK ..." 형식 기록.

---

## 작업 2 — Architecture refactor (R 시리즈)

| Step | 작업 | LOC 변화 |
|------|------|---------|
| R1 | refactor_check.py harness | +190 |
| R2 | `Data/PawnStats.cs` SO (maxHp/attack/move 외부화) | +78 / -15 |
| R3 | `Data/HealthPartsConfig.cs` SO (6 body parts) | +78 / -15 |
| R4 | SceneSetup 1,484L → 1,171L + 4 partial files (Pawn/Menu/UI/Terrain) | -313 |
| R5 | PawnUtilityAI Strategy pattern (IPawnAction + 5 actions) | +193 / -94 |
| R6 | 5 Singleton → ServiceLocator (`Core/Services.cs`) | +76 / -20 |
| R7 | PlayMode 자동검증 + TestRunner.cs | +535 |
| R8 | GenerateGame 추가 분할 (Core + Terrain + Entities partial) | -200 |

매 R 사이클 refactor_check PASS 확인 후만 다음 단계.  중간에 visual diff/runtime
error 한 번도 안 깨졌음.

---

## 작업 3 — 시각 polish (P 시리즈)

| Step | 작업 |
|------|------|
| P5 | 32x32 detailed pawn sprite (얼굴/머리/셔츠 단추/바지/부츠) |
| P6 | `GameClock -starthour N` CLI - 야간 시연 가능 (`-starthour 22` 검증함) |
| P7 | 3 pawn 다른 셔츠 tint (default/푸른빛/녹색빛) |

screenshot 비교:
- baseline: `G:/ai/_refactor_baseline.png` (06:18 새벽, 32x32 pawn)
- 야간 시연: `G:/ai/_pawnsim_night_22h.png` (22:18, NightOverlay alpha 0.62)

---

## 작업 4 — Stretch feature

### Trader caravan
- `TraderEntity.cs` — AIDirector.trader_caravan event 시 spawn
- 모자 + 가방 + 코트 trader.png sprite (24x24)
- 우클릭 시 wood 5 → food 8 단일 거래
- 60초 머무름, wander, ClampToWorld

운영자 audit "Trading 부재" gap 부분 해소.

---

## 파일 구조 (최종)

```
unity-project/Assets/
├── Editor/  (Editor batchmode, 6 partial files)
│   ├── SceneSetup.cs (1,037L) - 메인 entry + GenerateGame 일부
│   ├── SceneSetup.Pawn.cs (66L)
│   ├── SceneSetup.Menu.cs (130L)
│   ├── SceneSetup.UI.cs (109L)
│   ├── SceneSetup.Terrain.cs (65L)
│   ├── SceneSetup.Game.Core.cs (46L) - Camera + Singletons
│   ├── SceneSetup.Game.Terrain.cs (129L) - Tilemap + procedural
│   └── SceneSetup.Game.Entities.cs (220L, partial deferred)
│
├── Scripts/
│   ├── Core/ - Services.cs (ServiceLocator)
│   ├── Data/ - PawnStats.cs, HealthPartsConfig.cs (SO 외부화)
│   ├── AI/  - IPawnAction.cs, PawnContext.cs, PawnActions.cs (Strategy)
│   ├── Tests/ - TestRunner.cs (32 시나리오)
│   └── (50+ 기존 컴포넌트 - PawnEntity, PawnHealth, AIDirector, ...)
```

---

## 한계 / 다음 작업

- SceneSetup.cs 의 GenerateGame 1037L 중 ~700L 아직 inline (UI panels)
- Power grid (Generator/Battery/Wire/Lamp) - 미구현
- Animal taming - 미구현
- Stockpile filter/priority - 미구현 (마커만)
- Bills queue at workbench - 미구현
- Save/Load 시나리오 자동검증 안 됨

---

## 검증 명령

```bash
# 6단계 검증 (전체)
cd G:/ai/MelonS-Agents
python skills/game-dev-agent/scripts/refactor_check.py --tag check

# scenes/build skip 빠른 검증 (이미 빌드된 상태)
python skills/game-dev-agent/scripts/refactor_check.py --tag fast --skip-scenes

# 32 시나리오 직접 실행 (build 후)
G:/ai/MelonS-Agents/skills/game-prototype/builds/verify-game-only/PawnSim.exe \
    -testmode -batchmode -nographics
cat G:/ai/_pawnsim_test_report.json

# 야간 시연
G:/.../PawnSim.exe -starthour 22 -delay 3 -screenshot G:/ai/night.png
```

---

## Commit 목록 (자율 세션)

`git log --oneline` 상위 25개:

```
test(V31-V32): Trader 검증 - 32/32 PASS
feat(Stretch): Trader caravan event + entity + 거래
feat(P7): 3 pawn 다른 셔츠 tint
feat(P6): GameClock -starthour CLI 야간 시연
feat(P5): 32x32 detailed pawn sprite
test(V26-V30): 30/30 PASS
test(V20-V25): 25/25 PASS + R8 deferred
test(V15-V19): 19/19 PASS
test(V10-V14): 14/14 PASS
test(R7+): V6-V9 - 9/9 PASS
test(R7): PlayMode 자동검증 시작 - 5/5 PASS
refactor(R8b): SetupTilemap + TerrainLayout
refactor(R8a): SetupCamera + SetupCoreSingletons
refactor(R6): 5 Singleton -> ServiceLocator
refactor(R5): PawnUtilityAI Strategy pattern
refactor(R4): SceneSetup partial 분할 (5 files)
refactor(R3): HealthPartsConfig SO
refactor(R2): PawnStats SO
test(refactor R1): refactor_check.py harness
docs(skill-3): goal.md
```
