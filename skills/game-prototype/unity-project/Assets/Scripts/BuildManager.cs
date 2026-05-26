using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 17: build-mode singleton.  Press B to toggle build mode.
    /// In build mode, clicking on an empty grid cell places a wall
    /// (costs 5 wood from ResourceManager).  Right-click to deselect.
    ///
    /// Generic city-builder mechanic — no game-specific naming.
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        public static BuildManager Instance { get; private set; }

        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private int wallCost = 5;
        [SerializeField] private SpriteRenderer ghostRenderer;
        [SerializeField] private Sprite wallSprite;

        public bool BuildModeActive { get; private set; }

        private Camera cam;

        public void SetRefs(GameObject prefab, Sprite sprite, SpriteRenderer ghost, int cost)
        {
            wallPrefab = prefab;
            wallSprite = sprite;
            ghostRenderer = ghost;
            wallCost = cost;
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
            if (Input.GetKeyDown(KeyCode.B)) ToggleBuildMode();
            if (BuildModeActive && Input.GetMouseButtonDown(1))
            {
                ToggleBuildMode();
                return;
            }
            UpdateGhost();
            if (BuildModeActive && Input.GetMouseButtonDown(0))
            {
                TryPlace();
            }
        }

        private void ToggleBuildMode()
        {
            BuildModeActive = !BuildModeActive;
            if (ghostRenderer != null) ghostRenderer.enabled = BuildModeActive;
        }

        private void UpdateGhost()
        {
            if (!BuildModeActive || ghostRenderer == null || cam == null) return;
            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            int cx = Mathf.RoundToInt(mw.x);
            int cy = Mathf.RoundToInt(mw.y);
            ghostRenderer.transform.position = new Vector3(cx, cy, 0);
            bool canAfford = ResourceManager.Instance != null && ResourceManager.Instance.wood >= wallCost;
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
                if (h.GetComponent<TreeEntity>() != null) return true;
                if (h.GetComponent<PawnEntity>() != null) return true;
                if (h.GetComponent<BerryBushEntity>() != null) return true;
            }
            return false;
        }

        private void TryPlace()
        {
            if (cam == null || wallPrefab == null) return;
            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            int cx = Mathf.RoundToInt(mw.x);
            int cy = Mathf.RoundToInt(mw.y);
            if (CellOccupied(cx, cy)) return;
            if (ResourceManager.Instance == null || ResourceManager.Instance.wood < wallCost) return;
            ResourceManager.Instance.AddWood(-wallCost);
            Instantiate(wallPrefab, new Vector3(cx, cy, 0), Quaternion.identity);
        }
    }
}
