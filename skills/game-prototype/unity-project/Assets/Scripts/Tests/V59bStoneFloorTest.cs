using System.Collections;
using UnityEngine;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify companion — V59b stone-floor move-speed isolation gate test.
    ///
    /// Wiki acceptance (Dim3 terrain-move-cost / Dim5 V59b):
    ///   "A gated test asserts stone-floor effective move speed (1.50x) >
    ///    wood-floor (1.30x) > bare ground as ONE PASS line."
    ///
    /// Chain verified as ONE binary PASS:
    ///   pawn C on StoneFloorEntity (MoveBonus 1.50x)  moves farther than
    ///   pawn B on FloorEntity      (MoveBonus 1.30x)  moves farther than
    ///   pawn A on bare ground      (bonus 1.0x)
    ///   over the same fixed 0.4 s window.
    ///
    /// Design mirrors TestV59_FloorMoveSpeed in TestRunner.cs (OLD fallback
    /// branch, UsePathfinding=false by default in isolated V-test scenes).
    /// Adds a third pawn on StoneFloorEntity to extend the V59 wood-only
    /// isolation into a 3-way ordered comparison.
    ///
    /// READ-ONLY accessors used:
    ///   FloorEntity.MoveBonus    (public virtual float) — 1.30x
    ///   StoneFloorEntity.MoveBonus (public override float) — 1.50x
    ///   FloorEntity.BonusAt(pos) (public static float) — live measurement
    ///   PawnMovement component   (public API: SetTarget, transform)
    ///   PawnMovement.WORLD_MIN/MAX (public static) — world bounds
    ///
    /// No runtime .cs is edited.  No BuildManager.cs, SceneSetup.cs,
    /// ArchitectMenu.cs, or existing test is touched.
    /// Do NOT run Unity — QA verifies.
    ///
    /// FLAG for QA: StoneFloorEntity.stoneMoveBonus is [SerializeField] private.
    ///   MoveBonus property override is public and sufficient for this test
    ///   (we spawn a live StoneFloorEntity and read BonusAt, not the field).
    ///   No accessor gap blocks this test.
    /// </summary>
    public class V59bStoneFloorTest : MonoBehaviour
    {
        public string outputPath = "G:/ai/_v59b_stone_floor_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V59bStoneFloorTest] start — V59b stone-floor move-speed companion");
            yield return new WaitForSeconds(0.1f);

            yield return RunOne("V59b-stone-floor-move-speed", TestV59b_StoneFloorMoveSpeed);

            Debug.Log($"[V59bStoneFloorTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V59bStoneFloorTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V59b — stone-floor > wood-floor > bare-ground move-speed
        // ----------------------------------------------------------------

        private IEnumerator TestV59b_StoneFloorMoveSpeed()
        {
            // All three lanes use the OLD fallback branch (UsePathfinding=false
            // by default in isolated test scenes — same contract as V59).
            // Three isolated y-rows well inside WORLD_MIN/MAX (±29):
            //   A = bare ground at y = -24
            //   B = wood floor  at y = -26
            //   C = stone floor at y = -28
            // x-start for each pawn: -18.  Target x: +10 (never reached in 0.4s).
            // Each pawn's lane is vertically separated by 2 units so the
            // OverlapBox (0.3f radius) on each floor cannot bleed into another lane.

            const float startX   = -18f;
            const float targetX  =  10f;
            const float yBare    = -24f;
            const float yWood    = -26f;
            const float yStone   = -28f;
            const float window   =  0.4f;
            // Floor collider spans the full travel distance so the pawn stays
            // on the bonus surface for the entire measurement window (lesson from V59).
            const float floorWidth = 30f;

            // ---- Pawn A — bare ground ----
            var goA = new GameObject("V59b_PawnA_Bare");
            goA.transform.position = new Vector3(startX, yBare, 0f);
            goA.AddComponent<SpriteRenderer>();
            goA.AddComponent<PawnEntity>();
            var mvA = goA.AddComponent<PawnMovement>();
            mvA.SetTarget(new Vector2(targetX, yBare));

            // ---- Pawn B — wood floor (FloorEntity, MoveBonus 1.30x) ----
            var floorWoodGo = new GameObject("V59b_FloorWood");
            floorWoodGo.transform.position = new Vector3(startX, yWood, 0f);
            floorWoodGo.AddComponent<SpriteRenderer>();
            floorWoodGo.AddComponent<FloorEntity>();
            var floorWoodCol = floorWoodGo.AddComponent<BoxCollider2D>();
            floorWoodCol.size   = new Vector2(floorWidth, 1f);
            floorWoodCol.isTrigger = true;
            var goB = new GameObject("V59b_PawnB_Wood");
            goB.transform.position = new Vector3(startX, yWood, 0f);
            goB.AddComponent<SpriteRenderer>();
            goB.AddComponent<PawnEntity>();
            var mvB = goB.AddComponent<PawnMovement>();
            mvB.SetTarget(new Vector2(targetX, yWood));

            // ---- Pawn C — stone floor (StoneFloorEntity, MoveBonus 1.50x) ----
            var floorStoneGo = new GameObject("V59b_FloorStone");
            floorStoneGo.transform.position = new Vector3(startX, yStone, 0f);
            floorStoneGo.AddComponent<SpriteRenderer>();
            floorStoneGo.AddComponent<StoneFloorEntity>();
            var floorStoneCol = floorStoneGo.AddComponent<BoxCollider2D>();
            floorStoneCol.size   = new Vector2(floorWidth, 1f);
            floorStoneCol.isTrigger = true;
            var goC = new GameObject("V59b_PawnC_Stone");
            goC.transform.position = new Vector3(startX, yStone, 0f);
            goC.AddComponent<SpriteRenderer>();
            goC.AddComponent<PawnEntity>();
            var mvC = goC.AddComponent<PawnMovement>();
            mvC.SetTarget(new Vector2(targetX, yStone));

            // ---- Sanity: confirm bonus values via BonusAt on each lane ----
            // Read after one physics frame so colliders are registered.
            yield return null;
            float bonusAtBare  = FloorEntity.BonusAt(new Vector2(startX + 1f, yBare));
            float bonusAtWood  = FloorEntity.BonusAt(new Vector2(startX + 1f, yWood));
            float bonusAtStone = FloorEntity.BonusAt(new Vector2(startX + 1f, yStone));

            // ---- Run the measurement window ----
            yield return new WaitForSeconds(window);

            float movedA = goA.transform.position.x - startX;
            float movedB = goB.transform.position.x - startX;
            float movedC = goC.transform.position.x - startX;

            // ---- Cleanup ----
            Object.Destroy(goA);
            Object.Destroy(goB);
            Object.Destroy(goC);
            Object.Destroy(floorWoodGo);
            Object.Destroy(floorStoneGo);

            // ---- Composite assertion — ONE binary PASS ----
            // Expected bonus ordering: stone 1.50x > wood 1.30x > bare 1.0x.
            // Derived distance ordering: movedC > movedB > movedA.
            // Thresholds: stone must be > wood by at least 1.10x of wood's ratio
            //   (i.e. C/A >= 1.40x, B/A >= 1.15x, C/B >= 1.08x).
            // These are conservative — actual ratios are 1.50x, 1.30x, 1.15x
            // respectively — so a small Physics2D overlap delta is tolerated.
            bool bareMovedOk  = movedA > 0.5f;   // A actually moved (sanity)
            bool woodFasterThanBare  = movedB > movedA * 1.15f;
            bool stoneFasterThanWood = movedC > movedB * 1.08f;
            bool stoneFasterThanBare = movedC > movedA * 1.40f;

            bool allPass = bareMovedOk && woodFasterThanBare && stoneFasterThanWood && stoneFasterThanBare;

            Assert(allPass,
                $"V59b stone>wood>bare: " +
                $"movedA(bare)={movedA:F3} " +
                $"movedB(wood)={movedB:F3}(B/A={movedB / Mathf.Max(0.01f, movedA):F3}x >=1.15 ok={woodFasterThanBare}) " +
                $"movedC(stone)={movedC:F3}(C/B={movedC / Mathf.Max(0.01f, movedB):F3}x >=1.08 ok={stoneFasterThanWood} C/A={movedC / Mathf.Max(0.01f, movedA):F3}x >=1.40 ok={stoneFasterThanBare}) " +
                $"bonusAt: bare={bonusAtBare:F3} wood={bonusAtWood:F3} stone={bonusAtStone:F3} " +
                $"bareOk={bareMovedOk} " +
                $"wiki Dim3/V59b FloorEntity.MoveBonus(wood=1.30x)/StoneFloorEntity.MoveBonus(stone=1.50x)");
        }
    }
}
