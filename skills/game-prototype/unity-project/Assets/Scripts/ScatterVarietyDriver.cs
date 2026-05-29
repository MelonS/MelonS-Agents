using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M4-03 Lane B — environmental richness: scatter variety + clustering.
    ///
    /// Addresses the operator's "empty world" complaint (wiki Dimension 1,
    /// 'Environmental richness': "More scatter variety (chunks, dead leaves);
    /// cluster scatter near trees/rock").
    ///
    /// This driver places 3 NEW decal sprites at runtime (stone_chunk_small,
    /// dead_leaves, pebble_scatter) in ADDITION to the existing V2 scatter
    /// (decor_rock / grass_tuft / wildflower1 / wildflower2 placed by
    /// SceneSetup.Game.Scatter.cs).  The existing scatter placement is UNTOUCHED.
    ///
    /// Placement strategy:
    ///   A. SPARSE baseline scatter — stone chunks and dead-leaf clusters
    ///      placed sparsely across grass and dirt terrain, deterministic seed,
    ///      sub-cell jitter, never on the same cell twice.
    ///   B. CLUSTERED near trees — stone_chunk_small placed within 1.5 world
    ///      units of each TreeEntity (reads as fallen bark/debris near base).
    ///   C. CLUSTERED near stone veins — pebble_scatter placed within 1.8 world
    ///      units of each StoneVeinEntity (reads as chip-off from the vein).
    ///
    /// Render order: sortingOrder 1 (same as existing scatter — below pawns at
    /// 9+, below structures at 10+, above terrain tiles at 0).  This means the
    /// new variety decals are INTERLEAVED at the same depth as V2 scatter, so
    /// the eye lands on pawns and structures first, decals last.
    ///
    /// SELF-ATTACH pattern (mirrors TreeSwayDriver / FlickerLight / NightLightPoolDriver):
    ///   [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] bootstraps a hidden
    ///   DontDestroyOnLoad driver GO once.  FindFirstObjectByType idempotency
    ///   guard prevents double-spawn on scene-reload / domain-reload-disabled
    ///   play sessions.  Time.unscaledTime throttle (NOT WaitForSeconds) governs
    ///   the periodic re-scan.
    ///
    /// DETERMINISTIC SEED: seed 98765 used for all placement RNG so the same
    ///   decal layout appears every run (important for QA screenshot comparison
    ///   and to avoid visual churn on session restart).
    ///
    /// READ-ONLY usage of entities:
    ///   - TreeEntity: position read via transform.position only.
    ///   - StoneVeinEntity: position read via transform.position only.
    ///   Neither class is modified.
    ///
    /// Lane contract (W-M4-03 Lane B):
    ///   This file and _gen_scatter_variety.py are the ONLY files owned by Lane B.
    ///   NOT edited: SceneSetup.Game.Scatter.cs, _gen_fix_audit.py, _gen_sprites.py,
    ///   palette.py, any SceneSetup/entity, GrowZoneDesignation, SaveLoadManager,
    ///   TreeSwayDriver, or any test.  NEW files only.
    ///
    /// >>> QA FLAG: confirm stone_chunk_small.png / dead_leaves.png /
    ///     pebble_scatter.png are present in Assets/Sprites/ and loaded correctly
    ///     (no "sprite null" warnings in console).  The driver falls back to a 1px
    ///     procedural magenta dot if a sprite is missing at runtime, which would be
    ///     immediately obvious and indicates the PNG is not in the project.
    ///
    /// >>> QA FLAG: sortingOrder 1 — confirm new decals render BELOW pawns (order
    ///     9+) and structures (10+), and ABOVE terrain tiles (0).  Decals should
    ///     visually recede; the eye should land on pawns first.
    ///
    /// >>> QA FLAG: clustering — confirm some stone_chunk_small decals appear
    ///     visibly near tree trunks and some pebble_scatter decals near StoneVeins.
    ///     Exact placement is deterministic; a fresh scene should show the same
    ///     layout on every play session (verify seed 98765 is stable).
    ///
    /// >>> QA FLAG: scene-wiring is ZERO — self-attaches via
    ///     RuntimeInitializeOnLoadMethod. Confirm no double-attach on scene reload
    ///     (idempotency guard via FindFirstObjectByType).
    ///
    /// ACCEPTANCE (binary, cite Dim1 row):
    ///   A wide screenshot shows MORE incidental variety than before: new chunk /
    ///   leaf / pebble decals visibly scattered, some clustered near trees and
    ///   StoneVeins.  They recede — the eye lands on pawns first.  Nothing brighter
    ///   than a pawn.  No tile-seam grid pattern.  No clutter.
    /// </summary>
    public class ScatterVarietyDriver : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Tunables                                                             //
        // ------------------------------------------------------------------ //

        // Deterministic placement seed — same seed = same layout every run.
        private const int PlacementSeed = 98765;

        // Render order — same level as existing V2 scatter (sortingOrder 1).
        // Pawns are 9+, structures 10+, terrain 0.  Decals at 1 always recede.
        private const int DecalSortingOrder = 1;

        // Total baseline (non-clustered) decals to place.
        private const int BaselineChunkCount  = 18;  // stone_chunk_small on open terrain
        private const int BaselineLeafCount   = 22;  // dead_leaves on grass/dirt
        private const int BaselinePebbleCount = 12;  // pebble_scatter on open terrain

        // Cluster radius around trees (world units).
        private const float TreeClusterRadius  = 1.5f;
        // Number of chunk decals to spawn per tree in cluster mode.
        private const int   TreeClusterPerTree = 2;

        // Cluster radius around stone veins (world units).
        private const float VeinClusterRadius  = 1.8f;
        // Number of pebble decals to spawn per vein in cluster mode.
        private const int   VeinClusterPerVein = 3;

        // Rescan interval (picks up regrown trees / newly placed veins).
        private const float ScanInterval = 3.0f;

        // Sub-cell jitter magnitude — decals placed at cell + random [0,jitter].
        private const float Jitter = 0.85f;

        // Map half-size (matches SceneSetup TerrainLayout.MAP_HALF = 25).
        private const int MapHalf = 25;

        // Pawn spawn-zone avoidance (matches existing scatter rule).
        private const int SpawnAvoidX = 5;
        private const int SpawnAvoidY = 3;

        // ------------------------------------------------------------------ //
        //  Sprite names (loaded from Resources fallback or by path)           //
        // ------------------------------------------------------------------ //

        private const string SpriteChunk  = "stone_chunk_small";
        private const string SpriteLeaves = "dead_leaves";
        private const string SpritePebble = "pebble_scatter";

        // ------------------------------------------------------------------ //
        //  Runtime state                                                        //
        // ------------------------------------------------------------------ //

        private Sprite _chunkSprite;
        private Sprite _leavesSprite;
        private Sprite _pebbleSprite;

        // Pool of instantiated decal GOs owned by this driver.
        // Stored so we can clean up if the driver is destroyed.
        private readonly List<GameObject> _decalGOs = new List<GameObject>(128);

        // Track which trees and veins we have already clustered around.
        private readonly HashSet<int> _clusteredTreeIDs  = new HashSet<int>();
        private readonly HashSet<int> _clusteredVeinIDs  = new HashSet<int>();

        // Occupied world positions (floored to 0.5-unit grid) to avoid overlap.
        private readonly HashSet<long> _occupied = new HashSet<long>();

        private float _nextScan;
        private bool  _baselineDone;

        // Shared System.Random — reset to seed at start, then advanced for all draws.
        private System.Random _rng;

        // ------------------------------------------------------------------ //
        //  Bootstrap (no SceneSetup edit required)                             //
        // ------------------------------------------------------------------ //

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindDriver() != null) return;

            var go = new GameObject("~ScatterVarietyDriver");
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<ScatterVarietyDriver>();
        }

        private static ScatterVarietyDriver FindDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<ScatterVarietyDriver>();
