using UnityEngine;
using UnityEngine.Tilemaps;
using MelonS.GameProto.Data;
using MelonS.GameProto.AI;

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

        // 운영자 피드백 #108 - 60x60 tile 맵의 안쪽 (±29) 으로 강제 clamp.
        //  타일이 그려진 영역 밖으로는 절대 못 나감.
        public static readonly Vector2 WORLD_MIN = new Vector2(-29f, -29f);
        public static readonly Vector2 WORLD_MAX = new Vector2( 29f,  29f);

        // Step 81: 호수/바위 통과 방지 — SceneSetup이 GroundTilemap 과
        //  Water/Rock TileBase 참조를 정적 세팅.  null 이면 obstacle 체크 skip.
        public static Tilemap GroundTilemap;
        public static TileBase WaterTile;
        public static TileBase RockTile;

        // #199 B0 — grid A* pathfinding scaffold.
        //  UsePathfinding: master flag.  DEFAULT OFF — nothing reads it in B0;
        //  B1 wires SetTarget to follow an A* path when true.  Old MoveTowards
        //  stays the live behavior until B2 flips this on.
        public static bool UsePathfinding = false;
        //  Grid: built once at scene start (TilemapStaticRefInit) from the
        //  tilemap.  Exists at runtime so B1 can consume it; null-safe everywhere.
        public static PathGrid Grid;

        public static bool IsBlockedAt(Vector2 worldPos)
        {
            if (GroundTilemap == null) return false;
            Vector3Int cell = GroundTilemap.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0));
            TileBase t = GroundTilemap.GetTile(cell);
            return t != null && (t == WaterTile || t == RockTile);
        }

        // #157 - 바닥 위 (FloorEntity) 면 이동 속도 보너스.
        private static bool IsOnFloor(Vector2 pos)
        {
            var hits = Physics2D.OverlapBoxAll(pos, Vector2.one * 0.3f, 0f);
            foreach (var h in hits)
                if (h != null && h.GetComponent<FloorEntity>() != null) return true;
            return false;
        }

        private Vector2? target;
        private PawnHealth health;  // Step45 — leg damage 영향

        // #199 B1 — A* path-follow state (only used when UsePathfinding == true).
        //  _path: inclusive cell list [start..goal] from AStar.FindPath.
        //  _pathIndex: index of the waypoint the pawn is currently walking toward.
        //  Stored in SetTarget, advanced cell-by-cell in Update / AdvanceAlongPath.
        //  R-4 de-risk: the path is computed ONCE per SetTarget (callers like
        //  PawnChopper re-call SetTarget every frame — see below, we skip recompute
        //  when the requested target cell is unchanged AND a live path exists).
        private System.Collections.Generic.List<Vector2Int> _path;
        private int _pathIndex;
        private Vector2Int _pathGoalCell;   // goal cell of the current cached path

        // #199 B1 — RimWorld "destination unreachable" signal.  Set true when an
        //  A* request returns null/empty (target cannot be reached) and the target
        //  is cleared.  Set false whenever a new target is accepted with a valid
        //  path.  C1 give-up timers should switch from "timer + dist>range" to
        //  consuming THIS flag (real unreachability), not straight-line distance.
        //  Exposed now so C1 can wire it; nothing reads it yet at runtime (flag OFF).
        public bool LastPathFailed { get; private set; }

        private void Awake()
        {
            health = GetComponent<PawnHealth>();
            if (stats == null) stats = PawnStats.CreateDefault();
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
            Vector2 clamped = ClampToWorld(worldPos);

            // #199 B1 — pathfinding branch.  When ON, compute an A* path from the
            //  pawn's current cell to the (clamped) target cell and follow it.
            //  When OFF, fall through to the OLD point-lerp behavior (byte-for-byte
            //  unchanged below) — this is the live path until B2.
            if (UsePathfinding && Grid != null)
            {
                // Clamp the goal cell into grid bounds (I19 "tree outside ±29"):
                //  a slightly-out-of-bounds work target snaps to the nearest
                //  in-bounds cell so the chopper still gets a reachable path,
                //  matching ClampToWorld intent.  ClampToWorld already bounded the
                //  world pos; the cell derived from it is therefore in-bounds.
                Vector2Int goalCell = PathGrid.WorldToCell(clamped);

                // R-4: callers re-call SetTarget every frame.  If we already have a
                //  live path to the same goal cell, do NOT recompute A* — keep
                //  following the cached path.  Only recompute on a goal-cell change.
                if (_path != null && _pathIndex < _path.Count && _pathGoalCell == goalCell)
                {
                    target = clamped;   // refresh world target (visual), keep path
                    return;
                }

                Vector2Int startCell = PathGrid.WorldToCell(transform.position);
                var path = AStar.FindPath(Grid, startCell, goalCell);
                if (path == null || path.Count == 0)
                {
                    // RimWorld "destination unreachable" — give up the target.
                    _path = null;
                    _pathIndex = 0;
                    target = null;
                    LastPathFailed = true;
                    return;
                }

                _path = path;
                _pathIndex = 0;
                _pathGoalCell = goalCell;
                target = clamped;
                LastPathFailed = false;
                return;
            }

            // ---- OLD behavior (flag OFF) — live until B2.  Unchanged. ----
            target = clamped;
            // Keep path state clean when running the old branch.
            _path = null;
            _pathIndex = 0;
            LastPathFailed = false;
        }

        public void ClearTarget()
        {
            target = null;
            _path = null;
            _pathIndex = 0;
        }

        /// <summary>
        /// #199 B1 — one tick of A* path-following, factored out so V-tests can
        /// drive it deterministically without a full scene/Update loop.  Moves
        /// the transform toward the current waypoint's cell center by
        /// <paramref name="maxDelta"/> world units (already speed-scaled by the
        /// caller), advances the waypoint index on arrival, and clears the target
        /// (returns true) when the final waypoint is reached.  No-op when there is
        /// no live path.  Smooth lerp between cell centers (RimWorld glide).
        /// Returns true when arrival happened this tick.
        /// </summary>
        public bool AdvanceAlongPath(float maxDelta)
        {
            if (_path == null || _pathIndex >= _path.Count) return false;

            Vector2 cur = transform.position;
            Vector2 wp = PathGrid.CellToWorld(_path[_pathIndex]);
            Vector2 next = Vector2.MoveTowards(cur, wp, maxDelta);
            transform.position = new Vector3(next.x, next.y, transform.position.z);

            // Within arriveDistance of the current waypoint → advance.
            if (Vector2.Distance(next, wp) <= stats.arriveDistance)
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count)
                {
                    // Final waypoint reached → arrive, exactly like the old branch.
                    target = null;
                    _path = null;
                    _pathIndex = 0;
                    return true;
                }
            }
            return false;
        }

        private void Update()
        {
            // #199 B1 — pathfinding branch (LIVE in the game since B2).  Reuses
            //  the FULL speed block (leg damage, abilities, floor bonus, door
            //  slowdown) then steps along the cached A* path.  When the flag is
            //  OFF (isolated V-test scene only) this is skipped and the minimal
            //  OLD fallback below runs.
            if (UsePathfinding && Grid != null)
            {
                if (!target.HasValue || _path == null) return;

                Vector2 curP = transform.position;
                float speedMulP = health != null ? health.MovementSpeedMultiplier() : 1f;
                var abilP = GetComponent<PawnAbilities>();
                if (abilP != null) speedMulP *= abilP.moveSpeedMul;
                if (IsOnFloor(curP)) speedMulP *= FloorEntity.MoveSpeedMul;
                if (DoorEntity.IsInsideDoor(curP))
                {
                    speedMulP *= DoorEntity.PassMul;
                    var doorHitsP = Physics2D.OverlapBoxAll(curP, Vector2.one * 0.3f, 0f);
                    foreach (var h in doorHitsP)
                    {
                        var d = h != null ? h.GetComponent<DoorEntity>() : null;
                        if (d != null) { d.NotifyPassing(); break; }
                    }
                }
                AdvanceAlongPath(stats.moveSpeed * speedMulP * Time.deltaTime);
                return;
            }

            // ===== OLD fallback branch (flag OFF) =====
            //  #199 B2: the live Game scene flips UsePathfinding ON at bootstrap
            //  (TilemapStaticRefInit), so this branch is NOT reachable in the
            //  shipped game — A* owns movement now.  It survives ONLY as the
            //  deterministic point-lerp the isolated V-test scene relies on
            //  (V28 movement-tick, the DoorEntity pass tests) where no bootstrap
            //  runs and the flag stays at its source default (false).
            //
            //  DELETED in B2 (real pathing replaces both, operator "real pathing
            //  owns movement"): the I19 perpendicular-nudge unstuck block and the
            //  x/y axis-slide obstacle dodge.  Both were band-aids for the lack of
            //  pathfinding; A* + LastPathFailed cover every case they patched.
            //  What stays: ClampToWorld, the Water/Rock IsBlockedAt stop, speed
            //  multipliers, arrive logic — cheap, correct, still wanted.
            if (!target.HasValue) return;

            Vector2 cur = transform.position;
            // Step45: 다리 다친 만큼 속도 감소
            float speedMul = health != null ? health.MovementSpeedMultiplier() : 1f;
            // #120 - PawnAbilities move speed multiplier
            var _abil = GetComponent<PawnAbilities>();
            if (_abil != null) speedMul *= _abil.moveSpeedMul;
            // #157 - wiki: 바닥 위 pawn 은 이동 속도 보너스 (paved tile 50% 근사).
            //  per-frame OverlapBox 인 점은 비싸지만 (#104 audit) tiny radius 라 ok.
            if (IsOnFloor(cur)) speedMul *= FloorEntity.MoveSpeedMul;
            // #171 - 문 통과 중 감속 (wiki: 0.45s pass-through delay).
            //  PassMul=0.65 이므로 평균 속도 65% (~40% 더 시간 소요).
            if (DoorEntity.IsInsideDoor(cur))
            {
                speedMul *= DoorEntity.PassMul;
                // 가까운 door 에 NotifyPassing - 시각 피드백 (밝아짐)
                var doorHits = Physics2D.OverlapBoxAll(cur, Vector2.one * 0.3f, 0f);
                foreach (var h in doorHits)
                {
                    var d = h != null ? h.GetComponent<DoorEntity>() : null;
                    if (d != null) { d.NotifyPassing(); break; }
                }
            }
            // Step 81: target 도 맵 안쪽으로 강제.  target 자체가 호수/바위면 stop.
            Vector2 clampedTarget = ClampToWorld(target.Value);
            if (IsBlockedAt(clampedTarget))
            {
                target = null;
                return;
            }
            Vector2 next = Vector2.MoveTowards(cur, clampedTarget, stats.moveSpeed * speedMul * Time.deltaTime);
            next = ClampToWorld(next);
            // #199 B2: x/y axis-slide obstacle dodge DELETED (was a no-pathfinding
            //  band-aid).  In this fallback branch we simply stop if the next step
            //  would enter an obstacle — the live game never hits this (A* routes
            //  around obstacles instead).  Keeps the cheap Water/Rock safety stop.
            if (IsBlockedAt(next))
            {
                target = null;
                return;
            }
            transform.position = new Vector3(next.x, next.y, transform.position.z);

            if (Vector2.Distance(next, target.Value) <= stats.arriveDistance)
            {
                target = null;
            }
        }
    }
}
