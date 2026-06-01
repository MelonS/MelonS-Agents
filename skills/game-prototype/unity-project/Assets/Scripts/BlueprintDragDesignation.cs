using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MelonS.GameProto
{
    /// <summary>
    /// Wave W-M4-05 Lane A / wiki #18 (Dimension 4 건축) — DRAG-RECT BLUEPRINT PLACEMENT.
    ///
    /// Wiki acceptance criterion (#18, genre-comparison.md line 171):
    ///   "Click-drag places a ROW of wall blueprints, not one cell."
    ///
    /// Single-cell placement ALREADY works: BuildManager consumes a left-click in its
    /// own Update and calls DoTryPlaceAt for the one cell under the cursor.  What was
    /// MISSING is the drag-rect / line span: holding the left button and dragging over
    /// a rectangle of cells should drop a blueprint on EVERY covered cell (a 1-wide
    /// drag is the degenerate line/row case).
    ///
    /// This file is a SELF-BOOTSTRAPPING input-mode manager that mirrors the proven
    /// DeconstructDesignation / MineDesignation / GrowZoneDesignation pattern EXACTLY.
    /// It does NOT edit BuildManager — it only calls BuildManager's READ-ONLY public
    /// API (BuildModeActive, CurrentMode, SizeFor, TryPlaceAt).  It activates ONLY
    /// while BuildManager.BuildModeActive is true, so it never fights any of the
    /// designation modes (those turn build mode off when they activate).
    ///
    /// ----------------------------------------------------------------------------
    /// DOUBLE-PLACEMENT SAFETY (why this never edits / collides with BuildManager):
    ///   BuildManager.Update still fires its single-cell place on the SAME left
    ///   mouse-DOWN.  So on a plain tap (no drag) BuildManager places the one anchor
    ///   cell and this manager does NOTHING (a tap < dragThreshold is ignored here).
    ///   On a real DRAG, this manager waits for mouse-UP and then loops EVERY covered
    ///   cell calling BuildManager.TryPlaceAt(cx,cy) once per cell.  The anchor cell
    ///   may already hold the blueprint BuildManager dropped on mouse-DOWN; a repeat
    ///   TryPlaceAt there is harmlessly REJECTED by BuildManager's own placement
    ///   validation (CellOccupied detects the existing BlueprintEntity), so no cell is
    ///   ever double-placed.  Multi-cell buildables (1×2 bed) step by their footprint
    ///   height so footprints tile without overlap.
    ///
    /// ----------------------------------------------------------------------------
    /// DISCIPLINE — mirrors GrowZoneDesignation.cs / MineDesignation.cs EXACTLY:
    ///   - [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] spawns ONE hidden
    ///     DontDestroyOnLoad manager GameObject; FindFirstObjectByType idempotency
    ///     guard means a domain/scene reload never spawns a second manager.
    ///   - It holds NO live singleton reference captured in OnEnable (bug-pattern #7
    ///     firewall).  It READ-ONLY poll-finds BuildManager via its static Instance
    ///     EACH frame and never subscribes to a singleton event with no live ref.
    ///   - All timing/throttling uses Time.unscaledTime deltas inside Update, NOT a
    ///     WaitForSeconds heartbeat coroutine (bug-pattern #9 firewall: no new
    ///     always-on background timer that would freeze on focus loss during a CLI QA
    ///     launch — the only WaitForSeconds/OnEnable tokens in this file are inside
    ///     these firewall-explanation comments, not code).
    ///   - AudioBank.Instance?.PlaySelect() is called READ-ONLY exactly ONCE per
    ///     committed drag (on mouse-UP), never inside the per-cell TryPlaceAt loop, so
    ///     a large rect never machine-guns the SFX (bug-pattern #4 firewall).
    ///
    ///   >>> QA FLAG (scene-wiring): the live drag GHOST preview is a runtime-built
    ///       outline of FOUR thin quad SpriteRenderers (a 1×1 white texture tinted),
    ///       created on the hidden manager GO — NO prefab / NO imported PNG, never
    ///       SceneSetup-wired.  QA should confirm: (1) entering build mode (B/F/etc.),
    ///       then press-drag over the map shows a rectangle outline following the
    ///       cursor; (2) on release a ROW/RECT of blueprints appears (not one cell);
    ///       (3) right-click / ESC mid-drag cancels with NO blueprints placed; (4) a
    ///       plain tap still places exactly one cell (BuildManager's existing path).
    ///       No SceneSetup*.cs file was edited this lane.
    /// </summary>
    public class BlueprintDragDesignation : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tune day-1 feel) -------
        [Header("Drag-rect")]
        // World units the cursor must move before a press becomes a DRAG.  Below this
        //  it is a plain tap → BuildManager's single-cell path handles it, we do nothing.
        [SerializeField] private float dragThreshold = 0.6f;
        // Safety cap so a wild drag across the whole map can't spawn thousands of
        //  blueprints in one frame (the reference sim also caps drag area implicitly).
        [SerializeField] private int maxCellsPerDrag = 400;

        [Header("Ghost preview")]
        [SerializeField] private Color ghostValidColor = new Color(0.55f, 0.85f, 1.0f, 0.85f);
        [SerializeField] private float ghostLineThickness = 0.08f;   // world units
        [SerializeField] private int ghostSortingOrder = 25;         // above ground/ghost(20)

        // ---- runtime state ---------------------------------------------------
        public static BlueprintDragDesignation Instance { get; private set; }

        private Camera cam;

        // Drag state: a left-press-then-drag (past dragThreshold) sweeps a rectangle;
        //  a press-release-without-drag is a tap and is left entirely to BuildManager.
        private bool pressing;          // left button is down inside the map
        private bool dragging;          // press has crossed dragThreshold → it's a drag
        private Vector3 pressStartWorld;

        // The 4 outline quads of the live ghost rect (top/bottom/left/right edges).
        private SpriteRenderer[] ghostEdges;

        // ============================================================
        //  Self-bootstrap — no SceneSetup edit (GrowZoneDesignation pattern).
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindExisting() != null) return;   // idempotent — never a 2nd manager
                var go = new GameObject("~BlueprintDragDesignation");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<BlueprintDragDesignation>();
                    });
        }

        private static BlueprintDragDesignation FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<BlueprintDragDesignation>();
