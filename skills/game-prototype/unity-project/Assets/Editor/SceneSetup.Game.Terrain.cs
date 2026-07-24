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
            // 운영자 피드백 #108 - 40x40 → 60x60.  #235 "기본맵부터 상당히 큼" → 90x90.
            //  PathGrid MIN/MAX, CameraController bounds, ScatterVarietyDriver.MapHalf,
            //  AStar nodeCap 모두 이 값에 동기화돼야 함 (각 파일 #235 주석 참조).
            public const int MAP_HALF = 45;
            public Tilemap tilemap;
            public Tile grassTile, dirtTile, waterTile, rockTile;
            // 호수 4개 (이전 2개) + 위치 비례 확장
            public Vector2[] lakeCenters = {
                new Vector2(15f, 18f), new Vector2(-18f, -12f),
                new Vector2(-22f, 22f), new Vector2(20f, -22f),
            };
            public float[] lakeRadii     = { 4.0f, 2.8f, 3.5f, 3.0f };
            // 바위 cluster — #43 운영자 "흙바닥/돌바닥은?": 5→8개, 맵 전역으로 확장 + 반경
            //  3.2→3.8 로 돌바닥이 눈에 띄게.  (스폰 중앙 회피 — 원점 ±10 안 없음)
            public Vector2[] rockClusterCenters = {
                new Vector2(-22f, 19f), new Vector2(24f, -21f), new Vector2(-5f, -24f),
                new Vector2(8f, 22f), new Vector2(22f, 6f),
                new Vector2(-34f, -20f), new Vector2(38f, 28f), new Vector2(-40f, 5f),
            };
            public float rockRadius = 3.8f;
            // 흙 패치 — #43 9→18개, 맵 전역으로 확장 + 반경 2.0→3.4 로 흙바닥이 넓게 드러나게.
            public Vector2[] dirtCenters = {
                new Vector2(-5f, 3f), new Vector2(6f, -9f), new Vector2(-15f, 7f),
                new Vector2(12f, 6f), new Vector2(-10f, -18f), new Vector2(21f, 12f),
                new Vector2(-18f, 1f), new Vector2(3f, -18f), new Vector2(17f, 16f),
                new Vector2(-30f, -28f), new Vector2(32f, -30f), new Vector2(-35f, 25f),
                new Vector2(30f, 30f), new Vector2(-28f, 12f), new Vector2(36f, -8f),
                new Vector2(-8f, 33f), new Vector2(10f, -34f), new Vector2(-38f, -8f),
            };
            public float dirtRadius = 3.4f;
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
            // 아트 v3 (2026-07-24 운영자 "림월드식·셀당 64"): 32px → 64px 페인털리
            //  세대 교체.  FLUX 심리스(오프셋+페더 패치 보정) + v3.1 지형 톤 클램프
            //  (S≤0.45, V×0.90).  잔디 4변형 = 단일 마스터의 wrap-roll 파생이라
            //  톤 동일 보장.  변형 수·슬롯 구조는 v2 와 동일 (rng 체인 무접촉).
            // 아트 B2 (2026-07-24 운영자 "Tiny Swords 이걸로 해봐"): 팩 지형 우선.
            //  TS 타일은 자체 완결 디자인이라 슬라이스·플립 불필요 (tsTerrain=true 면
            //  단일 타일 + 변형/플립 OFF — rng 소비는 유지해 체인 보존).
            //  팩 부재 시(신규 클론 등) 절차 원단 16조각으로 폴백 (스웨터 방지 계보).
            bool tsTerrain = System.IO.File.Exists("Assets/Sprites/ts_tile_grass.png");
            Tile[] grassSlices = null;
            if (tsTerrain)
            {
                layout.grassTile = LoadOrCreateTile("Assets/Sprites/ts_tile_grass.png", "Assets/Tiles/Grass.asset", 64f);
                layout.dirtTile  = LoadOrCreateTile("Assets/Sprites/ts_tile_sand.png",  "Assets/Tiles/Dirt.asset", 64f);
                layout.waterTile = LoadOrCreateTile("Assets/Sprites/ts_tile_water.png", "Assets/Tiles/Water.asset", 64f);
                layout.rockTile  = LoadOrCreateTile("Assets/Sprites/tile64_rock_a.png", "Assets/Tiles/Rock.asset", 64f);
            }
            else
            {
                grassSlices = new Tile[16];
                for (int gy = 0; gy < 4; gy++)
                    for (int gx = 0; gx < 4; gx++)
                        grassSlices[gy * 4 + gx] = LoadOrCreateTile(
                            $"Assets/Sprites/tile64_grass_p{gy}{gx}.png",
                            $"Assets/Tiles/GrassP{gy}{gx}.asset", 64f);
                layout.grassTile = grassSlices[0];   // 'grass 선택됨' 센티널 (아래 == 비교용)
                layout.dirtTile  = LoadOrCreateTile("Assets/Sprites/tile64_dirt_a.png",  "Assets/Tiles/Dirt.asset", 64f);
                layout.waterTile = LoadOrCreateTile("Assets/Sprites/tile64_water_a.png", "Assets/Tiles/Water.asset", 64f);
                layout.rockTile  = LoadOrCreateTile("Assets/Sprites/tile64_rock_a.png",  "Assets/Tiles/Rock.asset", 64f);
            }
            Tile dirtTileB = tsTerrain ? null
                : LoadOrCreateTile("Assets/Sprites/tile64_dirt_b.png", "Assets/Tiles/DirtB.asset", 64f);

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
                    // 셀 해시 변형 선택 — 추가 rng 호출 없이 결정론 (12345 체인 보존).
                    //  ⚠ rock 은 단일 변형만: PathGrid/PawnMovement 가 't == _rock' 레퍼런스
                    //  비교로 통행 차단을 판정하므로 rockTileB 셀은 통행 가능해져 버린다
                    //  (게임플레이 변경 금지).  rock b 변형은 PathGrid 가 타일 '집합'을
                    //  받도록 운영자 OK 후에만.  dirt 는 양쪽 다 통행이라 무해.
                    bool altVar = (((x * 73856093) ^ (y * 19349663)) & 2) == 0;
                    // 아트 B2: TS 타일은 방향성 디자인이라 flip 금지 (rng 소비는 유지).
                    bool allowFlip = !tsTerrain;
                    if (isRock) { layout.tilemap.SetTile(cell, layout.rockTile); ApplyRandomTileTransform(layout.tilemap, cell, rotRng, allowFlip); continue; }
                    bool isLake = false;
                    for (int li = 0; li < layout.lakeCenters.Length; li++)
                    {
                        if ((p - layout.lakeCenters[li]).magnitude < layout.lakeRadii[li] + (float)(rng.NextDouble()-0.5)*0.7f)
                        { isLake = true; break; }
                    }
                    if (isLake) { layout.tilemap.SetTile(cell, layout.waterTile); ApplyRandomTileTransform(layout.tilemap, cell, rotRng, allowFlip); continue; }
                    foreach (var dc in layout.dirtCenters)
                    {
                        if ((p - dc).magnitude < layout.dirtRadius + (float)(rng.NextDouble()-0.5)*0.5f)
                        { chosen = (altVar && dirtTileB != null) ? dirtTileB : layout.dirtTile; break; }
                    }
                    // 아트 v3 원단 조각(폴백 전용): grass 좌표 모듈로 4×4 조각 선택.
                    //  ⚠ gv 호출은 v2 가중변형의 잔재지만 체인 보존 위해 ts 모드에서도
                    //  그대로 1회 소비 (제거 시 이후 모든 셀의 rng 가 밀린다).
                    bool isGrass = (chosen == layout.grassTile);
                    if (isGrass)
                    {
                        double gv = rng.NextDouble();   // 체인 보존용 소비 (값 미사용)
                        if (grassSlices != null)
                        {
                            int sx = ((x % 4) + 4) % 4;
                            int sy = ((y % 4) + 4) % 4;
                            chosen = grassSlices[sy * 4 + sx];
                        }
                    }
                    layout.tilemap.SetTile(cell, chosen);
                    ApplyRandomTileTransform(layout.tilemap, cell, rotRng,
                        allowFlip && !(isGrass && grassSlices != null));
                }
            }

            // Step 81: runtime obstacle ref wire
            GameObject staticRefGo = new GameObject("TilemapStaticRefInit");
            TilemapStaticRefInit staticRef = staticRefGo.AddComponent<TilemapStaticRefInit>();
            staticRef.SetRefs(layout.tilemap, layout.waterTile, layout.rockTile);

            return layout;
        }

        // #109: 같은 타일 반복돼도 시각 변화.
        // TOP-1 (visual-polish-backlog 2026-06-11): 90° 회전 제거 — 타일 내부 음영
        //  방향이 칸마다 뒤집혀 "QR코드 노이즈" 인상의 절반이었다.  flip-X 만 적용
        //  (음영 상하 방향 보존).  ⚠ r.Next 호출 횟수는 그대로 1회/셀 — rotRng(67890)
        //  체인 결정론 보존 (호출 수가 바뀌면 이후 모든 셀의 변형이 바뀐다).
        private static void ApplyRandomTileTransform(Tilemap tm, Vector3Int cell, System.Random r,
                                                     bool apply = true)
        {
            tm.SetTileFlags(cell, TileFlags.None);  // LockColor 해제 같은 효과
            // 아트 v3 TA 지시(2026-07-24): flip-Y 추가 — 무광 저기복 페인털리라
            //  광원 방향 파괴 없음, 단일 참조 타일(물/바위)의 반복 완화.
            //  r.Next 호출 횟수 1회/셀 유지 (rotRng 67890 체인 결정론 보존),
            //  4값을 none/X/Y/XY 로 전부 소비.
            //  apply=false (잔디 원단 조각): flip 이 인접 조각 연속성을 깨므로
            //  identity 강제 — 단 r.Next 는 그대로 소비해 체인 보존.
            int variant = r.Next(0, 4);
            float sx = (apply && (variant & 1) == 1) ? -1f : 1f;
            float sy = (apply && (variant & 2) == 2) ? -1f : 1f;
            Matrix4x4 m = (sx < 0f || sy < 0f)
                ? Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(sx, sy, 1f))
                : Matrix4x4.identity;
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
