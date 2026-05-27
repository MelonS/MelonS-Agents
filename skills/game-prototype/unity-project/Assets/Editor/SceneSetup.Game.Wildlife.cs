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
            // Day 64: Wolf predator - 2 마리, 맵 외곽에서 wander
            Sprite wolfSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wolf.png");
            Vector2[] wolfPositions = new[] { new Vector2(-17f, 17f), new Vector2(17f, -17f) };
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

            // Day 23+41: 8 wandering deer - 40x40 맵 비례로 증가
            Sprite deerSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/deer.png");
            Vector2[] deerPositions = new[]
            {
                new Vector2(-14f,  8f),
                new Vector2( 13f,  9f),
                new Vector2( 16f, -3f),
                new Vector2(-15f, -4f),
                new Vector2( -2f, 15f),
                new Vector2(  6f,-12f),
                new Vector2(-10f, -2f),
                new Vector2( 11f,  4f),
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
