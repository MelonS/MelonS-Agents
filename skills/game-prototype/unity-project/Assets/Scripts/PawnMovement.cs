using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Pawn movement.  Day 2 = simple lerp to target.  No pathfinding /
    /// obstacle avoidance (Day 4+).
    /// </summary>
    public class PawnMovement : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float arriveDistance = 0.05f;

        private Vector2? target;
        private PawnHealth health;  // Day 45 — leg damage 영향
        private void Awake() { health = GetComponent<PawnHealth>(); }

        public bool HasTarget => target.HasValue;
        public bool IsMoving => target.HasValue;

        public void SetTarget(Vector2 worldPos)
        {
            target = worldPos;
        }

        public void ClearTarget()
        {
            target = null;
        }

        private void Update()
        {
            if (!target.HasValue) return;

            Vector2 cur = transform.position;
            // Day 45: 다리 다친 만큼 속도 감소
            float speedMul = health != null ? health.MovementSpeedMultiplier() : 1f;
            Vector2 next = Vector2.MoveTowards(cur, target.Value, moveSpeed * speedMul * Time.deltaTime);
            transform.position = new Vector3(next.x, next.y, transform.position.z);

            if (Vector2.Distance(next, target.Value) <= arriveDistance)
            {
                target = null;
            }
        }
    }
}
