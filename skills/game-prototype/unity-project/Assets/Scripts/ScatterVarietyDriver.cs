using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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
    /// W-M6-03 Lane B11 ADDITIONS (add-only, no existing behaviour changed):
    ///   D. ROCK SHAPE VARIETY — 3 new rock silhouettes (rock_variant_a/b/c)
    ///      scattered alongside stone_chunk_small.  Same deterministic seed,
    ///      same sortingOrder 1.  Silhouettes differ markedly so the eye reads
    ///      variety instead of repetition.
    ///   E. WATER-EDGE TRANSITION DECALS — water_edge_band placed 1-tile deep
    ///      along grass/water boundaries.  Reads terrain positions via
    ///      PawnMovement.GroundTilemap + PawnMovement.WaterTile (READ-ONLY
    ///      statics set by TilemapStaticRefInit; never written here).  One
    ///      band sprite per boundary cell, oriented so the gradient flows from
    ///      water (top) to grass (bottom).  If the tilemap is not yet set at
    ///      bootstrap time, edge placement is deferred to the next periodic scan.
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
    /// SPRITE LOADING (W-M4-04 Lane B fix — QA flag actioned):
    ///   PNGs live at Assets/Resources/scatter/*.png.  Resources.Load resolves
    ///   them in BOTH batchmode (Editor/AssetDatabase) AND player builds (Unity
    ///   bundles the entire Resources/ tree automatically).  The load path is
    ///   "scatter/<name>" matching the subfolder.  A magenta procedural dot is
    ///   kept ONLY as a last-resort fallback (should never be seen in a normal
    ///   build — indicates the Resources/scatter/ PNGs are missing from the project).
    ///
    /// READ-ONLY usage of entities:
    ///   - TreeEntity: position read via transform.position only.
    ///   - StoneVeinEntity: position read via transform.position only.
    ///   - PawnMovement.GroundTilemap / WaterTile: read-only statics; never written.
    ///   Neither class nor any statics are modified.
    ///
    /// Lane contract (W-M4-03/04 Lane B + W-M6-03 Lane B11):
    ///   This file and the scatter PNGs at Assets/Resources/scatter/ are the
    ///   ONLY files owned by these lanes.
    ///   NOT edited: _gen_scatter_variety.py, SceneSetup.Game.Scatter.cs,
    ///   _gen_fix_audit.py, _gen_sprites.py, palette.py (read-only import),
    ///   _gen_fence.py, _gen_barricade.py, _gen_table_chair.py, _gen_lamp.py,
    ///   any SceneSetup/entity, BuildManager, AudioBank, SaveLoadManager,
    ///   SelectionGizmoBar, BarricadeEntity, or any test.
    ///
    /// >>> QA FLAG: confirm scatter/stone_chunk_small.png / scatter/dead_leaves.png /
    ///     scatter/pebble_scatter.png are present at Assets/Resources/scatter/ and
    ///     loaded correctly (no "sprite null" warnings in console).  If any decal
    ///     appears as a tiny magenta dot the PNG is missing from Resources/scatter/.
    ///
    /// >>> QA FLAG (B11 new sprites): confirm scatter/rock_variant_a.png /
    ///     scatter/rock_variant_b.png / scatter/rock_variant_c.png /
    ///     scatter/water_edge_band.png are present at Assets/Resources/scatter/.
    ///     If any appears magenta the PNG is missing or misnamed.
    ///
    /// >>> QA FLAG: sortingOrder 1 — confirm new decals render BELOW pawns (order
    ///     9+) and structures (10+), and ABOVE terrain tiles (0).  Decals should
    ///     visually recede; the eye should land on pawns first.
    ///
    /// >>> QA FLAG (B11 rock variety): three distinct rock silhouettes (variant_a
    ///     wide slab, variant_b tall spire, variant_c fractured plate) should
    ///     be visibly different from each other and from stone_chunk_small in
    ///     a zoomed-out game view.  All clearly duller than pawn skin/cloth.
    ///
    /// >>> QA FLAG (B11 water-edge): water tiles should show an organic transition
    ///     band against adjacent grass tiles.  One band per grass-adjacent water
    ///     cell boundary.  If PawnMovement.GroundTilemap is null (headless test),
    ///     water-edge placement is gracefully skipped (no throw).
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
    /// >>> QA FLAG (SceneSetup stale entries): SceneSetup.cs ForceImportAllSprites
    ///     still lists the OLD Assets/Sprites/stone_chunk_small.png path (and the
    ///     other two).  These will emit "[SceneSetup] missing: ..." warnings during
    ///     scene-regen because the PNGs have moved to Resources/scatter/.  The
    ///     warnings are HARMLESS (ForceImport is an editor setup tool; the build
    ///     picks up Resources/ automatically), but QA should expect 3 extra missing-
    ///     file warnings in scene-regen output.  No SceneSetup edit was made per
    ///     lane contract (prefer path requiring no SceneSetup edit).
    ///
    /// >>> QA FLAG (B11 scene-wiring): new B11 sprites (rock_variant_a/b/c +
    ///     water_edge_band) live under Assets/Resources/scatter/ — they are auto-
    ///     bundled by Unity's Resources system.  No ForceImport SceneSetup entry
    ///     is required.  QA should verify their .meta files exist (auto-created on
    ///     first AssetDatabase refresh).
    ///
    /// ACCEPTANCE (binary, cite Dim1 row):
    ///   A wide screenshot shows MORE incidental variety than before: new chunk /
    ///   leaf / pebble decals visibly scattered, some clustered near trees and
    ///   StoneVeins.  They recede — the eye lands on pawns first.  Nothing brighter
    ///   than a pawn.  No tile-seam grid pattern.  No clutter.
    ///   In a fresh built game the 3 scatter decals render as their real
    ///   chunk/leaf/pebble sprites (NOT magenta dots) — QA screenshot shows no
    ///   magenta scatter.
    ///
    /// ACCEPTANCE (B11 additions, binary, cite wiki B11):
    ///   Water tiles show an edge band against grass.
    ///   Scattered rocks vary in shape (2-3 distinct rock silhouettes visible).
    ///   Everything recedes (eye still lands on pawns first), nothing brighter
    ///   than a pawn.
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

        // 운영자 fb 2026-05-31: function-first — 기능없는 장식 오브젝트 제거.
        // decor_rock/stone_chunk/rock_variant/pebble 은 보이는데 기능없고 StoneVein 과 혼동 → 0.
        // dead_leaves 도 기능없는 장식 오브젝트 → 0.
        // water_edge_band 는 지형 타일 텍스처 전환(순수 배경) → 유지(PlaceWaterEdgeDecals).
        private const int BaselineChunkCount  = 0;  // stone_chunk_small — 기능없는 장식 돌, 제거
        private const int BaselineLeafCount   = 0;  // dead_leaves — 기능없는 장식 오브젝트, 제거
        private const int BaselinePebbleCount = 0;  // pebble_scatter — 기능없는 장식, 제거

        // B11: rock shape variants — 기능없는 장식 바위, 제거.
        private const int BaselineRockACount = 0;  // rock_variant_a — 제거
        private const int BaselineRockBCount = 0;  // rock_variant_b — 제거
        private const int BaselineRockCCount = 0;  // rock_variant_c — 제거

        // 운영자 fb 2026-05-30: 장식 바위(chunk/variant)는 작은 땅 자갈처럼 보이게 축소하여
        //  minable StoneVein 과 명확히 구분.  SpawnDecal 이 rock 류 decal 에 이 scale 적용.
        //  (leaves/pebble/waterEdge 는 원래 작아서 영향 없음 — DecalScaleDefault 사용.)
        private const float RockDecalScale    = 0.45f; // 장식 돌: 작고 납작한 자갈로 축소
        private const float DecalScaleDefault = 1.0f;  // 그 외 decal 은 원래 크기

        // Cluster radius around trees (world units).
        private const float TreeClusterRadius  = 1.5f;
        // 운영자 fb 2026-05-31: function-first — 나무 주변 장식 돌 제거.
        private const int   TreeClusterPerTree = 0;  // chunk near trees 제거

        // Cluster radius around stone veins (world units).
        private const float VeinClusterRadius  = 1.8f;
        // 운영자 fb 2026-05-31: function-first — vein 주변 pebble 장식 제거.
        private const int   VeinClusterPerVein = 0;  // pebble near veins 제거

        // Rescan interval (picks up regrown trees / newly placed veins).
        private const float ScanInterval = 3.0f;

        // Sub-cell jitter magnitude — decals placed at cell + random [0,jitter].
        private const float Jitter = 0.85f;

        // Map half-size (matches SceneSetup TerrainLayout.MAP_HALF = 25).
        private const int MapHalf = 25;

        // Pawn spawn-zone avoidance (matches existing scatter rule).
        private const int SpawnAvoidX = 5;
        private const int SpawnAvoidY = 3;

        // B11: Cell scan range for water-edge detection.
        // PathGrid uses MIN=-30/MAX=29; we scan -MapHalf..MapHalf-1 to stay
        // within the terrain area used by SceneSetup (MAP_HALF=25 → ±25 cells).
        private const int WaterEdgeScanMin = -MapHalf;
        private const int WaterEdgeScanMax =  MapHalf - 1;

        // ------------------------------------------------------------------ //
        //  Resources paths (PNGs live at Assets/Resources/scatter/)           //
        //  Resources.Load path = subfolder/filename_without_extension         //
        // ------------------------------------------------------------------ //

        private const string ResourcePathChunk    = "scatter/stone_chunk_small";
        private const string ResourcePathLeaves   = "scatter/dead_leaves";
        private const string ResourcePathPebble   = "scatter/pebble_scatter";

        // B11 — new rock shape variants + water-edge band
        private const string ResourcePathRockA    = "scatter/rock_variant_a";
        private const string ResourcePathRockB    = "scatter/rock_variant_b";
        private const string ResourcePathRockC    = "scatter/rock_variant_c";
        private const string ResourcePathWaterEdge= "scatter/water_edge_band";

        // ------------------------------------------------------------------ //
        //  Runtime state                                                        //
        // ------------------------------------------------------------------ //

        private Sprite _chunkSprite;
        private Sprite _leavesSprite;
        private Sprite _pebbleSprite;

        // B11 — new sprites
        private Sprite _rockASprite;
        private Sprite _rockBSprite;
        private Sprite _rockCSprite;
        private Sprite _waterEdgeSprite;

        // Pool of instantiated decal GOs owned by this driver.
        // Stored so we can clean up if the driver is destroyed.
        private readonly List<GameObject> _decalGOs = new List<GameObject>(256);

        // Track which trees and veins we have already clustered around.
        private readonly HashSet<int> _clusteredTreeIDs  = new HashSet<int>();
        private readonly HashSet<int> _clusteredVeinIDs  = new HashSet<int>();

        // Occupied world positions (floored to 0.5-unit grid) to avoid overlap.
        private readonly HashSet<long> _occupied = new HashSet<long>();

        // B11 — track whether water-edge placement has completed.
        private bool _waterEdgeDone;

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

            // B11: retry water-edge placement if the tilemap wasn't ready at Start.
            if (!_waterEdgeDone)
                PlaceWaterEdgeDecals();
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
        //  Sprite loading — Resources.Load resolves in Editor, batchmode,     //
        //  and player builds because PNGs are under Assets/Resources/scatter/ //
        // ------------------------------------------------------------------ //

        private void LoadSprites()
        {
            _chunkSprite  = LoadSprite(ResourcePathChunk,  "stone_chunk_small");
            _leavesSprite = LoadSprite(ResourcePathLeaves, "dead_leaves");
            _pebbleSprite = LoadSprite(ResourcePathPebble, "pebble_scatter");

            // B11 — load new sprites
            _rockASprite     = LoadSprite(ResourcePathRockA,     "rock_variant_a");
            _rockBSprite     = LoadSprite(ResourcePathRockB,     "rock_variant_b");
            _rockCSprite     = LoadSprite(ResourcePathRockC,     "rock_variant_c");
            _waterEdgeSprite = LoadSprite(ResourcePathWaterEdge, "water_edge_band");
        }

        private static Sprite LoadSprite(string resourcePath, string friendlyName)
        {
            Sprite s = Resources.Load<Sprite>(resourcePath);
            if (s != null) return s;

            // Last-resort fallback — a magenta dot.
            // In a correct build this path is never reached because the PNGs
            // live at Assets/Resources/scatter/ and are auto-bundled by Unity.
            // Seeing a magenta decal in the built game means the Resources/scatter/
            // folder is missing from the project — report to QA immediately.
            Debug.LogWarning($"[ScatterVarietyDriver] Sprite not found at Resources path '{resourcePath}' " +
                             $"(friendly: {friendlyName}). Falling back to magenta dot. " +
                             "Ensure Assets/Resources/scatter/{friendlyName}.png is in the project.");
            return BuildFallbackSprite(friendlyName);
        }

        private static Sprite BuildFallbackSprite(string name)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.name       = $"FallbackSprite_{name}";
            var pix = new Color32[16];
            for (int i = 0; i < 16; i++)
                pix[i] = new Color32(200, 0, 200, 200); // magenta — clearly wrong, alerts QA
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
                PlaceBaselineRockVariants(); // B11: new rock shapes
                _baselineDone = true;
            }
            PlaceClusterDecals();
            PlaceWaterEdgeDecals(); // B11: water-edge transition bands
        }

        // -- Baseline: sparse scatter across the map ────────────────────────

        private void PlaceBaselineDecals()
        {
            // stone_chunk_small — on open terrain.  Rock-type → shrunk to pebble scale
            //  so it never reads as a minable StoneVein (운영자 fb 2026-05-30).
            PlaceBaseline(_chunkSprite, "Chunk", BaselineChunkCount, RockDecalScale);

            // dead_leaves — warm organic scatter, light accent (not a rock → default scale).
            PlaceBaseline(_leavesSprite, "Leaves", BaselineLeafCount, DecalScaleDefault);

            // pebble_scatter — small ground pebbles, already tiny → default scale.
            PlaceBaseline(_pebbleSprite, "Pebble", BaselinePebbleCount, DecalScaleDefault);
        }

        private void PlaceBaseline(Sprite sprite, string tag, int count, float scale)
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

                SpawnDecal(sprite, $"Var_{tag}_{placed}", wx, wy, scale);
                _occupied.Add(key);
                placed++;
            }
        }

        // -- B11: Baseline rock shape variants ──────────────────────────────
        // Scattered identically to stone_chunk_small but using the 3 new
        // silhouettes so the scene has visibly more rock shape variety.
        // Uses the SAME _rng state (continuing from BaselineDecals) so the
        // combined layout is deterministic and non-overlapping with existing decals.

        private void PlaceBaselineRockVariants()
        {
            // Rock-type silhouettes → shrunk to pebble scale (운영자 fb 2026-05-30).
            PlaceBaseline(_rockASprite, "RockA", BaselineRockACount, RockDecalScale);
            PlaceBaseline(_rockBSprite, "RockB", BaselineRockBCount, RockDecalScale);
            PlaceBaseline(_rockCSprite, "RockC", BaselineRockCCount, RockDecalScale);
        }

        // -- Cluster: extra decals near trees and stone veins ──────────────

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

                    SpawnDecal(_chunkSprite, $"Var_TreeChunk_{id}_{k}", wx, wy, RockDecalScale);
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

                    SpawnDecal(_pebbleSprite, $"Var_VeinPebble_{id}_{k}", wx, wy, DecalScaleDefault);
                    _occupied.Add(key);
                }

                _clusteredVeinIDs.Add(id);
            }
        }

        // ------------------------------------------------------------------ //
        //  B11 — Water-edge transition decals                                  //
        //                                                                      //
        //  Scans the tilemap (READ-ONLY via PawnMovement statics) for cells   //
        //  that are water-type AND have at least one grass/walkable neighbour. //
        //  Places water_edge_band at the water cell's position so the band     //
        //  bridges the hard tile boundary.                                     //
        //                                                                      //
        //  Graceful degradation: if PawnMovement.GroundTilemap is null (e.g.  //
        //  headless test scene), the scan is skipped and _waterEdgeDone stays  //
        //  false so the next periodic Update retries.                          //
        // ------------------------------------------------------------------ //

        private void PlaceWaterEdgeDecals()
        {
            if (_waterEdgeSprite == null) return;

            // READ-ONLY access to the tilemap statics set by TilemapStaticRefInit.
            // This driver NEVER writes these statics.
            Tilemap ground = PawnMovement.GroundTilemap;
            TileBase waterTile = PawnMovement.WaterTile;

            if (ground == null || waterTile == null)
            {
                // Tilemap not yet initialised (headless or very early bootstrap).
                // Retry on next periodic scan.
                return;
            }

            // Scan the terrain area used by SceneSetup (±MapHalf cells).
            // For each water cell, check 4 orthogonal neighbours.  If ANY
            // neighbour is NOT water (i.e. it is grass/dirt/walkable), this
            // water cell sits on the boundary — place an edge band.
            for (int cx = WaterEdgeScanMin; cx <= WaterEdgeScanMax; cx++)
            {
                for (int cy = WaterEdgeScanMin; cy <= WaterEdgeScanMax; cy++)
                {
                    var cell = new Vector3Int(cx, cy, 0);
                    TileBase t = ground.GetTile(cell);
                    if (t == null || t != waterTile) continue; // not a water cell

                    // Check 4-neighbour: is any adjacent cell non-water (grass/dirt)?
                    bool hasGrassNeighbour = false;
                    for (int d = 0; d < 4 && !hasGrassNeighbour; d++)
                    {
                        int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                        int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        var ncell = new Vector3Int(nx, ny, 0);
                        TileBase nt = ground.GetTile(ncell);
                        // null tile (map edge) is treated as non-water → also an edge
                        if (nt != waterTile)
                            hasGrassNeighbour = true;
                    }

                    if (!hasGrassNeighbour) continue; // interior water cell — skip

                    // Place the edge band at the water cell's world centre.
                    // The band sprite (16x4) is oriented horizontally so its top
                    // rows (water tones) align with the water tile and its bottom
                    // rows (grass tones) bleed onto the adjacent grass tile.
                    float wx = cx + 0.5f;
                    float wy = cy + 0.5f;

                    long key = GridKey(wx, wy);
                    if (_occupied.Contains(key)) continue; // another decal already here

                    SpawnDecal(_waterEdgeSprite, $"WaterEdge_{cx}_{cy}", wx, wy, DecalScaleDefault);
                    _occupied.Add(key);
                }
            }

            _waterEdgeDone = true;
        }

        // ------------------------------------------------------------------ //
        //  Decal spawn helper                                                   //
        // ------------------------------------------------------------------ //

        private void SpawnDecal(Sprite sprite, string goName, float wx, float wy, float scale)
        {
            var go = new GameObject(goName);
            go.transform.position = new Vector3(wx, wy, 0f);
            // 운영자 fb 2026-05-30: 장식 돌은 작은 자갈로 축소하여 minable StoneVein 과 구분.
            if (scale != 1.0f)
                go.transform.localScale = new Vector3(scale, scale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.sortingOrder = DecalSortingOrder;

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
