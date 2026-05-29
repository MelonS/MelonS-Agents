using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M4-06 Lane A — wiki Dimension 4 (건축) #20:
    ///   "Add table+chair buildable (eat/rec spot)."
    ///   Acceptance: table+chair can be built and a pawn uses it to eat.
    ///
    /// The placed table (the dominant mass of the combined table+chair piece).
    /// A passive structure entity in the same family as StoveEntity / BedEntity /
    /// LampEntity: a sprite + a 1×1 collider footprint, carrying NO per-frame
    /// state of its own.  The matching <see cref="ChairEntity"/> rides on the
    /// same placed object (added by BuildManager's lazy template) so the one
    /// build op produces both a table and a chair.
    ///
    /// HOW A PAWN "USES IT TO EAT" — reusing the EXISTING eat path, no new need:
    ///   The colony eat loop lives in PawnNeeds.TryEatTick(): when a pawn is
    ///   hungry it consumes a meal/food from the shared stockpile IN PLACE and
    ///   logs a mood thought (e.g. "맛있는 식사").  Exactly like the bed loop
    ///   detects a bed UNDER the pawn (PawnNeeds.GetBedUnderPawn via an
    ///   OverlapBox) to grant a rest/mood bonus, this entity exposes a static
    ///   read-only <see cref="IsEatSpotAt"/> probe that the eat path can opt
    ///   into with a SINGLE read-only line to grant an "ate at a table" mood
    ///   bonus — RimWorld's "ate at a table" thought.  No new dispatch, no new
    ///   pathfinding, no utility-AI edit: the pawn already walks the map and
    ///   eats where it stands, and a pawn standing on the table+chair cell
    ///   simply gets the bonus, the same shape as eating on/near a bed.
    ///
    ///   The one-line opt-in lives in PawnNeeds (out of this lane) and is
    ///   FLAGGED for QA in .claude/wb/table-chair.json — the helper here is the
    ///   read-only API that line calls, so the entity ships fully self-contained.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class TableEntity : MonoBehaviour
    {
        /// <summary>
        /// "Ate at a table" mood bonus (RimWorld grants a small positive thought
        /// for eating at a proper table).  SerializeField so the designer can
        /// tune day-1 feel without a code change.  Kept modest so the table is a
        /// nice-to-have, not a mandatory crutch.
        /// </summary>
        [SerializeField] private float ateAtTableMoodBonus = 4f;

        /// <summary>"ate at a table" thought lifetime, seconds (≈ one meal cycle).</summary>
        [SerializeField] private float ateAtTableThoughtDur = 300f;

        public float AteAtTableMoodBonus => ateAtTableMoodBonus;
        public float AteAtTableThoughtDur => ateAtTableThoughtDur;

        /// <summary>
        /// Read-only probe: is there a table at world position <paramref name="pos"/>?
        /// Mirrors <see cref="FloorEntity.BonusAt"/> / PawnNeeds.GetBedUnderPawn —
        /// the same tiny OverlapBox the eat/rest paths already use to detect
        /// furniture under a pawn.  Returns the TableEntity (or null) so the eat
        /// path can read its mood bonus.  Used by the (FLAGGED) one-line opt-in
        /// in PawnNeeds.TryEatTick so a pawn eating at a table gets the bonus.
        /// </summary>
        public static TableEntity EatSpotAt(Vector2 pos)
        {
            var hits = Physics2D.OverlapBoxAll(pos, Vector2.one * 0.4f, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var t = h.GetComponent<TableEntity>();
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Convenience bool wrapper for callers that only need the flag.</summary>
        public static bool IsEatSpotAt(Vector2 pos) => EatSpotAt(pos) != null;
    }
}
