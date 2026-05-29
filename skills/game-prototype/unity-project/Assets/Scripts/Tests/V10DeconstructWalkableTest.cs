using System.Collections;
using UnityEngine;
using MelonS.GameProto.AI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify B12 — V10 deconstruct -> walkable gated PASS.
    ///
    /// Wiki acceptance criterion (Dimension 5 B12 / v2 backlog):
    ///   "A test asserts deconstructing a wall makes its cell walkable."
    ///
    /// Chain verified as ONE binary PASS:
    ///   Step 1 — a WallEntity exists at a known world position and its PathGrid
    ///             cell is BLOCKED (IsWalkable == false after wall Start() runs).
    ///   Step 2 — the wall is designated for deconstruct via DeconstructDesignation
    ///             .TryMark / DeconstructTarget direct add — the designation marker
    ///             is attached (IsRemoved == false).
    ///   Step 3 — a PawnBuilder is placed in range and assigned the target via
    ///             SetDeconstructTarget; the sim window allows Update to run
    ///             AddWork until done and CompleteRemoval destroys the wall.
    ///   Step 4 — the previously-blocked PathGrid cell is now WALKABLE.
    ///             WallEntity.OnDestroy calls SetStructureBlocked(cell, false)
    ///             which decrements the ref-count to 0 and reopens the cell.
    ///
    /// Relationship to V15DeconstructRefundTest:
    ///   V15 covers the full refund chain (Steps 1-5 including material refund).
    ///   V10 is a FOCUSED walkable-cell gate test (binary PASS on the walkable
    ///   assertion only) — the B12 wiki acceptance is "cell becomes walkable",
    ///   separate from the refund verification in V15.
    ///   The test bootstrap mirrors V15 exactly; no duplicate refund assertions here.
    ///
    /// Harness conventions: same RunOne/Assert/GetWhiteSprite bootstrap pattern as
    /// V15DeconstructRefundTest / V4CombatChainTest.  This file is a COMPANION
    /// test under the EXISTING Tests asmdef (Assets/Scripts/Tests).  It does NOT
    /// modify TestRunner.cs or any runtime .cs file.
    ///
    /// Read-only API surface consumed (all public, no reflection):
    ///   PathGrid.FromMask(bool[,])           — public static factory
    ///   PathGrid.IsWalkable(Vector2Int)       — public
    ///   PathGrid.WorldToCell(Vector2)         — public static
    ///   PathGrid.SetStructureBlocked(...)     — public (called by WallEntity lifecycle)
    ///   PathGrid.SIZE                         — public const int
    ///   WallEntity.Material                   — public property
    ///   DeconstructDesignation.Instance       — public static property
    ///   DeconstructDesignation.TryMark(GO)   — public
    ///   DeconstructTarget.IsRemoved           — public property
    ///   DeconstructTarget.Initialize()        — public
    ///   PawnBuilder.SetDeconstructTarget(...) — public
    ///   PawnBuilder.HasDeconstructTask        — public property
    ///   PawnMovement.Grid                     — public static field
    ///   PawnMovement.UsePathfinding           — public static field
    ///   ResourceManager.Instance              — public static property
    ///
    /// API gaps flagged for QA:
    ///   G1 — DeconstructDesignation.Instance may be null in the isolated test scene
    ///        because [RuntimeInitializeOnLoadMethod] does not fire in a headless
    ///        runner.  The test bootstraps the manager via AddComponent and relies on
    ///        the public static Instance setter (same pattern as V15).
    ///   G2 — WallEntity.Start registers the wall cell with PawnMovement.Grid (the
    ///        static slot).  The test installs a fresh PathGrid into that slot so the
    ///        wall's own Start populates Step 1 correctly without a live tilemap.
    ///   G3 — ResourceManager.Instance may be null; the test bootstraps one so
    ///        DeconstructTarget.CompleteRemoval can call AddWood/AddStone.
    ///   G4 — PawnBuilder.UpdateDeconstruct is private (standard Unity Update path).
    ///        The test gives the builder a coroutine window (WaitForSeconds) so
    ///        Unity's own Update loop advances the work.  The builder is placed at
    ///        the wall's world position (within buildRange 1.5f) so it enters the
    ///        "in-range, do work" branch immediately.
    ///   G5 — TestRunner registration: this companion test .cs and its .meta file
    ///        are not registered in TestRunner.cs.  QA must confirm whether this
    ///        file needs an entry in the isolated test suite to count in the
    ///        76/76 tally, or whether it runs as a standalone scene-attached script.
    ///        Pattern mirrors V15DeconstructRefundTest (also not in TestRunner).
    /// </summary>
    public class V10DeconstructWalkableTest : MonoBehaviour
    {
        public string outputPath = "G:/ai/_v10_deconstruct_walkable_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V10DeconstructWalkableTest] start — B12 deconstruct->walkable");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V10-deconstruct-walkable", TestV10_DeconstructWalkableCell);

            Debug.Log($"[V10DeconstructWalkableTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V10DeconstructWalkableTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V10 — deconstruct -> cell walkable
        // ----------------------------------------------------------------

        private IEnumerator TestV10_DeconstructWalkableCell()
        {
            // ---- G2: Install a test PathGrid into PawnMovement.Grid so WallEntity.Start
            //     can register its cell without a live tilemap.  Save prior state.
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

            // ---- G3: Bootstrap ResourceManager so CompleteRemoval can refund material.
            ResourceManager rm = ResourceManager.Instance;
            GameObject rmGo = null;
            if (rm == null)
            {
                rmGo = new GameObject("V10_ResourceManager");
                rmGo.AddComponent<ResourceManager>();
                yield return null;   // Awake -> Services.Register
                rm = ResourceManager.Instance;
            }

            // ---- G1: Bootstrap DeconstructDesignation manager if absent.
            DeconstructDesignation manager = DeconstructDesignation.Instance;
            GameObject managerGo = null;
            if (manager == null)
            {
                managerGo = new GameObject("V10_DeconstructDesignation");
                manager = managerGo.AddComponent<DeconstructDesignation>();
                yield return null;   // Awake sets Instance
                manager = DeconstructDesignation.Instance;
            }

            // ---- Step 1: Spawn a WallEntity at world (8, 8) and assert BLOCKED.
            //     WallEntity.Start calls PawnMovement.RegisterWallCell -> testGrid
            //     .SetStructureBlocked(cell, true), incrementing the ref-count.
            Vector3 wallWorldPos = new Vector3(8.5f, 8.5f, 0f);   // cell-centre of (8,8)
            Vector2Int wallCell = PathGrid.WorldToCell(wallWorldPos);

            GameObject wallGo = new GameObject("V10_TestWall");
            wallGo.transform.position = wallWorldPos;
            wallGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            wallGo.AddComponent<BoxCollider2D>().size = Vector2.one * 0.9f;
            WallEntity wall = wallGo.AddComponent<WallEntity>();
            yield return null;   // Awake: hp = maxHp
            yield return null;   // Start: RegisterWallCell -> testGrid blocks wallCell

            bool step1_wallBlocked = !testGrid.IsWalkable(wallCell);

            // ---- Step 2: Designate the wall for deconstruct.
            //     TryMark is public on DeconstructDesignation; it attaches a
            //     DeconstructTarget and computes the ~50% refund.  Fallback: attach
            //     the marker directly if the manager bootstrap failed.
            DeconstructTarget deTarget = null;
            if (manager != null)
            {
                deTarget = manager.TryMark(wallGo);
            }
            if (deTarget == null)
            {
                deTarget = wallGo.AddComponent<DeconstructTarget>();
                deTarget.Initialize();
            }
            yield return null;

            bool step2_designated = deTarget != null && !deTarget.IsRemoved;

            // ---- Step 3: Assign PawnBuilder to the deconstruct target.
            //     Place builder within buildRange (1.5f) of the wall so UpdateDeconstruct
            //     enters "in-range, do work" immediately.  G4: we allow Unity's Update
            //     loop to drive the per-frame AddWork over a 4.5 s window.
            Vector3 builderPos = wallWorldPos + new Vector3(1.0f, 0f, 0f);
            GameObject builderGo = new GameObject("V10_TestBuilder");
            builderGo.transform.position = builderPos;
            builderGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            builderGo.AddComponent<BoxCollider2D>().size = Vector2.one * 0.8f;
            builderGo.AddComponent<PawnEntity>();
            builderGo.AddComponent<PawnHealth>();
            builderGo.AddComponent<PawnMovement>();
            PawnBuilder builder = builderGo.AddComponent<PawnBuilder>();
            yield return null;   // Awake

            if (deTarget != null && !deTarget.IsRemoved)
            {
                builder.SetDeconstructTarget(deTarget);
            }

            bool step3_taskAssigned = builder.HasDeconstructTask;

            // Sim window: 4.5 s lets Update drive AddWork until workToRemove (3f) is
            // satisfied and CompleteRemoval -> Destroy(wallGo) fires.
            yield return new WaitForSeconds(4.5f);

            // ---- Step 4 (primary gate): the previously-blocked cell is now WALKABLE.
            //     WallEntity.OnDestroy called Grid.SetStructureBlocked(cell, false),
            //     decrementing the ref-count to 0 and reopening the cell.
            bool step4_cellWalkable = testGrid.IsWalkable(wallCell);

            // Secondary check: the wall object is gone (CompleteRemoval destroyed it).
            bool step4_wallGone = (wallGo == null || wallGo.Equals(null)
                                   || !wallGo.activeInHierarchy
                                   || (deTarget != null && deTarget.IsRemoved));

            // ---- Cleanup ----
            if (builderGo != null) Object.Destroy(builderGo);
            if (wallGo    != null) Object.Destroy(wallGo);
            if (managerGo != null) Object.Destroy(managerGo);
            if (rmGo      != null) Object.Destroy(rmGo);

            PawnMovement.Grid = savedGrid;
            PawnMovement.UsePathfinding = savedUsePathfinding;

            // ---- Composite assertion — ONE binary PASS ----
            //
            // B12 wiki acceptance: "A test asserts deconstructing a wall makes its
            // cell walkable."  Primary gate = step4_cellWalkable.
            bool allPass = step1_wallBlocked
                        && step2_designated
                        && step3_taskAssigned
                        && step4_cellWalkable;

            Assert(allPass,
                $"chain: " +
                $"[1]wallBlocked={step1_wallBlocked}(cell={wallCell}) " +
                $"[2]designated={step2_designated} " +
                $"[3]taskAssigned={step3_taskAssigned} " +
                $"[4]cellWalkable={step4_cellWalkable},wallGone={step4_wallGone} " +
                $"| B12 wiki: deconstruct wall -> cell walkable");
        }

        // ---- Helpers ----

        private static Sprite _whiteSprite;
        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            _whiteSprite.name = "V10WhiteSprite";
            return _whiteSprite;
        }
    }
}
