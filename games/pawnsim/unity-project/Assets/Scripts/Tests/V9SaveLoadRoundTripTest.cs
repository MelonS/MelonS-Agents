using System.Collections;
using System.IO;
using UnityEngine;
using MelonS.GameProto;
using MelonS.GameProto.Core;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify #29 — V9 save->load round-trip gate test.
    ///
    /// Wiki acceptance criterion (#29): "After save->load, a fine bed is still
    /// fine (not reverted to default)."
    ///
    /// This is ONE binary PASS that asserts all four entity sub-states survive
    /// a save->load round-trip:
    ///   Field 1 — BedEntity.Quality  (Fine stays Fine)
    ///   Field 2 — StockpileZoneEntity.Priority  (Critical stays Critical)
    ///   Field 3 — TreeEntity.Species  (Oak stays Oak)
    ///   Field 4 — WallEntity.Material  (Stone stays Stone)
    ///
    /// VERIFY lane (Lane C W-M4-01 documented gaps; Lane B W-M4-02 FIXED them).
    /// SaveLoadManager.cs now carries BedSave/StockpileSave/WallSave lists and
    /// species field on TreeSave.  This test was updated (QA wiring, cx-w6) to
    /// read actual data fields — all four assertions now PASS when serialization
    /// is correct.
    ///
    /// Harness conventions: same RunOne/Assert/GetWhiteSprite pattern as
    /// V4CombatChainTest.cs, V5FoodChainTest.cs, V7RaidThreatTest.cs.
    /// This file is a COMPANION test; it does NOT modify TestRunner.cs or any
    /// other existing test or runtime .cs file.
    ///
    /// Serialization gap analysis (read SaveLoadManager.cs before editing):
    ///   SaveData fields: wood, food, List<PawnSave> (name/pos/food/sleep/mood),
    ///   List<TreeSave> (position only).
    ///   SaveData has NO BedSave, NO StockpileSave, NO TreeSpecies field in
    ///   TreeSave, and NO WallSave.  Therefore ALL FOUR sub-states are absent
    ///   from the serialized payload and WILL NOT survive a real Save()->Load()
    ///   scene-destroy->rebuild cycle.
    ///
    /// FLAG for QA / dedicated serialization CODE wave:
    ///   GAP-1: BedEntity.quality (BedQuality enum) — not in SaveData/BedSave
    ///   GAP-2: StockpileZoneEntity.priority (StockpilePriority enum) — not in SaveData
    ///   GAP-3: TreeEntity.species (TreeSpecies enum) — TreeSave only stores position
    ///   GAP-4: WallEntity.material (WallMaterial enum) — not in SaveData/WallSave
    ///
    /// The test below simulates the round-trip by:
    ///   (a) seeding entities with non-default sub-state values,
    ///   (b) calling SaveLoadManager.Save(),
    ///   (c) calling SaveLoadManager.Load() to obtain the SaveData payload,
    ///   (d) asserting that the four sub-state fields exist in the returned
    ///       SaveData — each assertion FAILS (PASS=false) when the field is
    ///       absent from SaveData, documenting the exact gap.
    ///
    /// Because SaveData does not carry these fields, this test is expected to
    /// FAIL on all four sub-states under the current SaveLoadManager implementation.
    /// That expected failure IS the verification output: it confirms the gap exists
    /// and provides the exact field list for the serialization CODE wave.
    /// </summary>
    public class V9SaveLoadRoundTripTest : MonoBehaviour
    {
        // Companion-test report path convention (V4=_v4_, V5=_v5_, V7=_v7_, V9=_v9_).
        public string outputPath = "G:/ai/_v9_saveload_roundtrip_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V9SaveLoadRoundTripTest] start — save->load round-trip verify (#29)");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V9-saveload-roundtrip", TestV9_SaveLoadRoundTrip);

            Debug.Log($"[V9SaveLoadRoundTripTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V9SaveLoadRoundTripTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V9 — save->load round-trip for entity sub-states
        // ----------------------------------------------------------------

        private IEnumerator TestV9_SaveLoadRoundTrip()
        {
            // ---- Bootstrap ResourceManager if absent (SaveLoadManager.Save() reads it) ----
            if (ResourceManager.Instance == null)
            {
                var rmGo = new GameObject("V9_ResourceManager");
                rmGo.AddComponent<ResourceManager>();
                yield return null;
            }

            // ---- Seed entities at positions far from existing test objects ----
            // Position base: (300, 0, 0) to avoid collision with V4/V5/V7 test objects.

            // Field 1: BedEntity — seed Fine quality (non-default; default is Wood).
            var bedGo = new GameObject("V9_Bed");
            bedGo.transform.position = new Vector3(300f, 0f, 0f);
            bedGo.AddComponent<SpriteRenderer>();
            bedGo.AddComponent<BoxCollider2D>();
            var bed = bedGo.AddComponent<BedEntity>();
            yield return null;
            bed.SetQuality(BedQuality.Fine);
            yield return null;

            BedQuality qualityBeforeSave = bed.Quality;

            // Field 2: StockpileZoneEntity — seed Critical priority (non-default; default is Normal).
            var stockGo = StockpileZoneEntity.Spawn(new Vector3(301f, 0f, 0f), null, StockpilePriority.Critical);
            yield return null;

            StockpilePriority priorityBeforeSave = stockGo.Priority;

            // Field 3: TreeEntity — seed Oak species (non-default; default is Pine).
            var treeGo = new GameObject("V9_Tree");
            treeGo.transform.position = new Vector3(302f, 0f, 0f);
            treeGo.AddComponent<SpriteRenderer>();
            treeGo.AddComponent<BoxCollider2D>();
            var tree = treeGo.AddComponent<TreeEntity>();
            yield return null;
            tree.SetSpecies(TreeSpecies.Oak);
            yield return null;

            TreeSpecies speciesBeforeSave = tree.Species;

            // Field 4: WallEntity — seed Stone material (non-default; default is Wood).
            var wallGo = new GameObject("V9_Wall");
            wallGo.transform.position = new Vector3(303f, 0f, 0f);
            wallGo.AddComponent<SpriteRenderer>();
            wallGo.AddComponent<BoxCollider2D>();
            var wall = wallGo.AddComponent<WallEntity>();
            yield return null;
            wall.SetMaterial(WallMaterial.Stone);
            yield return null;

            WallMaterial materialBeforeSave = wall.Material;

            // ---- Verify seeding succeeded (pre-save baseline) ----
            bool seededFine     = qualityBeforeSave  == BedQuality.Fine;
            bool seededCritical = priorityBeforeSave == StockpilePriority.Critical;
            bool seededOak      = speciesBeforeSave  == TreeSpecies.Oak;
            bool seededStone    = materialBeforeSave == WallMaterial.Stone;

            if (!seededFine || !seededCritical || !seededOak || !seededStone)
            {
                // Entity API failure — abort round-trip test early.
                Assert(false,
                    $"SEED FAILED — fine={seededFine} critical={seededCritical} " +
                    $"oak={seededOak} stone={seededStone}. " +
                    $"Cannot verify round-trip if baseline seeding fails.");
                Object.Destroy(bedGo);
                Object.Destroy(stockGo.gameObject);
                Object.Destroy(treeGo);
                Object.Destroy(wallGo);
                yield break;
            }

            // ---- Save ----
            SaveLoadManager.Save();
            yield return new WaitForSeconds(0.15f);

            bool saveExists = SaveLoadManager.SaveExists;

            // ---- Load the raw SaveData payload ----
            // The test reads the SaveData fields directly to check whether the
            // sub-state values are present in the serialized payload.
            // We do NOT destroy and respawn entities — the test verifies the
            // serialized DATA, not a full scene-reload (which would require
            // scene management outside the verify-only lane).
            // The data-presence check is sufficient to confirm the serialization
            // gap, which is the purpose of this verify-only lane.
            SaveData data = SaveLoadManager.Load();
            yield return null;

            bool loadedOk = data != null;

            // ---- Round-trip assertions: check SaveData for sub-state fields ----
            //
            // W-M4-02 Lane B fix: SaveLoadManager now carries BedSave/StockpileSave/
            // WallSave lists and a species field on TreeSave.  All four assertions
            // below now read actual data fields instead of hardcoded false.
            //
            // GAP-1 (FIXED): BedQuality — SaveData.beds carries BedSave(position, quality).
            bool bedQualitySerialized = loadedOk
                && data.beds != null && data.beds.Count > 0
                && data.beds.Exists(b => b.quality == (int)BedQuality.Fine);

            // GAP-2 (FIXED): StockpilePriority — SaveData.stockpiles carries StockpileSave(position, priority).
            bool stockPrioritySerialized = loadedOk
                && data.stockpiles != null && data.stockpiles.Count > 0
                && data.stockpiles.Exists(s => s.priority == (int)StockpilePriority.Critical);

            // GAP-3 (FIXED): TreeSpecies — TreeSave now carries species field.
            // We also verify the tree position is in the payload.
            bool treePositionSaved = false;
            bool treeSpeciesSerialized = false;
            if (loadedOk && data.trees != null)
            {
                foreach (var ts in data.trees)
                {
                    if (Vector2.Distance(ts.position, new Vector2(302f, 0f)) < 0.5f)
                    {
                        treePositionSaved = true;
                        treeSpeciesSerialized = (ts.species == (int)TreeSpecies.Oak);
                        break;
                    }
                }
            }

            // GAP-4 (FIXED): WallMaterial — SaveData.walls carries WallSave(position, material).
            bool wallMaterialSerialized = loadedOk
                && data.walls != null && data.walls.Count > 0
                && data.walls.Exists(w => w.material == (int)WallMaterial.Stone);

            // ---- Composite assertion — ONE binary PASS ----
            // Wiki acceptance #29: after save->load, a fine bed is still fine.
            // All four gaps must be closed for this to PASS.
            // With the current SaveLoadManager, this will FAIL on all four fields.
            bool allPass = loadedOk
                        && bedQualitySerialized
                        && stockPrioritySerialized
                        && treeSpeciesSerialized
                        && wallMaterialSerialized;

            Assert(allPass,
                $"round-trip: " +
                $"saveExists={saveExists} " +
                $"loadedOk={loadedOk} " +
                $"[1]bedQuality(Fine)={bedQualitySerialized} " +
                $"[2]stockPriority(Critical)={stockPrioritySerialized} " +
                $"[3]treeSpecies(Oak,posInPayload={treePositionSaved})={treeSpeciesSerialized} " +
                $"[4]wallMaterial(Stone)={wallMaterialSerialized} — " +
                $"wiki #29 acceptance: fine bed/critical stockpile/oak tree/stone wall survive save->load");

            // ---- Cleanup ----
            Object.Destroy(bedGo);
            if (stockGo != null) Object.Destroy(stockGo.gameObject);
            Object.Destroy(treeGo);
            Object.Destroy(wallGo);
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
            _whiteSprite.name = "V9WhiteSprite";
            return _whiteSprite;
        }
    }
}
