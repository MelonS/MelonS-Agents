# 자율작업 세션 요약 (2026-05-27 ~ 후속)

---

## [세션 2026-05-30~31 라이브+야간] 운영자 라이브 피드백 폭주 + 자율 처리 (75 commit)

운영자가 빌드 돌려보며 실시간 피드백을 쏟아냄. 멀티에이전트 병렬(파일 lane 분리)
+ Unity 직렬 검증 + strict LongPlay QA 로 처리. 핵심 교훈 2개: (1) ★대충/hack
금지([[no-sloppy-shortcuts]]) — 영상 녹화를 PNG-frame timelapse hack 으로 때우려다
거부당함, 표준 Unity Recorder 로 제대로. (2) ★auto-QA 관대함 — verify 가
survived=YES/“pre-existing” 만 보고 회귀(PAWN-STUCK, integration 39/42)를 통과시킨
사례 반복 → 내가 직접 플레이로그/프레임 판정으로 정정.

### 처리한 운영자 피드백 (전부 main + 검증)
- 8버그(우클릭컨텍스트/목재드롭/문모션/이동경로/시간/광맥/사망비주얼/늑대제거)
- 생존 루프: 식량생산(HarvestCrop AI), raid calibration(survivable), 침대 자율취침
  (stuck 회귀 fix), 아이템 물리운반(teleport 제거 — 림이 직접 carry)
- UI 전면 재구성: 콜로니심식 Architect 메뉴(Orders/Zone/… 카테고리, raycast-click
  버그 fix — UITheme fill.raycastTarget=false 가 원인), 인스펙터 통합+선택구동,
  설정 메뉴(Save/Load+SFX/BGM 볼륨+PlayerPrefs), 건축버튼 툴팁/아이콘
- 영역지정: 저장존 드래그+아이템필터+폐기존 / 지붕 기능+지붕영역 메뉴(그늘 오버레이)
- 전투 lunge 모션 / 적·동물 벽통과 fix / 기능없는 장식 제거
- ★녹화 기능(표준): Unity Recorder 5.1.0 + record-gameplay.py — 1x/줌12/오디오
  mp4 직접. 집(침대2+화덕+저장존) 완전판. 재사용.
- integration 회귀 정정: Chop/Mine 우선순위(자원축적 복구), I2 stall, I6 stale → 42/42

### 검증 인프라(재사용)
- LongPlaySurvivalRunner(-longplay): 집건축→5분+생존→플레이로그(docs/playtest-logs/)
  + 불변식(exception/시간/벽통과/stuck). 매 wave strict 판정.
- record-gameplay.py(-record): Unity Recorder 실시간 1x 영상.

### 현재 상태 / honest 결론
- day-final-2026-05-31: isolated 76/76, integration 42/42, Build Click 9/9, LongPlay
  생존(자원축적 복구 후), 0 불변식위반. 운영자 피드백 전부 반영.
- ★non-gated 그래픽/UI/사운드 표면 소진(사운드92/그래픽85/UI88) — 더 cosmetic =
  busywork 라 grind 안 함.

### ⚠️ 운영자 결정 / flagged
1. ★다음 도약 = OP-gated 티어(mood thought-sum+3tier break / work-priority grid /
   terrain move-cost) — “보이는 the reference sim”→“플레이되는 the reference sim”. 승인 필요(스펙 작성됨).
2. late-game 자원 고갈 — base 근처 나무/광맥/동물 소진 + 먼 자원 path-fail(give-up)
   → 장기 생산 plateau(생존은 surplus 로 유지). edge 밸런스, 별도 wave 후보.
3. 게임 “1x” 속도 = 60 game-min/real-sec(하루 24초)로 빠름 — 자연화(예 하루 1~4분)
   할지. needs decay 가 실초 기준이라 늦추면 밸런스 재튜닝 동반.

---

## [세션 2026-05-30 야간] 멀티에이전트 오케스트레이션 + 위키 비교 구동 자율 체인 (48 commit)

운영자 directive: 멀티에이전트 파이프라인을 "연쇄반응(chain reaction)"으로 — 수동
개입 없이 PM 발행→병렬 제작→직렬 Unity 검증→commit/자동롤백 루프. + the reference sim 위키
(장르 위키) 전면 비교분석 구동. + 취침 중 마일스톤 추가하며 아침까지 자율 루프.

### 한 줄
**Workflow 엔진으로 자율 연쇄체인 3라운드(cx 6wave + cy 4wave + cz 4wave) 전부 GREEN,
롤백 0, 48 commit. 위키 비교 v1/v2 가 백로그 구동. 차원별 근접도: Design 70→80 /
Sound 30→80 / Move 75→82 / Build 65→80 / Gameplay 80→85 / UI 70→82. non-gated
표면 소진 → 다음 도약은 OP-gated(운영자 결정).**

