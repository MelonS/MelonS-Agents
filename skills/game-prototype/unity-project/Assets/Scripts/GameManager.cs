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
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == "-testmode") isolatedTest = true;
                if (arg == "-integration") integrationTest = true;
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

            // 운영자 피드백 (2026-05-27): "디자인 구리고 프로토타입 수준도 안되고"
            // → 선택된 콜로니스트 발밑에 명시적 노란 ring (펄스)
            SelectionRing.EnsureInScene();
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
            // → #113 RimWorld 우클릭 prioritize 컨텍스트 메뉴
            ContextMenuUI.EnsureInScene();
            // → #110 림월드 Architect 좌측 카테고리 메뉴 (F8)
            ArchitectMenu.EnsureInScene();
            // → #114 림월드 Work tab (F1) — per-pawn 우선순위 grid
            WorkTabUI.EnsureInScene();
            // → #126 림월드 Schedule (F4) — 24h slot grid
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

            // 운영자 피드백 — 게임 시작 시 자원 0 이면 빌드 모드도 못 켜고 무엇도 못함.
            // 림월드 starter 처럼 약간의 자원: 벽 6 + 화덕 1 + 식사 며칠
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.AddWood(40);   // 벽 6 + 화덕 1 (남는 거 약간)
                ResourceManager.Instance.AddFood(10);   // 모자라면 AI 사냥/채집 발동
                ResourceManager.Instance.AddMeals(2);   // 식사 2 (즉시 먹을 수 있음)
                Debug.Log("[GameManager] starter resources: wood=40 food=10 meals=2");
            }

            if (integrationTest)
            {
                var iGo = new GameObject("__IntegrationTestRunner__");
                iGo.AddComponent<MelonS.GameProto.Tests.IntegrationTestRunner>();
                Debug.Log("[GameManager] -integration → IntegrationTestRunner activated");
            }
        }
    }
}
