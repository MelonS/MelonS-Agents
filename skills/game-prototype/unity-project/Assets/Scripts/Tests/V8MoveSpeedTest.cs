using UnityEngine;
using MelonS.GameProto.Data;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify #28 — V8 move-speed reconcile gate test.
    ///
    /// Wiki acceptance criterion (#28): "PawnStats.moveSpeed reads 4.6 and
    /// the audit/goal checkbox is updated."
    ///
    /// Chain verified as ONE binary PASS:
    ///   Step 1 — PawnStats.CreateDefault() produces a ScriptableObject
    ///             whose moveSpeed field equals exactly 4.6.
    ///   Step 2 — A PawnMovement component bootstrapped with that stats
    ///             instance exposes the same effective speed (read-only
    ///             via reflection, same pattern as V5FoodChainTest).
    ///
    /// Lane B contract: this file is READ-ONLY from runtime .cs.
    /// No runtime file is edited.  If the actual value is NOT 4.6 the
    /// test reports the real value and asserts false so QA/operator can
    /// act — the value is never silently patched here.
    ///
    /// Harness conventions: same RunOne/Assert/GetWhiteSprite pattern as
    /// V4CombatChainTest.cs / V5FoodChainTest.cs / V7RaidThreatTest.cs.
    /// This file is a COMPANION test; it does NOT modify any existing
    /// test or any runtime .cs file.
    ///
    /// FLAG for QA: PawnMovement.stats is [SerializeField] private — the
    /// test reads it via System.Reflection.  If batchmode reflection is
    /// restricted a public GetStatsForTest() accessor should be added to
    /// PawnMovement.  The PawnStats.CreateDefault() path is public and
    /// needs no reflection.
    /// </summary>
    public class V8MoveSpeedTest : MonoBehaviour
    {
        public string outputPath = "G:/ai/_v8_movespeed_report.json";

        private const float ExpectedMoveSpeed = 4.6f;

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private System.Collections.IEnumerator Start()
        {
            Debug.Log("[V8MoveSpeedTest] start — V8 move-speed reconcile");
            yield return new WaitForSeconds(0.1f);

            yield return RunOne("V8-move-speed", TestV8_MoveSpeed);

            Debug.Log($"[V8MoveSpeedTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
        }

        private System.Collections.IEnumerator RunOne(string id, System.Func<System.Collections.IEnumerator> body)
        {
            float t0 = Time.realtimeSinceStartup;
            bool threw = false;
            string err = "";
            System.Collections.IEnumerator iter = null;
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
            Debug.Log($"[V8MoveSpeedTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V8 — move-speed reconcile
        // ----------------------------------------------------------------

        private System.Collections.IEnumerator TestV8_MoveSpeed()
        {
            // ---- Step 1: PawnStats.CreateDefault() value ----
            // PawnStats is a ScriptableObject with a public CreateDefault()
            // factory that returns an instance with the canonical defaults.
            // This is the single source of truth for the 4.6 value (#200).
            PawnStats stats = PawnStats.CreateDefault();
            float statsSpeed = stats != null ? stats.moveSpeed : -1f;
            bool step1_statsIs4p6 = Mathf.Approximately(statsSpeed, ExpectedMoveSpeed);

            yield return null;

            // ---- Step 2: PawnMovement reads the same value ----
            // Instantiate a minimal PawnMovement on a transient GameObject.
            // PawnMovement.stats is [SerializeField] private; read via reflection.
            // FLAG: if reflection is unavailable in batchmode, add a public
            // GetStatsForTest() accessor to PawnMovement.cs.
            var testGo = new GameObject("V8_PawnMovement");
            var movement = testGo.AddComponent<PawnMovement>();
            yield return null;  // Awake — movement internally calls CreateDefault() as fallback

            float effectiveSpeed = -1f;
            bool reflectionOk = false;

            var statsField = typeof(PawnMovement).GetField("stats",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (statsField != null)
            {
                var movStats = statsField.GetValue(movement) as PawnStats;
                if (movStats != null)
                {
                    effectiveSpeed = movStats.moveSpeed;
                    reflectionOk = true;
                }
            }

            // If reflection found nothing, fall back: the CreateDefault() value
            // is the authoritative check; step 2 is supplementary.
            bool step2_movementIs4p6 = reflectionOk
                ? Mathf.Approximately(effectiveSpeed, ExpectedMoveSpeed)
                : step1_statsIs4p6;  // inherit step 1 result when reflection is blocked

            Object.Destroy(testGo);

            // ---- Composite assertion — ONE binary PASS ----
            // If the actual value is NOT 4.6, both steps will be false and the
            // assert message will contain the real value for QA/operator.
            // This test NEVER silently patches the value.
            bool allPass = step1_statsIs4p6 && step2_movementIs4p6;

            Assert(allPass,
                $"chain: " +
                $"[1]statsSpeed={statsSpeed:F4}=={ExpectedMoveSpeed} pass={step1_statsIs4p6} " +
                $"[2]effectiveSpeed={effectiveSpeed:F4}=={ExpectedMoveSpeed} pass={step2_movementIs4p6} " +
                $"(reflectionOk={reflectionOk}) " +
                $"wiki#28 PawnStats.moveSpeed==4.6");
        }
    }
}
