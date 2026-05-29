using System.Collections;
using System.Reflection;
using UnityEngine;
using MelonS.GameProto;
using MelonS.GameProto.Core;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify #25 — V5 food chain gate test.
    ///
    /// Wiki acceptance criterion: "A single gated test asserts the full food
    /// chain raises a pawn's food need."
    ///
    /// Chain verified as ONE binary PASS:
    ///   Step 1 — crop plant exists (CropEntity spawned, growth = 0)
    ///   Step 2 — crop ripens (growth forced to 1.0 via reflection, IsRipe == true)
    ///   Step 3 — harvest yields raw food (crop.Harvest() → ResourceManager.food rises)
    ///   Step 4 — cook at stove converts food to meal (stove.CookOne() → meals rises)
    ///   Step 5 — pawn eats (PawnNeeds.food low + meals available → food need rises
    ///             after one Update cycle, driven by PawnNeeds.TryEatTick via Update)
    ///
    /// Harness conventions: same RunOne/Assert/SpawnTestPawn pattern as V4CombatChainTest.cs
    /// and TestRunner.cs.  This file is a COMPANION test that does NOT modify
    /// TestRunner.cs or any runtime .cs file.
    ///
    /// API gaps flagged for QA (see .claude/wb/v5-food-chain-verify.json):
    ///   G1 — ResearchManager.Instance may be null in the headless test scene;
    ///        the test bootstraps one when absent (same pattern as V4CombatChainTest).
    ///   G2 — ResourceManager.Instance routes through Services.Get; the test
    ///        bootstraps one when absent so Harvest() and CookOne() can register food.
    ///   G3 — CropEntity.growth is [SerializeField] private; ripening is forced via
    ///        reflection (same pattern as TestRunner.TestV5_CropHarvest and V48).
    ///   G4 — PawnNeeds.TryEatTick is private and fires automatically from Update.
    ///        The test sets pawn.food below eatThreshold (40) and waits 1 s for
    ///        Update to trigger the eat tick.  There is no public ForceEat() method;
    ///        if QA finds the tick does not fire in headless test scenes (Time.deltaTime
    ///        stalls), a public ForceEat() accessor should be added to PawnNeeds.
    ///   G5 — PawnNeeds requires GameClock.Instance to decide IsNightTime(); without
    ///        it the night-check returns false (IsNightTime returns false when
    ///        GameClock.Instance == null), so the eating path still executes normally.
    ///        WeatherController is not referenced during the eat path, so it can be absent.
    ///   G6 — StoveEntity.CanCookOne() requires ResourceManager.food >= 3.  The test
    ///        ensures the crop harvest deposits enough raw food (harvestFood default = 8)
    ///        before calling CookOne().
    /// </summary>
    public class V5FoodChainTest : MonoBehaviour
    {
        // Shared report path — matches the companion-test convention (V4 uses _v4_, this uses _v5_).
        public string outputPath = "G:/ai/_v5_food_chain_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V5FoodChainTest] start — V5 food chain");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V5-food-chain", TestV5_FullFoodChain);

            Debug.Log($"[V5FoodChainTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V5FoodChainTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V5 — full food chain
        // ----------------------------------------------------------------

        private IEnumerator TestV5_FullFoodChain()
        {
            // ---- G1: Bootstrap ResearchManager if absent ----
            if (ResearchManager.Instance == null)
            {
                var rmGo = new GameObject("V5_ResearchManager");
                rmGo.AddComponent<ResearchManager>();
                yield return null;
            }

            // ---- G2: Bootstrap ResourceManager if absent ----
            // ResourceManager routes via Services.Get.  In the headless test scene
            // there may be no ResourceManager present; create one so Harvest() and
            // CookOne() can credit food/meals.
            ResourceManager rm = ResourceManager.Instance;
            if (rm == null)
            {
                var rmGo = new GameObject("V5_ResourceManager");
                rmGo.AddComponent<ResourceManager>();
                yield return null;
                rm = ResourceManager.Instance;
            }

            // Snapshot resource state before the chain so we can detect deltas even
            // if prior tests left residual food/meals in the manager.
            int foodBefore = rm.food;
            int mealsBefore = rm.meals;

            // ---- Step 1 — plant: spawn CropEntity with growth = 0 ----
            // Position: (200, 0, 0) — far from prior test objects.
            var cropGo = new GameObject("V5_Crop");
            cropGo.transform.position = new Vector3(200f, 0f, 0f);
            cropGo.AddComponent<SpriteRenderer>();
            var crop = cropGo.AddComponent<CropEntity>();
            yield return null;  // Awake + Start → RefreshVisual

            bool step1_planted = crop != null && !crop.IsRipe;

            // ---- Step 2 — ripen: force growth to 1.0 via reflection ----
            // G3: CropEntity.growth is SerializeField private.
            // Same pattern used by TestRunner.TestV5_CropHarvest (line 1382-1384) and
            // TestV48_CropColorStage.
            var growthField = typeof(CropEntity).GetField("growth",
                BindingFlags.NonPublic | BindingFlags.Instance);
            growthField.SetValue(crop, 1.0f);
            yield return null;  // allow Update to see new growth (IsRipe = growth >= 1f)

            bool step2_ripe = crop.IsRipe;

            // ---- Step 3 — harvest: crop.Harvest() returns harvestFood (default 8) ----
            // Harvest() calls ResourceManager.Instance.AddFood(harvestFood).
            int gained = crop.Harvest();
            yield return null;

            int foodAfterHarvest = rm.food;
            bool step3_harvested = gained > 0 && foodAfterHarvest > foodBefore;

            // ---- Step 4 — cook: stove.CookOne() converts 3 raw food → 1 meal ----
            // G6: CanCookOne() requires food >= 3; harvest deposited 8 so this is satisfied.
            var stoveGo = new GameObject("V5_Stove");
            stoveGo.transform.position = new Vector3(201f, 0f, 0f);
            stoveGo.AddComponent<SpriteRenderer>();
            stoveGo.AddComponent<BoxCollider2D>();
            var stove = stoveGo.AddComponent<StoveEntity>();
            yield return null;  // Awake

            bool canCook = stove.CanCookOne();
            bool cooked = stove.CookOne();
            yield return null;

            int mealsAfterCook = rm.meals;
            bool step4_cooked = cooked && mealsAfterCook > mealsBefore;

            // ---- Step 5 — pawn eats: food need rises after eat tick ----
            // Spawn a pawn with PawnNeeds.  Set food below eatThreshold (default 40)
            // so TryEatTick fires in the next Update.  meals is now >= 1 so the cook-
            // meal branch runs: food += eatRestore (default 30), mood += 10.
            // G4: TryEatTick is private and fires from PawnNeeds.Update.
            var pawnGo = new GameObject("V5_Pawn");
            pawnGo.transform.position = new Vector3(202f, 0f, 0f);
            pawnGo.AddComponent<SpriteRenderer>();
            pawnGo.AddComponent<BoxCollider2D>();
            pawnGo.AddComponent<PawnEntity>();
            var pawnNeeds = pawnGo.AddComponent<PawnNeeds>();
            yield return null;  // Awake

            // Force food low so TryEatTick triggers (food < eatThreshold 40).
            // G5: GameClock.Instance == null in headless test → IsNightTime() returns
            // false → IsSleeping stays false → Update reaches TryEatTick normally.
            float foodBeforeEat = 20f;
            pawnNeeds.food = foodBeforeEat;

            // Wait enough frames for PawnNeeds.Update to execute TryEatTick.
            // The eat tick has a 0.5 s interval guard (eatTickInterval); we wait 1 s
            // to guarantee at least one tick fires.
            yield return new WaitForSeconds(1.0f);

            float foodAfterEat = pawnNeeds.food;
            // The pawn's food need should have risen by eatRestore (30) minus the
            // foodDecay (0.14/s × 1s ≈ 0.14) that also ran during the wait.
            // Net rise must be > 0 compared to the starting low value.
            bool step5_foodRose = foodAfterEat > foodBeforeEat;

            // ---- Cleanup ----
            Object.Destroy(cropGo);
            Object.Destroy(stoveGo);
            Object.Destroy(pawnGo);

            // ---- Composite assertion — ONE binary PASS ----
            bool allPass = step1_planted && step2_ripe && step3_harvested
                        && step4_cooked  && step5_foodRose;

            Assert(allPass,
                $"chain: " +
                $"[1]planted={step1_planted} " +
                $"[2]ripe={step2_ripe} " +
                $"[3]harvested={step3_harvested}(gained={gained},food {foodBefore}->{foodAfterHarvest}) " +
                $"[4]cooked={step4_cooked}(canCook={canCook},meals {mealsBefore}->{mealsAfterCook}) " +
                $"[5]foodRose={step5_foodRose}(food {foodBeforeEat:F1}->{foodAfterEat:F1})");
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
            _whiteSprite.name = "V5WhiteSprite";
            return _whiteSprite;
        }
    }
}
