using System.Collections.Generic;
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

        // 첫사이클 T7 (2026-06-12) — 바닥 판정 3곳(이동 보너스/IsOnFloor/폭풍 차단)이
        //  전부 Physics2D 인데 바닥 프리팹·런타임 석재 모두 무콜라이더라 효과가 영구
        //  미발동이었다 (V59b 는 손수 콜라이더를 달아 거짓 PASS).  물리 대신 정적 셀
        //  레지스트리 — Start/OnDestroy 등록·해제, 프리팹/런타임 양 경로 커버.
        private static readonly Dictionary<Vector2Int, float> floorCells = new Dictionary<Vector2Int, float>();
        private Vector2Int regCell;
        private bool registered;

        private void Start()
        {
            regCell = new Vector2Int(Mathf.FloorToInt(transform.position.x),
                                     Mathf.FloorToInt(transform.position.y));
            if (!floorCells.TryGetValue(regCell, out float cur) || MoveBonus > cur)
                floorCells[regCell] = MoveBonus;
            registered = true;
        }

        private void OnDestroy()
        {
            if (registered) floorCells.Remove(regCell);
        }

        public static bool HasFloorAt(Vector2 pos)
            => floorCells.ContainsKey(new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y)));

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
            // T7 — 물리 쿼리 → 셀 레지스트리 (콜라이더 부재로 영구 1.0 이던 것).
            return floorCells.TryGetValue(new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y)),
                                          out float b) ? b : 1f;
        }
    }
}
