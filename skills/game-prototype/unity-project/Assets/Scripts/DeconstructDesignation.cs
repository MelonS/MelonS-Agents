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
    /// </summary>
    public class DeconstructDesignation : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tune day-1 feel) -------
        [Header("Dispatch")]
        [SerializeField] private float dispatchInterval = 0.5f;   // how often we assign idle builders
        [SerializeField] private float pickRadius = 0.45f;        // click hit-test box half-extent

        [Header("Toggle button")]
        [SerializeField] private float btnWidth = 96f;
        [SerializeField] private float btnHeight = 40f;
        [SerializeField] private int btnFontSize = 16;
        [SerializeField] private float btnBottomInset = 104f;     // sit above the control bar

        // ---- runtime state ---------------------------------------------------
        public static DeconstructDesignation Instance { get; private set; }

        /// <summary>True while the player is in deconstruct designation mode.</summary>
        public bool ModeActive { get; private set; }

        private Camera cam;
        private float lastDispatch = -999f;

        // Live marked structures awaiting / undergoing removal.  Read-only poll
        //  dispatch hands these to idle builders; entries self-prune when removed.
        private readonly List<DeconstructTarget> marked = new List<DeconstructTarget>(16);

        // self-built toggle button (on the existing Canvas).
        private Button toggleBtn;
        private Image toggleFill;
        private Text toggleLabel;
        private bool toggleBuilt;

        // ============================================================
        //  Self-bootstrap — no SceneSetup edit (AlertStackUI/RainSound pattern).
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindExisting() != null) return;   // idempotent — never a 2nd manager
            var go = new GameObject("~DeconstructDesignation");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<DeconstructDesignation>();
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
            RefreshToggleVisual();
        }

        public void Toggle() => SetMode(!ModeActive);

        private void Update()
        {
            if (cam == null) cam = Camera.main;
            EnsureToggleButton();   // read-only poll-find of the Canvas (no OnEnable capture)

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
                    SetMode(false);
                }
                else if (Input.GetMouseButtonDown(0))
                {
                    HandleClick(Input.mousePosition, checkOverUI: true);
                }
            }

            PruneMarked();
            DispatchToIdleBuilders();
            RefreshToggleVisual();
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

        // ============================================================
        //  Self-built toggle button on the existing Canvas (no SceneSetup edit).
        //  Mirrors GuiControlBar.EnsureInScene: poll-find a Canvas, parent a new
        //  child button onto it, edit nothing that already exists.
        // ============================================================
        private void EnsureToggleButton()
        {
            if (toggleBuilt && toggleBtn != null) return;
            var canvas = FindCanvas();
            if (canvas == null) return;   // retry next frame (read-only poll)

            var go = new GameObject("Btn_해체");
            go.transform.SetParent(canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(btnWidth, btnHeight);
            // Sit just above the bottom control bar, offset left so it doesn't
            //  overlap the centered GuiControlBar.
            rt.anchoredPosition = new Vector2(-360f, btnBottomInset);

            var border = go.AddComponent<Image>();
            border.color = UITheme.Divider;
            var fillRt = UITheme.MakeBorderedPanel(rt, 2f, UITheme.BtnInactiveBg);
            toggleFill = fillRt.parent.GetComponent<Image>();
            // Button click needs a raycastable graphic; the fill body catches it
            //  (MakeBorderedPanel leaves graphics non-raycast by default).
            if (toggleFill != null) toggleFill.raycastTarget = true;

            toggleBtn = go.AddComponent<Button>();
            toggleBtn.targetGraphic = toggleFill;
            toggleBtn.onClick.AddListener(() =>
            {
                AudioBank.Instance?.PlaySelect();   // wiki #5: UI click blip (existing helper)
                Toggle();
            });

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            toggleLabel = labelGo.AddComponent<Text>();
            toggleLabel.text = "해체 (X)";
            toggleLabel.font = UITheme.LoadKoreanFont(btnFontSize);
            toggleLabel.fontSize = btnFontSize;
            toggleLabel.fontStyle = FontStyle.Bold;
            toggleLabel.color = UITheme.TextPrimary;
            toggleLabel.alignment = TextAnchor.MiddleCenter;
            var lrt = toggleLabel.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            toggleBuilt = true;
            RefreshToggleVisual();
        }

        private void RefreshToggleVisual()
        {
            if (toggleFill != null)
            {
                var target = ModeActive ? UITheme.BtnActiveBg : UITheme.BtnInactiveBg;
                if (toggleFill.color != target) toggleFill.color = target;
            }
            if (toggleLabel != null)
                toggleLabel.color = ModeActive ? UITheme.TextDark : UITheme.TextPrimary;
        }

        private static Canvas FindCanvas()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<Canvas>();
#else
            return Object.FindObjectOfType<Canvas>();
#endif
        }
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
        // ~50% material refund (RimWorld deconstruct returns roughly half).  Walls
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
                || go.GetComponent<BedEntity>() != null;
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
                refundWood = 4;                            // wood bed cost 8 → 50% = 4
                footprint = new Vector2Int(1, 2);          // beds are 1×2
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
