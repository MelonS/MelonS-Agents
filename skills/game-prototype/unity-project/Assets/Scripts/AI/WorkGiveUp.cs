using UnityEngine;

namespace MelonS.GameProto.AI
{
    /// <summary>
    /// #199 B2 (R-1 pulled forward) — shared "should this worker give up its
    /// work target?" decision, used by every worker behaviour
    /// (Chop/Gather/Hunt/Build/Mine/Cook/Haul/Doctor).
    ///
    /// WHY THIS EXISTS / what it replaces:
    ///   Before B2 every worker gave up on  `Time.time - taskStart > GiveUpSec
    ///   && dist > range`  — i.e. "I've been at it a while AND I'm still far
    ///   from the target's CENTRE".  That was honest only because the OLD
    ///   point-lerp moved straight at the target, so "far for a while" really
    ///   meant "can't get there".
    ///
    ///   With grid A* (flag now LIVE) a pawn legitimately DETOURS: its straight-
    ///   line distance to the target centre can stay large for many seconds while
    ///   it walks the long way around a lake/wall, then drops on arrival.  The
    ///   old condition would false-trip mid-detour and the worker would abandon a
    ///   reachable tree → REGRESSION (chopping stalls, wood stops rising).
    ///
    /// THE NEW CONTRACT (RimWorld-style "destination unreachable"):
    ///   Give up when EITHER
    ///     (a) the pathfinder reported the target genuinely UNREACHABLE
    ///         (PawnMovement.LastPathFailed — empty A* result), OR
    ///     (b) the pawn has been working the task past GiveUpSec AND has made
    ///         NO net progress toward the target for a full GiveUpSec window
    ///         (a real stall: stuck on geometry, oscillating, or target moved
    ///         out of reach).  A detouring pawn keeps closing distance, so it
    ///         keeps refreshing its progress clock and never trips (b).
    ///
    ///   Net effect: workers give up on REAL unreachability, not on
    ///   distance-to-centre.  C1 will refine "in range" to adjacency geometry;
    ///   this struct only governs give-up, which is the load-bearing R-1 risk.
    ///
    /// Value type, held per-worker.  Call Reset() when a new target is assigned;
    /// call ShouldGiveUp() each frame while the target is not yet in range.
    /// </summary>
    public struct WorkGiveUp
    {
        // Progress is "meaningful" only if distance improves by at least this
        // much; tiny float jitter must not count as progress (would let a truly
        // stuck pawn refresh its clock forever) nor reset it (a slow approach
        // still counts).  0.15 world unit ≈ a few frames of normal walk.
        private const float ProgressEpsilon = 0.15f;

        private float _taskStart;
        private float _lastProgressTime;
        private float _bestDist;       // closest distance-to-target seen so far
        private bool _started;

        /// <summary>Begin (or restart) the give-up clock for a freshly assigned
        /// target.  `now` = Time.time, `dist` = current distance to target.</summary>
        public void Reset(float now, float dist)
        {
            _taskStart = now;
            _lastProgressTime = now;
            _bestDist = dist;
            _started = true;
        }

        /// <summary>
        /// Decide whether to abandon the current work target.
        ///   now         = Time.time
        ///   dist        = current straight-line distance to the target centre
        ///   pathFailed  = PawnMovement.LastPathFailed (true = A* found no path)
        ///   giveUpSec   = the worker's configured patience window
        /// Returns true to give up.  Side-effect: updates the progress clock.
        /// </summary>
        public bool ShouldGiveUp(float now, float dist, bool pathFailed, float giveUpSec)
        {
            // First call without Reset() — be safe, treat as just-started.
            if (!_started) Reset(now, dist);

            // (a) Genuine unreachability — the pathfinder gave up.  Honour it
            //     immediately; no point waiting out the timer.
            if (pathFailed) return true;

            // Track net progress: if we got meaningfully closer, refresh the
            // progress clock and remember the new best.
            if (dist < _bestDist - ProgressEpsilon)
            {
                _bestDist = dist;
                _lastProgressTime = now;
            }

            // (b) Stalled: past the patience window since start AND no progress
            //     for a full window.  A detouring pawn keeps closing distance →
            //     keeps refreshing _lastProgressTime → never trips here.
            bool pastStart = now - _taskStart > giveUpSec;
            bool stalled = now - _lastProgressTime > giveUpSec;
            return pastStart && stalled;
        }
    }
}
