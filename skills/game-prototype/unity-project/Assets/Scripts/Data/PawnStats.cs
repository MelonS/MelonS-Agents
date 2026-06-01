using UnityEngine;

namespace MelonS.GameProto.Data
{
    /// <summary>
    /// R2 - PawnEntity 전투/이동 stats 외부 데이터.
    /// 게임 밸런스 = 이 ScriptableObject 의 Inspector 만 수정.
    /// PawnEntity / PawnMovement 가 Awake 에서 fallback 으로 default 인스턴스 생성.
    ///
    /// 이전 (SerializeField 하드코딩):
    ///   PawnEntity.maxHp = 30
    ///   PawnEntity.attackDamage = 1
    ///   PawnEntity.attackRange = 1.0
    ///   PawnEntity.attackInterval = 1.0
    ///   PawnMovement.moveSpeed = 3.0
    /// 이후 (이 SO 단일 출처):
    ///   stats = PawnStatsLoader.Instance
    /// </summary>
    [CreateAssetMenu(fileName = "PawnStats", menuName = "MelonS/PawnStats")]
    public class PawnStats : ScriptableObject
    {
        [Header("Combat")]
        public int   maxHp = 30;
        public int   attackDamage = 1;
        public float attackRange = 1.0f;
        public float attackInterval = 1.0f;

        [Header("Movement")]
        // #200 genre fidelity: human base move speed is 4.6 cells/sec.
        //  Was 3.0 (~35% too slow).  MoveTowards clamps to target so no overshoot.
        public float moveSpeed = 4.6f;
        public float arriveDistance = 0.05f;

        [Header("Bandit AI (defensive)")]
        public float banditSearchInterval = 0.25f;

        /// <summary>
        /// Code-time fallback 인스턴스 - SO asset 없어도 game 멈추지 X.
        /// asset 만들고 Inspector 에서 wire 하면 override.
        /// </summary>
        public static PawnStats CreateDefault()
        {
            var s = ScriptableObject.CreateInstance<PawnStats>();
            s.name = "PawnStats(default)";
            return s;
        }
    }
}
