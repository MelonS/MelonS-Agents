using System.Collections.Generic;
using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// ROOF wave (운영자: "RimWorld처럼 지붕영역 설정 메뉴 따로 있음") — ROOF SHADE OVERLAY.
    ///
    /// Draws a subtle translucent dark quad over every cell that
    /// <see cref="RoofDesignation"/> has marked as roofed, giving the RimWorld
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
        [SerializeField] private Color shadeTint = new Color(0.06f, 0.07f, 0.12f, 0.32f); // soft indoor shade
        [SerializeField] private int shadeSortingOrder = 24;   // above pawns/structures, below night overlay(25)

        // ---- runtime state ---------------------------------------------------
        public static RoofOverlayRenderer Instance { get; private set; }

        // Pooled shade quads, one per roofed cell.  Reused across rebuilds — never
        //  re-allocated per frame (GC-churn firewall).
        private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>(128);
        private GameObject quadRoot;

        // Last roof Version we rebuilt for.  A cheap per-frame compare; a full rebuild
        //  runs ONLY when this differs from RoofDesignation.Version.
        private int lastVersion = -1;

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
                if (lastVersion != -1) { DeactivateAll(); lastVersion = -1; }
                return;
            }

            // Rebuild ONLY when the roofed set actually changed (cheap Version compare).
            if (roof.Version != lastVersion)
            {
                Rebuild(roof);
                lastVersion = roof.Version;
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
                sr.color = shadeTint;
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
                sr.color = shadeTint;
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
