using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M4-05 Lane B — wiki Dimension 4 (건축) #21:
    ///   "Stone/paved floor variant (from stone, faster)."
    ///   Acceptance (binary): a stone floor is buildable and gives a HIGHER
    ///   move bonus than the wood floor.
    ///
    /// The placed stone / paved floor tile.  It is a FloorEntity SUBCLASS — this
    /// is deliberate and load-bearing: every floor consumer in the project
    /// detects floors with <c>GetComponent&lt;FloorEntity&gt;()</c>
    /// (PawnMovement.IsOnFloor / BonusAt, PawnNeeds indoor check,
    /// EntityInspectorPanel, HoverTooltip).  Because StoneFloorEntity IS-A
    /// FloorEntity, all of those keep working with ZERO changes — the stone
    /// floor counts as indoor, gets the move bonus, shows the floor tooltip,
    /// etc.  The ONLY thing that differs is the magnitude of the move bonus,
    /// which it expresses by overriding the existing virtual <see cref="FloorEntity.MoveBonus"/>.
    ///
    /// Like FloorEntity it carries NO collider — pawns walk over it.  The wood
    /// floor gives 1.30x; this gives the wiki's full paved-tile 1.50x (50%),
    /// strictly higher, satisfying the binary acceptance through the EXISTING
    /// bonus mechanism (no new movement system).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class StoneFloorEntity : FloorEntity
    {
        /// <summary>
        /// 석재 바닥 이동 보너스 - wiki paved tile 50% (1.50x).  wood 1.30x 보다
        /// 높음 → 수락 기준 충족.  값을 SerializeField 로 노출해 디자이너가
        /// 코드 변경 없이 튜닝 가능 (단, 항상 wood 의 1.30 보다 높게 유지).
        /// </summary>
        [SerializeField] private float stoneMoveBonus = 1.50f;

        public override float MoveBonus => stoneMoveBonus;
    }
}
