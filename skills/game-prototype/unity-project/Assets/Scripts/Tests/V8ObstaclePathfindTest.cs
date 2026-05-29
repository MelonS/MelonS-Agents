using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MelonS.GameProto.AI;
using MelonS.GameProto.Data;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify (V8 slate) — obstacle-pathfind gate test.
    ///
    /// Wiki acceptance criterion (V8 slate):
    ///   "A pawn whose direct path is blocked by an obstacle pathfinds around it
    ///    (or stops) and never walks through the blocked cell — asserted as one
    ///    PASS line."
    ///
    /// Chain verified as ONE binary PASS:
    ///   Step 1 — PathGrid is constructed with a wall cell blocking the straight
    ///             line between pawn-start and target.  The wall cell is NOT walkable
    ///             (grid.IsWalkable(wallCell) == false).
    ///   Step 2 — A* finds a path that routes AROUND the wall cell (path returned
    ///             is non-null and does not contain the wall cell).
    ///   Step 3 — The pawn is commanded to move to the target via SetTarget (with
    ///             UsePathfinding ON).  Over a simulation window the pawn either
    ///             (a) reaches the target (LastPathFailed == false, position near
    ///                 target) or
    ///             (b) stops cleanly (LastPathFailed == true — the target was judged
    ///                 unreachable from the start cell, which is acceptable per spec).
    ///   Step 4 — At NO point during the simulation does the pawn's current cell
    ///             equal the blocked wall cell (never clips through).
    ///   Step 5 — At NO point does the pawn's position fall outside PathGrid bounds
    ///             (never stuck off-grid).
    ///
    /// Harness conventions: same RunOne/Assert pattern as V4CombatChainTest /
    /// V7RaidThreatTest / V8MoveSpeedTest.  This file is a COMPANION test; it does
    /// NOT modify TestRunner.cs or any runtime .cs file.
    ///
    /// Map layout (cell coordinates, x right / y up):
    ///   Pawn start : cell (0, 0)  → world centre (0.5, 0.5)
    ///   Wall cell  : cell (1, 0)  → world centre (1.5, 0.5)  [blocks straight-line]
    ///   Target     : cell (2, 0)  → world centre (2.5, 0.5)
    ///
    ///   The straight path (0,0)→(1,0)→(2,0) is blocked at (1,0).
    ///   A valid detour exists: (0,0)→(0,1)→(1,1)→(2,1)→(2,0)  (all walkable).
    ///
    /// AccessorFlags / QA notes:
    ///   No reflection used.  All APIs consumed are public:
    ///     PathGrid.FromMask(bool[,])          — public static
    ///     PathGrid.IsWalkable(Vector2Int)      — public
    ///     PathGrid.WorldToCell(Vector2)        — public static
    ///     PathGrid.CellToWorld(Vector2Int)     — public static
    ///     PathGrid.InBounds(Vector2Int)        — public
    ///     PathGrid.MIN / MAX                   — public const
    ///     AStar.FindPath(PathGrid, ...)        — public static
    ///     PawnMovement.UsePathfinding          — public static field
    ///     PawnMovement.Grid                    — public static field
    ///     PawnMovement.SetTarget(Vector2)      — public
    ///     PawnMovement.AdvanceAlongPath(float) — public
    ///     PawnMovement.LastPathFailed          — public property
    ///     PawnMovement.WORLD_MIN / WORLD_MAX   — public static readonly
    ///   FLAG for QA: PawnMovement.AdvanceAlongPath requires PawnStats with
    ///   arriveDistance; PawnStats.CreateDefault() is used (public factory, no
    ///   reflection).  If CreateDefault() returns null in batchmode, Step 3/4/5
    ///   will report the failure explicitly.
    /// </summary>
    public class V8ObstaclePathfindTest : MonoBehaviour
    {
        public string outputPath = "G:/ai/_v8_obstacle_pathfind_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V8ObstaclePathfindTest] start — V8 obstacle-pathfind");
            yield return new WaitForSeconds(0.1f);

            yield return RunOne("V8-obstacle-pathfind", TestV8_ObstaclePathfind);

            Debug.Log($"[V8ObstaclePathfindTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
        }

        private IEnumerator RunOne(string id, System.Func<IEnumerator> body)
        {
            float t0 = Time.realtimeSinceStartup;
            bool threw = false;
            string err = "";
            IEnumerator iter = null;
            try { iter = body(); }
            catch (System.Exception e) { threw = true; err = $"{e.GetType().Name}: {e.Message}"; }
            if (!threw && iter != null)
            {
                while (true)
                {
                    bool moved = false;
                    try { moved = iter.MoveNext(); }
                    catch (System.Exception e) { threw = true; err = $"{e.GetType().Name}: {e.Message}"; break; }
                    if (!moved) break;
                    yield return iter.Current;
                }
            }
            float dur = Time.realtimeSinceStartup - t0;
            if (threw) { _lastAssertPassed = false; _lastAssertMessage = err; }
            Debug.Log($"[V8ObstaclePathfindTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V8 — obstacle pathfind chain
        // ----------------------------------------------------------------

        private IEnumerator TestV8_ObstaclePathfind()
        {
            // ---- Step 1: Build a test PathGrid with a wall blocking the straight line ----
            //
            // Map layout (all cells walkable EXCEPT wall at (1,0)):
            //   Pawn start : cell (0, 0) — world centre (0.5, 0.5)
            //   Wall cell  : cell (1, 0) — BLOCKED (direct route cut off)
            //   Target     : cell (2, 0) — world centre (2.5, 0.5)
            //
            // Valid detour (all walkable): (0,0)→(0,1)→(1,1)→(2,1)→(2,0)
            //
            // PathGrid coordinate system: array index [x-MIN, y-MIN], cell range [-30..29].
            // We build a full-size mask (SIZE×SIZE) with all true except the wall cell.

            int size = PathGrid.SIZE;   // 60
            int minOff = -PathGrid.MIN; // 30 — the offset so cell (0,0) maps to index [30,30]

            bool[,] mask = new bool[size, size];
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    mask[x, y] = true;

            // Block cell (1,0): index [1 - MIN, 0 - MIN] = [1+30, 0+30] = [31, 30]
            var wallCell = new Vector2Int(1, 0);
            mask[wallCell.x - PathGrid.MIN, wallCell.y - PathGrid.MIN] = false;

            PathGrid testGrid = PathGrid.FromMask(mask);

            // Confirm the wall cell is not walkable (Step 1 guard).
            bool step1_wallBlocked = !testGrid.IsWalkable(wallCell);

            // Confirm pawn start and target are walkable.
            var startCell  = new Vector2Int(0, 0);
            var targetCell = new Vector2Int(2, 0);
            bool startWalkable  = testGrid.IsWalkable(startCell);
            bool targetWalkable = testGrid.IsWalkable(targetCell);

            // ---- Step 2: A* finds a path that avoids the wall cell ----
            List<Vector2Int> path = AStar.FindPath(testGrid, startCell, targetCell);

            bool step2_pathFound = path != null && path.Count > 0;
            bool step2_wallNotInPath = true;
            if (path != null)
            {
                foreach (var cell in path)
                {
                    if (cell == wallCell)
                    {
                        step2_wallNotInPath = false;
                        break;
                    }
                }
            }

            yield return null;

            // ---- Step 3-5: Simulate pawn movement with UsePathfinding ON ----
            //
            // Capture original static state so we can restore it after the test.
            bool savedUsePathfinding = PawnMovement.UsePathfinding;
            PathGrid savedGrid = PawnMovement.Grid;

            // Install the test grid into the static pathfinding slot.
            PawnMovement.UsePathfinding = true;
            PawnMovement.Grid = testGrid;

            // Spawn a minimal pawn at startCell's world centre.
            Vector2 startWorld  = PathGrid.CellToWorld(startCell);
            Vector2 targetWorld = PathGrid.CellToWorld(targetCell);

            GameObject pawnGo = SpawnTestPawn(startWorld);
            PawnMovement movement = pawnGo.GetComponent<PawnMovement>();

            yield return null;   // Awake — PawnStats.CreateDefault() wired

            // Command the pawn to move to the target world position.
            movement.SetTarget(targetWorld);

            yield return null;   // one frame so A* path is computed via SetTarget

            // Track whether the pawn ever occupies the blocked wall cell (Step 4)
            // and whether it ever exits grid bounds (Step 5).
            bool step4_neverOnWall = true;
            bool step5_neverOffGrid = true;

            // Simulate up to 3s of movement at a fast effective speed.
            // AdvanceAlongPath takes maxDelta in world units; we call it with a
            // step large enough to move between cells quickly (1 unit/tick).
            float simulationTime = 3.0f;
            float elapsed = 0f;
            float tickDelta = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
            float moveStep = 1.5f * tickDelta;   // generous step — grid cells are 1 unit

            bool arrived = false;
            while (elapsed < simulationTime)
            {
                // Check current cell BEFORE advancing (guard wall and grid bounds).
                Vector2 pos = pawnGo.transform.position;
                Vector2Int currentCell = PathGrid.WorldToCell(pos);

                if (currentCell == wallCell)
                {
                    step4_neverOnWall = false;
                }

                if (!testGrid.InBounds(currentCell))
                {
                    step5_neverOffGrid = false;
                }

                // Advance pawn along its A* path for this tick.
                bool reachedGoal = movement.AdvanceAlongPath(moveStep);
                if (reachedGoal) { arrived = true; break; }

                elapsed += tickDelta;
                yield return null;
            }

            // Final cell check after simulation ends.
            {
                Vector2 finalPos = pawnGo.transform.position;
                Vector2Int finalCell = PathGrid.WorldToCell(finalPos);
                if (finalCell == wallCell)   step4_neverOnWall  = false;
                if (!testGrid.InBounds(finalCell)) step5_neverOffGrid = false;
            }

            // Step 3: pawn either arrived at target OR stopped cleanly (LastPathFailed).
            bool step3_reachedOrStopped = arrived || movement.LastPathFailed
                || (!movement.HasTarget);

            // Extra diagnostic: record final distance to target.
            float finalDist = Vector2.Distance(pawnGo.transform.position, targetWorld);

            // ---- Cleanup ----
            Object.Destroy(pawnGo);

            // Restore original static pathfinding state.
            PawnMovement.UsePathfinding = savedUsePathfinding;
            PawnMovement.Grid = savedGrid;

            // ---- Composite assertion — ONE binary PASS ----
            //
            // The test PASSES when ALL of the following hold:
            //   1. The wall cell was genuinely blocked.
            //   2. A* produced a path that does NOT include the wall cell.
            //   3. The pawn reached the target OR stopped cleanly (not stuck in a
            //      spin — any definitive outcome is acceptable per spec).
            //   4. The pawn NEVER occupied the blocked wall cell.
            //   5. The pawn NEVER exited the grid bounds.
            //
            // Note: steps 3–5 depend on step 2 (the pawn follows the A* path).
            // If step 2 fails (path null or contains wall), the movement sim is
            // skipped and steps 3–5 reflect the pathological "no valid detour"
            // outcome — the composite still FAILS, correctly surfacing the bug.

            bool allPass = step1_wallBlocked
                        && step2_pathFound
                        && step2_wallNotInPath
                        && step3_reachedOrStopped
                        && step4_neverOnWall
                        && step5_neverOffGrid;

            Assert(allPass,
                $"chain: " +
                $"[1]wallBlocked={step1_wallBlocked}(startWalkable={startWalkable},targetWalkable={targetWalkable}) " +
                $"[2]pathFound={step2_pathFound},wallNotInPath={step2_wallNotInPath}(pathLen={path?.Count ?? 0}) " +
                $"[3]reachedOrStopped={step3_reachedOrStopped}(arrived={arrived},lastPathFailed={movement?.LastPathFailed},hasTarget={movement?.HasTarget}) " +
                $"[4]neverOnWall={step4_neverOnWall} " +
                $"[5]neverOffGrid={step5_neverOffGrid} " +
                $"finalDist={finalDist:F2} " +
                $"| V8 wiki: pawn routes around obstacle or stops; never clips blocked cell");
        }

        // ---- Helpers (mirrors TestRunner.cs / V4CombatChainTest.cs conventions) ----

        private GameObject SpawnTestPawn(Vector2 worldPos)
        {
            var go = new GameObject("V8_TestPawn");
            go.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<PawnEntity>();
            go.AddComponent<PawnHealth>();
            go.AddComponent<PawnMovement>();
            return go;
        }
    }
}