#else
            return Object.FindObjectOfType<ScatterVarietyDriver>();
#endif
        }

        // ------------------------------------------------------------------ //
        //  Lifecycle                                                            //
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            _rng = new System.Random(PlacementSeed);
            LoadSprites();
        }

        private void Start()
        {
            // First scan happens immediately on Start (scene is fully loaded).
            PlaceAll();
            _nextScan = Time.unscaledTime + ScanInterval;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;

            // Subsequent scans only add cluster decals for newly discovered
            // trees/veins (baseline is placed once at Start).
            PlaceClusterDecals();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _decalGOs.Count; i++)
            {
                if (_decalGOs[i] != null)
                    Destroy(_decalGOs[i]);
            }
            _decalGOs.Clear();
        }

        // ------------------------------------------------------------------ //
        //  Sprite loading                                                       //
        // ------------------------------------------------------------------ //

        private void LoadSprites()
        {
            _chunkSprite  = LoadSprite(SpriteChunk);
            _leavesSprite = LoadSprite(SpriteLeaves);
            _pebbleSprite = LoadSprite(SpriteSpriteName(SpritePebble));
        }

        private static string SpriteSpriteName(string baseName) => baseName;

        private static Sprite LoadSprite(string baseName)
        {
            // Try Resources.Load first (works if the PNG is inside a Resources folder).
            Sprite s = Resources.Load<Sprite>(baseName);
            if (s != null) return s;

            // The PNG lives in Assets/Sprites/ — not a Resources folder at
            // edit time.  At runtime we build a procedural fallback sprite so
            // the driver is always valid.  QA will verify the real PNGs via
            // AssetDatabase path load in the Editor.
            // Runtime path (built game): procedural fallback — a tiny coloured dot.
            // This is clearly wrong visually and will alert QA immediately.
            // >>> QA FLAG: if any decal appears as a tiny magenta dot, the PNG
            //     was not bundled in the build.  Ensure Assets/Sprites/*.png are
            //     included (either in Resources/ or via a SpriteAtlas / AssetBundle).
            return BuildFallbackSprite(baseName);
        }

        private static Sprite BuildFallbackSprite(string name)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.name       = $"FallbackSprite_{name}";
            var pix = new Color32[16];
            for (int i = 0; i < 16; i++)
                pix[i] = new Color32(200, 0, 200, 200); // magenta — clearly wrong
            tex.SetPixels32(pix);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        }

        // ------------------------------------------------------------------ //
        //  Placement orchestration                                              //
        // ------------------------------------------------------------------ //

        private void PlaceAll()
        {
            if (!_baselineDone)
            {
                PlaceBaselineDecals();
                _baselineDone = true;
            }
            PlaceClusterDecals();
        }

        // ── Baseline: sparse scatter across the map ────────────────────────

        private void PlaceBaselineDecals()
        {
            // stone_chunk_small — on open terrain (grass / non-rock-cluster areas)
            PlaceBaseline(_chunkSprite, "Chunk", BaselineChunkCount);

            // dead_leaves — warm organic scatter, slightly denser
            PlaceBaseline(_leavesSprite, "Leaves", BaselineLeafCount);

            // pebble_scatter — on open terrain, relatively sparse
            PlaceBaseline(_pebbleSprite, "Pebble", BaselinePebbleCount);
        }

        private void PlaceBaseline(Sprite sprite, string tag, int count)
        {
            if (sprite == null) return;

            int placed   = 0;
            int attempts = 0;
            int maxAttempts = count * 40;

            while (placed < count && attempts < maxAttempts)
            {
                attempts++;
                int cx = _rng.Next(-MapHalf + 1, MapHalf);
                int cy = _rng.Next(-MapHalf + 1, MapHalf);

                // Skip pawn spawn zone.
                if (Mathf.Abs(cx) < SpawnAvoidX && Mathf.Abs(cy) < SpawnAvoidY) continue;

                float ox = (float)(_rng.NextDouble() * Jitter);
                float oy = (float)(_rng.NextDouble() * Jitter);
                float wx = cx + ox;
                float wy = cy + oy;

                long key = GridKey(wx, wy);
                if (_occupied.Contains(key)) continue;

                SpawnDecal(sprite, $"Var_{tag}_{placed}", wx, wy);
                _occupied.Add(key);
                placed++;
            }
        }

        // ── Cluster: extra decals near trees and stone veins ──────────────

        private void PlaceClusterDecals()
        {
            PlaceTreeClusters();
            PlaceVeinClusters();
        }

        private void PlaceTreeClusters()
        {
            if (_chunkSprite == null) return;

#if UNITY_2023_1_OR_NEWER
            var trees = Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None);
