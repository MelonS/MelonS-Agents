using UnityEngine;
using UnityEngine.Tilemaps;
using MelonS.GameProto.Data;

namespace MelonS.GameProto
{
    /// <summary>
    /// Pawn movement.  Day 2 = simple lerp to target.  No pathfinding /
    /// obstacle avoidance (Day 4+).
    /// </summary>
    public class PawnMovement : MonoBehaviour
    {
        // R2: moveSpeed/arriveDistance 외부화 - PawnStats SO 참조
        [SerializeField] private PawnStats stats;
        // legacy fallback - SO 없으면 default 30/1/1.0/1.0/3.0 채워줌

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
        // I19 bug — pawn 이 obstacle 옆에서 target 계속 cancel 되며 정체.
        //  1.5s 동안 안 움직였으면 perpendicular nudge 로 escape.
        private Vector3 lastPos;
        private float lastMoveTime;
        private float lastUnstuckTime = -10f;

        private void Awake()
        {
            health = GetComponent<PawnHealth>();
            if (stats == null) stats = PawnStats.CreateDefault();
            lastPos = transform.position;
            lastMoveTime = Time.time;
        }

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
            // I19 bug fix — chopper/AI 가 world bound 밖 entity 위치를 target 으로 줄 때
            //  ClampToWorld 가 안 적용돼서 pawn 이 도달 못 함.  여기서 강제 clamp.
            target = ClampToWorld(worldPos);
        }

        public void ClearTarget()
        {
            target = null;
        }

        private void Update()
        {
            // I19 unstuck — pawn 이 target 있는데 1.5s 동안 안 움직였으면 다른 방향 nudge.
            //  perpendicular shift 0.5 unit (rock 한 칸 정도).  cooldown 3s.
            if (target.HasValue)
            {
                Vector3 curPos = transform.position;
                if ((curPos - lastPos).sqrMagnitude > 0.001f) { lastPos = curPos; lastMoveTime = Time.time; }
                if (Time.time - lastMoveTime > 1.5f && Time.time - lastUnstuckTime > 3f)
                {
                    // 4 방향 시도 - 가장 가까운 (target 방향과 90도) 으로 nudge
                    Vector2 toTarget = (target.Value - (Vector2)curPos).normalized;
                    Vector2[] nudges = {
                        new Vector2(-toTarget.y,  toTarget.x) * 0.6f,
                        new Vector2( toTarget.y, -toTarget.x) * 0.6f,
                        new Vector2( toTarget.x,  toTarget.y) * 0.6f,
                        new Vector2(-toTarget.x, -toTarget.y) * 0.6f,
                    };
                    foreach (var n in nudges)
                    {
                        Vector2 candidate = ClampToWorld((Vector2)curPos + n);
                        if (!IsBlockedAt(candidate))
                        {
                            transform.position = new Vector3(candidate.x, candidate.y, curPos.z);
                            lastPos = transform.position;
                            lastMoveTime = Time.time;
                            lastUnstuckTime = Time.time;
                            break;
                        }
                    }
                }
            }

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
            Vector2 next = Vector2.MoveTowards(cur, clampedTarget, stats.moveSpeed * speedMul * Time.deltaTime);
            next = ClampToWorld(next);
            // I19 bug fix - 다음 step 이 obstacle 이라도 alternative direction 시도.
            //  rock 옆에서 pawn 영원히 멈춰있던 버그 (target 즉시 cancel → chop/etc. fail).
            if (IsBlockedAt(next))
            {
                // x-axis 만 이동 시도 (y 는 유지)
                Vector2 nextX = new Vector2(next.x, cur.y);
                Vector2 nextY = new Vector2(cur.x, next.y);
                if (!IsBlockedAt(nextX))      next = nextX;
                else if (!IsBlockedAt(nextY)) next = nextY;
                else
                {
                    // 양쪽 다 막힘 — target 자체를 cancel (영구 stuck 방지)
                    target = null;
                    return;
                }
            }
            transform.position = new Vector3(next.x, next.y, transform.position.z);

            if (Vector2.Distance(next, target.Value) <= stats.arriveDistance)
            {
                target = null;
            }
        }
    }
}
