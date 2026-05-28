using UnityEngine;
using UnityEngine.EventSystems;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 17-25: build-mode singleton.
    ///   B: 벽 (목재 5)
    ///   F: 바닥 (목재 1, no collider, indoor marker)
    ///   G: 문 (목재 3, trigger collider)
    ///   T: 화덕 (목재 10, Day 25 cooking station)
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        public static BuildManager Instance { get; private set; }

        public enum Mode { Off, Wall, Floor, Door, Stove, Bed, WallStone, BedSleepingSpot, BedFine }  // #127 - stone wall, #154 - bed quality 3종
        public Mode CurrentMode { get; private set; } = Mode.Off;
        public bool BuildModeActive => CurrentMode != Mode.Off;
        // #182 - placement cooldown: SetMode 후 0.15s 동안 TryPlace skip.
        //  목적: Architect 버튼 click 같은 frame race 방지 (EventSystem guard 보다 견고).
        private float setModeTime = -10f;
        private const float PlaceCooldownSec = 0.15f;

        [SerializeField] private GameObject wallPrefab, floorPrefab, doorPrefab, stovePrefab, bedPrefab;
        // #159 - BuildAutoQA 가 spriteAsset 접근 필요 (외부 prefab 인스턴스는 SR.sprite 비할당).
        public Sprite WallSpriteRef => wallSprite;
        public Sprite BedSpriteRef => bedSprite;
        public GameObject WallPrefabRef => wallPrefab;
        public GameObject BedPrefabRef => bedPrefab;
        [SerializeField] private int wallCost = 5, floorCost = 1, doorCost = 3, stoveCost = 10, bedCost = 8;
        [SerializeField] private int wallStoneCost = 5;  // #127 - 석재 5
        // #154 - bed quality 별 cost (wiki: sleeping spot 0 / wood bed 8 / fine 30).
        //  Fine 은 wiki 가 비싸지만 (60+) 프로토타입에선 30 으로 낮춰 reachable.
        [SerializeField] private int bedSleepingSpotCost = 0;
        [SerializeField] private int bedFineCost = 30;
        [SerializeField] private SpriteRenderer ghostRenderer;
        [SerializeField] private Sprite wallSprite, floorSprite, doorSprite, stoveSprite, bedSprite;

        private Camera cam;

        public void SetRefs(GameObject wall, GameObject floor, GameObject door, GameObject stove,
                            Sprite wallS, Sprite floorS, Sprite doorS, Sprite stoveS,
                            SpriteRenderer ghost,
                            GameObject bed = null, Sprite bedS = null)
        {
            wallPrefab = wall; floorPrefab = floor; doorPrefab = door; stovePrefab = stove;
            wallSprite = wallS; floorSprite = floorS; doorSprite = doorS; stoveSprite = stoveS;
            ghostRenderer = ghost;
            bedPrefab = bed; bedSprite = bedS;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            cam = Camera.main;
        }

        private void Update()
        {
            if (cam == null) cam = Camera.main;
            if (Input.GetKeyDown(KeyCode.B)) SetMode(CurrentMode == Mode.Wall  ? Mode.Off : Mode.Wall);
            if (Input.GetKeyDown(KeyCode.F)) SetMode(CurrentMode == Mode.Floor ? Mode.Off : Mode.Floor);
            if (Input.GetKeyDown(KeyCode.G)) SetMode(CurrentMode == Mode.Door  ? Mode.Off : Mode.Door);
            if (Input.GetKeyDown(KeyCode.T)) SetMode(CurrentMode == Mode.Stove ? Mode.Off : Mode.Stove);
            // 운영자 피드백 - 침대 추가
            if (Input.GetKeyDown(KeyCode.Y)) SetMode(CurrentMode == Mode.Bed ? Mode.Off : Mode.Bed);
            if (BuildModeActive && Input.GetMouseButtonDown(1)) { SetMode(Mode.Off); return; }
            UpdateGhost();
            // #179/#182 - click 처리.  cooldown 으로 SetMode 직후 race 방지.
            //  EventSystem guard 는 over-blocking 위험 (TopBar/GuiControlBar 가 항상 raycast target).
            //  대신 PlaceCooldownSec (0.15s) 으로 메뉴 버튼 click → SetMode → 즉시 TryPlace race 차단.
            if (BuildModeActive && Input.GetMouseButtonDown(0))
            {
                float since = Time.unscaledTime - setModeTime;
                if (since < PlaceCooldownSec)
                {
                    Debug.Log($"[Build] CLICK skip: cooldown {since:F2}s < {PlaceCooldownSec}s (mode just set)");
                }
                else
                {
                    // UI 위 클릭은 EventSystem 이 별도로 처리 (Button 클릭 등).
                    //  여기는 map area 만 도달함 - UI Image 가 raycastTarget=true 면 EventSystem 가 이벤트 소비.
                    //  단 ClickSelector 와 BuildManager 둘 다 Input.GetMouseButtonDown 으로 raw input 잡음.
                    //  BuildManager 는 raw input 받지만, UI 가 raycastTarget 으로 EventSystem 이벤트만 소비.
                    //  결과: UI 위 클릭이어도 BuildManager.Update 가 받음.  EventSystem.IsPointerOverGameObject() check 추가.
                    bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                    Vector3 mwLog = (cam != null) ? cam.ScreenToWorldPoint(Input.mousePosition) : Vector3.zero;
                    if (overUI)
                    {
                        Debug.Log($"[Build] CLICK skip: overUI=true at screen={Input.mousePosition} world=({mwLog.x:F1},{mwLog.y:F1})");
                    }
                    else
                    {
                        Debug.Log($"[Build] CLICK at screen={Input.mousePosition} world=({mwLog.x:F1},{mwLog.y:F1})");
                        TryPlace();
                    }
                }
            }
        }

        public void SetMode(Mode m)
        {
            CurrentMode = m;
            setModeTime = Time.unscaledTime;  // #182 cooldown reset
            if (ghostRenderer == null) return;
            if (m == Mode.Off) { ghostRenderer.enabled = false; return; }
            ghostRenderer.enabled = true;
            ghostRenderer.sprite = m switch
            {
                Mode.Wall  => wallSprite,
                Mode.Floor => floorSprite,
                Mode.Door  => doorSprite,
                Mode.Stove => stoveSprite,
                Mode.Bed   => bedSprite,
                Mode.BedSleepingSpot => bedSprite,  // #154
                Mode.BedFine => bedSprite,  // #154
                _ => wallSprite,
            };
            ghostRenderer.sortingOrder = m == Mode.Floor ? 1 : 20;
        }

        private int CostFor(Mode m) => m switch
        {
            Mode.Wall            => wallCost,
            Mode.WallStone       => wallStoneCost,
            Mode.Floor           => floorCost,
            Mode.Door            => doorCost,
            Mode.Stove           => stoveCost,
            Mode.Bed             => bedCost,
            Mode.BedSleepingSpot => bedSleepingSpotCost,  // #154 - 0
            Mode.BedFine         => bedFineCost,          // #154 - 30
            _ => 0,
        };

        private GameObject PrefabFor(Mode m) => m switch
        {
            Mode.Wall            => wallPrefab,
            Mode.WallStone       => wallPrefab,  // 같은 prefab, 다른 자원
            Mode.Floor           => floorPrefab,
            Mode.Door            => doorPrefab,
            Mode.Stove           => stovePrefab,
            Mode.Bed             => bedPrefab,
            Mode.BedSleepingSpot => bedPrefab,  // #154 - 같은 prefab, quality 다름
            Mode.BedFine         => bedPrefab,  // #154
            _ => null,
        };

        private void UpdateGhost()
        {
            if (!BuildModeActive || ghostRenderer == null || cam == null) return;
            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            // 운영자 fb #1 - tile 시각 center 가 (x+0.5, y+0.5).  FloorToInt → +0.5 로 정렬.
            //  이전 RoundToInt 는 tile 모서리에 wall 배치돼서 floor 와 mismatch.
            int cx = Mathf.FloorToInt(mw.x);
            int cy = Mathf.FloorToInt(mw.y);
            ghostRenderer.transform.position = new Vector3(cx + 0.5f, cy + 0.5f, 0);
            int cost = CostFor(CurrentMode);
            bool stoneMode = CurrentMode == Mode.WallStone;
            bool canAfford = ResourceManager.Instance != null
                && (stoneMode ? ResourceManager.Instance.stone : ResourceManager.Instance.wood) >= cost;
            bool cellFree  = !CellOccupied(cx, cy);
            ghostRenderer.color = (canAfford && cellFree)
                ? new Color(1f, 1f, 1f, 0.55f)
                : new Color(1f, 0.4f, 0.4f, 0.55f);
        }

        private bool CellOccupied(int cx, int cy)
        {
            // tile center 기준 검사 (+0.5)
            var hits = Physics2D.OverlapBoxAll(new Vector2(cx + 0.5f, cy + 0.5f), Vector2.one * 0.4f, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                if (h.GetComponent<WallEntity>() != null) return true;
                if (h.GetComponent<DoorEntity>() != null) return true;
                if (h.GetComponent<TreeEntity>() != null) return true;
                if (h.GetComponent<PawnEntity>() != null) return true;
                if (h.GetComponent<BerryBushEntity>() != null) return true;
                if (h.GetComponent<StoveEntity>() != null) return true;
                if (h.GetComponent<BedEntity>() != null) return true;
                if (h.GetComponent<BlueprintEntity>() != null) return true;  // #118
            }
            return false;
        }

        /// <summary>#179 - test harness 호출용 (Input.mousePosition 우회).
        ///  실제 cell (cx, cy) 에 청사진 placement 시도.  성공 시 true.</summary>
        public bool TryPlaceAt(int cx, int cy)
        {
            return DoTryPlaceAt(cx, cy);
        }

        private void TryPlace()
        {
            if (cam == null) { Debug.LogWarning("[Build] TryPlace skip: camera null"); return; }
            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            int cx = Mathf.FloorToInt(mw.x);
            int cy = Mathf.FloorToInt(mw.y);
            DoTryPlaceAt(cx, cy);
        }

        private bool DoTryPlaceAt(int cx, int cy)
        {
            // #179 - silent return 4개를 명시 log 로 진단 가능하게.
            var prefab = PrefabFor(CurrentMode);
            if (prefab == null)
            {
                Debug.LogWarning($"[Build] TryPlace skip: prefab null for mode={CurrentMode}");
                return false;
            }
            int cost = CostFor(CurrentMode);
            if (CellOccupied(cx, cy))
            {
                Debug.Log($"[Build] TryPlace skip: cell ({cx},{cy}) occupied for mode={CurrentMode}");
                return false;
            }
            // #189 - 운영자 fb "건축 여전히 안 됨" root cause:
            //   이전: ResourceManager null 이면 return false → 운영자 click 시
            //   silent fail.  하지만 #142 이후 blueprint spawn 시 자원 차감 X
            //   (hauler 가 운반 후 차감), 그러니 ResourceManager 가 null 이어도
            //   BlueprintEntity 생성은 OK.  check 제거 → 청사진 정상 spawn.
            //   ResourceManager null 자체는 다른 시스템 문제로 별도 진단.
            if (ResourceManager.Instance == null)
            {
                Debug.LogWarning("[Build] WARNING: ResourceManager.Instance null - blueprint 는 spawn 진행 (hauler 가 자재 운반)");
            }
            Debug.Log($"[Build] TryPlace OK: mode={CurrentMode} → blueprint at ({cx+0.5f}, {cy+0.5f}), need wood={(CurrentMode == Mode.WallStone ? 0 : cost)} stone={(CurrentMode == Mode.WallStone ? cost : 0)}");
            // 운영자 fb v4 - 림월드 정상 흐름: 청사진 spawn 시 자원 차감 X.
            //  hauler 가 자재를 청사진 위치까지 운반 후 PawnBuilder 가 건설 작업.
            bool stoneMode = CurrentMode == Mode.WallStone;
            int needWood = stoneMode ? 0 : cost;
            int needStone = stoneMode ? cost : 0;

            Sprite ghostSpr = SpriteForCurrentMode();
            var bpGo = new GameObject($"Blueprint_{CurrentMode}");
            bpGo.transform.position = new Vector3(cx + 0.5f, cy + 0.5f, 0);
            var bp = bpGo.AddComponent<BlueprintEntity>();
            float secs = CurrentMode == Mode.Floor ? 2f : 5f;
            bp.Init(CurrentMode, prefab, ghostSpr, needWood, needStone, secs);
            return true;
        }

        private Sprite SpriteForCurrentMode() => CurrentMode switch
        {
            Mode.Wall  => wallSprite,
            Mode.Floor => floorSprite,
            Mode.Door  => doorSprite,
            Mode.Stove => stoveSprite,
            Mode.Bed   => bedSprite,
            Mode.BedSleepingSpot => bedSprite,
            Mode.BedFine         => bedSprite,
            _ => wallSprite,
        };
    }
}
