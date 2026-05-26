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

        public enum Mode { Off, Wall, Floor, Door, Stove }
        public Mode CurrentMode { get; private set; } = Mode.Off;
        public bool BuildModeActive => CurrentMode != Mode.Off;

        [SerializeField] private GameObject wallPrefab, floorPrefab, doorPrefab, stovePrefab;
        [SerializeField] private int wallCost = 5, floorCost = 1, doorCost = 3, stoveCost = 10;
        [SerializeField] private SpriteRenderer ghostRenderer;
        [SerializeField] private Sprite wallSprite, floorSprite, doorSprite, stoveSprite;

        private Camera cam;

        public void SetRefs(GameObject wall, GameObject floor, GameObject door, GameObject stove,
                            Sprite wallS, Sprite floorS, Sprite doorS, Sprite stoveS,
                            SpriteRenderer ghost)
        {
            wallPrefab = wall; floorPrefab = floor; doorPrefab = door; stovePrefab = stove;
            wallSprite = wallS; floorSprite = floorS; doorSprite = doorS; stoveSprite = stoveS;
            ghostRenderer = ghost;
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
            if (BuildModeActive && Input.GetMouseButtonDown(1)) { SetMode(Mode.Off); return; }
            UpdateGhost();
            if (BuildModeActive && Input.GetMouseButtonDown(0)) TryPlace();
        }

        private void SetMode(Mode m)
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
            _ => 0,
        };

        private GameObject PrefabFor(Mode m) => m switch
        {
            Mode.Wall  => wallPrefab,
            Mode.Floor => floorPrefab,
            Mode.Door  => doorPrefab,
            Mode.Stove => stovePrefab,
            _ => null,
        };

        private void UpdateGhost()
        {
            if (!BuildModeActive || ghostRenderer == null || cam == null) return;
            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            int cx = Mathf.RoundToInt(mw.x);
            int cy = Mathf.RoundToInt(mw.y);
            ghostRenderer.transform.position = new Vector3(cx, cy, 0);
            int cost = CostFor(CurrentMode);
            bool canAfford = ResourceManager.Instance != null && ResourceManager.Instance.wood >= cost;
            bool cellFree  = !CellOccupied(cx, cy);
            ghostRenderer.color = (canAfford && cellFree)
                ? new Color(1f, 1f, 1f, 0.55f)
                : new Color(1f, 0.4f, 0.4f, 0.55f);
        }

        private bool CellOccupied(int cx, int cy)
        {
            var hits = Physics2D.OverlapBoxAll(new Vector2(cx, cy), Vector2.one * 0.4f, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                if (h.GetComponent<WallEntity>() != null) return true;
                if (h.GetComponent<DoorEntity>() != null) return true;
                if (h.GetComponent<TreeEntity>() != null) return true;
                if (h.GetComponent<PawnEntity>() != null) return true;
                if (h.GetComponent<BerryBushEntity>() != null) return true;
                if (h.GetComponent<StoveEntity>() != null) return true;
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
            int cx = Mathf.RoundToInt(mw.x);
            int cy = Mathf.RoundToInt(mw.y);
            if (CellOccupied(cx, cy)) return;
            if (ResourceManager.Instance == null || ResourceManager.Instance.wood < cost) return;
            ResourceManager.Instance.AddWood(-cost);
            Instantiate(prefab, new Vector3(cx, cy, 0), Quaternion.identity);

            var pawns = Object.FindObjectsByType<PawnSkills>(FindObjectsSortMode.None);
            PawnSkills nearest = null;
            float bestSq = float.MaxValue;
            Vector3 here = new Vector3(cx, cy, 0);
            foreach (var p in pawns)
            {
                if (p == null) continue;
                float sq = (p.transform.position - here).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; nearest = p; }
            }
            if (nearest != null) nearest.AddXP(SkillKind.Build, 25f);
        }
    }
}