#else
            return Object.FindObjectOfType<BlueprintDragDesignation>();
#endif
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

            var bm = BuildManager.Instance;   // read-only poll-find each frame (no OnEnable capture)

            // Only operate while a build mode is active.  If build mode turns off
            //  mid-drag (ESC, right-click handled by BuildManager, mode switch), abort
            //  the drag with no placement and hide the ghost.
            if (bm == null || !bm.BuildModeActive || cam == null)
            {
                if (pressing || dragging) CancelDrag();
                return;
            }

            // Right-click / ESC cancels an in-progress drag (same convention as the
            //  other designation managers).  BuildManager itself also turns build mode
            //  off on right-click, which the guard above catches next frame — but we
            //  cancel HERE first so no stray cell is placed on the same input.
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelDrag();
                return;
            }

            HandleDragInput(bm);
            UpdateGhost(bm);
        }

        // ---- left-press / drag / release → place a rect/line of blueprints ----

        private void HandleDragInput(BuildManager bm)
        {
            if (Input.GetMouseButtonDown(0))
            {
                // Don't begin a drag if the press started over a UI element (the same
                //  guard BuildManager.HandleLeftClickAt uses for its single click).
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    pressing = false; dragging = false;
                    return;
                }
                pressing = true;
                dragging = false;
                pressStartWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            }
            else if (pressing && Input.GetMouseButton(0))
            {
                // Promote a press to a drag once it has moved past the threshold.
                Vector3 now = cam.ScreenToWorldPoint(Input.mousePosition);
                if (!dragging && Vector3.Distance(now, pressStartWorld) >= dragThreshold)
                    dragging = true;
            }
            else if (pressing && Input.GetMouseButtonUp(0))
            {
                Vector3 endWorld = cam.ScreenToWorldPoint(Input.mousePosition);
                if (dragging)
                    CommitRect(bm, pressStartWorld, endWorld);
                // else: a plain tap → BuildManager's single-cell path already placed it.
                pressing = false;
                dragging = false;
                HideGhost();
            }
        }

        /// <summary>QA / test entry point: place a rect of blueprints spanning two
        /// world points (no real pointer / overUI guard needed).  Returns the number
        /// of cells where a blueprint was actually placed.</summary>
        public int SimulateDragRect(Vector2 worldA, Vector2 worldB)
        {
            var bm = BuildManager.Instance;
            if (bm == null || !bm.BuildModeActive) return 0;
            return CommitRect(bm, worldA, worldB);
        }

        /// <summary>QA / test entry point: place a rect of blueprints spanning two
        /// grid cells inclusive.  Returns the number of cells placed.</summary>
        public int SimulateDragCells(Vector2Int a, Vector2Int b)
        {
            var bm = BuildManager.Instance;
            if (bm == null || !bm.BuildModeActive) return 0;
            return PlaceRectCells(bm,
                Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>Loop every cell covered by the world-space rectangle [a,b] and
        /// place one blueprint per cell via BuildManager.TryPlaceAt (read-only API).
        /// A blueprint already present on a cell (e.g. the anchor cell BuildManager
        /// placed on mouse-DOWN) is harmlessly rejected by BuildManager's own
        /// validation, so no cell is double-placed.  Plays ONE select blip for the
        /// whole commit (firewall #4 — never inside the loop).</summary>
        private int CommitRect(BuildManager bm, Vector3 a, Vector3 b)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x));
            int x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y));
            int y1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y));
            return PlaceRectCells(bm, x0, y0, x1, y1);
        }

        private int PlaceRectCells(BuildManager bm, int x0, int y0, int x1, int y1)
        {
            // Step by the buildable's footprint so multi-cell items (1×2 bed) tile
            //  without overlapping; 1×1 walls/floors step by 1 → the dense row/rect.
            Vector2Int size = BuildManager.SizeFor(bm.CurrentMode);
            int stepX = Mathf.Max(1, size.x);
            int stepY = Mathf.Max(1, size.y);

            int placed = 0;
            int visited = 0;
            for (int cx = x0; cx <= x1; cx += stepX)
            {
                for (int cy = y0; cy <= y1; cy += stepY)
                {
                    if (++visited > maxCellsPerDrag)   // safety cap (firewall: no runaway loop)
                    {
                        Debug.LogWarning($"[BlueprintDrag] drag exceeded {maxCellsPerDrag} cells — capped");
                        FinishCommit(placed);
                        return placed;
                    }
                    if (bm.TryPlaceAt(cx, cy)) placed++;   // read-only API; rejects occupied cells
                }
            }
            FinishCommit(placed);
            return placed;
        }

        private void FinishCommit(int placed)
        {
            if (placed > 0)
            {
                // ONE blip for the whole drag — never per cell (firewall #4).
                AudioBank.Instance?.PlaySelect();
                Debug.Log($"[BlueprintDrag] drag placed {placed} blueprint(s)");
            }
        }

        private void CancelDrag()
        {
            pressing = false;
            dragging = false;
            HideGhost();
        }

        // ============================================================
        //  Live ghost rect — a 4-edge outline that follows the cursor while dragging.
        //  Runtime-built quads (no prefab / no PNG) parented to the hidden manager GO.
        // ============================================================
        private void UpdateGhost(BuildManager bm)
        {
            if (!dragging)
            {
                HideGhost();
                return;
            }

            Vector3 now = cam.ScreenToWorldPoint(Input.mousePosition);
            int x0 = Mathf.FloorToInt(Mathf.Min(pressStartWorld.x, now.x));
            int x1 = Mathf.FloorToInt(Mathf.Max(pressStartWorld.x, now.x));
            int y0 = Mathf.FloorToInt(Mathf.Min(pressStartWorld.y, now.y));
            int y1 = Mathf.FloorToInt(Mathf.Max(pressStartWorld.y, now.y));

            // World-space rect spanning whole cells (cell c covers [c, c+1)).
            float left = x0, bottom = y0, right = x1 + 1f, top = y1 + 1f;
            float w = right - left, h = top - bottom;
            float cxw = (left + right) * 0.5f, cyw = (bottom + top) * 0.5f;

            EnsureGhostEdges();
            float t = ghostLineThickness;
            // edges: 0=bottom 1=top 2=left 3=right
            PlaceEdge(ghostEdges[0], cxw, bottom + t * 0.5f, w, t);
            PlaceEdge(ghostEdges[1], cxw, top - t * 0.5f, w, t);
            PlaceEdge(ghostEdges[2], left + t * 0.5f, cyw, t, h);
            PlaceEdge(ghostEdges[3], right - t * 0.5f, cyw, t, h);
            foreach (var e in ghostEdges) { e.color = ghostValidColor; e.enabled = true; }
        }

        private void EnsureGhostEdges()
        {
            if (ghostEdges != null && ghostEdges[0] != null) return;
            ghostEdges = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"DragGhostEdge_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = WhiteSprite();
                sr.sortingOrder = ghostSortingOrder;
                sr.enabled = false;
                ghostEdges[i] = sr;
            }
        }

        // One edge quad: a unit white sprite scaled to (w,h) and positioned at (x,y).
        private static void PlaceEdge(SpriteRenderer sr, float x, float y, float w, float h)
        {
            sr.transform.position = new Vector3(x, y, 0f);
            sr.transform.localScale = new Vector3(Mathf.Max(0.001f, w), Mathf.Max(0.001f, h), 1f);
        }

        private void HideGhost()
        {
            if (ghostEdges == null) return;
            foreach (var e in ghostEdges) if (e != null) e.enabled = false;
        }

        // A shared 1×1 white sprite (pixelsPerUnit 1 → 1 world unit), tinted per edge.
        private static Sprite sharedWhiteSprite;
        private static Sprite WhiteSprite()
        {
            if (sharedWhiteSprite != null) return sharedWhiteSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            sharedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 1f);
            return sharedWhiteSprite;
        }
    }
}
