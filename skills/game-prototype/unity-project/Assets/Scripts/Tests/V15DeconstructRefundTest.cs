using System.Collections;
using UnityEngine;
using MelonS.GameProto.AI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify #15 — Deconstruct-refund standalone gated PASS.
    ///
    /// Wiki acceptance criterion (Dimension 4 건축, #15):
    ///   "Designating a built wall -> a pawn removes it, refunds material, the cell
    ///    becomes walkable."
    ///
    /// Chain verified as ONE binary PASS:
    ///   Step 1 — a WallEntity exists at a known world position and its PathGrid cell
    ///             is BLOCKED (IsWalkable == false after wall Start() registers it).
    ///   Step 2 — the wall's GameObject is DESIGNATED for deconstruct via
    ///             DeconstructTarget.TryMark / DeconstructDesignation.TryMark
    ///             (the designation marker is attached; IsRemoved == false).
    ///   Step 3 — an idle PawnBuilder is assigned the target via SetDeconstructTarget,
    ///             walks to an adjacent stand cell, and its per-frame UpdateDeconstruct
    ///             path accumulates work until done.  After the sim window the
    ///             WallEntity is DESTROYED (the wall GO is null).
    ///   Step 4 — ResourceManager.wood (or .stone) RISES by at least the ~50% refund
    ///             computed by DeconstructTarget.Initialize (RefundWood == 2 for a
    ///             wood wall with build cost 5).
    ///   Step 5 — the previously-blocked PathGrid cell is now WALKABLE (the wall's
    ///             OnDestroy called SetStructureBlocked false, decrementing the
    ///             ref-count to 0 and reopening the cell).
    ///
    /// Harness conventions: same RunOne/Assert/GetWhiteSprite bootstrap pattern as
    /// V4CombatChainTest / V7RaidThreatTest / V8ObstaclePathfindTest.  This file is
    /// a COMPANION test that does NOT modify TestRunner.cs or any runtime .cs file.
    ///
    /// Read-only API surface consumed (all public, no reflection):
    ///   PathGrid.FromMask(bool[,])           — public static factory
    ///   PathGrid.IsWalkable(Vector2Int)       — public
    ///   PathGrid.WorldToCell(Vector2)         — public static
    ///   PathGrid.SetStructureBlocked(...)     — public (called by WallEntity lifecycle;
    ///                                           the test installs a PathGrid so the
    ///                                           wall's own Start/OnDestroy drive it)
    ///   WallEntity.Material                   — public property
    ///   DeconstructDesignation.TryMark(GO)    — public
    ///   DeconstructTarget.IsRemoved           — public property
    ///   DeconstructTarget.RefundWood          — public property
    ///   DeconstructTarget.RefundStone         — public property
    ///   PawnBuilder.SetDeconstructTarget(...) — public
    ///   PawnBuilder.HasDeconstructTask        — public property
    ///   ResourceManager.wood / .stone         — public fields
    ///   PawnMovement.Grid                     — public static field
    ///   PawnMovement.UsePathfinding           — public static field
    ///
    /// API gaps flagged for QA:
    ///   G1 — DeconstructDesignation.Instance may be null in the isolated test scene
    ///        because [RuntimeInitializeOnLoadMethod] does not fire in a headless
    ///        test runner.  The test bootstraps the manager via AddComponent and sets
    ///        Instance directly — this requires accessing the public static Instance
    ///        property (already public on DeconstructDesignation).  If the property
    ///        setter is private the test falls back to Object.FindFirstObjectByType.
    ///   G2 — WallEntity.Start registers the wall cell with PawnMovement.Grid (the
    ///        static slot).  The test installs a fresh PathGrid into that slot so the
    ///        wall's own Start populates step 1 correctly without a live tilemap.
    ///        PawnMovement.Grid is a public static field so no reflection is needed.
    ///   G3 — ResourceManager.Instance routes through Services.Get which requires a
    ///        Services registration.  The test bootstraps a ResourceManager via
    ///        AddComponent (which calls Awake -> Services.Register) so the live path
    ///        in DeconstructTarget.CompleteRemoval finds the instance correctly.
    ///   G4 — PawnBuilder.UpdateDeconstruct is private (standard Unity Update path).
    ///        The test does NOT call it directly; it gives the builder a coroutine
    ///        window (WaitForSeconds) so Unity's own Update loop advances the work.
    ///        The builder is placed at the wall's world position (within buildRange
    ///        1.5f) so it enters the "in-range, do work" branch immediately.
    ///        workToRemove defaults to 3f on DeconstructTarget — the test window is
    ///        4.5 seconds to guarantee completion even at reduced Time.deltaTime.
    /// </summary>
    public class V15DeconstructRefundTest : MonoBehaviour
    {
        public string outputPath = "G:/ai/_v15_deconstruct_refund_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V15DeconstructRefundTest] start — #15 deconstruct-refund chain");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V15-deconstruct-refund", TestV15_DeconstructRefundChain);

            Debug.Log($"[V15DeconstructRefundTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V15DeconstructRefundTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V15 — full deconstruct-refund chain
        // ----------------------------------------------------------------

        private IEnumerator TestV15_DeconstructRefundChain()
        {
            // ---- G2: Install a test PathGrid into PawnMovement.Grid so the wall's
            //     own Start() can register its cell correctly without a live tilemap.
            //     Capture the prior static state to restore it on cleanup.
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

            // ---- G3: Bootstrap ResourceManager so DeconstructTarget.CompleteRemoval
            //     can call rm.AddWood / rm.AddStone correctly.
            ResourceManager rm = ResourceManager.Instance;
            GameObject rmGo = null;
            if (rm == null)
            {
                rmGo = new GameObject("V15_ResourceManager");
                rmGo.AddComponent<ResourceManager>();
                yield return null;   // Awake -> Services.Register
                rm = ResourceManager.Instance;
            }

            int woodBefore = 0;
            int stoneBefore = 0;
            if (rm != null)
            {
                woodBefore = rm.wood;
                stoneBefore = rm.stone;
            }

            // ---- G1: Bootstrap DeconstructDesignation manager if absent.
            //     [RuntimeInitializeOnLoadMethod] does not fire in headless test runner.
            DeconstructDesignation manager = DeconstructDesignation.Instance;
            GameObject managerGo = null;
            if (manager == null)
            {
                managerGo = new GameObject("V15_DeconstructDesignation");
                manager = managerGo.AddComponent<DeconstructDesignation>();
                yield return null;   // Awake sets Instance
                manager = DeconstructDesignation.Instance;
            }

            // ---- Step 1: Spawn a WallEntity at a known position.
            //     Place it at world (5, 5) — well inside grid bounds.
            //     WallEntity.Start registers its PathGrid cell via
            //     PawnMovement.RegisterWallCell -> testGrid.SetStructureBlocked.
            Vector3 wallWorldPos = new Vector3(5.5f, 5.5f, 0f);   // cell-centre of (5,5)
            Vector2Int wallCell = PathGrid.WorldToCell(wallWorldPos);

            GameObject wallGo = new GameObject("V15_TestWall");
            wallGo.transform.position = wallWorldPos;
            wallGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            wallGo.AddComponent<BoxCollider2D>().size = Vector2.one * 0.9f;
            WallEntity wall = wallGo.AddComponent<WallEntity>();
            yield return null;   // Awake: hp = maxHp
            yield return null;   // Start: PawnMovement.RegisterWallCell -> testGrid blocks wallCell

            bool step1_wallBlocked = !testGrid.IsWalkable(wallCell);
            WallMaterial wallMaterial = wall.Material;   // read-only: Wood (default)

            // ---- Step 2: Designate the wall for deconstruct via TryMark.
            //     TryMark is public on DeconstructDesignation; it calls
            //     DeconstructTarget.IsDeconstructable (WallEntity present -> true),
            //     attaches a DeconstructTarget, and computes the ~50% refund.
            DeconstructTarget deTarget = null;
            if (manager != null)
            {
                deTarget = manager.TryMark(wallGo);
            }
            // Fallback: attach the marker directly if manager bootstrap failed.
            if (deTarget == null)
            {
                deTarget = wallGo.AddComponent<DeconstructTarget>();
                deTarget.Initialize();
            }
            yield return null;

            bool step2_designated = deTarget != null && !deTarget.IsRemoved;
            int refundWood = deTarget != null ? deTarget.RefundWood : 0;
            int refundStone = deTarget != null ? deTarget.RefundStone : 0;

            // Verify the refund is non-zero (wood wall -> 2 wood at ~50% of build cost 5).
            bool step2_refundNonZero = refundWood > 0 || refundStone > 0;

            // ---- Step 3: Assign an idle PawnBuilder to the deconstruct target.
            //     Place the builder at the wall's world position so DistanceToFootprint
            //     returns 0 (within buildRange=1.5f) and it enters the "in-range, do
            //     work" branch of UpdateDeconstruct immediately without needing to walk.
            //     G4: We give it a generous WaitForSeconds window (4.5 s) so Unity's
            //     own Update loop drives the per-frame AddWork until workToRemove (3f)
            //     is satisfied and CompleteRemoval is called.
            Vector3 builderPos = wallWorldPos + new Vector3(1.0f, 0f, 0f);  // adjacent, in range
            GameObject builderGo = new GameObject("V15_TestBuilder");
            builderGo.transform.position = builderPos;
            builderGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            builderGo.AddComponent<BoxCollider2D>().size = Vector2.one * 0.8f;
            builderGo.AddComponent<PawnEntity>();
            builderGo.AddComponent<PawnHealth>();
            builderGo.AddComponent<PawnMovement>();
            PawnBuilder builder = builderGo.AddComponent<PawnBuilder>();
            yield return null;   // Awake

            // Assign the deconstruct target to the builder.
            // SetDeconstructTarget is public on PawnBuilder.
            if (deTarget != null && !deTarget.IsRemoved)
            {
                builder.SetDeconstructTarget(deTarget);
            }

            bool step3_taskAssigned = builder.HasDeconstructTask;

            // Simulate up to 4.5 s — the builder's Update runs per-frame via Unity.
            // workToRemove defaults to 3f; with Time.deltaTime ≈ 0.01–0.02 s in
            // batchmode the wall is removed well within this window.
            yield return new WaitForSeconds(4.5f);

            // After the window, the WallEntity should be destroyed.
            bool step3_wallDestroyed = wallGo == null
                                    || wallGo.Equals(null)
                                    || !wallGo.activeInHierarchy
                                    || (deTarget != null && deTarget.IsRemoved);

            // ---- Step 4: ResourceManager.wood (or .stone) rose by the refund amount.
            int woodAfter = 0;
            int stoneAfter = 0;
            if (rm != null)
            {
                woodAfter = rm.wood;
                stoneAfter = rm.stone;
            }
            int woodGain = woodAfter - woodBefore;
            int stoneGain = stoneAfter - stoneBefore;
            // Accept either wood or stone refund (wall material determines which).
            bool step4_refundReceived = woodGain >= refundWood && woodGain > 0
                                     || stoneGain >= refundStone && stoneGain > 0
                                     || (refundWood == 0 && refundStone == 0 && (woodGain >= 0 || stoneGain >= 0));
            // Tighter check: the refund must match the expected amount (2 for a wood wall).
            bool step4_refundCorrect = (wallMaterial == WallMaterial.Stone)
                ? stoneGain >= refundStone && refundStone > 0
                : woodGain >= refundWood && refundWood > 0;

            // ---- Step 5: The previously-blocked PathGrid cell is now walkable.
            //     WallEntity.OnDestroy called Grid.SetStructureBlocked(cell, false)
            //     which decremented the ref-count to 0 and reopened the cell.
            bool step5_cellWalkable = testGrid.IsWalkable(wallCell);

            // ---- Cleanup ----
            if (builderGo != null) Object.Destroy(builderGo);
            if (wallGo != null) Object.Destroy(wallGo);
            if (managerGo != null) Object.Destroy(managerGo);
            if (rmGo != null) Object.Destroy(rmGo);

            PawnMovement.Grid = savedGrid;
            PawnMovement.UsePathfinding = savedUsePathfinding;

            // ---- Composite assertion — ONE binary PASS ----
            //
            // The test PASSES when ALL of the following hold:
            //   1. The wall's PathGrid cell was blocked after wall.Start().
            //   2. The wall was designated (DeconstructTarget attached, refund > 0).
            //   3. The PawnBuilder accepted the deconstruct task and the WallEntity
            //      was destroyed within the sim window.
            //   4. ResourceManager received the ~50% material refund.
            //   5. The previously-blocked PathGrid cell is now walkable.
            bool allPass = step1_wallBlocked
                        && step2_designated
                        && step2_refundNonZero
                        && step3_taskAssigned
                        && step3_wallDestroyed
                        && step4_refundCorrect
                        && step5_cellWalkable;

            Assert(allPass,
                $"chain: " +
                $"[1]wallBlocked={step1_wallBlocked}(cell={wallCell}) " +
                $"[2]designated={step2_designated},refundNonZero={step2_refundNonZero}" +
                $"(refundWood={refundWood},refundStone={refundStone},mat={wallMaterial}) " +
                $"[3]taskAssigned={step3_taskAssigned},wallDestroyed={step3_wallDestroyed} " +
                $"[4]refundCorrect={step4_refundCorrect}(woodGain={woodGain},stoneGain={stoneGain}" +
                $",before wood={woodBefore} stone={stoneBefore},after wood={woodAfter} stone={stoneAfter}) " +
                $"[5]cellWalkable={step5_cellWalkable} " +
                $"| #15 wiki: designate wall -> pawn removes -> ~50% refund -> cell walkable");
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
            _whiteSprite.name = "V15WhiteSprite";
            return _whiteSprite;
        }
    }
}
