using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M6-02 B3 — wiki Dimension 4 (건축) row B3:
    ///   "Fence + fence-gate buildable (1 wood, passable, low) — vanilla
    ///    Structure tab staple, cheap."
    ///   Acceptance (binary): a fence is buildable from Architect, renders a
    ///   LOW barrier, and BLOCKS NOTHING — a pawn paths straight through where
    ///   a fence stands (the gate variant likewise passable).
    ///
    /// The placed fence (and its gate variant) — a thin marker component in the
    /// same family as <see cref="FloorEntity"/>: a sprite, no behaviour state.
    ///
    /// PASSABILITY — the load-bearing property of this row:
    ///   A fence must NOT block pathing.  Pathing in this prototype is decided
    ///   by two cooperating systems, and a fence is invisible to BOTH:
    ///     1. PawnMovement.IsBlockedAt — reads ONLY the raw terrain tilemap
    ///        (Water/Rock).  A fence is a built entity, not terrain, so it is
    ///        already invisible to terrain blocking with zero change.
    ///     2. Physics-based obstacle avoidance / wall occupancy — keyed on a
    ///        SOLID (non-trigger) BoxCollider2D the way WallEntity carries one.
    ///        A fence therefore carries NO collider at all (exactly like
    ///        FloorEntity, which pawns walk over).  WallEntity / DoorEntity own
    ///        the colliders that stop pawns; FenceEntity deliberately owns none.
    ///   Net result: a pawn walks straight through a cell that holds a fence,
    ///   which is the colony-sim fence (vanilla fences are pawn-passable; they
    ///   only block farm animals via a separate roaming system that is OUT OF
    ///   SCOPE here per the over-scoping guardrails).
    ///
    /// LOW BARRIER — the visual read:
    ///   The fence renders LOW.  Its sprite is a short rail (drawn in the lower
    ///   ~half of the 16×16 tile by _gen_fence.py), and its SpriteRenderer sits
    ///   at sortingOrder 2 — above the ground/floor tiles (floors are 1) but
    ///   BELOW furniture/wall bodies (those are 5+).  So a fence reads as a low
    ///   waist-height barrier line, never as a full wall.
    ///
    /// NO cover-math:
    ///   the reference sim fences give a small ranged cover bonus.  Cover math is
    ///   explicitly OUT OF SCOPE / OP-gated for this lane — this component
    ///   carries none.  A future cover wave can add it without touching this.
    ///
    /// Gate variant:
    ///   The fence-gate is the SAME entity with <see cref="IsGate"/> = true and
    ///   a gate sprite.  It is passable in exactly the same way (no collider) —
    ///   it does not gate anything mechanically in this prototype, it is the
    ///   visual "opening" in a fence run.  Kept on one component so the single
    ///   build catalogue lane adds one entity type, not two.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FenceEntity : MonoBehaviour
    {
        /// <summary>
        /// True for the fence-GATE variant, false for a plain fence rail.
        /// Set by BuildManager when it instantiates the prefab template.  Used
        /// only for inspection / future systems; passability is identical for
        /// both (neither carries a collider).
        /// </summary>
        public bool IsGate;

        /// <summary>
        /// A fence is always passable — it blocks no pawn.  Exposed read-only so
        /// any future pathing/cover consumer can assert the contract in one place
        /// rather than re-deriving it.  Mirrors FloorEntity's "pawns walk over"
        /// guarantee.
        /// </summary>
        public bool IsPassable => true;
    }
}
