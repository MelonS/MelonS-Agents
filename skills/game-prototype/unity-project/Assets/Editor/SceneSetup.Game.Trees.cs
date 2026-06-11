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
            // 아트 v2 A단계 (2026-06-11): 수종 3종을 32x48 캐노피 변형으로 교체
            //  (구 tree/birch/oak 16px — 자작나무가 '흰 막대+초록 사각'으로 읽히던 것).
            MelonS.GameProto.TreeEntity.SpeciesSprites = new[] {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/flora32_tree_a.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/flora32_tree_b.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/flora32_tree_c.png"),
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
                // 종 분포 Pine 45% / Birch 30% / Oak 25% (동일 seed tr 로 결정론적).
                int roll = tr.Next(100);
                TreeSpecies sp = roll < 45 ? TreeSpecies.Pine
                               : (roll < 75 ? TreeSpecies.Birch : TreeSpecies.Oak);
                t.name = $"Tree_{sp}_{pos.x}_{pos.y}";
                t.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0);  // tile center 정렬
                var te = t.GetComponent<TreeEntity>();
                if (te != null) te.SetSpecies(sp);
            }
        }
    }
}
