using UnityEngine;

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

        public enum Mode { Off, Wall, Floor, Door, Stove, Bed }
        public Mode CurrentMode { get; private set; } = Mode.Off;
        public bool BuildModeActive => CurrentMode != Mode.Off;

        [SerializeField] private GameObject wallPrefab, floorPrefab, doorPrefab, stovePrefab, bedPrefab;
        [SerializeField] private int wallCost = 5, floorCost = 1, doorCost = 3, stoveCost = 10, bedCost = 8;
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
            if (BuildModeActive && Input.GetMouseButtonDown(0)) TryPlace();
        }

        public void SetMode(Mode m)
        {
            CurrentMode = m;
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
                _ => wallSprite,
            };
            ghostRenderer.sortingOrder = m == Mode.Floor ? 1 : 20;
        }

        private int CostFor(Mode m) => m switch
        {
            Mode.Wall  => wallCost,
            Mode.Floor => floorCost,
            Mode.Door  => doorCost,
            Mode.Stove => stoveCost,
            Mode.Bed   => bedCost,
            _ => 0,
        };

        private GameObject PrefabFor(Mode m) => m switch
        {
            Mode.Wall  => wallPrefab,
            Mode.Floor => floorPrefab,
            Mode.Door  => doorPrefab,
            Mode.Stove => stovePrefab,
            Mode.Bed   => bedPrefab,
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
            bool canAfford = ResourceManager.Instance != null && ResourceManager.Instance.wood >= cost;
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

        private void TryPlace()
        {
            if (cam == null) return;
            var prefab = PrefabFor(CurrentMode);
            if (prefab == null) return;
            int cost = CostFor(CurrentMode);
            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            // tile 시각 center 정렬 (운영자 #1 fix)
            int cx = Mathf.FloorToInt(mw.x);
            int cy = Mathf.FloorToInt(mw.y);
            if (CellOccupied(cx, cy)) return;
            if (ResourceManager.Instance == null || ResourceManager.Instance.wood < cost) return;
            ResourceManager.Instance.AddWood(-cost);

            // 운영자 fb #118 - 림월드 청사진 패턴.  즉시 완성 대신 BlueprintEntity spawn.
            //  pawn (PawnBuilder) 가 가서 5초 작업 후 finished prefab 으로 교체.
            //  floor (cost=1) 처럼 작은 것은 청사진 없이 즉시 (1차 단순화로 통일 - 모두 청사진).
            Sprite ghostSpr = SpriteForCurrentMode();
            var bpGo = new GameObject($"Blueprint_{CurrentMode}");
            bpGo.transform.position = new Vector3(cx + 0.5f, cy + 0.5f, 0);
            var bp = bpGo.AddComponent<BlueprintEntity>();
            // 건설 시간 — wall/door/stove/bed 큰 거는 5s, floor 작은 거는 2s.
            float secs = CurrentMode == Mode.Floor ? 2f : 5f;
            bp.Init(CurrentMode, prefab, ghostSpr, secs);
        }

        private Sprite SpriteForCurrentMode() => CurrentMode switch
        {
            Mode.Wall  => wallSprite,
            Mode.Floor => floorSprite,
            Mode.Door  => doorSprite,
            Mode.Stove => stoveSprite,
            Mode.Bed   => bedSprite,
            _ => wallSprite,
        };
    }
}
