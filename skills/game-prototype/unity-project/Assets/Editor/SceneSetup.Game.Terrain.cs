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
            /// <summary>지형 전환 오버레이 (2026-07-29).  모래를 엣지 타일로 덧그린다.
            ///  베이스에는 잔디가 깔려 있어야 프린지가 잔디로 비친다.</summary>
            public Tilemap overlay;
            /// <summary>모래 엣지 세트 [row, col] — 열:좌/중/우/단독, 행:상/중/하/단독.</summary>
            public Tile[,] sandEdge;
            /// <summary>암반 바닥 엣지 세트 — 광맥 아래에 깔아 '암반 지대'로 보이게 한다.
            ///  오버레이 전용이므로 통행 판정(베이스 타일맵)에는 영향이 없다.</summary>
            public Tile[,] rockEdge;
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

            // 지형 전환 오버레이 (2026-07-29) — 잔디↔모래 경계의 직각 계단 해소(G-3).
            //  팩의 4×4 엣지 타일은 가장자리가 투명하므로 **아래에 잔디가 깔려 있어야**
            //  너덜너덜한 경계가 된다.  그래서 베이스(Ground)에는 잔디를 깔고, 모래는
            //  이 오버레이에 이웃 마스크로 고른 엣지 타일로 그린다.
            //  게임플레이 영향 0 — PathGrid 는 베이스의 water/rock 참조만 보고(흙은
            //  잔디와 똑같이 통행 가능), TilemapStaticRefInit 에도 베이스만 넘긴다.
            GameObject ovGo = new GameObject("GroundOverlay");
            ovGo.transform.SetParent(gridGo.transform);
            layout.overlay = ovGo.AddComponent<Tilemap>();
            TilemapRenderer ovr = ovGo.AddComponent<TilemapRenderer>();
            ovr.sortingOrder = 1;   // 베이스(0) 위, 엔티티/그림자보다 아래

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
                // 모래 엣지 세트 (2026-07-29) — 열:좌/중/우/단독, 행:상/중/하/단독.
                //  한 장이라도 없으면 통째로 포기하고 기존 단일 타일 경로를 쓴다
                //  (부분 적용은 경계가 더 이상해진다).
                var se = new Tile[4, 4];
                bool seOk = true;
                for (int r = 0; r < 4 && seOk; r++)
                    for (int c = 0; c < 4 && seOk; c++)
                    {
                        string sp = $"Assets/Sprites/ts_tile_sand_e{r}{c}.png";
                        if (!System.IO.File.Exists(sp)) { seOk = false; break; }
                        se[r, c] = LoadOrCreateTile(sp, $"Assets/Tiles/SandE{r}{c}.asset", 64f);
                        if (se[r, c] == null) seOk = false;
                    }
                layout.sandEdge = seOk ? se : null;
                // 암반 바닥 엣지 (2026-07-29) — 팩 의존 없이 절차 생성한 세트라
                //  클린 클론에서도 재현된다.
                var re = new Tile[4, 4];
                bool reOk = true;
                for (int r = 0; r < 4 && reOk; r++)
                    for (int c = 0; c < 4 && reOk; c++)
                    {
                        string rp = $"Assets/Sprites/tile64_rockedge_e{r}{c}.png";
                        if (!System.IO.File.Exists(rp)) { reOk = false; break; }
                        re[r, c] = LoadOrCreateTile(rp, $"Assets/Tiles/RockE{r}{c}.asset", 64f);
                        if (re[r, c] == null) reOk = false;
                    }
                layout.rockEdge = reOk ? re : null;
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
            // 흙 셀을 모아 두었다가 루프 뒤에 오버레이로 다시 그린다.  루프 안에서
            //  chosen 을 바꾸면 아래 isGrass 분기의 rng 소비가 달라져 **전 지형이 밀린다**.
            var dirtCells = new System.Collections.Generic.List<Vector2Int>();
            // 호숫가 모래톱용 (2026-07-29) — 물 셀을 모아 둔다.  베이스는 건드리지 않는다.
            var waterCells = new System.Collections.Generic.List<Vector2Int>();
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
                    if (isLake) { layout.tilemap.SetTile(cell, layout.waterTile); ApplyRandomTileTransform(layout.tilemap, cell, rotRng, allowFlip); waterCells.Add(new Vector2Int(x, y)); continue; }
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
                    if (!isGrass && (chosen == layout.dirtTile || chosen == dirtTileB))
                        dirtCells.Add(new Vector2Int(x, y));
                }
            }

            // ── 지형 전환 오버레이 패스 (2026-07-29) ────────────────────────
            //  베이스의 흙 셀을 **잔디로 되돌리고**, 모래는 오버레이에 이웃 마스크로
            //  고른 엣지 타일로 그린다.  엣지 타일의 투명 프린지 아래로 잔디가 비쳐
            //  직각 계단이 너덜너덜한 경계가 된다 (G-3).
            //  마스크 규약: 열 = (좌,우 이웃), 행 = (상,하 이웃).
            //    열 0=왼쪽잘림(오른쪽만 이웃) 1=가운데 2=오른쪽잘림 3=좌우 없음
            //    행 0=위잘림(아래만 이웃)   1=가운데 2=아래잘림   3=상하 없음
            if (layout.sandEdge != null && dirtCells.Count > 0)
            {
                var dirtSet = new System.Collections.Generic.HashSet<Vector2Int>(dirtCells);
                foreach (var dc in dirtCells)
                {
                    var c3 = new Vector3Int(dc.x, dc.y, 0);
                    layout.tilemap.SetTile(c3, layout.grassTile);   // 베이스 = 잔디
                    bool nl = dirtSet.Contains(new Vector2Int(dc.x - 1, dc.y));
                    bool nr = dirtSet.Contains(new Vector2Int(dc.x + 1, dc.y));
                    bool nt = dirtSet.Contains(new Vector2Int(dc.x, dc.y + 1));
                    bool nb = dirtSet.Contains(new Vector2Int(dc.x, dc.y - 1));
                    int col = (!nl && nr) ? 0 : (nl && nr) ? 1 : (nl && !nr) ? 2 : 3;
                    int row = (!nt && nb) ? 0 : (nt && nb) ? 1 : (nt && !nb) ? 2 : 3;
                    layout.overlay.SetTile(c3, layout.sandEdge[row, col]);
                }
                Debug.Log($"[Terrain] 전환 오버레이: 모래 {dirtCells.Count}칸 엣지 적용");
            }

            // ── 호숫가 모래톱 (2026-07-29) ─────────────────────────────────
            //  라이브 WebGL 캡처에서 호수가 "텍스처 없는 파란 직사각형"으로 읽혔다.
            //  잔디·모래·암반은 오늘 전부 너덜 경계를 얻었는데 물만 각진 계단이라
            //  혼자 미완성으로 보인 것 — 개별 퀄리티가 아니라 **경계 규약의 불일치**다.
            //
            //  물 셀에 접한 **육지** 셀에 모래 엣지를 깔아 1칸 폭 모래톱 링을 만든다.
            //  링 셀의 이웃 판정은 링 자신을 기준으로 하므로, 잔디 쪽과 물 쪽 양변이
            //  모두 "이웃 없음"으로 잡혀 양쪽이 너덜해진다 — 모래가 잔디로 스미고
            //  물로도 잠기는 실제 물가의 모양이다.
            //
            //  ⚠ 베이스 타일맵은 손대지 않는다.  PathGrid 가 타일 레퍼런스 비교로
            //  통행을 판정하므로(t == _water), 물/육지 판정은 그대로 유지된다.
            //  오버레이는 순수 시각 레이어라 통행에 영향이 없다.
            //  ⚠ rng 를 쓰지 않는다 — 결정론 체인 보존.
            if (layout.sandEdge != null && waterCells.Count > 0)
            {
                var waterSet = new System.Collections.Generic.HashSet<Vector2Int>(waterCells);
                var shoreSet = new System.Collections.Generic.HashSet<Vector2Int>();
                // 8-이웃으로 모은다.  4-이웃만 쓰면 호수 **모서리가 비어** 링이 끊기고
                //  액자 테두리처럼 보인다 (1차 시도의 실패).
                foreach (var w in waterCells)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = w.x + dx, ny = w.y + dy;
                        if (nx < -half || nx >= half || ny < -half || ny >= half) continue;
                        var n = new Vector2Int(nx, ny);
                        if (waterSet.Contains(n)) continue;
                        // 암반은 절벽이라 모래톱을 두르지 않는다.
                        if (layout.tilemap.GetTile(new Vector3Int(nx, ny, 0)) == layout.rockTile) continue;
                        shoreSet.Add(n);
                    }
                }
                foreach (var s in shoreSet)
                {
                    // ⚠ 이웃 판정에 **물도 포함**한다.  링 자신만으로 판정하면 물 쪽 변까지
                    //  "이웃 없음"이 되어 팩 엣지 타일의 어두운 외곽선이 물가에 그려지고,
                    //  모래톱이 해변이 아니라 **액자 테두리**로 읽힌다 (1차 시도의 실패).
                    //  물을 이웃으로 치면 물 쪽은 잘리지 않아 모래가 수면까지 맞닿고,
                    //  잔디 쪽 변만 너덜해진다 — 실제 물가의 모양이다.
                    bool nl = shoreSet.Contains(new Vector2Int(s.x - 1, s.y)) || waterSet.Contains(new Vector2Int(s.x - 1, s.y));
                    bool nr = shoreSet.Contains(new Vector2Int(s.x + 1, s.y)) || waterSet.Contains(new Vector2Int(s.x + 1, s.y));
                    bool nt = shoreSet.Contains(new Vector2Int(s.x, s.y + 1)) || waterSet.Contains(new Vector2Int(s.x, s.y + 1));
                    bool nb = shoreSet.Contains(new Vector2Int(s.x, s.y - 1)) || waterSet.Contains(new Vector2Int(s.x, s.y - 1));
                    int col = (!nl && nr) ? 0 : (nl && nr) ? 1 : (nl && !nr) ? 2 : 3;
                    int row = (!nt && nb) ? 0 : (nt && nb) ? 1 : (nt && !nb) ? 2 : 3;
                    layout.overlay.SetTile(new Vector3Int(s.x, s.y, 0), layout.sandEdge[row, col]);
                }
                Debug.Log($"[Terrain] 호숫가 모래톱: 물 {waterCells.Count}칸 → 모래톱 {shoreSet.Count}칸");
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
