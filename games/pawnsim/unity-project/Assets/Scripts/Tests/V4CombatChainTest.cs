using System.Collections;
using UnityEngine;
using MelonS.GameProto;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify #26 — V4 combat chain gate test.
    ///
    /// Wiki acceptance criterion: "A gated test asserts a drafted arrow reduces
    /// enemy HP to death."
    ///
    /// Chain verified as ONE binary PASS:
    ///   Step 1 — pawn is drafted (IsDrafted == true)
    ///   Step 2 — pawn fires an arrow (ArrowProjectile count rises in ranged window)
    ///   Step 3 — arrow hit applies damage (bandit.Hp drops below initial)
    ///   Step 4 — at Hp 0 the enemy is downed/dead (bandit.IsDead == true)
    ///
    /// Harness conventions: same RunOne/Assert/SpawnTestPawn/GetWhiteSprite pattern
    /// as TestRunner.cs.  This file is a COMPANION test that feeds the same JSON
    /// report path used by qa.py.  It does NOT modify TestRunner.cs or any runtime
    /// .cs file.
    ///
    /// API gaps flagged for QA (see .claude/wb/v4-combat-chain-verify.json):
    ///   G1 — ResearchManager.Instance may be null in the headless test scene;
    ///        the test bootstraps one when absent.
    ///   G2 — PawnUtilityAI.arrowSprite is SerializeField private; the public
    ///        SetArrowSprite() accessor covers this so no reflection is needed.
    ///   G3 — BanditEnemy has no RequireComponent(Rigidbody2D); the test adds one
    ///        manually so ArrowProjectile.OnTriggerEnter2D fires reliably.
    ///   G4 — BanditEnemy.maxHp is SerializeField private (default 20); the test
    ///        pre-damages the bandit to leave it at 4 HP so a single arrow hit
    ///        (damage 4) is sufficient for a guaranteed kill within the 3 s window.
    ///        This exercises the full chain without requiring 5+ arrow cycles.
    /// </summary>
    public class V4CombatChainTest : MonoBehaviour
    {
        // Shared report path matches TestRunner / IntegrationTestRunner convention.
        public string outputPath = "G:/ai/_v4_combat_chain_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V4CombatChainTest] start — V4 combat chain");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V4-combat-chain", TestV4_FullCombatChain);

            Debug.Log($"[V4CombatChainTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V4CombatChainTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V4 — full combat chain
        // ----------------------------------------------------------------

        private IEnumerator TestV4_FullCombatChain()
        {
            // ---- Bootstrap services ----
            // G1: ResearchManager.Instance may be absent in the isolated test scene.
            if (ResearchManager.Instance == null)
            {
                var rmGo = new GameObject("V4_ResearchManager");
                rmGo.AddComponent<ResearchManager>();
                yield return null;
            }

            // Unlock simple_bow so PawnUtilityAI.HandleDraftedCombat enters the
            // ranged path (canShoot = true).
            var rm = ResearchManager.Instance;
            if (rm != null)
            {
                foreach (var tech in rm.techs)
                    if (tech.id == "simple_bow") { tech.completed = true; break; }
            }

            // ---- Spawn pawn with AI (includeAI=true wires SetArrowSprite) ----
            // Position: (100, 0, 0) — far from any ambient enemies in the scene.
            Vector3 pawnPos = new Vector3(100f, 0f, 0f);
            GameObject pawnGo = SpawnTestPawn(pawnPos, includeAI: true);
            var pawn = pawnGo.GetComponent<PawnEntity>();
            var ai   = pawnGo.GetComponent<PawnUtilityAI>();

            // G2: arrowSprite accessed via the public SetArrowSprite accessor.
            ai.SetArrowSprite(GetWhiteSprite());

            // ---- Spawn enemy (BanditEnemy) at ranged distance ----
            // Place bandit at (103, 0): distance = 3 units, which satisfies
            // attackRange(1.2) < d <= RangedAttackRange(5.0).  The arrow travels
            // at speed 8 so it crosses 3 units in ~0.375 s.
            Vector3 banditPos = new Vector3(103f, 0f, 0f);
            GameObject banditGo = new GameObject("V4_Bandit");
            banditGo.transform.position = banditPos;
            banditGo.AddComponent<SpriteRenderer>();
            // G3: BanditEnemy needs Rigidbody2D + BoxCollider2D (trigger) so
            // ArrowProjectile.OnTriggerEnter2D fires on physics overlap.
            var banditRb  = banditGo.AddComponent<Rigidbody2D>();
            banditRb.gravityScale = 0f;
            banditRb.bodyType = RigidbodyType2D.Kinematic;
            var banditCol = banditGo.AddComponent<BoxCollider2D>();
            banditCol.size = new Vector2(0.8f, 0.8f);
            banditCol.isTrigger = false;   // solid so CircleCollider2D on arrow triggers
            var bandit = banditGo.AddComponent<BanditEnemy>();
            yield return null;   // Awake → Hp = maxHp (20)

            // G4: Pre-damage bandit to Hp = 4 (one arrow hit of 4 damage = death).
            // This guarantees the kill within a single RangedAttackInterval (1.5 s)
            // without modifying runtime code.
            int initialHp = bandit.Hp;                 // 20 (maxHp default)
            int preDamage = initialHp - 4;             // 16 damage → leaves Hp = 4
            bandit.TakeDamage(preDamage, null);
            yield return null;
            int hpAfterPreDamage = bandit.Hp;          // expected: 4

            // ---- Step 1 — Draft the pawn ----
            pawn.SetDrafted(true);
            yield return null;
            bool step1_drafted = pawn.IsDrafted;

            // ---- Set attack target ----
            pawn.DraftedAttackTarget = bandit;

            // Count arrows before the first shot.
            int arrowsBefore = CountArrows();

            // ---- Step 2 — Wait for first arrow to spawn ----
            // PawnUtilityAI.HandleDraftedCombat fires on Update; RangedAttackInterval=1.5 s.
            // Allow 2.5 s so the first shot fires and travels to the bandit.
            float deadline = Time.realtimeSinceStartup + 2.5f;
            int arrowsPeak = arrowsBefore;
            bool step2_arrowFired = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                int cur = CountArrows();
                if (cur > arrowsBefore) { arrowsPeak = cur; step2_arrowFired = true; }
                yield return null;
            }

            // ---- Step 3 — HP dropped below initial (pre-damage) level ----
            // The arrow hit applies 4 damage.  If bandit is already dead the HP
            // accessed before Destroy fires may still be valid in this frame.
            int hpAfterShot = bandit != null ? bandit.Hp : 0;
            bool step3_hpDropped = hpAfterShot < hpAfterPreDamage || (bandit == null || bandit.IsDead);

            // ---- Step 4 — Enemy downed / dead at HP 0 ----
            // Allow one extra frame for Destroy to propagate.
            yield return new WaitForSeconds(0.2f);
            bool step4_dead = bandit == null || bandit.IsDead;

            // ---- Cleanup ----
            if (banditGo != null) Object.Destroy(banditGo);
            if (pawnGo   != null) Object.Destroy(pawnGo);

            // ---- Composite assertion ----
            // Each step must PASS independently; the test is binary PASS only when
            // the full chain completes.
            bool allPass = step1_drafted && step2_arrowFired && step3_hpDropped && step4_dead;
            Assert(allPass,
                $"chain: " +
                $"[1]drafted={step1_drafted} " +
                $"[2]arrowFired={step2_arrowFired}(arrowsPeak={arrowsPeak},before={arrowsBefore}) " +
                $"[3]hpDropped={step3_hpDropped}(preDmgHp={hpAfterPreDamage}->afterShot={hpAfterShot}) " +
                $"[4]dead={step4_dead} " +
                $"| initHp={initialHp} preDmg={preDamage}");
        }

        // ---- Helpers (mirrors TestRunner.cs conventions) ----

        private GameObject SpawnTestPawn(Vector3 pos, bool includeAI = true)
        {
            var go = new GameObject("V4_TestPawn");
            go.transform.position = pos;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<PawnEntity>();
            go.AddComponent<PawnMovement>();
            go.AddComponent<PawnHealth>();
            go.AddComponent<PawnChopper>();
            if (includeAI)
            {
                var ai = go.AddComponent<PawnUtilityAI>();
                ai.SetArrowSprite(GetWhiteSprite());
            }
            return go;
        }

        private static Sprite _whiteSprite;
        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            _whiteSprite.name = "V4ArrowSprite";
            return _whiteSprite;
        }

        private static int CountArrows()
        {
            return Object.FindObjectsByType<ArrowProjectile>(FindObjectsSortMode.None).Length;
        }
    }
}
