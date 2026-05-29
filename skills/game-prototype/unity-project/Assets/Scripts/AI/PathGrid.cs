using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MelonS.GameProto.AI
{
    /// <summary>
    /// #199 B0 — Walkability grid over the 60x60 tile map, RimWorld-style.
    ///
    /// The map is 60x60 cells.  Tilemap cells span x,y ∈ [-30, 29] (see
    /// SceneSetup.Game.Terrain.cs: for (x = -half; x &lt; half; ...) with
    /// half = 30).  Pawns are clamped to world ±29 (PawnMovement.WORLD_MIN/MAX),
    /// so every reachable world position falls inside a tile cell.
    ///
    /// A cell is **blocked** when its terrain tile == WaterTile or RockTile
    /// (reusing PawnMovement.GroundTilemap / WaterTile / RockTile so the grid's
    /// notion of "blocked" is identical to the live movement guard).  Walls
    /// (B3) will be layered on later via Rebuild(); B0 is terrain-only.
    ///
    /// Built ONCE from the tilemap (Rebuild scans 3600 cells — cheap, not
    /// per-frame).  Headless-safe: if the tilemap ref is null every cell is
    /// treated walkable (no throw).
    ///
    /// Cell↔world mapping is consistent with Tilemap.WorldToCell (cellSize 1):
    /// world (wx,wy) → cell (floor(wx), floor(wy)); cell (cx,cy) → world
    /// center (cx+0.5, cy+0.5).
    ///
    /// NOTHING consumes this in B0 (PawnMovement.UsePathfinding == false).
    /// </summary>
    public class PathGrid
    {
        // Cell coordinate bounds (inclusive).  Mirrors SetupTilemap's loop.
        public const int MIN = -30;
        public const int MAX = 29;       // exclusive 30 in the loop → last cell is 29
        public const int SIZE = MAX - MIN + 1;   // 60

        // walkable[x - MIN, y - MIN].  true = passable.
        private readonly bool[,] _walkable = new bool[SIZE, SIZE];

        private readonly Tilemap _ground;
        private readonly TileBase _water;
        private readonly TileBase _rock;

        // #199 B3 — structure blockers (walls).  RimWorld: a wall fully blocks its
        //  cell; pawns route around it.  WallEntity registers its cell on enable
        //  and unregisters on destroy via the static MarkStructureBlocked API.
        //  Reference-COUNTED so overlapping / double-registered walls (e.g. two
        //  walls the same starter cell maps to) clear correctly — the cell only
        //  reopens when the LAST blocker on it is gone.  Folded into walkability by
        //  Rebuild() and by the per-cell RecomputeCell() fast path.  Doors are NOT
        //  registered → door cells stay walkable (pawns path THROUGH them).
        private readonly Dictionary<Vector2Int, int> _structureBlockers
            = new Dictionary<Vector2Int, int>();

        // #199 B3 — grid version counter.  Bumped on EVERY walkability change that
        //  comes from a structure (wall build/destroy → Rebuild / Mark / Unmark).
        //  In-flight pawns cache the version their path was computed against; when
        //  the live grid's Version moves on (a wall changed mid-walk), the pawn
        //  invalidates its cached path and re-paths so it can't walk through a
        //  freshly-built wall or keep routing around a wall that was just removed.
        //  Starts at 1 so a pawn's default cached 0 always looks stale until it
        //  computes a real path (forces a clean first computation).
        public int Version { get; private set; } = 1;

        private void BumpVersion() { Version++; }

        /// <summary>
        /// Build a grid from explicit tilemap refs.  Used by the scene
        /// bootstrap (via PawnMovement statics) and directly by tests.
        /// Any ref may be null (headless) — null tilemap = all-walkable.
        /// </summary>
        public PathGrid(Tilemap ground, TileBase water, TileBase rock)
        {
            _ground = ground;
            _water = water;
            _rock = rock;
            Rebuild();
        }

        /// <summary>
        /// Build a test grid directly from a walkability mask (no tilemap).
        /// mask[x,y] indexed [0,SIZE) maps to cell (x+MIN, y+MIN).  Lets V-tests
        /// construct a known map without AssetDatabase / scene.
        /// </summary>
        public static PathGrid FromMask(bool[,] mask)
        {
            var g = new PathGrid(null, null, null);  // all-walkable baseline
            int w = mask.GetLength(0);
            int h = mask.GetLength(1);
            for (int x = 0; x < SIZE; x++)
                for (int y = 0; y < SIZE; y++)
                    g._walkable[x, y] = (x < w && y < h) ? mask[x, y] : true;
            return g;
        }

        /// <summary>(Re)scan the tilemap into the walkability map.  Cheap full
        /// scan of 3600 cells — call on build/destroy of obstacles, NOT per
        /// frame.  Walls layer in at B3.</summary>
        public void Rebuild()
        {
            for (int x = 0; x < SIZE; x++)
            {
                for (int y = 0; y < SIZE; y++)
                {
                    bool terrainBlocked = false;
                    if (_ground != null)
                    {
                        Vector3Int c = new Vector3Int(x + MIN, y + MIN, 0);
                        TileBase t = _ground.GetTile(c);
                        terrainBlocked = t != null && (t == _water || t == _rock);
                    }
                    // #199 B3 — a cell is walkable iff terrain allows it AND no
                    //  structure (wall) occupies it.  Doors never register here.
                    var cell = new Vector2Int(x + MIN, y + MIN);
                    bool structureBlocked =
                        _structureBlockers.TryGetValue(cell, out int n) && n > 0;
                    _walkable[x, y] = !terrainBlocked && !structureBlocked;
                }
            }
            BumpVersion();
        }

        // #199 B3 — true if a wall blocker is registered on this cell.  Used by
        //  RecomputeCell so terrain-walkable cells under a wall stay blocked.
        private bool HasStructureBlocker(Vector2Int cell)
            => _structureBlockers.TryGetValue(cell, out int n) && n > 0;

        // #199 B3 — terrain-only walkability for one cell (ignores structures).
        private bool TerrainWalkable(Vector2Int cell)
        {
            if (_ground == null) return true;   // headless: nothing blocks
            Vector3Int c = new Vector3Int(cell.x, cell.y, 0);
            TileBase t = _ground.GetTile(c);
            return !(t != null && (t == _water || t == _rock));
        }

        // #199 B3 — recompute a SINGLE cell's walkability from terrain + blockers.
        //  Cheaper than a full Rebuild for the common single-wall build/destroy.
        private void RecomputeCell(Vector2Int cell)
        {
            if (!InBounds(cell)) return;
            _walkable[cell.x - MIN, cell.y - MIN] =
                TerrainWalkable(cell) && !HasStructureBlocker(cell);
        }

        /// <summary>
        /// #199 B3 — register/unregister a structure (wall) blocker on a cell and
        /// update walkability immediately.  Reference-counted: build adds 1,
        /// destroy removes 1; the cell only reopens at count 0.  Bumps Version so
        /// in-flight pawns re-path (item 3).  No-op for out-of-bounds cells.
        /// Operates on a PathGrid INSTANCE; the live game routes through the static
        /// helper on PawnMovement.Grid (see WallEntity).
        /// </summary>
        public void SetStructureBlocked(Vector2Int cell, bool blocked)
        {
            if (!InBounds(cell)) return;
            _structureBlockers.TryGetValue(cell, out int n);
            if (blocked) n++;
            else n = Mathf.Max(0, n - 1);
            if (n > 0) _structureBlockers[cell] = n;
            else _structureBlockers.Remove(cell);
            RecomputeCell(cell);
            BumpVersion();
        }

        /// <summary>
        /// W-M4-01 Lane A / wiki #15 (Deconstruct) — explicit, symmetric named
        /// counterparts to the wall ref-count add/remove, so the deconstruct path
        /// can decrement a cell's wall blocker with intent that reads next to how a
        /// build INCREMENTED it.  These are thin wrappers over SetStructureBlocked
        /// (which is already reference-counted + Version-bumping): BlockWallCell is
        /// what a built wall does (count+1), ReleaseWallCell is what a deconstructed
        /// wall does (count−1, cell reopens at 0).  The live game's normal wall
        /// lifecycle still flows through WallEntity.Start/OnDestroy →
        /// PawnMovement.RegisterWallCell / Grid.SetStructureBlocked — these names
        /// exist so the #15 removal flow and any test can express the decrement
        /// directly and provably balanced against the build-time increment.
        /// </summary>
        public void BlockWallCell(Vector2Int cell)   => SetStructureBlocked(cell, true);
        public void ReleaseWallCell(Vector2Int cell) => SetStructureBlocked(cell, false);

        public bool InBounds(Vector2Int cell)
            => cell.x >= MIN && cell.x <= MAX && cell.y >= MIN && cell.y <= MAX;

        /// <summary>True if the cell is in-bounds and not blocked.
        /// Out-of-bounds is treated as NOT walkable (the map edge is a wall).</summary>
        public bool IsWalkable(Vector2Int cell)
        {
            if (!InBounds(cell)) return false;
            return _walkable[cell.x - MIN, cell.y - MIN];
        }

        /// <summary>Test/edit helper — force a cell's walkability (used by
        /// tests to drop obstacle lines).</summary>
        public void SetWalkable(Vector2Int cell, bool walkable)
        {
            if (!InBounds(cell)) return;
            _walkable[cell.x - MIN, cell.y - MIN] = walkable;
            BumpVersion();
        }

        // ---- world ↔ cell conversion (matches Tilemap.WorldToCell, cellSize 1)

        public static Vector2Int WorldToCell(Vector2 world)
            => new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

        /// <summary>World-space CENTER of a cell (pawn stands at cell center).</summary>
        public static Vector2 CellToWorld(Vector2Int cell)
            => new Vector2(cell.x + 0.5f, cell.y + 0.5f);

        // ---- #199 C1 — RimWorld-style adjacent work-cell selection ------------
        //
        //  RimWorld pawns do NOT stand ON the thing they work; they stand in a
        //  WALKABLE cell ADJACENT (8-neighbour, diagonals included) to the target
        //  and reach over.  These helpers return the world CENTER of the best such
        //  adjacent cell so every worker (Chop/Gather/Build/Mine/Cook/Haul/...)
        //  uses ONE shared rule instead of copy-pasting it 7×.
        //
        //  Selection rule (cheap variant per plan): among all walkable 8-neighbour
        //  cells of the target footprint, pick the one whose CENTER is closest to
        //  the pawn's current position.  This minimises the pawn's remaining walk
        //  AND naturally lands it on the near side of the object (so it doesn't
        //  walk the long way round to a far neighbour).  We avoid running a full
        //  A* per candidate (R-4 cost) — the nearest walkable neighbour is the
        //  RimWorld-cheap heuristic and A* from the pawn to that cell still routes
        //  around any obstacle in between.
        //
        //  Corner-cut safety: a DIAGONAL stand cell is only offered if the pawn
        //  could actually step onto it without squeezing between two blocked
        //  corners relative to the TARGET — i.e. at least one of the two cells
        //  orthogonally shared between the diagonal neighbour and the target cell
        //  is walkable.  Mirrors AStar's no-corner-cut rule so the chosen stand
        //  cell is genuinely reachable to reach-over the target.

        // 8-neighbour offsets (cardinals first, then diagonals — same order/intent
        //  as AStar.Dirs so adjacency is consistent across the codebase).
        private static readonly Vector2Int[] Neighbours8 =
        {
            new Vector2Int( 1,  0), new Vector2Int(-1,  0),
            new Vector2Int( 0,  1), new Vector2Int( 0, -1),
            new Vector2Int( 1,  1), new Vector2Int( 1, -1),
            new Vector2Int(-1,  1), new Vector2Int(-1, -1),
        };

        /// <summary>
        /// #199 C1 — covered-cell set for a target whose TRANSFORM sits at
        /// <paramref name="targetWorld"/> with the given <paramref name="footprint"/>.
        ///
        /// Convention MATCHES BuildManager placement: a w×h entity is placed at the
        /// geometric centre (cx + w/2, cy + h/2) of its w×h cell block, so the
        /// covered cells are (cx+i, cy+j) for i∈[0,w), j∈[0,h).  Recovering cx from
        /// the centre: cx = floor(targetWorld.x − w/2 + 0.5).  For a 1×1 target this
        /// reduces to floor(targetWorld.x) == WorldToCell — so a tree/bush/stove at
        /// any sub-cell position maps to the cell it stands in, while a 1×2 bed at
        /// (cx+0.5, cy+1.0) recovers exactly {(cx,cy),(cx,cy+1)}.
        /// </summary>
        public static void CoveredCells(Vector2 targetWorld, Vector2Int footprint, HashSet<Vector2Int> into)
        {
            into.Clear();
            int fw = Mathf.Max(1, footprint.x);
            int fh = Mathf.Max(1, footprint.y);
            int xLo = Mathf.FloorToInt(targetWorld.x - fw * 0.5f + 0.5f);
            int yLo = Mathf.FloorToInt(targetWorld.y - fh * 0.5f + 0.5f);
            for (int dx = 0; dx < fw; dx++)
                for (int dy = 0; dy < fh; dy++)
                    into.Add(new Vector2Int(xLo + dx, yLo + dy));
        }

        /// <summary>
        /// #199 C1 — single-cell target by CELL (test/direct use).  Finds the best
        /// WALKABLE 8-neighbour cell of <paramref name="targetCell"/> for a pawn at
        /// <paramref name="fromPos"/>.  Outputs the stand cell's world CENTRE.
        /// Returns false when NO neighbour is walkable (unreachable).
        /// </summary>
        public bool TryGetAdjacentStandCell(Vector2Int targetCell, Vector2 fromPos, out Vector2 standWorld)
            => TryGetAdjacentStandCell(CellToWorld(targetCell), new Vector2Int(1, 1), fromPos, out standWorld);

        /// <summary>
        /// #199 C1 — primary entry.  Find the best WALKABLE cell ADJACENT
        /// (8-neighbour) to the target's footprint for a pawn at
        /// <paramref name="fromPos"/> to stand in and reach over, RimWorld-style.
        /// <paramref name="targetWorld"/> is the target transform position,
        /// <paramref name="footprint"/> its size in cells (1×1 default; 1×2 bed,
        /// 2×1 bench).  "Adjacent" = adjacent to ANY footprint cell.  Cells INSIDE
        /// the footprint are never offered (stand beside, not on).  Picks the
        /// walkable adjacent cell whose centre is closest to <paramref name="fromPos"/>.
        /// Returns false (object unreachable) if no adjacent cell is walkable.
        /// </summary>
        // Reusable scratch set — avoids a per-frame allocation (R-4 GC hygiene);
        //  Unity is single-threaded so a shared static is safe here.
        private static readonly HashSet<Vector2Int> _coverScratch = new HashSet<Vector2Int>();
        public bool TryGetAdjacentStandCell(
            Vector2 targetWorld, Vector2Int footprint, Vector2 fromPos, out Vector2 standWorld)
        {
            var covered = _coverScratch;
            CoveredCells(targetWorld, footprint, covered);

            // Fallback output: target centre (only used when found==false, which
            //  callers treat as unreachable anyway).
            standWorld = targetWorld;

            bool found = false;
            float bestSq = float.MaxValue;
            Vector2Int bestCell = default;

            foreach (var cc in covered)
            {
                for (int d = 0; d < Neighbours8.Length; d++)
                {
                    Vector2Int step = Neighbours8[d];
                    Vector2Int nc = cc + step;
                    if (covered.Contains(nc)) continue;     // inside footprint → not a stand cell
                    if (!IsWalkable(nc)) continue;          // out-of-bounds counts as not walkable

                    // Corner-cut safety for a diagonal neighbour of THIS footprint
                    //  cell: at least one shared orthogonal cell must be walkable
                    //  (or be part of the footprint, which the pawn reaches over),
                    //  else the pawn can't legally step from the gap into nc.
                    bool diagonal = step.x != 0 && step.y != 0;
                    if (diagonal)
                    {
                        Vector2Int side1 = new Vector2Int(cc.x + step.x, cc.y);
                        Vector2Int side2 = new Vector2Int(cc.x, cc.y + step.y);
                        bool s1 = IsWalkable(side1) || covered.Contains(side1);
                        bool s2 = IsWalkable(side2) || covered.Contains(side2);
                        if (!s1 && !s2) continue;
                    }

                    float sq = ((Vector2)CellToWorld(nc) - fromPos).sqrMagnitude;
                    if (sq < bestSq)
                    {
                        bestSq = sq;
                        bestCell = nc;
                        found = true;
                    }
                }
            }

            if (found) standWorld = CellToWorld(bestCell);
            return found;
        }

        // ---- #199 C2 — reservable stand-cell selection -----------------------
        //
        //  RimWorld reserves the cell a worker stands in so two pawns don't path to
        //  the same adjacent cell.  TryGetAdjacentStandCell above always returns the
        //  single nearest walkable neighbour; for C2 we need to be able to SKIP a
        //  neighbour another pawn already reserved and fall back to the next-nearest
        //  free one.  This overload returns the chosen stand CELL (not just world)
        //  and lets the caller pass a predicate that rejects cells reserved by
        //  others.  The predicate stays out of PathGrid (which is pure grid data) —
        //  the worker supplies the ReservationManager check.

        /// <summary>
        /// #199 C2 — like TryGetAdjacentStandCell but returns the chosen stand CELL
        /// and skips any candidate for which <paramref name="rejectCell"/> returns
        /// true (e.g. "reserved by another pawn").  Picks the nearest ACCEPTED
        /// walkable adjacent cell to <paramref name="fromPos"/>.  Returns false when
        /// no accepted walkable adjacent cell exists (fully blocked OR every free
        /// neighbour is already reserved → caller waits / re-picks).
        /// </summary>
        public bool TryGetAdjacentStandCell(
            Vector2 targetWorld, Vector2Int footprint, Vector2 fromPos,
            System.Func<Vector2Int, bool> rejectCell, out Vector2Int standCell)
        {
            var covered = _coverScratch;
            CoveredCells(targetWorld, footprint, covered);

            standCell = default;
            bool found = false;
            float bestSq = float.MaxValue;

            foreach (var cc in covered)
            {
                for (int d = 0; d < Neighbours8.Length; d++)
                {
                    Vector2Int step = Neighbours8[d];
                    Vector2Int nc = cc + step;
                    if (covered.Contains(nc)) continue;
                    if (!IsWalkable(nc)) continue;
                    if (rejectCell != null && rejectCell(nc)) continue;   // C2: reserved by other

                    bool diagonal = step.x != 0 && step.y != 0;
                    if (diagonal)
                    {
                        Vector2Int side1 = new Vector2Int(cc.x + step.x, cc.y);
                        Vector2Int side2 = new Vector2Int(cc.x, cc.y + step.y);
                        bool s1 = IsWalkable(side1) || covered.Contains(side1);
                        bool s2 = IsWalkable(side2) || covered.Contains(side2);
                        if (!s1 && !s2) continue;
                    }

                    float sq = ((Vector2)CellToWorld(nc) - fromPos).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; standCell = nc; found = true; }
                }
            }
            return found;
        }
    }
}
