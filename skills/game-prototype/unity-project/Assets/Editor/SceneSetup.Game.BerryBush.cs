using UnityEngine;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10m - SceneSetup.cs BerryBush spawn 블록 extract.
    //   원본 SceneSetup.cs L208-229 (~20 LOC, comment 포함 30L).
    public static partial class SceneSetup
    {
        private static void SpawnBerryBushes(Sprite treeSprite)
        {
            // Day 11/41: 10 berry bushes (60x60 맵).  AI gatherer 가 target.
            // Round 7: dedicated berry_bush.png art landed → tree-reuse + neon
            //   tint 제거.  BerryBushEntity.Awake 가 spriteRenderer.color 를 base 로
            //   캐시한 뒤 stock 별 brightness 곱(0.35~1.0)을 적용하므로, base 를
            //   Color.white 로 두면 새 스프라이트가 본래 색으로 시작하고 stock
            //   상태는 밝기 변화로 계속 표현된다 (treeSprite fallback only if 미import).
            // 아트 v2 A단계: 32px 덤불 (수확후 프레임 flora32_bush_picked 은 후속 배선).
            Sprite bushSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/flora32_bush_berry.png");
            if (bushSprite == null)
            {
                Debug.LogWarning("[SceneSetup] berry_bush.png missing → falling back to tree sprite");
                bushSprite = treeSprite;
            }
            Vector2[] bushPositions = new[]
            {
                new Vector2(-9f,  -2f),  new Vector2(  6f,  -8f), new Vector2( 11f,   3f),
                new Vector2(-13f,  10f), new Vector2(  3f,  13f), new Vector2( -6f, -14f),
                new Vector2(-22f,  6f),  new Vector2( 21f,  18f), new Vector2(-19f, -21f),
                new Vector2( 17f, -16f),
            };
            foreach (var pos in bushPositions)
            {
                GameObject b = new GameObject($"BerryBush_{pos.x}_{pos.y}");
                b.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0);  // tile center 정렬
                SpriteRenderer bsr = b.AddComponent<SpriteRenderer>();
                bsr.sprite = bushSprite;
                bsr.sortingOrder = 5;
                bsr.color = Color.white;   // neutral base; BerryBushEntity tints by stock brightness
                BoxCollider2D bcol = b.AddComponent<BoxCollider2D>();
                bcol.size = Vector2.one;
                b.AddComponent<BerryBushEntity>();
            }
        }
    }
}
