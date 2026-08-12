using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto.AI
{
    /// <summary>
    /// #199 B0 — A* pathfinder over PathGrid, modeled on the reference sim's grid pathing.
    ///
    /// genre fidelity RULES (operator: "최대한 레퍼런스 콜로니심와 같은 방식으로"):
    ///  - 8-directional movement.
    ///  - Integer move cost: 10 cardinal, 14 diagonal (~ 1.414 scaled ×10).
    ///    Integer costs avoid float drift so ties resolve deterministically.
    ///  - Heuristic: octile distance, h = 10*(max-min) + 14*min where
    ///    min=min(dx,dy), max=max(dx,dy).  This matches the move costs exactly,
    ///    so it is ADMISSIBLE (never overestimates) → optimal paths.
    ///  - NO diagonal corner-cutting: a diagonal step A→B is allowed only if
    ///    BOTH orthogonally-adjacent shared cells (the two cells you'd "clip")
    ///    are walkable.  the reference sim forbids squeezing between two blocked corners.
    ///  - Node cap (default 4000 expanded nodes): a pathological/unreachable
    ///    request returns "no path" instead of hanging the frame.
    ///  - Pure function.  No MonoBehaviour, no persistent static state.
    ///    Deterministic for a given grid + endpoints (tie-break by lower f,
    ///    then by insertion order via a stable open-set scan).
    ///
    /// Result: cell path INCLUDING start and goal (start at [0], goal at [last]).
    /// Returns null when goal is unreachable, out of bounds, blocked, or the
    /// node cap is hit.  An empty path is never returned for a valid same-cell
    /// request — start==goal yields a single-element list [start].
    /// </summary>
    public static class AStar
    {
        public const int COST_CARDINAL = 10;
        public const int COST_DIAGONAL = 14;
        public const int DEFAULT_NODE_CAP = 12000;  // #235 90x90(8100셀) 맵 cross-path 여유 (4000→12000)

        // 8 neighbour offsets.  Cardinals first, then diagonals — deterministic
        // expansion order for stable tie-breaking.
        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int( 1,  0), new Vector2Int(-1,  0),
            new Vector2Int( 0,  1), new Vector2Int( 0, -1),
            new Vector2Int( 1,  1), new Vector2Int( 1, -1),
            new Vector2Int(-1,  1), new Vector2Int(-1, -1),
        };

        /// <summary>
        /// Find a path from start to goal over the grid.  Returns a cell list
        /// [start ... goal] inclusive, or null if unreachable / capped.
        /// </summary>
        public static List<Vector2Int> FindPath(
            PathGrid grid, Vector2Int start, Vector2Int goal, int nodeCap = DEFAULT_NODE_CAP)
        {
            if (grid == null) return null;
            if (!grid.IsWalkable(start) || !grid.IsWalkable(goal)) return null;
            if (start == goal) return new List<Vector2Int> { start };

            var open = new List<Node>();                       // simple binary-free open set
            var nodes = new Dictionary<Vector2Int, Node>();     // best-known node per cell
            var closed = new HashSet<Vector2Int>();

            var startNode = new Node(start, 0, Heuristic(start, goal), default, false);
            open.Add(startNode);
            nodes[start] = startNode;

            int expanded = 0;

            while (open.Count > 0)
            {
                // Pop lowest f; tie-break on lower h (closer to goal), then on
                // the deterministic neighbour-insertion order captured by the
                // scan index → stable across runs.
                int bestIdx = 0;
                Node best = open[0];
                for (int i = 1; i < open.Count; i++)
                {
                    Node n = open[i];
                    if (n.F < best.F || (n.F == best.F && n.H < best.H))
                    {
                        best = n; bestIdx = i;
                    }
                }
                open.RemoveAt(bestIdx);

                if (best.Cell == goal)
                    return Reconstruct(nodes, best);

                if (closed.Contains(best.Cell)) continue;
                closed.Add(best.Cell);

                expanded++;
                if (expanded > nodeCap) return null;   // pathological → no path

                for (int d = 0; d < Dirs.Length; d++)
                {
                    Vector2Int step = Dirs[d];
                    Vector2Int nc = best.Cell + step;
                    if (!grid.IsWalkable(nc)) continue;
                    if (closed.Contains(nc)) continue;

                    bool diagonal = step.x != 0 && step.y != 0;
                    if (diagonal)
                    {
                        // No corner-cutting: both shared orthogonal cells must
                        // be walkable (the reference sim rule).
                        Vector2Int side1 = new Vector2Int(best.Cell.x + step.x, best.Cell.y);
                        Vector2Int side2 = new Vector2Int(best.Cell.x, best.Cell.y + step.y);
                        if (!grid.IsWalkable(side1) || !grid.IsWalkable(side2)) continue;
                    }

                    int stepCost = diagonal ? COST_DIAGONAL : COST_CARDINAL;
                    int g = best.G + stepCost;

                    if (nodes.TryGetValue(nc, out Node existing))
                    {
                        if (g >= existing.G) continue;   // not a better path
                        existing.G = g;
                        existing.Parent = best.Cell;
                        existing.HasParent = true;
                        nodes[nc] = existing;
                        open.Add(existing);              // re-queue with better g
                    }
                    else
                    {
                        var nn = new Node(nc, g, Heuristic(nc, goal), best.Cell, true);
                        nodes[nc] = nn;
                        open.Add(nn);
                    }
                }
            }

            return null;   // open exhausted → unreachable
        }

        // Octile distance — matches 10/14 move costs, admissible.
        private static int Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            int min = Mathf.Min(dx, dy);
            int max = Mathf.Max(dx, dy);
            return COST_DIAGONAL * min + COST_CARDINAL * (max - min);
        }

        private static List<Vector2Int> Reconstruct(
            Dictionary<Vector2Int, Node> nodes, Node goalNode)
        {
            var path = new List<Vector2Int>();
            Node cur = goalNode;
            // Guard against malformed chains (cap on map size² steps).
            int safety = PathGrid.SIZE * PathGrid.SIZE + 1;
            while (true)
            {
                path.Add(cur.Cell);
                if (!cur.HasParent) break;
                if (!nodes.TryGetValue(cur.Parent, out cur)) break;
                if (--safety <= 0) break;
            }
            path.Reverse();   // start → goal
            return path;
        }

        // Mutable struct stored by value in the dictionary; re-inserted into the
        // open set when a better g is found (lazy-deletion via closed set).
        private struct Node
        {
            public Vector2Int Cell;
            public int G;            // cost from start
            public int H;            // heuristic to goal
            public Vector2Int Parent;
            public bool HasParent;
            public int F => G + H;

            public Node(Vector2Int cell, int g, int h, Vector2Int parent, bool hasParent)
            {
                Cell = cell; G = g; H = h; Parent = parent; HasParent = hasParent;
            }
        }
    }
}
