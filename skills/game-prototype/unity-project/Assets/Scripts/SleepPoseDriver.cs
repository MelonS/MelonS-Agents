using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Wave W-M3-02 / Lane A — sleep pose via body SpriteRenderer.color dim (wiki #12).
    ///
    /// Wiki acceptance criterion (rimworld-comparison.md Dim1 'Pawn rendering' +
    /// Dim3 'Facing/rotation' follow-up, backlog #12): "a sleeping pawn is visually
    /// distinct (rotated/flat/dimmed) from a standing one."  We deliver the cheapest
    /// distinct read — a DIM/desaturate of the body sprite — so a sleeping colonist
    /// is unmistakably darker than a standing one, completing the facing/motion story
    /// now that #11 (flipX facing) has landed.
    ///
    /// CRITICAL NON-FIGHT RULE (three distinct child properties, no collision):
    ///   The pawn's visible body renderer is driven by three independent systems,
    ///   each owning ONE property so none can clobber another:
    ///     - PawnSpriteBob  owns the child localPosition.Y   (walk-bob).
    ///     - PawnFacing     owns the child SpriteRenderer.flipX (E/W facing).
    ///     - SleepPoseDriver (this) owns the child SpriteRenderer.color (sleep dim).
    ///   color, flipX, and localPosition are three separate fields — writing one
    ///   never touches the others, which is the entire reason this lane cannot
    ///   collide with facing or walk-bob.
    ///
    ///   HARD RULE — this component writes ONLY the CHILD body renderer's .color.
    ///   It MUST NOT touch:
    ///     * child SpriteRenderer.flipX        (PawnFacing owns it),
    ///     * child Transform.localPosition.Y   (PawnSpriteBob owns it),
    ///     * the ROOT transform                (PathGrid.WorldToCell / reservations
    ///                                          / eject read the root — never move it),
    ///     * the ROOT renderer's color         (the root is the tint anchor owned by
    ///                                          selection/blueprint/night systems).
    ///   We never read or assign those fields anywhere below.
    ///
    /// Sleep detection (located the SAME way PawnInfoPanel/PawnNeeds expose it):
    ///   PawnNeeds.IsSleeping is a public bool { get; private set; } on the pawn
    ///   ROOT (PawnInfoPanel reads it via pawn.GetComponent&lt;PawnNeeds&gt;()).
    ///   PawnNeeds, PawnMovement and PawnEntity all live on the same root object, so
    ///   a SleepPoseDriver placed on the root reaches PawnNeeds with a single
    ///   GetComponent and reaches the body child by name — mirroring PawnFacing.
    ///
    /// Lane constraint (W-M3-02 lane A): this wave may ONLY add this one file.
    /// Pawns are built by SceneSetup.Pawn (an Editor script, Build Engineer's lane).
    /// We may not edit SceneSetup*, PawnMovement, PawnSpriteBob, PawnFacing, or
    /// PawnNeeds.  So SleepPoseDriver is SELF-ATTACHING using the proven
    /// FlickerLight.cs / PawnFacing.cs driver + EnsureOn + periodic-sweep pattern:
    ///   - a [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] bootstrap spawns a
    ///     hidden persistent driver GameObject;
    ///   - the driver periodically scans for PawnMovement instances (the pawn ROOT)
    ///     lacking a SleepPoseDriver and ensures each gets exactly one (handles
    ///     pawns spawned mid-game within &lt;scanInterval&gt; seconds).
    ///
    /// Body-renderer location (matches PawnSpriteBob / PawnFacing wiring):
    ///   SceneSetup.Pawn puts the visible body SpriteRenderer on a CHILD named
    ///   "PawnSpriteBob"; a separate "PawnShadow" child has its own renderer which
    ///   we must NOT dim.  We resolve the body child by name and explicitly skip the
    ///   shadow and the root renderer; if the named child is missing (renamed art
    ///   pipeline) we fall back to the first child SpriteRenderer that is neither the
    ///   shadow nor the root renderer.  Everything is null-guarded → graceful no-op.
    ///
    /// Color contract / restore:
    ///   We capture the body renderer's awake color as the "awake base" and, while
    ///   sleeping, write base * dimTint (a darker, slightly desaturated multiply
    ///   toward ~0.6 value).  On wake we write the base back exactly.  We re-capture
    ///   the base whenever the renderer's current color is (within epsilon) neither
    ///   the base nor the dimmed value — so if another (future) system legitimately
    ///   re-tints the AWAKE body, we adopt that as the new base rather than fighting
    ///   it.  This wave no other lane writes the child color, so in practice base ==
    ///   the spawn color.
    /// </summary>
    [DisallowMultipleComponent]
    public class SleepPoseDriver : MonoBehaviour
    {
        [Header("Sleep dim feel")]
        // Multiply applied to the awake base color while sleeping.  RGB pulled
        //  toward ~0.6 value (darker) and very slightly toward grey (desaturate)
        //  reads clearly as "asleep / at rest" without going black.  Alpha kept at
        //  1 so the pawn stays fully visible (it is dimmer, not fading out).
        //  >>> Director feel-check: 0.6 value should read as "asleep" but not
        //      "dead/invisible"; tune here if too dark/too subtle.
        [SerializeField] private Color dimTint = new Color(0.6f, 0.6f, 0.66f, 1f);

        // How fast color eases between awake and dimmed (units/sec in color space).
        //  A short fade avoids a hard pop when a pawn drops to sleep / wakes.
        [SerializeField] private float fadeSpeed = 6f;

        // Names used by SceneSetup.Pawn for the pawn's children (see header).
        private const string BodyChildName = "PawnSpriteBob";
        private const string ShadowChildName = "PawnShadow";

        // ---- runtime state ----
        // The visible body renderer whose .color we drive.  Resolved in Awake;
        //  re-resolved lazily if it ever goes null (defensive).
        private SpriteRenderer bodyRenderer;
        // The ROOT renderer (tint anchor) — cached only so the fallback search can
        //  exclude it.  We never write to it.
        private SpriteRenderer rootRenderer;
        // The sleep flag source on the pawn root.
        private PawnNeeds needs;

        // The body's awake (un-dimmed) color.  Captured in Awake and re-adopted if
        //  the awake color legitimately changes (see UpdateBaseIfChanged).
        private Color awakeBase = Color.white;
        private bool hasBase;
        // The current eased color we are driving toward target (awakeBase or dimmed).
        private Color current;

        private const float ColorEpsilon = 0.02f;

        private void Awake()
        {
            rootRenderer = GetComponent<SpriteRenderer>();
            needs = GetComponent<PawnNeeds>();
            bodyRenderer = ResolveBodyRenderer();

            if (bodyRenderer != null)
            {
                awakeBase = bodyRenderer.color;
                current = awakeBase;
                hasBase = true;
            }
        }

        /// <summary>
        /// Find the visible body SpriteRenderer the SAME way PawnSpriteBob /
        /// PawnFacing are wired: it lives on the child named "PawnSpriteBob".  Skip
        /// the shadow child and the root renderer.  Fully null-guarded; returns null
        /// if no suitable child renderer exists (then Update is a graceful no-op).
        /// </summary>
        private SpriteRenderer ResolveBodyRenderer()
        {
            // Primary: the body child by name (matches SceneSetup.Pawn wiring).
            Transform body = transform.Find(BodyChildName);
            if (body != null)
            {
                var sr = body.GetComponent<SpriteRenderer>();
                if (sr != null) return sr;
            }

            // Fallback: first child SpriteRenderer that is neither the shadow nor
            //  the root renderer.  Handles a renamed art pipeline without ever
            //  dimming the shadow.
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;
                if (child.name == ShadowChildName) continue;
                var sr = child.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                if (sr == rootRenderer) continue; // never the tint anchor
                return sr;
            }

            return null;
        }

        private bool IsSleeping()
        {
            // Locate the sleep flag exactly as PawnInfoPanel does.  Re-resolve
            //  defensively if needs was lost.  No needs → treat as awake (no-op).
            if (needs == null) needs = GetComponent<PawnNeeds>();
            return needs != null && needs.IsSleeping;
        }

        private void Update()
        {
            // Re-resolve defensively if the body renderer was lost (e.g. child
            //  rebuilt by another system).  Null-safe no-op if still unavailable.
            if (bodyRenderer == null)
            {
                bodyRenderer = ResolveBodyRenderer();
                if (bodyRenderer == null) return;
                awakeBase = bodyRenderer.color;
                current = awakeBase;
                hasBase = true;
            }

            if (!hasBase)
            {
                awakeBase = bodyRenderer.color;
                current = awakeBase;
                hasBase = true;
            }

            bool sleeping = IsSleeping();
            Color dimmed = Multiply(awakeBase, dimTint);

            // Adopt a NEW awake base if the renderer's current color drifted away
            //  from both our base and our dimmed value while the pawn is awake —
            //  i.e. some other (future) system legitimately re-tinted the awake
            //  body.  We never do this while sleeping (the renderer is then OUR
            //  dimmed value by definition).  This wave no other lane writes the
            //  child color, so this path is dormant in practice but keeps us from
            //  ever stomping a legitimate awake re-tint.
            if (!sleeping)
            {
                Color shown = bodyRenderer.color;
                if (!Approximately(shown, current) &&
                    !Approximately(shown, awakeBase) &&
                    !Approximately(shown, dimmed))
                {
                    awakeBase = shown;
                    current = shown;
                    dimmed = Multiply(awakeBase, dimTint);
                }
            }

            Color target = sleeping ? dimmed : awakeBase;

            // Ease toward target, then write ONLY the color channel of the CHILD
            //  body renderer.  We never read/write flipX, localPosition, or the
            //  root transform anywhere in this method.
            current = Color.MoveTowards(current, target, fadeSpeed * Time.unscaledDeltaTime);
            bodyRenderer.color = current;
        }

        private void OnDisable()
        {
            // Restore the awake base so a disabled driver never leaves a pawn stuck
            //  dimmed.  Only the color channel is touched.
            if (bodyRenderer != null && hasBase)
                bodyRenderer.color = awakeBase;
        }

        private static Color Multiply(Color a, Color b)
        {
            return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < ColorEpsilon
                && Mathf.Abs(a.g - b.g) < ColorEpsilon
                && Mathf.Abs(a.b - b.b) < ColorEpsilon
                && Mathf.Abs(a.a - b.a) < ColorEpsilon;
        }
    }

    /// <summary>
    /// Self-attach bootstrap + runtime sweep for SleepPoseDriver — copies the
    /// FlickerLight.cs / PawnFacing.cs driver/EnsureOn/scan structure so no
    /// SceneSetup edit is needed.
    ///
    /// runInBackground note (bug-pattern #9): this driver throttles via Time INSIDE
    /// Update (NOT a WaitForSeconds heartbeat coroutine), so it introduces no new
    /// always-on background timer.  AutoScreenshotter already owns
    /// Application.runInBackground for CLI QA paths; we add nothing background-only
    /// here, so short- and long-delay QA both behave identically.
    /// </summary>
    internal static class SleepPoseDriverBootstrap
    {
        private const float ScanInterval = 1.0f; // seconds between rescans

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Idempotent: avoid a second driver on scene reload / domain-reload-
            //  disabled play sessions.
            if (FindDriver() != null) return;

            var go = new GameObject("~SleepPoseDriver");
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<SleepPoseSweepDriver>();
        }

        private static SleepPoseSweepDriver FindDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<SleepPoseSweepDriver>();
