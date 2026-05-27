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
            // Day 41: tree 위치 - 40x40 맵에 20그루 (분포 균등 + 호수·바위 회피).
            //  결정론적 (seed=24680).
            var treePositionsList = new System.Collections.Generic.List<Vector2>();
            System.Random tr = new System.Random(24680);
            int tries = 0;
            while (treePositionsList.Count < 20 && tries < 400)
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
                t.name = $"Tree_{pos.x}_{pos.y}";
                t.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0);  // tile center 정렬
            }
        }
    }
}
