using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>8-way WASD/Arrow movement.  Day 1 simple kinematic.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private float speed = 5f;
        private Rigidbody2D rb;
        public Vector2 Velocity { get; private set; }

        public static PlayerMovement Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        private void FixedUpdate()
        {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            Vector2 raw = new Vector2(x, y);
            if (raw.sqrMagnitude > 1f) raw = raw.normalized;
            Velocity = raw * speed;
            rb.MovePosition(rb.position + Velocity * Time.fixedDeltaTime);
        }
    }
}