#else
            return Object.FindObjectOfType<SleepPoseSweepDriver>();
#endif
        }

        /// <summary>Ensure the given pawn ROOT has exactly one SleepPoseDriver.</summary>
        internal static void EnsureOn(PawnMovement pm)
        {
            if (pm == null) return;
            // PawnMovement lives on the pawn ROOT, so AddComponent here lands the
            //  SleepPoseDriver on the ROOT — exactly where it reads PawnNeeds and
            //  reaches the body child.  [DisallowMultipleComponent] + this guard
            //  make it idempotent.
            if (pm.GetComponent<SleepPoseDriver>() == null)
                pm.gameObject.AddComponent<SleepPoseDriver>();
        }

        /// <summary>
        /// Hidden persistent driver.  Periodically scans for PawnMovement instances
        /// (pawn roots) lacking a SleepPoseDriver and attaches one.  The lane-safe
        /// alternative to editing SceneSetup.Pawn / the pawn prefab.
        /// </summary>
        private sealed class SleepPoseSweepDriver : MonoBehaviour
        {
            private float nextScan;
            // Reused scratch buffer — no per-frame List allocation.
            private static readonly List<PawnMovement> scratch = new List<PawnMovement>(32);

            private void Update()
            {
                if (Time.unscaledTime < nextScan) return;
                nextScan = Time.unscaledTime + ScanInterval;

                scratch.Clear();
#if UNITY_2023_1_OR_NEWER
                var found = Object.FindObjectsByType<PawnMovement>(FindObjectsSortMode.None);
#else
                var found = Object.FindObjectsOfType<PawnMovement>();
#endif
                for (int i = 0; i < found.Length; i++)
                    EnsureOn(found[i]);
            }
        }
    }
}
