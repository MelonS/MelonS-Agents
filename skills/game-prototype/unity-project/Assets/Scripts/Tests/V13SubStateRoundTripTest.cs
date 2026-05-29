using System.Collections;
using UnityEngine;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify B13 — entity sub-state save->load round-trip gate test.
    ///
    /// Wiki acceptance criterion (B13):
    ///   "After save->load, a Fine bed is still Fine AND a tier-1 stockpile is
    ///    still tier-1" — ONE binary PASS line.
    ///
    /// This test is a NEW companion file for W-M6-02 B13.  It does NOT edit
    /// V9SaveLoadRoundTripTest.cs or any other existing test or runtime file.
    ///
    /// What is verified as ONE binary PASS:
    ///   [1] BedEntity.Quality   Fine    round-trips through Save()->Load()
    ///   [2] StockpileZoneEntity.Priority Normal (tier-1, enum value 1) round-trips
    ///   [3] TreeEntity.Species  Oak     round-trips (cheaply assertable, same serializer)
    ///   [4] WallEntity.Material Stone   round-trips (cheaply assertable, same serializer)
    ///
    /// The test validates the DATA payload only — it calls SaveLoadManager.Save(),
    /// then SaveLoadManager.Load(), and inspects the returned SaveData lists.
    /// A full scene-destroy->rebuild cycle is QA's domain (Unity batchmode); this
    /// test is intentionally data-only so it stays within the verify lane.
    ///
    /// Harness conventions: RunOne/Assert/GetWhiteSprite pattern from
    /// V15DeconstructRefundTest.cs and V9SaveLoadRoundTripTest.cs.
    ///
    /// Serialization confirmed in SaveLoadManager.cs (W-M4-02 #29 fix):
    ///   SaveData.beds        List<BedSave>       (position + quality int)
    ///   SaveData.stockpiles  List<StockpileSave> (position + priority int)
    ///   SaveData.trees       List<TreeSave>      (position + species int)
    ///   SaveData.walls       List<WallSave>      (position + material int)
    ///
    /// ApplyLoadedSubStates wiring note (FLAG for QA):
    ///   SaveLoadManager.ApplyLoadedSubStates(data) must be called by the scene-
    ///   reload host (GameManager or equivalent) after scene entities are rebuilt
    ///   from SaveData.  This test verifies serialization only; the load-path
    ///   wiring (who calls ApplyLoadedSubStates on scene-reload) is outside this
    ///   lane and must be confirmed by QA in the live game scene.
    ///
    /// .meta flag for QA:
    ///   This new file requires a Unity-generated .meta (guid).  The Unity editor
    ///   auto-generates it on first import.  In batchmode the file is compiled if
    ///   it lands in the Assets/Scripts/Tests/ folder that shares the parent
    ///   MelonS.GameProto assembly (no separate asmdef).  QA must confirm
    ///   compilation succeeds before asserting runtime PASS.
    /// </summary>
    public class V13SubStateRoundTripTest : MonoBehaviour
    {
        public string outputPath = "G:/ai/_v13_substate_roundtrip_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V13SubStateRoundTripTest] start — B13 sub-state save->load round-trip");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V13-substate-roundtrip", TestV13_SubStateRoundTrip);

            Debug.Log($"[V13SubStateRoundTripTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V13SubStateRoundTripTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V13 — B13 sub-state round-trip: Fine bed + tier-1 stockpile
        //        + Oak tree + Stone wall all survive Save()->Load()
        // ----------------------------------------------------------------

        private IEnumerator TestV13_SubStateRoundTrip()
        {
            // Bootstrap ResourceManager so SaveLoadManager.Save() can read resources.
            GameObject rmGo = null;
            if (ResourceManager.Instance == null)
            {
                rmGo = new GameObject("V13_ResourceManager");
                rmGo.AddComponent<ResourceManager>();
                yield return null;
            }

            // Positions at (400, 0) to avoid collision with V4/V5/V7/V9/V15 test objects.

            // [1] BedEntity — seed Fine quality (wiki acceptance: Fine bed stays Fine).
            var bedGo = new GameObject("V13_Bed");
            bedGo.transform.position = new Vector3(400f, 0f, 0f);
            bedGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            bedGo.AddComponent<BoxCollider2D>();
            var bed = bedGo.AddComponent<BedEntity>();
            yield return null;
            bed.SetQuality(BedQuality.Fine);
            yield return null;

            // [2] StockpileZoneEntity — seed Normal priority (tier-1, enum value 1).
            // Wiki acceptance: tier-1 stockpile is still tier-1 after save->load.
            var stockGo = StockpileZoneEntity.Spawn(new Vector3(401f, 0f, 0f), null,
                StockpilePriority.Normal);
            yield return null;

            // [3] TreeEntity — seed Oak species (cheaply assertable via same int-cast serializer).
            var treeGo = new GameObject("V13_Tree");
            treeGo.transform.position = new Vector3(402f, 0f, 0f);
            treeGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            treeGo.AddComponent<BoxCollider2D>();
            var tree = treeGo.AddComponent<TreeEntity>();
            yield return null;
            tree.SetSpecies(TreeSpecies.Oak);
            yield return null;

            // [4] WallEntity — seed Stone material (cheaply assertable via same int-cast serializer).
            var wallGo = new GameObject("V13_Wall");
            wallGo.transform.position = new Vector3(403f, 0f, 0f);
            wallGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            wallGo.AddComponent<BoxCollider2D>();
            var wall = wallGo.AddComponent<WallEntity>();
            yield return null;
            wall.SetMaterial(WallMaterial.Stone);
            yield return null;

            // Verify seeding succeeded before exercising Save.
            bool seededFine   = bed.Quality == BedQuality.Fine;
            bool seededNormal = stockGo.Priority == StockpilePriority.Normal;
            bool seededOak    = tree.Species == TreeSpecies.Oak;
            bool seededStone  = wall.Material == WallMaterial.Stone;

            if (!seededFine || !seededNormal || !seededOak || !seededStone)
            {
                Assert(false,
                    $"SEED FAILED — fine={seededFine} normal={seededNormal} " +
                    $"oak={seededOak} stone={seededStone}. " +
                    $"Entity public setters did not apply correctly.");
                Cleanup(bedGo, stockGo, treeGo, wallGo, rmGo);
                yield break;
            }

            // Save — writes all four lists to Application.persistentDataPath/save.json.
            SaveLoadManager.Save();
            yield return new WaitForSeconds(0.15f);

            bool saveFileExists = SaveLoadManager.SaveExists;

            // Load — reads the JSON back and returns the SaveData payload.
            SaveData data = SaveLoadManager.Load();
            yield return null;

            bool loadedOk = data != null;

            // [1] BedQuality.Fine round-trip: SaveData.beds must contain an entry
            //     at position (400, 0) with quality == (int)BedQuality.Fine == 2.
            bool bedFineRoundTrip = loadedOk
                && data.beds != null
                && data.beds.Exists(b =>
                    Vector2.Distance(b.position, new Vector2(400f, 0f)) < 0.6f
                    && b.quality == (int)BedQuality.Fine);

            // [2] StockpilePriority.Normal (tier-1) round-trip: SaveData.stockpiles
            //     must contain an entry near (401, 0) with priority == 1.
            bool stockNormalRoundTrip = loadedOk
                && data.stockpiles != null
                && data.stockpiles.Exists(s =>
                    Vector2.Distance(s.position, new Vector2(401f, 0f)) < 0.6f
                    && s.priority == (int)StockpilePriority.Normal);

            // [3] TreeSpecies.Oak round-trip: TreeSave.species == (int)TreeSpecies.Oak == 2.
            bool treeOakRoundTrip = false;
            if (loadedOk && data.trees != null)
            {
                foreach (var ts in data.trees)
                {
                    if (Vector2.Distance(ts.position, new Vector2(402f, 0f)) < 0.6f
                        && ts.species == (int)TreeSpecies.Oak)
                    {
                        treeOakRoundTrip = true;
                        break;
                    }
                }
            }

            // [4] WallMaterial.Stone round-trip: WallSave.material == (int)WallMaterial.Stone == 1.
            bool wallStoneRoundTrip = loadedOk
                && data.walls != null
                && data.walls.Exists(w =>
                    Vector2.Distance(w.position, new Vector2(403f, 0f)) < 0.6f
                    && w.material == (int)WallMaterial.Stone);

            Cleanup(bedGo, stockGo, treeGo, wallGo, rmGo);

            // ONE binary PASS — wiki B13 acceptance:
            //   Fine bed is still Fine AND tier-1 stockpile is still tier-1
            //   (plus Oak tree and Stone wall cheaply confirmed via same serializer).
            bool allPass = saveFileExists
                        && loadedOk
                        && bedFineRoundTrip
                        && stockNormalRoundTrip
                        && treeOakRoundTrip
                        && wallStoneRoundTrip;

            Assert(allPass,
                $"round-trip: " +
                $"saveExists={saveFileExists} loadedOk={loadedOk} " +
                $"[1]bedFine(pos=400,0)={bedFineRoundTrip} " +
                $"[2]stockNormal/tier-1(pos=401,0)={stockNormalRoundTrip} " +
                $"[3]treeOak(pos=402,0)={treeOakRoundTrip} " +
                $"[4]wallStone(pos=403,0)={wallStoneRoundTrip} " +
                $"| B13 wiki: Fine bed stays Fine, tier-1 stockpile stays tier-1");
        }

        private static void Cleanup(GameObject bedGo, StockpileZoneEntity stockGo,
            GameObject treeGo, GameObject wallGo, GameObject rmGo)
        {
            if (bedGo != null) Object.Destroy(bedGo);
            if (stockGo != null) Object.Destroy(stockGo.gameObject);
            if (treeGo != null) Object.Destroy(treeGo);
            if (wallGo != null) Object.Destroy(wallGo);
            if (rmGo != null) Object.Destroy(rmGo);
        }

        private static Sprite _whiteSprite;
        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            _whiteSprite.name = "V13WhiteSprite";
            return _whiteSprite;
        }
    }
}