### 아키텍처 (운영자 설계 채택)
- `.claude/whiteboard.json` + `.claude/wb/<role>.json` — 에이전트 컨텍스트 유지(병렬
  write 안전: per-role 파일, PM 단일 병합). `_pawnsim_chain_x.js` — 연쇄체인 스크립트.
- 파이프라인: PM(위키 gap+MILESTONES+git log 읽고 비충돌·비게이트 서브태스크 발행)
  → 병렬 메이커(코드/아트/사운드, Unity 미실행) → 직렬 QA+integrator(wip 브랜치 보존
  → `refactor_check --fresh-build` → GREEN이면 merge→main+push, RED이면 main 자동롤백
  +wip 보존+wb/qa.json 버그리포트) → 다음 wave 자동.
- Unity batchmode 단일 배타 → 검증만 직렬, 제작 병렬. `--fresh-build`로 stale-build
  맹점 해소 (이전 세션 #210 에서 발견).

### 누적 ship (차원별, 전부 검증·main)
- **사운드(최대 gap)**: sfxBuild/Alert(tier)/Mine/UI-click/door/cook/shoot/footstep,
  wolf-howl 배선(호출처 0이었음), ambient bed + 야간 변주, 동적 음악, 비/날씨 파티클,
  AnimalEntity 전투 PlayChop→PlayHit 오용 fix.
- **이동/비주얼**: pawn facing(flipX), walk-bob+idle-breathe, tree-sway, sleep-pose,
  carry-pose+attack-lunge, 야간 light-pools, scatter 데코+variety(magenta fix), vignette.
- **건축(#15-21)**: deconstruct, mine/grow-zone designation, drag-rect 배치, area-cancel,
  standing-lamp/torch, fence+gate, barricade, autodoor, table+chair, stone-floor.
- **UI**: top-right alert/letter stack(클릭→카메라 pan), inspector 탭, 멀티셀렉트 마키,
  gizmo 커맨드바, 플로팅 전투/작업 텍스트, hotkey overlay.
- **검증(continuous gate)**: V1-V16 시나리오 + raid-threat + moveSpeed reconcile +
  save-load round-trip + serialization fix + substate. 매 wave isolated/integration/
  build-click/real-qa/visual-diff 통과.

### 운영자 결정 대기 (다음 도약 — 전부 OP-gated, 스펙 작성됨)
1. **Mood thought-sum + 3-tier mental-break** (`docs/spec-needs-mood-balance.md`) —
   현재 mood free-fall 버그성 모델. 진짜 the reference sim 기분 시스템으로.
2. **Work-priority grid** (`docs/spec-work-priority.md`) — 직업 우선순위 1-4 그리드.
   "운반 우선순위" 운영자 관심사 직결. 현재 WorkKind collapse 로 Haul 개별제어 불가.
3. **Terrain move-cost** — 지형별 이동비용.
> 이 셋이 "보이고 들리는 the reference sim" → "플레이되는 the reference sim" 도약. 승인 주시면 같은
> 연쇄체인으로 진행. over-scope(roofs/temperature/joy/hediffs/cover/14-skill)는 가드 유지.

### 최종 빌드
`skills/game-prototype/builds/day-final-2026-05-30/PawnSim.exe` — 누적 전부 반영
(스크린샷 `G:/ai/_final_check.png`: 정착지/콜로니스트/횃불/울타리/작물/UI 전부 정상,
매지나·debug박스·회귀 0). 비교: `docs/genre-comparison.md`(v1) + `-v2.md`.

---

## [세션 2026-05-29 야간 2부] #201-#208 — 림 갇힘 fix + 디자인/UI 전면 개편 (자율 5h+)

운영자 directive: (1) "건축 완료 시점 림이 있으면 고정되서 못움직임" 버그 fix.
(2) "디자인+ui개선 계속... 아직 너무 별로", "기획자 선정 → 아트 → QA 파이프라인",
"자리 비울거 같으니 자율로 5시간 이상".

### 한 줄
**#201 벽-갇힘 버그 fix (eject + 상시 안전망) + #202-208 디자인/UI 전면 개편 —
game-director(백로그) → game-artist(아트) → game-programmer(와이어링) → game-qa
파이프라인으로 백로그(A1-A10, U1-U9) 전 항목 완료. 최종 QA: 시각 폴리시
~3/10 → ~6.5-7/10, "너무 별로" bar 통과.**

### #201 벽-갇힘 버그 (`c5e9e22`)
원인: 셀 blocked 등록 시 eject 부재 + 탈출이 SetTarget 시만 발동(idle 림 영구
갇힘). fix: WallEntity.Start 가 EjectPawnsFromCell 호출(the reference sim push-out) +
PawnMovement.Update 상시 안전망(blocked 셀이면 nearest walkable 로). I42 가
fix off=FAIL/on=PASS 로 버그 재현. V76.

### #202-208 디자인/UI 개편 (game-director 진단: "스타일 내전")
진단: 생성기 2종이 상충 팔레트(디테일/네온 vs flat-Kenney) → "네온 배 갈색 감자"
콜로니스트가 과채도 잔디에 묻힘. UI 는 검정 debug 박스.

| commit | 내용 |
|--------|------|
| `e558ef3` #202 | A1 palette.py 단일 소스 + 생성기 통합 / A2 콜로니스트 flat+2px 외곽선 + 3 변형(청/적갈/올리브) 와이어링 |
| `2c84294` #203 | A3 지형 타일 muted(grass 네온→올리브) + door 정합 |
| `2c1467c` #204 | U1-U3 UI 패널 시스템 통일(MakeBorderedPanel) - 컨트롤바/툴팁/인스펙터/이름표/플로팅바 |
| `3d06c4f` #205 | U4/U7/U8 - 상단바/ArchitectMenu/튜토리얼/SL 버튼 전부 통일 |
| `b2ad22b` #206 | 자원 아이콘 4종 + lamp/berry/marker/flower/arrow 네온 제거 |
| `558585f` #207 | A4 목재 구조물 cohesion(floor 한 단계 darker) + A8 작물 + A9 드롭 shadow |
| `46ed505` #208 | 상단 자원 아이콘 가독성(24→36px, 텍스트 밀착) |

매 commit harness PASS, 매 단계 스크린샷 검증. 최종 누적 QA(game-qa):
isolated 76/76 / integration 42/42 / Build Click 9/9 / REAL QA wood +55 /
GATE GREEN. unified 팔레트로 terrain+pawn+building+UI 한 스타일.

### ⚠ 운영자용 메모
- **KlingAI 7.4k 포인트**: 인게임 픽셀 스프라이트/UI 엔 부적합(cohesion 깨짐).
  단, **메인메뉴 배경 키아트** 한 장엔 적합 — 원하면 프롬프트 제공. 뽑아서
  `Assets/Sprites/menu_bg.png` 넣어주면 programmer 가 와이어링.
- 남은 diminishing-returns 항목(QA): 작물 필드 마커 soil patch, 인스펙터 빈 상태.

---

## [세션 2026-05-29 야간] #199+#200 — the reference sim 풀 그리드 정합 + 충실도 감사/튜닝

운영자 directive (자기 전): "최대한 레퍼런스 콜로니심와 같은 방식으로 처리하도록" +
"위키에 다른 카테고리들 알아서 찾아서 현재 구현된것들 문제 있는지 체크하면서 개선해".
앞선 발화: 림 사이즈 1x1 맞는지 위키 확인 + 건축/이동/액션 잘 처리.

### 한 줄
**위키 확정(림=1x1, 침대=1x2) → A+B+C 풀 그리드 정합 (#199, 5 commit) =
point-자유이동을 the reference sim 그리드 A* 경로탐색으로 전면 전환 + 충실도 감사 →
밸런스 튜닝 5건 (#200). 전부 game-pm→programmer→qa 파이프라인 + 매 단계 harness PASS.**

### #199 — the reference sim 풀 그리드 정합 (A+B+C, 9 step → 4 commit)
위키: 림(pawn)=1x1, 침대=1x2 (침대는 이미 정합이었음). 감사로 진짜 문제 3개 발견:
림 2x2 선택 collider / point-기반 자유 lerp (경로탐색 없음) / 칸 점유·예약 없음.

| commit | 내용 |
|--------|------|
| `61dd2e7` #199-A | 림 1x1 (collider fix, "2x2"는 stale 주석이었음) + 카메라 ortho 6→3.5 + 플로팅 UI 재배치. V66/V67 |
| `3b2c373` #199-B core | PathGrid+AStar (8방향, cost10/14, octile, corner-cut 금지, cap 4000) → flag ON 컷오버 → I19 nudge/axis-slide hack 삭제 → WorkGiveUp helper (LastPathFailed+무진전) 10 worker 적용. V68-72 |
| `c8aa5ca` #199-B3 | 벽 경로 차단(ref-count registry) + 문 통과 + PathGrid.Version in-flight 무효화(걷던 림이 새 벽 안 뚫음). I38/I39 |
| `7271079` #199-C | 인접 작업칸(TryGetAdjacentStandCell, range→1.5) + ReservationManager(중복 점유 방지) + 건축 배치 검증(물/바위/점유 reject + 토스트). V73-75/I40/I41. 부수 flaky I2/I25 fix |

검증: isolated 65→75, integration 37→41. 매 phase game-qa milestone gate PASS.
효과: 3 림이 서로 다른 나무 벌목 (totalPawnMove 6.9→26.6), 벽 우회, 대상 옆에 섬.

### #200 — the reference sim 값 충실도 감사 + 튜닝 (`ad6326f`)
감사 리포트 `docs/audit-genre-fidelity-2026-05-29.md` (~28 시스템 vs 위키).
Top-5 적용: 식량감소 0.5→0.14(굶주림 0.83일→3일, prefab baked값도), 이동 3.0→4.6
+ 늑대 chase 5.0(scene baked 3마리도), head HP 10→20/torso 30→40, 활 설명 정합,
스킬 XP base ×10. 거짓양성 테스트 2건(V59/I3) 적발·강화.

### ⚠ 운영자 검토 대기 (자율로 안 건드림 — 값 추정 or 로직변경)
1. **head HP 20** — 추정치 (위키 head≈25 미확인). SerializeField 조정 가능.
2. **화살 데미지 vs 설명** — 설명을 "3~5 dmg"로 교정함(전투 재밸런스 회피).
   화살을 실제로 더 세게 하려면 전 적 HP 재스케일 = 별도 작업.
3. **mood free-fall + mental-break 3-tier** — 감사 MED. 행동 로직 변경이라
   §5 logic-change 규칙상 운영자 OK 후 진행 (이번엔 보류).

---

## [세션 2026-05-29] #198 — 멀티-에이전트 파이프라인 강제 (QA / 건축 fix / 디자인)

운영자 directive: **"QA / 건축 fix / 디자인 / 파이프라인. 파이프라인 =
`.claude/agents/` 의 game-pm/programmer/qa 직군 invoke 패턴 강제 적용."**

### 한 줄
**game-pm(계획) → game-programmer(건축 fix) → game-artist(bed_fine sprite) →
game-programmer(wiring) → game-qa(게이트) 파이프라인으로 실행.  QA baseline 이
2개 실결함 발견 (Build Click QA prefix mismatch + 시각 baseline 해상도 staleness)
→ fix → bed_fine.png 신규 + Fine 침대 sprite-swap wiring → #198 PASS.**

### 파이프라인 흐름 (직군별 invoke)
| 단계 | 직군 | 산출 |
|------|------|------|
| 계획 | game-pm | `docs/plan-198.md` — 보수적 scope (상위 2개), binary 합격 기준, QA 게이트 |
| QA baseline | (orchestrator) | refactor_check `baseline-198` → **실결함 2건 발견** |
| 건축 fix | game-programmer | Build Click QA 가 안 돌던 root cause = 로그 prefix `[BuildClickQA-v2]` ↔ 하니스 grep `[BuildClickQA]` mismatch.  rename fix + bed SpriteRef null-guard + V65 추가 (64→65) |
| 디자인 (art) | game-artist | `bed_fine.png` 16x32 (royal-blue/gold, 골드 헤드보드) — wood 의 red/brown 과 명확히 구분.  `_gen_bed.py` 에 `gen_bed_fine()` 추가 |
| 디자인 (wiring) | game-programmer | `BedEntity.SetQuality` sprite-swap (Fine→bed_fine+white, Wood/Spot→bed_wood+tint), `SceneSetup.Game.Prefabs` bedFineSprite bake, ForceImportAllSprites 등록 |
| 게이트 | game-qa | full harness `--tag 198 --accept-visual` → **PASS** |

### 검증 (game-qa, 정직)
- build OK / runtime error 0 / REAL QA wood +47
- **Build Click QA `OVERALL: PASS` 8/8** (이전엔 silently skip — 이제 진짜 게이트됨)
- isolated **65/65** (V65 신규, V56/V60 회귀 0) / integration **37/37** (I36 고급침대 restMul=1.40)
- visual diff 0.42% (신규 bed sprite 의도 변경 → baseline 재수용)
- PNG Read: 월드 정상 렌더 + bed_wood vs bed_fine 시각 구분 확인

### 발견된 교훈
- **"코드 있음 != 검증됨" 의 메타 버전**: 검증 하니스 자체도 검증돼야 한다.
  Build Click QA 는 코드가 멀쩡히 돌고 `OVERALL: PASS` 를 찍고 있었지만, 로그
  prefix 한 글자(`-v2`) 차이로 하니스가 결과를 못 읽어 **수개월간 silently skip**.
  baseline QA 를 정직하게 돌린 게 이걸 잡았다.
- 시각 baseline 은 해상도가 바뀌면(2858x1481 → 3840x2160) size-mismatch 로
  무조건 FAIL — `--accept-visual` 재수용 필요.

### 다음 increment (#199 후보, plan-198 §6)
- SaveLoadManager fidelity (BedQuality/StockpilePriority/TreeSpecies/WallMaterial 직렬화 + V-scenario)
- P7 combat 시퀀스 screenshot
- Stretch (power/trading/taming/bills) — 운영자 명시 지시 전까지 보류

---

## [후속 세션 2026-05-27 (운영자 자는 동안)] — UX 폴리시 + 키보드 의존 제거

운영자 깨기 직전 발화: **"디자인 구리고 프로토타입 수준도 안되고 ui들 그냥
표시만해주는 키보드 의존도 너무 높음. gui가 전혀 되질 않음. 사람의 이동도
안됨 여전히. 일단 자러가니깐 계속 자율로 작업하도록. 스스로 검증할 방법을
찾고 계속해서 퀄리티 업글을 시켜. 기능추가는 보수적으로 게임이 되는게 먼저임."**

### 한 줄
**4 운영자 불만 해결 + integration 5→23 시나리오 + 진짜 movement 버그 fix + SceneSetup 1057→310L (-70.7%) + AI give-up safety + 위협 알림 + build warning 0 + FindObjects per-Update 캐시 5건 + 49+ commit.**

### 운영자 불만 → 대응

| 불만 | 대응 commit |
|------|-------------|
| "사람 이동 안됨 여전히" | `ef75182` UI 가로채기 EventSystem 차단 + ClickEffect X 마커 |
| "gui 전혀 안됨, 키보드 의존" | `7765749` GuiControlBar 10 버튼 (멈춤/1x/2x/4x/징집/벽/바닥/문/화덕/연구) |
| "프로토타입 수준도 안됨" (1) | `fb4fee1` SelectionRing(노란 펄스) + starter 자원 + 튜토리얼 9→3 압축 |
| "프로토타입 수준도 안됨" (2) | `585090f` camera ortho 10→6 (pawn 디테일 보임) + 위치 (0.5,1.0) |
| "프로토타입 수준도 안됨" (3) | `b74a6f5` HoverTooltip - 14 종 entity hover 시 한국어 설명 |
| 빌드 모드 우클릭 race | `e6dd403` 빌드 활성 시 ClickSelector 좌/우 클릭 차단 |
| 이름/상태/HP 안 보임 | `ec6db96` zoom 6 기준 라벨/바 사이즈 키움 + 상태 라인 (벌목/이동/...) |
| refactor_check 자동화 | `576e5b2` integration 도 매 commit 자동 실행 (step 7/7) |
| GUI 벽 버튼 검증 + 인코딩 | `53c3e41` I17 wall button + cp949 console fix |
| pawn 화면 밖 자주 나감 | `f059417` 선택 시 카메라 부드러운 focus (0.6s) |
| batchmode lerp 안 수렴 | `47eb2fe` MoveTowards 고정 30u/s + I18 camera focus 검증 |
| **★ 진짜 movement 버그 발견** | `0242436` PawnMovement.SetTarget ClampToWorld + unstuck nudge + PawnChopper give-up 10s |

### ★ I19 발견된 진짜 movement bug

운영자 자기 전 "사람의 이동도 안됨 여전히" 의 진짜 원인:

1. **Tree 가 world bound (±19) 밖에 spawn** 가능 (SceneSetup 의 randomization).
2. PawnChopper.SetTreeTarget(tree.position) → PawnMovement.target = (-20.4, ...).
3. **기존 SetTarget 은 clamp 안 함** → pawn (-19, ...) 에 도달, 목표 (-20.4) 못 감.
4. PawnChopper.Update 매 프레임 SetTarget 재호출 → 영원히 stuck.
5. corner pawn 은 한 번 stuck 되면 inner 로도 못 빠져나옴.

**증거**: I19 test (chop 완성 검증) 처음 도입 → wood 40→40, 0 unit moved. 디버그 로그로 stuck 위치 + target 외부 좌표 확인.

**수정 (3-layer 안전망)**:
- `PawnMovement.SetTarget`: ClampToWorld 강제 (target 항상 reachable).
- `PawnMovement.Update`: 1.5s 안 움직였으면 perpendicular nudge 0.6 unit (3s cooldown).
- `PawnChopper.Update`: 10s 동안 in-range 못 가면 ClearTask (영원 stuck 방지).

검증: I19 fresh pawn (5,5) spawn → tree 우클릭 → 15s 안에 pawn 6.71 이동 + tree 1개 destroyed + wood 40→45.

운영자 자기 전 "이동 안됨" 불만은 이거. UI 가로채기 fix (`ef75182`) 만으로 부족했음.

### 추가 통합 시나리오 I19-I21

- I19 chop end-to-end (fresh pawn / 15s / wood up)
- I20 crop 수확 (growth=1 강제 → 우클릭 → food +5)
- I21 drafted vs wolf (옆에 wolf spawn → 5s 안에 HP 0)

21/21 integration PASS.

### R10 SceneSetup partial 추가 분할

매 sub-step 풀 cycle PASS:
- **R10** (settlement): `76737a9` Day 57 정착지 블록 (벽/바닥/스토브/벤치/crops/lamp/stockpile) → `SceneSetup.Game.Settlement.cs` (100L extract)
- **R10b** (wildlife): `4d9b9ec` Wolf 2 + Deer 8 spawn → `SceneSetup.Game.Wildlife.cs` (40L extract)
- **R10c** (prefabs): `d916a00` 6 prefab (Tree/Wall/Floor/Door/Stove/Bench) 생성 → `SceneSetup.Game.Prefabs.cs` (61L extract)
- **R10d** (event log): `273e3bb` EventLog panel → `SceneSetup.Game.EventLog.cs` (36L extract)
- **R10e** (tutorial): `e2d619a` Tutorial overlay UI → `SceneSetup.Game.Tutorial.cs` (33L extract)
- **R10f** (save/hint): `d11d419` SaveLoad btn + ControlHint → `SceneSetup.Game.SaveHint.cs` (36L extract)
- **R10g** (research): `779e5d8` Research strip + popup picker → `SceneSetup.Game.Research.cs` (91L extract)
- **R10h** (pawn info): `2ea7bf5` PawnInfoPanel + health text → `SceneSetup.Game.PawnInfo.cs` (78L extract)
- **R10i** (skill panel): `60a3107` SkillPanel (채집/벌목/건축/전투) → `SceneSetup.Game.SkillPanel.cs` (42L extract)
- **R10j** (top bar): `7c9890c` TopBar (clock+speed+resources) → `SceneSetup.Game.TopBar.cs` (143L extract, 최대)
- **R10k** (audio): `9aeb36e` AudioBank wiring → `SceneSetup.Game.Audio.cs` (25L extract)
- **R10l** (trees): `16014f0` Tree spawn (20 deterministic) → `SceneSetup.Game.Trees.cs` (33L extract)
- **R10m** (berry): `cfa8546` BerryBush spawn → `SceneSetup.Game.BerryBush.cs` (30L extract)

**SceneSetup.cs 1057L → 310L (-747L, -70.7%)**.  14 partial file.  매 sub-commit 통합 22 + isolated 55 풀 cycle PASS.

### I22 SaveLoad 통합 검증

`de85438` V34 isolated 가 SaveData.wood 만 검증.  I22 는 실제 spawn 된 3 pawn
+ ~20 tree 까지 진짜 게임 상태에서 Save → 자원 dirty → Load → 복원 확인 (PASS).

### I23 60초 stress (4x speed = 4분 game) + 안전성

`5505a68` Application.logMessageReceived 후킹 - 60s 시뮬 exception 0회 검증.
이제 매 commit 검증 사이클이 실제 4분 게임플레이 포함.

### 추가 안전망

- `02cf1a1` AI Strategy actions (FindNearestX) world bound (±18.5) 필터 - unreachable target pick 금지
- `de7360a` PawnHunter/Gatherer give-up 15s/10s (PawnChopper 와 같은 패턴)
- `86cc4d9` PawnCook give-up 10s - 4 worker 모두 unreachable stuck 방어 완성
- `bec88ce` ThreatAlertUI - wolf 5u 또는 bandit 8u 접근 시 빨강 ⚠ 알림 (auto-pause X, 시각 only)
- `39dfde5` API 현대화 - FindObjectsOfType → FindObjectsByType 8 파일, build warning 0
- `706350e` V55 trader flakiness fix (3s 대기, 0.05 threshold)
- `16287dc` + `9b37bf2` ResearchManager/Bench - FindObjectsByType per-Update → 1s 캐시 (60x 호출 감소)
- `01e1fe9` SelectionRing + GuiControlBar - FindFirstObjectByType per-Update 캐시

### 검증

- **isolated**: 55/55 PASS (변동 X)
- **integration (Game.unity 실 spawn 위)**: 5 → 18 시나리오 PASS
  - I6-I10 GUI 버튼 (bar 생성/멈춤/4x/벽/징집)
  - I11 ScreenToWorld round-trip + OverlapPoint (err=0.0000, hitsPawn=True)
  - I12 SelectionRing 생성 + 선택 따라옴 (alpha>0.6)
  - I13 starter 자원 (wood=40, food+meals>=3)
  - I14 HoverTooltip MonoBehaviour 1 ea
  - I15 BuildManager mode toggle
  - I16 사용자 smoke - pawn 선택→tree 우클릭→PawnChopper.HasTask=True
  - I17 GUI 벽 버튼 → BuildManager mode
  - I18 select pawn → 카메라 그쪽으로 pan (>0.5 unit moved)
- 매 commit 마다 `refactor_check.py` 자동 실행 (isolated + integration 둘 다)
- 한 사이클 ~110s (이전 80s, integration step 추가)

### 신규 컴포넌트

- `ClickEffect.cs` (X 마커 0.6s fade)
- `GuiControlBar.cs` (10 버튼 self-bootstrap)
- `SelectionRing.cs` (yellow pulse, drafted 면 cyan)
- `HoverTooltip.cs` (14 entity 한국어 설명)
- `IntegrationTestRunner.cs` I6-I16 (11 시나리오 추가)

### 파일 변경 요약

```
신규: ClickEffect / GuiControlBar / SelectionRing / HoverTooltip (4 컴포넌트)
수정: ClickSelector / BuildManager / ResearchUI / GameManager
       / TutorialOverlay / PawnNameLabel / PawnFloatingBars
       / SceneSetup.Game.Core (camera ortho/pos)
       / IntegrationTestRunner (I6-I16)
```

### 깨어났을 때 바로 보면 좋은 것

1. **`G:/ai/_refactor_baseline.png`** — 새 baseline (zoom 6, GUI 버튼, 이름 라벨 visible)
2. **integration 결과**: `G:/ai/_pawnsim_integration_report.json` (16/16 PASS)
3. 화면 하단 [멈춤][1x][2x][4x][징집][벽][바닥][문][화덕][연구] 버튼 클릭 가능
4. pawn hover 시 한국어 tooltip
5. 콜로니스트 선택 시 발밑 노란 펄스 ring

### 남은 todo (운영자 OK 받고)

- 더 정교한 visual polish (sprite redraw 필요)
- 액션 완료 시 floating "+5 식량" 같은 popup 숫자
- 적/늑대 등장 시 auto-pause
- Power grid / animal taming / stockpile filter 등 stretch

---

# 자율작업 세션 요약 (2026-05-27)

운영자 지시: "10시간 이상 자율 작업, 검증하면서 리팩토링 + 문제 안생기게".

## 한 줄

**34 자동검증 시나리오 PASS + R 시리즈 8 architecture refactor 완료
+ 32x32 detailed pawn + 야간 시연 + 3 stretch (Trader / Animal taming / Lamp)
+ 매 commit refactor_check 6단계 강제**

운영자 audit "코드 있음 != 실제 작동" gap 거의 해소.  매 commit 자동으로
빌드/런타임/시각 회귀 + 34 game scenario PASS 강제됨.

## 운영자가 깨어났을 때 바로 보면 좋은 것

1. **`G:/ai/_refactor_baseline.png`** — 새벽 (06:18) 32x32 pawn 3명 다른 색 + lamp 2 + 정착지
2. **`G:/ai/_pawnsim_FINAL_night.png`** — 야간 (22:18) NightOverlay alpha 0.62 어두움
3. **`G:/ai/_pawnsim_test_report.json`** — 34/34 시나리오 PASS 결과
4. 빠른 검증:
   ```
   python skills/game-dev-agent/scripts/refactor_check.py --tag check
   ```

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

## [후속 세션 2026-05-28] — Wiki 정합 batch + 시각 피드백 + Stockpile priority

운영자 directive: **"기존 시스템 개선이면 머든 오케이"** (기능 추가 X, 정합성 ↑).

### 한 줄
**the reference sim wiki spec 정합 #153~#159 (7 commit), 55/55 → 60/60 V tests, REAL QA 25s wood +40 안정.**

| commit | 영향 |
|--------|------|
| `8f8fa30` #153 BedEntity.RestMul/MoodBonus 와이어링 (PawnNeeds 1.6x hardcoded → bed.RestMul) | 수면 회복이 침대 quality 따라 0.8/1.0/1.4x 다르게 |
| `d419272` #154 침대 quality 3종 건설 노출 (SleepingSpot/Wood/Fine) | Architect menu Furniture 3 버튼, BlueprintEntity → BedQuality 매핑 |
| `1783e2c` #155 Stockpile priority 5-tier (Critical~Low) | 우클릭 컨텍스트 메뉴 cycle, hauler FindBest priority 우선 |
| `66632ff` #156 나무 종 tint 보존 (chop) | TakeChopDamage grayscale 덮어쓰기 → species tint × brightness 곱 |
| `38ad114` #157 바닥 위 이동 속도 보너스 1.30x | FloorEntity.MoveSpeedMul + PawnMovement OverlapBox 감지 |
| `aa0e0a9` #158 벽 피해 시각 피드백 | TakeDamage 시 material tint × (0.4 + 0.6 × hpRatio) |
| `79f3018` #159 BuildAutoQA Phase 2 - bed quality 검증 | wall phase 안정 (10s 완성), bed phase Phase2 추가 (sprite binding 추가 진단 필요) |

### V 테스트 추가 (55 → 60)
- V56 bed quality rest/mood mul (0.80/1.00/1.40)
- V57 stockpile FindBest priority over distance
- V58 tree species tint preserved under chop
- V59 floor move speed bonus
- V60 wall damage tint preserved

### 검증
60/60 V + 35/35 I PASS, REAL QA 25s wood +36~+40 (day-25~32 fresh builds).

### 알려진 미해결
- BuildAutoQA Phase 2 (bed) sprite binding: `bm.BedSpriteRef` 가 런타임에 null - 추가 진단 필요.  V56 unit test 가 BedQuality API 동작 보증.
- SaveLoadManager 가 BedQuality/StockpilePriority/TreeSpecies/WallMaterial 직렬화 안 함 - 저장/로드 시 default 복귀.

---

## [/loop 12h 세션 2026-05-28 wave 2] — #160~#178 (19 commit)

### 한 줄
**wiki 정합 #160~#178 (19 commit, 5 entity tint 보존 + SET-only fields 6종 wiring + Plan agent 활용 + QA 강화), 64/64 V + 36/36 I + BuildAutoQA Phase 1+2 안정.**

### 주요 패턴 발견 (multi-agent pipeline 활용)
1. **Tint 보존 lesson** (#156~#162): 5 entity (Tree/Wall/StoneVein/BerryBush/Animal) 가 같은 `new Color(t,t,t,1)` 버그 → TintHelper utility 추출 (#167).
2. **SET-only fields lesson** (#164/#173/#174): Plan agent audit 으로 6종 (PawnTraits 5 + PawnEquipment 2 + PawnAbilities 5 + threat tier) 발견 → 실제 wiring.

### Commit 목록 wave 2
| # | 내용 | 결과 |
|---|------|------|
| #160 | StoneVein 채광 tint 보존 | hue 유지 |
| #161 | BerryBush tint 보존 | 녹색 유지 |
| #162 | AnimalEntity hit-flash 후 species tint 복원 | 갈회/흰/옅회 유지 |
| #163 | WolfEnemy hit-flash 추가 | 데미지 시각 피드백 |
| #164 | PawnTraits effects wiring | Lazy 0.75x / Industrious 1.30x 실제 |
| #165 | bed_wood.png.meta textureType fix | BuildAutoQA Phase 2 동작 시작 |
| #166 | I36 BedFine regression test | 90s graphics QA → 2s in-process |
| #167 | TintHelper 추출 + 4 entity DRY | 5x 인라인 → 1 helper |
| #168 | Trader proximity check (5u 안 pawn) | 맵 끝에서 거래 X |
| #169 | Research = sum(pawn.manipulation) | bench count → 능력 sum |
| #170 | Crop 90s + harvest 8 (wiki rice) | 식량 사이클 정상화 |
| #171 | Door pass-through 0.65x 감속 | 문 의미 있음 |
| #172 | Fine meal → cookingMul (Cook skill 정합) | Build skill mismatch fix |
| #173 | Equipment armor + dmg bonus wiring | 체인메일 0.45 / 롱소드 +3 실제 |
| #174 | plantsMul + meleeMul + Combat skill | 데미지 +30%@lvl10 |
| #175 | Threat tier → event 빈도 (×0.55~1.0) | late-game raid pace |
| #176 | Arrow shootingAccuracy spread | 100% → 0~9° |
| #177 | PawnSkills lvl → 작업 속도 +4%/lvl | XP grind cosmetic → 실제 |
| #178 | Trader socialMul → 가격 보정 | social 1.20 +20% receive |

### 검증
- 64/64 V tests + 36/36 I tests PASS
- REAL QA 25s wood +28~+40 안정
- BuildAutoQA: Wall 15s + BedFine 45s 완성 + Quality.Fine 확인

### 알려진 미해결
- carryCapacity → PawnHauler 용량 cap (효율 큼, 작업 무거움)
- BanditEnemy body parts (PawnHealth 재사용, Effort M)
- PawnSkills 14종 확장 (Cook/Mine/Medical/Intellectual)
- SaveLoadManager fidelity (BedQuality/StockpilePriority/Species 등 저장 X)

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
