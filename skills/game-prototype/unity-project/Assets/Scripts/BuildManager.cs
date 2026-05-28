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
                HandleLeftClickAt(Input.mousePosition, checkOverUI: true);
            }
        }

        /// <summary>
        /// #191 - real Input chain 과 시뮬레이션이 동일 path 공유.
        ///   Update 의 raw input → cooldown → overUI → TryPlace 시퀀스 그대로 재현.
        ///   BuildClickAutoQA v2 가 ExecuteEvents 로 button click 시뮬 후 이 메서드로 map click 시뮬.
        /// </summary>
        public BuildClickResult HandleLeftClickAt(Vector2 screenPos, bool checkOverUI)
        {
            if (!BuildModeActive) return BuildClickResult.ModeOff;
            float since = Time.unscaledTime - setModeTime;
            if (since < PlaceCooldownSec)
            {
                Debug.Log($"[Build] CLICK skip: cooldown {since:F2}s < {PlaceCooldownSec}s (mode just set)");
                return BuildClickResult.Cooldown;
            }
            if (checkOverUI)
            {
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (overUI)
                {
                    Vector3 mwLog = (cam != null) ? cam.ScreenToWorldPoint(screenPos) : Vector3.zero;
                    Debug.Log($"[Build] CLICK skip: overUI=true at screen={screenPos} world=({mwLog.x:F1},{mwLog.y:F1})");
                    if (BuildClickToast.Instance != null) BuildClickToast.Instance.ShowFail("✗ UI 위 클릭 - 맵에 직접 클릭하세요");
                    return BuildClickResult.OverUI;
                }
            }
            if (cam == null) cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[Build] CLICK skip: camera null"); return BuildClickResult.NoCamera; }
            Vector3 mw = cam.ScreenToWorldPoint(screenPos);
            int cx = Mathf.FloorToInt(mw.x);
            int cy = Mathf.FloorToInt(mw.y);
            Debug.Log($"[Build] CLICK at screen={screenPos} world=({mw.x:F1},{mw.y:F1}) cell=({cx},{cy})");
            bool placed = DoTryPlaceAt(cx, cy);
            return placed ? BuildClickResult.Placed : BuildClickResult.PlaceFailed;
        }

        /// <summary>#191 - QA / test 용 직접 진입점.  cooldown / overUI 무시하지 않음 (진짜 path).</summary>
        public BuildClickResult SimulateMapClick(Vector2 screenPos) => HandleLeftClickAt(screenPos, checkOverUI: false);

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
            // #190 - 각 실패 path 에 BuildClickToast 추가 (운영자가 화면에서 바로 원인 확인).
            var prefab = PrefabFor(CurrentMode);
            if (prefab == null)
            {
                Debug.LogWarning($"[Build] TryPlace skip: prefab null for mode={CurrentMode}");
                if (BuildClickToast.Instance != null) BuildClickToast.Instance.ShowFail($"✗ prefab 미설정 ({CurrentMode})");
                return false;
            }
            int cost = CostFor(CurrentMode);
            if (CellOccupied(cx, cy))
            {
                Debug.Log($"[Build] TryPlace skip: cell ({cx},{cy}) occupied for mode={CurrentMode}");
                if (BuildClickToast.Instance != null) BuildClickToast.Instance.ShowFail($"✗ 셀 점유됨 ({cx},{cy}) - 다른 곳 시도");
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
            // #190 - 클릭 성공 토스트 + 시각 ring (운영자가 "어디에 청사진 생겼지?" 즉시 확인)
            if (BuildClickToast.Instance != null)
            {
                string kr = ModeKr(CurrentMode);
                BuildClickToast.Instance.ShowSuccess($"✓ 청사진 - {kr} @ ({cx},{cy})");
            }
            ClickEffect.Spawn(new Vector3(cx + 0.5f, cy + 0.5f, 0), new Color(0.55f, 0.85f, 1.0f, 0.95f));
            return true;
        }

        private static string ModeKr(Mode m) => m switch
        {
            Mode.Wall            => "벽(목재)",
            Mode.WallStone       => "벽(석재)",
            Mode.Floor           => "바닥",
            Mode.Door            => "문",
            Mode.Stove           => "화덕",
            Mode.Bed             => "목재 침대",
            Mode.BedSleepingSpot => "수면 자리",
            Mode.BedFine         => "고급 침대",
            _ => m.ToString(),
        };

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

    public enum BuildClickResult { Placed, PlaceFailed, ModeOff, Cooldown, OverUI, NoCamera }
}
