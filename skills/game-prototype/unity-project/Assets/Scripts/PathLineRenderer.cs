using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 요청 — 림 우클릭 이동 시 이동 경로 표시.
    ///
    /// Draws the SELECTED pawn's live A* path (PawnMovement) as a world-space
    /// polyline connecting cell centres, from the pawn's feet to its destination.
    /// RimWorld shows this faint dotted route when you order a colonist somewhere;
    /// here it is a muted solid LineRenderer that appears on a move order and
    /// disappears the instant the pawn arrives (path cleared) or selection is lost.
    ///
    /// Lane discipline: NEW self-bootstrapping file.  It reads
    /// ClickSelector.CurrentSelection and PawnMovement's READ-ONLY path getters
    /// (TryGetRemainingPathWorld / PathVersionToken) — it never edits either.
    /// PawnMovement gained only those two read getters; its movement logic is
    /// byte-for-byte unchanged.
    ///
    /// Bug-pattern firewall:
    ///  #4  audio buzz       — no AudioBank calls at all.
    ///  #5  GetInstanceID     — no GetInstanceID compares (no collision logic).
    ///  #6  OnCollisionEnter  — no collision handlers.
    ///  #7  Singleton race     — ClickSelector is POLLED in Update (FindFirstObjectByType
    ///                            cached), never subscribed to in OnEnable.
    ///  #8  justSpawned        — no spawn-grace field.
    ///  #9  runInBackground    — no coroutine / WaitForSeconds / background timer;
    ///                            everything is frame-driven in Update.
    /// GC hygiene: the world-point List is reused; the LineRenderer's positions are
    /// only rebuilt when PawnMovement.PathVersionToken changes (path rebuilt or a
    /// waypoint advanced), so steady-state frames allocate nothing.
    /// </summary>
    public class PathLineRenderer : MonoBehaviour
    {
        // --- tunables (SerializeField so designer can tweak day-1 feel) --------
        [Tooltip("Line width in world units (muted, thin — a route hint, not a wall).")]
        [SerializeField] private float lineWidth = 0.12f;

        [Tooltip("Muted route colour (low alpha so it reads as a hint under the pawns).")]
        [SerializeField] private Color lineColor = new Color(0.55f, 0.85f, 1.0f, 0.55f);

        [Tooltip("Sorting order: above ground/floor (0) and selection ring (6), below pawns (10).")]
        [SerializeField] private int sortingOrder = 8;

        // --- runtime state ----------------------------------------------------
        private LineRenderer line;
        private ClickSelector cachedSelector;

        // Reused buffer — no per-frame List allocation (GC hygiene).
        private readonly List<Vector2> pointBuf = new List<Vector2>(64);

        // Cache: which path-version is currently drawn, so we rebuild positions only
        //  when it actually changes (waypoint advance or full re-path).  -1 = nothing
        //  drawn (forces a rebuild on the first visible frame).
        private int drawnToken = -1;
        private PawnMovement drawnMovement;   // movement whose path is currently drawn

        // ============================================================
        //  Self-bootstrap — GameSceneGate (never spawn on MainMenu).
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindExisting() != null) return;   // idempotent — never a 2nd renderer
                var go = new GameObject("~PathLineRenderer");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<PathLineRenderer>();
            });
        }

        private static PathLineRenderer FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<PathLineRenderer>();
#else
            return Object.FindObjectOfType<PathLineRenderer>();
#endif
        }

        private void Awake()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;                 // world space — points are world coords
            line.alignment = LineAlignment.View;       // flat ribbon facing the (2D) camera
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = lineWidth;
            line.positionCount = 0;

            // Built-in unlit sprite shader is present in every Unity install and
            //  respects vertex colour + alpha; gives a clean muted line in 2D.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            line.material = mat;
            line.startColor = lineColor;
            line.endColor = lineColor;

            line.sortingOrder = sortingOrder;          // above ground/ring, below pawns
            line.enabled = false;                      // hidden until there's a path
        }

        private ClickSelector FindSelector()
        {
            if (cachedSelector != null) return cachedSelector;
#if UNITY_2023_1_OR_NEWER
            cachedSelector = Object.FindFirstObjectByType<ClickSelector>();
#else
            cachedSelector = Object.FindObjectOfType<ClickSelector>();
#endif
            return cachedSelector;
        }

        private void Update()
        {
            // Poll selection (singleton-subscriber pattern — never subscribe in OnEnable).
            ClickSelector sel = FindSelector();
            PawnEntity pawn = sel != null ? sel.CurrentSelection : null;

            // Nothing selected → hide.
            if (pawn == null)
            {
                Hide();
                return;
            }

            PawnMovement mv = pawn.GetComponent<PawnMovement>();
            if (mv == null)
            {
                Hide();
                return;
            }

            // Selection changed since last frame → force a rebuild of the line.
            if (!ReferenceEquals(mv, drawnMovement))
            {
                drawnMovement = mv;
                drawnToken = -1;
            }

            // No active path (idle / arrived) → hide.  PathVersionToken is 0 when the
            //  path is null, so this also clears the line on arrival.
            int token = mv.PathVersionToken;
            if (token == 0)
            {
                Hide();
                return;
            }

            // Same path as last frame → leave the existing LineRenderer untouched
            //  (steady-state fast path, zero allocation).
            if (token == drawnToken && line.enabled) return;

            // Path changed (rebuilt or advanced a waypoint) → refresh points.
            if (!mv.TryGetRemainingPathWorld(pointBuf) || pointBuf.Count < 2)
            {
                Hide();
                return;
            }

            line.positionCount = pointBuf.Count;
            for (int i = 0; i < pointBuf.Count; i++)
            {
                Vector2 p = pointBuf[i];
                // Slightly negative z keeps the line in front of ground sprites that
                //  sit at z=0 even before sorting order is considered (belt + braces).
                line.SetPosition(i, new Vector3(p.x, p.y, -0.05f));
            }
            line.enabled = true;
            drawnToken = token;
        }

        private void Hide()
        {
            if (line.enabled)
            {
                line.enabled = false;
                line.positionCount = 0;
            }
            drawnToken = -1;
            drawnMovement = null;
        }

        private void OnDestroy()
        {
            if (line != null && line.material != null) Destroy(line.material);
        }
    }
}
