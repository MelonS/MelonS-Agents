using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto.AI
{
    /// <summary>
    /// #199 C2 — RimWorld-style reservation registry.
    ///
    /// In RimWorld a pawn that takes a job RESERVES its target (the tree / bush /
    /// vein / blueprint / stove / pile) and usually the cell it stands in.  Other
    /// pawns skip reserved things and pick something else; on job end / abandon /
    /// death the reservation releases so the target frees up again.  This class is
    /// the single registry that replaces the per-worker `claimed`/`ReservedBy`
    /// scans previously scattered across PawnActions / each Pawn* worker.
    ///
    /// DESIGN — firewall-safe (lesson #7 singleton-subscription race):
    ///   This is a PLAIN STATIC class, not a MonoBehaviour singleton.  Nothing
    ///   subscribes to it in OnEnable; callers just call the static methods.  There
    ///   is no Instance to be null mid-frame, no event to mis-order — so the
    ///   subscription-race lesson cannot recur here.  Headless-safe: every method
    ///   no-throws when called with null and works without any scene object.
    ///
    /// STORAGE:
    ///   _targetOwner : target  -> claimant GameObject     (who holds this target)
    ///   _cellOwner   : cell    -> claimant GameObject     (who holds this stand cell)
    ///   _byClaimant  : claimant -> set of keys it holds   (for O(1) ReleaseAll)
    ///   A "key" boxes either a target (the GameObject / entity object) or a cell
    ///   (Vector2Int) so ReleaseAll can clear both kinds in one pass.
    ///
    /// DESTROYED-OBJECT SAFETY (R-5 leak guard):
    ///   Unity GameObjects compare == null once destroyed.  Every read path
    ///   (IsReservedByOther / TryReserve) treats a reservation whose CLAIMANT is
    ///   destroyed as FREE and cleans it up lazily, so a pawn that dies without a
    ///   clean ReleaseAll cannot permanently lock a target.  Callers also call
    ///   ReleaseAll on death/downed/draft (PawnEntity.OnDestroy + the AI), which is
    ///   the primary release; lazy cleanup is the backstop.
    ///
    ///   Targets that are GameObjects are likewise auto-freed when destroyed: a
    ///   reservation on a destroyed target is reported free.  Non-GameObject
    ///   targets (rare) rely on explicit Release.
    /// </summary>
    public static class ReservationManager
    {
        // target object -> claimant.  target is usually a component (TreeEntity,
        //  BlueprintEntity, ...) or its GameObject; we key on the object reference.
        private static readonly Dictionary<object, GameObject> _targetOwner
            = new Dictionary<object, GameObject>();

        // stand cell -> claimant.
        private static readonly Dictionary<Vector2Int, GameObject> _cellOwner
            = new Dictionary<Vector2Int, GameObject>();

        // claimant -> the keys (targets + cells) it currently holds.  Lets
        //  ReleaseAll(claimant) free everything in one pass without scanning the
        //  whole registry.  Boxed Vector2Int is used as a cell key.
        private static readonly Dictionary<GameObject, HashSet<object>> _byClaimant
            = new Dictionary<GameObject, HashSet<object>>();

        // ----- helpers --------------------------------------------------------

        // A Unity Object that has been Destroy()d compares == null even though the
        //  managed ref is non-null.  Treat such an owner as gone.
        private static bool IsDeadOwner(GameObject owner)
            => owner == null;   // Unity's overloaded == catches destroyed objects

        // A target keyed by a destroyed Unity Object is itself gone → its
        //  reservation is meaningless and should be reported free.
        private static bool IsDeadTarget(object target)
        {
            if (target == null) return true;
            // Component / GameObject targets become "fake null" when destroyed.
            if (target is Object uo) return uo == null;
            return false;
        }

        private static void Track(GameObject claimant, object key)
        {
            if (!_byClaimant.TryGetValue(claimant, out var set))
            {
                set = new HashSet<object>();
                _byClaimant[claimant] = set;
            }
            set.Add(key);
        }

        private static void Untrack(GameObject claimant, object key)
        {
            if (claimant == null) return;
            if (_byClaimant.TryGetValue(claimant, out var set))
            {
                set.Remove(key);
                if (set.Count == 0) _byClaimant.Remove(claimant);
            }
        }

        // ----- work-target reservation ---------------------------------------

        /// <summary>
        /// Try to reserve a work <paramref name="target"/> for <paramref name="claimant"/>.
        /// Returns true (and records) when the target is FREE or already held by the
        /// SAME claimant (idempotent re-reserve).  Returns false when another, still-
        /// alive claimant holds it.  A reservation whose existing owner is destroyed
        /// is treated as free and reclaimed.  No-throw on null args.
        /// </summary>
        public static bool TryReserve(object target, GameObject claimant)
        {
            if (claimant == null || IsDeadTarget(target)) return false;

            if (_targetOwner.TryGetValue(target, out var owner))
            {
                if (owner == claimant) return true;          // idempotent re-reserve
                if (!IsDeadOwner(owner)) return false;       // held by someone alive
                // owner destroyed → stale entry, fall through and reclaim.
                Untrack(owner, target);
            }

            _targetOwner[target] = claimant;
            Track(claimant, target);
            return true;
        }

        /// <summary>
        /// True when <paramref name="target"/> is reserved by a DIFFERENT, still-
        /// alive claimant than <paramref name="claimant"/>.  Used by AI selection to
        /// skip already-claimed work.  A free target, one held by the same claimant,
        /// or one whose owner is destroyed all return false (selectable).  When the
        /// owner is found destroyed, the stale entry is cleaned up here so it can't
        /// leak.  No-throw on null args.
        /// </summary>
        public static bool IsReservedByOther(object target, GameObject claimant)
        {
            if (IsDeadTarget(target)) return false;
            if (!_targetOwner.TryGetValue(target, out var owner)) return false;
            if (owner == claimant) return false;
            if (IsDeadOwner(owner))
            {
                // Stale (owner destroyed without ReleaseAll) → free it now.
                _targetOwner.Remove(target);
                Untrack(owner, target);
                return false;
            }
            return true;
        }

        /// <summary>True when ANYONE alive currently reserves the target.</summary>
        public static bool IsReserved(object target)
        {
            if (IsDeadTarget(target)) return false;
            if (!_targetOwner.TryGetValue(target, out var owner)) return false;
            if (IsDeadOwner(owner)) { _targetOwner.Remove(target); Untrack(owner, target); return false; }
            return true;
        }

        /// <summary>The claimant that holds the target, or null (incl. destroyed-owner).</summary>
        public static GameObject OwnerOf(object target)
        {
            if (IsDeadTarget(target)) return null;
            if (!_targetOwner.TryGetValue(target, out var owner)) return null;
            if (IsDeadOwner(owner)) { _targetOwner.Remove(target); Untrack(owner, target); return null; }
            return owner;
        }

        /// <summary>
        /// Release a single <paramref name="target"/> IF <paramref name="claimant"/>
        /// is its owner (or the current owner is destroyed).  No-op otherwise — a
        /// pawn can't release someone else's reservation.  No-throw on null args.
        /// </summary>
        public static void Release(object target, GameObject claimant)
        {
            if (target == null) return;
            if (!_targetOwner.TryGetValue(target, out var owner)) return;
            if (owner == claimant || IsDeadOwner(owner))
            {
                _targetOwner.Remove(target);
                Untrack(owner, target);
            }
        }

        // ----- stand-cell reservation ----------------------------------------

        /// <summary>
        /// Try to reserve a stand <paramref name="cell"/> for <paramref name="claimant"/>
        /// so two pawns don't path to the same adjacent cell.  Same semantics as
        /// TryReserve (free / same-claimant idempotent → true; held-by-other-alive →
        /// false; destroyed owner → reclaimed).  No-throw on null claimant.
        /// </summary>
        public static bool TryReserveCell(Vector2Int cell, GameObject claimant)
        {
            if (claimant == null) return false;
            object key = cell;   // boxed Vector2Int key
            if (_cellOwner.TryGetValue(cell, out var owner))
            {
                if (owner == claimant) return true;
                if (!IsDeadOwner(owner)) return false;
                Untrack(owner, key);
            }
            _cellOwner[cell] = claimant;
            Track(claimant, key);
            return true;
        }

        /// <summary>True when a different, alive claimant holds this stand cell.</summary>
        public static bool IsCellReservedByOther(Vector2Int cell, GameObject claimant)
        {
            if (!_cellOwner.TryGetValue(cell, out var owner)) return false;
            if (owner == claimant) return false;
            if (IsDeadOwner(owner))
            {
                _cellOwner.Remove(cell);
                Untrack(owner, cell);
                return false;
            }
            return true;
        }

        /// <summary>Release a stand cell IF the claimant owns it (or owner destroyed).</summary>
        public static void ReleaseCell(Vector2Int cell, GameObject claimant)
        {
            if (!_cellOwner.TryGetValue(cell, out var owner)) return;
            if (owner == claimant || IsDeadOwner(owner))
            {
                _cellOwner.Remove(cell);
                Untrack(owner, cell);
            }
        }

        // ----- bulk release ---------------------------------------------------

        /// <summary>
        /// Release EVERYTHING (targets + cells) held by <paramref name="claimant"/>.
        /// Called on task switch, pawn death / downed / draft, and OnDestroy so a
        /// pawn that leaves work never leaks a reservation (R-5).  No-throw on null.
        /// </summary>
        public static void ReleaseAll(GameObject claimant)
        {
            if (claimant == null)
            {
                // A null/destroyed claimant: sweep any entries it might still own.
                //  (Defensive — primary path passes the live GameObject.)
                SweepDeadOwners();
                return;
            }
            if (!_byClaimant.TryGetValue(claimant, out var set)) return;
            // Copy keys out first — we mutate the dictionaries while iterating.
            var keys = new List<object>(set);
            foreach (var key in keys)
            {
                if (key is Vector2Int cell)
                {
                    if (_cellOwner.TryGetValue(cell, out var co) && co == claimant)
                        _cellOwner.Remove(cell);
                }
                else
                {
                    if (_targetOwner.TryGetValue(key, out var to) && to == claimant)
                        _targetOwner.Remove(key);
                }
            }
            _byClaimant.Remove(claimant);
        }

        /// <summary>
        /// Remove any reservation whose owner GameObject has been destroyed.  A
        /// cheap backstop against leaks when a pawn is destroyed without ReleaseAll
        /// (e.g. scene teardown).  O(n) over the registry — call sparingly.
        /// </summary>
        public static void SweepDeadOwners()
        {
            _staleTargets.Clear();
            foreach (var kv in _targetOwner)
                if (IsDeadOwner(kv.Value) || IsDeadTarget(kv.Key)) _staleTargets.Add(kv.Key);
            foreach (var k in _staleTargets)
            {
                _targetOwner.TryGetValue(k, out var o);
                _targetOwner.Remove(k);
                Untrack(o, k);
            }

            _staleCells.Clear();
            foreach (var kv in _cellOwner)
                if (IsDeadOwner(kv.Value)) _staleCells.Add(kv.Key);
            foreach (var c in _staleCells)
            {
                _cellOwner.TryGetValue(c, out var o);
                _cellOwner.Remove(c);
                Untrack(o, c);
            }
        }
        // Reusable scratch lists for SweepDeadOwners (avoid per-call allocs).
        private static readonly List<object> _staleTargets = new List<object>();
        private static readonly List<Vector2Int> _staleCells = new List<Vector2Int>();

        /// <summary>Test/scene-reset hook — wipe the whole registry.  Called by the
        /// test harness between V cases so one test's reservations don't leak into
        /// the next, mirroring Services.Clear().</summary>
        public static void Clear()
        {
            _targetOwner.Clear();
            _cellOwner.Clear();
            _byClaimant.Clear();
        }

        // ----- introspection (tests / debug) ---------------------------------

        public static int ReservedTargetCount => _targetOwner.Count;
        public static int ReservedCellCount => _cellOwner.Count;
    }
}
