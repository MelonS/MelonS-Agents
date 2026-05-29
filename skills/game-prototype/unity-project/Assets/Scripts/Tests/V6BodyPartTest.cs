using System.Collections;
using UnityEngine;
using MelonS.GameProto;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify V6 — body-part damage + bleed + downed/death gate test.
    ///
    /// Wiki acceptance criterion (V6 slate):
    ///   "A gated test asserts body-part damage -> part HP drops -> bleed ->
    ///   downed/death as ONE PASS line."
    ///
    /// Chain verified as ONE binary PASS:
    ///   Step 1 — TakeDamage(dmg, preferPart) targets the chosen part;
    ///             part.hp drops below its initial value.
    ///   Step 2 — part.bleedRate rises above zero (bleeding accrues on damage,
    ///             matching PawnHealth line: target.bleedRate += dmg * 0.25f).
    ///   Step 3a — Non-vital part (LeftLeg) damaged to 0 does NOT kill the pawn
    ///             (IsDead stays false), confirming non-vital zero-HP semantics.
    ///   Step 3b — Vital part (Head) damaged to 0: IsDead becomes true AND
    ///             IsDowned becomes true (CheckDeath sets both on head/torso HP==0).
    ///
    /// Harness conventions: same RunOne/Assert/report-path bootstrap as
    /// V4CombatChainTest.cs / V7RaidThreatTest.cs.  This file is a COMPANION
    /// test that feeds the same JSON report path pattern used by qa.py.
    /// It does NOT modify TestRunner.cs or any runtime .cs file.
    ///
    /// API gaps flagged for QA (see .claude/wb/v6-bodypart.json):
    ///   G1 — PawnHealth.parts[] is public and BodyPart.hp / bleedRate are public
    ///        fields — read directly, no reflection needed.
    ///   G2 — PawnHealth.TakeDamage(int, PartId?) is the existing public damage
    ///        API used by ArrowProjectile / BanditEnemy (same path combat uses).
    ///   G3 — PawnHealth requires [SerializeField] HealthPartsConfig; if the
    ///        ScriptableObject reference is not wired in the headless test scene,
    ///        Awake() falls back to HealthPartsConfig.CreateDefault() which
    ///        creates a runtime instance with the canonical defaults (head 20,
    ///        torso 40, limbs 18-20). No editor wiring is needed for this test.
    ///   G4 — IsDowned for PawnHealth is set via CheckDeath (private), which
    ///        fires inside TakeDamage. The test reads IsDowned as a public
    ///        property, no reflection needed.
    ///   G5 — The bleed tick in PawnHealth.Update fires once per second; the
    ///        test verifies bleedRate immediately after TakeDamage (before any
    ///        Update tick) so the assertion is frame-deterministic.
    /// </summary>
    public class V6BodyPartTest : MonoBehaviour
    {
        // Report path follows companion-test convention (V4=_v4_, V5=_v5_, V7=_v7_).
        public string outputPath = "G:/ai/_v6_bodypart_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V6BodyPartTest] start — V6 body-part dmg+bleed+downed/death");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V6-bodypart-chain", TestV6_BodyPartChain);

            Debug.Log($"[V6BodyPartTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V6BodyPartTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V6 — body-part damage + bleed + downed/death chain
        // ----------------------------------------------------------------

        private IEnumerator TestV6_BodyPartChain()
        {
            // ---- Spawn a pawn with PawnHealth ----
            // G3: Awake() falls back to HealthPartsConfig.CreateDefault() when the
            //     SO reference is not wired, so no editor asset is needed here.
            var pawnGo = new GameObject("V6_TestPawn");
            pawnGo.AddComponent<SpriteRenderer>();
            var health = pawnGo.AddComponent<PawnHealth>();
            // Allow Awake to run and initialize parts[].
            yield return null;

            // Confirm parts array was populated (should have 6 entries).
            bool partsInitialized = health.parts != null && health.parts.Length == 6;

            // ----------------------------------------------------------------
            // Step 1 + Step 2 — Part HP drops; bleedRate rises
            // Target: LeftLeg (non-vital, maxHp 20). Damage 10 (=50% of maxHp).
            // Expected after TakeDamage:
            //   part.hp == maxHp - 10 = 10  (<  initial 20) — Step 1
            //   part.bleedRate == 10 * 0.25f = 2.5f          — Step 2
            // ----------------------------------------------------------------
            const PawnHealth.PartId targetPart = PawnHealth.PartId.LeftLeg;
            const int dmgStep1 = 10;

            PawnHealth.BodyPart legPart = health.GetPart(targetPart);
            int legInitialHp = legPart.hp;       // expected: 20 (HealthPartsConfig default)
            float bleedBefore = legPart.bleedRate; // expected: 0 before any damage

            // Apply damage via the existing public combat API (same path used by
            // ArrowProjectile and BanditEnemy).
            PawnHealth.BodyPart affected = health.TakeDamage(dmgStep1, targetPart);
            yield return null;

            int legHpAfterDmg = legPart.hp;
            float bleedAfterDmg = legPart.bleedRate;

            bool step1_hpDropped = (legHpAfterDmg < legInitialHp);
            bool step2_bleedAccrued = (bleedAfterDmg > bleedBefore);

            // ----------------------------------------------------------------
            // Step 3a — Non-vital part at 0 HP does NOT kill pawn
            // Drain LeftLeg fully (remaining HP after step 1 = legHpAfterDmg).
            // ----------------------------------------------------------------
            int dmgStep3a = legHpAfterDmg + 5;   // exceed remaining HP
            health.TakeDamage(dmgStep3a, targetPart);
            yield return null;

            int legHpAfterDrain = health.GetPart(targetPart).hp;  // should be 0
            bool step3a_nonVitalZeroNoDeath = (legHpAfterDrain == 0) && !health.IsDead;

            // ----------------------------------------------------------------
            // Step 3b — Vital part (Head) at 0 HP → IsDead + IsDowned both true
            // Head maxHp = 20 (HealthPartsConfig default). Apply 25 damage (> maxHp)
            // routed to head; CheckDeath sets IsDead = true, IsDowned = true.
            // ----------------------------------------------------------------
            const PawnHealth.PartId headPart = PawnHealth.PartId.Head;
            int headInitialHp = health.GetPart(headPart).hp;  // still 20 (undamaged)
            const int dmgStep3b = 25;   // guaranteed to reduce head to 0

            health.TakeDamage(dmgStep3b, headPart);
            yield return null;

            int headHpAfterLethal = health.GetPart(headPart).hp;  // expected: 0
            bool step3b_headAtZero = (headHpAfterLethal == 0);
            bool step3b_isDead     = health.IsDead;
            bool step3b_isDowned   = health.IsDowned;

            // ---- Cleanup ----
            if (pawnGo != null) Object.Destroy(pawnGo);

            // ---- Composite assertion — ONE binary PASS ----
            bool allPass = partsInitialized
                        && step1_hpDropped
                        && step2_bleedAccrued
                        && step3a_nonVitalZeroNoDeath
                        && step3b_headAtZero
                        && step3b_isDead
                        && step3b_isDowned;

            Assert(allPass,
                $"chain: " +
                $"[init]partsOk={partsInitialized}(len={health.parts?.Length ?? -1}) " +
                $"[1]hpDropped={step1_hpDropped}({legInitialHp}->{legHpAfterDmg},dmg={dmgStep1}) " +
                $"[2]bleedAccrued={step2_bleedAccrued}(before={bleedBefore:F2}->after={bleedAfterDmg:F2}) " +
                $"[3a]nonVitalNoDeath={step3a_nonVitalZeroNoDeath}(legHp={legHpAfterDrain},isDead={health.IsDead}) " +
                $"[3b]headLethal={step3b_headAtZero}(headHp={headInitialHp}->{headHpAfterLethal},dmg={dmgStep3b})" +
                $" isDead={step3b_isDead} isDowned={step3b_isDowned}");
        }
    }
}
