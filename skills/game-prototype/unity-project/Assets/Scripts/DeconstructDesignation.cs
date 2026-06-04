using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonS.GameProto.AI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Wave W-M4-01 Lane A / wiki #15 (Dimension 4 건축, the ★★★ "design a base"
    /// player-felt win) — DECONSTRUCT.
    ///
    /// Wiki acceptance criterion (#15):
    ///   "Designating a built wall → a pawn removes it, refunds material, the cell
    ///    becomes walkable."
    ///
    /// This file is a SELF-BOOTSTRAPPING designation + input-mode manager that lets
    /// the player enter a "deconstruct mode" and click built structures to MARK
    /// them for removal.  It then poll-dispatches the removal to idle builders,
    /// which run the deconstruct ACTION (on PawnBuilder.cs) reusing the existing
    /// build/haul pathing — no new pathfinding.  On completion the structure refunds
    /// ~50% of its material and is destroyed; a destroyed WallEntity reopens its
    /// PathGrid cell via its OWN OnDestroy (PathGrid.SetStructureBlocked false →
    /// ref-count decrement), mirroring exactly how WallEntity.Start INCREMENTED it.
    ///
    /// ----------------------------------------------------------------------------
    /// DISCIPLINE — mirrors AlertStackUI.cs / RainSoundDriver.cs EXACTLY:
    ///   - [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] spawns ONE hidden
    ///     DontDestroyOnLoad manager GameObject; FindFirstObjectByType idempotency
    ///     guard means a domain/scene reload never spawns a second manager.
    ///   - It holds NO live singleton reference captured in OnEnable (bug-pattern
    ///     #7 firewall).  It READ-ONLY poll-finds live managers each frame
    ///     (BuildManager / PawnBuilders) and re-finds if they are replaced — never
    ///     subscribes to a singleton event with no live ref.
    ///   - Expiry / timing uses Time.unscaledTime deltas inside Update, NOT a
    ///     WaitForSeconds heartbeat coroutine (bug-pattern #9 firewall: no new
    ///     always-on background timer that would freeze on focus loss).
    ///
    /// MODE-TOGGLE ROUTING (lane hot-file budget):
    ///   The lane permits AT MOST ONE SceneSetup hot-file edit for the toggle
    ///   button, but PREFERS routing through existing infra.  This manager needs
    ///   NEITHER: like GuiControlBar.EnsureInScene it self-attaches a small toggle
    ///   button onto the EXISTING screen Canvas (read-only: it only parents a new
    ///   child, edits no existing UI), and also binds the X hotkey.  So this wave
    ///   spends ZERO of its SceneSetup hot-file budget.
    ///
    ///   >>> QA FLAG (scene-wiring): the deconstruct toggle button is created at
    ///       runtime on the first Canvas found (bottom-center-left of the control
    ///       bar area) and the designation marker is a runtime-built sprite-less
    ///       quad + "✕" label (no prefab / no imported PNG).  Both are
    ///       code-generated, never SceneSetup-wired — QA should confirm the button
    ///       appears, toggles mode, and the red ✕ marker shows on a clicked wall.
    ///       No SceneSetup*.cs file was edited this lane.
    ///
    /// ----------------------------------------------------------------------------
    /// B7 — AREA-CANCEL / DECONSTRUCT-AREA DRAG (wiki genre-comparison-v2 B7):
    ///   On left-mouse-DOWN in deconstruct mode we record the start world cell and
    ///   show a code-generated rectangular selection quad (a 1×1 white Sprite
    ///   stretched over the box, DANGER_RED translucent — same NO-PREFAB style as
    ///   the ✕ marker).  On left-mouse-UP we sweep EVERY cell in the box and:
    ///     (1) TryMark each deconstructable structure (reusing the EXACT single-
    ///         click TryMark path per cell — no new pathfinding/dispatch; the
    ///         existing DispatchToIdleBuilders loop already handles N targets), and
    ///     (2) CANCEL each pending unbuilt BlueprintEntity (an unbuilt blueprint is
    ///         CANCELLED, not deconstructed — exactly as this file already notes).
    ///   A near-zero drag (< dragClickThreshold) falls back to the existing single-
    ///   cell click, so prior single-click behavior is unchanged.
    ///
    ///   >>> QA FLAG (code-gen overlay): the drag selection rectangle is a runtime
    ///       SpriteRenderer quad (1×1 white texture, no prefab / no imported PNG),
    ///       child of this manager, sortingOrder 29.  QA: drag a box over 3 walls →
    ///       all 3 get a ✕; drag over 3 pending blueprints in cancel mode → all 3
    ///       vanish (B7 binary acceptance).
    ///
    ///   >>> QA FLAG (missing BlueprintEntity cancel API): BlueprintEntity.cs has
    ///       NO public Cancel()/Remove() — its only self-destruct is the PRIVATE
    ///       Complete()→Destroy.  This lane may NOT edit BlueprintEntity.cs, so we
    ///       cancel via the public Unity Destroy(bp.gameObject); stale builder/
    ///       hauler reservations self-release on the next poll's Unity null-check.
    ///       A proper BlueprintEntity.Cancel() (refund already-collected materials +
    ///       proactively release ReservedBy/HaulReservedBy) is FLAGGED for a
    ///       follow-up wave that owns the BlueprintEntity lane.
    /// </summary>
    public class DeconstructDesignation : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tune day-1 feel) -------
        [Header("Dispatch")]
        [SerializeField] private float dispatchInterval = 0.5f;   // how often we assign idle builders
        [SerializeField] private float pickRadius = 0.45f;        // click hit-test box half-extent

        // ── operator fb #1 (2026-05-31): the standalone "해체 (X)" toggle button
        //   was REMOVED.  In the reference sim, Deconstruct lives inside the Architect menu's
        //   Orders (지시) category, not as a standalone screen button.  The
        //   deconstruct DESIGNATION logic below is unchanged and is now invoked from
        //   ArchitectMenu (Orders → 해체 → DeconstructDesignation.Instance.SetMode(true)).
        //   The X HOTKEY is preserved.  All toggle-button UI fields / builders /
        //   RefreshToggleVisual were deleted.

        [Header("Drag-rect (B7 area-cancel / deconstruct-area)")]
        // A drag shorter than this (in world units) on mouse-UP counts as a plain
        //  click → preserves the EXISTING single-cell TryMark behavior exactly.
        [SerializeField] private float dragClickThreshold = 0.35f;

        // ---- runtime state ---------------------------------------------------
        public static DeconstructDesignation Instance { get; private set; }

        /// <summary>True while the player is in deconstruct designation mode.</summary>
        public bool ModeActive { get; private set; }

        private Camera cam;
        private float lastDispatch = -999f;

        // Live marked structures awaiting / undergoing removal.  Read-only poll
        //  dispatch hands these to idle builders; entries self-prune when removed.
        private readonly List<DeconstructTarget> marked = new List<DeconstructTarget>(16);

        // ---- drag-rect runtime state (B7) ------------------------------------
        // Left-mouse-DOWN in deconstruct mode records the start world point; while
        //  the button is held we show a code-generated selection quad; on UP we
        //  sweep every cell in the box.  A near-zero drag falls back to single click.
        private bool dragging;
        private Vector3 dragStartWorld;
        private GameObject dragOverlay;          // code-generated quad (no prefab — QA flag)
        private SpriteRenderer dragOverlaySr;
        private Sprite dragOverlaySprite;         // 1×1 white sprite, generated once

        // ============================================================
        //  Self-bootstrap — no SceneSetup edit (AlertStackUI/RainSound pattern).
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindExisting() != null) return;   // idempotent — never a 2nd manager
                var go = new GameObject("~DeconstructDesignation");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<DeconstructDesignation>();
                    });
        }

        private static DeconstructDesignation FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<DeconstructDesignation>();
