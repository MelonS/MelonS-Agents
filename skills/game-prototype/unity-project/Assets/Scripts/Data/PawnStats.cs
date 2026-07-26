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
        // #다콜로니심식/#8(2026-06-03): 맨손/기본 근접 데미지 1 → 5.  1 이면 강도(20HP·armor
        //  0.12)를 ~20 타(~20초)나 때려야 해 전투가 지루했다(운영자 fb).  활(3~5)·적 HP(18~20)
        //  소스케일과 정합되게 5 로 — ~4 타에 제압(the reference sim 맨손도 몇 대면 제압).  적 공격력은
        //  그대로라 위협 유지(전투가 시시해지지 않음).  무기 장착 시 +무기데미지로 더 강해진다.
        public int   attackDamage = 5;
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
