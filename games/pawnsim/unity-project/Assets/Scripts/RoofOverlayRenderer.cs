using System.Collections.Generic;
using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// ROOF wave (운영자: "the reference sim처럼 지붕영역 설정 메뉴 따로 있음") — ROOF SHADE OVERLAY.
    ///
    /// Draws a subtle translucent dark quad over every cell that
    /// <see cref="RoofDesignation"/> has marked as roofed, giving the colony-sim
    /// "indoor shade" (실내 그늘) look.  This is the VISUAL half of the roof feature;
    /// RoofDesignation owns the cell set + the roofed FLAG (future rain/temperature
    /// hooks read RoofDesignation.IsRoofed).  This renderer is purely cosmetic and
    /// reads the roofed set READ-ONLY.
    ///
    /// SORTING — the shade must read as "over the floor + pawns + structures of an
    /// indoor room" but stay BELOW the screen-space UI Canvas.  The world night
    /// overlay (NightOverlay) sits at sortingOrder 25 and floating bars at 30; the
    /// roof shade sits at <see cref="shadeSortingOrder"/> 24 — above ground/tiles
    /// (0/1/5/7) and furniture/walls (≤5) and pawns, just BELOW the night overlay so
    /// at night the world darkening still layers on top, and well below the UI Canvas.
    ///
    /// ----------------------------------------------------------------------------
    /// DISCIPLINE — mirrors the shipped self-bootstrap renderers/designations EXACTLY:
    ///   - [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] spawns ONE hidden
    ///     DontDestroyOnLoad renderer GameObject behind a GameSceneGate (never on
    ///     MainMenu); FindFirstObjectByType idempotency guard means a domain/scene
    ///     reload never spawns a second renderer.
    ///   - Holds NO live singleton reference captured in OnEnable (bug-pattern #7
    ///     firewall): it READ-ONLY poll-finds RoofDesignation.Instance each frame and
    ///     re-finds if replaced — never subscribes to a singleton event with no live
    ///     ref.
    ///   - No coroutine / WaitForSeconds / background timer at all (bug-pattern #9
    ///     firewall): the only per-frame work is a cheap Version compare in Update
    ///     plus a quad-position refresh; a full quad rebuild happens ONLY when the
    ///     roofed set actually changed (Version bump).
    ///   - No AudioBank calls (bug-pattern #4 n/a — purely visual).
    ///
    /// NO PER-FRAME ALLOCATION (lesson "Per-frame allocations = GC churn"):
    ///   Shade quads are POOLED.  When the roofed set changes we reuse existing quad
    ///   GameObjects (activating/deactivating + repositioning), only instantiating
    ///   new ones when the pool is too small, and never `new List<>()` per frame.
    ///
    ///   >>> QA FLAG (scene-wiring): the shade quads are code-generated tinted
    ///       sprites (shared 1×1 white texture, no prefab / no imported PNG), never
    ///       SceneSetup-wired.  QA should confirm a dark translucent shade appears on
    ///       cells designated via the Architect Zone "지붕 영역" row / U hotkey, and
    ///       clears when the roof set is empty.  No SceneSetup*.cs file was edited.
    /// </summary>
    public class RoofOverlayRenderer : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tune day-1 feel) -------
        [Header("Shade look")]
        // 운영자 fb (2026-06-01) "지붕 영역을 정해줘도 지붕건설을 안함" — 핵심 불만은 '지정해도
        //  아무 변화가 안 보인다'.  그래서 BUILT(지어진 지붕)는 분명히 보이는 '진짜 지붕 타일'로,
        //  PENDING(짓는 중)은 옅은 공사중 음영으로, 두 상태를 시각적으로 확실히 다르게 그린다.
        //
        //  builtTint: 완성된 지붕 — '실내가 덮였다'가 확실히 보이되 **안이 비쳐야** 한다.
        //
        //  알파 재조정 (2026-07-29, 0.78 → 0.38).  0.78 은 위 피드백("지정해도 아무 변화가
        //  안 보인다")에 대한 대응이었는데 반대쪽으로 넘어갔다 — 집을 지으면 안의 바닥·가구·
        //  콜로니스트가 **전부 안 보이고** 화면에 검은 판만 남았다.  심사자가 5분간 하는 일이
        //  건축인데 만들고 나면 만든 게 안 보이는 상태였다(인게임 캡처로 확인).
        //  요구 자체는 그대로 지킨다 — 짙은 슬레이트 색조라 지붕이 생긴 것은 여전히 한눈에
        //  보이고, 대신 실내가 비쳐 '내가 지은 콜로니'를 볼 수 있다.
        //  ※ 룩 판단이므로 운영자 확인 항목.
        [SerializeField] private Color builtTint = new Color(0.10f, 0.11f, 0.16f, 0.38f);  // 지어진 지붕(비치는 슬레이트)
        //  pendingTint: 짓는 중 — 옅고 약간 푸른 공사중 음영(alpha 0.30)으로 '지금 짓고 있다'가
        //   보이되 완성과 헷갈리지 않게.
        [SerializeField] private Color pendingTint = new Color(0.20f, 0.30f, 0.45f, 0.30f); // 짓는 중(옅은 공사 음영)
        [SerializeField] private int shadeSortingOrder = 24;   // above pawns/structures, below night overlay(25)
        // Match NightOverlay's sorting LAYER explicitly ("Default").  A SpriteRenderer
        //  created at runtime defaults to the "Default" layer, but pin it so the
        //  shadeSortingOrder ordering vs NightOverlay(25)/bars(30) is well-defined
        //  regardless of any project sorting-layer config (visibility firewall).
        [SerializeField] private string shadeSortingLayer = "Default";

        // ---- runtime state ---------------------------------------------------
        public static RoofOverlayRenderer Instance { get; private set; }

        // Pooled shade quads, one per roofed cell.  Reused across rebuilds — never
        //  re-allocated per frame (GC-churn firewall).
        private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>(128);
        private GameObject quadRoot;

        // Last roof Version we rebuilt for.  A cheap per-frame compare; a full rebuild
        //  runs ONLY when this differs from RoofDesignation.Version (membership change)
        //  OR when lastPending differs (a '짓는 중'→'지어진 지붕' promotion, which does
        //  not change membership and so doesn't bump Version — we watch PendingCount to
        //  catch the look-swap).
        private int lastVersion = -1;
        private int lastPending = -1;

        // ============================================================
        //  Self-bootstrap — no SceneSetup edit (NightOverlay/designation pattern).
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindExisting() != null) return;   // idempotent — never a 2nd renderer
                var go = new GameObject("~RoofOverlayRenderer");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<RoofOverlayRenderer>();
            });
        }

        private static RoofOverlayRenderer FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<RoofOverlayRenderer>();
