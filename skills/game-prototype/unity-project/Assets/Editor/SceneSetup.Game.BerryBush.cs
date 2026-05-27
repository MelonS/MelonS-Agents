using UnityEngine;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10m - SceneSetup.cs BerryBush spawn 블록 extract.
    //   원본 SceneSetup.cs L208-229 (~20 LOC, comment 포함 30L).
    public static partial class SceneSetup
    {
        private static void SpawnBerryBushes(Sprite treeSprite)
        {
            // Day 11/41: 6 berry bushes (40x40 맵).  AI gatherer 가 target.
            //   tree sprite 재사용 (green tint).  BerryBushEntity.Awake 가 stock 별
            //   sprite color 갱신 — 여기 green tint 는 placeholder.
            Vector2[] bushPositions = new[]
            {
                new Vector2(-9f,  -2f),
                new Vector2(  6f,  -8f),
                new Vector2( 11f,   3f),
                new Vector2(-13f,  10f),
                new Vector2(  3f,  13f),
                new Vector2( -6f, -14f),
            };
            foreach (var pos in bushPositions)
            {
                GameObject b = new GameObject($"BerryBush_{pos.x}_{pos.y}");
                b.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0);  // tile center 정렬
                SpriteRenderer bsr = b.AddComponent<SpriteRenderer>();
                bsr.sprite = treeSprite;
                bsr.sortingOrder = 5;
                bsr.color = new Color(0.6f, 0.9f, 0.4f, 1f);
                BoxCollider2D bcol = b.AddComponent<BoxCollider2D>();
                bcol.size = Vector2.one;
                b.AddComponent<BerryBushEntity>();
            }
        }
    }
}
