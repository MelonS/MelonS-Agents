using System.Collections;
using System.Reflection;
using UnityEngine;
using MelonS.GameProto;
using MelonS.GameProto.Core;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify #27 — V7 raid-threat gate test.
    ///
    /// Wiki acceptance criterion: "A gated test asserts a bandit spawns day 3
    /// and moves toward the colony."
    ///
    /// Chain verified as ONE binary PASS:
    ///   Step 1 — GameClock is at Day 3 / Hour 6 (the director mode raid window)
    ///   Step 2 — AIDirector.SpawnRaid() executes (forced via reflection), producing
    ///             at least one BanditEnemy in the scene
    ///   Step 3 — the spawned bandit's distance to the colony reference point
    ///             DECREASES over a short simulation window (bandit closes in)
    ///
    /// Harness conventions: same RunOne/Assert/GetWhiteSprite pattern as
    /// V4CombatChainTest.cs and V5FoodChainTest.cs.  This file is a COMPANION
    /// test that does NOT modify TestRunner.cs or any runtime .cs file.
    ///
    /// API gaps flagged for QA (see .claude/wb/buildengineer.json):
    ///   G1 — GameClock.GameSeconds is { private set }; advancing the clock to
    ///        Day 3 Hour 6 requires System.Reflection (same pattern as V5's
    ///        CropEntity.growth field set). If batchmode reflection fails, a
    ///        public SetGameSecondsForTest(float) accessor should be added to
    ///        GameClock.
    ///   G2 — AIDirector.SpawnRaid() is private; invoking the actual raid-spawn
    ///        path requires System.Reflection. If batchmode reflection fails, a
    ///        public ForceRaidForTest() accessor should be added to AIDirector.
    ///   G3 — No public AIDirector.ForceRaid() exists; the test drives the raid
    ///        path through reflection on SpawnRaid() rather than through the
    ///        natural TryScheduleRaid() polling path (which would require waiting
    ///        real time for Update() to fire at exactly hour=6).
    ///   G4 — AIDirector.SpawnRaid() internally calls AudioBank.Instance?.PlayAlert()
    ///        which may be null in the headless test scene; the null-guard in
    ///        SpawnRaid() already handles this gracefully (no crash expected).
    ///   G5 — BanditEnemy.FindNearestPawn() uses FindObjectsByType which requires
    ///        at least one live PawnEntity in the scene; the test spawns one at the
    ///        origin (colony reference) before triggering the raid so the bandit has
    ///        a valid target to move toward.
    /// </summary>
    public class V7RaidThreatTest : MonoBehaviour
    {
        // Shared report path — companion-test convention (V4=_v4_, V5=_v5_, V7=_v7_).
        public string outputPath = "G:/ai/_v7_raid_threat_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V7RaidThreatTest] start — V7 raid threat");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V7-raid-threat", TestV7_RaidThreat);

            Debug.Log($"[V7RaidThreatTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V7RaidThreatTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V7 — raid threat chain
        // ----------------------------------------------------------------

        private IEnumerator TestV7_RaidThreat()
        {
            // ---- G1: Bootstrap GameClock if absent, advance to Day 3 Hour 6 ----
            // GameClock.Instance routes through Services.Get<GameClock>().
            // Day 3 requires GameSeconds in [172800, 259200) — use day=3, hour=6:
            //   GameSeconds = (3-1)*86400 + 6*3600 = 172800 + 21600 = 194400
            // GameClock.GameSeconds has { private set } — must use reflection.
            GameClock clock = GameClock.Instance;
            if (clock == null)
            {
                var clockGo = new GameObject("V7_GameClock");
                clockGo.AddComponent<GameClock>();
                yield return null;
                clock = GameClock.Instance;
            }

            const float day3Hour6Seconds = (2f * 86400f) + (6f * 3600f); // = 194400
            bool clockAdvanced = false;
            if (clock != null)
            {
                // G1: private set requires reflection
                var gsField = typeof(GameClock).GetProperty("GameSeconds",
                    BindingFlags.Public | BindingFlags.Instance);
                if (gsField != null && gsField.CanWrite)
                {
                    gsField.SetValue(clock, day3Hour6Seconds);
                    clockAdvanced = true;
                }
                else
                {
                    // Try backing field (compiler-generated for auto-property)
                    var backingField = typeof(GameClock).GetField("<GameSeconds>k__BackingField",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (backingField != null)
                    {
                        backingField.SetValue(clock, day3Hour6Seconds);
                        clockAdvanced = true;
                    }
                }
            }
            yield return null;

            // Verify clock reports Day=3 after the advance.
            int clockDay = (clock != null) ? clock.Day : -1;
            int clockHour = (clock != null) ? clock.Hour : -1;
            bool step1_dayThree = clockDay == 3;

            // ---- G5: Spawn colony reference pawn at origin ----
            // BanditEnemy.FindNearestPawn() scans for live PawnEntity instances.
            // The test spawns one at (0,0,0) as the colony anchor so the bandit
            // has a valid target to path toward.
            // Position far from where raid bandits will spawn (raidSpawnRadius=12).
            var colonyGo = new GameObject("V7_ColonyPawn");
            colonyGo.transform.position = Vector3.zero;   // colony reference point
            colonyGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            colonyGo.AddComponent<BoxCollider2D>().size = new Vector2(0.8f, 0.8f);
            colonyGo.AddComponent<PawnEntity>();
            colonyGo.AddComponent<PawnMovement>();
            colonyGo.AddComponent<PawnHealth>();
            yield return null;  // Awake

            // ---- Bootstrap AIDirector if absent ----
            AIDirector director = Object.FindFirstObjectByType<AIDirector>();
            bool directorWasAbsent = (director == null);
            if (director == null)
            {
                var dirGo = new GameObject("V7_AIDirector");
                director = dirGo.AddComponent<AIDirector>();
                yield return null;  // Awake + Start (ScheduleNext called)
            }

            // Ensure the director uses Steady (deterministic tier at day 3 = tier 1).
            director.directorMode = DirectorMode.Steady;

            // Count bandits before invoking the raid path.
            int banditsBefore = CountBandits();

            // ---- Step 2 — Invoke AIDirector.SpawnRaid() via reflection ----
            // G2/G3: SpawnRaid() is private; no public ForceRaid() exists.
            // The method signature is: private void SpawnRaid()
            bool spawnRaidInvoked = false;
            var spawnRaidMethod = typeof(AIDirector).GetMethod("SpawnRaid",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (spawnRaidMethod != null)
            {
                spawnRaidMethod.Invoke(director, null);
                spawnRaidInvoked = true;
            }
            // Allow one frame for the spawned GameObjects to complete Awake.
            yield return null;
            yield return null;

            int banditsAfter = CountBandits();
            bool step2_banditSpawned = banditsAfter > banditsBefore;

            // ---- Step 3 — Bandit moves TOWARD colony (distance closes) ----
            // Find the bandit(s) spawned in step 2 and record their starting
            // distance to the colony reference point (origin).
            // BanditEnemy.moveSpeed = 1.2 units/s; over a 2s window it covers
            // ~2.4 units toward the target, which is a reliable positive delta.
            Vector3 colonyPos = Vector3.zero;
            BanditEnemy[] banditsNow = Object.FindObjectsByType<BanditEnemy>(FindObjectsSortMode.None);
            float distAtSpawn = float.MaxValue;
            BanditEnemy trackedBandit = null;
            foreach (var b in banditsNow)
            {
                if (b == null) continue;
                float d = Vector3.Distance(b.transform.position, colonyPos);
                if (d < distAtSpawn) { distAtSpawn = d; trackedBandit = b; }
            }

            bool step3_closingIn = false;
            float distAfterSim = distAtSpawn;

            if (trackedBandit != null)
            {
                // Simulate 2 seconds of movement — BanditEnemy.Update moves toward
                // the nearest PawnEntity (our V7_ColonyPawn at origin) each frame.
                // WaitForSeconds lets Unity run real Update() calls.
                yield return new WaitForSeconds(2.0f);

                if (trackedBandit != null)  // may be destroyed if it reached contact range
                {
                    distAfterSim = Vector3.Distance(trackedBandit.transform.position, colonyPos);
                    step3_closingIn = distAfterSim < distAtSpawn;
                }
                else
                {
                    // Bandit was destroyed — it reached the colony pawn (contact range
                    // < 1.0) which means it closed the entire distance: still counts as
                    // "moved toward colony."
                    step3_closingIn = true;
                    distAfterSim = 0f;
                }
            }

            // ---- Cleanup ----
            // Remove all V7 test bandits so they don't pollute other tests.
            foreach (var b in Object.FindObjectsByType<BanditEnemy>(FindObjectsSortMode.None))
            {
                if (b != null && b.gameObject.name.StartsWith("Bandit_Raid_"))
                    Object.Destroy(b.gameObject);
            }
            if (colonyGo != null) Object.Destroy(colonyGo);
            if (directorWasAbsent && director != null) Object.Destroy(director.gameObject);

            // ---- Composite assertion — ONE binary PASS ----
            bool allPass = step1_dayThree && step2_banditSpawned && step3_closingIn;

            Assert(allPass,
                $"chain: " +
                $"[1]day3={step1_dayThree}(clock.Day={clockDay},hour={clockHour},advanced={clockAdvanced},gs={day3Hour6Seconds}) " +
                $"[2]banditSpawned={step2_banditSpawned}(before={banditsBefore},after={banditsAfter},reflectionOk={spawnRaidInvoked}) " +
                $"[3]closingIn={step3_closingIn}(distAtSpawn={distAtSpawn:F2},distAfterSim={distAfterSim:F2})");
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
            _whiteSprite.name = "V7WhiteSprite";
            return _whiteSprite;
        }

        private static int CountBandits()
        {
            return Object.FindObjectsByType<BanditEnemy>(FindObjectsSortMode.None).Length;
        }
    }
}