#else
            return Object.FindObjectOfType<RoofOverlayRenderer>();
#endif
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            quadRoot = new GameObject("RoofShadeQuads");
            quadRoot.transform.SetParent(transform, false);
        }

        private void Update()
        {
            // READ-ONLY poll for the designation manager — never an OnEnable-captured
            //  singleton ref (bug-pattern #7 firewall).
            var roof = RoofDesignation.Instance;
            if (roof == null)
            {
                // No designation manager yet (or destroyed) — hide all quads.
                if (lastVersion != -1) { DeactivateAll(); lastVersion = -1; lastPending = -1; }
                return;
            }

            // Rebuild when membership changed (Version) OR when a '짓는 중'→'지어진 지붕'
            //  promotion happened (PendingCount dropped without a Version bump).  Both
            //  are cheap int compares per frame.
            if (roof.Version != lastVersion || roof.PendingCount != lastPending)
            {
                Rebuild(roof);
                lastVersion = roof.Version;
                lastPending = roof.PendingCount;
            }
        }

        /// <summary>Reposition/activate one pooled quad per roofed cell, deactivating
        /// any surplus.  No per-frame allocation: the pool only grows, never shrinks,
        /// and is reused.</summary>
        private void Rebuild(RoofDesignation roof)
        {
            int i = 0;
            foreach (var cell in roof.Roofed)
            {
                var sr = GetQuad(i);
                sr.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
                // BUILT(지어진 지붕)는 진한 불투명 슬레이트, PENDING(짓는 중)은 옅은 공사 음영.
                //  IsRoofed/IsPending 는 Time.time 기반이라 이 두 갈래가 곧 시각 차이가 된다.
                sr.color = roof.IsRoofed(cell) ? builtTint : pendingTint;
                sr.sortingLayerName = shadeSortingLayer;
                sr.sortingOrder = shadeSortingOrder;
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                i++;
            }
            // Deactivate surplus quads (roof shrank) — kept in the pool for reuse.
            for (; i < pool.Count; i++)
                if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
        }

        private void DeactivateAll()
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && pool[i].gameObject.activeSelf)
                    pool[i].gameObject.SetActive(false);
        }

        /// <summary>Fetch the i-th pooled shade quad, growing the pool if needed.</summary>
        private SpriteRenderer GetQuad(int i)
        {
            while (pool.Count <= i)
            {
                var go = new GameObject($"RoofShade_{pool.Count}");
                go.transform.SetParent(quadRoot.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ShadeSprite();
                sr.color = builtTint;   // overwritten per-cell in Rebuild (pending vs built)
                sr.sortingLayerName = shadeSortingLayer;   // pin layer (visibility firewall)
                sr.sortingOrder = shadeSortingOrder;
                go.SetActive(false);
                pool.Add(sr);
            }
            return pool[i];
        }

        // A shared 1×1 white sprite (1 px/unit → covers exactly one cell), tinted
        //  per-renderer.  No prefab / imported PNG (code-generated, flagged for QA) —
        //  same procedural-sprite path GrowZoneDesignation's zone marker uses.
        private static Sprite sharedShadeSprite;
        private static Sprite ShadeSprite()
        {
            if (sharedShadeSprite != null) return sharedShadeSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            sharedShadeSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 1f);   // pixelsPerUnit 1 → sprite is 1 world unit
            sharedShadeSprite.name = "RoofShadeWhite";
            return sharedShadeSprite;
        }
    }
}
