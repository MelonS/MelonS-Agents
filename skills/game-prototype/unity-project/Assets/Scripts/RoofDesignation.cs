using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// ROOF wave (운영자: "RimWorld처럼 지붕영역 설정 메뉴 따로 있음") — ROOF AREA DESIGNATION.
    ///
    /// RimWorld concept (필수만, 위키 근사):
    ///   In vanilla RimWorld the player paints a "Roof Area" (지붕 영역) under the
    ///   Architect ZONE/ORDERS group; designated cells become ROOFED.  A roof gives
    ///   the indoor "shade" look (this prototype's visual effect) and carries a
    ///   ROOFED flag for future rain/temperature hooks.  RimWorld also AUTO-ROOFS a
    ///   room fully enclosed by walls; we implement a light version of that (수동
    ///   지정이 필수, auto-roof는 bonus).
    ///
    /// This file is the player-facing DESIGNATION layer.  It MIRRORS the shipped
    /// MineDesignation.cs / GrowZoneDesignation.cs EXACTLY — same self-bootstrap
    /// discipline, same drag-rect MarkRect / single-click MarkCell, same throttled
    /// poll Update, same mutual-exclusion with the other designation modes — but
    /// instead of dispatching a worker it simply MARKS cells as roofed in a shared
    /// cell set that <see cref="RoofOverlayRenderer"/> reads READ-ONLY to draw the
    /// shade overlay.  No worker / pathfinding is involved (roofing is instant in
    /// this prototype — the "build a roof" job is intentionally out of scope/필수만).
    ///
    /// ----------------------------------------------------------------------------
    /// DISCIPLINE — mirrors MineDesignation.cs / GrowZoneDesignation.cs EXACTLY:
    ///   - [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] spawns ONE hidden
    ///     DontDestroyOnLoad manager GameObject behind a GameSceneGate (never on
    ///     MainMenu); FindFirstObjectByType idempotency guard means a domain/scene
    ///     reload never spawns a second manager.
    ///   - Holds NO live singleton reference captured in OnEnable (bug-pattern #7
    ///     firewall): it READ-ONLY poll-finds live managers each frame and re-finds
    ///     if replaced — never subscribes to a singleton event with no live ref.
    ///   - All timing uses Time.unscaledTime deltas inside Update, NOT a
    ///     WaitForSeconds heartbeat coroutine (bug-pattern #9 firewall: no new
    ///     always-on background timer that would freeze on focus loss during a CLI
    ///     QA launch — the only WaitForSeconds/OnEnable tokens in this file are
    ///     inside these firewall comments, not code).
    ///   - AudioBank.Instance?.PlaySelect() is called read-only, once per newly-
    ///     roofed cell + once per auto-roof pass only (PlaySelect self-throttles
    ///     inside AudioBank) — never a tight per-cell loop buzz (bug-pattern #4
    ///     firewall); an idempotent re-mark of an already-roofed cell is silent.
    ///
    /// MUTUAL EXCLUSION (one left-click never fires two handlers):
    ///   Roof mode is mutually exclusive with the BuildManager build mode and the
    ///   Deconstruct / Mine / Grow / Stockpile designation modes.  Entering roof mode
    ///   cancels the others (via their existing read-only SetMode APIs); if any of
    ///   the others becomes active, roof mode leaves.  So a single left-click is
    ///   consumed by exactly one designation handler.  Right-click / ESC cancels
    ///   roof mode (same convention as the others).
    ///
    /// MODE-TOGGLE ROUTING (lane hot-file budget):
    ///   Like the other designations, this manager spends ZERO SceneSetup budget.
    ///   Roof is entered from ArchitectMenu's Zone (구역) category (operator:
    ///   "지붕영역 지정 메뉴 따로 있음") or the FREE hotkey L… (L is taken by Lamp) —
    ///   we bind the FREE key 'U' (roof "Up"; B/F/G/T/Y build, N/R, X deconstruct,
    ///   M mine, P plant, O stockpile, K floor-stone, J table, H, L lamp, E fence,
    ///   and WASD camera are all taken — U is free).
    ///
    ///   >>> QA FLAG (scene-wiring): roof mode is entered from the Architect Zone
    ///       category row (지붕 영역) or the U hotkey; the roofed-cell SHADE overlay
    ///       is drawn by RoofOverlayRenderer (a separate self-bootstrap renderer,
    ///       code-generated tinted quads, no prefab/PNG).  No SceneSetup*.cs file
    ///       was edited this lane.
    /// </summary>
    public class RoofDesignation : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tune day-1 feel) -------
        [Header("Drag-rect")]
        [SerializeField] private float dragThreshold = 0.35f;     // world units before a click becomes a drag

        [Header("Auto-roof (enclosed room)")]
        [SerializeField] private bool autoRoofEnclosed = true;    // auto-roof a wall-enclosed room on designate
        [SerializeField] private int autoRoofMaxCells = 400;      // safety cap so a flood-fill can't run away (open map)

        // ---- runtime state ---------------------------------------------------
        public static RoofDesignation Instance { get; private set; }

        /// <summary>True while the player is in roof designation mode.</summary>
        public bool ModeActive { get; private set; }

        // The shared set of roofed cells.  RoofOverlayRenderer reads this READ-ONLY
        //  (via the public Roofed accessor) to draw the shade overlay; future rain /
        //  temperature systems can read IsRoofed(cell) as the roofed FLAG hook.
        private readonly HashSet<Vector2Int> roofed = new HashSet<Vector2Int>(128);

        /// <summary>Monotonic version bumped whenever the roofed set changes, so the
        /// overlay renderer can cheaply detect "did the roof change since I last
        /// rebuilt?" without diffing the whole set every frame.</summary>
        public int Version { get; private set; }

        private Camera cam;

        // Drag-rect state: a left-press-then-drag sweeps a rectangle of cells; a
        //  press-release-without-drag is treated as a single click (one cell).
        private bool dragging;
        private Vector3 dragStartWorld;

        // ============================================================
        //  Self-bootstrap — no SceneSetup edit (MineDesignation pattern).
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindExisting() != null) return;   // idempotent — never a 2nd manager
                var go = new GameObject("~RoofDesignation");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<RoofDesignation>();
            });
        }

        private static RoofDesignation FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<RoofDesignation>();
