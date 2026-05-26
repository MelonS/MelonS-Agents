using UnityEngine;
using UnityEngine.Tilemaps;

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

        // Step 81: 맵 경계 — 40x40 tile 맵의 안쪽 (±19) 으로 강제 clamp.
        //  타일이 그려진 영역 밖으로는 절대 못 나감.
        public static readonly Vector2 WORLD_MIN = new Vector2(-19f, -19f);
        public static readonly Vector2 WORLD_MAX = new Vector2( 19f,  19f);

        // Step 81: 호수/바위 통과 방지 — SceneSetup이 GroundTilemap 과
        //  Water/Rock TileBase 참조를 정적 세팅.  null 이면 obstacle 체크 skip.
        public static Tilemap GroundTilemap;
        public static TileBase WaterTile;
        public static TileBase RockTile;

        public static bool IsBlockedAt(Vector2 worldPos)
        {
            if (GroundTilemap == null) return false;
            Vector3Int cell = GroundTilemap.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0));
            TileBase t = GroundTilemap.GetTile(cell);
            return t != null && (t == WaterTile || t == RockTile);
        }

        private Vector2? target;
        private PawnHealth health;  // Step45 — leg damage 영향
        private void Awake() { health = GetComponent<PawnHealth>(); }

        public static Vector2 ClampToWorld(Vector2 p)
        {
            return new Vector2(
                Mathf.Clamp(p.x, WORLD_MIN.x, WORLD_MAX.x),
                Mathf.Clamp(p.y, WORLD_MIN.y, WORLD_MAX.y));
        }

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
            // Step45: 다리 다친 만큼 속도 감소
            float speedMul = health != null ? health.MovementSpeedMultiplier() : 1f;
            // Step 81: target 도 맵 안쪽으로 강제.  target 자체가 호수/바위면 stop.
            Vector2 clampedTarget = ClampToWorld(target.Value);
            if (IsBlockedAt(clampedTarget))
            {
                target = null;
                return;
            }
            Vector2 next = Vector2.MoveTowards(cur, clampedTarget, moveSpeed * speedMul * Time.deltaTime);
            next = ClampToWorld(next);
            // 다음 step 이 호수/바위면 가지 말것 — target 취소
            if (IsBlockedAt(next))
            {
                target = null;
                return;
            }
            transform.position = new Vector3(next.x, next.y, transform.position.z);

            if (Vector2.Distance(next, target.Value) <= arriveDistance)
            {
                target = null;
            }
        }
    }
}
