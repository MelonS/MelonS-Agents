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

        // 운영자 피드백 #108 - tile 맵 안쪽으로 강제 clamp.  #235 90x90 → ±44
        //  (PathGrid.MAX=44 와 동기화. 타일 영역 밖으로는 절대 못 나감.)
        public static readonly Vector2 WORLD_MIN = new Vector2(-44f, -44f);
        public static readonly Vector2 WORLD_MAX = new Vector2( 44f,  44f);

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

        // #199 B3 — wall→grid coupling.  WallEntity calls these on build/destroy so
        //  the live PathGrid marks/clears its cell.  Null-safe: if the grid hasn't
        //  been built yet (or in a headless V-scene without bootstrap) the call is a
        //  no-op — the wall simply isn't a path blocker there, which is correct for
        //  those contexts.  Reference-counted inside PathGrid, so a cell two walls
        //  map to reopens only when both are gone.  Cell is derived the SAME way
        //  pawns derive their cell (WorldToCell of the world position) so a wall and
        //  the pawns share one coordinate convention.
        public static void RegisterWallCell(Vector2 worldPos)
        {
            if (Grid == null) return;
            Grid.SetStructureBlocked(PathGrid.WorldToCell(worldPos), true);
        }

        public static void UnregisterWallCell(Vector2 worldPos)
        {
            if (Grid == null) return;
            Grid.SetStructureBlocked(PathGrid.WorldToCell(worldPos), false);
        }

        // #201 — the reference sim eject-on-block.  When a solid structure (wall) COMPLETES
        //  on a cell, any pawn already standing in that cell would be sealed inside
        //  an impassable cell (AStar refuses a blocked START cell → the pawn can
        //  never path out, and an idle pawn never even calls SetTarget).  the reference sim
        //  PUSHES such a pawn out to the nearest open cell.  WallEntity.Start()
        //  calls this AFTER it registers the cell as blocked, so TryNearestWalkable
        //  sees the cell as occupied and lands the pawn on a genuinely-open
        //  neighbour.  Wall-build is a rare event (not per-frame), so the
        //  FindObjectsByType scan here is fine (lesson #4: it is NOT in a tight loop).
        //
        //  Edge case — a pawn fully enclosed by walls (no walkable neighbour within
        //  the bounded ring search): TryNearestWalkable returns false; we LEAVE the
        //  pawn where it is (can't push it into a wall) and clear its path so it
        //  doesn't keep trying to walk through. The continuous safety net in Update
        //  will retry once a neighbouring cell opens. No crash, no infinite loop.
        public static void EjectPawnsFromCell(Vector2 cellWorld)
        {
            if (Grid == null) return;
            Vector2Int blockedCell = PathGrid.WorldToCell(cellWorld);
            var pawns = Object.FindObjectsByType<PawnMovement>(FindObjectsSortMode.None);
            foreach (var pm in pawns)
            {
                if (pm == null) continue;
                Vector2Int pawnCell = PathGrid.WorldToCell(pm.transform.position);
                if (pawnCell != blockedCell) continue;   // not on the blocked cell
                pm.EjectToNearestWalkable(blockedCell);
            }
        }

        // #201 — push THIS pawn out of a now-blocked cell to the nearest open cell.
        //  Instance method so it can also serve the Update safety net (Part 2).
        //  Moves the transform to the open cell's CENTRE and clears the cached path
        //  so the pawn re-paths cleanly on its next SetTarget (it won't try to walk
        //  back through the wall).  If genuinely sealed in, clears the path but
        //  leaves the transform (documented enclosed-pawn edge case).
        private void EjectToNearestWalkable(Vector2Int fromCell)
        {
            if (Grid == null) return;
            if (TryNearestWalkable(fromCell, out Vector2Int open))
            {
                Vector2 dest = PathGrid.CellToWorld(open);
                transform.position = new Vector3(dest.x, dest.y, transform.position.z);
            }
            // else: sealed in — can't eject into a wall; leave position, just clear path.
            _path = null;
            _pathIndex = 0;
            _pathGridVersion = -1;
            target = null;
        }

        // #199 C1 — shared colony-sim-style "stand adjacent to the work object"
        //  helper for all workers (Chop/Gather/Build/Mine/Cook/Haul/...).  Returns
        //  the world position the worker should WALK TO (an adjacent walkable cell
        //  centre next to the target footprint) and whether such a cell exists.
        //
        //  Null-safe: when the live Grid hasn't been built (headless V-scenes with
        //  no bootstrap, or pathfinding off) we fall back to the target's OWN
        //  position and return true — i.e. old "walk onto it" behaviour, so those
        //  contexts don't regress.  When the Grid exists but the target is fully
        //  enclosed (no walkable neighbour) we return false → caller treats the
        //  target as unreachable, same as LastPathFailed.
        //
        //  targetWorld = the work object's transform position.
        //  footprint   = its cell size (1×1 default; 1×2 bed / 2×1 bench).
        public static bool TryGetWorkStandPos(Vector2 targetWorld, Vector2Int footprint,
            Vector2 fromPos, out Vector2 standWorld)
        {
            if (Grid == null || !UsePathfinding)
            {
                standWorld = targetWorld;   // no grid → legacy "walk onto target"
                return true;
            }
            return Grid.TryGetAdjacentStandCell(targetWorld, footprint, fromPos, out standWorld);
        }

        // 1×1 convenience overload (the common case).
        public static bool TryGetWorkStandPos(Vector2 targetWorld, Vector2 fromPos, out Vector2 standWorld)
            => TryGetWorkStandPos(targetWorld, new Vector2Int(1, 1), fromPos, out standWorld);

        // #199 C2 — RESERVED stand-cell helper (fixes C1's per-frame recompute
        //  caveat AND prevents two pawns targeting the same adjacent cell).  A
        //  worker calls this ONCE on commit and then every frame; it:
        //    1. If a cell is already locked (lockedCell != INVALID), still walkable,
        //       and still reserved by THIS claimant → reuse it (no recompute, no
        //       re-reserve scan).  This is the per-frame fast path that replaces
        //       C1's recompute-every-frame.
        //    2. Otherwise pick the nearest walkable adjacent cell NOT reserved by
        //       another pawn, reserve it for the claimant, store it in lockedCell,
        //       and return its world centre.
        //  Returns false when the target is unreachable (no walkable neighbour) OR
        //  every free neighbour is reserved by others (caller treats as "wait/
        //  re-pick", same as unreachable for the give-up path).
        //
        //  Null-safe / headless: when no Grid or pathfinding off, falls back to the
        //  target's own position (legacy walk-onto) and does NOT reserve a cell —
        //  isolated V-test scenes without a bootstrap keep working unchanged.
        public static readonly Vector2Int INVALID_CELL = new Vector2Int(int.MinValue, int.MinValue);

        // #199 C2 — "has the pawn ARRIVED at its reserved stand cell?"  Because the
        //  stand cell is now LOCKED (not recomputed every frame), AdvanceAlongPath
        //  stops the pawn within arriveDistance of the cell CENTRE — which can leave
        //  it ~arriveDistance short, i.e. at the cell edge.  A pure dist-to-target
        //  check then sits exactly on the work-range boundary and flaps (the reference sim:
        //  the pawn IS in its reserved work cell, so it should work regardless of the
        //  sub-cell offset).  Workers treat "standing in the reserved cell" as
        //  in-range.  Returns false for an invalid lock (legacy dist path still
        //  applies, e.g. headless V-scenes / moving targets with no stand cell).
        public bool AtStandCell(Vector2Int standCell)
        {
            if (standCell.x == INVALID_CELL.x) return false;
            return PathGrid.WorldToCell(transform.position) == standCell;
        }

        public static bool TryReserveWorkStandPos(
            Vector2 targetWorld, Vector2Int footprint, Vector2 fromPos,
            GameObject claimant, ref Vector2Int lockedCell, out Vector2 standWorld)
        {
            if (Grid == null || !UsePathfinding)
            {
                standWorld = targetWorld;   // legacy: no grid → walk onto target
                return true;
            }

            // Fast path — already locked, still valid, still ours, AND still adjacent
            //  to the CURRENT target.  #218 운영자 fb "목재 순간이동" root cause: a hauler
            //  reuses its locked stand cell across a TARGET CHANGE (pile→stockpile).  The
            //  old cell sits next to the pile, not the stockpile, yet the fast path kept
            //  it → AtStandCell() instantly true at the new target → pawn "arrives" w/o
            //  walking → deposits wood at the stockpile while standing at the tree
            //  (=teleport).  Gate the fast path on the locked cell still being adjacent
            //  to targetWorld; otherwise fall through and pick a fresh cell near it.
            if (lockedCell.x != INVALID_CELL.x
                && Grid.IsWalkable(lockedCell)
                && !ReservationManager.IsCellReservedByOther(lockedCell, claimant)
                && DistanceToFootprint(targetWorld, footprint, PathGrid.CellToWorld(lockedCell)) <= 1.2f)  // #275 1.6→1.2 직각 인접만 (순간이동 재발 방지)
            {
                // Re-assert ownership (idempotent) so a sweep can't drop it.
                ReservationManager.TryReserveCell(lockedCell, claimant);
                standWorld = PathGrid.CellToWorld(lockedCell);
                return true;
            }

            // Pick nearest free (not reserved-by-other) walkable adjacent cell.
            GameObject c = claimant;
            bool found = Grid.TryGetAdjacentStandCell(
                targetWorld, footprint, fromPos,
                cell => ReservationManager.IsCellReservedByOther(cell, c),
                out Vector2Int chosen);

            if (!found)
            {
                // Release any previously-locked cell — we couldn't get a stand cell.
                if (lockedCell.x != INVALID_CELL.x)
                {
                    ReservationManager.ReleaseCell(lockedCell, claimant);
                    lockedCell = INVALID_CELL;
                }
                standWorld = targetWorld;
                return false;
            }

            // Moving to a new locked cell → release the old one first.
            if (lockedCell.x != INVALID_CELL.x && lockedCell != chosen)
                ReservationManager.ReleaseCell(lockedCell, claimant);

            ReservationManager.TryReserveCell(chosen, claimant);
            lockedCell = chosen;
            standWorld = PathGrid.CellToWorld(chosen);
            return true;
        }

        // #199 C1 — "in range to work?" distance for a MULTI-CELL target.  A pawn
        //  standing adjacent to one footprint cell can be far from the footprint's
        //  transform CENTRE (e.g. a 1×2 bed: adjacent-to-far-cell ≈ 1.8 from
        //  centre).  Measuring to the NEAREST footprint cell centre instead keeps
        //  the in-range constant honest (~1.5 covers diagonal adjacency to the
        //  nearest cell regardless of footprint size).  For a 1×1 target this is
        //  identical to distance-to-centre.  Returns straight-line world distance
        //  to the closest covered-cell centre.
        // Reusable scratch set so this allocation-free per call (R-4 GC hygiene).
        private static readonly System.Collections.Generic.HashSet<Vector2Int> _footprintScratch
            = new System.Collections.Generic.HashSet<Vector2Int>();
        public static float DistanceToFootprint(Vector2 targetWorld, Vector2Int footprint, Vector2 fromPos)
        {
            // 1×1 short-circuit: distance to the target centre directly (matches
            //  legacy dist-to-transform for trees/bushes at sub-cell positions).
            if (footprint.x <= 1 && footprint.y <= 1)
                return Vector2.Distance(targetWorld, fromPos);
            PathGrid.CoveredCells(targetWorld, footprint, _footprintScratch);
            float best = float.MaxValue;
            foreach (var cell in _footprintScratch)
            {
                Vector2 c = PathGrid.CellToWorld(cell);
                float sq = (c - fromPos).sqrMagnitude;
                if (sq < best) best = sq;
            }
            return Mathf.Sqrt(best);
        }

        public static bool IsBlockedAt(Vector2 worldPos)
        {
            if (GroundTilemap == null) return false;
            Vector3Int cell = GroundTilemap.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0));
            TileBase t = GroundTilemap.GetTile(cell);
            return t != null && (t == WaterTile || t == RockTile);
        }

        // #199 C3 — find the nearest walkable cell to a (possibly blocked) cell,
        //  searching in rings of growing Chebyshev radius.  Used as the escape hatch
        //  when a pawn is caught on a freshly-completed wall cell.  Bounded search
        //  (radius ≤ 8) so a pawn truly sealed in returns false (give up).
        private static bool TryNearestWalkable(Vector2Int from, out Vector2Int result)
        {
            result = from;
            if (Grid == null) return false;
            for (int r = 1; r <= 8; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        // only the outer ring of this radius
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var c = new Vector2Int(from.x + dx, from.y + dy);
                        if (Grid.IsWalkable(c)) { result = c; return true; }
                    }
                }
            }
            return false;
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

        // #199 B3 — grid version the cached path was computed against.  When the
        //  live Grid.Version moves on (a wall was built/destroyed mid-walk), the
        //  cached path is STALE and must be recomputed so the pawn respects the new
        //  obstacle / opened gap.  SetTarget's recompute-skip checks this; Update
        //  also force-repaths once when it detects a version change, so a pawn under
        //  a one-shot SetTarget (manual move) re-routes too — not just workers that
        //  re-call SetTarget every frame.
        private int _pathGridVersion = -1;

        // #199 B1 — the reference sim "destination unreachable" signal.  Set true when an
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
        // 운영자 2026-06-02: 떠도는중(idle wander) 시 PawnUtilityAI 가 0.5 로 낮춰 절반 속도.
        //  실제 작업 이동은 1.0.  모든 이동 속도 계산에 곱해진다.
        public float MoveSpeedScale = 1f;

        // 운영자 요청 (우클릭 이동 경로 표시) — READ-ONLY exposure of the live A*
        //  path for PathLineRenderer.  Returns the REMAINING cell-centre world
        //  points the pawn still has to walk (from the pawn's current position up to
        //  the goal), or null when there is no active path.  The pawn's current
        //  position is prepended so the drawn line starts at the pawn, not at the
        //  already-reached previous waypoint (colony-sim-style "line from feet to dest").
        //  Allocation note: this allocates only when a renderer asks for it — the
        //  renderer caches the array and only re-fetches when the path changes, so
        //  this is NOT a per-frame allocation in the tight loop.  No state mutated.
        public bool TryGetRemainingPathWorld(System.Collections.Generic.List<Vector2> outPoints)
        {
            if (outPoints == null) return false;
            outPoints.Clear();
            if (_path == null || _pathIndex >= _path.Count) return false;

            // Start at the pawn's feet so the polyline visibly originates at the pawn.
            outPoints.Add(transform.position);
            for (int i = _pathIndex; i < _path.Count; i++)
                outPoints.Add(PathGrid.CellToWorld(_path[i]));
            return outPoints.Count >= 2;
        }

        // Lightweight change-detection token for the renderer's cache: changes when
        //  the path is rebuilt (goal cell or grid version) or the pawn advances a
        //  waypoint.  The renderer compares this each frame and only rebuilds its
        //  LineRenderer point array when it differs — keeps the hot path allocation-free.
        public int PathVersionToken => _path == null ? 0
            : (_pathGoalCell.x * 73856093) ^ (_pathGoalCell.y * 19349663)
              ^ (_pathIndex * 83492791) ^ (_path.Count << 3) ^ _pathGridVersion;

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
                //  #199 B3: ALSO recompute when the grid Version changed (a wall was
                //  built/destroyed) — a same-goal path computed before the change is
                //  stale and could walk through a brand-new wall.  This is the
                //  in-flight invalidation (plan item 3).
                if (_path != null && _pathIndex < _path.Count
                    && _pathGoalCell == goalCell
                    && _pathGridVersion == Grid.Version)
                {
                    target = clamped;   // refresh world target (visual), keep path
                    return;
                }

                Vector2Int startCell = PathGrid.WorldToCell(transform.position);
                // #199 C3 (item 3) — a wall blueprint may be placed on a cell a pawn
                //  is standing on (the reference sim allows it; the pawn walks off).  If the
                //  wall COMPLETES before the pawn moved, the pawn's start cell becomes
                //  blocked and AStar would refuse to path (it rejects a blocked start)
                //  → trapped.  Escape hatch: when the start cell is no longer walkable,
                //  path from the nearest walkable neighbour instead so the pawn can
                //  step OUT of the wall it was caught in rather than freezing.
                if (!Grid.IsWalkable(startCell))
                {
                    Vector2Int escape;
                    if (TryNearestWalkable(startCell, out escape))
                        startCell = escape;
                    // else: genuinely sealed in on all sides → falls through to the
                    //  null-path give-up below (LastPathFailed), same as the reference sim.
                }
                var path = AStar.FindPath(Grid, startCell, goalCell);
                if (path == null || path.Count == 0)
                {
                    // the reference sim "destination unreachable" — give up the target.
                    _path = null;
                    _pathIndex = 0;
                    target = null;
                    LastPathFailed = true;
                    return;
                }

                _path = path;
                _pathIndex = 0;
                _pathGoalCell = goalCell;
                _pathGridVersion = Grid.Version;
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
            _pathGridVersion = -1;
        }

        /// <summary>
        /// #199 B1 — one tick of A* path-following, factored out so V-tests can
        /// drive it deterministically without a full scene/Update loop.  Moves
        /// the transform toward the current waypoint's cell center by
        /// <paramref name="maxDelta"/> world units (already speed-scaled by the
        /// caller), advances the waypoint index on arrival, and clears the target
        /// (returns true) when the final waypoint is reached.  No-op when there is
        /// no live path.  Smooth lerp between cell centers (the reference sim glide).
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
                // #201 — continuous safety net (defense in depth for the eject-on-
                //  block fix).  If this pawn's CURRENT cell is not walkable — for ANY
                //  reason (wall completed under it, future cause) and regardless of
                //  whether it has a target — push it toward the nearest open cell.
                //  Cheap: the IsWalkable check is O(1) and the rare-event eject only
                //  runs when actually on a blocked cell (normally never).  Engages
                //  ONLY when standing on a blocked cell, so it never fights the normal
                //  path-follow below; once on an open cell, normal behaviour resumes.
                Vector2Int myCell = PathGrid.WorldToCell(transform.position);
                if (!Grid.IsWalkable(myCell))
                {
                    if (TryNearestWalkable(myCell, out Vector2Int open))
                    {
                        Vector2 dest = PathGrid.CellToWorld(open);
                        float speed = stats.moveSpeed * MoveSpeedScale
                            * (health != null ? health.MovementSpeedMultiplier() : 1f);
                        var ab = GetComponent<PawnAbilities>();
                        if (ab != null) speed *= ab.moveSpeedMul;
                        Vector2 step = Vector2.MoveTowards(transform.position, dest, speed * Time.deltaTime);
                        transform.position = new Vector3(step.x, step.y, transform.position.z);
                    }
                    // else: enclosed — no open neighbour; wait (do nothing) until one
                    //  opens.  Don't path-follow this frame (we're inside a wall).
                    return;
                }

                if (!target.HasValue || _path == null) return;

                // #199 B3 — in-flight invalidation (plan item 3).  If a wall was
                //  built/destroyed since this path was computed, the cached path is
                //  stale.  Re-path to the SAME world target through SetTarget (which
                //  recomputes because the version moved).  Covers the one-shot
                //  SetTarget case (manual move) — workers re-call SetTarget every
                //  frame and get the same effect via the recompute-skip guard.
                if (_pathGridVersion != Grid.Version)
                {
                    Vector2 reTarget = target.Value;
                    SetTarget(reTarget);
                    // If the rebuild walled us off, SetTarget cleared the target →
                    //  LastPathFailed is now set; bail this frame.
                    if (!target.HasValue || _path == null) return;
                }

                Vector2 curP = transform.position;
                float speedMulP = MoveSpeedScale * (health != null ? health.MovementSpeedMultiplier() : 1f);
                var abilP = GetComponent<PawnAbilities>();
                if (abilP != null) speedMulP *= abilP.moveSpeedMul;
                // #157 / W-M4-05 #21 - 바닥 위 이동 보너스.  BonusAt 가 해당 cell 의
                //  가장 높은 FloorEntity.MoveBonus 를 돌려줌 (wood 1.30x, stone 1.50x).
                //  바닥 없으면 1.0 → 보너스 없음.  IsOnFloor 와 동일한 tiny OverlapBox.
                speedMulP *= FloorEntity.BonusAt(curP);
                if (DoorEntity.IsInsideDoor(curP))
                {
                    // W-M6-04 (B5) — per-instance pass mul: plain door 0.65,
                    //  autodoor ≈0.95 (slows less = faster cross).  PassMulAt
                    //  returns 0.65 for a plain door, so this is identical to the
                    //  former `*= DoorEntity.PassMul` const for every plain door.
                    speedMulP *= DoorEntity.PassMulAt(curP);
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
            float speedMul = MoveSpeedScale * (health != null ? health.MovementSpeedMultiplier() : 1f);
            // #120 - PawnAbilities move speed multiplier
            var _abil = GetComponent<PawnAbilities>();
            if (_abil != null) speedMul *= _abil.moveSpeedMul;
            // #157 / W-M4-05 #21 - wiki: 바닥 위 pawn 은 이동 속도 보너스 (paved 50% 근사).
            //  BonusAt 가 cell 의 최고 FloorEntity.MoveBonus 반환 (wood 1.30x / stone 1.50x),
            //  바닥 없으면 1.0.  per-frame OverlapBox 비용은 tiny radius 라 ok (#104 audit).
            speedMul *= FloorEntity.BonusAt(cur);
            // #171 - 문 통과 중 감속 (wiki: 0.45s pass-through delay).
            //  PassMul=0.65 이므로 평균 속도 65% (~40% 더 시간 소요).
            if (DoorEntity.IsInsideDoor(cur))
            {
                // W-M6-04 (B5) — per-instance pass mul (see UsePathfinding branch
                //  above): plain door 0.65, autodoor ≈0.95.  PassMulAt returns
                //  0.65 for a plain door = identical to the former const.
                speedMul *= DoorEntity.PassMulAt(cur);
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
