using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonS.GameProto.AI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Wave W-M4-02 Lane A / wiki #16 (Dimension 4 건축) — MINE DESIGNATION.
    ///
    /// Wiki acceptance criterion (#16):
    ///   "Drag-selecting rock cells → a miner walks over and mines them, yielding
    ///    stone."
    ///
    /// The mining MECHANIC already exists end-to-end: PawnMiner.SetVeinTarget walks
    /// a pawn to a reserved adjacent stand cell and calls
    /// StoneVeinEntity.TakeMineDamage each work tick; the vein drops stone chunks on
    /// depletion (inside TakeMineDamage).  What was MISSING is the player-facing
    /// DESIGNATION + dispatch layer: a "mine mode" the player toggles, in which a
    /// drag-rect (or a single click) over rock/ore cells MARKS the StoneVeinEntities
    /// under them, after which idle miners are dispatched to the marked veins.
    ///
    /// This file mirrors the just-shipped DeconstructDesignation.cs EXACTLY (W-M4-01
    /// Lane A, wiki #15) — same self-bootstrap discipline, same self-attached Canvas
    /// toggle button, same runtime marker, same throttled idle-worker dispatch — but
    /// targets StoneVeinEntity + PawnMiner instead of structures + PawnBuilder.  It
    /// introduces NO new pathfinding (PawnMiner already reuses the build/haul
    /// TryReserveWorkStandPos + WorkGiveUp pathing) and edits neither PawnMiner.cs
    /// (the mine action is already complete) nor StoneVeinEntity.cs.
    ///
    /// ----------------------------------------------------------------------------
    /// DISCIPLINE — mirrors DeconstructDesignation.cs / AlertStackUI.cs / RainSound
    /// Driver.cs EXACTLY:
    ///   - [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] spawns ONE hidden
    ///     DontDestroyOnLoad manager GameObject; FindFirstObjectByType idempotency
    ///     guard means a domain/scene reload never spawns a second manager.
    ///   - It holds NO live singleton reference captured in OnEnable (bug-pattern #7
    ///     firewall).  It READ-ONLY poll-finds live managers each frame
    ///     (BuildManager / DeconstructDesignation / Canvas / PawnMiners) and re-finds
    ///     if they are replaced — never subscribes to a singleton event with no live
    ///     ref.
    ///   - Expiry / timing uses Time.unscaledTime deltas inside Update, NOT a
    ///     WaitForSeconds heartbeat coroutine (bug-pattern #9 firewall: no new
    ///     always-on background timer that would freeze on focus loss during a CLI
    ///     QA launch).
    ///
    /// MUTUAL EXCLUSION (one left-click never fires two handlers):
    ///   Mine mode is mutually exclusive with BOTH the BuildManager build mode AND
    ///   the DeconstructDesignation deconstruct mode.  Entering mine mode cancels the
    ///   other two; if either of the other two becomes active, mine mode leaves.  So
    ///   a single left-click is consumed by exactly one designation handler.  Right-
    ///   click / ESC cancels mine mode (same convention as build / deconstruct).
    ///
    /// MODE-TOGGLE ROUTING (lane hot-file budget):
    ///   Like DeconstructDesignation (which proved ZERO SceneSetup budget works),
    ///   this manager self-attaches its toggle button onto the EXISTING screen Canvas
    ///   (it only parents a new child, edits no existing UI) and binds the M hotkey.
    ///   So this wave spends ZERO of its SceneSetup hot-file budget.
    ///
    ///   >>> QA FLAG (scene-wiring): the mine toggle button is created at runtime on
    ///       the first Canvas found (bottom-center, offset right of the centered
    ///       GuiControlBar, opposite side from the Deconstruct button) and the
    ///       designation marker is a runtime-built pick glyph ("") TextMesh (no
    ///       prefab / no imported PNG).  Both are code-generated, never SceneSetup-
    ///       wired — QA should confirm the button appears, toggles mode, the M hotkey
    ///       toggles it, and the pick glyph shows on a marked vein.  No SceneSetup*.cs
    ///       file was edited this lane.
    /// </summary>
    public class MineDesignation : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tune day-1 feel) -------
        [Header("Dispatch")]
        [SerializeField] private float dispatchInterval = 0.5f;   // how often we assign idle miners
        [SerializeField] private float pickRadius = 0.45f;        // click hit-test box half-extent

        [Header("Drag-rect")]
        [SerializeField] private float dragThreshold = 0.35f;     // world units before a click becomes a drag

        // ── operator fb #1 (2026-05-31): the standalone "채광 (M)" toggle button
        //   was REMOVED.  In the reference sim, Mine lives inside the Architect menu's
        //   Orders (지시) category, not as a standalone screen button.  The mine
        //   DESIGNATION logic below is unchanged and is now invoked from
        //   ArchitectMenu (Orders → 채광 → MineDesignation.Instance.SetMode(true)).
        //   The M HOTKEY is preserved.  All toggle-button UI fields / builders /
        //   RefreshToggleVisual were deleted.

        // ---- runtime state ---------------------------------------------------
        public static MineDesignation Instance { get; private set; }

        /// <summary>True while the player is in mine designation mode.</summary>
        public bool ModeActive { get; private set; }

        private Camera cam;
        private float lastDispatch = -999f;

        // Live marked veins awaiting / undergoing mining.  Throttled read-only poll
        //  dispatch hands these to idle miners; entries self-prune when mined out.
        private readonly List<MineTarget> marked = new List<MineTarget>(32);

        // Drag-rect state: a left-press-then-drag sweeps a rectangle of cells; a
        //  press-release-without-drag is treated as a single click (one cell).
        private bool dragging;
        private Vector3 dragStartWorld;

        // ============================================================
        //  Self-bootstrap — no SceneSetup edit (DeconstructDesignation pattern).
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindExisting() != null) return;   // idempotent — never a 2nd manager
                var go = new GameObject("~MineDesignation");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<MineDesignation>();
                    });
        }

        private static MineDesignation FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<MineDesignation>();
