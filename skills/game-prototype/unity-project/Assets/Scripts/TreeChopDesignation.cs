using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using MelonS.GameProto.AI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// #231 운영자 fb "우클릭으로 벌목이 안됨" — RimWorld 정합.  STEP2 에서 자동 벌목을
    /// 끊은 뒤 벌목 수단이 per-tree 우클릭 메뉴뿐이라 불편/발견難 했다.  RimWorld 처럼
    /// 야생나무 '벌목' 을 drag-지정하는 도구.  MineDesignation(채광)의 정확한 미러 —
    /// 같은 drag-rect 마킹 + 같은 throttled idle-worker dispatch, 단 StoneVeinEntity/
    /// PawnMiner 대신 TreeEntity/PawnChopper 를 대상으로 한다.  PawnChopper 의 기존
    /// SetTreeTarget(walk→chop) 경로를 그대로 쓰고 AI 파일은 건드리지 않는다.
    ///
    /// 진입: ArchitectMenu Orders(지시) → "벌목 (C)" → SetMode(true), 또는 C 핫키.
    /// </summary>
    public class TreeChopDesignation : MonoBehaviour
    {
        [SerializeField] private float dispatchInterval = 0.5f;
        [SerializeField] private float pickRadius = 0.45f;
        [SerializeField] private float dragThreshold = 0.35f;

        public static TreeChopDesignation Instance { get; private set; }
        public bool ModeActive { get; private set; }

        private Camera cam;
        private float lastDispatch = -999f;
        private readonly List<ChopTarget> marked = new List<ChopTarget>(32);
        private bool dragging;
        private Vector3 dragStartWorld;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindExisting() != null) return;
                var go = new GameObject("~TreeChopDesignation");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<TreeChopDesignation>();
            });
        }

        private static TreeChopDesignation FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<TreeChopDesignation>();
#else
            return Object.FindObjectOfType<TreeChopDesignation>();
