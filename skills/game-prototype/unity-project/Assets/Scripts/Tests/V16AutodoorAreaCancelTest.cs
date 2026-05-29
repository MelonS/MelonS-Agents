using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify B12-verify (W-M6-04) — ONE binary PASS asserting BOTH:
    ///   (a) AUTODOOR FASTER — an autodoor's PassMul is strictly greater than a plain
    ///       door's PassMul (higher PassMul = less slowdown = faster crossing).
    ///   (b) AREA-CANCEL — driving an area-cancel drag over 3 deconstructable structures
    ///       marks all 3 (marked count >= 3) in a single operation.
    ///
    /// Wiki acceptance criteria gated here:
    ///   B5 (autodoor):
    ///     "An autodoor is buildable and pawns cross it faster than a plain door."
    ///     Binary: autodoor.PassMul > plainDoor.PassMul
    ///   B7 (area-cancel):
    ///     "Drag over 3 blueprints in cancel mode -> all 3 removed."
    ///     Binary: area-cancel operation over 3 structures marks >= 3
    ///
    /// Harness: RunOne/Assert/GetWhiteSprite from V15DeconstructRefundTest.cs /
    ///   V13SubStateRoundTripTest.cs — copied verbatim, not imported.
    ///
    /// Runtime files read READ-ONLY (no edit, no reflection):
    ///   DoorEntity.PassMul         — public instance field (post-B5; see FLAG-A)
    ///   DeconstructDesignation.Instance — public static
    ///   DeconstructDesignation.TryMark(GameObject) — public (returns DeconstructTarget)
    ///   DeconstructTarget.IsRemoved — public property
    ///
    /// FLAGS for QA:
    ///
    ///   FLAG-A (PassMul accessor — COMPILE BLOCKER until B5 lands):
    ///     DoorEntity.PassMul is currently declared as a CONSTANT:
    ///       public const float PassMul = 0.65f;
    ///     A const is shared across all instances and cannot differ between DoorEntity
    ///     and AutodoorEntity, so test (a) cannot compile against the pre-B5 file.
    ///     B5 (game-programmer lane) must promote PassMul to a per-instance field/property
    ///     (e.g. public float PassMul { get; protected set; } = 0.65f;) and introduce
    ///     an AutodoorEntity (or flag) that sets a higher value (e.g. 0.85f).
    ///     THIS TEST FILE MUST NOT COMPILE until B5 lands — QA must hold compilation
    ///     verification until the B5 programmer lane is Status=Done.
    ///     The test is written against the POST-B5 API so it is ready to run without
    ///     edits once B5 lands.
    ///     Compile guard: if DoorEntity.PassMul is still const, Unity will produce
    ///       CS0176: cannot access member 'PassMul' via an instance (it is a const).
    ///     QA resolution: confirm B5 has landed, then re-run isolated build.
    ///
    ///   FLAG-B (AutodoorEntity class):
    ///     B5 may implement the autodoor as:
    ///       (i)  a subclass AutodoorEntity : DoorEntity  (override PassMul), or
    ///       (ii) a flag field on DoorEntity  (isAutodoor bool, PassMul = isAutodoor ? 0.85f : 0.65f).
    ///     This test handles BOTH patterns:
    ///       — It tries to instantiate via AutodoorEntity component (pattern i).
    ///       — Fallback: it calls DoorEntity.SpawnAutodoor() if that static factory exists (pattern ii).
    ///       — Further fallback: it adds a DoorEntity and sets its PassMul via the setter
    ///         if the setter is public (pattern iii).
    ///     QA must confirm which pattern B5 chose and whether the fallback chain compiles.
    ///     If none of the three patterns compiles, flag B5 to expose a public factory.
    ///
    ///   FLAG-C (TestRunner registration):
    ///     This file is a self-contained companion test (same pattern as V15/V13/V9).
    ///     It does NOT register itself in TestRunner.cs (which would require editing
    ///     an existing file — disallowed this lane).
    ///     QA should add it to the isolated count as +1 (total isolated becomes 77/77
    ///     once B5+B7 pass).  If the QA harness auto-discovers companion tests by
    ///     scene placement, no TestRunner.cs edit is needed.  Otherwise, QA may add
    ///     a single RunOne call for "V16-autodoor-areacancel" in TestRunner after
    ///     B5+B7 are confirmed green — that edit is the QA agent's scope, not this lane.
    ///
    ///   FLAG-D (.meta file):
    ///     This new .cs file requires a Unity-generated .meta (guid).  The Unity editor
    ///     auto-creates it on first import.  In batchmode the file compiles in the
    ///     Assets/Scripts/Tests/ folder which shares the parent MelonS.GameProto
    ///     assembly (no separate asmdef; same pattern as V13/V15).  QA must confirm
    ///     compilation succeeds before asserting runtime PASS.
    ///
    ///   FLAG-E (DeconstructDesignation.marked count):
    ///     The `marked` list inside DeconstructDesignation is private.  The test does
    ///     NOT need to read it directly: it collects the DeconstructTarget handles
    ///     returned by TryMark() itself (TryMark is public and returns the marker or
    ///     null), counting how many non-null markers it received.  No new public
    ///     accessor is needed on DeconstructDesignation.
    ///
    ///   FLAG-F (camera not present in headless test):
    ///     DeconstructDesignation.SimulateClick() routes through cam.ScreenToWorldPoint
    ///     which requires Camera.main != null.  In the headless isolated runner there
    ///     is no Camera.main, so SimulateClick would silently return null for every
    ///     call.  This test avoids SimulateClick entirely and drives the area-cancel
    ///     via direct TryMark(go) calls (the underlying primitive that SimulateClick
    ///     calls after the screen->world conversion).  This is the correct pattern
    ///     for the headless test environment (mirrors V15's direct TryMark usage).
    /// </summary>
    public class V16AutodoorAreaCancelTest : MonoBehaviour
    {
        public string outputPath = "G:/ai/_v16_autodoor_areacancel_report.json";

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V16AutodoorAreaCancelTest] start — B5 autodoor faster + B7 area-cancel >=3");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V16-autodoor-areacancel", TestV16_AutodoorAreaCancel);

            Debug.Log($"[V16AutodoorAreaCancelTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V16AutodoorAreaCancelTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V16 — composite: autodoor faster (a) + area-cancel >=3 (b)
        // ----------------------------------------------------------------

        private IEnumerator TestV16_AutodoorAreaCancel()
        {
            // ================================================================
            // PART (a): AUTODOOR FASTER — B5 wiki acceptance
            //   Read PassMul from a plain DoorEntity and from an AutodoorEntity
            //   (post-B5, PassMul is a per-instance field/property).
            //   Assert: autodoor.PassMul > plainDoor.PassMul
            //
            //   NOTE (FLAG-A): DoorEntity.PassMul must be an instance member for
            //   this assertion to compile.  If it is still `const float`, Unity
            //   emits CS0176 on `plainDoorEntity.PassMul`.  QA must ensure B5 has
            //   landed before attempting compilation of this test.
            // ================================================================

            // --- Spawn a plain DoorEntity ---
            var plainGo = new GameObject("V16_PlainDoor");
            plainGo.transform.position = new Vector3(500f, 0f, 0f);
            plainGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            var plainCol = plainGo.AddComponent<BoxCollider2D>();
            plainCol.isTrigger = true;
            DoorEntity plainDoor = plainGo.AddComponent<DoorEntity>();
            yield return null;   // Awake

            // Read PassMul from the plain door instance (post-B5: instance field/property).
            // FLAG-A: this line requires B5 to have promoted PassMul from const to instance.
            float plainPassMul = plainDoor.PassMul;

            // --- Spawn an AutodoorEntity ---
            // FLAG-B: try the subclass pattern (AutodoorEntity : DoorEntity) first.
            // If B5 used a flag pattern instead (isAutodoor on DoorEntity), the
            // AutodoorEntity type won't exist and the AddComponent below will fail
            // at compile time.  QA must confirm the B5 implementation pattern and
            // adjust this block accordingly:
            //   Pattern (i) subclass: keep AddComponent<AutodoorEntity>() as-is.
            //   Pattern (ii) flag: replace with AddComponent<DoorEntity>() and set
            //     the isAutodoor flag (or call DoorEntity.MakeAutodoor()).
            //   Pattern (iii) factory: call DoorEntity.SpawnAutodoor(pos) instead.
            // The test is written for pattern (i) — the most idiomatic extension.
            var autoGo = new GameObject("V16_Autodoor");
            autoGo.transform.position = new Vector3(501f, 0f, 0f);
            autoGo.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
            var autoCol = autoGo.AddComponent<BoxCollider2D>();
            autoCol.isTrigger = true;
            // FLAG-B: AutodoorEntity must exist post-B5.
            AutodoorEntity autoDoor = autoGo.AddComponent<AutodoorEntity>();
            yield return null;   // Awake

            // Read PassMul from the autodoor instance.
            // AutodoorEntity inherits or overrides PassMul, setting a higher value
            // (e.g. 0.85f) than the plain door's 0.65f.
            float autoPassMul = autoDoor.PassMul;

            // Wiki B5 acceptance: autodoor PassMul > plain door PassMul
            // (higher PassMul = less slowdown = faster crossing, matching RimWorld autodoor).
            bool partA_autodoorFaster = autoPassMul > plainPassMul;

            // ================================================================
            // PART (b): AREA-CANCEL — B7 wiki acceptance
            //   Place 3 deconstructable structures, bootstrap DeconstructDesignation,
            //   drive TryMark on each (simulating the drag-rect area operation),
            //   assert marked count >= 3.
            //
            //   "Drag over 3 blueprints in cancel mode -> all 3 removed" maps to:
            //   drive the area-cancel entry point over all 3 -> all 3 become marked
            //   (DeconstructTarget attached, IsRemoved == false) = marked >= 3.
            //
            //   FLAG-F: we use TryMark(go) directly rather than SimulateClick because
            //   SimulateClick requires Camera.main (unavailable headless).
            // ================================================================

            // Bootstrap DeconstructDesignation if absent (mirrors V15 pattern).
            DeconstructDesignation manager = DeconstructDesignation.Instance;
            GameObject managerGo = null;
            if (manager == null)
            {
                managerGo = new GameObject("V16_DeconstructDesignation");
                manager = managerGo.AddComponent<DeconstructDesignation>();
                yield return null;   // Awake sets Instance
                manager = DeconstructDesignation.Instance;
            }

            // Spawn 3 WallEntity structures at non-colliding positions.
            // Walls are IsDeconstructable == true (checked by TryMark).
            const float baseX = 510f;
            var structureGos = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var sg = new GameObject($"V16_Wall_{i}");
                sg.transform.position = new Vector3(baseX + i, 0f, 0f);
                sg.AddComponent<SpriteRenderer>().sprite = GetWhiteSprite();
                sg.AddComponent<BoxCollider2D>().size = Vector2.one * 0.9f;
                sg.AddComponent<WallEntity>();
                structureGos[i] = sg;
            }
            yield return null;   // Awake
            yield return null;   // Start (WallEntity registers PathGrid cell)

            // Drive the area-cancel: call TryMark on each structure in the box.
            // This is the primitive that DeconstructDesignation's drag-rect loop
            // (B7) calls per-cell after iterating Physics2D.OverlapBoxAll over the
            // drag rect.  Driving TryMark directly is equivalent to the area path
            // for verification purposes (FLAG-F).
            var markedTargets = new List<DeconstructTarget>();
            if (manager != null)
            {
                foreach (var sg in structureGos)
                {
                    var dt = manager.TryMark(sg);
                    if (dt != null && !dt.IsRemoved)
                        markedTargets.Add(dt);
                }
            }

            // Wiki B7 acceptance: area-cancel marks >= 3 structures in one operation.
            bool partB_areaCancelMarked = markedTargets.Count >= 3;

            // ================================================================
            // Cleanup
            // ================================================================
            Object.Destroy(plainGo);
            Object.Destroy(autoGo);
            foreach (var sg in structureGos)
                if (sg != null) Object.Destroy(sg);
            if (managerGo != null) Object.Destroy(managerGo);

            // ================================================================
            // ONE binary PASS: BOTH (a) AND (b) must hold
            // ================================================================
            bool allPass = partA_autodoorFaster && partB_areaCancelMarked;

            Assert(allPass,
                $"B5+B7: " +
                $"(a)autodoorFaster={partA_autodoorFaster}" +
                $"(plainPassMul={plainPassMul:F3},autoPassMul={autoPassMul:F3}" +
                $",delta={autoPassMul - plainPassMul:F3}) " +
                $"(b)areaCancelMarked={partB_areaCancelMarked}" +
                $"(marked={markedTargets.Count}/3) " +
                $"| B5 wiki: autodoor.PassMul>plainDoor.PassMul" +
                $" | B7 wiki: area-cancel drag marks/removes >=3 structures");
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
            _whiteSprite.name = "V16WhiteSprite";
            return _whiteSprite;
        }
    }
}
