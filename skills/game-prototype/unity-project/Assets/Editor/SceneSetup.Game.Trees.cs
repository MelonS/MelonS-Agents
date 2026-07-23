using UnityEngine;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10l - SceneSetup.cs Tree 위치 deterministic spawn 블록 extract.
    //   원본 SceneSetup.cs L157-191 (35 LOC).
    public static partial class SceneSetup
    {
        private static void SpawnTrees(GameObject treePrefab, int mapHalf,
                                       Vector2[] lakeCenters, float[] lakeRadii,
                                       Vector2[] rockClusterCenters, float rockRadius)
        {
            // 운영자 2026-06-02: 나무가 소나무만 → 종별 스프라이트(Pine/Birch/Oak) 주입+배정.
            //  신규 png 는 Sprite/Single/PPU16/Point 로 import(기본 Multiple 이면 로드 null).
            AssetDatabase.Refresh();
            foreach (var tp2 in new[] { "tree_birch.png", "tree_oak.png" })
            {
                string apath = $"Assets/Sprites/{tp2}";
                if (!System.IO.File.Exists(apath)) continue;
                AssetDatabase.ImportAsset(apath, ImportAssetOptions.ForceSynchronousImport);
                var ti = AssetImporter.GetAtPath(apath) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.spritePixelsPerUnit = 16;
                    ti.filterMode = FilterMode.Point;
                    ti.SaveAndReimport();
                }
            }
            // L1 (2026-07-24): 신규 스프라이트(로브 d/e/f + 침엽 a/b) 임포트 보정 —
            //  신규 png 기본 임포트가 Multiple 이면 로드 null (기존 교훈 동일).
            foreach (var np in new[] { "flora32_tree_d.png", "flora32_tree_e.png",
                                       "flora32_tree_f.png", "flora32_conifer_a.png",
                                       "flora32_conifer_b.png" })
            {
                string apath2 = $"Assets/Sprites/{np}";
                if (!System.IO.File.Exists(apath2)) continue;
                AssetDatabase.ImportAsset(apath2, ImportAssetOptions.ForceSynchronousImport);
                var ti2 = AssetImporter.GetAtPath(apath2) as TextureImporter;
                if (ti2 != null)
                {
                    ti2.textureType = TextureImporterType.Sprite;
                    ti2.spriteImportMode = SpriteImportMode.Single;
                    ti2.spritePixelsPerUnit = 32;
                    ti2.filterMode = FilterMode.Point;
                    ti2.SaveAndReimport();
                }
            }
            // 아트 v2 A단계 (2026-06-11): 수종 3종을 32x48 캐노피 변형으로 교체.
            // L1 (2026-07-24): 5종 체계 — 실루엣이 종 정체성 (침엽 vs 로브 3형).
            MelonS.GameProto.TreeEntity.SpeciesSprites = new[] {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/flora32_conifer_a.png"),  // Pine
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/flora32_tree_d.png"),     // Birch
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/flora32_tree_e.png"),     // Oak
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/flora32_tree_f.png"),     // Maple
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/flora32_conifer_b.png"),  // Spruce
            };

            // #108: 60x60 맵 = 9x 면적.  20 → 45 그루 비례.
            //  결정론적 (seed=24680).
            var treePositionsList = new System.Collections.Generic.List<Vector2>();
            System.Random tr = new System.Random(24680);
            int tries = 0;
            while (treePositionsList.Count < 45 && tries < 900)
            {
                tries++;
                int tx = tr.Next(-(mapHalf-2), mapHalf-1);
                int ty = tr.Next(-(mapHalf-2), mapHalf-1);
                Vector2 tp = new Vector2(tx, ty);
                bool skip = false;
                // pawn spawn (-2,0)(0,0)(2,0) 근처 회피
                if (Mathf.Abs(tx) < 4 && Mathf.Abs(ty) < 2) continue;
                for (int li = 0; li < lakeCenters.Length; li++)
                    if ((tp - lakeCenters[li]).magnitude < lakeRadii[li] + 1.5f) { skip = true; break; }
                if (skip) continue;
                foreach (var rc in rockClusterCenters)
                    if ((tp - rc).magnitude < rockRadius + 0.8f) { skip = true; break; }
                if (skip) continue;
                // 다른 tree 와 최소 거리 2.0
                foreach (var ex in treePositionsList)
                    if (Vector2.Distance(ex, tp) < 2.0f) { skip = true; break; }
                if (skip) continue;
                treePositionsList.Add(tp);
            }
            foreach (var pos in treePositionsList)
            {
                GameObject t = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab);
                // L1: 종 분포 Pine 30/Birch 20/Oak 20/Maple 15/Spruce 15.
                //  위치 추첨은 위 while 에서 전부 소진된 뒤라 매핑 변경해도 위치 불변
                //  (트리당 roll 1회 소비도 동일 = 결정론 유지).
                int roll = tr.Next(100);
                TreeSpecies sp = roll < 30 ? TreeSpecies.Pine
                               : roll < 50 ? TreeSpecies.Birch
                               : roll < 70 ? TreeSpecies.Oak
                               : roll < 85 ? TreeSpecies.Maple : TreeSpecies.Spruce;
                t.name = $"Tree_{sp}_{pos.x}_{pos.y}";
                t.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0);  // tile center 정렬
                var te = t.GetComponent<TreeEntity>();
                if (te != null) te.SetSpecies(sp);
            }
        }
    }
}