#else
            return Object.FindObjectOfType<MineDesignation>();
#endif
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            cam = Camera.main;
        }

        // ---- mode control ----------------------------------------------------

        /// <summary>Enter/leave mine mode.  Mutually exclusive with BOTH the
        /// BuildManager build mode AND the DeconstructDesignation deconstruct mode,
        /// so the three click-handlers never fight over one left-click.</summary>
        public void SetMode(bool on)
        {
            ModeActive = on;
            if (on)
            {
                // Cancel the other two designation modes so one click fires one handler.
                if (BuildManager.Instance != null && BuildManager.Instance.BuildModeActive)
                    BuildManager.Instance.SetMode(BuildManager.Mode.Off);
                if (DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive)
                    DeconstructDesignation.Instance.SetMode(false);
            }
            if (!on) { dragging = false; }
        }

        public void Toggle() => SetMode(!ModeActive);

        private void Update()
        {
            if (cam == null) cam = Camera.main;

            // Hotkey M mirrors the build hotkeys (B/F/G/T/Y) and deconstruct (X).
            if (Input.GetKeyDown(KeyCode.M)) Toggle();

            // If a build OR deconstruct mode was entered elsewhere, leave mine mode so
            //  they never fight over the same left-click.
            if (ModeActive)
            {
                bool buildOn = BuildManager.Instance != null && BuildManager.Instance.BuildModeActive;
                bool deconOn = DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive;
                if (buildOn || deconOn) SetMode(false);
            }

            if (ModeActive)
            {
                // Right-click / ESC cancels the mode (same convention as build).
                if (SimInput.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    SetMode(false);
                }
                else
                {
                    HandleDragInput();
                }
            }

            PruneMarked();
            // #작업배정-단일화(운영자 승인 2026-06-03): 벌목과 동일 — 별도 dispatch 제거.
            //  MineStoneAction 이 '마킹된 광맥만' 자율 선택하는 단일 경로로 통일.
            // DispatchToIdleMiners();  // retired — 단일 경로(MineStoneAction)로 대체
        }

        /// <summary>#작업배정-단일화 — MineStoneAction 이 '지정된 광맥만' 자율 채광하도록
        /// 마킹 여부 조회.</summary>
        public bool IsMarked(StoneVeinEntity v)
        {
            if (v == null) return false;
            for (int i = 0; i < marked.Count; i++)
            {
                var m = marked[i];
                if (m != null && !m.IsMined && m.Vein == v) return true;
            }
            return false;
        }

        // #save-load 완성(2026-06-04) — 마킹된(아직 안 캔) 광맥 위치 목록.  SaveLoadManager 가
        //  저장하고, 로드 시 위치 매칭으로 재마킹한다(지정이 로드 시 소실되던 것 fix).
        public System.Collections.Generic.List<Vector2> GetMarkedVeinPositions()
        {
            var list = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < marked.Count; i++)
            {
                var m = marked[i];
                if (m != null && !m.IsMined && m.Vein != null)
                    list.Add(m.Vein.transform.position);
            }
            return list;
        }

        // ---- left-press / drag / release → mark vein(s) ----------------------

        private void HandleDragInput()
        {
            if (cam == null) return;

            if (SimInput.GetMouseButtonDown(0))
            {
                // Don't start a drag if the press began over a UI element.
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;
                dragging = true;
                dragStartWorld = cam.ScreenToWorldPoint(SimInput.mousePosition);
            }
            else if (SimInput.GetMouseButtonUp(0) && dragging)
            {
                dragging = false;
                Vector3 endWorld = cam.ScreenToWorldPoint(SimInput.mousePosition);
                if (Vector3.Distance(endWorld, dragStartWorld) < dragThreshold)
                    MarkCell(endWorld);                 // a tap → single cell
                else
                    MarkRect(dragStartWorld, endWorld); // a sweep → rectangle of cells
            }
        }

        // ---- public test / QA entry points (no real pointer needed) ----------

        /// <summary>QA / test entry point: mark whatever vein sits under a screen
        /// position (no overUI guard).  Returns the MineTarget marked, or null.</summary>
        public MineTarget SimulateClick(Vector2 screenPos)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return null;
            return MarkCell(cam.ScreenToWorldPoint(screenPos));
        }

        /// <summary>QA / test entry point: drag-select a world-space rectangle and
        /// mark every vein whose cell falls inside it.  Returns the count marked.</summary>
        public int SimulateDragRect(Vector2 worldA, Vector2 worldB) => MarkRect(worldA, worldB);

        // ---- marking ---------------------------------------------------------

        private MineTarget MarkCell(Vector3 world)
        {
            int cx = Mathf.FloorToInt(world.x);
            int cy = Mathf.FloorToInt(world.y);
            return MarkVeinAtCell(cx, cy);
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
                    if (MarkVeinAtCell(cx, cy) != null) n++;
            return n;
        }

        /// <summary>Mark the StoneVeinEntity occupying cell (cx,cy), if any, for
        /// mining.  Hit-tests the cell CENTRE with an OverlapBox (matches how
        /// ClickSelector / BuildManager.CellOccupied probe a cell) so the player can
        /// click anywhere in the tile.  Idempotent: a re-mark returns the existing
        /// marker.</summary>
        private MineTarget MarkVeinAtCell(int cx, int cy)
        {
            Vector2 cellCenter = new Vector2(cx + 0.5f, cy + 0.5f);
            var hits = Physics2D.OverlapBoxAll(cellCenter, Vector2.one * pickRadius, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var de = TryMark(h.gameObject);
                if (de != null) return de;
            }
            return null;
        }

        /// <summary>Mark a GameObject for mining if it is a (live) StoneVeinEntity and
        /// not already marked.  Returns the marker (existing or new), or null if the
        /// object is not a mineable vein.</summary>
        public MineTarget TryMark(GameObject go)
        {
            if (go == null) return null;
            var existing = go.GetComponent<MineTarget>();
            if (existing != null) return existing;          // idempotent re-mark

            if (!MineTarget.IsMineable(go)) return null;

            var mt = go.AddComponent<MineTarget>();
            mt.Initialize();
            marked.Add(mt);
            // wiki #5 reuse: a single PlaySelect blip per mark (throttled inside
            //  AudioBank; never a tight-loop buzz — bug-pattern #4 firewall).
            AudioBank.Instance?.PlaySelect();
            ClickEffect.Spawn(go.transform.position, new Color(0.75f, 0.78f, 0.85f, 0.95f)); // slate/ore
            Debug.Log($"[Mine] designated {go.name} ({mt.VeinTypeKr}) for mining");
            return mt;
        }

        private void PruneMarked()
        {
            for (int i = marked.Count - 1; i >= 0; i--)
            {
                var m = marked[i];
                if (m == null || m.gameObject == null || m.IsMined) marked.RemoveAt(i);
            }
        }

        // ---- dispatch to idle miners (read-only poll, like DeconstructDesignation)
        //  We do NOT touch PawnUtilityAI / PawnContext / PawnActions (out of lane);
        //  instead this manager assigns the marked vein directly to an idle miner via
        //  the EXISTING PawnMiner.SetVeinTarget, exactly the way MineStoneAction would
        //  — but gated on a player DESIGNATION rather than auto-picking any vein, and
        //  assigned by the manager so this lane edits no AI files.  Throttled to
        //  dispatchInterval so it isn't a per-frame FindObjects scan.
        private void DispatchToIdleMiners()
        {
            if (Time.unscaledTime - lastDispatch < dispatchInterval) return;
            lastDispatch = Time.unscaledTime;
            if (marked.Count == 0) return;

            var miners = FindMiners();
            if (miners == null || miners.Length == 0) return;

            foreach (var mt in marked)
            {
                if (mt == null || mt.IsMined) continue;
                // Skip a vein already reserved (a miner is already heading to it —
                //  either via this dispatch or the existing MineStoneAction).
                // #38 키 일치: SetVeinTarget 은 mt.Vein(StoneVeinEntity)을 예약 키로 쓰므로
                //  여기서도 mt.Vein 으로 조회해야 재배정(전원 몰림)을 막는다. (mt.gameObject → 키 불일치)
                if (ReservationManager.IsReserved(mt.Vein)) continue;

                PawnMiner best = null;
                float bestSq = float.MaxValue;
                Vector2 tp = mt.transform.position;
                foreach (var m in miners)
                {
                    if (m == null) continue;
                    if (m.HasTask) continue;            // busy mining something already
                    if (!MinerAvailable(m)) continue;   // not drafted / sleeping / manual / breaking
                    // #236 도달 불가 광맥 무한 재배정 방지 — #230 이 MineStoneAction 을 지우면서
                    //  PawnMiner._giveUpUntil cooldown 을 소비하던 유일한 경로가 사라져, 포기 직후
                    //  같은 miner 가 즉시 재배정되는 루프(LongPlay 120 give-up).  여기서 skip.
                    if (m.IsRecentlyGivenUp(mt.Vein)) continue;
                    float sq = ((Vector2)m.transform.position - tp).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = m; }
                }
                // PawnMiner.SetVeinTarget reserves the vein through ReservationManager
                //  itself, so the next dispatch tick sees it claimed and skips it.
                if (best != null) best.SetVeinTarget(mt.Vein);
            }
        }

        /// <summary>A miner is eligible if it's a normal colonist not currently drafted
        /// / sleeping / breaking / under manual control (matches PawnUtilityAI's gates,
        /// read-only — same set DeconstructDesignation.BuilderAvailable uses).</summary>
        private static bool MinerAvailable(PawnMiner m)
        {
            var entity = m.GetComponent<PawnEntity>();
            if (entity != null && (entity.IsDrafted || entity.IsUnderManualControl)) return false;
            var needs = m.GetComponent<PawnNeeds>();
            if (needs != null && (needs.IsSleeping || needs.IsBreaking)) return false;
            return true;
        }

        private static PawnMiner[] FindMiners()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<PawnMiner>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<PawnMiner>();
