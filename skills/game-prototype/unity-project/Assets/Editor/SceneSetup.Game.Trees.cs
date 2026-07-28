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
            // 아트 v3 (2026-07-25 운영자 "림월드식·셀당 64"): 페인털리 64×96 수종
            //  스프라이트 — FLUX 생성+큐레이션.  PPU 64 = 1×1.5칸 유지.
            foreach (var np in new[] { "flora64_oak.png", "flora64_birch.png",
                                       "flora64_maple.png", "flora64_pine.png",
                                       "flora64_spruce.png" })
            {
                string apath2 = $"Assets/Sprites/{np}";
                if (!System.IO.File.Exists(apath2)) continue;
                AssetDatabase.ImportAsset(apath2, ImportAssetOptions.ForceSynchronousImport);
                var ti2 = AssetImporter.GetAtPath(apath2) as TextureImporter;
                if (ti2 != null)
                {
                    ti2.textureType = TextureImporterType.Sprite;
                    ti2.spriteImportMode = SpriteImportMode.Single;
                    ti2.spritePixelsPerUnit = 64;   // v3 페인털리 밀도
                    ti2.filterMode = FilterMode.Point;
                    ti2.SaveAndReimport();
                }
            }
            // 아트 v2 A단계 (2026-06-11): 수종 3종을 32x48 캐노피 변형으로 교체.
            // L1 (2026-07-24): 5종 체계 — 실루엣이 종 정체성 (침엽 vs 로브 3형).
            // 아트 B2 (2026-07-24): Tiny Swords 단일 수종 우선 (팩엔 나무 1종 —
            //  종 구분은 scale/틴트로 유지, 종별 실루엣은 정합 생성 후속).  192px
            //  프레임 PPU96 = 2×2칸 시각 footprint (셀 점유는 기존 1칸 불변).
            // 혼합 캐노피 (2026-07-29) — B2 가 후속으로 미룬 "종별 실루엣"을 이행.
            //  B2 는 5종 슬롯에 ts_tree 를 **다섯 번** 넣어 두었다.  게임 로직은 5종
            //  분포(Pine30/Birch20/Oak20/Maple15/Spruce15)를 이미 굴리고 있었는데
            //  화면에는 전부 같은 나무가 나와, 45그루가 깔린 맵이 단일 수종 모노컬처로
            //  읽혔다(인게임 캡처로 확인 — 캐릭터보다 나무가 화면을 지배).
            //  자체 제작 flora64 5종이 이미 커밋돼 있고(.meta 포함, GUID 안정) 실루엣과
            //  색이 뚜렷이 다르므로, 침엽 상층은 ts_tree(PPU96=2×2칸)로 무게를 주고
            //  나머지는 flora64(PPU64≈1×1.5칸)로 하층을 채운다 — 합성 비교에서
            //  ts단일/flora64단독/혼합 중 혼합이 가장 숲답게 읽혔다.
            //  셀 점유는 여전히 1칸 (시각 footprint 만 다름).
            var tsTree = ImportSpriteAt("Assets/Sprites/ts_tree.png", 96f);
            Sprite F64(string n) => ImportSpriteAt($"Assets/Sprites/flora64_{n}.png", 64f);
            // 슬롯 순서 = TreeSpecies enum 순서 (Pine, Birch, Oak, Maple, Spruce)
            var pine = tsTree != null ? tsTree : F64("pine");
            MelonS.GameProto.TreeEntity.SpeciesSprites = new[] {
                pine, F64("birch"), F64("oak"), F64("maple"), F64("spruce"),
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
