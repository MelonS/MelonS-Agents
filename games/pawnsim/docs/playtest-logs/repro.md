# PawnSim Long-Play Survival Log — `repro`

- Generated: 2026-05-31 11:52:10
- Run duration: 40s real (3x game speed)
- Snapshots: 4 (every ~12s)
- In-game days reached: 6

## SURVIVAL VERDICT

**Colony survived: YES**

- Food trend: FALLING (-15) — sustainability risk
- Min food across run: 64 | Min sleep: 46 | Min mood: 52
- End-of-run avg food: 64 | avg sleep: 46
- Total invariant violations: 0

No issues found — colony sustained itself around the built house.

## PHASE 1 — House construction

- Topped up bank for build: wood=440 stone=200
- Wall ring: 14/14 segments built around (4,3)..(8,7)
- Door @ (6,3): built
- Beds: 2/2 built inside the house
- Stove @ (5,6): built
- Stockpile zone: 3 cells around (5..7, 5)

## PHASE 2 — Survival timeline

| t(s) | Day HH:MM | wood(Δ) | food(Δ) | meals(Δ) | stone(Δ) | pawn needs (food/sleep/mood · task) | violations |
|------|-----------|---------|---------|----------|----------|-------------------------------------|------------|
| 0 | 1 12:26 | 455(+0) | 10(+0) | 2(+0) | 200(+0) | 서연: 79/78/56 · 벌목<br>민지: 79/78/52 · 유휴<br>지훈: 79/78/55 · 벌목 | - |
| 12 | 3 00:26 | 498(+43) | 2(-8) | 10(+8) | 203(+3) | 서연: 74/67/56 · 사냥<br>민지: 74/67/52 · 사냥<br>지훈: 74/67/55 · 사냥 | - |
| 24 | 4 12:26 | 498(+0) | 2(+0) | 10(+0) | 203(+0) | 서연: 69/56/56 · 사냥<br>민지: 69/56/52 · 유휴<br>지훈: 69/56/55 · 유휴 | - |
| 36 | 6 00:26 | 502(+4) | 19(+17) | 31(+21) | 207(+4) | 서연: 64/46/56 · 운반<br>민지: 64/46/52 · 요리<br>지훈: 64/46/55 · 요리 | - |

## Screenshots

- t=0s -> `G:/ai/_longplay/00_0s.png`
- t=12s -> `G:/ai/_longplay/01_12s.png`
- t=24s -> `G:/ai/_longplay/02_24s.png`
- t=36s -> `G:/ai/_longplay/03_36s.png`