#endif
        }

        // operator fb #1: the standalone "Btn_채광" toggle button + its
        //  EnsureToggleButton / RefreshToggleVisual / FindCanvas helpers were
        //  removed.  Mine is now entered from ArchitectMenu's Orders (지시) category
        //  (or the M hotkey).  The designation logic above is unchanged.
    }

    /// <summary>
    /// wiki #16 — the DESIGNATION MARKER: a runtime component attached to a
    /// StoneVeinEntity when the player marks it for mining.  It carries a pick ""
    /// glyph overlay so the marked vein reads at a glance, and a back-reference to
    /// its StoneVeinEntity so the dispatch can hand it to a PawnMiner.  The actual
    /// mining work + chunk-drop is entirely the existing StoneVeinEntity.
    /// TakeMineDamage path (driven by PawnMiner) — this marker only EXISTS while the
    /// vein does; when the vein is mined out (Destroy(gameObject) inside
    /// TakeMineDamage), this component dies with it and is pruned from the manager
    /// list.  StoneVeinEntity.cs is intentionally NOT edited (no designation-aware
    /// accessor was needed — IsDestroyed already reports depletion).
    /// </summary>
    public class MineTarget : MonoBehaviour
    {
        private StoneVeinEntity vein;
        private GameObject marker;

        public StoneVeinEntity Vein => vein;
        /// <summary>True once the vein has been mined out (destroyed).  The vein's
        /// own TakeMineDamage Destroy()s the GameObject on depletion, so this marker
        /// goes null with it; this getter also reports a vein that reached IsDestroyed
        /// in the same frame before the Destroy resolves.</summary>
        public bool IsMined => vein == null || vein.IsDestroyed;
        public string VeinTypeKr => vein != null ? vein.TypeKr : "돌";

        /// <summary>True if this GameObject is a mineable rock/ore vein.</summary>
        public static bool IsMineable(GameObject go)
        {
            if (go == null) return false;
            var v = go.GetComponent<StoneVeinEntity>();
            return v != null && !v.IsDestroyed;
        }

        /// <summary>Cache the vein reference and draw the pick "" designation overlay.</summary>
        public void Initialize()
        {
            vein = GetComponent<StoneVeinEntity>();
            BuildMarker();
        }

        private void BuildMarker()
        {
            if (marker != null) return;
            marker = new GameObject("MineMark");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = Vector3.zero;

            // A pick "" TextMesh rendered above the vein sprite.  No imported PNG /
            //  prefab — code-generated (flagged for QA), exactly like the deconstruct
            //  "×" marker.
            var tm = marker.AddComponent<TextMesh>();
            tm.text = "";
            tm.fontSize = 48;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = UITheme.TextPrimary;
            var f = Resources.Load<Font>("Fonts/GowunDodum") ?? Resources.Load<Font>("Fonts/NotoSansKR");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 48);
            if (f != null) { tm.font = f; tm.GetComponent<MeshRenderer>().material = f.material; }
            var mr = marker.GetComponent<MeshRenderer>();
            // UI겹침 P1-7 (2026-06-14): 30 → 28. 광맥 스프라이트(≤20) 위는 유지하되
            //  이름 라벨(shadow 29/name 30)과의 30==30 타이를 풀어, 마킹 광맥 남쪽 림의
            //  골드 이름 위로 흰 가 겹쳐 판독 불가하던 것 해소(이름 아래로 정렬).
            mr.sortingOrder = 28;
        }
    }
}