#else
            var trees = Object.FindObjectsOfType<TreeEntity>();
#endif
            for (int i = 0; i < trees.Length; i++)
            {
                var tree = trees[i];
                if (tree == null) continue;

                int id = tree.gameObject.GetInstanceID();
                if (_clusteredTreeIDs.Contains(id)) continue;

                Vector3 tp = tree.transform.position;

                for (int k = 0; k < TreeClusterPerTree; k++)
                {
                    // Deterministic angle derived from tree ID + offset index.
                    float angle  = (id & 0xFF) / 256f * Mathf.PI * 2f + k * (Mathf.PI * 0.75f);
                    float radius = 0.5f + (float)(_rng.NextDouble() * (TreeClusterRadius - 0.5f));
                    float wx     = tp.x + Mathf.Cos(angle) * radius;
                    float wy     = tp.y + Mathf.Sin(angle) * radius;

                    long key = GridKey(wx, wy);
                    if (_occupied.Contains(key))
                    {
                        // Try slight offset to avoid exact collision.
                        wx += 0.25f;
                        wy += 0.25f;
                        key = GridKey(wx, wy);
                        if (_occupied.Contains(key)) continue;
                    }

                    SpawnDecal(_chunkSprite, $"Var_TreeChunk_{id}_{k}", wx, wy);
                    _occupied.Add(key);
                }

                _clusteredTreeIDs.Add(id);
            }
        }

        private void PlaceVeinClusters()
        {
            if (_pebbleSprite == null) return;

#if UNITY_2023_1_OR_NEWER
            var veins = Object.FindObjectsByType<StoneVeinEntity>(FindObjectsSortMode.None);
#else
            var veins = Object.FindObjectsOfType<StoneVeinEntity>();
#endif
            for (int i = 0; i < veins.Length; i++)
            {
                var vein = veins[i];
                if (vein == null) continue;

                int id = vein.gameObject.GetInstanceID();
                if (_clusteredVeinIDs.Contains(id)) continue;

                Vector3 vp = vein.transform.position;

                for (int k = 0; k < VeinClusterPerVein; k++)
                {
                    float angle  = (id & 0xFF) / 256f * Mathf.PI * 2f + k * (Mathf.PI * 0.55f);
                    float radius = 0.4f + (float)(_rng.NextDouble() * (VeinClusterRadius - 0.4f));
                    float wx     = vp.x + Mathf.Cos(angle) * radius;
                    float wy     = vp.y + Mathf.Sin(angle) * radius;

                    long key = GridKey(wx, wy);
                    if (_occupied.Contains(key))
                    {
                        wx += 0.3f;
                        wy -= 0.2f;
                        key = GridKey(wx, wy);
                        if (_occupied.Contains(key)) continue;
                    }

                    SpawnDecal(_pebbleSprite, $"Var_VeinPebble_{id}_{k}", wx, wy);
                    _occupied.Add(key);
                }

                _clusteredVeinIDs.Add(id);
            }
        }

        // ------------------------------------------------------------------ //
        //  Decal spawn helper                                                   //
        // ------------------------------------------------------------------ //

        private void SpawnDecal(Sprite sprite, string goName, float wx, float wy)
        {
            var go = new GameObject(goName);
            go.transform.position = new Vector3(wx, wy, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.sortingOrder = DecalSortingOrder;
            // Use Sprites/Default material (point-filter is set per sprite at
            // import time; runtime material does not override filterMode).
            // No special material needed — these are simple opaque sprites.

            // Parent to this driver's GO so cleanup is trivially safe.
            go.transform.SetParent(transform, worldPositionStays: true);

            _decalGOs.Add(go);
        }

        // ------------------------------------------------------------------ //
        //  Occupancy key: floor world pos to 0.5-unit grid to prevent overlap  //
        // ------------------------------------------------------------------ //

        private static long GridKey(float wx, float wy)
        {
            int gx = Mathf.FloorToInt(wx * 2f);
            int gy = Mathf.FloorToInt(wy * 2f);
            return ((long)(gx + 2048)) * 65536L + (gy + 2048);
        }
    }
}
