# p1-storm-mood 재현 FAIL 캡처 (fix 전) — 2026-06-10

longplay-2026-06-10-cycle2 에서 발굴 (3일차 자정 전원 동시 정신붕괴, t=588→600 mood 59/35/13→0/0/0).
원인: PawnNeeds 폭풍 직접 드레인 -3/스케일초 (실측 -2.94/s 일치) + '야외 폭풍' thought 미배선.

```
[refactor] (4.4/7) fresh build → G:\ai\MelonS-Agents\skills\game-prototype\builds\_harness-latest ...
  fresh build OK
[repro] run day-X-2026-06-10/PawnSim.exe -repro p1-storm-mood.json (timeout 120s)
[repro] scenario 'P1 폭풍 mood — 폭풍 전체 노출에도 mood 폭주 하락이 없어야 한다 (longplay 2026-06-10 발굴: 직접드레인 -3/초×60초=-180 → 3일차 자정 전원 동시 정신붕괴. 기대 동작: '야외 폭풍' thought -6 + 자연 decay 만 = 60스케일초 하락 <20. hasThought 를 앞에 둬 -6 일회분이 drop 측정에 안 섞이게 함)' — 6 steps
  [ 0] PASS  wait                     waited 1.5s
  [ 1] PASS  shot                     G:\ai\_repro_shots\p1-storm-mood\00_start.png
  [ 2] PASS  setWeather               weather=storm
  [ 3] FAIL  assert:hasThought        no '야외 폭풍' in 5s (active: 좋은 옷차림,)
  [ 4] FAIL  assert:needDropsAtMost   mood 60.5→39.9 (drop 20.6, 허용 <20) in 7s(scaled)
  [ 5] PASS  shot                     G:\ai\_repro_shots\p1-storm-mood\01_storm_mood.png
[repro] OVERALL: FAIL (2 step)  shots=G:\ai\_repro_shots\p1-storm-mood

```
