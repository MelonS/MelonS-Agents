# PawnSim Long-Play Survival Log — `qfx`

- Generated: 2026-05-31 02:30:52
- Run duration: 150s real (3x game speed)
- Snapshots: 13 (every ~12s)
- In-game days reached: 19

## SURVIVAL VERDICT

**Colony survived: YES**

- Food trend: FALLING (-40) — sustainability risk
- Min food across run: 19 | Min sleep: 0 | Min mood: 49
- End-of-run avg food: 29 | avg sleep: 33
- Total invariant violations: 8

### Issues found
- SLEEP-CRASH: a pawn hit sleep=0
- 8 invariant violation(s) across the timeline

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
| 0 | 1 12:50 | 440(+0) | 10(+0) | 2(+0) | 200(+0) | 지훈: 79/78/56 · 채광<br>민지: 79/78/52 · 채광<br>서연: 79/78/52 · 채광 | - |
| 12 | 3 00:47 | 445(+5) | 12(+2) | 12(+10) | 214(+14) | 지훈: 74/67/56 · 채광<br>민지: 74/67/52 · 요리<br>서연: 74/67/52 · 요리 | - |
| 24 | 4 12:50 | 445(+0) | 0(-12) | 16(+4) | 214(+0) | 지훈: 69/56/56 · 사냥<br>민지: 69/56/52 · 사냥<br>서연: 69/56/52 · 사냥 | - |
| 36 | 6 00:50 | 445(+0) | 20(+20) | 40(+24) | 216(+2) | 지훈: 64/46/56 · 운반<br>민지: 64/46/52 · 요리<br>서연: 64/46/52 · 요리 | - |
| 48 | 7 12:50 | 450(+5) | 0(-20) | 58(+18) | 216(+0) | 지훈: 59/35/53 · 채광<br>민지: 59/35/49 · 치료<br>서연: 59/35/49 · 유휴 | - |
| 60 | 9 00:50 | 450(+0) | 8(+8) | 58(+0) | 216(+0) | 지훈: 54/24/56 · 이동<br>민지: 55/76/54 · 유휴<br>서연: 54/24/55 · 이동 | - |
| 72 | 10 12:50 | 455(+5) | 2(-6) | 67(+9) | 216(+0) | 지훈: 49/13/56 · 휴식이동<br>민지: 80/65/60 · 유휴<br>서연: 49/87/55 · 이동 | - |
| 84 | 12 00:50 | 460(+5) | 9(+7) | 70(+3) | 216(+0) | 지훈: 44/2/56 · 휴식이동<br>민지: 75/54/60 · 요리<br>서연: 44/76/55 · 이동 | - |
| 96 | 13 12:50 | 460(+0) | 2(-7) | 83(+13) | 216(+0) | 지훈: 39/0/56 · 휴식이동<br>민지: 70/44/60 · 수확<br>서연: 39/65/55 · 이동 | - |
| 108 | 15 00:54 | 463(+3) | 0(-2) | 89(+6) | 216(+0) | 지훈: 34/0/56 · 휴식이동 ⏳<br>민지: 65/33/60 · 이동<br>서연: 34/55/55 · 이동 ⏳ | PAWN-STUCK: 지훈 task='휴식이동' no-move 36s<br>PAWN-STUCK: 서연 task='이동' no-move 36s |
| 120 | 16 12:54 | 463(+0) | 0(+0) | 89(+0) | 216(+0) | 지훈: 29/0/56 · 휴식이동 ⏳<br>민지: 60/22/60 · 이동<br>서연: 29/44/55 · 이동 ⏳ | PAWN-STUCK: 지훈 task='휴식이동' no-move 48s<br>PAWN-STUCK: 서연 task='이동' no-move 48s |
| 132 | 18 00:54 | 463(+0) | 1(+1) | 94(+5) | 216(+0) | 지훈: 24/0/59 · 휴식이동 ⏳<br>민지: 55/88/60 · 수확<br>서연: 24/33/55 · 이동 ⏳ | PAWN-STUCK: 지훈 task='휴식이동' no-move 60s<br>PAWN-STUCK: 서연 task='이동' no-move 60s |
| 144 | 19 12:54 | 463(+0) | 13(+12) | 106(+12) | 216(+0) | 지훈: 19/0/59 · 휴식이동 ⏳<br>민지: 50/77/58 · 요리<br>서연: 19/22/55 · 이동 ⏳ | PAWN-STUCK: 지훈 task='휴식이동' no-move 72s<br>PAWN-STUCK: 서연 task='이동' no-move 72s |

## Screenshots

- t=0s -> `G:/ai/_longplay/00_0s.png`
- t=12s -> `G:/ai/_longplay/01_12s.png`
- t=24s -> `G:/ai/_longplay/02_24s.png`
- t=36s -> `G:/ai/_longplay/03_36s.png`
- t=48s -> `G:/ai/_longplay/04_48s.png`
- t=60s -> `G:/ai/_longplay/05_60s.png`
- t=72s -> `G:/ai/_longplay/06_72s.png`
- t=84s -> `G:/ai/_longplay/07_84s.png`
- t=96s -> `G:/ai/_longplay/08_96s.png`
- t=108s -> `G:/ai/_longplay/09_108s.png`
- t=120s -> `G:/ai/_longplay/10_120s.png`
- t=132s -> `G:/ai/_longplay/11_132s.png`
- t=144s -> `G:/ai/_longplay/12_144s.png`