#endif
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            cam = Camera.main;
        }

        /// <summary>벌목 모드 on/off.  build/mine/deconstruct 모드와 상호배타(한 클릭 = 한 핸들러).</summary>
        public void SetMode(bool on)
        {
            ModeActive = on;
            if (on)
            {
                if (BuildManager.Instance != null && BuildManager.Instance.BuildModeActive)
                    BuildManager.Instance.SetMode(BuildManager.Mode.Off);
                if (MineDesignation.Instance != null && MineDesignation.Instance.ModeActive)
                    MineDesignation.Instance.SetMode(false);
                if (DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive)
                    DeconstructDesignation.Instance.SetMode(false);
            }
            if (!on) dragging = false;
        }

        public void Toggle() => SetMode(!ModeActive);

        private void Update()
        {
            if (cam == null) cam = Camera.main;
            if (Input.GetKeyDown(KeyCode.C)) Toggle();

            if (ModeActive)
            {
                bool buildOn = BuildManager.Instance != null && BuildManager.Instance.BuildModeActive;
                bool mineOn  = MineDesignation.Instance != null && MineDesignation.Instance.ModeActive;
                bool deconOn = DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive;
                if (buildOn || mineOn || deconOn) SetMode(false);
            }

            if (ModeActive)
            {
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) SetMode(false);
                else HandleDragInput();
            }

            PruneMarked();
            DispatchToIdleChoppers();
        }

        private void HandleDragInput()
        {
            if (cam == null) return;
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                dragging = true;
                dragStartWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0) && dragging)
            {
                dragging = false;
                Vector3 endWorld = cam.ScreenToWorldPoint(Input.mousePosition);
                if (Vector3.Distance(endWorld, dragStartWorld) < dragThreshold) MarkCell(endWorld);
                else MarkRect(dragStartWorld, endWorld);
            }
        }

        // QA / test entry points (no real pointer needed) — mirror MineDesignation.
        public ChopTarget SimulateClick(Vector2 screenPos)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return null;
            return MarkCell(cam.ScreenToWorldPoint(screenPos));
        }
        public int SimulateDragRect(Vector2 worldA, Vector2 worldB) => MarkRect(worldA, worldB);
        /// <summary>월드 위치의 나무를 직접 마킹(테스트용, screen 변환 없이).</summary>
        public ChopTarget MarkWorld(Vector2 world) => MarkCell(new Vector3(world.x, world.y, 0f));

        private ChopTarget MarkCell(Vector3 world)
            => MarkTreeAtCell(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

        private int MarkRect(Vector3 a, Vector3 b)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x));
            int x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y));
            int y1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y));
            int n = 0;
            for (int cx = x0; cx <= x1; cx++)
                for (int cy = y0; cy <= y1; cy++)
                    if (MarkTreeAtCell(cx, cy) != null) n++;
            return n;
        }

        private ChopTarget MarkTreeAtCell(int cx, int cy)
        {
            Vector2 cellCenter = new Vector2(cx + 0.5f, cy + 0.5f);
            var hits = Physics2D.OverlapBoxAll(cellCenter, Vector2.one * pickRadius, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var ct = TryMark(h.gameObject);
                if (ct != null) return ct;
            }
            return null;
        }

        public ChopTarget TryMark(GameObject go)
        {
            if (go == null) return null;
            var existing = go.GetComponent<ChopTarget>();
            if (existing != null) return existing;          // idempotent re-mark
            if (!ChopTarget.IsChoppable(go)) return null;
            var ct = go.AddComponent<ChopTarget>();
            ct.Initialize();
            marked.Add(ct);
            AudioBank.Instance?.PlaySelect();
            ClickEffect.Spawn(go.transform.position, new Color(0.55f, 0.40f, 0.22f, 0.95f)); // wood/bark
            Debug.Log($"[Chop] designated {go.name} for chopping");
            return ct;
        }

        private void PruneMarked()
        {
            for (int i = marked.Count - 1; i >= 0; i--)
            {
                var m = marked[i];
                if (m == null || m.gameObject == null || m.IsChopped) marked.RemoveAt(i);
            }
        }

        // idle PawnChopper 에 마킹된 나무를 직접 배정 (MineDesignation.DispatchToIdleMiners 미러).
        private void DispatchToIdleChoppers()
        {
            if (Time.unscaledTime - lastDispatch < dispatchInterval) return;
            lastDispatch = Time.unscaledTime;
            if (marked.Count == 0) return;

            var choppers = FindChoppers();
            if (choppers == null || choppers.Length == 0) return;

            foreach (var ct in marked)
            {
                if (ct == null || ct.IsChopped) continue;
                if (ReservationManager.IsReserved(ct.gameObject)) continue;
                PawnChopper best = null;
                float bestSq = float.MaxValue;
                Vector2 tp = ct.transform.position;
                foreach (var c in choppers)
                {
                    if (c == null) continue;
                    if (c.HasTask) continue;
                    if (!ChopperAvailable(c)) continue;
                    if (c.IsRecentlyGivenUp(ct.Tree)) continue;  // #236 도달 불가 나무 무한 재배정 방지
                    float sq = ((Vector2)c.transform.position - tp).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = c; }
                }
                if (best != null) best.SetTreeTarget(ct.Tree);
            }
        }

        private static bool ChopperAvailable(PawnChopper c)
        {
            var entity = c.GetComponent<PawnEntity>();
            if (entity != null && (entity.IsDrafted || entity.IsUnderManualControl)) return false;
            var needs = c.GetComponent<PawnNeeds>();
            if (needs != null && (needs.IsSleeping || needs.IsBreaking)) return false;
            return true;
        }

        private static PawnChopper[] FindChoppers()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<PawnChopper>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<PawnChopper>();
#endif
        }
    }

    /// <summary>#231 — 벌목 지정 마커.  플레이어가 벌목 지정한 TreeEntity 에 붙는 런타임
    /// 컴포넌트.  "🪓" 글리프 오버레이 + TreeEntity 백레퍼런스(dispatch 가 PawnChopper 에
    /// 넘김).  실제 벌목/목재드롭은 기존 TreeEntity.TakeChopDamage(PawnChopper 구동)가 전담.
    /// 나무가 쓰러지면(Destroy) 이 컴포넌트도 같이 사라지고 매니저 리스트에서 prune 된다.
    /// MineTarget 의 정확한 미러.</summary>
    public class ChopTarget : MonoBehaviour
    {
        private TreeEntity tree;
        private GameObject marker;

        public TreeEntity Tree => tree;
        public bool IsChopped => tree == null || tree.IsDestroyed;

        public static bool IsChoppable(GameObject go)
        {
            if (go == null) return false;
            var t = go.GetComponent<TreeEntity>();
            return t != null && !t.IsDestroyed;
        }

        public void Initialize()
        {
            tree = GetComponent<TreeEntity>();
            BuildMarker();
        }

        private void BuildMarker()
        {
            if (marker != null) return;
            marker = new GameObject("ChopMark");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            var tm = marker.AddComponent<TextMesh>();
            tm.text = "🪓";
            tm.fontSize = 36;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.95f, 0.85f, 0.55f, 1f);
            var mr = marker.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 30;
        }
    }
}
