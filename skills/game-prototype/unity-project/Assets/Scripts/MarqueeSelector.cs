using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using MelonS.GameProto.AI;   // #264 PathGrid (formation fan-out)

namespace MelonS.GameProto
{
    /// <summary>
    /// B1 (genre-comparison-v2) — Multi-select marquee.
    ///
    /// Owns the left-DRAG gesture on empty ground: mouse-down on empty ground,
    /// move past a small pixel threshold, mouse-up.  On release it OverlapArea's
    /// the box, collects every PawnEntity inside, shows each pawn's selection ring
    /// (PawnEntity.SetSelected(true)) and exposes <see cref="CurrentMultiSelection"/>.
    /// A right-click then issues a move-order to ALL selected pawns, reusing the
    /// EXACT single-select empty-ground move path (PawnMovement.SetTarget +
    /// ManualMoveUntil) — NO new pathfinding is introduced.
    ///
    /// Lane discipline: this is a NEW self-bootstrapping file.  It reads
    /// ClickSelector / PawnEntity / PawnMovement / BuildManager / the designation
    /// managers READ-ONLY and never edits them.  A single left-CLICK (press &amp;
    /// release WITHOUT crossing the drag threshold) is deliberately ignored here so
    /// ClickSelector still owns single-selection — the two systems never double-fire
    /// because the click-vs-drag split is exclusive (a gesture is EITHER under the
    /// threshold = click = ClickSelector, OR over it = drag = this file).
    ///
    /// Bug-pattern firewall: no AudioBank loop, no GetInstanceID compares, no
    /// collision handlers, no Singleton.OnX subscription (managers are polled in
    /// Update, singleton-subscriber pattern), no justSpawned default, no
    /// WaitForSeconds/background timer (no coroutines).  The GL overlay draws in
    /// OnRenderObject which is frame-driven, not time-driven.
    /// </summary>
    public class MarqueeSelector : MonoBehaviour
    {
        // --- tunables (SerializeField so designer can tweak day-1 feel) --------
        [Tooltip("Pixels the cursor must move from press before a press becomes a marquee DRAG (below this = a single click, left to ClickSelector).")]
        [SerializeField] private float dragThresholdPixels = 6f;

        // 감사 rank4: 형광 녹색 → warm gold(선택 계열 통일).  씬에 옛 녹색이 baked 돼
        //  SerializeField 기본값을 덮으므로 non-serialized 로 둬 source(gold)가 항상 적용되게 함.
        //  AccentGold = (0.957,0.843,0.541).
        private Color marqueeFill = new Color(0.957f, 0.843f, 0.541f, 0.12f);
        private Color marqueeBorder = new Color(0.957f, 0.843f, 0.541f, 0.85f);

        [Tooltip("Manual-control grace applied to each pawn on a multi move-order (matches ClickSelector's 15f).")]
        [SerializeField] private float manualMoveGrace = 15f;

        // --- runtime state ----------------------------------------------------
        private Camera cam;

        private bool pressArmed;          // a left-press began on empty, eligible ground
        private bool dragging;            // press has crossed the pixel threshold -> marquee live
        private Vector3 pressScreen;      // screen-space press origin (for threshold test)
        private Vector3 pressWorld;       // world-space press origin (marquee corner)
        private Vector3 curWorld;         // world-space cursor now (marquee opposite corner)

        // The multi-selection.  Read API for other systems (e.g. a future gizmo bar).
        private readonly List<PawnEntity> multiSelection = new List<PawnEntity>(32);
        /// <summary>Read-only view of the pawns currently multi-selected by a marquee.</summary>
        public IReadOnlyList<PawnEntity> CurrentMultiSelection => multiSelection;
        /// <summary>True while two-or-more pawns are box-selected (marquee owns the selection).</summary>
        public bool HasMultiSelection => multiSelection.Count > 0;

        // GL material for the overlay quad/lines (built lazily, never per-frame).
        private Material lineMat;

        // Reusable buffer for OverlapArea results — no per-frame List allocation.
        private readonly Collider2D[] overlapBuf = new Collider2D[256];

        // ============================================================
        //  Self-bootstrap — no SceneSetup edit (MineDesignation pattern).
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindExisting() != null) return;   // idempotent — never a 2nd marquee
                var go = new GameObject("~MarqueeSelector");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<MarqueeSelector>();
                    });
        }

        private static MarqueeSelector FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<MarqueeSelector>();
#else
            return Object.FindObjectOfType<MarqueeSelector>();
