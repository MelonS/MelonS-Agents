[repro] run day-X-2026-06-10/PawnSim.exe -repro p0-chop-menu.json (timeout 240s)
[repro] scenario 'P0 나무 클릭 → 벌목 메뉴 (운영자 2026-06-03 '여전히 없음' 재확인 건)' — 8 steps
  [ 0] PASS  wait                     waited 1.5s
  [ 1] PASS  shot                     G:\ai\_repro_shots\p0-chop-menu\00_start.png
  [ 2] PASS  worldclick               Lclick world(1.5,2.5) screen(1114,771) at=Tree_Oak_1_2
  [ 3] PASS  shot                     G:\ai\_repro_shots\p0-chop-menu\01_after_tree_click.png
  [ 4] PASS  assert:contextMenuOpen   contextMenu open=True (expect True)
  [ 5] PASS  clickui                  REAL click 'label:벌목' @ (254.29, 217.43)
  [ 6] PASS  shot                     G:\ai\_repro_shots\p0-chop-menu\02_after_chop_click.png
  [ 7] PASS  assert:chopDesignations  chop designations=1 (need ≥1)
[repro] OVERALL: PASS  shots=G:\ai\_repro_shots\p0-chop-menu
[repro] run day-X-2026-06-10/PawnSim.exe -repro p0-pawn-move.json (timeout 240s)
[repro] scenario 'P0 림 기본 이동 — 선택 후 우클릭 지점 도달 (운영자 '조작 안 되고 제자리 엉뚱한 짓')' — 8 steps
  [ 0] PASS  wait                     waited 1.5s
  [ 1] PASS  shot                     G:\ai\_repro_shots\p0-pawn-move\00_start.png
  [ 2] PASS  worldclick               Lclick world(1.5,-0.5) screen(1109,314) at=Pawn(Clone)
  [ 3] PASS  assert:selection         selection=민지 (expect any)
  [ 4] PASS  shot                     G:\ai\_repro_shots\p0-pawn-move\01_selected.png
  [ 5] PASS  worldright               Rclick world(3.5,-0.5) screen(1269,540) at=empty
  [ 6] PASS  assert:selectedNearClick selected '민지' closest 0.76 to rclick(3.5,-0.5) (need ≤0.8) in 0.3s
  [ 7] PASS  shot                     G:\ai\_repro_shots\p0-pawn-move\02_after_move.png
[repro] OVERALL: PASS  shots=G:\ai\_repro_shots\p0-pawn-move
[repro] run day-X-2026-06-10/PawnSim.exe -repro p0-remote-chop.json (timeout 120s)
[repro] scenario 'P0 원거리 벌목 — 림이 나무에 도달해서 베는가 (운영자 '도달 안 하고 제자리 벌목'. 거리 상한 2.0 = stand-cell 모델 정상범위: 대각 인접칸 중심 1.414 + 도착 허용오차 ~0.5. treeCountBelow 는 벌목 완료 전에 기준값을 찍도록 activityNearClick 통과 직후 배치)' — 9 steps
  [ 0] PASS  wait                     waited 1.5s
  [ 1] PASS  shot                     G:\ai\_repro_shots\p0-remote-chop\00_start.png
  [ 2] PASS  worldright               Rclick world(1.5,2.5) screen(1114,771) at=Tree_Oak_1_2
  [ 3] PASS  assert:chopDesignations  chop designations=1 (need ≥1)
  [ 4] PASS  assert:anyPawnActivity   '지훈' activity contains '벌목' at 0.8s
  [ 5] PASS  shot                     G:\ai\_repro_shots\p0-remote-chop\01_chopping.png
  [ 6] PASS  assert:activityNearClick closest-while-'벌목' 1.64 to rclick (need ≤2) in 0.0s
  [ 7] PASS  assert:treeCountBelow    trees 45→44 (need -1) in 5.5s
  [ 8] PASS  shot                     G:\ai\_repro_shots\p0-remote-chop\02_final.png
[repro] OVERALL: PASS  shots=G:\ai\_repro_shots\p0-remote-chop
[repro] run day-X-2026-06-10/PawnSim.exe -repro p1-mood-negative-direct.json (timeout 60s)
[repro] scenario 'P1 부정 thought 환류 (결정론) — sleep 20 세팅 → '수면 부족' thought + mood 하락. (food 대신 sleep 인 이유: 식량이 있으면 림이 food<40 에서 즉시 먹어 배고픔을 자가 해소하는 레이스가 있음. 침대는 시작 맵에 없어 수면 부족이 유지됨 — 배고픔과 같은 PawnThoughts.Update 환류 경로)' — 6 steps
  [ 0] PASS  wait                     waited 1.5s
  [ 1] PASS  shot                     G:\ai\_repro_shots\p1-mood-negative-direct\00_start.png
  [ 2] PASS  setNeed                  setNeed sleep=20 (서연)
  [ 3] PASS  assert:needDrops         mood 78.8→75.8 (drop 3.0, need ≥2.5) in 0s(scaled)
  [ 4] PASS  assert:hasThought        thought '수면 부족' present at 0s
  [ 5] PASS  shot                     G:\ai\_repro_shots\p1-mood-negative-direct\01_sleepy_thought.png
