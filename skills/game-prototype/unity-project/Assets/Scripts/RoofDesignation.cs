using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// ROOF wave (운영자: "the reference sim처럼 지붕영역 설정 메뉴 따로 있음") — ROOF AREA DESIGNATION.
    ///
    /// the reference sim concept (필수만, 위키 근사):
    ///   In vanilla the reference sim the player paints a "Roof Area" (지붕 영역) under the
    ///   Architect ZONE/ORDERS group; designated cells become ROOFED.  A roof gives
    ///   the indoor "shade" look (this prototype's visual effect) and carries a
    ///   ROOFED flag for future rain/temperature hooks.  the reference sim also AUTO-ROOFS a
    ///   room fully enclosed by walls; we implement a light version of that (수동
    ///   지정이 필수, auto-roof는 bonus).
    ///
    /// This file is the player-facing DESIGNATION layer.  It MIRRORS the shipped
    /// MineDesignation.cs / GrowZoneDesignation.cs EXACTLY — same self-bootstrap
    /// discipline, same drag-rect MarkRect / single-click MarkCell, same throttled
    /// poll Update, same mutual-exclusion with the other designation modes — but
    /// instead of dispatching a worker it simply MARKS cells as roofed in a shared
    /// cell set that <see cref="RoofOverlayRenderer"/> reads READ-ONLY to draw the
    /// shade overlay.  No worker / pathfinding is involved, but 운영자 fb (2026-06-01,
    /// "지붕 영역을 정해줘도 지붕건설을 안함") added a light TIME-BASED build progression:
    /// a designated cell is '짓는 중'(pending) for buildSeconds, then auto-promotes to
    /// '지어진 지붕'(built) — so the designation visibly turns into a real roof without
    /// touching any worker/pathfinding file (lane-conflict-safe, 필수만).
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

        [Header("Build progression (the reference sim처럼 짓는 시간)")]
        // 운영자 fb (2026-06-01) "지붕 영역을 정해줘도 지붕건설을 안함" — vanilla the reference sim 에서
        //  지붕 영역을 지정하면 즉시 roofed 가 아니라 pawn 이 잠시 '건설'한 뒤 확정된다.  이
        //  프로토타입은 별도 worker/pathfinding 파일을 건드리지 않으므로(레인 충돌 방지) 시간
        //  기반의 가벼운 진행만 둔다: 지정 → buildSeconds 동안 '짓는 중'(pending) → 자동으로
        //  '지어진 지붕'(built) 확정.  RoofOverlayRenderer 가 두 상태를 시각적으로 다르게 그려
        //  "지정 즉시 변화가 보이고 → 잠시 후 진짜 지붕이 된다"가 눈에 보이게 한다.
        // 소크 r2 채점(2026-06-12) 관찰 #1 — 지붕이 자재·노동 없이 벽보다 먼저 ~1초에
        //  일괄 완공.  타이머 자동 승격을 폐기하고 '빌더 노동'으로 전환: 지정 셀은
        //  무기한 pending 으로 남고, PawnBuilder 가 셀에 도달해 buildSeconds 만큼
        //  노동해야 BUILT 로 승격된다 (BuildRoofAction → SetRoofTarget → TickRoofWork).
        //  자재는 레퍼런스 패리티대로 무료(지붕은 노동만 소모).
        [SerializeField] private float buildSeconds = 2.5f;       // 셀당 필요 노동초 (빌더 작업속도 배율 적용)

        // ---- runtime state ---------------------------------------------------
        public static RoofDesignation Instance { get; private set; }

        /// <summary>True while the player is in roof designation mode.</summary>
        public bool ModeActive { get; private set; }

        /// <summary>운영자 fb (2026-06-01) "다른 영역도 제거하는 기능 필요" — true 면 같은
        /// drag-rect 가 지붕을 '지우는' ERASE 모드.  add 경로와 대칭: 같은 입력/드래그
        /// 로직을 타되 AddRoof 대신 RemoveRoof 를 호출한다.  the reference sim 의 Zone 'Expand/
        /// Clear' 토글과 동일 개념(여기선 roofed 셀 제거).  ModeActive 안에서만 의미가 있다.</summary>
        public bool EraseMode { get; private set; }

        // The shared map of roof cells.  Each designated cell carries a BUILD-DONE
        //  timestamp (Time.time at which it finishes building); while now < that time
        //  the cell is PENDING ('짓는 중'), at/after it the cell is BUILT ('지어진 지붕').
        //  RoofOverlayRenderer reads this READ-ONLY (via Roofed / IsRoofed / IsPending)
        //  to draw the two distinct visuals; future rain/temperature systems read
        //  IsRoofed(cell) as the roofed FLAG hook (only a BUILT roof counts as roofed).
        private readonly Dictionary<Vector2Int, float> roofCells = new Dictionary<Vector2Int, float>(128);

        /// <summary>Monotonic version bumped whenever the roof set's MEMBERSHIP changes
        /// (add/remove), so the overlay renderer can cheaply detect a structural change.
        /// NOTE: a pending→built transition does NOT bump Version (membership is
        /// unchanged) — the renderer also polls PendingCount each frame to catch those.</summary>
        public int Version { get; private set; }

        /// <summary>Count of cells still in the '짓는 중'(pending) state.  The overlay
        /// renderer watches this (cheap) so a pending→built promotion (which does not
        /// change membership / Version) still triggers a visual refresh.</summary>
        public int PendingCount { get; private set; }

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
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<RoofDesignation>();
            });
        }

        private static RoofDesignation FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<RoofDesignation>();
