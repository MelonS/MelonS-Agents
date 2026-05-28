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
    }
}