[repro] OVERALL: PASS  shots=G:\ai\_repro_shots\p1-mood-negative-direct
[repro] run day-X-2026-06-10/PawnSim.exe -repro p1-mood-negative.json (timeout 280s)
[repro] scenario 'P1 자연 플레이 — needs decay 작동(초반 단조구간 측정) + 식사 환류(배부름) + mood 자연 하강 (운영자 '게이지 안 줄어듦'/'기분 안 나빠짐' 회귀가드. needDrops 를 시작 직후에 두는 이유: food/sleep 80 에선 식사(<40)·수면붕괴가 불가능해 하락이 단조 — sawtooth 바닥(needBelow) 잡기는 식사 타이밍 운에 좌우돼 플래키)' — 9 steps
  [ 0] PASS  wait                     waited 1.5s
  [ 1] PASS  shot                     G:\ai\_repro_shots\p1-mood-negative\00_start.png
  [ 2] PASS  speed                    timeScale=4
  [ 3] PASS  assert:needDrops         food 79.5→74.5 (drop 5.0, need ≥5) in 38s(scaled)
  [ 4] PASS  assert:needDrops         sleep 74.9→64.9 (drop 10.0, need ≥10) in 83s(scaled)
  [ 5] PASS  assert:hasThought        thought '배부름' present at 104s
  [ 6] PASS  assert:needBelow         mood=63.9 (need ≤70) in 0s(scaled)
  [ 7] PASS  speed                    timeScale=1
  [ 8] PASS  shot                     G:\ai\_repro_shots\p1-mood-negative\01_natural_play.png
[repro] OVERALL: PASS  shots=G:\ai\_repro_shots\p1-mood-negative
[repro] run day-X-2026-06-10/PawnSim.exe -repro p1-storm-mood.json (timeout 120s)
[repro] scenario 'P1 폭풍 mood — 폭풍 전체 노출에도 mood 폭주 하락이 없어야 한다 (longplay 2026-06-10 발굴: 직접드레인 -3/초×60초=-180 → 3일차 자정 전원 동시 정신붕괴. 기대 동작: '야외 폭풍' thought -6 + 자연 decay 만 = 60스케일초 하락 <20. hasThought 를 앞에 둬 -6 일회분이 drop 측정에 안 섞이게 함)' — 6 steps
  [ 0] PASS  wait                     waited 1.5s
  [ 1] PASS  shot                     G:\ai\_repro_shots\p1-storm-mood\00_start.png
  [ 2] PASS  setWeather               weather=storm
  [ 3] PASS  assert:hasThought        thought '야외 폭풍' present at 0s
  [ 4] PASS  assert:needDropsAtMost   mood 68.8→71.9 (drop -3.1, 허용 <20) in 60s(scaled)
  [ 5] PASS  shot                     G:\ai\_repro_shots\p1-storm-mood\01_storm_mood.png
[repro] OVERALL: PASS  shots=G:\ai\_repro_shots\p1-storm-mood
[repro] run day-X-2026-06-10/PawnSim.exe -repro p1-wood-durability.json (timeout 120s)
[repro] scenario 'P1 통나무 내구도 — 옥외 더미가 닳아 흐려지다 소멸하는가 (운영자 '그건 안 됐다')' — 8 steps
  [ 0] PASS  wait                     waited 1.5s
  [ 1] PASS  shot                     G:\ai\_repro_shots\p1-wood-durability\00_start.png
  [ 2] PASS  speed                    timeScale=8
  [ 3] PASS  assert:pileDurabilityDrops durability 98.4→88.2 (drop 10.1, need ≥10) in 24s(scaled)
  [ 4] PASS  shot                     G:\ai\_repro_shots\p1-wood-durability\01_decaying_faded.png
  [ 5] PASS  assert:pileDurabilityDrops pile destroyed at 210s(scaled), durability 88.1→0.0 (소진 소멸)
  [ 6] PASS  speed                    timeScale=1
  [ 7] PASS  shot                     G:\ai\_repro_shots\p1-wood-durability\02_pile_gone.png
[repro] OVERALL: PASS  shots=G:\ai\_repro_shots\p1-wood-durability
[refactor] (4.4/7) fresh build → G:\ai\MelonS-Agents\skills\game-prototype\builds\_harness-latest ...
  fresh build OK

[repro_all] ── p0-chop-menu.json (timeout 240s) ────────────────────

[repro_all] ── p0-pawn-move.json (timeout 240s) ────────────────────

[repro_all] ── p0-remote-chop.json (timeout 120s) ────────────────────

[repro_all] ── p1-mood-negative-direct.json (timeout 60s) ────────────────────

[repro_all] ── p1-mood-negative.json (timeout 280s) ────────────────────

[repro_all] ── p1-storm-mood.json (timeout 120s) ────────────────────

[repro_all] ── p1-wood-durability.json (timeout 120s) ────────────────────

[repro_all] ━━━ 요약 ━━━
  PASS  p0-chop-menu.json
  PASS  p0-pawn-move.json
  PASS  p0-remote-chop.json
  PASS  p1-mood-negative-direct.json
  PASS  p1-mood-negative.json
  PASS  p1-storm-mood.json
  PASS  p1-wood-durability.json
[repro_all] OVERALL: PASS
