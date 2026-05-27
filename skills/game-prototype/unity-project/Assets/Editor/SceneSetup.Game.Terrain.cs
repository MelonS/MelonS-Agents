using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R8: GenerateGame() 의 tilemap + procedural terrain 블록 추출.
    public static partial class SceneSetup
    {
        // R8: layout 데이터 묶음 (lake/rock/dirt 위치 — 후속 entity/flower 배치 시 충돌 회피 용)
        public class TerrainLayout
        {
            // 운영자 피드백 #108 - 40x40 → 60x60 (림월드 small-medium 수준)
            public const int MAP_HALF = 30;
            public Tilemap tilemap;
            public Tile grassTile, dirtTile, waterTile, rockTile;
            // 호수 4개 (이전 2개) + 위치 비례 확장
            public Vector2[] lakeCenters = {
                new Vector2(15f, 18f), new Vector2(-18f, -12f),
                new Vector2(-22f, 22f), new Vector2(20f, -22f),
            };
            public float[] lakeRadii     = { 4.0f, 2.8f, 3.5f, 3.0f };
            // 바위 cluster 5개 (이전 3개)
            public Vector2[] rockClusterCenters = {
                new Vector2(-22f, 19f), new Vector2(24f, -21f), new Vector2(-5f, -24f),
                new Vector2(8f, 22f), new Vector2(22f, 6f),
            };
            public float rockRadius = 3.2f;
            // 흙 패치 9개 (이전 6개)
            public Vector2[] dirtCenters = {
                new Vector2(-5f, 3f), new Vector2(6f, -9f), new Vector2(-15f, 7f),
                new Vector2(12f, 6f), new Vector2(-10f, -18f), new Vector2(21f, 12f),
                new Vector2(-18f, 1f), new Vector2(3f, -18f), new Vector2(17f, 16f),
            };
            public float dirtRadius = 2.0f;
        }

        /// <summary>R8: Grid + Tilemap + 4 tile asset + procedural 배치 + TilemapStaticRefInit</summary>
        private static TerrainLayout SetupTilemap()
        {
            var layout = new TerrainLayout();

            // Grid + Ground Tilemap
            GameObject gridGo = new GameObject("Grid");
            Grid grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1, 1, 0);

            GameObject tmGo = new GameObject("Ground");
            tmGo.transform.SetParent(gridGo.transform);
            layout.tilemap = tmGo.AddComponent<Tilemap>();
            TilemapRenderer tmr = tmGo.AddComponent<TilemapRenderer>();
            tmr.sortingOrder = 0;

            // 4 tile asset (Day 39+40)
            Directory.CreateDirectory("Assets/Tiles");
            layout.grassTile = LoadOrCreateTile("Assets/Sprites/tile_grass.png", "Assets/Tiles/Grass.asset");
            layout.dirtTile  = LoadOrCreateTile("Assets/Sprites/tile_dirt.png",  "Assets/Tiles/Dirt.asset");
            layout.waterTile = LoadOrCreateTile("Assets/Sprites/tile_water.png", "Assets/Tiles/Water.asset");
            layout.rockTile  = LoadOrCreateTile("Assets/Sprites/tile_rock.png",  "Assets/Tiles/Rock.asset");

            // Procedural 결정론적 (seed=12345) + #109 cell 당 random rotation/flip 으로
            //  같은 타일이 반복돼도 시각 변화 (operator: "타일이미지가 너무 구림" + "autotile" 피드백)
            System.Random rng = new System.Random(12345);
            System.Random rotRng = new System.Random(67890);
            int half = TerrainLayout.MAP_HALF;
            for (int x = -half; x < half; x++)
            {
                for (int y = -half; y < half; y++)
                {
                    Tile chosen = layout.grassTile;
                    Vector2 p = new Vector2(x, y);
                    bool isRock = false;
                    foreach (var rc in layout.rockClusterCenters)
                    {
                        if ((p - rc).magnitude < layout.rockRadius + (float)(rng.NextDouble()-0.5)*1.2f)
                        { isRock = true; break; }
                    }
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (isRock) { layout.tilemap.SetTile(cell, layout.rockTile); ApplyRandomTileTransform(layout.tilemap, cell, rotRng); continue; }
                    bool isLake = false;
                    for (int li = 0; li < layout.lakeCenters.Length; li++)
                    {
                        if ((p - layout.lakeCenters[li]).magnitude < layout.lakeRadii[li] + (float)(rng.NextDouble()-0.5)*0.7f)
                        { isLake = true; break; }
                    }
                    if (isLake) { layout.tilemap.SetTile(cell, layout.waterTile); ApplyRandomTileTransform(layout.tilemap, cell, rotRng); continue; }
                    foreach (var dc in layout.dirtCenters)
                    {
                        if ((p - dc).magnitude < layout.dirtRadius + (float)(rng.NextDouble()-0.5)*0.5f)
                        { chosen = layout.dirtTile; break; }
                    }
                    layout.tilemap.SetTile(cell, chosen);
                    ApplyRandomTileTransform(layout.tilemap, cell, rotRng);
                }
            }

            // Step 81: runtime obstacle ref wire
            GameObject staticRefGo = new GameObject("TilemapStaticRefInit");
            TilemapStaticRefInit staticRef = staticRefGo.AddComponent<TilemapStaticRefInit>();
            staticRef.SetRefs(layout.tilemap, layout.waterTile, layout.rockTile);

            return layout;
        }

        // #109: 같은 타일 반복돼도 시각 변화 - 4 가지 rotation/flip 중 1 랜덤 선택.
        //  TileFlags.LockTransform 으로 SetTransformMatrix 가 영구 유지.
        private static void ApplyRandomTileTransform(Tilemap tm, Vector3Int cell, System.Random r)
        {
            tm.SetTileFlags(cell, TileFlags.None);  // LockColor 해제 같은 효과
            int variant = r.Next(0, 4);  // 0=identity, 1=90, 2=180, 3=270 deg
            float angle = variant * 90f;
            Matrix4x4 m = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, angle), Vector3.one);
            tm.SetTransformMatrix(cell, m);
        }

        /// <summary>R8 + #108: 54 꽃 procedural 배치 (60x60 맵 비례) - lake/rock/dirt/spawn 회피.</summary>
        private static void SetupFlowerDecor(TerrainLayout layout)
        {
            Sprite flowerSpr = LoadOrSetupSprite("Assets/Sprites/decor_flower.png");
            if (flowerSpr == null) return;
            int half = TerrainLayout.MAP_HALF;
            System.Random fr = new System.Random(98765);
            int placed = 0; int attempts = 0;
            while (placed < 54 && attempts < 1200)
            {
                attempts++;
                int fx = fr.Next(-(half-1), half);
                int fy = fr.Next(-(half-1), half);
                Vector2 fp = new Vector2(fx, fy);
                bool skip = false;
                for (int li = 0; li < layout.lakeCenters.Length; li++)
                    if ((fp - layout.lakeCenters[li]).magnitude < layout.lakeRadii[li] + 1.5f) { skip = true; break; }
                if (skip) continue;
                foreach (var rc in layout.rockClusterCenters)
                    if ((fp - rc).magnitude < layout.rockRadius + 0.5f) { skip = true; break; }
                if (skip) continue;
                foreach (var dc in layout.dirtCenters)
                    if ((fp - dc).magnitude < layout.dirtRadius) { skip = true; break; }
                if (skip) continue;
                if (Mathf.Abs(fx) < 4 && Mathf.Abs(fy) < 2) continue;  // pawn spawn 회피
                GameObject fgo = new GameObject($"Flower_{placed}");
                fgo.transform.position = new Vector3(fx + 0.5f, fy + 0.5f, 0);
                SpriteRenderer flowerSr = fgo.AddComponent<SpriteRenderer>();
                flowerSr.sprite = flowerSpr;
                flowerSr.sortingOrder = 2;
                placed++;
            }
            Debug.Log($"[SceneSetup] 꽃 {placed}개 배치");
        }
    }
}
