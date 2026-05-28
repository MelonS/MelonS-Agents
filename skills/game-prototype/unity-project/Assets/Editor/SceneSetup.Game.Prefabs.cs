using UnityEngine;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10c - SceneSetup.cs GenerateGame 의 prefab 생성 블록 extract.
    //   Tree / Wall / Floor / Door / Stove / ResearchBench prefab.
    //   원본 SceneSetup.cs L195-296 (~100 LOC).
    public static partial class SceneSetup
    {
        public struct BuildPrefabSet
        {
            public GameObject treePrefab;
            public GameObject wallPrefab;
            public GameObject floorPrefab;
            public GameObject doorPrefab;
            public GameObject stovePrefab;
            public GameObject benchPrefab;
            public GameObject bedPrefab;
            public Sprite wallSprite, floorSprite, doorSprite, stoveSprite, treeSprite, bedSprite;
            public Sprite bedFineSprite;  // #198 D4-1 - Fine quality 전용 sprite (royal-blue/gold)
        }

        private static BuildPrefabSet GenerateBuildPrefabs()
        {
            BuildPrefabSet ps = new BuildPrefabSet();

            // Trees (Day 3+12): build a Tree prefab asset once.  RegrowthScheduler
            //  uses this prefab too — unified path for initial + regen.
            ps.treeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/tree.png");
            if (ps.treeSprite == null)
            {
                string p = "Assets/Sprites/tree.png";
                TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spritePixelsPerUnit = 16;
                    ti.SaveAndReimport();
                    ps.treeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                }
            }
            GameObject treeTemplate = new GameObject("Tree");
            SpriteRenderer templateSr = treeTemplate.AddComponent<SpriteRenderer>();
            templateSr.sprite = ps.treeSprite;
            templateSr.sortingOrder = 5;
            BoxCollider2D templateCol = treeTemplate.AddComponent<BoxCollider2D>();
            templateCol.size = new Vector2(1.5f, 1.5f);
            treeTemplate.AddComponent<TreeEntity>();
            ps.treePrefab = PrefabUtility.SaveAsPrefabAsset(treeTemplate, TreePrefabPath);
            Object.DestroyImmediate(treeTemplate);
            Debug.Log($"[SceneSetup] Tree prefab -> {TreePrefabPath}");

            // Day 17-18: Build mode - Wall + Floor + Door prefabs.
            ps.wallSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wall_wood.png");
            ps.floorSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/floor_wood.png");
            ps.doorSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/door_wood.png");

            GameObject wallTemplate = new GameObject("Wall");
            var wsr = wallTemplate.AddComponent<SpriteRenderer>();
            wsr.sprite = ps.wallSprite; wsr.sortingOrder = 7;
            var wbox = wallTemplate.AddComponent<BoxCollider2D>(); wbox.size = Vector2.one;
            wallTemplate.AddComponent<WallEntity>();
            ps.wallPrefab = PrefabUtility.SaveAsPrefabAsset(wallTemplate, "Assets/Prefabs/Wall.prefab");
            Object.DestroyImmediate(wallTemplate);

            GameObject floorTemplate = new GameObject("Floor");
            var fsr = floorTemplate.AddComponent<SpriteRenderer>();
            fsr.sprite = ps.floorSprite; fsr.sortingOrder = 1;
            floorTemplate.AddComponent<FloorEntity>();
            ps.floorPrefab = PrefabUtility.SaveAsPrefabAsset(floorTemplate, "Assets/Prefabs/Floor.prefab");
            Object.DestroyImmediate(floorTemplate);

            GameObject doorTemplate = new GameObject("Door");
            var dsr = doorTemplate.AddComponent<SpriteRenderer>();
            dsr.sprite = ps.doorSprite; dsr.sortingOrder = 6;
            var dbox = doorTemplate.AddComponent<BoxCollider2D>(); dbox.size = Vector2.one; dbox.isTrigger = true;
            doorTemplate.AddComponent<DoorEntity>();
            ps.doorPrefab = PrefabUtility.SaveAsPrefabAsset(doorTemplate, "Assets/Prefabs/Door.prefab");
            Object.DestroyImmediate(doorTemplate);

            // Day 25: Stove prefab
            ps.stoveSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/stove.png");
            GameObject stoveTemplate = new GameObject("Stove");
            var ssr = stoveTemplate.AddComponent<SpriteRenderer>();
            ssr.sprite = ps.stoveSprite; ssr.sortingOrder = 5;
            var sbox = stoveTemplate.AddComponent<BoxCollider2D>(); sbox.size = Vector2.one;
            stoveTemplate.AddComponent<StoveEntity>();
            ps.stovePrefab = PrefabUtility.SaveAsPrefabAsset(stoveTemplate, "Assets/Prefabs/Stove.prefab");
            Object.DestroyImmediate(stoveTemplate);

            // Day 52: Research bench prefab + ResearchManager singleton
            Sprite benchSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/research_bench.png");
            GameObject benchTemplate = new GameObject("ResearchBench");
            var rbsr = benchTemplate.AddComponent<SpriteRenderer>();
            rbsr.sprite = benchSprite; rbsr.sortingOrder = 5;
            var rbcol = benchTemplate.AddComponent<BoxCollider2D>(); rbcol.size = Vector2.one;
            benchTemplate.AddComponent<ResearchBench>();
            ps.benchPrefab = PrefabUtility.SaveAsPrefabAsset(benchTemplate, "Assets/Prefabs/ResearchBench.prefab");
            Object.DestroyImmediate(benchTemplate);

            // 운영자 피드백 #107: 침대 prefab
            ps.bedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/bed_wood.png");
            // #198 D4-1: Fine quality 전용 sprite (royal-blue/gold).  BedEntity.SetQuality 가 swap.
            ps.bedFineSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/bed_fine.png");
            GameObject bedTemplate = new GameObject("Bed");
            var bedsr = bedTemplate.AddComponent<SpriteRenderer>();
            bedsr.sprite = ps.bedSprite; bedsr.sortingOrder = 4;
            var bedcol = bedTemplate.AddComponent<BoxCollider2D>();
            bedcol.size = new Vector2(0.95f, 0.95f);
            bedcol.isTrigger = true;  // pawn 통과 가능 (위에 눕기)
            var bedEntity = bedTemplate.AddComponent<BedEntity>();
            // #198 D4-1: 두 quality sprite ref 를 prefab 에 baked (완성 path 에서 SetQuality 가 swap).
            bedEntity.SetSpriteRefs(ps.bedSprite, ps.bedFineSprite);
            ps.bedPrefab = PrefabUtility.SaveAsPrefabAsset(bedTemplate, "Assets/Prefabs/Bed.prefab");
            Object.DestroyImmediate(bedTemplate);

            return ps;
        }
    }
}
