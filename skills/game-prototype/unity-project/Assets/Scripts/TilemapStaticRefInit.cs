using UnityEngine;
using UnityEngine.Tilemaps;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Step 81 — Initializes PawnMovement.GroundTilemap / WaterTile / RockTile
    /// at runtime Start.  Statics don't persist from Editor batchmode →
    /// runtime build.  This component is added to the Game scene by SceneSetup.
    /// </summary>
    public class TilemapStaticRefInit : MonoBehaviour
    {
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private TileBase waterTile;
        [SerializeField] private TileBase rockTile;

        public void SetRefs(Tilemap gt, TileBase wt, TileBase rt)
        {
            groundTilemap = gt;
            waterTile = wt;
            rockTile = rt;
        }

        private void Awake()
        {
            // Earliest possible — before PawnMovement.Update runs
            PawnMovement.GroundTilemap = groundTilemap;
            PawnMovement.WaterTile = waterTile;
            PawnMovement.RockTile = rockTile;
            // #199 B0 — build the A* walkability grid once at scene start so it
            //  EXISTS at runtime.  Null-safe (headless = all walkable).
            PawnMovement.Grid = new PathGrid(groundTilemap, waterTile, rockTile);
            // #199 B2 — LIVE CUTOVER.  Flip pathfinding ON now that the Grid is
            //  built.  The live game + the -integration (real game flow) suite
            //  now move every pawn via grid A* (operator: "최대한 림월드와 같은 방식으로").
            //
            //  EXCEPTION — the isolated -testmode suite runs in THIS SAME Game
            //  scene (this Awake fires before GameManager picks its branch), so a
            //  unconditional flip would leak the flag + a Game-tilemap Grid into
            //  the isolated unit scene.  That breaks the unit tests that exercise
            //  the OLD point-lerp primitive with out-of-grid pawns (V41 wanders a
            //  pawn at (60,0) — out of A* bounds → A* would correctly refuse → no
            //  target → false FAIL).  The isolated suite is by design (plan §1/B2)
            //  the OLD-branch / primitive scene; the pathfinding path is proven
            //  there by V71/V72 which flip the flag themselves and restore it.
            //  So: skip the flip in -testmode only.
            bool isolatedTest = false;
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (arg == "-testmode") { isolatedTest = true; break; }
            if (!isolatedTest) PawnMovement.UsePathfinding = true;
            Debug.Log($"[TilemapStaticRefInit] ground={(groundTilemap!=null)} water={(waterTile!=null)} rock={(rockTile!=null)} pathGrid=built isolatedTest={isolatedTest} usePathfinding={PawnMovement.UsePathfinding}");
        }
    }
}
