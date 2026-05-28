using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// #119 - 림월드 채광 작업.  PawnChopper 와 동일 패턴.
    ///  StoneVeinEntity target 설정 후 mineRange 도달 → 데미지 누적 → 소진 시 chunks drop.
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnMiner : MonoBehaviour
    {
        [SerializeField] private float mineRange = 1.2f;
        [SerializeField] private float mineDamagePerSec = 20f;  // 광맥 HP 200 → 10s
        [SerializeField] private float giveUpAfterSec = 12f;

        private StoneVeinEntity targetVein;
        private PawnMovement movement;
        // #199 B2 (R-1) - path-aware give-up (see WorkGiveUp).
        private WorkGiveUp giveUp;

        public bool HasTask => targetVein != null;
        public StoneVeinEntity Target => targetVein;

        private void Awake() { movement = GetComponent<PawnMovement>(); }

        public void SetVeinTarget(StoneVeinEntity vein)
        {
            targetVein = vein;
            if (vein != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, vein.transform.position));
                movement.SetTarget(vein.transform.position);
            }
        }

        public void ClearTask()
        {
            targetVein = null;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (targetVein == null) return;
            if (targetVein.IsDestroyed) { ClearTask(); return; }
            float dist = Vector2.Distance(transform.position, targetVein.transform.position);
            // #199 B2 (R-1) - give up only on real unreachability/stall, not detour.
            if (dist > mineRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, giveUpAfterSec))
            {
                Debug.Log($"[Miner] {name} give up vein (dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                ClearTask();
                return;
            }
            if (dist <= mineRange)
            {
                movement.ClearTarget();
                var abil = GetComponent<PawnAbilities>();  // #120
                float mul = abil != null ? abil.miningMul * abil.manipulation : 1f;
                // #164 - PawnTraits workSpeedMul 적용
                var traits = GetComponent<PawnTraits>();
                if (traits != null) mul *= traits.workSpeedMul;
                // #177 - Chop skill level 재활용 (Mining 별도 skill 없음, +4%/lvl)
                var skills = GetComponent<PawnSkills>();
                if (skills != null) mul *= 1f + skills.GetLevel(SkillKind.Chop) * 0.04f;
                bool done = targetVein.TakeMineDamage(mineDamagePerSec * Time.deltaTime * mul);
                if (skills != null) skills.AddXP(SkillKind.Chop, mineDamagePerSec * Time.deltaTime * 0.5f);
                if (done) ClearTask();
            }
            else
            {
                movement.SetTarget(targetVein.transform.position);
            }
        }
    }
}