#endif
        }

        private void Awake()
        {
            cam = Camera.main;
        }

        private void EnsureCamera()
        {
            if (cam == null) cam = Camera.main;
        }

        // True when any modal/build/designation mode should suppress marquee input.
        //  Mirrors the guards ClickSelector + the designation managers respect.
        private bool InputBlocked()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return true;
            if (BuildManager.Instance != null && BuildManager.Instance.BuildModeActive)
                return true;
            if (DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive)
                return true;
            if (MineDesignation.Instance != null && MineDesignation.Instance.ModeActive)
                return true;
            if (GrowZoneDesignation.Instance != null && GrowZoneDesignation.Instance.ModeActive)
                return true;
            if (ContextMenuUI.Instance != null && ContextMenuUI.Instance.IsOpen)
                return true;
            return false;
        }

        private Vector3 ScreenToWorld(Vector3 screen)
        {
            Vector3 w = cam.ScreenToWorldPoint(screen);
            w.z = 0f;
            return w;
        }

        private void Update()
        {
            EnsureCamera();
            if (cam == null) return;

            // -------- left-button DOWN: maybe arm a marquee ------------------
            if (Input.GetMouseButtonDown(0))
            {
                pressArmed = false;
                dragging = false;
                if (InputBlocked()) return;

                Vector3 mw = ScreenToWorld(Input.mousePosition);
                // Arm ONLY when the press is on EMPTY ground (no PawnEntity under it).
                //  A press on a pawn is a single-select gesture owned by ClickSelector;
                //  arming there would risk double-handling.  A press on empty ground
                //  that never crosses the threshold also stays a ClickSelector click
                //  (which clears selection) — we only ACT once the threshold is passed.
                Collider2D hit = Physics2D.OverlapPoint(mw);
                bool onPawn = hit != null && hit.GetComponent<PawnEntity>() != null;
                if (onPawn) return;

                pressArmed = true;
                pressScreen = Input.mousePosition;
                pressWorld = mw;
                curWorld = mw;
                return;
            }

            // -------- left-button HELD: promote to drag past threshold -------
            if (pressArmed && Input.GetMouseButton(0))
            {
                curWorld = ScreenToWorld(Input.mousePosition);
                if (!dragging)
                {
                    float moved = Vector2.Distance(Input.mousePosition, pressScreen);
                    if (moved >= dragThresholdPixels)
                    {
                        // Crossing the threshold = this is a marquee, not a click.
                        //  Clear ClickSelector's single selection so the two systems
                        //  never show two competing selections simultaneously.
                        dragging = true;
                        ClearClickSelectorSingle();
                    }
                }
                return;
            }

            // -------- left-button UP: finalize ------------------------------
            if (Input.GetMouseButtonUp(0))
            {
                if (pressArmed && dragging)
                {
                    FinalizeMarquee();
                }
                // Below-threshold release: do NOTHING — ClickSelector handled the click.
                pressArmed = false;
                dragging = false;
                return;
            }

            // -------- right-button: 다중 선택 전원에게 명령 ---------
            //  #262 운영자 fb "징집은 여러 림 한꺼번에 선택해 쓰는 기능인데 여러 마리 선택 시
            //   제대로 안 됨" — 이전엔 우클릭이 이동만 전원 적용했고, '적 우클릭=공격'은 ClickSelector
            //   가 currentSelection(1명)만 처리해 그룹 공격이 안 됐다.  적/늑대/동물을 우클릭하고
            //   선택에 징집된 림이 있으면 **전원**에게 attack/hunt target 을 박는다.  빈 땅이면 전원 이동.
            if (Input.GetMouseButtonDown(1) && HasMultiSelection && !InputBlocked())
            {
                Vector3 wt = ScreenToWorld(Input.mousePosition);
                var hit = Physics2D.OverlapPoint(new Vector2(wt.x, wt.y));
                BanditEnemy bandit = hit != null ? hit.GetComponent<BanditEnemy>() : null;
                WolfEnemy   wolf   = hit != null ? hit.GetComponent<WolfEnemy>()   : null;
                AnimalEntity animal= hit != null ? hit.GetComponent<AnimalEntity>(): null;
                bool anyDrafted = false;
                for (int i = 0; i < multiSelection.Count; i++)
                    if (multiSelection[i] != null && !multiSelection[i].IsDead && multiSelection[i].IsDrafted)
                    { anyDrafted = true; break; }
                if (anyDrafted && (bandit != null || wolf != null || animal != null))
                {
                    int atk = 0;
                    for (int i = 0; i < multiSelection.Count; i++)
                    {
                        var p = multiSelection[i];
                        if (p == null || p.IsDead || !p.IsDrafted) continue;
                        p.DraftedAttackTarget = bandit;
                        p.DraftedWolfTarget   = wolf;
                        p.DraftedHuntTarget   = (bandit == null && wolf == null) ? animal : null;
                        atk++;
                    }
                    ClickEffect.Spawn(wt, new Color(1f, 0.3f, 0.3f, 0.95f));  // 빨강 = 공격 명령
                    string tgtKr = bandit != null ? "적" : (wolf != null ? "늑대" : "동물");
                    Debug.Log($"[MarqueeSelector] 다중 공격 명령 {atk}명 → {tgtKr}");
                }
                else
                {
                    IssueMoveOrderToAll(wt);
                }
            }
        }

        /// <summary>Box-select every PawnEntity inside the drawn rectangle.</summary>
        private void FinalizeMarquee()
        {
            Vector2 min = new Vector2(Mathf.Min(pressWorld.x, curWorld.x), Mathf.Min(pressWorld.y, curWorld.y));
            Vector2 max = new Vector2(Mathf.Max(pressWorld.x, curWorld.x), Mathf.Max(pressWorld.y, curWorld.y));

            // Clear any prior marquee rings before re-selecting.
            ClearMultiSelection();

            int n = Physics2D.OverlapAreaNonAlloc(min, max, overlapBuf);
            for (int i = 0; i < n; i++)
            {
                Collider2D c = overlapBuf[i];
                if (c == null) continue;
                PawnEntity pawn = c.GetComponent<PawnEntity>();
                if (pawn == null || pawn.IsDead) continue;
                if (multiSelection.Contains(pawn)) continue;   // a pawn can carry multiple colliders
                multiSelection.Add(pawn);
                pawn.SetSelected(true);   // shows the selection ring/marker (PawnEntity visual)
            }

            Debug.Log($"[MarqueeSelector] box-selected {multiSelection.Count} pawn(s).");
        }

        /// <summary>Reuse the EXACT single-select empty-ground move path for every pawn:
        ///  PawnMovement.SetTarget + a ManualMoveUntil grace.  NO new pathfinding.</summary>
        private void IssueMoveOrderToAll(Vector3 worldTarget)
        {
            ClickEffect.Spawn(worldTarget, new Color(1f, 0.9f, 0.3f, 0.95f));  // same yellow X as ClickSelector
            var target = new Vector2(worldTarget.x, worldTarget.y);

            // 살아있는 선택 림만 추려 순서 보존 (dead 제거).
            var pawns = new List<PawnEntity>(multiSelection.Count);
            for (int i = multiSelection.Count - 1; i >= 0; i--)
            {
                if (multiSelection[i] == null || multiSelection[i].IsDead) { multiSelection.RemoveAt(i); continue; }
            }
            for (int i = 0; i < multiSelection.Count; i++) pawns.Add(multiSelection[i]);

            // #264 분대 분산: 모두 같은 칸으로 보내 겹치던 버그(운영자 "여러마리 제대로 안 됨")
            //  수정.  target 중심 확장 링에서 walkable 셀을 림 수만큼 골라 1:1 배정 (콜로니심식 fan-out).
            var dests = BuildFormationCells(target, pawns.Count);

            for (int i = 0; i < pawns.Count; i++)
            {
                PawnEntity pawn = pawns[i];
                // Drafted pawns keep their own right-click semantics under ClickSelector;
                //  the marquee move-order applies the undrafted manual-move path, matching
                //  ClickSelector's empty-ground branch (clear residual tasks, then SetTarget).
                var chopper = pawn.GetComponent<PawnChopper>();
                if (chopper != null) chopper.ClearTask();
                var gather = pawn.GetComponent<PawnGatherer>();
                if (gather != null) gather.ClearTask();
                var hunter = pawn.GetComponent<PawnHunter>();
                if (hunter != null) hunter.ClearTask();
                var cook = pawn.GetComponent<PawnCook>();
                if (cook != null) cook.ClearTask();

                var mv = pawn.GetComponent<PawnMovement>();
                if (mv != null) mv.SetTarget(dests[i]);
                pawn.ManualMoveUntil = Time.time + manualMoveGrace;   // AI override grace, same as ClickSelector
            }
            Debug.Log($"[MarqueeSelector] move-order issued to {pawns.Count} pawn(s) (formation fan-out).");
        }

        /// <summary>#264 target 중심 확장 링(8-이웃)에서 walkable 셀을 n개 모아 반환.
        ///  분대 이동 시 림들이 한 칸에 겹치지 않도록 서로 다른 도착 셀을 준다.
        ///  walkable 셀이 부족하면 남은 자리는 target 자체로 채움(최소한의 fallback).</summary>
        private List<Vector2> BuildFormationCells(Vector2 target, int n)
        {
            var result = new List<Vector2>(n > 0 ? n : 1);
            if (n <= 0) return result;
            var grid = PawnMovement.Grid;
            Vector2Int center = PathGrid.WorldToCell(target);
            var used = new HashSet<Vector2Int>();
            for (int ring = 0; ring <= 6 && result.Count < n; ring++)
            {
                for (int dx = -ring; dx <= ring && result.Count < n; dx++)
                for (int dy = -ring; dy <= ring && result.Count < n; dy++)
                {
                    if (ring > 0 && Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring) continue; // 링 경계 셀만
                    var c = new Vector2Int(center.x + dx, center.y + dy);
                    if (!used.Add(c)) continue;
                    if (grid != null && !grid.IsWalkable(c)) continue;
                    result.Add(PathGrid.CellToWorld(c));
                }
            }
            while (result.Count < n) result.Add(target);
            return result;
        }

        /// <summary>Drop all marquee rings + empty the multi-selection list.</summary>
        private void ClearMultiSelection()
        {
            for (int i = 0; i < multiSelection.Count; i++)
            {
                if (multiSelection[i] != null) multiSelection[i].SetSelected(false);
            }
            multiSelection.Clear();
        }

        /// <summary>Read-only nudge to ClickSelector: a marquee owns selection now, so
        ///  the single-select ring should drop.  ClickSelector exposes only Select /
        ///  CurrentSelection publicly; clearing its current pawn via SetSelected(false)
        ///  on that pawn is the minimal read-only way to avoid two competing rings
        ///  WITHOUT editing ClickSelector.  ClickSelector re-evaluates on its next click.</summary>
        private void ClearClickSelectorSingle()
        {
            ClickSelector cs = FindClickSelector();
            if (cs == null) return;
            cs.ClearInspect();  // #버그헌트: 마퀴 시작 시 비-pawn inspect 패널 잔존 해소(pawn 무관)
            PawnEntity single = cs.CurrentSelection;
            if (single == null) return;
            // Don't deselect a pawn we're about to box-select; FinalizeMarquee will set it
            //  true again anyway.  Dropping the visual now keeps the frame consistent.
            single.SetSelected(false);
        }

        private ClickSelector cachedSelector;
        private ClickSelector FindClickSelector()
        {
            if (cachedSelector != null) return cachedSelector;
#if UNITY_2023_1_OR_NEWER
            cachedSelector = Object.FindFirstObjectByType<ClickSelector>();
#else
            cachedSelector = Object.FindObjectOfType<ClickSelector>();
#endif
            return cachedSelector;
        }

        // ============================================================
        //  Marquee overlay — GL lines + a translucent fill quad.
        //  Drawn in OnRenderObject so it needs no extra GameObject/camera.
        // ============================================================
        private void EnsureLineMaterial()
        {
            if (lineMat != null) return;
            // Built-in unlit color shader — present in every Unity install.
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMat = new Material(shader);
            lineMat.hideFlags = HideFlags.HideAndDontSave;
            lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMat.SetInt("_ZWrite", 0);
        }

        private void OnRenderObject()
        {
            if (!dragging || cam == null) return;
            // Only draw for the camera that owns the view (avoid double-draw on extra cams).
            if (Camera.current != cam) return;

            EnsureLineMaterial();
            lineMat.SetPass(0);

            GL.PushMatrix();
            GL.LoadOrtho();   // 0..1 viewport space — convert world corners to viewport.

            Vector3 a = cam.WorldToViewportPoint(pressWorld);
            Vector3 b = cam.WorldToViewportPoint(curWorld);
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
            float y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);

            // translucent fill
            GL.Begin(GL.QUADS);
            GL.Color(marqueeFill);
            GL.Vertex3(x0, y0, 0f);
            GL.Vertex3(x1, y0, 0f);
            GL.Vertex3(x1, y1, 0f);
            GL.Vertex3(x0, y1, 0f);
            GL.End();

            // border
            GL.Begin(GL.LINES);
            GL.Color(marqueeBorder);
            GL.Vertex3(x0, y0, 0f); GL.Vertex3(x1, y0, 0f);
            GL.Vertex3(x1, y0, 0f); GL.Vertex3(x1, y1, 0f);
            GL.Vertex3(x1, y1, 0f); GL.Vertex3(x0, y1, 0f);
            GL.Vertex3(x0, y1, 0f); GL.Vertex3(x0, y0, 0f);
            GL.End();

            GL.PopMatrix();
        }

        private void OnDestroy()
        {
            if (lineMat != null) Destroy(lineMat);
        }
    }
}
