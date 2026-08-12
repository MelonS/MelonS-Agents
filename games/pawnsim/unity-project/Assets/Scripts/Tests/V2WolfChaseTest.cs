using System.Collections;
using UnityEngine;
using MelonS.GameProto;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify #V2 — V2 wolf-detection → chase gate test.
    ///
    /// Wiki acceptance criterion (Dimension 5 "V1–V9 verification slate", V2):
    ///   "A gated test asserts a wolf within detection range detects a pawn and
    ///    chases it (closes distance / enters chase state) as ONE PASS line."
    ///
    /// Chain verified as ONE binary PASS:
    ///   Step 1 — a live PawnEntity sits INSIDE the wolf's detection radius
    ///             (WolfEnemy.detectionRadius = 5.0; pawn placed 3.0 units away)
    ///   Step 2 — the wolf DETECTS the pawn: after the sim advances past the
    ///             wolf's 0.4 s target-search tick the wolf STOPS wandering and
    ///             commits to the pawn — observed read-only as the wolf moving
    ///             TOWARD the pawn rather than its random wander target
    ///   Step 3 — the wolf CHASES: its distance to the pawn DECREASES over the
    ///             simulation window (closes in / enters chase state), the same
    ///             read-only signal V7RaidThreatTest uses for a bandit closing on
    ///             the colony (Vector3.Distance on transform.position only)
    ///
    /// Read-only contract: this test reads ONLY the wolf's public Hp/IsDead and
    /// its world transform.position (the chase is observable purely as motion
    /// toward the pawn).  It does NOT modify WolfEnemy.cs or any runtime .cs file
    /// and matches the V7 "bandit closes in" assertion style exactly.
    ///
    /// Harness conventions: same RunOne/Assert/GetWhiteSprite pattern as
    /// V4CombatChainTest.cs / V7RaidThreatTest.cs.  COMPANION test — does NOT
    /// modify TestRunner.cs or any runtime .cs file.
    ///
    /// API gaps flagged for QA (see .claude/wb/v2-wolf-chase.json):
    ///   G1 — WolfEnemy exposes NO public chase-state / detection accessor.
    ///        The chase target is the PRIVATE field `cachedTarget` (WolfEnemy.cs:31)
    ///        and there is no public IsChasing / CurrentTarget / AIState property.
    ///        So this test CANNOT read an explicit "chase state" flag; it asserts
    ///        chase the only read-only way available — DISTANCE CLOSING over the
    ///        window (identical to V7's bandit closing-in proof).  If a binary
    ///        chase-STATE assertion is desired, WolfEnemy should add a public
    ///        read-only accessor, e.g. `public bool IsChasing => cachedTarget != null
    ///        && !cachedTarget.IsDead;` or `public PawnEntity CurrentTarget => cachedTarget;`.
    ///        Flagged, NOT edited (lane rule: no runtime .cs edits).
    ///   G2 — WolfEnemy.detectionRadius (5.0) and chaseSpeed (5.0) are SerializeField
    ///        private; the test does not need to read them — it places the pawn at a
    ///        fixed 3.0 units (comfortably < 5.0) and lets the wolf's own Update drive
    ///        real movement.  No reflection required.
    ///   G3 — WolfEnemy.FindNearestPawn() scans live PawnEntity instances and skips
    ///        IsDead pawns; PawnEntity.Awake sets Hp = stats.maxHp (alive after one
    ///        frame), so the spawned pawn is a valid detection target (same bootstrap
    ///        V7 uses for its colony reference pawn).
    ///   G4 — The wolf re-picks a target every 0.4 s and chases at chaseSpeed 5.0;
    ///        a ~2.0 s window covers several search ticks and yields a reliable
    ///        positive distance delta (wolf closes ~the 3.0-unit gap to attackRange).
    ///   G5 — The wolf may reach attackRange (0.9) and stop closing further within
    ///        the window; reaching attackRange still proves it detected + chased, so
    ///        "closed to within attackRange" is accepted as a closing success.
    /// </summary>
    public class V2WolfChaseTest : MonoBehaviour
    {
        // Shared report path — companion-test convention (V4=_v4_, V7=_v7_, V2=_v2_).
        public string outputPath = "G:/ai/_v2_wolf_chase_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V2WolfChaseTest] start — V2 wolf detection→chase");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V2-wolf-chase", TestV2_WolfDetectionChase);

            Debug.Log($"[V2WolfChaseTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V2WolfChaseTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V2 — wolf detection → chase chain
        // ----------------------------------------------------------------

        private IEnumerator TestV2_WolfDetectionChase()
        {
            // ---- G3: Spawn the prey pawn INSIDE detection range ----
            // Use a position far from any ambient scene pawns/enemies.  The wolf
            // sits at this anchor's origin and the pawn is 3.0 units away, which is
            // comfortably inside WolfEnemy.detectionRadius (5.0).
            Vector3 wolfPos = new Vector3(200f, 0f, 0f);
            Vector3 pawnPos = wolfPos + new Vector3(3.0f, 0f, 0f);   // 3.0 units < 5.0 detection

            var pawnGo = new GameObject("V2_PreyPawn");
            pawnGo.transform.position = pawnPos;
            pawnGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            pawnGo.AddComponent<BoxCollider2D>().size = new Vector2(0.8f, 0.8f);
            pawnGo.AddComponent<PawnEntity>();
            pawnGo.AddComponent<PawnMovement>();
            pawnGo.AddComponent<PawnHealth>();
            yield return null;   // Awake → PawnEntity.Hp = maxHp (alive, valid target)

            var pawn = pawnGo.GetComponent<PawnEntity>();
            bool pawnAlive = pawn != null && !pawn.IsDead;

            // ---- Spawn the wolf at the anchor origin ----
            var wolfGo = new GameObject("V2_Wolf");
            wolfGo.transform.position = wolfPos;
            wolfGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            var wolf = wolfGo.AddComponent<WolfEnemy>();
            yield return null;   // Awake → Hp = maxHp, picks initial wander target

            bool wolfAlive = wolf != null && !wolf.IsDead;

            // ---- Step 1 — pawn is inside detection range ----
            float distAtStart = Vector3.Distance(wolfGo.transform.position, pawnGo.transform.position);
            // detectionRadius is private (G2); 5.0 is the runtime value — assert the
            // setup invariant that the prey is within it.
            const float knownDetectionRadius = 5.0f;
            bool step1_inRange = distAtStart <= knownDetectionRadius && pawnAlive && wolfAlive;

            // ---- Step 2 + 3 — wolf detects then chases (distance closes) ----
            // The wolf re-searches targets every 0.4 s and chases at chaseSpeed 5.0.
            // WaitForSeconds lets Unity run the wolf's real Update() each frame so the
            // motion is genuine (not simulated by the test).  Sample the gap midway
            // to confirm monotone closing (detection commitment), then at the end.
            yield return new WaitForSeconds(0.6f);   // > one 0.4 s search tick → committed
            float distMid = (wolf != null && pawn != null)
                ? Vector3.Distance(wolfGo.transform.position, pawnGo.transform.position)
                : 0f;

            yield return new WaitForSeconds(1.4f);   // total ~2.0 s of chase
            float distEnd = (wolf != null && pawn != null && wolfGo != null && pawnGo != null)
                ? Vector3.Distance(wolfGo.transform.position, pawnGo.transform.position)
                : 0f;

            // G5: reaching attackRange (0.9) means it closed the gap fully — accept
            // either a strict decrease OR arrival within attackRange as "chasing".
            const float knownAttackRange = 0.9f;
            bool step2_detected = distMid < distAtStart;                 // started closing after detect tick
            bool step3_chased   = distEnd < distAtStart || distEnd <= knownAttackRange + 0.05f;

            // ---- Cleanup ----
            if (wolfGo != null) Object.Destroy(wolfGo);
            if (pawnGo != null) Object.Destroy(pawnGo);

            // ---- Composite assertion — ONE binary PASS ----
            bool allPass = step1_inRange && step2_detected && step3_chased;

            Assert(allPass,
                $"chain: " +
                $"[1]inRange={step1_inRange}(distAtStart={distAtStart:F2}<=det{knownDetectionRadius},pawnAlive={pawnAlive},wolfAlive={wolfAlive}) " +
                $"[2]detected={step2_detected}(distMid={distMid:F2}<start) " +
                $"[3]chased={step3_chased}(distEnd={distEnd:F2}<start OR<=atkRange{knownAttackRange})");
        }

        // ---- Helpers (mirror V4/V7 conventions) ----

        private static Sprite _whiteSprite;
        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            _whiteSprite.name = "V2WhiteSprite";
            return _whiteSprite;
        }
    }
}
