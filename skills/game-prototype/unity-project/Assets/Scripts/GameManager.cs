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
                // Day 36: pawn 안 보이는 문제 진단 — Instantiate 후 sprite 상태 로깅 + visible fallback
                var sr = p.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    if (sr.sprite == null)
                    {
                        Debug.LogError($"[GameManager] pawn[{i}] SpriteRenderer.sprite NULL at runtime — applying flat-color fallback");
                        sr.color = new Color(0.95f, 0.65f, 0.35f, 1f);
                    }
                    else
                    {
                        Debug.Log($"[GameManager] pawn[{i}] sprite={sr.sprite.name} enabled={sr.enabled} color={sr.color}");
                    }
                    sr.enabled = true;   // 명시적으로 enable
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
                i++;
            }
            Debug.Log($"[GameManager] Day 4: spawned {spawnPositions.Length} colonists");
        }
    }
}
