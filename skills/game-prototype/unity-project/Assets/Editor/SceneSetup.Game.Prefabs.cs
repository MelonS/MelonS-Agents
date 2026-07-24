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

        // Polish — free-standing furniture grounding shadow.  Same idea as the
        //  tree-base shadow: a soft ellipse (shadow_tree.png, alpha-baked) parented
        //  under the body so the object reads as resting ON the ground instead of
        //  floating.  Only for object-like furniture (stove / bench / bed); walls /
        //  floors / doors are tile-structural and need no drop shadow.  Shadow sits
        //  one sortingOrder under the body, on the same layer, nudged down so it
        //  pools at the object's base.
        private static void AttachGroundShadow(GameObject parent, SpriteRenderer bodySr,
                                               float yOffset, float scale)
        {
            Sprite shadow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/shadow_tree.png");
            if (shadow == null)
            {
                Debug.LogWarning($"[SceneSetup] shadow_tree.png NULL — {parent.name} shadow skipped");
                return;
            }
            GameObject go = new GameObject("GroundShadow");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(0f, yOffset, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = shadow;
            sr.color = new Color(1f, 1f, 1f, 0.7f);   // slightly lighter than tree-base
            sr.sortingLayerID = bodySr.sortingLayerID;
            sr.sortingOrder = bodySr.sortingOrder - 1;
        }

        private static BuildPrefabSet GenerateBuildPrefabs()
        {
            BuildPrefabSet ps = new BuildPrefabSet();

            // Trees (Day 3+12): build a Tree prefab asset once.  RegrowthScheduler
            //  uses this prefab too — unified path for initial + regen.
            // 아트 v2 A단계 (2026-06-11): 16px tree.png → 32x48 flora32_tree_a.png
            //  (PPU32 = 1x1.5 유닛 — ArtV2Import 가 임포트 설정 강제).
            ps.treeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/flora32_tree_a.png");
            if (ps.treeSprite == null)
            {
                string p = "Assets/Sprites/flora32_tree_a.png";
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
            // Polish v2 — tree grounding shadow at trunk base.  Wired into the
            //  shared Tree prefab template, so SpawnTrees (PrefabUtility.Instantiate)
            //  AND RegrowthScheduler (same prefab) both inherit it → all 20 +
            //  regrown trees get the shadow.  shadow_tree.png 20x6 @ PPU16 =
            //  1.25 x 0.375 world units, alpha baked → white. sortingOrder =
            //  body(5) − 1 = 4, under the trunk; same default sortingLayer.
            Sprite treeShadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Sprites/shadow_tree.png");
            GameObject treeShadowGo = new GameObject("TreeShadow");
            treeShadowGo.transform.SetParent(treeTemplate.transform, false);
            // 아트 v2: 나무가 1.5h(베이스 -0.75) — 그림자를 둥치 밑으로.
            treeShadowGo.transform.localPosition = new Vector3(0f, -0.70f, 0f);
            treeShadowGo.transform.localScale = Vector3.one;
            var treeShadowSr = treeShadowGo.AddComponent<SpriteRenderer>();
            treeShadowSr.sprite = treeShadowSprite;
            treeShadowSr.color = Color.white;
            treeShadowSr.sortingLayerID = templateSr.sortingLayerID;
            treeShadowSr.sortingOrder = templateSr.sortingOrder - 1;  // = 4
            if (treeShadowSprite == null)
                Debug.LogWarning("[SceneSetup] shadow_tree.png NULL — tree shadow skipped");
            ps.treePrefab = PrefabUtility.SaveAsPrefabAsset(treeTemplate, TreePrefabPath);
            Object.DestroyImmediate(treeTemplate);
            Debug.Log($"[SceneSetup] Tree prefab -> {TreePrefabPath}");

            // Day 17-18: Build mode - Wall + Floor + Door prefabs.
            ps.wallSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_wall_wood.png");
            ps.floorSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_floor_wood.png");
            ps.doorSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_door_wood.png");

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
            ps.stoveSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_stove.png");
            GameObject stoveTemplate = new GameObject("Stove");
            var ssr = stoveTemplate.AddComponent<SpriteRenderer>();
            ssr.sprite = ps.stoveSprite; ssr.sortingOrder = 5;
            var sbox = stoveTemplate.AddComponent<BoxCollider2D>(); sbox.size = Vector2.one;
            stoveTemplate.AddComponent<StoveEntity>();
            AttachGroundShadow(stoveTemplate, ssr, -0.34f, 0.85f);
            ps.stovePrefab = PrefabUtility.SaveAsPrefabAsset(stoveTemplate, "Assets/Prefabs/Stove.prefab");
            Object.DestroyImmediate(stoveTemplate);

            // Day 52: Research bench prefab + ResearchManager singleton
            Sprite benchSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_research_bench.png");
            GameObject benchTemplate = new GameObject("ResearchBench");
            var rbsr = benchTemplate.AddComponent<SpriteRenderer>();
            rbsr.sprite = benchSprite; rbsr.sortingOrder = 5;
            var rbcol = benchTemplate.AddComponent<BoxCollider2D>(); rbcol.size = Vector2.one;
            benchTemplate.AddComponent<ResearchBench>();
            // bench is 2x1 — a wider shadow spans the footprint.
            AttachGroundShadow(benchTemplate, rbsr, -0.30f, 1.15f);
            ps.benchPrefab = PrefabUtility.SaveAsPrefabAsset(benchTemplate, "Assets/Prefabs/ResearchBench.prefab");
            Object.DestroyImmediate(benchTemplate);

            // 운영자 피드백 #107: 침대 prefab
            // 아트 B2 (2026-07-24 PoC 승인): 절차 드로잉 128px 침대 우선 (PPU128 = 1×2칸),
            //  없으면 구세대 struct32 폴백.  생성기: scripts/gen-ts-furniture.py
            ps.bedSprite = ImportSpriteAt("Assets/Sprites/struct64_bed_wood.png", 128f)
                           ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_bed_wood.png");
            // #198 D4-1: Fine quality 전용 sprite.  BedEntity.SetQuality 가 swap.
            ps.bedFineSprite = ImportSpriteAt("Assets/Sprites/struct64_bed_fine.png", 128f)
                               ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_bed_fine.png");
            GameObject bedTemplate = new GameObject("Bed");
            var bedsr = bedTemplate.AddComponent<SpriteRenderer>();
            bedsr.sprite = ps.bedSprite; bedsr.sortingOrder = 4;
            var bedcol = bedTemplate.AddComponent<BoxCollider2D>();
            bedcol.size = new Vector2(0.95f, 0.95f);
            bedcol.isTrigger = true;  // pawn 통과 가능 (위에 눕기)
            var bedEntity = bedTemplate.AddComponent<BedEntity>();
            // #198 D4-1: 두 quality sprite ref 를 prefab 에 baked (완성 path 에서 SetQuality 가 swap).
            bedEntity.SetSpriteRefs(ps.bedSprite, ps.bedFineSprite);
            // bed is 1x2 (16x32) — shadow pools at the foot of the frame.
            AttachGroundShadow(bedTemplate, bedsr, -0.92f, 0.95f);
            ps.bedPrefab = PrefabUtility.SaveAsPrefabAsset(bedTemplate, "Assets/Prefabs/Bed.prefab");
            Object.DestroyImmediate(bedTemplate);

            return ps;
        }
    }
}