#else
            return Object.FindObjectOfType<RoofDesignation>();
#endif
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            cam = Camera.main;
        }

        // ---- public roofed-cell accessors (read by RoofOverlayRenderer + future
        //  rain/temperature hooks) ---------------------------------------------

        /// <summary>True if cell (cx,cy) is currently roofed (the roofed FLAG hook).</summary>
        public bool IsRoofed(int cx, int cy) => roofed.Contains(new Vector2Int(cx, cy));
        public bool IsRoofed(Vector2Int cell) => roofed.Contains(cell);

        /// <summary>READ-ONLY enumeration of the roofed cells, for the overlay
        /// renderer to draw a shade quad on each.  Returns the live set; callers
        /// must not mutate it (they only enumerate).</summary>
        public IReadOnlyCollection<Vector2Int> Roofed => roofed;
        public int RoofedCount => roofed.Count;

        // ---- mode control ----------------------------------------------------

        /// <summary>Enter/leave roof mode.  Mutually exclusive with the BuildManager
        /// build mode and the Deconstruct / Mine / Grow / Stockpile designation modes,
        /// so the click-handlers never fight over one left-click.  Cancels the others
        /// on entry via their existing read-only SetMode APIs (no edit to those files).
        /// </summary>
        public void SetMode(bool on)
        {
            ModeActive = on;
            if (on)
            {
                if (BuildManager.Instance != null && BuildManager.Instance.BuildModeActive)
                    BuildManager.Instance.SetMode(BuildManager.Mode.Off);
                if (DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive)
                    DeconstructDesignation.Instance.SetMode(false);
                if (MineDesignation.Instance != null && MineDesignation.Instance.ModeActive)
                    MineDesignation.Instance.SetMode(false);
                if (GrowZoneDesignation.Instance != null && GrowZoneDesignation.Instance.ModeActive)
                    GrowZoneDesignation.Instance.SetMode(false);
                if (StockpileDesignation.Instance != null && StockpileDesignation.Instance.ModeActive)
                    StockpileDesignation.Instance.SetMode(false);
            }
            if (!on) { dragging = false; }
        }

        public void Toggle() => SetMode(!ModeActive);

        private void Update()
        {
            if (cam == null) cam = Camera.main;

            // Hotkey U (roof "Up") — free key (build B/F/G/T/Y, N/R, deconstruct X,
            //  mine M, plant P, stockpile O, floor-stone K, table J, lamp L, fence E,
            //  H, and WASD camera are all taken).
            if (Input.GetKeyDown(KeyCode.U)) Toggle();

            // If another mode was entered elsewhere, leave roof mode so they never
            //  fight over the same left-click.
            if (ModeActive && AnotherModeActive()) SetMode(false);

            if (ModeActive)
            {
                // Right-click / ESC cancels the mode (same convention as build).
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                    SetMode(false);
                else
                    HandleDragInput();
            }
        }

        private static bool AnotherModeActive()
        {
            if (BuildManager.Instance != null && BuildManager.Instance.BuildModeActive) return true;
            if (DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive) return true;
            if (MineDesignation.Instance != null && MineDesignation.Instance.ModeActive) return true;
            if (GrowZoneDesignation.Instance != null && GrowZoneDesignation.Instance.ModeActive) return true;
            if (StockpileDesignation.Instance != null && StockpileDesignation.Instance.ModeActive) return true;
            return false;
        }

        // ---- left-press / drag / release → roof cell(s) ----------------------

        private void HandleDragInput()
        {
            if (cam == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                // Don't start a drag if the press began over a UI element.
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;
                dragging = true;
                dragStartWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0) && dragging)
            {
                dragging = false;
                Vector3 endWorld = cam.ScreenToWorldPoint(Input.mousePosition);
                if (Vector3.Distance(endWorld, dragStartWorld) < dragThreshold)
                    MarkCell(endWorld);                 // a tap → single cell
                else
                    MarkRect(dragStartWorld, endWorld); // a sweep → rectangle of cells
            }
        }

        // ---- public test / QA entry points (no real pointer needed) ----------

        /// <summary>QA / test entry point: roof the cell under a screen position
        /// (no overUI guard).  Returns the cell roofed.</summary>
        public Vector2Int SimulateClick(Vector2 screenPos)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return Vector2Int.zero;
            return MarkCell(cam.ScreenToWorldPoint(screenPos));
        }

        /// <summary>QA / test entry point: drag-select a world-space rectangle and
        /// roof every cell inside it.  Returns the count newly roofed.</summary>
        public int SimulateDragRect(Vector2 worldA, Vector2 worldB) => MarkRect(worldA, worldB);

        /// <summary>QA / test entry point: roof a specific cell directly.</summary>
        public bool DesignateCell(Vector2Int cell) => AddRoof(cell, playBlip: true, fx: true);

        // ---- marking ---------------------------------------------------------

        private Vector2Int MarkCell(Vector3 world)
        {
            int cx = Mathf.FloorToInt(world.x);
            int cy = Mathf.FloorToInt(world.y);
            var cell = new Vector2Int(cx, cy);
            AddRoof(cell, playBlip: true, fx: true);
            // RimWorld auto-roof: if this single cell completed/enclosed a room, fill it.
            if (autoRoofEnclosed) TryAutoRoofEnclosedFrom(cell);
            return cell;
        }

        private int MarkRect(Vector3 a, Vector3 b)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x));
            int x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y));
            int y1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y));
            int n = 0;
            for (int cx = x0; cx <= x1; cx++)
                for (int cy = y0; cy <= y1; cy++)
                    if (AddRoof(new Vector2Int(cx, cy), playBlip: false, fx: false)) n++;
            if (n > 0)
            {
                // Single throttled blip + one FX at the rect centre for the whole
                //  sweep (never a per-cell tight-loop buzz — bug-pattern #4 firewall).
                AudioBank.Instance?.PlaySelect();
                ClickEffect.Spawn(
                    new Vector3((x0 + x1) * 0.5f + 0.5f, (y0 + y1) * 0.5f + 0.5f, 0f),
                    new Color(0.30f, 0.32f, 0.40f, 0.95f)); // slate shade
                Debug.Log($"[Roof] designated {n} cell(s) as roof area");
            }
            return n;
        }

        /// <summary>Add one cell to the roofed set.  Idempotent (a re-roof of an
        /// already-roofed cell is a silent no-op).  Bumps Version on a real change so
        /// the overlay renderer rebuilds.  Returns true if the cell was newly added.</summary>
        private bool AddRoof(Vector2Int cell, bool playBlip, bool fx)
        {
            if (roofed.Contains(cell)) return false;   // idempotent re-roof (silent)
            roofed.Add(cell);
            Version++;
            if (playBlip) AudioBank.Instance?.PlaySelect();
            if (fx)
            {
                ClickEffect.Spawn(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f),
                    new Color(0.30f, 0.32f, 0.40f, 0.95f)); // slate shade
                Debug.Log($"[Roof] designated cell ({cell.x},{cell.y}) as roof area");
            }
            return true;
        }

        // ---- RimWorld auto-roof of a wall-enclosed room ----------------------
        //  A light version of vanilla's auto-roof: when the player roofs a cell, we
        //  flood-fill from it through walkable, non-wall cells; if that region is
        //  FULLY enclosed by walls/doors (the flood never escapes past
        //  autoRoofMaxCells and never hits the grid edge), every cell in it is
        //  auto-roofed.  If the region is open (flood would run away), we bail and
        //  leave only the manually-painted cells — so 수동 지정만으로도 OK (필수만).
        //  Read-only: uses PawnMovement.IsBlockedAt + the existing wall/door physics
        //  probes; edits no other file.

        private void TryAutoRoofEnclosedFrom(Vector2Int seed)
        {
            // The seed itself sits on a roofable interior cell.  If the seed is a wall
            //  cell there is no interior to fill.
            if (IsWallCell(seed)) return;

            var region = new HashSet<Vector2Int>();
            var stack = new Stack<Vector2Int>();
            stack.Push(seed);
            region.Add(seed);

            while (stack.Count > 0)
            {
                if (region.Count > autoRoofMaxCells) return;   // open / too big → bail (manual only)
                var c = stack.Pop();

                Span<Vector2Int> neighbours = stackalloc Vector2Int[4];
                neighbours[0] = new Vector2Int(c.x + 1, c.y);
                neighbours[1] = new Vector2Int(c.x - 1, c.y);
                neighbours[2] = new Vector2Int(c.x, c.y + 1);
                neighbours[3] = new Vector2Int(c.x, c.y - 1);

                for (int i = 0; i < 4; i++)
                {
                    var n = neighbours[i];
                    if (region.Contains(n)) continue;
                    // A wall/door cell is the enclosure boundary — don't cross it,
                    //  don't add it to the interior region.
                    if (IsWallCell(n)) continue;
                    // An impassable terrain cell (water/rock) also bounds the room.
                    if (IsImpassableTerrain(n)) continue;
                    region.Add(n);
                    stack.Push(n);
                }
            }

            // The flood stayed bounded (≤ cap) without escaping → enclosed room.
            //  Roof every interior cell.
            int added = 0;
            foreach (var c in region)
                if (AddRoof(c, playBlip: false, fx: false)) added++;
            if (added > 0)
            {
                AudioBank.Instance?.PlaySelect();
                Debug.Log($"[Roof] auto-roofed enclosed room ({region.Count} cells, +{added} new)");
            }
        }

        /// <summary>True if the cell holds a wall or (closed/structural) door — the
        /// enclosure boundary for auto-roof.  Read-only physics probe at the cell
        /// centre (the same OverlapBox the other designations use).</summary>
        private static bool IsWallCell(Vector2Int cell)
        {
            Vector2 center = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            var hits = Physics2D.OverlapBoxAll(center, Vector2.one * 0.45f, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var go = h.gameObject;
                if (go.GetComponent<WallEntity>() != null) return true;
                if (go.GetComponent<DoorEntity>() != null) return true;   // a door closes a room
            }
            return false;
        }

        /// <summary>True if the cell is impassable terrain (water/rock) — also a room
        /// boundary.  Uses the EXACT raw-tilemap guard the other designations reuse;
        /// a null tilemap (pure unit-test scene) returns false → not a boundary.</summary>
        private static bool IsImpassableTerrain(Vector2Int cell)
        {
            Vector2 center = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            return PawnMovement.IsBlockedAt(center);
        }
    }
}
