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
            while (treePositionsList.Count < 20 && tries < 400)
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
            foreach (var pos in treePositionsList)
            {
                GameObject t = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.treePrefab);
                t.name = $"Tree_{pos.x}_{pos.y}";
                t.transform.position = new Vector3(pos.x, pos.y, 0);
            }

            // RegrowthScheduler (Day 12)
            GameObject rsGo = new GameObject("RegrowthScheduler");
            RegrowthScheduler rs = rsGo.AddComponent<RegrowthScheduler>();
            rs.SetTreePrefab(prefabs.treePrefab);

            // Build prefabs (Day 17-18): wall/floor/door/stove/bench
            Sprite wallSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wall_wood.png");
            Sprite floorSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/floor_wood.png");
            Sprite doorSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/door_wood.png");
            Sprite stoveSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/stove.png");
            Sprite benchSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/research_bench.png");

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
            Sprite cropSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice.png");
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
