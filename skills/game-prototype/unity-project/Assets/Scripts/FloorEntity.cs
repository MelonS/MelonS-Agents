using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 18: built floor tile.  No collider — pawns walk over.
    /// #157 - wiki: paved tile +50% pathcost reduction (50% 이동 속도 보너스 근사).
    ///   wood floor = 30% (1.30x).
    ///
    /// W-M4-05 Lane B (wiki Dim4 #21 "Stone/paved floor variant — from stone,
    ///   faster"): the move-speed bonus is now an INSTANCE value (virtual
    ///   <see cref="MoveBonus"/>) so a subclass can give a HIGHER bonus while
    ///   reusing this exact mechanism.  <see cref="StoneFloorEntity"/> overrides
    ///   it to the wiki's full paved 50% (1.50x).  The legacy static
    ///   <see cref="MoveSpeedMul"/> const is kept unchanged for back-compat
    ///   (older callers / tests), but live movement now reads the highest
    ///   per-cell instance bonus via <see cref="BonusAt"/>.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FloorEntity : MonoBehaviour
    {
        public bool IsIndoor => true;

        /// <summary>
        /// 이동 속도 보너스 - wiki: paved 50%.  wood 보드는 30%.
        /// LEGACY static const (kept for back-compat — base wood value).  Live
        /// movement now uses the per-instance <see cref="MoveBonus"/> via
        /// <see cref="BonusAt"/> so stone/paved floors can exceed this.
        /// </summary>
        public const float MoveSpeedMul = 1.30f;

        /// <summary>
        /// Per-instance move-speed multiplier for THIS floor tile.  Base wood
        /// floor = 1.30x (matches <see cref="MoveSpeedMul"/>); subclasses
        /// override for a higher bonus (stone/paved = 1.50x).  virtual so the
        /// existing single-floor mechanism stays one code path.
        /// </summary>
        public virtual float MoveBonus => MoveSpeedMul;

        /// <summary>
        /// W-M4-05 #21 — return the HIGHEST floor move-bonus among any
        /// FloorEntity overlapping <paramref name="pos"/>, or 1.0 (no bonus)
        /// when none.  Reuses the same tiny OverlapBox the old IsOnFloor used,
        /// but resolves the instance magnitude so a stone floor under the pawn
        /// gives its higher bonus.  Returns the base const value when only a
        /// plain wood floor is present, so behaviour for existing floors is
        /// unchanged.
        /// </summary>
        public static float BonusAt(Vector2 pos)
        {
            float best = 1f;
            var hits = Physics2D.OverlapBoxAll(pos, Vector2.one * 0.3f, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var f = h.GetComponent<FloorEntity>();
                if (f != null && f.MoveBonus > best) best = f.MoveBonus;
            }
            return best;
        }
    }
}
