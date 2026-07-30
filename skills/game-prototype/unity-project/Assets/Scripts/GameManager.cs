using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Game scene bootstrap — spawns initial pawn(s) and holds global
    /// game state references.  Day 1 = single pawn at origin.  Later
    /// days will own colonist list, needs system, AI Director hookup.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Spawn settings")]
        [SerializeField] private GameObject pawnPrefab;
        // #199 A2 — per-colonist 셔츠 변형 sprite.  SceneSetup 가 [blue, rust, olive]
        //  순서로 채움.  colonist i 는 colonistVariantSprites[i] 를 입음 (sr.color=white,
        //  sprite 자체가 옷색을 담음 → 더 이상 tint multiplier 불필요).  비어있거나
        //  부족하면 prefab 기본 sprite(pawn_colonist) 유지 (fallback).
        [SerializeField] private Sprite[] colonistVariantSprites;
        [SerializeField] private Sprite arrowSpriteRuntime;  // Day 50
        [SerializeField] private Sprite woodPileSpriteRuntime;  // #116 - 벌목 후 wood pile drop
        [SerializeField] private Sprite stoneChunkSpriteRuntime;  // #119 - 채광 후 stone chunk drop
        [SerializeField] private Sprite meatPileSpriteRuntime;    // #129 - 동물 죽음 시 meat drop
        [SerializeField] private Vector2[] spawnPositions = new Vector2[]
        {
            // 운영자 피드백 - "림들 겹쳐서 이동" - 일렬 spawn → 같은 target pick.
            //  spread (x, y) 둘 다 변화시켜 spawn 부터 분리.
            new Vector2(-1.5f, 1.5f),
            new Vector2( 1.5f, 0.5f),
            new Vector2(-0.5f, -1.5f),
        };
        // Day 32: 한국 이름 — generic, 흔한 한국 이름 (저작권 무관).
        private static readonly string[] KoreanNames = new[]
        {
            "지훈", "민지", "서연", "준호", "예린", "도현", "수아", "현우",
        };

        private System.Collections.IEnumerator PauseAtStart()
        {
            yield return null;   // TimeController/AlertStackUI 부트 대기 (1프레임)
            if (TimeController.Instance != null && ReproHarness.Enabled == false)
            {
                TimeController.Instance.SetScale(0f);
                AlertStackUI.Notify("일시정지 — 둘러보고 1배속으로 시작하세요", 1);
                Debug.Log("[GameManager] 일시정지 시작 (둘러보기)");
            }
        }

        private void Start()
        {
            // 퀵픽 '일시정지 시작' (사람-리듬 #1 합치) — 레퍼런스처럼 정지 상태로
            //  시작해 플레이어가 맵을 둘러보고 계획한 뒤 직접 시작한다.
            //  하네스/배치 실행은 제외 (ReproHarness 가 BeginSim 에서 1x 강제).
            if (!ReproHarness.Enabled) StartCoroutine(PauseAtStart());

            // R7: -testmode → isolated unit test 55개 (normal spawn skip)
            // 통합 검증: -integration → normal spawn + IntegrationTestRunner 둘 다 (진짜 game state 위에서)
            bool isolatedTest = false;
            bool integrationTest = false;
            bool pawnDiag = false;
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == "-testmode") isolatedTest = true;
                if (arg == "-integration") integrationTest = true;
                if (arg == "-pawndiag") pawnDiag = true;
            }
            if (isolatedTest)
            {
                var trGo = new GameObject("__TestRunner__");
                trGo.AddComponent<MelonS.GameProto.Tests.TestRunner>();
                Debug.Log("[GameManager] -testmode → TestRunner (isolated) activated");
                return;
            }
            // integrationTest: normal spawn 진행 후 IntegrationTestRunner 추가 (아래 spawn block 후)


            if (pawnPrefab == null)
            {
                Debug.LogWarning("[GameManager] pawnPrefab not assigned");
                return;
            }
            int i = 0;
            foreach (var pos in spawnPositions)
            {
                GameObject p = Instantiate(pawnPrefab, pos, Quaternion.identity);
                // P5: 32x32 detailed sprite 도입 후 — prefab scale 1.0 (이전 2x scale 불필요)
                p.transform.localScale = new Vector3(1f, 1f, 1f);
                var sr = p.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    // #199 A2 — colonist i 에게 변형 셔츠 sprite 배정.
                    //  SceneSetup 가 [blue, rust, olive] 채움 → 3 colonist 각각 다른 muted 색.
                    //  sprite 자체가 옷색을 담으므로 color multiplier 없이 white 로 둠.
                    //  변형이 없거나 부족하면 prefab 기본 sprite(pawn_colonist) 유지 (fallback).
                    if (colonistVariantSprites != null &&
                        i < colonistVariantSprites.Length &&
                        colonistVariantSprites[i] != null)
                    {
                        sr.sprite = colonistVariantSprites[i];
                    }
                    else if (colonistVariantSprites != null && colonistVariantSprites.Length > 0)
                    {
                        Debug.LogWarning($"[GameManager] colonist[{i}] variant sprite missing — keeping prefab default (pawn_colonist)");
                    }

                    if (sr.sprite == null)
                    {
                        Debug.LogError($"[GameManager] pawn[{i}] SpriteRenderer.sprite NULL — flat-color fallback");
                        sr.color = new Color(0.95f, 0.65f, 0.35f, 1f);
                    }
                    else
                    {
                        // variant/기본 sprite 가 옷색을 담음 → multiplier 없이 true color.
                        sr.color = Color.white;
                    }
                    sr.enabled = true;
                }
                PawnEntity entity = p.GetComponent<PawnEntity>();
                if (entity != null)
                {
                    string name = KoreanNames[i % KoreanNames.Length];
                    var nameField = typeof(PawnEntity).GetField("pawnName",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (nameField != null) nameField.SetValue(entity, name);
                    // #트레잇 결정성(2026-06-03): 이름 설정 직후 트레잇을 그 이름으로 재시드 →
                    //  콜로니스트마다 다른 트레잇(이전엔 전원 "Pawn(Clone)" 시드로 동일).
                    p.GetComponent<PawnTraits>()?.ReRollFromName(name);
                    // 시작 스킬 개인차 (2026-06-12) — 트레잇과 같은 이름-시드 결정론.
                    p.GetComponent<PawnSkills>()?.ReRollFromName(name);
                    // 스킬이 정해진 **직후** 작업 우선순위 기본값을 그 스킬에서 유도
                    //  (2026-07-29) — 순서 중요: ReRollFromName 보다 먼저 부르면 전 림이
                    //  같은 기본 스킬을 보고 같은 우선순위가 나온다.
                    p.GetComponent<PawnWorkSettings>()?.ApplyDefaultsFromSkills();
                    // #199 A2 — unselectedColor=white 로 강제: idle(미선택/미징집) 시
                    //  PawnEntity.ApplyVisual 이 sr.color=unselectedColor 로 덮어쓰는데,
                    //  여기서 white 가 아니면 변형 sprite 의 옷색이 tint 로 더럽혀짐.
                    //  selected=yellow / drafted=cyan 분기는 그대로 동작.
                    var tintField = typeof(PawnEntity).GetField("unselectedColor",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (tintField != null) tintField.SetValue(entity, Color.white);
                }
                // Day 50: arrow sprite injection — PawnUtilityAI 가 ranged
                //  attack용으로 사용 (단 research "simple_bow" 완료 후 활성).
                var ai = p.GetComponent<PawnUtilityAI>();
                if (ai != null && arrowSpriteRuntime != null) ai.SetArrowSprite(arrowSpriteRuntime);
                i++;
            }
            Debug.Log($"[GameManager] 콜로니 시작: spawned {spawnPositions.Length} colonists");   // r8 관찰 — 'Day 4' 하드코딩 라벨이 HUD '봄 1일'과 혼선

            AssignStartingResearcher();
            StartCoroutine(RoofStarterBuildings());

            // 운영자 피드백 (2026-05-27): "키보드 의존도 너무 높음, gui 가 전혀 안됨"
            // → GUI 버튼 바 자동 부착 (Speed/Draft/Build/Research 10 버튼)
            GuiControlBar.EnsureInScene();

            // 콜로니심식 콜로니스트 바 (화면 상단 중앙) — 이름 + HP/mood 바, 클릭 시 선택+카메라 focus
            ColonistBar.EnsureInScene();

            // 운영자 피드백 (2026-05-27): "디자인 구리고 프로토타입 수준도 안되고"
            // → 선택된 콜로니스트 발밑에 명시적 노란 ring (펄스)
            SelectionRing.EnsureInScene();
            MultiSelectionRings.EnsureInScene();  // #audit3 - 마퀴 다중선택 pawn 발밑 펄스 링
            InspectHighlight.EnsureInScene();  // #138 - 선택된 entity outline
            ResourceMonitorLogger.EnsureInScene();  // #140 - 자원 변화 log dump (QA 검증)
            ResourceLowAlert.EnsureInScene();       // #136 - 자원 부족 popup
            BuildAutoQA.EnsureInScene();            // #147 - -build-qa flag 시 자동 건축 시나리오
            BuildClickAutoQA.EnsureInScene();       // #180 - -build-click-qa flag (운영자 fb 진짜 click QA)
            ArchitectClickAutoQA.EnsureInScene();   // archfix - -architect-click-qa flag (REAL pointer raycast on architect menu)
            PawnActionAutoQA.EnsureInScene();       // #192 - -pawn-action-qa flag (chop/harvest/tame/trade/mine/attack)
            ReproHarness.EnsureInScene();           // WORKFLOW-V2 - -repro <json> 운영자 버그 재현 시나리오 (SimInput 경유 진짜 입력 경로)
            // → mouse hover 시 entity 종류 + 가능한 action 표시
            HoverTooltip.EnsureInScene();
            // → wolf/bandit 접근 시 큰 빨강 "! 위협" 우상단 (auto-pause X, 시각 only)
            // #ui백로그 7.2 — ThreatAlertUI 비활성: AlertStackUI(threatTier>=1 영속 카드)와
            //  동일 위협을 이중 표시하던 구식 스톱갭.  파일은 보존(#232 선례), 부트만 끊는다.
            // ThreatAlertUI.EnsureInScene();
            // → 비-pawn entity 좌클릭 시 인포 패널 (#105)
            EntityInspectorPanel.EnsureInScene();
            FeatureAuditQA.EnsureInScene();   // #248 -feature-audit 자가-발견 QA
            // → #113 the reference sim 우클릭 prioritize 컨텍스트 메뉴
            ContextMenuUI.EnsureInScene();
            // → #110 레퍼런스 콜로니심 Architect 좌측 카테고리 메뉴 (F8)
            ArchitectMenu.EnsureInScene();
            // → #114 레퍼런스 콜로니심 Work tab (F1) — per-pawn 우선순위 grid
            WorkTabUI.EnsureInScene();
            // → #126 레퍼런스 콜로니심 Schedule (F4) — 24h slot grid
            ScheduleUI.EnsureInScene();
            // → #190 운영자 "청사진 설치 안 됨" 진단 토스트 (build click 결과 화면 표시)
            BuildClickToast.EnsureInScene();

            // #44(2026-06-05): GameSaveButtons 가 Game.unity 에 사전 배치돼 있지 않아 F5/F9 +
            //  설정메뉴 저장/불러오기 행이 모두 무효였다(인게임 save/load 전체 접근 불가).  pawn
            //  프리팹 + (있으면) 기존 트리 스프라이트를 배선해 런타임 생성 → save/load 활성화.
            Sprite treeSprForSave = null;
            var anyTree = FindFirstObjectByType<TreeEntity>();
            if (anyTree != null)
            {
                var tsr = anyTree.GetComponent<SpriteRenderer>();
                if (tsr != null) treeSprForSave = tsr.sprite;
            }
            GameSaveButtons.EnsureInScene(pawnPrefab, treeSprForSave);

            // #116 - wood pile sprite 를 TreeEntity static field 에 박음.
            //  fallback: SerializeField 못 받았으면 Resources 등 안 거치고 즉시 inventory 추가 (legacy).
            if (woodPileSpriteRuntime != null)
            {
                TreeEntity.WoodPileSprite = woodPileSpriteRuntime;
                PawnHauler.WoodPileSpriteRef = woodPileSpriteRuntime;
            }
            // #119
            if (stoneChunkSpriteRuntime != null)
            {
                StoneVeinEntity.StoneChunkSprite = stoneChunkSpriteRuntime;
                PawnHauler.StoneChunkSpriteRef = stoneChunkSpriteRuntime;
            }
            // #129
            if (meatPileSpriteRuntime != null)
            {
                MeatPileEntity.SharedSprite = meatPileSpriteRuntime;
                PawnHauler.MeatPileSpriteRef = meatPileSpriteRuntime;
            }

            // #229 맨땅 시작 정합 — 맨땅 시작이라 첫 베이스를 직접 지을 자재 +
            //  지을 동안 버틸 식량 버퍼가 필요하다.  the reference sim(목재~300 + 생존식량~50)에 매핑:
            //   목재 300 = 벽/바닥/화덕/침대 첫 베이스 건설분
            //   식사 50  = 생존식량 버퍼(바닥에서 자고 이걸로 버티며 농장·화덕 건설)
            //  무한 축적 방지는 STEP2(벌목/채광 지정제)에서 — 자동 채집을 끊어 이 버퍼가
            //  건설로 '소비'되고 플레이어 지정으로만 다시 모인다.
            if (ResourceManager.Instance != null)
            {
                // #243 운영자 fb "목재 처음에 수치로 주지말고 바닥에 랜덤하게 떨궈둬" — the reference sim
                //  추락 잔해처럼 시작 목재를 '카운터 숫자'가 아니라 바닥의 물리 wood pile 로 흩뿌린다.
                //  pawn 이 이걸 청사진까지 운반(haul)해 organic 하게 건설 → 건설 루프가 물리 자재로
                //  굴러간다(카운터는 stockpile 운반 시 자연 적립).  결정론적 scatter(seed) — spawn
                //  cell(±2,±2) 회피, 6 pile × 50 = 300 목재.
                var woodRng = new System.Random(73101);
                int dropped = 0, tries = 0;
                while (dropped < 6 && tries < 200)
                {
                    tries++;
                    float wx = (float)(woodRng.NextDouble() * 14.0 - 7.0);   // ±7 범위
                    float wy = (float)(woodRng.NextDouble() * 14.0 - 7.0);
                    if (Mathf.Abs(wx) < 2.5f && Mathf.Abs(wy) < 2.5f) continue;  // pawn spawn 회피
                    // 운영자 fb(2026-06-03) "통나무 더미 50개와 5개가 다른 아이템처럼 보임":
                    //  시작 50-더미만 sprite=null 을 넘겨 EnsureSprite 의 fallback(절차생성/Resources)
                    //  으로 그려진 반면, 나무 벌목 5-더미는 authored WoodPileSprite 를 써서 외형이
                    //  달랐다.  나무 드롭과 동일한 authored sprite(woodPileSpriteRuntime)를 넘겨
                    //  모든 통나무 더미가 같은 아이템으로 보이게 통일(수량은 hover '×N' 으로 구분).
                    WoodPileEntity.Spawn(new Vector3(Mathf.Floor(wx) + 0.5f, Mathf.Floor(wy) + 0.5f, 0f), 50, woodPileSpriteRuntime);
                    dropped++;
                }
                // 시작 식량도 콜로니심식 물리 드롭 (목재처럼) — 운영자 '다 콜로니심식으로'(2026-06-03).
                //  이전: AddMeals(50) 추상 카운터 → '식사:50' 이 맵 어디에도 없어 혼란("이거 어디 있는
                //  아이템?").  지금: spawn 근처에 비부패성 '간편식' 물리 더미를 흩뿌려 pawn 이 걸어가
                //  먹는다(섭취는 PawnNeeds.BeginEatWalk→ConsumeAtSource 가 이미 물리 더미 지원).
                //  카운터는 운반→저장 시 자연 적립(목재와 동일 organic 흐름).
                var foodRng = new System.Random(91537);
                int foodDropped = 0, foodTries = 0;
                while (foodDropped < 6 && foodTries < 200)
                {
                    foodTries++;
                    float fx = (float)(foodRng.NextDouble() * 12.0 - 6.0);
                    float fy = (float)(foodRng.NextDouble() * 12.0 - 6.0);
                    if (Mathf.Abs(fx) < 2.5f && Mathf.Abs(fy) < 2.5f) continue;  // pawn spawn 회피
                    MeatPileEntity.Spawn(
                        new Vector3(Mathf.Floor(fx) + 0.5f, Mathf.Floor(fy) + 0.5f, 0f),
                        10, MeatPileEntity.SharedSprite, "간편식", 3000f);  // 긴 수명=비부패성(간편식)
                    foodDropped++;
                }
                Debug.Log($"[GameManager] starter resources (콜로니심식 물리): 목재 {dropped*50} + 간편식 {foodDropped}더미 바닥 드롭 (추상 카운터 폐기)");

                // #첫인상 (2026-07-27): 시작 저장구역을 미리 깔아 둔다.
                //
                // 배경 — 가상 유저 10인 평가에서 **8명 전원이 같은 1순위**를 지목했다:
                // "게임 내 9시간(6:00→14:42)이 지나도 자원이 전부 0, 화면에 아무 변화가 없다".
                // 원인은 버그가 아니라 시작 상태였다.  위 블록이 목재 300을 물리 더미로 흩뿌리는데
                // (장르 정공법 — 운반이 실제 노동), **저장구역이 하나도 없으면 운반 대상이 없어
                // 아무도 움직이지 않는다.**  게다가 튜토리얼은 "이제 콜로니스트가 알아서 일합니다"
                // 라고 말한다 — 게임이 자기 자신에 대해 거짓말을 하는 상태였다.
                //
                // 그래서 시작 시 작은 저장구역 하나를 스폰 근처에 깔아 둔다.  그러면 첫 30초 안에
                // 콜로니스트가 바닥 목재를 나르기 시작하고 카운터가 0에서 올라가는 게 눈에 보인다.
                // 튜토리얼 ①(저장공간 만들기)은 여전히 유효하다 — 플레이어는 이걸 '확장'하게 된다.
                //
                // 위치: 스폰(±2.5) 바깥 북동쪽 3×3.  목재/간편식 드롭이 스폰 ±2.5 를 피하므로
                // 이 구역이 기존 더미를 덮어써 '이미 저장된 것처럼' 보이는 일도 없다.
                StartCoroutine(PlaceStarterStockpile());
            }

            if (integrationTest)
            {
                var iGo = new GameObject("__IntegrationTestRunner__");
                iGo.AddComponent<MelonS.GameProto.Tests.IntegrationTestRunner>();
                Debug.Log("[GameManager] -integration → IntegrationTestRunner activated");
            }
            if (pawnDiag)
            {
                var dGo = new GameObject("__PawnDiagnostics__");
                dGo.AddComponent<PawnDiagnostics>();
                Debug.Log("[GameManager] -pawndiag → PawnDiagnostics activated");
            }
        }

        /// <summary>시작 정착지의 **기성 건물에 지붕을 덮는다**.
        ///
        /// 시작 집의 벽은 SceneSetup 이 씬에 구워 넣은 것이라 BuildManager.SpawnFinished 를
        /// 타지 않는다 — 즉 밀폐 알림(NotifyWallBuilt)이 한 번도 울리지 않는다.  그래서
        /// 지붕·침대·문이 다 갖춰진 집 옆에 "지붕 아래 잠자리 3 → 0/3" 이 떠 있었다.
        ///
        /// 좌표를 여기 박지 않고 **침대에서 역으로 찾는다**: 침대가 놓인 칸을 실내 후보로
        /// 보고 밀폐 판정을 돌린다.  집이 어디로 옮겨지든, 세이브에서 복원되든 따라온다.
        /// 야외에 놓인 침대는 플러드가 바깥으로 새 나가 판정이 실패하므로 조용히 넘어간다
        /// (= 노숙은 여전히 노숙이다).</summary>
        private System.Collections.IEnumerator RoofStarterBuildings()
        {
            const int MaxWaitFrames = 120;   // ~2초 — RoofDesignation 은 자가 부트스트랩이라 늦을 수 있다
            for (int i = 0; i < MaxWaitFrames && RoofDesignation.Instance == null; i++)
                yield return null;
            var rd = RoofDesignation.Instance;
            if (rd == null)
            {
                // 조용히 포기하지 않는다 — 이 기능이 안 붙은 걸 로그 없이 넘기면
                //  "고쳤는데 화면은 그대로"를 또 반복하게 된다.
                Debug.LogWarning("[GameManager] RoofDesignation 미준비 — 시작 건물 지붕 생략");
                yield break;
            }

            // 물리 콜라이더(벽/문)가 자리를 잡은 뒤에 판정해야 한다 — IsWallCell 이
            //  OverlapBox 로 벽을 찾으므로, 트랜스폼이 물리에 동기화되기 전이면 벽을
            //  못 보고 "밀폐 아님"으로 잘못 판정한다.
            //
            // ⚠ 여기서 `WaitForFixedUpdate` 를 쓰면 안 된다 (2026-07-31 실측으로 확인).
            //  이 게임은 **일시정지 상태(timeScale 0)로 부팅**하는데, timeScale 0 이면
            //  FixedUpdate 가 아예 돌지 않아 코루틴이 영원히 그 자리에 선다.  1차 구현이
            //  정확히 그랬고, 증상은 "지붕 로그가 한 줄도 안 찍히고 목표는 0/6" 이었다.
            //  `yield return null` 은 Update 기반이라 정지 중에도 진행한다.
            yield return null;
            Physics2D.SyncTransforms();

            int total = 0;
            var seeded = new System.Collections.Generic.HashSet<Vector2Int>();
            foreach (var b in Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                var cell = new Vector2Int(Mathf.FloorToInt(b.transform.position.x),
                                          Mathf.FloorToInt(b.transform.position.y));
                if (!seeded.Add(cell)) continue;   // 같은 방의 침대 3개 → 판정 1회면 충분
                total += rd.RoofEnclosedInstant(cell);
            }
            if (total > 0) Debug.Log($"[GameManager] 시작 건물 지붕 {total}칸 덮음");
        }

        /// <summary>창립 콜로니의 **연구 담당 1명**을 정한다.
        ///
        /// 2026-07-31 관측: 데모 영상 전 구간에서 연구가 0/100 → 7/100 이었고 HUD 에는
        /// "연구자 없음"이 떠 있었다.  원인은 속도가 아니라 배정이다 —
        /// `ApplyDefaultsFromSkills` 의 마지막 else 가 **전원에게 연구 4(최하)** 를 주는데,
        /// 운반이 3 이라 항상 위에 있고 바닥의 더미는 마르지 않는다.  그래서 우선순위 4 인
        /// 연구는 차례가 영영 오지 않았고, "연구 1개 완료" 목표는 도달 불가였다.
        ///
        /// 기본값 규칙 자체는 건드리지 않는다(모든 림에게 연구 2를 주면 이번엔 아무도
        /// 운반을 안 한다).  대신 **창립 시 한 명을 지명**한다 — 정착지가 역할을 나누는 건
        /// 장르 관례이고, 부수효과로 직업 탭 3열이 서로 달라진다(간접 조작의 근거 화면).
        ///
        /// 지명 규칙: 육체 숙련(채집·벌목·건축·전투) 합이 가장 낮은 림.  손재주로 먹고살기
        /// 어려운 사람이 책상에 앉는다 — 콜로니 입장에서 기회비용이 가장 작은 배치다.
        /// 동점이면 이름 순으로 끊어 결정성을 보장한다(트레잇·스킬과 같은 이름-시드 원칙).</summary>
        private static void AssignStartingResearcher()
        {
            PawnWorkSettings best = null;
            string bestName = null;
            int bestScore = int.MaxValue;
            foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                if (p == null || p.IsDead) continue;
                var ws = p.GetComponent<PawnWorkSettings>();
                if (ws == null) continue;
                var sk = p.GetComponent<PawnSkills>();
                int score = sk == null ? 0
                    : sk.GetLevel(SkillKind.Gather) + sk.GetLevel(SkillKind.Chop)
                    + sk.GetLevel(SkillKind.Build)  + sk.GetLevel(SkillKind.Combat);
                string nm = p.PawnName ?? p.name;
                if (score < bestScore
                    || (score == bestScore && string.CompareOrdinal(nm, bestName) < 0))
                {
                    bestScore = score; best = ws; bestName = nm;
                }
            }
            if (best == null) return;
            // 2 = 요리와 같은 밴드.  운반(3)·벌목보다 위라 실제로 벤치에 앉지만, 치료(1)
            //  같은 응급보다는 아래다.  플레이어는 직업 탭에서 언제든 바꿀 수 있다.
            best.SetPriority(WorkKind.Research, 2);
            Debug.Log($"[GameManager] 연구 담당 지명: {bestName} (육체 숙련 합 {bestScore}) — 연구 우선순위 2");
        }

        /// <summary>시작 저장구역 3×3 을 깐다 (첫인상 — 위 호출부 주석 참조).
        ///
        /// 코루틴인 이유: StockpileDesignation 은 RuntimeInitializeOnLoadMethod 로 자가
        /// 부트스트랩하고 PathGrid 도 씬 초기화 중에 채워진다.  GameManager.Start 시점엔
        /// 둘 다 아직 없을 수 있어 한 프레임 양보한 뒤 준비될 때까지 짧게 기다린다.
        /// 끝내 준비 안 되면 조용히 포기한다 — 시작 편의 기능이 부팅을 막아선 안 된다.</summary>
        private System.Collections.IEnumerator PlaceStarterStockpile()
        {
            const int MaxWaitFrames = 120;   // ~2초 (60fps 기준).  헤드리스/저사양 여유
            for (int i = 0; i < MaxWaitFrames && StockpileDesignation.Instance == null; i++)
                yield return null;
            var sd = StockpileDesignation.Instance;
            if (sd == null)
            {
                Debug.LogWarning("[GameManager] 시작 저장구역 생략 — StockpileDesignation 미준비");
                yield break;
            }

            // 위치: 스폰 회피 반경(2.5) 바깥 **남서쪽** (-6..-4, -6..-4).
            //
            // 처음엔 북동쪽 (3..5, 3..5) 에 뒀다가 옮겼다 — 그 자리가 **튜토리얼이 집을 짓는
            // 구역과 정면으로 겹쳤다.**  실측: p2-first10min 이 벽을 (3.5~7.5, 4.5) 에 놓는데
            // 클릭이 `at=Stockpile_5_5` / `at=Stockpile_6_5` 로 잡혀 배치가 거부됐다.
            // 테스트만의 문제가 아니라 **시연 문제**다 — 플레이어가 튜토리얼대로 집을 지으려는
            // 자리에 창고가 미리 깔려 있으면 "왜 안 지어지지?" 가 된다.
            // 남서쪽은 목재/간편식 산포 범위(±7) 안이라 운반 거리도 짧게 유지된다.
            int placed = 0;
            for (int cx = -6; cx <= -4; cx++)
                for (int cy = -6; cy <= -4; cy++)
                    if (sd.DesignateCell(new Vector2Int(cx, cy)) != null) placed++;

            Debug.Log($"[GameManager] 시작 저장구역 {placed}칸 배치 — 바닥 목재 운반이 즉시 시작된다");

            // 시작 벌목 지정 (2026-07-27, 가상 유저 5라운드 공통 지적의 마지막 조각).
            //
            // 저장구역만으로는 **첫 30초**만 살아난다: 바닥 목재 300 이 창고로 옮겨진 뒤
            // 새 일감이 없어 콜로니가 다시 멈춘다.  실측으로 게임 내 7시간 30분 동안
            // 목재 +0 / 석재 +0 / 가치 +0 이었고, 면접관 페르소나는 이걸 두고
            // "02→03 의 300 은 생산이 아니라 시작 지급분이 옮겨진 것뿐"이라고 정확히 짚었다.
            //
            // 그래서 스폰 근처 나무 몇 그루에 벌목을 미리 지정해 둔다.  효과:
            //  1) 플레이어가 아무것도 안 해도 **나무가 쓰러지고 목재가 계속 오르는 걸 본다**
            //     (스트리머 페르소나: "일한 흔적이 화면에 남아야 한다")
            //  2) 그게 끝나면 다시 멈추는데, 그 시점에 튜토리얼 ①이 "나무를 우클릭 → 벌목"을
            //     안내한다 — **먼저 보고, 그다음 직접 해보는** 온보딩 순서가 된다.
            // ── 되돌림 (2026-07-27 05:10) ────────────────────────────────
            // 시작 벌목 지정을 넣었더니 **건축 배치가 실패**했다 (p2-first10min /
            // p3-rotate-bed 에서 blueprintCount=0).  ChopTarget 마커가 배치 검사에
            // 걸리는 것으로 보인다.  건축은 시연에서 반드시 보여줄 경로이므로,
            // "화면이 계속 변한다"보다 우선한다.
            //
            // 저장구역만으로도 첫 30초의 0→300 은 이미 확보돼 있고, 튜토리얼 ①이
            // "나무를 우클릭 → 벌목"을 가르치므로 그다음 생산은 플레이어가 잇는다.
            // 아래 함수는 원인(마커-배치 충돌)을 규명한 뒤 되살릴 것.
            //   MarkStarterChopTargets(6);
        }

        /// <summary>스폰 근처 나무 n 그루를 벌목 지정.  가까운 순으로 고르되 스폰 바로 위는
        ///  피한다(폰이 서 있는 칸에 작업을 걸면 첫 이동이 어색하다).</summary>
        private static void MarkStarterChopTargets(int n)
        {
            var cd = TreeChopDesignation.Instance;
            if (cd == null) { Debug.LogWarning("[GameManager] 시작 벌목 지정 생략 — 매니저 미준비"); return; }

            var trees = Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None);
            if (trees == null || trees.Length == 0) return;

            System.Array.Sort(trees, (a, b) =>
                a.transform.position.sqrMagnitude.CompareTo(b.transform.position.sqrMagnitude));

            int marked = 0;
            foreach (var t in trees)
            {
                if (marked >= n) break;
                if (t == null || t.IsDestroyed) continue;
                Vector3 p = t.transform.position;
                if (Mathf.Abs(p.x) < 3f && Mathf.Abs(p.y) < 3f) continue;   // 스폰 바로 위 회피
                if (cd.TryMark(t.gameObject) != null) marked++;
            }
            Debug.Log($"[GameManager] 시작 벌목 지정 {marked}그루 — 첫 화면부터 생산이 이어진다");
        }
    }
}
