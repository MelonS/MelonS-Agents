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

        private void Start()
        {
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
            Debug.Log($"[GameManager] Day 4: spawned {spawnPositions.Length} colonists");

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
            // → mouse hover 시 entity 종류 + 가능한 action 표시
            HoverTooltip.EnsureInScene();
            // → wolf/bandit 접근 시 큰 빨강 "⚠ 위협" 우상단 (auto-pause X, 시각 only)
            ThreatAlertUI.EnsureInScene();
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
                // 시작 식량도 RimWorld식 물리 드롭 (목재처럼) — 운영자 '다 림월드식으로'(2026-06-03).
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
                Debug.Log($"[GameManager] starter resources (RimWorld식 물리): 목재 {dropped*50} + 간편식 {foodDropped}더미 바닥 드롭 (추상 카운터 폐기)");
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
    }
}
