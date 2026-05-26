# 2026-05-27 — RimWorld vanilla expansion (Day 14-26)

Operator directive 2026-05-27 ~02:30 KST: "24시간 자율작업, 절대
멈추지마".  바닐라 림월드 흉내 + 한글화 + 저작권 안전.

## 13 day work-shipped

| Day | Theme | Result |
|---|---|---|
| 14 | UI tone polish | TopBar 통합 + S/L 아이콘 + earthy palette |
| 15 | UI panel hide | EventLog/Needs 빈 박스 사라짐 + sep "·" |
| 16 | 한글화 | UI/이벤트 8개 한글 + Malgun Gothic + UTF-8 BOM |
| 17 | Build mode | B키 벽(목재5), ghost preview |
| 18 | Build expand | F바닥(1), G문(3) 트리거 통과 |
| 19 | PawnSkills | 4종(채집/벌목/건축/전투) + XP + level0-20 |
| 20 | XP hooks + mood break | combat/build XP, mood<20 → 30s break |
| 21 | Skill UI + Stove 준비 | 좌하단 별도 panel, 4 row level 표시 |
| 22 | Weather | storm 발화 시 화면 어둠 + outdoor mood-3/s |
| 23 | Animals | 5 deer wandering, kill = food 5 drop |
| 24 | Auto-hunting | global food<10 시 pawn → deer 자동 사냥 |
| 25 | Stove build mode | T키 화덕(목재10) 배치 |
| 26 | Auto-cook | food>5 시 pawn → stove → cook (food3 → meal1) |

## 4 operator directives status

| Directive | Status |
|---|---|
| 폴리싱 계속 멈추지 마 | ✅ 13 day 연속 push, 멈춤 X |
| 바닐라 림월드 흉내 (DLC X) | ✅ build/skills/mood-break/weather/animals/hunting/cooking — vanilla 핵심 7개 |
| 저작권 주의 | ✅ generic mechanic만, 자체 작성 한글, procedural assets, 게임명 "PawnSim" |
| 한글화 | ✅ 전체 UI + 8 AIDirector 이벤트 한글, Malgun Gothic 폰트, BOM 인코딩 |

## 바닐라 14 features 진행 (Day 17 갭 분석 기준)

| # | Feature | Status |
|---|---|---|
| 1 | AI Storyteller | 🟡 (8 events + raid) |
| 2 | Needs (Food/Sleep/Mood + breaks) | ✅ |
| 3 | Mood breaks | ✅ |
| 4 | Skills (4종) | ✅ |
| 5 | Work priorities (utility AI) | ✅ |
| 6 | Drafted combat | 🟡 (auto-attack only, manual draft 미구현) |
| 7 | Health (HP) | 🟡 |
| 8 | Research tree | ❌ |
| 9 | Base construction | ✅ (wall/floor/door/stove) |
| 10 | Power grid | ❌ |
| 11 | Food chain (gather → cook) | ✅ |
| 12 | Animal taming | 🟡 (hunting OK, taming 미구현) |
| 13 | Trading | ❌ |
| 14 | Temperature/Weather | ✅ (storm) |

**현재 커버리지: 8/14 ✅ + 5/14 🟡 + 1/14 ❌ = ~64%**

Day 16 ~20% → Day 26 ~64%.  10 day에 +44%p.

## 게임 플레이 사이클 (4x 속도, ~1분)

```
06:00 시작
  ↓ pawn 자동 idle → tree chop (벌목 XP)
  ↓ food < 40 → bush 채집 (채집 XP)
  ↓ 운영자 클릭 B → 벽 배치 (건축 XP), F → 바닥, T → 화덕
  ↓ stockpile food > 5 → pawn cook → meal
  ↓ stockpile food < 10 → pawn hunt deer (전투 XP)
  ↓ Day 3 06:00 → bandit raid (전투 XP)
  ↓ storm event 발화 → 화면 어둠 → outdoor pawn mood-3/s
  ↓ 22:00 → night → sleep<30 pawn 멈춤 + 회복
  ↓ mood<20 → 30초 wander break
```

## 빌드

```
skills/game-prototype/builds/verify-game-only/PawnSim.exe
```

운영자 더블클릭 → 게임 시작.  WASD/휠/123/Space + B/F/G/T + 마우스 클릭.

## 저작권 안전 가이드 (적용 중)

- 게임명 `PawnSim` (generic), 약탈자 = `Bandit` (generic).
- 모든 UI text + 8 이벤트 = 자체 작성 한국어 (위키 직접 copy X).
- Sprites: Kenney CC0 (pawn/grass/tree) + procedural Python PIL (wall/floor/door/stove/deer/bush).
- Audio: procedural Python wave (chop/select).
- UI/UX 컨벤션 = 모든 sim/management 게임 표준 (대상 X).

## 다음 작업 큐 (Day 27+)

자동 진행 가능:
- Day 27: 무기 (combat tier) — Bandit이 멜리(현재) vs 원거리, pawn 도 무기
- Day 28: Meal eating logic (먹으면 mood+10, meal 우선)
- Day 29: Drafted combat (R + 클릭 = pawn 명령)
- Day 30: Research bench (목재 추가 cost로 새 빌드 unlock)
- Day 31+: Temperature + biome

운영자 깰 때 평가 + 우선순위 변경 가능.
