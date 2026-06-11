using UnityEngine;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R8: GenerateGame() 의 world entities + build prefabs + starter settlement 블록 추출.
    public static partial class SceneSetup
    {
        /// <summary>R8: SceneSetup 안에서 만든 모든 prefab 참조 묶음.</summary>
        public class WorldPrefabs
        {
            public GameObject treePrefab, wallPrefab, floorPrefab, doorPrefab, stovePrefab, benchPrefab;
        }

        /// <summary>R8: Wolf 2 + Deer 8 + Tree 20 + RegrowthScheduler + Build prefabs + Bench prefab + Manager singletons.</summary>
        private static WorldPrefabs SetupWorldEntities(TerrainLayout layout)
        {
            var prefabs = new WorldPrefabs();

            // Wolf 2 마리 (Day 64)
            Sprite wolfSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wolf.png");
            Vector2[] wolfPositions = { new Vector2(-17f, 17f), new Vector2(17f, -17f) };
            foreach (var wpos in wolfPositions)
            {
                GameObject wGo = new GameObject($"Wolf_{wpos.x}_{wpos.y}");
                wGo.transform.position = new Vector3(wpos.x, wpos.y, 0);
                wGo.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
                var wsr2 = wGo.AddComponent<SpriteRenderer>();
                wsr2.sprite = wolfSpr;
                wsr2.sortingOrder = 8;
                var wcol = wGo.AddComponent<BoxCollider2D>();
                wcol.size = new Vector2(1.2f, 0.8f);
                wGo.AddComponent<WolfEnemy>();
            }

            // Deer 8 마리 (Day 23+41)
            Sprite deerSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/deer.png");
            Vector2[] deerPositions = {
                new Vector2(-14f, 8f), new Vector2(13f, 9f), new Vector2(16f, -3f),
                new Vector2(-15f, -4f), new Vector2(-2f, 15f), new Vector2(6f, -12f),
                new Vector2(-10f, -2f), new Vector2(11f, 4f),
            };
            foreach (var dpos in deerPositions)
            {
                GameObject dGo = new GameObject($"Deer_{dpos.x}_{dpos.y}");
                dGo.transform.position = new Vector3(dpos.x, dpos.y, 0);
                var dsr2 = dGo.AddComponent<SpriteRenderer>();
                dsr2.sprite = deerSpr;
                dsr2.sortingOrder = 8;
                dGo.AddComponent<Rigidbody2D>();
                var dcol = dGo.AddComponent<CircleCollider2D>();
                dcol.radius = 0.4f;
                dGo.AddComponent<AnimalEntity>();
            }

            // Tree prefab + 20 그루 procedural 배치 (Day 41)
            Sprite treeSprite = LoadOrSetupSprite("Assets/Sprites/tree.png");
            GameObject treeTemplate = new GameObject("Tree");
            SpriteRenderer templateSr = treeTemplate.AddComponent<SpriteRenderer>();
            templateSr.sprite = treeSprite;
            templateSr.sortingOrder = 5;
            BoxCollider2D templateCol = treeTemplate.AddComponent<BoxCollider2D>();
            templateCol.size = new Vector2(1.5f, 1.5f);
            treeTemplate.AddComponent<TreeEntity>();
            prefabs.treePrefab = PrefabUtility.SaveAsPrefabAsset(treeTemplate, TreePrefabPath);
            Object.DestroyImmediate(treeTemplate);

            var treePositionsList = new System.Collections.Generic.List<Vector2>();
            System.Random tr = new System.Random(24680);
            int tries = 0;
            int half = TerrainLayout.MAP_HALF;
            while (treePositionsList.Count < 20 && tries < 400)  // (NOTE: SetupWorldEntities 는 R10 이후 dead — 실제 tree 스폰은 Trees.cs)
            {
                tries++;
                int tx = tr.Next(-(half-2), half-1);
                int ty = tr.Next(-(half-2), half-1);
                Vector2 tp = new Vector2(tx, ty);
                bool skip = false;
                if (Mathf.Abs(tx) < 4 && Mathf.Abs(ty) < 2) continue;  // pawn spawn 회피
                for (int li = 0; li < layout.lakeCenters.Length; li++)
                    if ((tp - layout.lakeCenters[li]).magnitude < layout.lakeRadii[li] + 1.5f) { skip = true; break; }
                if (skip) continue;
                foreach (var rc in layout.rockClusterCenters)
                    if ((tp - rc).magnitude < layout.rockRadius + 0.8f) { skip = true; break; }
                if (skip) continue;
                foreach (var ex in treePositionsList)
                    if (Vector2.Distance(ex, tp) < 2.0f) { skip = true; break; }
                if (skip) continue;
                treePositionsList.Add(tp);
            }
            // #149 - tree species 분포 (Pine 6 / Birch 5 / Oak 4 = 15 grown).
            var sppList = new System.Collections.Generic.List<TreeSpecies>();
            for (int i = 0; i < 6; i++) sppList.Add(TreeSpecies.Pine);
            for (int i = 0; i < 5; i++) sppList.Add(TreeSpecies.Birch);
            for (int i = 0; i < 4; i++) sppList.Add(TreeSpecies.Oak);
            for (int i = 0; i < treePositionsList.Count; i++)
            {
                var pos = treePositionsList[i];
                GameObject t = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.treePrefab);
                var spp = sppList[i % sppList.Count];
                string krName = spp == TreeSpecies.Pine ? "Pine" : (spp == TreeSpecies.Birch ? "Birch" : "Oak");
                t.name = $"{krName}_{pos.x}_{pos.y}";
                t.transform.position = new Vector3(pos.x, pos.y, 0);
                var te = t.GetComponent<TreeEntity>();
                if (te != null) te.SetSpecies(spp);
            }

            // #119 - 광맥 (stone vein) 12 개 procedural 배치 (레퍼런스 콜로니심처럼 cluster).
            SpawnStoneVeins(layout, treePositionsList);

            // RegrowthScheduler (Day 12)
            GameObject rsGo = new GameObject("RegrowthScheduler");
            RegrowthScheduler rs = rsGo.AddComponent<RegrowthScheduler>();
            rs.SetTreePrefab(prefabs.treePrefab);

            // Build prefabs (Day 17-18): wall/floor/door/stove/bench
            Sprite wallSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_wall_wood.png");
            Sprite floorSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_floor_wood.png");
            Sprite doorSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_door_wood.png");
            Sprite stoveSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_stove.png");
            Sprite benchSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_research_bench.png");

            prefabs.wallPrefab  = MakeStaticPrefab("Wall",  wallSprite,  7, true,  false, "Assets/Prefabs/Wall.prefab",  go => go.AddComponent<WallEntity>());
            prefabs.floorPrefab = MakeStaticPrefab("Floor", floorSprite, 1, false, false, "Assets/Prefabs/Floor.prefab", go => go.AddComponent<FloorEntity>());
            prefabs.doorPrefab  = MakeStaticPrefab("Door",  doorSprite,  6, true,  true,  "Assets/Prefabs/Door.prefab",  go => go.AddComponent<DoorEntity>());
            prefabs.stovePrefab = MakeStaticPrefab("Stove", stoveSprite, 5, true,  false, "Assets/Prefabs/Stove.prefab", go => go.AddComponent<StoveEntity>());
            prefabs.benchPrefab = MakeStaticPrefab("ResearchBench", benchSprite, 5, true, false, "Assets/Prefabs/ResearchBench.prefab", go => go.AddComponent<ResearchBench>());

            // ResearchManager singleton
            GameObject rmGo2 = new GameObject("ResearchManager");
            rmGo2.AddComponent<ResearchManager>();

            return prefabs;
        }

        /// <summary>Helper - 동일 패턴의 정적 build prefab 생성 (sprite + sortingOrder + 선택 collider).</summary>
        private static GameObject MakeStaticPrefab(string name, Sprite spr, int sortOrder,
            bool addCollider, bool colliderTrigger, string prefabPath, System.Action<GameObject> addEntityComp)
        {
            GameObject t = new GameObject(name);
            var sr = t.AddComponent<SpriteRenderer>();
            sr.sprite = spr; sr.sortingOrder = sortOrder;
            if (addCollider)
            {
                var box = t.AddComponent<BoxCollider2D>();
                box.size = Vector2.one;
                box.isTrigger = colliderTrigger;
            }
            addEntityComp(t);
            GameObject pf = PrefabUtility.SaveAsPrefabAsset(t, prefabPath);
            Object.DestroyImmediate(t);
            return pf;
        }

        /// <summary>#119 fix - GenerateGame() 진입점: tree list 없이 호출 가능한 overload.</summary>
        private static void SpawnStoneVeins(TerrainLayout layout)
        {
            SpawnStoneVeins(layout, new System.Collections.Generic.List<Vector2>());
        }

        /// <summary>#237 (x,y) 셀의 4 cardinal 이웃 중 하나라도 걷기 가능(물/바위/맵밖 아님)이면
        ///  true.  도달 가능 stand cell 이 보장됨.  실제 생성된 tilemap 타일로 검사하므로
        ///  지형 RNG 지터까지 런타임 PathGrid 와 정확히 일치.  terrain 은 entity spawn 보다
        ///  먼저 생성됨(SceneSetup.cs: SetupTilemap → SpawnStoneVeins).</summary>
        private static bool HasWalkableNeighbor(TerrainLayout layout, int x, int y)
        {
            int half = TerrainLayout.MAP_HALF;
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i], ny = y + dy[i];
                if (nx < -half || nx >= half || ny < -half || ny >= half) continue;  // 맵밖 = 막힘
                var tile = layout.tilemap.GetTile(new Vector3Int(nx, ny, 0));
                if (tile == layout.waterTile || tile == layout.rockTile) continue;   // 막힌 지형
                return true;
            }
            return false;
        }

        /// <summary>#119 - 광맥 (StoneVeinEntity) cluster 배치.  rock terrain 근처 우선.</summary>
        private static void SpawnStoneVeins(TerrainLayout layout, System.Collections.Generic.List<Vector2> treePositions)
        {
            // 아트 v2 (운영자 2026-06-12 "바위도 이상해") — 16px '검은 액자' 광맥 →
            //  32px 신작 (struct32_ 프리픽스라 ArtV2Import 가 PPU32 자동 강제).
            Sprite veinSpr = LoadOrSetupSprite("Assets/Sprites/struct32_stone_vein.png");
            if (veinSpr == null) veinSpr = LoadOrSetupSprite("Assets/Sprites/stone_vein.png");  // 폴백
            if (veinSpr == null) { Debug.LogWarning("[StoneVein] sprite null"); return; }

            int half = TerrainLayout.MAP_HALF;
            int placed = 0;
            int target = 45;  // #220 "맵에 석재 없음" 12→20.  #235 90x90 면적 ×2.25 → 20→45.
            System.Random sr = new System.Random(31415);
            var positions = new System.Collections.Generic.List<Vector2>();
            // #250 운영자 fb "광맥은 그룹을 이루는 형태로 서로 모여있어야" — 균일 산개 대신 ~7개
            //  클러스터 중심을 잡고 각 중심 주위 작은 반경에 광맥을 모아 배치(the reference sim 광맥처럼).
            var centers = new System.Collections.Generic.List<Vector2>();
            int ctries = 0;
            // #257 운영자 "여전히 너무 띄엄띄엄": 중심 7→5 (적고 큰 군집) + 군집 간 간격 9→12
            //  로 띄워, 각 군집을 더 조밀히 채운다.
            while (centers.Count < 5 && ctries < 300)
            {
                ctries++;
                int ccx = sr.Next(-(half-5), half-4);
                int ccy = sr.Next(-(half-5), half-4);
                if (Mathf.Abs(ccx) < 6 && Mathf.Abs(ccy) < 4) continue;  // spawn 넓게 회피
                var cc = new Vector2(ccx, ccy);
                bool bad = false;
                for (int li = 0; li < layout.lakeCenters.Length; li++)
                    if ((cc - layout.lakeCenters[li]).magnitude < layout.lakeRadii[li] + 3f) { bad = true; break; }
                if (bad) continue;
                foreach (var ex in centers) if (Vector2.Distance(ex, cc) < 12f) { bad = true; break; }  // 클러스터 간 간격
                if (bad) continue;
                centers.Add(cc);
            }
            if (centers.Count == 0) centers.Add(new Vector2(Mathf.RoundToInt(half * 0.5f), Mathf.RoundToInt(half * 0.5f)));
            int tries = 0;
            while (placed < target && tries < 1500)
            {
                tries++;
                var center = centers[sr.Next(0, centers.Count)];
                int sx = Mathf.RoundToInt(center.x) + sr.Next(-2, 3);   // #257 ±3→±2 더 조밀
                int sy = Mathf.RoundToInt(center.y) + sr.Next(-2, 3);
                if (Mathf.Abs(sx) > half - 1 || Mathf.Abs(sy) > half - 1) continue;
                Vector2 sp = new Vector2(sx, sy);
                // pawn spawn 회피
                if (Mathf.Abs(sx) < 4 && Mathf.Abs(sy) < 2) continue;
                // 호수 회피
                bool skip = false;
                for (int li = 0; li < layout.lakeCenters.Length; li++)
                    if ((sp - layout.lakeCenters[li]).magnitude < layout.lakeRadii[li] + 1.5f) { skip = true; break; }
                if (skip) continue;
                // 나무 회피
                foreach (var tp in treePositions)
                    if (Vector2.Distance(tp, sp) < 1.8f) { skip = true; break; }
                if (skip) continue;
                // 기존 광맥 회피 — #257 1.4→0.9 (cardinal 인접 거리 1.0 허용 → 서로 맞붙어
                //  RimWorld 광맥처럼 조밀한 덩어리 형성).  같은 셀 중복만 차단.
                //  단 HasWalkableNeighbor 로 각 광맥의 채굴 가능성은 여전히 보장.
                foreach (var ex in positions)
                    if (Vector2.Distance(ex, sp) < 0.9f) { skip = true; break; }
                if (skip) continue;
                // #237 도달 가능성 보장 — 광맥은 자기 셀을 막으므로 pawn 은 인접 cell 에 서서
                //  캔다.  4 cardinal 이웃이 모두 물/바위/맵밖이면 'no free adjacent stand cell'
                //  로 영원히 못 캐는 광맥(맵 확대 #235 로 box-in 사례 ↑ → miner 무한 give-up).
                //  실제 tilemap 타일로 검사(런타임 PathGrid 와 동일 소스, 지형 지터까지 정확).
                if (!HasWalkableNeighbor(layout, sx, sy)) continue;
                positions.Add(sp);
                placed++;
            }
            // #127 - 4종 광물 type 분포 (sandstone 가장 많고, granite/marble 희귀).
            var typeWeights = new[] { (StoneType.Sandstone, 4), (StoneType.Limestone, 3),
                                       (StoneType.Granite, 2), (StoneType.Marble, 1) };
            int totalW = 0; foreach (var (_, w) in typeWeights) totalW += w;
            System.Random tr2 = new System.Random(42);
            foreach (var pos in positions)
            {
                var vgo = new GameObject($"StoneVein_{pos.x}_{pos.y}");
                vgo.transform.position = new Vector3(pos.x, pos.y, 0);
                var vsr = vgo.AddComponent<SpriteRenderer>();
                vsr.sprite = veinSpr;
                vsr.sortingOrder = 5;
                var vcol = vgo.AddComponent<BoxCollider2D>();
                vcol.size = new Vector2(1.4f, 1.4f);
                var vein = vgo.AddComponent<StoneVeinEntity>();
                // 가중 random pick
                int pick = tr2.Next(0, totalW);
                StoneType chosen = StoneType.Sandstone;
                int acc = 0;
                foreach (var (t, w) in typeWeights)
                {
                    acc += w; if (pick < acc) { chosen = t; break; }
                }
                vein.SetType(chosen);
            }
            Debug.Log($"[StoneVein] spawned {placed} veins");
        }

        /// <summary>R8: 시작 정착지 (벽 5+바닥 6+화덕+연구대+12 crops+9 stockpile 마커).  (Day 57+67+79)</summary>
        private static void SetupStarterSettlement(WorldPrefabs prefabs, TerrainLayout layout)
        {
            // 북쪽 벽 5칸 (-7..-3, y=1)
            Vector2Int[] wallSpots = {
                new Vector2Int(-7, 1), new Vector2Int(-6, 1), new Vector2Int(-5, 1),
                new Vector2Int(-4, 1), new Vector2Int(-3, 1),
            };
            foreach (var w in wallSpots)
            {
                GameObject wGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.wallPrefab);
                wGo.name = $"StarterWall_{w.x}_{w.y}";
                wGo.transform.position = new Vector3(w.x, w.y, 0f);
            }
            // 인테리어 floor 6칸
            Vector2Int[] floorSpots = {
                new Vector2Int(-6, 0), new Vector2Int(-5, 0), new Vector2Int(-4, 0),
                new Vector2Int(-6, -1), new Vector2Int(-5, -1), new Vector2Int(-4, -1),
            };
            foreach (var f in floorSpots)
            {
                GameObject fGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.floorPrefab);
                fGo.name = $"StarterFloor_{f.x}_{f.y}";
                fGo.transform.position = new Vector3(f.x, f.y, 0f);
            }
            // 시작 화덕 + 연구대
            GameObject starterStove = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.stovePrefab);
            starterStove.name = "StarterStove";
            starterStove.transform.position = new Vector3(-4f, 0f, 0f);
            GameObject starterBench = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.benchPrefab);
            starterBench.name = "StarterResearchBench";
            starterBench.transform.position = new Vector3(-6f, 0f, 0f);

            // 12 crops (3 stage 시각 다양성)
            Sprite cropSprSeedling = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice_seedling.png");
            if (cropSprSeedling == null) Debug.LogWarning("[SetupStarterSettlement] crop_rice_seedling.png NULL — stage sprite missing");
            Sprite cropSprGrowing = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice_growing.png");
            if (cropSprGrowing == null) Debug.LogWarning("[SetupStarterSettlement] crop_rice_growing.png NULL — stage sprite missing");
            Sprite cropSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice.png");
            if (cropSprite == null) Debug.LogWarning("[SetupStarterSettlement] crop_rice.png NULL — stage sprite missing");
            var growthField = typeof(CropEntity).GetField("growth",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int cx = 3; cx <= 6; cx++)
            {
                for (int cy = -4; cy <= -2; cy++)
                {
                    GameObject cGo = new GameObject($"Crop_{cx}_{cy}");
                    cGo.transform.position = new Vector3(cx + 0.5f, cy + 0.5f, 0f);
                    var csr = cGo.AddComponent<SpriteRenderer>();
                    csr.sprite = cropSprite;
                    csr.sortingOrder = 3;
                    var ce = cGo.AddComponent<CropEntity>();
                    if (growthField != null)
                    {
                        float startGrowth = 0.4f + ((cx + cy + 100) % 5) * 0.10f;
                        growthField.SetValue(ce, startGrowth);
                    }
                    // V6 crop-stage wiring: wire the 3 stage sprite SerializeFields so
                    //  CropEntity.RefreshVisual() uses the sprite-swap path (not legacy
                    //  color-tint fallback). Same SerializedObject pattern as PawnSpriteBob
                    //  (SceneSetup.Pawn.cs) so refs persist into scene instances.
                    var ceSo = new SerializedObject(ce);
                    ceSo.FindProperty("spriteSeedling").objectReferenceValue = cropSprSeedling;
                    ceSo.FindProperty("spriteGrowing").objectReferenceValue = cropSprGrowing;
                    ceSo.FindProperty("spriteRipe").objectReferenceValue = cropSprite;
                    ceSo.ApplyModifiedPropertiesWithoutUndo();
                    layout.tilemap.SetTile(new Vector3Int(cx, cy, 0), layout.dirtTile);
                }
            }

            // 9 stockpile 마커 (3x3 visible 표지만; 실제 storage 로직은 향후)
            Sprite stockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/stockpile_marker.png");
            for (int sx = -2; sx <= 0; sx++)
            {
                for (int sy = -3; sy <= -1; sy++)
                {
                    GameObject mGo = new GameObject($"StockMark_{sx}_{sy}");
                    mGo.transform.position = new Vector3(sx + 0.5f, sy + 0.5f, 0f);
                    var msr = mGo.AddComponent<SpriteRenderer>();
                    msr.sprite = stockSprite;
                    msr.sortingOrder = 4;
                }
            }
        }
    }
}
