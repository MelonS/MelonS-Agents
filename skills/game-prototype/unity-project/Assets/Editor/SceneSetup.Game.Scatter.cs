using UnityEngine;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // Polish Wave v3 / Lane C — world scatter decor (rock, grass tuft, wildflowers).
    // ForceImportAllSprites (SceneSetup.cs) already handles PPU16 + Point for all four PNGs
    // before this method is called.  Scatter placed deterministically (seed 54321) so builds
    // are reproducible; avoids lakes, rock clusters, dirt patches, pawn spawn origin, and
    // does NOT overlap decor_flower tiles (they share the same avoidance set).
    // sortingOrder = 1 (below decor_flower at 2, above tilemap at 0) — scatter recedes.
    public static partial class SceneSetup
    {
        // Proportions: rocks sparse (20), grass tufts medium (35), wf1/wf2 each light (18).
        // Total budget 91 objects; avoid clusters that would create a grid pattern.
        private static void SpawnScatterDecor(TerrainLayout layout)
        {
            Sprite rockSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/decor_rock.png");
            Sprite grassSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/grass_tuft.png");
            Sprite wf1Spr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wildflower1.png");
            Sprite wf2Spr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wildflower2.png");

            if (rockSpr == null) Debug.LogWarning("[SceneSetup] scatter: decor_rock.png sprite null — skipped");
            if (grassSpr == null) Debug.LogWarning("[SceneSetup] scatter: grass_tuft.png sprite null — skipped");
            if (wf1Spr == null) Debug.LogWarning("[SceneSetup] scatter: wildflower1.png sprite null — skipped");
            if (wf2Spr == null) Debug.LogWarning("[SceneSetup] scatter: wildflower2.png sprite null — skipped");

            // [rock*20, grass*35, wf1*18, wf2*18]
            Sprite[] spritePool = new Sprite[]
            {
                rockSpr, rockSpr, rockSpr, rockSpr, rockSpr,
                rockSpr, rockSpr, rockSpr, rockSpr, rockSpr,
                rockSpr, rockSpr, rockSpr, rockSpr, rockSpr,
                rockSpr, rockSpr, rockSpr, rockSpr, rockSpr,    // 20 rocks
                grassSpr, grassSpr, grassSpr, grassSpr, grassSpr,
                grassSpr, grassSpr, grassSpr, grassSpr, grassSpr,
                grassSpr, grassSpr, grassSpr, grassSpr, grassSpr,
                grassSpr, grassSpr, grassSpr, grassSpr, grassSpr,
                grassSpr, grassSpr, grassSpr, grassSpr, grassSpr,
                grassSpr, grassSpr, grassSpr, grassSpr, grassSpr,
                grassSpr, grassSpr, grassSpr, grassSpr, grassSpr,  // 35 grass
                wf1Spr, wf1Spr, wf1Spr, wf1Spr, wf1Spr,
                wf1Spr, wf1Spr, wf1Spr, wf1Spr, wf1Spr,
                wf1Spr, wf1Spr, wf1Spr, wf1Spr, wf1Spr,
                wf1Spr, wf1Spr, wf1Spr,                           // 18 wf1
                wf2Spr, wf2Spr, wf2Spr, wf2Spr, wf2Spr,
                wf2Spr, wf2Spr, wf2Spr, wf2Spr, wf2Spr,
                wf2Spr, wf2Spr, wf2Spr, wf2Spr, wf2Spr,
                wf2Spr, wf2Spr, wf2Spr,                           // 18 wf2
            };
            int total = spritePool.Length; // 91

            int half = TerrainLayout.MAP_HALF;
            System.Random rng = new System.Random(54321);
            int placed = 0;
            int attempts = 0;
            int idx = 0;
            while (idx < total && attempts < total * 30)
            {
                attempts++;
                // Sub-cell offset [0.1, 0.9] so scatter doesn't always sit at the cell corner.
                float ox = (float)(rng.NextDouble() * 0.8 + 0.1);
                float oy = (float)(rng.NextDouble() * 0.8 + 0.1);
                int fx = rng.Next(-(half - 1), half);
                int fy = rng.Next(-(half - 1), half);
                Vector2 fp = new Vector2(fx + ox, fy + oy);

                bool skip = false;
                // Avoid lakes (+1.5 buffer)
                for (int li = 0; li < layout.lakeCenters.Length; li++)
                    if ((fp - layout.lakeCenters[li]).magnitude < layout.lakeRadii[li] + 1.5f) { skip = true; break; }
                if (skip) continue;
                // Avoid rock clusters (+0.5 buffer)
                foreach (var rc in layout.rockClusterCenters)
                    if ((fp - rc).magnitude < layout.rockRadius + 0.5f) { skip = true; break; }
                if (skip) continue;
                // Avoid dirt patches (inside only — scatter can be near edge)
                foreach (var dc in layout.dirtCenters)
                    if ((fp - dc).magnitude < layout.dirtRadius - 0.3f) { skip = true; break; }
                if (skip) continue;
                // Avoid pawn spawn zone (near origin)
                if (Mathf.Abs(fx) < 5 && Mathf.Abs(fy) < 3) continue;

                Sprite spr = spritePool[idx];
                if (spr == null) { idx++; continue; }  // skip null entries gracefully

                string tag = spr == rockSpr ? "Rock" :
                             spr == grassSpr ? "Grass" :
                             spr == wf1Spr ? "WF1" : "WF2";
                GameObject go = new GameObject($"Scatter_{tag}_{placed}");
                go.transform.position = new Vector3(fx + ox, fy + oy, 0f);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = spr;
                // sortingOrder 1 — below decor_flower (2), above tilemap (0).
                // Scatter visually recedes under gameplay entities.
                sr.sortingOrder = 1;
                placed++;
                idx++;
            }
            Debug.Log($"[SceneSetup] scatter decor {placed}/{total} placed");
        }
    }
}