#else
            return UnityEngine.Object.FindObjectOfType<RoofDesignation>();
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

        /// <summary>True if cell (cx,cy) is a FINISHED roof (the roofed FLAG hook — a
        /// '짓는 중' pending cell does NOT count as roofed for rain/temperature yet).</summary>
        public bool IsRoofed(int cx, int cy) => IsRoofed(new Vector2Int(cx, cy));
        public bool IsRoofed(Vector2Int cell)
            => roofCells.TryGetValue(cell, out float done) && Time.time >= done;

        /// <summary>True if cell is designated but still '짓는 중'(under construction):
        /// it is in the set but its build-done time hasn't arrived yet.  The overlay
        /// renderer draws these with a distinct in-progress look.</summary>
        public bool IsPending(int cx, int cy) => IsPending(new Vector2Int(cx, cy));
        public bool IsPending(Vector2Int cell)
            => roofCells.TryGetValue(cell, out float done) && Time.time < done;

        /// <summary>True if cell is in the roof set at all (pending OR built).</summary>
        public bool IsDesignated(Vector2Int cell) => roofCells.ContainsKey(cell);

        /// <summary>READ-ONLY enumeration of ALL roof cells (pending + built), for the
        /// overlay renderer to draw a quad on each (it picks the look via IsRoofed /
        /// IsPending).  Returns the live key collection; callers only enumerate.</summary>
        public IReadOnlyCollection<Vector2Int> Roofed => roofCells.Keys;
        public int RoofedCount => roofCells.Count;

        // ---- mode control ----------------------------------------------------

        /// <summary>Enter/leave roof mode.  Mutually exclusive with the BuildManager
        /// build mode and the Deconstruct / Mine / Grow / Stockpile designation modes,
        /// so the click-handlers never fight over one left-click.  Cancels the others
        /// on entry via their existing read-only SetMode APIs (no edit to those files).
        /// </summary>
        public void SetMode(bool on) => SetModeInternal(on, erase: false);

        /// <summary>운영자 fb (2026-06-01): 지붕 영역을 '지우는' 모드로 진입.  add 모드와
        /// 같은 drag-rect 입력을 쓰되 RemoveRoof 를 호출한다.  ArchitectMenu Zone →
        /// 지붕 제거 행(또는 Shift+U 토글)에서 호출.</summary>
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

            // ---- build progression tick (the reference sim처럼 '짓는 중' → '지어진 지붕') ----
            //  Cheap O(pending) scan: only recomputes the pending count, and only does
            //  any work while at least one cell is still building.  No coroutine /
            //  WaitForSeconds heartbeat — this rides the existing per-frame Update
            //  (bug-pattern #9 firewall: no new always-on background timer).  Once
            //  PendingCount hits 0 the early-out makes this effectively free.
            TickBuildProgress();

            // Hotkey U (roof "Up") — free key (build B/F/G/T/Y, N/R, deconstruct X,
            //  mine M, plant P, stockpile O, floor-stone K, table J, lamp L, fence E,
            //  H, and WASD camera are all taken).  Shift+U toggles the ERASE submode
            //  (운영자 fb: 지붕 영역 제거); plain U is add.
            if (Input.GetKeyDown(KeyCode.U))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shift) { if (ModeActive && EraseMode) SetEraseMode(false); else SetEraseMode(true); }
                else       { if (ModeActive && !EraseMode) SetMode(false);     else SetMode(true); }
            }

            // If another mode was entered elsewhere, leave roof mode so they never
            //  fight over the same left-click.
            if (ModeActive && AnotherModeActive()) SetMode(false);

            if (ModeActive)
            {
                // Right-click / ESC cancels the mode (same convention as build).
                if (SimInput.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
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
            if (EraseMode) { RemoveRoof(cell, playBlip: true, fx: true); return cell; }
            AddRoof(cell, playBlip: true, fx: true);
            // the reference sim auto-roof: if this single cell completed/enclosed a room, fill it.
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
                    if (EraseMode ? RemoveRoof(new Vector2Int(cx, cy), playBlip: false, fx: false)
                                  : AddRoof(new Vector2Int(cx, cy), playBlip: false, fx: false)) n++;
            if (n > 0)
            {
                // Single throttled blip + one FX at the rect centre for the whole
                //  sweep (never a per-cell tight-loop buzz — bug-pattern #4 firewall).
                AudioBank.Instance?.PlaySelect();
                ClickEffect.Spawn(
                    new Vector3((x0 + x1) * 0.5f + 0.5f, (y0 + y1) * 0.5f + 0.5f, 0f),
                    new Color(0.30f, 0.32f, 0.40f, 0.95f)); // slate shade
                Debug.Log($"[Roof] {(EraseMode ? "removed" : "designated")} {n} cell(s) roof area");
            }
            return n;
        }

        // ---- build progression -----------------------------------------------

        /// <summary>Promote any '짓는 중'(pending) cell whose build-done time has
        /// arrived to BUILT, and keep PendingCount in sync.  Called once per frame from
        /// Update; early-outs to (near) free once nothing is building.  When a cell
        /// finishes we DON'T bump Version (membership unchanged) — the renderer watches
        /// PendingCount to refresh on a pending→built transition.</summary>
        // ---- 지붕 노동 시공 API (소크 r2 관찰 #1) -----------------------------
        //  cell → 누적 노동초 / cell → 점유 빌더.  완료 시 roofCells[cell]=now 로
        //  바꿔 두면 기존 TickBuildProgress 가 다음 프레임에 BUILT 로 승격 +
        //  PendingCount 동기화 + 배치 로그까지 기존 경로 그대로 처리한다.
        private readonly Dictionary<Vector2Int, float> roofWork = new Dictionary<Vector2Int, float>(32);
        private readonly Dictionary<Vector2Int, GameObject> roofClaims = new Dictionary<Vector2Int, GameObject>(8);

        /// <summary>점유되지 않은 가장 가까운 pending 지붕 셀을 점유. 없으면 false.</summary>
        public bool TryClaimNearestPending(Vector3 from, GameObject claimant, out Vector2Int best)
        {
            best = default; float bestSq = float.MaxValue; bool found = false;
            foreach (var kv in roofCells)
            {
                if (!float.IsPositiveInfinity(kv.Value)) continue;   // BUILT(또는 승격 직전)
                if (roofClaims.TryGetValue(kv.Key, out var by) && by != null && by != claimant) continue;
                if (!HasSupport(kv.Key)) continue;   // 소크 r3 #3 — 벽 골조 먼저, 지붕은 그 다음
                Vector2 c = new Vector2(kv.Key.x + 0.5f, kv.Key.y + 0.5f);
                float sq = (c - (Vector2)from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = kv.Key; found = true; }
            }
            if (found) roofClaims[best] = claimant;
            return found;
        }

        /// <summary>소크 r3 이상 관찰 #3 — 지붕이 벽 골조와 무관하게 시공되던 문제.
        /// 레퍼런스처럼 지지 구조물(벽/문)이 근처(체비셰프 2셀)에 완공돼 있어야 시공
        /// 가능.  지지가 없으면 영구 pending — 벽이 완공되는 순간 자연히 시공 시작
        /// = 골조→지붕 순서가 시스템으로 강제된다.</summary>
        private static bool HasSupport(Vector2Int cell)
        {
            foreach (var w in UnityEngine.Object.FindObjectsByType<WallEntity>(FindObjectsSortMode.None))
            {
                if (w == null) continue;
                Vector3 p = w.transform.position;
                if (Mathf.Abs(Mathf.FloorToInt(p.x) - cell.x) <= 2
                    && Mathf.Abs(Mathf.FloorToInt(p.y) - cell.y) <= 2) return true;
            }
            foreach (var d in UnityEngine.Object.FindObjectsByType<DoorEntity>(FindObjectsSortMode.None))
            {
                if (d == null) continue;
                Vector3 p = d.transform.position;
                if (Mathf.Abs(Mathf.FloorToInt(p.x) - cell.x) <= 2
                    && Mathf.Abs(Mathf.FloorToInt(p.y) - cell.y) <= 2) return true;
            }
            return false;
        }

        public void ReleaseClaim(Vector2Int cell, GameObject claimant)
        {
            if (roofClaims.TryGetValue(cell, out var by) && by == claimant) roofClaims.Remove(cell);
        }

        public bool IsPendingCell(Vector2Int cell)
            => roofCells.TryGetValue(cell, out float v) && float.IsPositiveInfinity(v);

        /// <summary>빌더의 프레임당 노동 적립.  완료(또는 셀 소멸) 시 true.</summary>
        public bool TickRoofWork(Vector2Int cell, float workSec, string builderName)
        {
            if (!IsPendingCell(cell)) { roofClaims.Remove(cell); roofWork.Remove(cell); return true; }
            float w = (roofWork.TryGetValue(cell, out float cur) ? cur : 0f) + workSec;
            if (w < Mathf.Max(0.1f, buildSeconds)) { roofWork[cell] = w; return false; }
            roofCells[cell] = Time.time - 0.001f;   // BUILT — TickBuildProgress 가 승격 처리
            roofWork.Remove(cell); roofClaims.Remove(cell);
            Debug.Log($"[Roof] built ({cell.x},{cell.y}) by {builderName}");
            return true;
        }

        private void TickBuildProgress()
        {
            if (PendingCount == 0) return;   // nothing building — effectively free

            int pending = 0;
            float now = Time.time;
            foreach (var kv in roofCells)
                if (now < kv.Value) pending++;   // still '짓는 중'

            int finished = PendingCount - pending;   // how many promoted to BUILT this frame
            PendingCount = pending;

            if (finished > 0)
            {
                // One throttled blip when a batch of roof finishes building (never a
                //  per-cell tight loop — bug-pattern #4 firewall; PlaySelect self-throttles).
                AudioBank.Instance?.PlaySelect();
                Debug.Log($"[Roof] {finished} cell(s) finished building → 지어진 지붕");
            }
        }

        /// <summary>Add one cell to the roof set.  Idempotent (a re-roof of an already-
        /// designated cell is a silent no-op).  Bumps Version on a real change so the
        /// overlay rebuilds.  The cell starts '짓는 중'(pending) and auto-promotes to
        /// BUILT after buildSeconds (TickBuildProgress).  Returns true if newly added.</summary>
        private bool AddRoof(Vector2Int cell, bool playBlip, bool fx)
        {
            if (roofCells.ContainsKey(cell)) return false;   // idempotent re-roof (silent)
            // 노동 시공: 빌더가 TickRoofWork 로 완료할 때까지 무기한 pending.
            roofCells.Add(cell, float.PositiveInfinity);
            PendingCount++;                                   // starts building (노동 대기)
            Version++;
            if (playBlip) AudioBank.Instance?.PlaySelect();
            if (fx)
            {
                ClickEffect.Spawn(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f),
                    new Color(0.30f, 0.32f, 0.40f, 0.95f)); // slate shade
                Debug.Log($"[Roof] designated cell ({cell.x},{cell.y}) — 지붕 짓는 중");
            }
            return true;
        }

        /// <summary>운영자 fb (2026-06-01): remove one cell from the roofed set (erase
        /// mode).  Symmetric to AddRoof — idempotent (un-roofing a non-roofed cell is a
        /// silent no-op), bumps Version on a real change so RoofOverlayRenderer rebuilds
        /// and the shade quad disappears.  Returns true if the cell was actually removed.</summary>
        private bool RemoveRoof(Vector2Int cell, bool playBlip, bool fx)
        {
            if (!roofCells.TryGetValue(cell, out float done)) return false;   // wasn't designated — silent no-op
            roofCells.Remove(cell);
            roofWork.Remove(cell); roofClaims.Remove(cell);
            if (Time.time < done && PendingCount > 0) PendingCount--;   // it was still building
            Version++;
            if (playBlip) AudioBank.Instance?.PlaySelect();
            if (fx)
            {
                ClickEffect.Spawn(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f),
                    new Color(0.55f, 0.55f, 0.60f, 0.85f)); // lighter slate = removed
                Debug.Log($"[Roof] removed roof at cell ({cell.x},{cell.y})");
            }
            return true;
        }

        /// <summary>QA / test entry point: erase the roof on a specific cell directly.</summary>
        public bool EraseCell(Vector2Int cell) => RemoveRoof(cell, playBlip: true, fx: true);

        // ---- the reference sim auto-roof of a wall-enclosed room ----------------------
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
