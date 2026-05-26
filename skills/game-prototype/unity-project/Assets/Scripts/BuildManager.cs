using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 17-18: build-mode singleton.  Three modes:
    ///   B: wall (목재 5)
    ///   F: floor (목재 1, no collider, indoor marker)
    ///   G: door (목재 3, trigger collider)
    /// Click to place, right-click to deselect.
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        public static BuildManager Instance { get; private set; }

        public enum Mode { Off, Wall, Floor, Door }
        public Mode CurrentMode { get; private set; } = Mode.Off;
        public bool BuildModeActive => CurrentMode != Mode.Off;

        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject floorPrefab;
        [SerializeField] private GameObject doorPrefab;
        [SerializeField] private int wallCost  = 5;
        [SerializeField] private int floorCost = 1;
        [SerializeField] private int doorCost  = 3;
        [SerializeField] private SpriteRenderer ghostRenderer;
        [SerializeField] private Sprite wallSprite, floorSprite, doorSprite;

        private Camera cam;

        public void SetRefs(GameObject wall, GameObject floor, GameObject door,
                            Sprite wallS, Sprite floorS, Sprite doorS,
                            SpriteRenderer ghost)
        {
            wallPrefab = wall; floorPrefab = floor; doorPrefab = door;
            wallSprite = wallS; floorSprite = floorS; doorSprite = doorS;
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
            ghostRenderer.sprite = m == Mode.Wall ? wallSprite
                                 : m == Mode.Floor ? floorSprite
                                 : doorSprite;
            ghostRenderer.sortingOrder = m == Mode.Floor ? 1 : 20;
        }

        private int CostFor(Mode m) => m switch
        {
            Mode.Wall  => wallCost,
            Mode.Floor => floorCost,
            Mode.Door  => doorCost,
            _ => 0,
        };

        private GameObject PrefabFor(Mode m) => m switch
        {
            Mode.Wall  => wallPrefab,
            Mode.Floor => floorPrefab,
            Mode.Door  => doorPrefab,
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
            // Floors don't count as occupied; can stack walls/doors on top
            // of a floor (typical layout).
            var hits = Physics2D.OverlapBoxAll(new Vector2(cx, cy), Vector2.one * 0.4f, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                if (h.GetComponent<WallEntity>() != null) return true;
                if (h.GetComponent<DoorEntity>() != null) return true;
                if (h.GetComponent<TreeEntity>() != null) return true;
                if (h.GetComponent<PawnEntity>() != null) return true;
                if (h.GetComponent<BerryBushEntity>() != null) return true;
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
        }
    }
}
