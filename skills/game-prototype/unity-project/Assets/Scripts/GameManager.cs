using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Game scene bootstrap — spawns initial pawn(s) and holds global
    /// game state references.  Day 1 = single pawn at origin.  Later
    /// days will own colonist list, needs system, AI Director hookup.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Spawn settings")]
        [SerializeField] private GameObject pawnPrefab;
        [SerializeField] private Sprite arrowSpriteRuntime;  // Day 50
        [SerializeField] private Vector2[] spawnPositions = new Vector2[]
        {
            new Vector2(-2f, 0f),
            new Vector2( 0f, 0f),
            new Vector2( 2f, 0f),
        };
        // Day 32: 한국 이름 — generic, 흔한 한국 이름 (저작권 무관).
        private static readonly string[] KoreanNames = new[]
        {
            "지훈", "민지", "서연", "준호", "예린", "도현", "수아", "현우",
        };

        private void Start()
        {
            if (pawnPrefab == null)
            {
                Debug.LogWarning("[GameManager] pawnPrefab not assigned");
                return;
            }
            int i = 0;
            foreach (var pos in spawnPositions)
            {
                GameObject p = Instantiate(pawnPrefab, pos, Quaternion.identity);
                // Day 37 영구 fix: pawn 16x16 sprite가 ortho 8 카메라에서 너무 작아
                //  보였던 문제 — scale 2x를 prefab 외부에서도 한 번 더 강제.
                //  (prefab 자체에도 GeneratePawnPrefab에서 2x scale 박혀있음)
                p.transform.localScale = new Vector3(2f, 2f, 1f);
                var sr = p.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    if (sr.sprite == null)
                    {
                        Debug.LogError($"[GameManager] pawn[{i}] SpriteRenderer.sprite NULL — flat-color fallback");
                        sr.color = new Color(0.95f, 0.65f, 0.35f, 1f);
                    }
                    sr.enabled = true;
                }
                PawnEntity entity = p.GetComponent<PawnEntity>();
                if (entity != null)
                {
                    string name = KoreanNames[i % KoreanNames.Length];
                    var nameField = typeof(PawnEntity).GetField("pawnName",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (nameField != null) nameField.SetValue(entity, name);
                }
                // Day 50: arrow sprite injection — PawnUtilityAI 가 ranged
                //  attack용으로 사용 (단 research "simple_bow" 완료 후 활성).
                var ai = p.GetComponent<PawnUtilityAI>();
                if (ai != null && arrowSpriteRuntime != null) ai.SetArrowSprite(arrowSpriteRuntime);
                i++;
            }
            Debug.Log($"[GameManager] Day 4: spawned {spawnPositions.Length} colonists");
        }
    }
}
