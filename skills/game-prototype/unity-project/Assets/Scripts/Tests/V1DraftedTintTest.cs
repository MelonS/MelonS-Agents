using System.Collections;
using UnityEngine;
using MelonS.GameProto;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// M5-verify #V1 — V1 drafted-tint gate test.
    ///
    /// Wiki acceptance criterion (genre-comparison.md Dim 5 'V1-V9 verification
    /// slate', V1 = drafted tint; 'Defend' loop row #114): "a gated test asserts
    /// drafting a pawn sets drafted state + drafted tint, and undrafting clears it,
    /// as ONE PASS line."
    ///
    /// Chain verified as ONE binary PASS:
    ///   Step 1 — draft a pawn via the existing public draft API (PawnEntity.SetDrafted,
    ///            the same call the R-key/ClickSelector path uses) -> IsDrafted == true
    ///   Step 2 — the drafted visual marker/tint reflects it: the renderer applies the
    ///            cyan draft tint (SpriteRenderer.color == PawnEntity's drafted cyan
    ///            0.55/0.85/1.0/1.0).  Read the renderer color READ-ONLY off the pawn
    ///            GameObject (the public marker the renderer paints), the way V7 reads
    ///            bandit transform state read-only — NO runtime .cs edit.
    ///   Step 3 — undrafting clears BOTH: IsDrafted == false AND the tint reverts off
    ///            the drafted cyan (back to the un-selected white the renderer applies
    ///            when the pawn is neither drafted nor selected).
    ///
    /// Harness conventions: same RunOne/Assert/SpawnTestPawn/GetWhiteSprite pattern as
    /// V4CombatChainTest.cs / V7RaidThreatTest.cs.  This is a COMPANION test that feeds
    /// the same JSON-report path convention used by qa.py and does NOT modify
    /// TestRunner.cs or any runtime .cs file.
    ///
    /// How the tint is applied (read-only knowledge from PawnEntity.cs, NOT edited):
    ///   PawnEntity.SetDrafted(value) flips IsDrafted then calls ApplyVisual(), which
    ///   sets the SpriteRenderer.color to new Color(0.55f, 0.85f, 1.0f, 1f) (cyan) when
    ///   drafted, else selectedOutlineColor when selected, else unselectedColor
    ///   (Color.white default).  The test reads SpriteRenderer.color — a fully public
    ///   read-only handle — so no new accessor and no reflection are needed.
    ///
    /// API gaps flagged for QA (see .claude/wb/v1-drafted.json):
    ///   (none required) — IsDrafted is a public getter, SetDrafted is public, and the
    ///   drafted tint is observable READ-ONLY via the pawn's SpriteRenderer.color.
    ///   The exact cyan/white constants live in PawnEntity.ApplyVisual as private
    ///   SerializeFields; the test does NOT read those private fields — it asserts
    ///   against the same literal cyan the renderer paints and that undraft moves the
    ///   color OFF that cyan (and onto the documented white default).  G-NOTE: if a
    ///   future re-skin changes the drafted color constant, this literal must track it
    ///   — a public read-only PawnEntity.DraftedTint accessor would remove that
    ///   coupling (FLAGGED, not required for PASS).
    /// </summary>
    public class V1DraftedTintTest : MonoBehaviour
    {
        // Shared report path matches the companion-test convention (V4=_v4_, V7=_v7_).
        public string outputPath = "G:/ai/_v1_drafted_tint_report.json";

        // The drafted cyan the renderer paints in PawnEntity.ApplyVisual (read-only
        // mirror of that literal; the test asserts the renderer matches it).
        private static readonly Color DraftedCyan = new Color(0.55f, 0.85f, 1.0f, 1f);
        // The un-selected white default (PawnEntity.unselectedColor = Color.white).
        private static readonly Color UndraftedWhite = Color.white;

        private static bool _lastAssertPassed;
        private static string _lastAssertMessage;

        private static void Assert(bool cond, string msg)
        {
            _lastAssertPassed = cond;
            _lastAssertMessage = msg;
        }

        private IEnumerator Start()
        {
            Debug.Log("[V1DraftedTintTest] start — V1 drafted tint");
            yield return new WaitForSeconds(0.2f);

            yield return RunOne("V1-drafted-tint", TestV1_DraftedTint);

            Debug.Log($"[V1DraftedTintTest] {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage}");
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
            Debug.Log($"[V1DraftedTintTest] {id} {(_lastAssertPassed ? "PASS" : "FAIL")} — {_lastAssertMessage} ({dur:F2}s)");
        }

        // ----------------------------------------------------------------
        // V1 — drafted-tint chain
        // ----------------------------------------------------------------

        private IEnumerator TestV1_DraftedTint()
        {
            // ---- Spawn a plain pawn far from any ambient entities ----
            // includeAI:false — the draft tint is applied purely by PawnEntity, so we
            // don't need PawnUtilityAI / arrow wiring for this verification.
            Vector3 pawnPos = new Vector3(200f, 0f, 0f);
            GameObject pawnGo = SpawnTestPawn(pawnPos, includeAI: false);
            var pawn = pawnGo.GetComponent<PawnEntity>();
            var sr   = pawnGo.GetComponent<SpriteRenderer>();   // public read-only handle
            yield return null;   // Awake -> ApplyVisual sets undrafted/unselected color

            // Baseline sanity: a fresh, undrafted, unselected pawn is NOT drafted and
            // the renderer is NOT showing the drafted cyan.
            bool baseline_notDrafted = !pawn.IsDrafted;
            Color colorBeforeDraft = sr != null ? sr.color : Color.clear;
            bool baseline_notCyan = !Approx(colorBeforeDraft, DraftedCyan);

            // ---- Step 1 — draft the pawn via the public draft API (R-key path) ----
            pawn.SetDrafted(true);
            yield return null;   // SetDrafted -> ApplyVisual paints the cyan tint
            bool step1_draftedState = pawn.IsDrafted;

            // ---- Step 2 — the drafted visual marker/tint reflects it (READ-ONLY) ----
            // Read the color the renderer applied off the pawn's SpriteRenderer.
            Color colorWhenDrafted = sr != null ? sr.color : Color.clear;
            bool step2_draftedTint = Approx(colorWhenDrafted, DraftedCyan);

            // ---- Step 3 — undrafting clears BOTH state and tint ----
            pawn.SetDrafted(false);
            yield return null;   // SetDrafted -> ApplyVisual reverts the tint
            bool step3a_undraftedState = !pawn.IsDrafted;
            Color colorAfterUndraft = sr != null ? sr.color : Color.clear;
            // Tint must move OFF the drafted cyan; because the pawn is neither drafted
            // nor selected it reverts to the documented white default.
            bool step3b_tintCleared = !Approx(colorAfterUndraft, DraftedCyan)
                                      && Approx(colorAfterUndraft, UndraftedWhite);

            // ---- Cleanup ----
            if (pawnGo != null) Object.Destroy(pawnGo);

            // ---- Composite assertion — ONE binary PASS ----
            // PASS only when: fresh pawn is undrafted/not-cyan, drafting flips state
            // true AND paints the cyan tint, and undrafting clears state AND tint.
            bool allPass = baseline_notDrafted && baseline_notCyan
                           && step1_draftedState && step2_draftedTint
                           && step3a_undraftedState && step3b_tintCleared;

            Assert(allPass,
                $"chain: " +
                $"[0]baseline(notDrafted={baseline_notDrafted},notCyan={baseline_notCyan},col={Fmt(colorBeforeDraft)}) " +
                $"[1]draftedState={step1_draftedState} " +
                $"[2]draftedTint={step2_draftedTint}(col={Fmt(colorWhenDrafted)},expectCyan={Fmt(DraftedCyan)}) " +
                $"[3a]undraftedState={step3a_undraftedState} " +
                $"[3b]tintCleared={step3b_tintCleared}(col={Fmt(colorAfterUndraft)},expectWhite={Fmt(UndraftedWhite)})");
        }

        // ---- Helpers (mirror V4/V7 conventions) ----

        private GameObject SpawnTestPawn(Vector3 pos, bool includeAI = false)
        {
            var go = new GameObject("V1_TestPawn");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSprite();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<PawnEntity>();
            go.AddComponent<PawnMovement>();
            go.AddComponent<PawnHealth>();
            if (includeAI)
                go.AddComponent<PawnUtilityAI>();
            return go;
        }

        // Per-channel approximate equality (renderer color stored as float RGBA).
        private static bool Approx(Color a, Color b)
        {
            const float eps = 0.01f;
            return Mathf.Abs(a.r - b.r) < eps
                && Mathf.Abs(a.g - b.g) < eps
                && Mathf.Abs(a.b - b.b) < eps
                && Mathf.Abs(a.a - b.a) < eps;
        }

        private static string Fmt(Color c)
        {
            return $"({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2})";
        }

        private static Sprite _whiteSprite;
        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            _whiteSprite.name = "V1WhiteSprite";
            return _whiteSprite;
        }
    }
}
