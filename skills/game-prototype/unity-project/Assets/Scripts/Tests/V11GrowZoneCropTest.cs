using System.Collections;
using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify B12 — V11 grow-zone -> crop-appears gated PASS.
    ///
    /// Wiki acceptance criterion (Dimension 5 B12 / v2 backlog):
    ///   "Painting a grow zone yields a crop."
    ///
    /// Chain verified as ONE binary PASS:
    ///   Step 1 — a target cell is confirmed to have NO CropEntity (assert absent).
    ///   Step 2 — GrowZoneDesignation designates that cell as a grow zone via the
    ///             public DesignateCell(Vector2Int) entry point.  The cell record
    ///             exists in the manager (GrowCell returned, not null).
    ///   Step 3 — an idle PawnEntity is dispatched by the manager to the cell;
    ///             the sim window allows UpdatePlantJobs to drive the walk->plant
    ///             cycle.  After the window a CropEntity exists on the cell
    ///             (assert crop present).
    ///
    /// Design notes:
    ///   - The test bootstraps a GrowZoneDesignation manager (if absent) mirroring
    ///     V15 / V10 (RuntimeInitializeOnLoadMethod does not fire in headless runner).
    ///   - The target cell (10, 10) is placed where IsPlantableCell returns true:
    ///     no tilemap in the headless scene -> PawnMovement.IsBlockedAt returns false
    ///     (no GroundTilemap) -> not blocked; PawnMovement.Grid is set to an all-
    ///     walkable test grid -> walkable; Physics2D overlap finds nothing -> free.
    ///   - The PawnEntity is placed at (11.5, 10.5) — one cell east of the target —
    ///     and given PawnMovement so the manager's FindNearestIdlePawn picks it up.
    ///     PawnIdle gate: not drafted, not sleeping, not moving, no worker HasTask.
    ///   - PlantCropAt spawns a new GameObject with CropEntity via AddComponent.
    ///     The test detects the crop by scanning for CropEntity instances near the
    ///     target cell center, mirroring GrowZoneDesignation.CellHasCrop.
    ///   - CropEntity.cs is NOT edited.  The test reads CropEntity presence READ-ONLY
    ///     via Object.FindObjectsByType / a cell-center OverlapBox.
    ///
    /// Harness conventions: same RunOne/Assert/GetWhiteSprite bootstrap pattern as
    /// V10DeconstructWalkableTest / V15DeconstructRefundTest / V4CombatChainTest.
    /// This file is a COMPANION test under the EXISTING Tests asmdef
    /// (Assets/Scripts/Tests).  It does NOT modify TestRunner.cs or any runtime .cs.
    ///
    /// Read-only API surface consumed (all public, no reflection):
    ///   GrowZoneDesignation.Instance          — public static property
    ///   GrowZoneDesignation.DesignateCell()   — public (QA/test entry point)
    ///   GrowZoneDesignation.IsPlantableCell() — public (used defensively)
    ///   GrowZoneDesignation.GrowCell          — public nested class
    ///   CropEntity                            — presence check only (GetComponent)
    ///   PawnEntity, PawnMovement              — spawned idle pawn
    ///   Physics2D.OverlapBoxAll               — cell occupancy check
    ///   PathGrid.FromMask, PathGrid.SIZE      — public (test grid install)
    ///   PawnMovement.Grid, .UsePathfinding    — public static fields
    ///   PawnMovement.IsBlockedAt              — public static (called by IsPlantableCell)
    ///
    /// API gaps flagged for QA:
    ///   G1 — GrowZoneDesignation.Instance may be null in the isolated test scene.
    ///        The test bootstraps the manager via AddComponent and relies on the
    ///        public static Instance property (set in Awake).  If the setter is
    ///        private the test falls back to Object.FindFirstObjectByType.
    ///   G2 — GrowZoneDesignation.DispatchToIdlePlanters and UpdatePlantJobs are
    ///        private (standard Unity Update path).  The test does NOT call them
    ///        directly; it gives the manager a coroutine window (WaitForSeconds) so
    ///        Unity's own Update loop drives the dispatch->walk->plant cycle.
    ///        plantRange is 1.5f; the pawn is placed 1 unit east of the cell ->
    ///        distance = 1.0f < 1.5f -> the "in range" branch fires immediately.
    ///   G3 — PawnMovement.LastPathFailed and AtStandCell are accessed internally by
    ///        GrowZoneDesignation.UpdatePlantJobs; not needed by this test.
    ///   G4 — GrowZoneDesignation.DispatchToIdlePlanters calls
    ///        ReservationManager.IsCellReservedByOther; in a headless scene the
    ///        ReservationManager may not be initialised.  If this causes a NullRef
    ///        the test catches it via the RunOne try/catch and flags it here.
    ///   G5 — TestRunner registration: this companion test .cs and its .meta file
    ///        are not registered in TestRunner.cs.  QA must confirm whether this
    ///        file needs an entry in the isolated test suite to count in the 76/76
    ///        tally, or whether it runs as a standalone scene-attached script.
    ///        Pattern mirrors V15DeconstructRefundTest (also not in TestRunner).
    /// </summary>
    public class V11GrowZoneCropTest : MonoBehaviour
    {
        public string outputPath = "G:/ai/_v11_grow_zone_crop_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V11GrowZoneCropTest] start — B12 grow-zone->crop-appears");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V11-grow-zone-crop", TestV11_GrowZoneCropAppears);

            Debug.Log($"[V11GrowZoneCropTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V11GrowZoneCropTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V11 — grow-zone designation -> crop appears on cell
        // ----------------------------------------------------------------

        private IEnumerator TestV11_GrowZoneCropAppears()
        {
            // The target grow cell: (10, 10) — well inside grid bounds, no pre-existing
            // structures in a headless test scene.
            Vector2Int targetCell = new Vector2Int(10, 10);
            Vector2 cellCenter = new Vector2(targetCell.x + 0.5f, targetCell.y + 0.5f);

            // ---- Install a test PathGrid so IsPlantableCell's walkable check passes.
            //     GrowZoneDesignation.IsPlantableCell reads PawnMovement.Grid.IsWalkable;
            //     an all-walkable grid ensures the cell is accepted.
            PathGrid savedGrid = PawnMovement.Grid;
            bool savedUsePathfinding = PawnMovement.UsePathfinding;

            int size = PathGrid.SIZE;
            bool[,] allWalkable = new bool[size, size];
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    allWalkable[x, y] = true;

            PathGrid testGrid = PathGrid.FromMask(allWalkable);
            PawnMovement.Grid = testGrid;
            PawnMovement.UsePathfinding = true;

            // ---- G1: Bootstrap GrowZoneDesignation manager if absent.
            GrowZoneDesignation gzd = GrowZoneDesignation.Instance;
            GameObject managerGo = null;
            if (gzd == null)
            {
                managerGo = new GameObject("V11_GrowZoneDesignation");
                gzd = managerGo.AddComponent<GrowZoneDesignation>();
                yield return null;   // Awake sets Instance
                gzd = GrowZoneDesignation.Instance;
                // Fallback if Instance setter is private.
                if (gzd == null)
                {
#if UNITY_2023_1_OR_NEWER
                    gzd = Object.FindFirstObjectByType<GrowZoneDesignation>();
#else
                    gzd = Object.FindObjectOfType<GrowZoneDesignation>();
#endif
                }
            }

            // ---- Step 1: Assert NO CropEntity exists on the target cell.
            bool step1_noCropBefore = !CellHasCrop(cellCenter);

            // ---- Step 2: Designate the target cell as a grow zone.
            //     DesignateCell is the public QA/test entry point on GrowZoneDesignation
            //     (doc comment: "QA / test entry point: designate a specific cell
            //     directly.").  It calls MarkCellAt internally, which calls
            //     IsPlantableCell (our test grid makes it pass) and creates a GrowCell.
            GrowZoneDesignation.GrowCell growCell = null;
            if (gzd != null)
            {
                growCell = gzd.DesignateCell(targetCell);
            }

            bool step2_cellDesignated = growCell != null;
            yield return null;

            // ---- Step 3: Spawn an idle PawnEntity near the cell.
            //     Place the pawn at (11.5, 10.5) — one cell east of target center
            //     (10.5, 10.5).  Distance = 1.0f < plantRange 1.5f -> the manager's
            //     UpdatePlantJobs "in range" branch fires without a walk phase.
            //     G2: UpdatePlantJobs is private; we let Unity's Update loop drive it
            //     over a 3 s window.
            //
            //     PawnIdle gate: the pawn must not be drafted, sleeping, moving, or
            //     carrying a task.  A freshly-spawned PawnEntity satisfies all gates
            //     (no workers, not drafted, not sleeping, not moving).
            Vector3 pawnPos = new Vector3(11.5f, 10.5f, 0f);
            GameObject pawnGo = new GameObject("V11_TestPawn");
            pawnGo.transform.position = pawnPos;
            pawnGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            pawnGo.AddComponent<BoxCollider2D>().size = Vector2.one * 0.8f;
            pawnGo.AddComponent<PawnEntity>();
            pawnGo.AddComponent<PawnMovement>();
            yield return null;   // Awake

            // Give the manager's Update loop time to run DispatchToIdlePlanters and
            // UpdatePlantJobs (plantRange 1.5f > distance 1.0f -> plants immediately).
            // 3 s is generous for a single "in range" plant event.
            yield return new WaitForSeconds(3.0f);

            // ---- Step 4 (primary gate): a CropEntity now exists on the target cell.
            //     GrowZoneDesignation.PlantCropAt spawns a new GameObject with
            //     CropEntity via AddComponent at (cell.x + 0.5, cell.y + 0.5, 0).
            //     We detect it by scanning a small box at the cell center.
            bool step3_cropAppeared = CellHasCrop(cellCenter);

            // Secondary: the grow zone record self-pruned (crop appeared -> PruneZone
            // drops the marker next tick after planting).  Not required for PASS but
            // logged for diagnostics.
            bool step3_zoneCleared = gzd == null || !gzd.IsPlantableCell(targetCell.x, targetCell.y)
                                     || step3_cropAppeared;   // zone clears once crop present

            // ---- Cleanup ----
            if (pawnGo    != null) Object.Destroy(pawnGo);
            if (managerGo != null) Object.Destroy(managerGo);

            // Destroy any crop spawned during the test (cell + 0.5 centre).
            CleanupCropsAt(cellCenter);

            PawnMovement.Grid = savedGrid;
            PawnMovement.UsePathfinding = savedUsePathfinding;

            // ---- Composite assertion — ONE binary PASS ----
            //
            // B12 wiki acceptance: "Painting a grow zone yields a crop."
            // Primary gate = step3_cropAppeared.
            bool allPass = step1_noCropBefore
                        && step2_cellDesignated
                        && step3_cropAppeared;

            Assert(allPass,
                $"chain: " +
                $"[1]noCropBefore={step1_noCropBefore}(cell={targetCell}) " +
                $"[2]cellDesignated={step2_cellDesignated}(growCell={(growCell != null ? "ok" : "null")}) " +
                $"[3]cropAppeared={step3_cropAppeared},zoneCleared={step3_zoneCleared} " +
                $"| B12 wiki: grow-zone designation -> crop appears on cell");
        }

        // ---- Helpers ----

        /// <summary>True if a CropEntity exists within the 0.9x0.9 box at cellCenter
        /// (matches GrowZoneDesignation.CellHasCrop's 0.45 half-extent).</summary>
        private static bool CellHasCrop(Vector2 cellCenter)
        {
            var hits = Physics2D.OverlapBoxAll(cellCenter, Vector2.one * 0.9f, 0f);
            foreach (var h in hits)
                if (h != null && h.GetComponent<CropEntity>() != null) return true;
            return false;
        }

        /// <summary>Destroy any CropEntity GameObjects near cellCenter (test cleanup).</summary>
        private static void CleanupCropsAt(Vector2 cellCenter)
        {
#if UNITY_2023_1_OR_NEWER
            var crops = Object.FindObjectsByType<CropEntity>(FindObjectsSortMode.None);
#else
            var crops = Object.FindObjectsOfType<CropEntity>();
#endif
            foreach (var c in crops)
            {
                if (c == null) continue;
                if (Vector2.Distance(c.transform.position, cellCenter) < 1.5f)
                    Object.Destroy(c.gameObject);
            }
        }

        private static Sprite _whiteSprite;
        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            _whiteSprite.name = "V11WhiteSprite";
            return _whiteSprite;
        }
    }
}