#else
            return Object.FindObjectOfType<DeconstructDesignation>();
#endif
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            cam = Camera.main;
        }

        // ---- mode control ----------------------------------------------------

        /// <summary>Enter/leave deconstruct mode.  Mutually exclusive with the
        /// BuildManager build mode (entering deconstruct cancels any build ghost,
        /// and vice-versa, so the two click-handlers never both fire on one click).</summary>
        public void SetMode(bool on)
        {
            ModeActive = on;
            if (on && BuildManager.Instance != null && BuildManager.Instance.BuildModeActive)
                BuildManager.Instance.SetMode(BuildManager.Mode.Off);
        }

        public void Toggle() => SetMode(!ModeActive);

        private void Update()
        {
            if (cam == null) cam = Camera.main;

            // Hotkey X mirrors the build hotkeys (B/F/G/T/Y) — toggle deconstruct.
            if (Input.GetKeyDown(KeyCode.X)) Toggle();

            // If a build mode was entered elsewhere, leave deconstruct so they
            //  never fight over the same left-click.
            if (ModeActive && BuildManager.Instance != null && BuildManager.Instance.BuildModeActive)
                SetMode(false);

            if (ModeActive)
            {
                // Right-click / ESC cancels the mode (same convention as build).
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelDrag();
                    SetMode(false);
                }
                else
                {
                    UpdateDrag();
                }
            }
            else
            {
                CancelDrag();   // leaving the mode mid-drag tears the overlay down
            }

            PruneMarked();
            DispatchToIdleBuilders();
        }

        // ---- click → mark a structure ---------------------------------------

        /// <summary>QA / test entry point: simulate a deconstruct click at a screen
        /// position (no overUI guard) so the harness can drive it without a real
        /// pointer.  Returns the DeconstructTarget marked, or null.</summary>
        public DeconstructTarget SimulateClick(Vector2 screenPos) => HandleClick(screenPos, checkOverUI: false);

        private DeconstructTarget HandleClick(Vector2 screenPos, bool checkOverUI)
        {
            if (checkOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return null;
            if (cam == null) cam = Camera.main;
            if (cam == null) return null;

            Vector3 mw = cam.ScreenToWorldPoint(screenPos);
            int cx = Mathf.FloorToInt(mw.x);
            int cy = Mathf.FloorToInt(mw.y);
            Vector2 cellCenter = new Vector2(cx + 0.5f, cy + 0.5f);

            // Find a deconstructable structure under the cursor cell.  We look at
            //  the cell CENTRE (matches BuildManager.CellOccupied) so the player
            //  clicks anywhere in the tile.
            var hits = Physics2D.OverlapBoxAll(cellCenter, Vector2.one * pickRadius, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var de = TryMark(h.gameObject);
                if (de != null) return de;
            }
            return null;
        }

        /// <summary>Mark a structure GameObject for removal if it is a deconstructable
        /// built thing (wall / door / stove / bed) and not already marked.  Returns
        /// the marker (existing or new), or null if the object can't be deconstructed.</summary>
        public DeconstructTarget TryMark(GameObject go)
        {
            if (go == null) return null;
            // Already marked? return the existing marker (idempotent click).
            var existing = go.GetComponent<DeconstructTarget>();
            if (existing != null) return existing;

            if (!DeconstructTarget.IsDeconstructable(go)) return null;

            var de = go.AddComponent<DeconstructTarget>();
            de.Initialize();
            marked.Add(de);
            ClickEffect.Spawn(go.transform.position, new Color(0.95f, 0.44f, 0.36f, 0.95f)); // DANGER_RED ish
            Debug.Log($"[Deconstruct] marked {go.name} for removal ({de.RefundWood}🪵 / {de.RefundStone}⛏ refund on done)");
            return de;
        }

        // ============================================================
        //  B7 — drag-rect: hold-and-drag a box to mark/cancel MANY things at once.
        //  A near-zero drag is a plain click (existing single-cell TryMark path,
        //  unchanged).  Reuses TryMark per cell — NO new pathfinding / dispatch;
        //  DispatchToIdleBuilders already handles N marked targets.
        // ============================================================
        private void UpdateDrag()
        {
            if (cam == null) return;

            // DOWN — begin a drag (skip if pressing over a UI button like the toggle).
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;
                dragging = true;
                dragStartWorld = ScreenToWorldFlat(Input.mousePosition);
                UpdateDragOverlay(dragStartWorld, dragStartWorld);
            }

            // HELD — grow the selection overlay.
            if (dragging && Input.GetMouseButton(0))
            {
                Vector3 cur = ScreenToWorldFlat(Input.mousePosition);
                UpdateDragOverlay(dragStartWorld, cur);
            }

            // UP — resolve.  Tiny drag → single click (preserve existing behavior);
            //  otherwise sweep every cell in the box.
            if (dragging && Input.GetMouseButtonUp(0))
            {
                Vector3 end = ScreenToWorldFlat(Input.mousePosition);
                dragging = false;
                HideDragOverlay();

                Vector2 delta = (Vector2)(end - dragStartWorld);
                if (delta.magnitude < dragClickThreshold)
                {
                    // No meaningful drag → exactly the prior single-click path.
                    HandleClick(Input.mousePosition, checkOverUI: false);
                }
                else
                {
                    SweepBox(dragStartWorld, end);
                }
            }
        }

        private void CancelDrag()
        {
            if (!dragging) return;
            dragging = false;
            HideDragOverlay();
        }

        /// <summary>Mark every deconstructable structure AND cancel every unbuilt
        /// blueprint whose cell falls inside the drag box.  Each cell reuses the
        /// EXISTING TryMark path (one OverlapBox per cell, like the single click) so
        /// there is no new pathfinding or dispatch logic — DispatchToIdleBuilders
        /// already assigns idle builders to all marked targets.  Returns the count
        /// of newly affected things (marked structures + cancelled blueprints).
        /// Exposed for the QA harness (B12-verify) to drive a box without a pointer.</summary>
        public int SweepBox(Vector3 worldA, Vector3 worldB)
        {
            int minX = Mathf.FloorToInt(Mathf.Min(worldA.x, worldB.x));
            int maxX = Mathf.FloorToInt(Mathf.Max(worldA.x, worldB.x));
            int minY = Mathf.FloorToInt(Mathf.Min(worldA.y, worldB.y));
            int maxY = Mathf.FloorToInt(Mathf.Max(worldA.y, worldB.y));

            int affected = 0;
            // De-dupe within a single sweep: a multi-cell structure (e.g. a 1×2 bed)
            //  overlaps several cells, but TryMark is already idempotent (returns the
            //  existing marker), so a repeat cell adds nothing — we only count the
            //  first mark.  A HashSet guards the blueprint side the same way.
            var seenBlueprints = new HashSet<int>();

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cy = minY; cy <= maxY; cy++)
                {
                    Vector2 cellCenter = new Vector2(cx + 0.5f, cy + 0.5f);
                    var hits = Physics2D.OverlapBoxAll(cellCenter, Vector2.one * pickRadius, 0f);
                    if (hits == null) continue;
                    foreach (var h in hits)
                    {
                        if (h == null) continue;
                        var go = h.gameObject;

                        // (1) DECONSTRUCT a built structure — reuse the click path.
                        //     TryMark returns the existing marker if already marked,
                        //     so it never double-counts; count only fresh marks.
                        bool wasMarked = go.GetComponent<DeconstructTarget>() != null;
                        var de = TryMark(go);
                        if (de != null && !wasMarked) { affected++; continue; }

                        // (2) AREA-CANCEL a pending BLUEPRINT — an UNBUILT blueprint is
                        //     CANCELLED, not deconstructed (per this file's own note &
                        //     the DeconstructTarget.IsDeconstructable doc).
                        var bp = go.GetComponent<BlueprintEntity>();
                        if (bp != null && !bp.IsComplete
                            && seenBlueprints.Add(go.GetInstanceID()))
                        {
                            if (CancelBlueprint(bp)) affected++;
                        }
                    }
                }
            }

            if (affected > 0)
                Debug.Log($"[Deconstruct] area drag affected {affected} thing(s) " +
                          $"in box [{minX},{minY}]..[{maxX},{maxY}]");
            return affected;
        }

        /// <summary>Cancel an unbuilt blueprint.
        ///
        /// >>> QA FLAG (missing public cancel API): BlueprintEntity.cs exposes NO
        ///     public Cancel()/Remove() — its only self-destruct is the PRIVATE
        ///     Complete() → Destroy(gameObject).  This lane is forbidden to edit
        ///     BlueprintEntity.cs, so a clean cancel accessor cannot be added here.
        ///     We therefore cancel via the public Unity API Destroy(blueprint
        ///     .gameObject).  Any PawnBuilder / PawnHauler holding a stale reference
        ///     self-releases on the Unity null-check next poll (same way PruneMarked
        ///     drops a marker whose gameObject became null) — so no dangling
        ///     reservation results.  A proper BlueprintEntity.Cancel() would ALSO
        ///     refund already-collectedWood/Stone and proactively release ReservedBy
        ///     / HaulReservedBy; that refund-on-cancel is NOT possible without
        ///     editing BlueprintEntity.cs and is flagged for a follow-up wave.</summary>
        private bool CancelBlueprint(BlueprintEntity bp)
        {
            if (bp == null) return false;
            ClickEffect.Spawn(bp.transform.position, new Color(0.95f, 0.44f, 0.36f, 0.95f));
            Debug.Log($"[Deconstruct] cancelled pending blueprint {bp.name} " +
                      $"(mode {bp.Mode}) — collected {bp.collectedWood}🪵/{bp.collectedStone}⛏ " +
                      $"NOT refunded (no public BlueprintEntity.Cancel — see QA flag)");
            Destroy(bp.gameObject);
            return true;
        }

        private Vector3 ScreenToWorldFlat(Vector2 screenPos)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return Vector3.zero;
            Vector3 w = cam.ScreenToWorldPoint(screenPos);
            w.z = 0f;
            return w;
        }

        // ---- code-generated selection overlay (no prefab — QA flag) ----------
        //  Same no-prefab discipline as the red ✕ DeconstructTarget marker: a 1×1
        //  white sprite stretched across the drag box, tinted DANGER_RED translucent.
        private void UpdateDragOverlay(Vector3 a, Vector3 b)
        {
            EnsureDragOverlay();
            if (dragOverlay == null) return;

            float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
            float minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);
            // Snap to whole cells so the overlay matches the cell sweep exactly.
            int cMinX = Mathf.FloorToInt(minX), cMaxX = Mathf.FloorToInt(maxX);
            int cMinY = Mathf.FloorToInt(minY), cMaxY = Mathf.FloorToInt(maxY);
            float w = (cMaxX - cMinX) + 1f;
            float h = (cMaxY - cMinY) + 1f;
            dragOverlay.transform.position = new Vector3(cMinX + w * 0.5f, cMinY + h * 0.5f, 0f);
            dragOverlay.transform.localScale = new Vector3(w, h, 1f);
            dragOverlay.SetActive(true);
        }

        private void EnsureDragOverlay()
        {
            if (dragOverlay != null) return;
            if (dragOverlaySprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                tex.wrapMode = TextureWrapMode.Clamp;
                dragOverlaySprite = Sprite.Create(
                    tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            dragOverlay = new GameObject("~DeconstructDragOverlay");
            dragOverlay.transform.SetParent(transform, false);
            dragOverlaySr = dragOverlay.AddComponent<SpriteRenderer>();
            dragOverlaySr.sprite = dragOverlaySprite;
            dragOverlaySr.color = new Color(0.95f, 0.44f, 0.36f, 0.28f); // DANGER_RED translucent
            dragOverlaySr.sortingOrder = 29;   // just under the ✕ markers (30)
            dragOverlay.SetActive(false);
        }

        private void HideDragOverlay()
        {
            if (dragOverlay != null) dragOverlay.SetActive(false);
        }

        private void PruneMarked()
        {
            for (int i = marked.Count - 1; i >= 0; i--)
            {
                var m = marked[i];
                if (m == null || m.gameObject == null || m.IsRemoved) marked.RemoveAt(i);
            }
        }

        // ---- dispatch to idle builders (read-only poll, like BuildBlueprintAction)
        //  We do NOT touch PawnUtilityAI / PawnContext / PawnActions (out of lane);
        //  instead this manager assigns the deconstruct action directly, exactly the
        //  way BuildBlueprintAction assigns a blueprint — to a builder that has NO
        //  current task.  Throttled to dispatchInterval so it isn't a per-frame
        //  FindObjects scan.
        private void DispatchToIdleBuilders()
        {
            if (Time.unscaledTime - lastDispatch < dispatchInterval) return;
            lastDispatch = Time.unscaledTime;
            if (marked.Count == 0) return;

            var builders = FindBuilders();
            if (builders == null || builders.Length == 0) return;

            foreach (var de in marked)
            {
                if (de == null || de.IsRemoved) continue;
                if (de.AssignedTo != null) continue;   // already has a worker

                PawnBuilder best = null;
                float bestSq = float.MaxValue;
                Vector2 tp = de.transform.position;
                foreach (var b in builders)
                {
                    if (b == null) continue;
                    if (b.HasTask) continue;            // busy building / hauling / already deconstructing
                    if (!BuilderAvailable(b)) continue; // not drafted / sleeping / manual
                    float sq = ((Vector2)b.transform.position - tp).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = b; }
                }
                if (best != null) best.SetDeconstructTarget(de);
            }
        }

        /// <summary>A builder is eligible if it's a normal colonist not currently
        /// drafted / sleeping / under manual control (matches PawnUtilityAI's gates,
        /// read-only).</summary>
        private static bool BuilderAvailable(PawnBuilder b)
        {
            var entity = b.GetComponent<PawnEntity>();
            if (entity != null && (entity.IsDrafted || entity.IsUnderManualControl)) return false;
            var needs = b.GetComponent<PawnNeeds>();
            if (needs != null && (needs.IsSleeping || needs.IsBreaking)) return false;
            return true;
        }

        private static PawnBuilder[] FindBuilders()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<PawnBuilder>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<PawnBuilder>();
#endif
        }

        // operator fb #1: the standalone "Btn_해체" toggle button + its
        //  EnsureToggleButton / RefreshToggleVisual / FindCanvas helpers were
        //  removed.  Deconstruct is now entered from ArchitectMenu's Orders (지시)
        //  category (or the X hotkey).  The designation logic above is unchanged.
    }

    /// <summary>
    /// wiki #15 — the DESIGNATION MARKER: a runtime component attached to a built
    /// structure when the player marks it for removal.  It carries the work-to-
    /// remove, the material refund, and a red "✕" overlay so the marked structure
    /// reads at a glance.  PawnBuilder runs the actual removal work against it
    /// (AddWork), then calls CompleteRemoval() which refunds ~50% material and
    /// destroys the structure — a destroyed WallEntity reopens its PathGrid cell via
    /// its own OnDestroy, so the cell becomes walkable with no extra grid call here.
    /// </summary>
    public class DeconstructTarget : MonoBehaviour
    {
        // ~50% material refund (the reference sim deconstruct returns roughly half).  Walls
        //  cost 5 (wood OR stone per material), so 50% → 2.  Computed at Initialize
        //  from the structure type; serialized so it survives inspection/tuning.
        [SerializeField] private int refundWood;
        [SerializeField] private int refundStone;
        [SerializeField] private float workToRemove = 3f;   // secs of construction work
        [SerializeField] private Vector2Int footprint = new Vector2Int(1, 1);

        private float work;
        private bool removed;
        private GameObject marker;

        public int RefundWood => refundWood;
        public int RefundStone => refundStone;
        public Vector2Int Footprint => footprint;
        public bool IsRemoved => removed;
        public float Progress => Mathf.Clamp01(workToRemove > 0f ? work / workToRemove : 1f);

        /// <summary>The builder currently assigned to remove this (null = unclaimed).
        /// Set via the central ReservationManager-style AssignTo/Release below so two
        /// builders never deconstruct the same structure.</summary>
        public GameObject AssignedTo { get; private set; }

        public void AssignTo(GameObject b) { if (AssignedTo == null) AssignedTo = b; }
        public void ReleaseFrom(GameObject b) { if (AssignedTo == b) AssignedTo = null; }

        /// <summary>True if this GameObject is a built structure the player may
        /// deconstruct (wall / door / stove / bed).  Blueprints are NOT included —
        /// an unbuilt blueprint is cancelled, not deconstructed (different flow).</summary>
        public static bool IsDeconstructable(GameObject go)
        {
            if (go == null) return false;
            return go.GetComponent<WallEntity>() != null
                || go.GetComponent<DoorEntity>() != null
                || go.GetComponent<StoveEntity>() != null
                || go.GetComponent<BedEntity>() != null
                // #버그헌트(2026-06-04): 바리케이드는 통행 차단(RegisterWallCell)인데 해체 대상에서
                //  빠져 한 번 놓으면 영구 봉쇄였다(출구 막으면 pawn 영영 갇힘).  OnDestroy 가
                //  SetStructureBlocked(false)로 셀을 풀므로 해체하면 실제로 통행이 복구된다.
                || go.GetComponent<BarricadeEntity>() != null;
        }

        /// <summary>Compute the refund + footprint from the structure type, then draw
        /// the red ✕ designation overlay.  ~50% of build cost (wall 5→2).</summary>
        public void Initialize()
        {
            var wall = GetComponent<WallEntity>();
            if (wall != null)
            {
                // Wall build cost is 5 wood OR 5 stone by material → 50% = 2.
                if (wall.Material == WallMaterial.Stone) refundStone = 2;
                else refundWood = 2;                       // Wood (Steel n/a as buildable)
            }
            // #버그헌트(2026-06-04): AutodoorEntity 는 DoorEntity 서브클래스 → Door 분기보다 먼저
            //  검사해야 한다.  자동문 비용 6 → 50% = 3 (이전엔 Door 분기에 걸려 1만 환불).
            else if (GetComponent<AutodoorEntity>() != null)
            {
                refundWood = 3;                            // autodoor cost 6 → 50% = 3
            }
            else if (GetComponent<DoorEntity>() != null)
            {
                refundWood = 1;                            // door cost 3 → ~50% = 1
            }
            else if (GetComponent<StoveEntity>() != null)
            {
                refundWood = 5;                            // stove cost 10 → 50% = 5
            }
            else if (GetComponent<BedEntity>() != null)
            {
                // #버그헌트(2026-06-04): 품질별 환불.  이전엔 4 고정 → (a) 수면자리(비용 0)를
                //  해체하면 +4 가 생기는 자원 복제 익스플로잇, (b) 고급침대(비용 30)는 4 만
                //  환불되는 과소환불.  50% 규칙: SleepingSpot 0 / Wood 4 / Fine 15.
                var bed = GetComponent<BedEntity>();
                refundWood = bed.Quality switch
                {
                    BedQuality.SleepingSpot => 0,
                    BedQuality.Fine         => 15,         // fine cost 30 → 50% = 15
                    _                       => 4,          // Wood cost 8 → 50% = 4
                };
                footprint = new Vector2Int(1, 2);          // beds are 1×2
            }
            // #버그헌트(2026-06-04): 바리케이드 환불.  비용 5 → 50% = 2.
            else if (GetComponent<BarricadeEntity>() != null)
            {
                refundWood = 2;                            // barricade cost 5 → 50% = 2
            }
            BuildMarker();
        }

        private void BuildMarker()
        {
            if (marker != null) return;
            marker = new GameObject("DeconstructMark");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = Vector3.zero;

            // A "✕" TextMesh in DANGER_RED, rendered above the structure sprite.
            //  No imported PNG / prefab — code-generated (flagged for QA).
            var tm = marker.AddComponent<TextMesh>();
            tm.text = "✕";
            tm.fontSize = 48;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = UITheme.TextDanger;
            var f = Font.CreateDynamicFontFromOSFont("Arial", 48);
            if (f != null) { tm.font = f; tm.GetComponent<MeshRenderer>().material = f.material; }
            var mr = marker.GetComponent<MeshRenderer>();
            mr.sortingOrder = 30;   // above structure sprites (walls sort ~ low)
        }

        /// <summary>PawnBuilder calls this per frame while in range.  Returns true
        /// when the structure is fully deconstructed (work complete).</summary>
        public bool AddWork(float deltaSec)
        {
            if (removed) return true;
            work += deltaSec;
            return work >= workToRemove;
        }

        /// <summary>Finish the removal: refund ~50% material to the global pool, then
        /// destroy the structure.  Destroying a WallEntity reopens its PathGrid cell
        /// through WallEntity.OnDestroy (PathGrid.SetStructureBlocked false → wall
        /// ref-count decrement → Version bump → in-flight pawns re-path), mirroring
        /// exactly how the build INCREMENTED it — so the cell becomes walkable with
        /// no extra grid call needed here.</summary>
        public void CompleteRemoval()
        {
            if (removed) return;
            removed = true;

            var rm = ResourceManager.Instance;
            if (rm != null)
            {
                if (refundWood > 0) rm.AddWood(refundWood);
                if (refundStone > 0) rm.AddStone(refundStone);
            }
            Debug.Log($"[Deconstruct] removed {name} → refunded {refundWood}🪵 {refundStone}⛏; cell reopens via structure OnDestroy");

            // Cell-reopen note: a WallEntity decrements its PathGrid wall ref-count
            //  in its OWN OnDestroy (PathGrid.SetStructureBlocked false / the named
            //  ReleaseWallCell helper), symmetric to the build-time increment, so
            //  destroying the structure below is all that's needed to make the cell
            //  walkable.  Doors/stoves/beds never register a blocker, so their cells
            //  were already walkable.  No extra grid call required here.

            if (marker != null) Destroy(marker);
            Destroy(gameObject);
        }
    }
}
