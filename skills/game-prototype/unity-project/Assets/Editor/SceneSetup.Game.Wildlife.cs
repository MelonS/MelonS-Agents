using UnityEngine;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10 - SceneSetup.cs GenerateGame 의 wolf + deer spawn 블록 extract.
    //   Day 64 wolf 2 + Day 23/41 deer 8.
    //   원본 SceneSetup.cs L174-214 (40 LOC).
    public static partial class SceneSetup
    {
        private static void SpawnWildlife()
        {
            // Day 64 + #108: Wolf predator - 3 마리 (60x60), 맵 외곽에서 wander
            Sprite wolfSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wolf.png");
            Vector2[] wolfPositions = new[] {
                new Vector2(-27f, 27f), new Vector2(27f, -27f), new Vector2(-27f, -25f),
            };
            foreach (var wpos in wolfPositions)
            {
                GameObject wGo = new GameObject($"Wolf_{wpos.x}_{wpos.y}");
                wGo.transform.position = new Vector3(wpos.x + 0.5f, wpos.y + 0.5f, 0);
                wGo.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
                var wsr2 = wGo.AddComponent<SpriteRenderer>();
                wsr2.sprite = wolfSpr;
                wsr2.sortingOrder = 8;
                var wcol = wGo.AddComponent<BoxCollider2D>();
                wcol.size = new Vector2(1.2f, 0.8f);
                wGo.AddComponent<WolfEnemy>();
            }

            // Day 23+41 + #108: 14 wandering deer - 60x60 맵 비례
            Sprite deerSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/deer.png");
            Vector2[] deerPositions = new[]
            {
                new Vector2(-21f,  12f), new Vector2( 19f,  14f), new Vector2( 24f, -5f),
                new Vector2(-23f, -6f),  new Vector2( -3f, 23f),  new Vector2(  9f,-18f),
                new Vector2(-15f, -3f),  new Vector2( 16f,  6f),  new Vector2(-12f, 16f),
                new Vector2( 22f, 18f),  new Vector2(  5f, 14f),  new Vector2(-19f, 9f),
                new Vector2( 10f, -8f),  new Vector2(-7f, -22f),
            };
            foreach (var dpos in deerPositions)
            {
                GameObject dGo = new GameObject($"Deer_{dpos.x}_{dpos.y}");
                dGo.transform.position = new Vector3(dpos.x + 0.5f, dpos.y + 0.5f, 0);
                var dsr2 = dGo.AddComponent<SpriteRenderer>();
                dsr2.sprite = deerSpr;
                dsr2.sortingOrder = 8;
                dGo.AddComponent<Rigidbody2D>();
                var dcol = dGo.AddComponent<CircleCollider2D>();
                dcol.radius = 0.4f;
                dGo.AddComponent<AnimalEntity>();
            }
        }
    }
}
