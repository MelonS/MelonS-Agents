using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10 - SceneSetup.cs 의 GenerateGame 안의 Settlement (Day 57) 블록 extract.
    //   wall 5 + floor 6 + stove + bench + 12 crops + 2 lamps + 9 stockpile markers.
    //   원본 SceneSetup.cs L340-442 (100 LOC).
    public static partial class SceneSetup
    {
        private static void GenerateStarterSettlement(
            Tilemap tm, TileBase dirtTile,
            GameObject wallPrefab, GameObject floorPrefab,
            GameObject stovePrefab, GameObject benchPrefab)
        {
            // Day 57: 시작 정착지 - 화면 중앙 좌측에 미리 짠 구조물 + 농장 + stockpile.
            //  보여주기용 (벽 5 + 바닥 6 + 화덕 1 + 연구대 1).  pawn 1명이 이 안에
            //  자리잡고 시작하는 vanilla colony-sim 첫 게임 느낌.
            //  좌표 기준: 정착지 중심 (-5, 0).  방 크기 4x3.
            //
            //   X X X X X
            //   . . . . .   <- floor 안쪽 (음수 y)
            //   . . . . .
            //   X . . . X   <- 좌우 벽 + 가운데 floor
            //
            // 벽 5개: (-7, 1)..(-3, 1) 위쪽 가로벽 + 좌우 양끝 (-7, 0) (-3, 0)
            Sprite cropSprite         = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice.png");
            Sprite cropSeedlingSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice_seedling.png");
            Sprite cropGrowingSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice_growing.png");
            if (cropSeedlingSprite == null) Debug.LogWarning("[SceneSetup] crop_rice_seedling.png null — stage-sprite wiring skipped");
            if (cropGrowingSprite  == null) Debug.LogWarning("[SceneSetup] crop_rice_growing.png null — stage-sprite wiring skipped");
            Sprite stockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/stockpile_marker.png");

            // #229 the reference sim Crashlanded 정합 — 맨땅 시작.  미리 지어진 베이스(벽/바닥/화덕/
            //  연구대/작물/램프)를 제거하고 플레이어가 직접 건설하게 한다.  저장 구역만 유지
            //  (the reference sim 도 stockpile zone 은 무료 지정).  코드는 플래그로 보존(되돌리기 쉽게).
            bool prebuiltBase = false;
            if (prebuiltBase) {
            // 벽 5개 - 북쪽 벽 라인 (y=1, x=-7..-3) + 좌우 벽 (x=-7, x=-3, y=0)
            //  운영자 피드백: tile 시각 center 가 (x+0.5, y+0.5) 라서 entity 도 +0.5 정렬.
            Vector2Int[] wallSpots = new[]
            {
                new Vector2Int(-7, 1), new Vector2Int(-6, 1), new Vector2Int(-5, 1),
                new Vector2Int(-4, 1), new Vector2Int(-3, 1),
            };
            foreach (var w in wallSpots)
            {
                GameObject wGo = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab);
                wGo.name = $"StarterWall_{w.x}_{w.y}";
                wGo.transform.position = new Vector3(w.x + 0.5f, w.y + 0.5f, 0f);
            }
            // 바닥 6타일 (인테리어)
            Vector2Int[] floorSpots = new[]
            {
                new Vector2Int(-6, 0), new Vector2Int(-5, 0), new Vector2Int(-4, 0),
                new Vector2Int(-6, -1), new Vector2Int(-5, -1), new Vector2Int(-4, -1),
            };
            foreach (var f in floorSpots)
            {
                GameObject fGo = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
                fGo.name = $"StarterFloor_{f.x}_{f.y}";
                fGo.transform.position = new Vector3(f.x + 0.5f, f.y + 0.5f, 0f);
            }
            // 시작 화덕 + 시작 연구대 (tile center 정렬)
            GameObject starterStove = (GameObject)PrefabUtility.InstantiatePrefab(stovePrefab);
            starterStove.name = "StarterStove";
            starterStove.transform.position = new Vector3(-4f + 0.5f, 0f + 0.5f, 0f);
            GameObject starterBench = (GameObject)PrefabUtility.InstantiatePrefab(benchPrefab);
            starterBench.name = "StarterResearchBench";
            starterBench.transform.position = new Vector3(-6f + 0.5f, 0f + 0.5f, 0f);

            // Day 57: 농장 - 4x3 (12 타일) 작은 농장 (우측에 배치).  단순 데코로
            //  생장 시각 (lvl 1 sprout). Day 67-68에서 실제 농경 로직 추가 예정.
            //  좌표 (3..6, -2..-4).
            for (int cx = 3; cx <= 6; cx++)
            {
                for (int cy = -4; cy <= -2; cy++)
                {
                    GameObject cGo = new GameObject($"Crop_{cx}_{cy}");
                    cGo.transform.position = new Vector3(cx + 0.5f, cy + 0.5f, 0f);
                    var csr = cGo.AddComponent<SpriteRenderer>();
                    csr.sprite = cropSprite;
                    csr.sortingOrder = 3;
                    // Day 67-68: CropEntity component → 자라서 익으면 우클릭 수확
                    var ce = cGo.AddComponent<CropEntity>();
                    // Day 79: 시작 시 growth 0.4~0.85 무작위로 - 곧 수확 가능, 다양한 stage 시각.
                    var ceField = typeof(CropEntity).GetField("growth",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (ceField != null)
                    {
                        float startGrowth = 0.4f + ((cx + cy + 100) % 5) * 0.10f;  // deterministic
                        ceField.SetValue(ce, startGrowth);
                    }
                    // W-M1-02 Art: wire the three per-stage sprite refs via
                    // SerializedObject so [SerializeField] private fields persist
                    // into the scene.  When all three are assigned, CropEntity
                    // switches sprites at runtime instead of color-tinting.
                    if (cropSeedlingSprite != null && cropGrowingSprite != null && cropSprite != null)
                    {
                        var ceSo = new SerializedObject(ce);
                        ceSo.FindProperty("spriteSeedling").objectReferenceValue = cropSeedlingSprite;
                        ceSo.FindProperty("spriteGrowing").objectReferenceValue  = cropGrowingSprite;
                        ceSo.FindProperty("spriteRipe").objectReferenceValue     = cropSprite;
                        ceSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                    // 농경지 dirt 타일 강제 부착 (배경)
                    tm.SetTile(new Vector3Int(cx, cy, 0), dirtTile);
                }
            }

            // Stretch: 가로등 2개 - 시작 정착지 북쪽 벽 위 (tile center 정렬)
            Sprite lampSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/lamp.png");
            //  tile (x, y) 시각 center = (x+0.5, y+0.5).  벽 위쪽 (y=2) 행에 배치.
            Vector2Int[] lampSpots = { new Vector2Int(-7, 2), new Vector2Int(-3, 2) };
            foreach (var lp in lampSpots)
            {
                GameObject lampGo = new GameObject($"Lamp_{lp.x}_{lp.y}");
                lampGo.transform.position = new Vector3(lp.x + 0.5f, lp.y + 0.5f, 0f);
                var lsr = lampGo.AddComponent<SpriteRenderer>();
                lsr.sprite = lampSprite;
                lsr.sortingOrder = 6;
            }

            } // #229 end prebuiltBase — 맨땅 시작이라 위 구조물은 스폰 안 함

            // #246 운영자 fb "게임 시작 시 창고영역 세팅 안 되어 있고 유저가 정하게" —
            //  시작 시 stockpile zone 자동 스폰 안 함.  플레이어가 구상>지시>저장(O)로 직접
            //  지정 (the reference sim: 시작 시 저장구역 없음).
            bool autoStockpile = false;
            if (autoStockpile)
            for (int sx = -2; sx <= 0; sx++)
            {
                for (int sy = -3; sy <= -1; sy++)
                {
                    StockpileZoneEntity.Spawn(new Vector3(sx, sy, 0f), stockSprite);
                }
            }
        }
    }
}
