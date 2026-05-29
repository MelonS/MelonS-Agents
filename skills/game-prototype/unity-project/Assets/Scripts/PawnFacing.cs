using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Wave W-M3-01 / Lane A — pawn facing via SpriteRenderer.flipX (wiki #11).
    ///
    /// Wiki acceptance criterion (rimworld-comparison.md Dim3 'Facing / rotation',
    /// backlog #11): "a pawn walking left faces left, walking right faces right."
    /// This is the #2 player-felt gap — a pawn moonwalking reads as broken even
    /// with perfect pathing.  We give every pawn a horizontal facing that tracks
    /// its movement direction and HOLDS when idle (RimWorld keeps the last facing).
    ///
    /// CRITICAL ROOT-TRANSFORM RULE (shared with PawnSpriteBob.cs):
    ///   PathGrid.WorldToCell reads the pawn ROOT transform for cells /
    ///   reservations / eject (PawnMovement.RegisterWallCell, WorldToCell, the
    ///   Update standing safety-net).  Therefore this component NEVER moves /
    ///   rotates / scales the root transform.  It reads the root's WORLD x only to
    ///   measure delta, and writes ONLY SpriteRenderer.flipX on the visible CHILD
    ///   body renderer.  flipX is a draw-time mirror — it does NOT change the
    ///   child's transform, so PawnSpriteBob keeps sole ownership of the child's
    ///   localPosition Y (we never touch localPosition).  No two systems fight
    ///   over a transform: PawnSpriteBob owns child localPos, PawnFacing owns
    ///   child flipX.
    ///
    /// Lane constraint (W-M3-01 lane A): this wave may ONLY add this one file.
    /// Pawns are built by SceneSetup.Pawn (an Editor script, Build Engineer's
    /// lane).  We may not edit SceneSetup*, PawnMovement, or PawnSpriteBob.  So
    /// PawnFacing is SELF-ATTACHING using the FlickerLight.cs pattern:
    ///   - a [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] bootstrap spawns a
    ///     hidden persistent driver GameObject;
    ///   - the driver periodically scans for PawnMovement instances (the pawn
    ///     ROOT) lacking a PawnFacing and ensures each gets one (handles pawns
    ///     spawned mid-game).
    ///
    /// Body-renderer location (matches PawnSpriteBob's wiring):
    ///   SceneSetup.Pawn creates the visible body SpriteRenderer on a CHILD
    ///   GameObject named "PawnSpriteBob" under the pawn root, and wires that
    ///   renderer into PawnSpriteBob.childRenderer.  We locate the SAME renderer
    ///   by finding that child by name.  The pawn also has a "PawnShadow" child
    ///   with its own SpriteRenderer — we must NOT flip the shadow, so we match
    ///   the body child by name and explicitly skip the shadow.  If the named
    ///   child can't be found (renamed art pipeline), we fall back to the first
    ///   child SpriteRenderer that is NOT the shadow and NOT the root renderer.
    ///   Everything is null-guarded.
    /// </summary>
    [DisallowMultipleComponent]
    public class PawnFacing : MonoBehaviour
    {
        [Header("Facing feel")]
        // Minimum horizontal world movement per frame to count as "moving" in X.
        //  Below this we treat the pawn as not moving horizontally and HOLD the
        //  last facing (avoids flicker from sub-pixel jitter / float noise while
        //  the pawn is effectively standing still or moving purely vertically).
        [SerializeField] private float moveEpsilon = 0.0005f;

        // Names used by SceneSetup.Pawn for the pawn's children (see header).
        private const string BodyChildName = "PawnSpriteBob";
        private const string ShadowChildName = "PawnShadow";

        // ---- runtime state ----
        // The visible body renderer whose flipX we drive.  Resolved in Awake;
        //  re-resolved lazily if it ever goes null (defensive).
        private SpriteRenderer bodyRenderer;
        // The ROOT renderer (tint anchor) — cached only so the fallback search can
        //  exclude it (we must not flip the root renderer; it is draw-disabled but
        //  flipping it would still be the wrong contract).
        private SpriteRenderer rootRenderer;
        // Last root world-X, to compute per-frame horizontal delta.
        private float lastX;
        // Whether lastX has been seeded (so frame 1 doesn't read a garbage delta).
        private bool hasLastX;

        private void Awake()
        {
            rootRenderer = GetComponent<SpriteRenderer>();
            bodyRenderer = ResolveBodyRenderer();
            // Seed the position baseline so the first Update measures a real delta
            //  from the spawn position rather than from 0.
            lastX = transform.position.x;
            hasLastX = true;
        }

        /// <summary>
        /// Find the visible body SpriteRenderer the SAME way PawnSpriteBob is
        /// wired: it lives on the child named "PawnSpriteBob".  Skip the shadow
        /// child and the root renderer.  Fully null-guarded; returns null if no
        /// suitable child renderer exists (then Update is a graceful no-op).
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
            //  flipping the shadow.
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

        private void Update()
        {
            // Re-resolve defensively if the body renderer was lost (e.g. child
            //  rebuilt by another system).  Null-safe no-op if still unavailable.
            if (bodyRenderer == null)
            {
                bodyRenderer = ResolveBodyRenderer();
                if (bodyRenderer == null) return;
            }

            float x = transform.position.x;

            if (!hasLastX)
            {
                lastX = x;
                hasLastX = true;
                return;
            }

            float dx = x - lastX;
            lastX = x;

            // Idle (or purely vertical movement) → HOLD the last facing.  Only a
            //  meaningful horizontal delta updates flipX.  We measure the ROOT's
            //  WORLD x delta directly (NOT PawnMovement state) so facing tracks the
            //  actual on-screen motion regardless of which movement branch drove it
            //  (A* path-follow, eject push, manual move).  We never write to the
            //  root — only read its position.
            if (dx > moveEpsilon)
            {
                // Moving RIGHT → face right.  Default sprite art faces right, so
                //  right = un-flipped.
                if (bodyRenderer.flipX) bodyRenderer.flipX = false;
            }
            else if (dx < -moveEpsilon)
            {
                // Moving LEFT → face left = mirrored.
                if (!bodyRenderer.flipX) bodyRenderer.flipX = true;
            }
            // else: |dx| <= epsilon → not moving horizontally → keep last flipX.
        }
    }

    /// <summary>
    /// Self-attach bootstrap + runtime sweep for PawnFacing — copies the
    /// FlickerLight.cs driver/EnsureOn/scan structure so no SceneSetup edit is
    /// needed.  Lives in its own (non-nested) type only for clarity; behaviour is
    /// identical to FlickerLight's nested driver.
    ///
    /// runInBackground note (bug-pattern #9): this driver throttles via Time
    /// INSIDE Update (NOT a WaitForSeconds heartbeat coroutine), so it introduces
    /// no new always-on background timer.  AutoScreenshotter already owns
    /// Application.runInBackground for CLI QA paths; we add nothing background-only
    /// here, so short- and long-delay QA both behave identically.
    /// </summary>
    internal static class PawnFacingBootstrap
    {
        private const float ScanInterval = 1.0f; // seconds between rescans

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Idempotent: avoid a second driver on scene reload / domain-reload-
            //  disabled play sessions.
            if (FindDriver() != null) return;

            var go = new GameObject("~PawnFacingDriver");
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<PawnFacingDriver>();
        }

        private static PawnFacingDriver FindDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<PawnFacingDriver>();
#else
            return Object.FindObjectOfType<PawnFacingDriver>();
#endif
        }

        /// <summary>Ensure the given pawn ROOT has exactly one PawnFacing.</summary>
        internal static void EnsureOn(PawnMovement pm)
        {
            if (pm == null) return;
            // PawnMovement lives on the pawn ROOT, so AddComponent here lands the
            //  PawnFacing on the ROOT — exactly where it must read root delta-pos
            //  and reach the body child.  [DisallowMultipleComponent] + this guard
            //  make it idempotent.
            if (pm.GetComponent<PawnFacing>() == null)
                pm.gameObject.AddComponent<PawnFacing>();
        }

        /// <summary>
        /// Hidden persistent driver.  Periodically scans for PawnMovement
        /// instances (pawn roots) lacking a PawnFacing and attaches one.  The
        /// lane-safe alternative to editing SceneSetup.Pawn / the pawn prefab.
        /// </summary>
        private sealed class PawnFacingDriver : MonoBehaviour
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
