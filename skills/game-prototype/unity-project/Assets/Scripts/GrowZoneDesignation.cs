using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonS.GameProto.AI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Wave W-M4-03 Lane A / wiki #17 (Dimension 4 건축) — GROW-ZONE DESIGNATION.
    ///
    /// Wiki acceptance criterion (#17):
    ///   "Painting a grow zone on dirt → crops appear and grow there."
    ///
    /// The crop MECHANIC already exists end-to-end: CropEntity grows
    /// (growthPerSecond), ripens (IsRipe), and is right-click harvestable for food;
    /// the starter settlement already spawns CropEntity instances on dirt.  What was
    /// MISSING is the player-facing GROW-ZONE DESIGNATION + auto-plant layer: a "grow
    /// mode" the player toggles, in which a drag-rect (or a single click) over
    /// DIRT/soil cells MARKS a grow zone, after which idle pawns are dispatched to the
    /// designated cells to plant a fresh CropEntity on each.
    ///
    /// This file mirrors the just-shipped MineDesignation.cs / DeconstructDesignation
    /// .cs EXACTLY (W-M4-02 / W-M4-01 Lane A, wiki #16 / #15) — same self-bootstrap
    /// discipline, same self-attached Canvas toggle button, same runtime marker, same
    /// throttled idle-worker dispatch, same drag-rect MarkRect / single-click MarkCell
    /// — but targets DIRT cells + plants CropEntity instead of marking veins/structures.
    ///
    /// It introduces NO new pathfinding: the plant dispatch reuses the EXISTING
    /// PawnMovement adjacent-stand-cell reservation (TryReserveWorkStandPos) + the
    /// SAME movement/path the build/haul/mine workers use, and a Time-based give-up
    /// in the spirit of WorkGiveUp.  No dedicated grower/farmer pawn class exists, so
    /// — exactly the way MineDesignation dispatches via PawnMiner.SetVeinTarget but
    /// WITHOUT editing the utility-AI files — this manager owns the whole plant
    /// work-cycle (reserve → walk → plant) for an idle PawnEntity directly, editing no
    /// pawn/AI file.  CropEntity.cs is NOT edited: a freshly AddComponent'd CropEntity
    /// starts at growth 0 (the [SerializeField] default) and grows on its own, and the
    /// new crop's sprite is copied READ-ONLY off an existing scene CropEntity.
    ///
    /// ----------------------------------------------------------------------------
    /// DISCIPLINE — mirrors MineDesignation.cs / DeconstructDesignation.cs EXACTLY:
    ///   - [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] spawns ONE hidden
    ///     DontDestroyOnLoad manager GameObject; FindFirstObjectByType idempotency
    ///     guard means a domain/scene reload never spawns a second manager.
    ///   - It holds NO live singleton reference captured in OnEnable (bug-pattern #7
    ///     firewall).  It READ-ONLY poll-finds live managers each frame (BuildManager
    ///     / DeconstructDesignation / MineDesignation / Canvas / PawnEntities) and
    ///     re-finds if they are replaced — never subscribes to a singleton event with
    ///     no live ref.
    ///   - Expiry / timing uses Time.unscaledTime deltas inside Update, NOT a
    ///     WaitForSeconds heartbeat coroutine (bug-pattern #9 firewall: no new
    ///     always-on background timer that would freeze on focus loss during a CLI
    ///     QA launch — the only WaitForSeconds/OnEnable tokens in this file are inside
    ///     these firewall-explanation comments, not code).
    ///   - AudioBank.Instance?.PlaySelect() is called read-only, once per newly-marked
    ///     cell + once per toggle click only (PlaySelect self-throttles inside
    ///     AudioBank) — never a tight per-cell loop buzz (bug-pattern #4 firewall);
    ///     an idempotent re-mark of an already-designated cell is silent.
    ///
    /// MUTUAL EXCLUSION (one left-click never fires two handlers):
    ///   Grow mode is mutually exclusive with the BuildManager build mode, the
    ///   DeconstructDesignation deconstruct mode AND the MineDesignation mine mode.
    ///   Entering grow mode cancels the other three (via their existing read-only
    ///   SetMode APIs); if any of the other three becomes active, grow mode leaves.
    ///   So a single left-click is consumed by exactly one designation handler.
    ///   Right-click / ESC cancels grow mode (same convention as build/deconstruct/mine).
    ///
    /// MODE-TOGGLE ROUTING (lane hot-file budget):
    ///   Like MineDesignation / DeconstructDesignation (both proved ZERO SceneSetup
    ///   budget works), this manager self-attaches its toggle button onto the EXISTING
    ///   screen Canvas (it only parents a new child, edits no existing UI) and binds a
    ///   FREE hotkey — P (Plant) — chosen to avoid every taken key (B/F/G/T/Y build,
    ///   N/R, X deconstruct, M mine).  So this wave spends ZERO of its SceneSetup
    ///   hot-file budget.
    ///
    ///   >>> QA FLAG (scene-wiring): the grow toggle button is created at runtime on
    ///       the first Canvas found (bottom-center, offset further right of the Mine
    ///       button) and the per-cell zone marker is a runtime-built tinted quad
    ///       sprite (no prefab / no imported PNG).  Both are code-generated, never
    ///       SceneSetup-wired — QA should confirm the button appears, toggles grow
    ///       mode (mutually exclusive with build/deconstruct/mine), the P hotkey
    ///       toggles it, the green zone tint shows on designated dirt cells (and
    ///       non-dirt water/rock/occupied cells are rejected), and an idle colonist
    ///       walks to a designated empty cell and a CropEntity appears + grows there.
    ///       No SceneSetup*.cs file was edited this lane.
    /// </summary>
    public class GrowZoneDesignation : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tune day-1 feel) -------
        [Header("Dispatch")]
        [SerializeField] private float dispatchInterval = 0.5f;   // how often we assign idle planters
        [SerializeField] private float plantRange = 1.5f;         // in-range to plant (matches build/mine range)
        [SerializeField] private float plantGiveUpSec = 12f;      // abandon a walk that stalls (WorkGiveUp spirit)

        [Header("Drag-rect")]
        [SerializeField] private float dragThreshold = 0.35f;     // world units before a click becomes a drag

        [Header("Zone marker")]
        [SerializeField] private Color zoneTint = new Color(0.42f, 0.72f, 0.30f, 0.28f);  // subtle sprout-green
        [SerializeField] private int markerSortingOrder = 2;      // below crops(3)/pawns, above ground

        // ── operator fb #1 (2026-05-31): the standalone "경작 (P)" toggle button
        //   was REMOVED.  In RimWorld, Grow zone lives inside the Architect menu's
        //   Zone (구역) category, not as a standalone screen button.  The grow-zone
        //   DESIGNATION logic below is unchanged and is now invoked from
        //   ArchitectMenu (Zone → 경작 → GrowZoneDesignation.Instance.SetMode(true)).
        //   The P HOTKEY is preserved.  All toggle-button UI fields / builders /
        //   RefreshToggleVisual were deleted.

        // ---- runtime state ---------------------------------------------------
        public static GrowZoneDesignation Instance { get; private set; }

        /// <summary>True while the player is in grow designation mode.</summary>
        public bool ModeActive { get; private set; }

        /// <summary>운영자 fb (2026-06-01) "다른 영역도 제거하는 기능 필요" — true 면 같은
        /// drag-rect 가 경작 영역을 '지우는' ERASE 모드.  add 경로와 대칭: 같은 입력/드래그
        /// 로직을 타되 MarkCellAt 대신 EraseCellAt 을 호출해 designated 셀의 마커를
        /// 제거하고 zone 기록을 드롭한다.  이미 심어진 CropEntity 는 건드리지 않음(designation
        /// 만 제거 — RimWorld Zone Clear 와 동일).  ModeActive 안에서만 의미가 있다.</summary>
        public bool EraseMode { get; private set; }

        private Camera cam;
        private float lastDispatch = -999f;

        // Designated cells awaiting / undergoing planting.  Keyed by cell so a
        //  re-mark is idempotent.  Each record self-prunes once a CropEntity exists
        //  on its cell (planted by us OR already there).
        private readonly Dictionary<Vector2Int, GrowCell> zone = new Dictionary<Vector2Int, GrowCell>(64);

        // Active plant jobs: an idle pawn walking to a designated cell to plant.
        //  Owned ENTIRELY by this manager (no pawn/AI file edited) — reuses the
        //  EXISTING PawnMovement pathing + adjacent-stand-cell reservation.
        private readonly List<PlantJob> jobs = new List<PlantJob>(8);

        // Drag-rect state: a left-press-then-drag sweeps a rectangle of cells; a
        //  press-release-without-drag is treated as a single click (one cell).
        private bool dragging;
        private Vector3 dragStartWorld;

        // Cached crop sprite/sort, borrowed READ-ONLY off the first scene CropEntity
        //  so freshly-planted crops match the starter farm's look.  Null is fine —
        //  CropEntity falls back to its own color-tint path when no sprite is set.
        private Sprite cropSpriteCache;
        private int cropSortCache = 3;
        private bool cropSpriteResolved;

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
                var go = new GameObject("~GrowZoneDesignation");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<GrowZoneDesignation>();
                    });
        }

        private static GrowZoneDesignation FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<GrowZoneDesignation>();
