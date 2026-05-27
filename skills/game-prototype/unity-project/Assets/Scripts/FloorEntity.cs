using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 18: built floor tile.  No collider — pawns walk over.
    /// #157 - wiki: paved tile +50% pathcost reduction (50% 이동 속도 보너스 근사).
    ///   현재는 wood 1종이라 1.30x 으로 통일 - 추후 stone/paved 추가 가능.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FloorEntity : MonoBehaviour
    {
        public bool IsIndoor => true;

        /// <summary>이동 속도 보너스 - wiki: paved 50%.  wood 보드는 30%.</summary>
        public const float MoveSpeedMul = 1.30f;
    }
}
