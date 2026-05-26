using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Move toward Player at speed.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyAI : MonoBehaviour
    {
        [SerializeField] private float speed = 2f;
        private Rigidbody2D rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        private void FixedUpdate()
        {
            if (PlayerMovement.Instance == null) return;
            Vector2 me = rb.position;
            Vector2 target = PlayerMovement.Instance.transform.position;
            Vector2 dir = (target - me);
            if (dir.sqrMagnitude < 0.01f) return;
            dir = dir.normalized;
            rb.MovePosition(me + dir * speed * Time.fixedDeltaTime);
        }
    }
}