#else
            return Object.FindObjectOfType<GrowZoneDesignation>();
#endif
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            cam = Camera.main;
        }

        // ---- mode control ----------------------------------------------------

        /// <summary>Enter/leave grow mode.  Mutually exclusive with the BuildManager
        /// build mode, the DeconstructDesignation deconstruct mode AND the
        /// MineDesignation mine mode, so the four click-handlers never fight over one
        /// left-click.  Cancels the other three on entry via their existing read-only
        /// SetMode APIs (no edit to those files).</summary>
        public void SetMode(bool on) => SetModeInternal(on, erase: false);

        /// <summary>운영자 fb (2026-06-01): 경작 영역을 '지우는' 모드로 진입.  add 모드와
        /// 같은 drag-rect 입력을 쓰되 designation 을 제거한다(심어진 작물은 보존).
        /// ArchitectMenu Zone → 경작 제거 행(또는 Shift+P 토글)에서 호출.</summary>
        public void SetEraseMode(bool on) => SetModeInternal(on, erase: true);

        private void SetModeInternal(bool on, bool erase)
        {
            ModeActive = on;
            EraseMode = on && erase;
            if (on)
            {
                if (BuildManager.Instance != null && BuildManager.Instance.BuildModeActive)
                    BuildManager.Instance.SetMode(BuildManager.Mode.Off);
                if (DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive)
                    DeconstructDesignation.Instance.SetMode(false);
                if (MineDesignation.Instance != null && MineDesignation.Instance.ModeActive)
                    MineDesignation.Instance.SetMode(false);
            }
            if (!on) { dragging = false; }
        }

        public void Toggle() => SetMode(!ModeActive);

        private void Update()
        {
            if (cam == null) cam = Camera.main;

            // Hotkey P (Plant) — free key (build B/F/G/T/Y, N/R, deconstruct X, mine M
            //  are all taken).  Shift+P toggles the ERASE submode (운영자 fb: 경작 영역
            //  제거); plain P is add.
            if (Input.GetKeyDown(KeyCode.P))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shift) { if (ModeActive && EraseMode) SetEraseMode(false); else SetEraseMode(true); }
                else       { if (ModeActive && !EraseMode) SetMode(false);     else SetMode(true); }
            }

            // If a build / deconstruct / mine mode was entered elsewhere, leave grow
            //  mode so they never fight over the same left-click.
            if (ModeActive)
            {
                bool buildOn = BuildManager.Instance != null && BuildManager.Instance.BuildModeActive;
                bool deconOn = DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive;
                bool mineOn  = MineDesignation.Instance != null && MineDesignation.Instance.ModeActive;
                if (buildOn || deconOn || mineOn) SetMode(false);
            }

            if (ModeActive)
            {
                // Right-click / ESC cancels the mode (same convention as build).
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    SetMode(false);
                }
                else
                {
                    HandleDragInput();
                }
            }

            PruneZone();
            DispatchToIdlePlanters();
            UpdatePlantJobs();
        }

        // ---- left-press / drag / release → designate dirt cell(s) ------------

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

        /// <summary>QA / test entry point: designate the cell under a screen position
        /// (no overUI guard).  Returns the GrowCell created, or null if the cell is
        /// non-dirt (water/rock/occupied) and was rejected.</summary>
        public GrowCell SimulateClick(Vector2 screenPos)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return null;
            return MarkCell(cam.ScreenToWorldPoint(screenPos));
        }

        /// <summary>QA / test entry point: drag-select a world-space rectangle and
        /// designate every DIRT cell inside it.  Returns the count designated.</summary>
        public int SimulateDragRect(Vector2 worldA, Vector2 worldB) => MarkRect(worldA, worldB);

        /// <summary>QA / test entry point: designate a specific cell directly.</summary>
        public GrowCell DesignateCell(Vector2Int cell) => MarkCellAt(cell.x, cell.y);

        // ---- marking ---------------------------------------------------------

        private GrowCell MarkCell(Vector3 world)
        {
            int cx = Mathf.FloorToInt(world.x);
            int cy = Mathf.FloorToInt(world.y);
            if (EraseMode) { EraseCellAt(cx, cy); return null; }
            return MarkCellAt(cx, cy);
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
                    if (EraseMode ? EraseCellAt(cx, cy) : (MarkCellAt(cx, cy) != null)) n++;
            return n;
        }

        /// <summary>운영자 fb (2026-06-01): erase a grow-zone designation on cell (cx,cy).
        /// Symmetric to MarkCellAt — destroys the tinted marker, drops the zone record,
        /// and cancels any in-flight plant job aimed at that cell so a pawn doesn't plant
        /// on an un-designated cell.  Does NOT touch an already-planted CropEntity (the
        /// designation is removed, the crop keeps growing — RimWorld Zone Clear).  Returns
        /// true if a designation was actually removed (idempotent: erasing a non-zone cell
        /// is a silent no-op).</summary>
        public bool EraseCellAt(int cx, int cy)
        {
            var key = new Vector2Int(cx, cy);
            if (!zone.TryGetValue(key, out var gc)) return false;   // not designated — no-op
            if (gc != null && gc.Marker != null) Destroy(gc.Marker.gameObject);
            zone.Remove(key);

            // Cancel an in-flight plant job aimed at this cell (release its reservations
            //  via the existing EndJob path) so no pawn plants on the now-cleared cell.
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                if (jobs[i].Cell == key)
                {
                    if (jobs[i].Movement != null) jobs[i].Movement.ClearTarget();
                    EndJob(jobs[i]);
                    jobs.RemoveAt(i);
                }
            }

            AudioBank.Instance?.PlaySelect();
            ClickEffect.Spawn(new Vector3(cx + 0.5f, cy + 0.5f, 0f),
                new Color(0.70f, 0.55f, 0.30f, 0.95f)); // dirt-brown = de-designated
            Debug.Log($"[Grow] erased grow-zone designation at ({cx},{cy})");
            return true;
        }

        /// <summary>QA / test entry point: erase a grow-zone designation directly.</summary>
        public bool EraseCell(Vector2Int cell) => EraseCellAt(cell.x, cell.y);

        /// <summary>Designate cell (cx,cy) as grow zone if it is plantable DIRT/soil.
        /// REJECTS non-dirt the way build-placement validation rejects water/rock:
        /// a cell is plantable only if it is NOT impassable terrain (water/rock — the
        /// same PawnMovement.IsBlockedAt raw-tilemap guard BuildManager.TerrainBlocked
        /// uses), is in-bounds + walkable on the PathGrid, and is not occupied by a
        /// structure/blueprint or an existing crop.  Idempotent: a re-mark returns the
        /// existing GrowCell.</summary>
        private GrowCell MarkCellAt(int cx, int cy)
        {
            var key = new Vector2Int(cx, cy);
            if (zone.TryGetValue(key, out var existing) && existing != null)
                return existing;                       // idempotent re-mark (silent)

            if (!IsPlantableCell(cx, cy)) return null; // reject water/rock/occupied

            var gc = new GrowCell(key, MakeZoneMarker(cx, cy));
            zone[key] = gc;
            // wiki #5 reuse: a single PlaySelect blip per newly-designated cell
            //  (throttled inside AudioBank; never a tight-loop buzz — firewall #4).
            AudioBank.Instance?.PlaySelect();
            ClickEffect.Spawn(new Vector3(cx + 0.5f, cy + 0.5f, 0f),
                new Color(0.55f, 0.80f, 0.38f, 0.95f)); // sprout-green
            Debug.Log($"[Grow] designated dirt cell ({cx},{cy}) as grow zone");
            return gc;
        }

        /// <summary>True if (cx,cy) is plantable soil — NOT water/rock, walkable, and
        /// free of structures/blueprints/crops.  Mirrors the build-placement reject
        /// philosophy (water/rock rejected) but for the planting case soil is the
        /// POSITIVE: any non-blocked, unoccupied walkable ground accepts a crop, the
        /// way the starter settlement plants on plain dirt.</summary>
        public bool IsPlantableCell(int cx, int cy)
        {
            Vector2 center = new Vector2(cx + 0.5f, cy + 0.5f);

            // Reject impassable terrain (water/rock) — the EXACT raw-tilemap guard
            //  BuildManager.TerrainBlocked reuses.  A null tilemap (pure unit-test
            //  scene) returns false → not blocked, so isolated scenes don't false-reject.
            if (PawnMovement.IsBlockedAt(center)) return false;

            // Must be a walkable PathGrid cell (in-bounds, not a wall/blocker).  When
            //  no grid exists (legacy/headless), Grid is null → accept (don't false-reject).
            if (PawnMovement.Grid != null && !PawnMovement.Grid.IsWalkable(new Vector2Int(cx, cy)))
                return false;

            // Reject a cell already holding a structure / blueprint / existing crop —
            //  we don't plant on top of built things.
            var hits = Physics2D.OverlapBoxAll(center, Vector2.one * 0.45f, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var go = h.gameObject;
                if (go.GetComponent<CropEntity>() != null) return false;     // crop already here
                if (go.GetComponent<WallEntity>() != null) return false;
                if (go.GetComponent<DoorEntity>() != null) return false;
                if (go.GetComponent<StoveEntity>() != null) return false;
                if (go.GetComponent<BedEntity>() != null) return false;
                if (go.GetComponent<BlueprintEntity>() != null) return false;
                if (go.GetComponent<StoneVeinEntity>() != null) return false;
            }
            return true;
        }

        /// <summary>Build a subtle tinted quad marking a designated grow-zone cell.
        /// Runtime sprite (1×1 white texture tinted green) — no prefab / imported PNG
        /// (flagged for QA).</summary>
        private SpriteRenderer MakeZoneMarker(int cx, int cy)
        {
            var go = new GameObject($"GrowZone_{cx}_{cy}");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(cx + 0.5f, cy + 0.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ZoneSprite();
            sr.color = zoneTint;
            sr.sortingOrder = markerSortingOrder;   // below crops/pawns, above ground
            return sr;
        }

        // A shared 1×1 white sprite for all zone markers (tinted per-renderer).
        private static Sprite sharedZoneSprite;
        private static Sprite ZoneSprite()
        {
            if (sharedZoneSprite != null) return sharedZoneSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            sharedZoneSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 1f);   // pixelsPerUnit 1 → sprite is 1 world unit
            return sharedZoneSprite;
        }

        /// <summary>Drop a designated cell that already has a crop (planted by us OR
        /// already present), whose marker died, or whose cell turned non-plantable.</summary>
        private void PruneZone()
        {
            if (zone.Count == 0) return;
            List<Vector2Int> dead = null;
            foreach (var kv in zone)
            {
                var gc = kv.Value;
                if (gc == null || gc.Marker == null || CellHasCrop(kv.Key))
                    (dead ??= new List<Vector2Int>()).Add(kv.Key);
            }
            if (dead != null)
                foreach (var k in dead)
                {
                    if (zone.TryGetValue(k, out var gc) && gc != null && gc.Marker != null)
                        Destroy(gc.Marker.gameObject);
                    zone.Remove(k);
                }
        }

        private static bool CellHasCrop(Vector2Int cell)
        {
            Vector2 center = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            var hits = Physics2D.OverlapBoxAll(center, Vector2.one * 0.45f, 0f);
            foreach (var h in hits)
                if (h != null && h.GetComponent<CropEntity>() != null) return true;
            return false;
        }

        // ---- dispatch to idle planters (read-only poll, like MineDesignation) -----
        //  We do NOT touch PawnUtilityAI / PawnContext / PawnActions and there is NO
        //  dedicated grower pawn, so this manager dispatches an idle PawnEntity
        //  DIRECTLY — exactly the way MineDesignation hands a vein to a PawnMiner, but
        //  it owns the whole plant work-cycle here (reserve adjacent stand cell → walk
        //  → plant) using the EXISTING PawnMovement pathing.  No new pathfinding.
        //  Throttled to dispatchInterval so it isn't a per-frame FindObjects scan.
        private void DispatchToIdlePlanters()
        {
            if (Time.unscaledTime - lastDispatch < dispatchInterval) return;
            lastDispatch = Time.unscaledTime;
            if (zone.Count == 0) return;

            // Find designated cells not already claimed by an in-flight plant job and
            //  not already cell-reserved (a worker walking there for some other job).
            foreach (var kv in zone)
            {
                var key = kv.Value.Cell;
                if (CellClaimed(key)) continue;
                if (ReservationManager.IsCellReservedByOther(key, gameObject)) continue;

                var pawn = FindNearestIdlePawn(new Vector2(key.x + 0.5f, key.y + 0.5f));
                if (pawn == null) return;   // no idle pawn → try again next tick

                StartPlantJob(pawn, key);
            }
        }

        private bool CellClaimed(Vector2Int cell)
        {
            foreach (var j in jobs) if (j.Cell == cell) return true;
            return false;
        }

        private bool PawnBusyInJob(PawnEntity pawn)
        {
            foreach (var j in jobs) if (j.Pawn == pawn) return true;
            return false;
        }

        /// <summary>Find the nearest idle colonist to a target cell.  "Idle" mirrors
        /// the EXACT gate set PawnUtilityAI uses before it picks new work: no worker
        /// component reports HasTask, the pawn isn't moving, and it isn't drafted /
        /// under manual control / sleeping / breaking.  Also skips a pawn already in
        /// one of our plant jobs.  Read-only — edits no pawn/AI file.</summary>
        private PawnEntity FindNearestIdlePawn(Vector2 target)
        {
            var pawns = FindPawns();
            if (pawns == null || pawns.Length == 0) return null;

            PawnEntity best = null;
            float bestSq = float.MaxValue;
            foreach (var p in pawns)
            {
                if (p == null) continue;
                if (PawnBusyInJob(p)) continue;
                if (!PawnIdle(p)) continue;
                float sq = ((Vector2)p.transform.position - target).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p; }
            }
            return best;
        }

        /// <summary>True if the pawn is genuinely idle and available to plant — mirrors
        /// PawnUtilityAI's pre-Decide gates (read-only): not drafted / manual /
        /// sleeping / breaking, not moving, and no worker component has a task.</summary>
        private static bool PawnIdle(PawnEntity p)
        {
            if (p.IsDrafted || p.IsUnderManualControl) return false;

            var needs = p.GetComponent<PawnNeeds>();
            if (needs != null && (needs.IsSleeping || needs.IsBreaking)) return false;

            var movement = p.GetComponent<PawnMovement>();
            if (movement == null) return false;          // can't plant without movement
            if (movement.IsMoving) return false;

            // Every worker component must be task-free (the same set PawnUtilityAI
            //  checks before choosing new work).
            var chopper = p.GetComponent<PawnChopper>();   if (chopper != null && chopper.HasTask) return false;
            var gatherer = p.GetComponent<PawnGatherer>(); if (gatherer != null && gatherer.HasTask) return false;
            var hunter = p.GetComponent<PawnHunter>();     if (hunter != null && hunter.HasTask) return false;
            var cook = p.GetComponent<PawnCook>();         if (cook != null && cook.HasTask) return false;
            var hauler = p.GetComponent<PawnHauler>();     if (hauler != null && hauler.HasTask) return false;
            var builder = p.GetComponent<PawnBuilder>();   if (builder != null && builder.HasTask) return false;
            var miner = p.GetComponent<PawnMiner>();       if (miner != null && miner.HasTask) return false;
            var doctor = p.GetComponent<PawnDoctor>();     if (doctor != null && doctor.HasTask) return false;
            return true;
        }

        // ---- plant job lifecycle (reuses PawnMovement pathing; no new pathfinding) -

        private void StartPlantJob(PawnEntity pawn, Vector2Int cell)
        {
            var movement = pawn.GetComponent<PawnMovement>();
            if (movement == null) return;

            // Reserve an adjacent walkable stand cell next to the target cell, exactly
            //  the way PawnMiner/PawnBuilder reserve theirs (TryReserveWorkStandPos),
            //  then walk there.  No new pathfinding — the existing movement does it.
            Vector2Int standCell = PawnMovement.INVALID_CELL;
            Vector2 targetWorld = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            if (!PawnMovement.TryReserveWorkStandPos(targetWorld, Vector2Int.one,
                    pawn.transform.position, pawn.gameObject, ref standCell, out Vector2 stand))
            {
                // No free adjacent stand cell (unreachable/occupied) — try again later.
                Debug.Log($"[Grow] no free stand cell next to ({cell.x},{cell.y}) — defer");
                return;
            }
            // Also reserve the planting cell itself so a second dispatch / worker
            //  doesn't claim it (released when the job ends).
            ReservationManager.TryReserveCell(cell, pawn.gameObject);
            movement.SetTarget(stand);

            jobs.Add(new PlantJob
            {
                Pawn = pawn,
                Movement = movement,
                Cell = cell,
                StandCell = standCell,
                StartTime = Time.unscaledTime,
            });
            Debug.Log($"[Grow] dispatched {pawn.name} to plant at ({cell.x},{cell.y})");
        }

        /// <summary>Advance in-flight plant jobs: when the pawn reaches its reserved
        /// adjacent stand cell (or is in plant range), spawn a CropEntity on the cell
        /// and end the job.  Abandon a job whose pawn went away, whose cell got a crop
        /// some other way, or that stalls past plantGiveUpSec (WorkGiveUp spirit, via
        /// Time.unscaledTime — no WaitForSeconds heartbeat, firewall #9).</summary>
        private void UpdatePlantJobs()
        {
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                var j = jobs[i];

                // Pawn gone, or no longer plantable (crop appeared / cell turned bad).
                if (j.Pawn == null || j.Movement == null
                    || CellHasCrop(j.Cell) || !zone.ContainsKey(j.Cell))
                {
                    EndJob(j); jobs.RemoveAt(i); continue;
                }

                // Pawn got drafted / put to sleep / hand-controlled mid-walk → release.
                if (!PawnStillAvailable(j.Pawn))
                {
                    EndJob(j); jobs.RemoveAt(i); continue;
                }

                Vector2 cellWorld = new Vector2(j.Cell.x + 0.5f, j.Cell.y + 0.5f);
                float dist = Vector2.Distance(j.Pawn.transform.position, cellWorld);

                if (dist <= plantRange || j.Movement.AtStandCell(j.StandCell))
                {
                    j.Movement.ClearTarget();
                    PlantCropAt(j.Cell);
                    EndJob(j); jobs.RemoveAt(i); continue;
                }

                // Give up a stalled walk (unreachable / path keeps failing) so the cell
                //  frees up for another pawn — Time.unscaledTime budget, not a coroutine.
                if (Time.unscaledTime - j.StartTime > plantGiveUpSec
                    && (j.Movement.LastPathFailed || !j.Movement.IsMoving))
                {
                    Debug.Log($"[Grow] {j.Pawn.name} give up plant at ({j.Cell.x},{j.Cell.y}) (stalled)");
                    j.Movement.ClearTarget();
                    EndJob(j); jobs.RemoveAt(i); continue;
                }
            }
        }

        private static bool PawnStillAvailable(PawnEntity p)
        {
            if (p.IsDrafted || p.IsUnderManualControl) return false;
            var needs = p.GetComponent<PawnNeeds>();
            if (needs != null && (needs.IsSleeping || needs.IsBreaking)) return false;
            return true;
        }

        private void EndJob(PlantJob j)
        {
            if (j.Pawn == null) return;
            ReservationManager.ReleaseCell(j.Cell, j.Pawn.gameObject);
            if (j.StandCell.x != PawnMovement.INVALID_CELL.x)
                ReservationManager.ReleaseCell(j.StandCell, j.Pawn.gameObject);
        }

        // ---- plant a CropEntity on a designated cell (reuses the starter-settlement
        //  crop look READ-ONLY; CropEntity.cs untouched) -----------------------------
        //
        //  A freshly AddComponent'd CropEntity starts at growth 0 (the [SerializeField]
        //  default), so it sprouts and grows on its own with NO accessor needed — the
        //  one thing the task said to add ONLY if genuinely missing was a Spawn/
        //  SetGrowth(0) accessor, and it is NOT needed (the default IS 0).  We copy the
        //  crop sprite + sortingOrder off an existing scene CropEntity READ-ONLY so the
        //  new crop matches the starter farm; if none exists, CropEntity falls back to
        //  its own color-tint path (still grows + is harvestable).
        private void PlantCropAt(Vector2Int cell)
        {
            ResolveCropSprite();   // lazy read-only borrow of an existing crop's sprite

            var go = new GameObject($"Crop_{cell.x}_{cell.y}");
            go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            if (cropSpriteCache != null) sr.sprite = cropSpriteCache;
            sr.sortingOrder = cropSortCache;
            // CropEntity.Awake adds its own BoxCollider2D + starts growth at 0.
            go.AddComponent<CropEntity>();

            ClickEffect.Spawn(go.transform.position, new Color(0.55f, 0.80f, 0.38f, 0.95f));
            Debug.Log($"[Grow] planted crop at ({cell.x},{cell.y}) — grows from 0");

            // The designated cell now has a crop → PruneZone drops the marker next tick.
        }

        private void ResolveCropSprite()
        {
            if (cropSpriteResolved) return;
            cropSpriteResolved = true;
#if UNITY_2023_1_OR_NEWER
            var crops = Object.FindObjectsByType<CropEntity>(FindObjectsSortMode.None);
#else
            var crops = Object.FindObjectsOfType<CropEntity>();
#endif
            foreach (var c in crops)
            {
                if (c == null) continue;
                var sr = c.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    cropSpriteCache = sr.sprite;
                    cropSortCache = sr.sortingOrder;
                    return;
                }
            }
        }

        private static PawnEntity[] FindPawns()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<PawnEntity>();
#endif
        }

        // operator fb #1: the standalone "Btn_경작" toggle button + its
        //  EnsureToggleButton / RefreshToggleVisual / FindCanvas helpers were
        //  removed.  Grow zone is now entered from ArchitectMenu's Zone (구역)
        //  category (or the P hotkey).  The designation logic above is unchanged.

        // ---- nested records --------------------------------------------------

        /// <summary>A designated grow-zone cell: its grid coordinate + its tinted
        /// zone marker renderer.  Lives until a CropEntity exists on the cell, then
        /// PruneZone destroys the marker and drops the record.</summary>
        public class GrowCell
        {
            public Vector2Int Cell { get; }
            public SpriteRenderer Marker { get; }
            public GrowCell(Vector2Int cell, SpriteRenderer marker) { Cell = cell; Marker = marker; }
        }

        /// <summary>An in-flight plant job: an idle pawn walking to a designated cell
        /// to plant a crop, using the EXISTING PawnMovement pathing (no new pathfind).</summary>
        private class PlantJob
        {
            public PawnEntity Pawn;
            public PawnMovement Movement;
            public Vector2Int Cell;
            public Vector2Int StandCell;
            public float StartTime;
        }
    }
}
