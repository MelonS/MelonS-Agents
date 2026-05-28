using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #118 - 림월드 Construction 작업.
    ///  nearest BlueprintEntity 찾아가 5초 동안 work → 완성.
    ///  PawnHauler 와 동일 패턴 (target reserve, walk, accumulate progress).
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnBuilder : MonoBehaviour
    {
        [SerializeField] private float buildRange = 1.5f;
        [SerializeField] private float giveUpAfterSec = 12f;

        private BlueprintEntity targetBp;
        private PawnMovement movement;
        // #199 B2 (R-1) - path-aware give-up (see WorkGiveUp).
        private WorkGiveUp giveUp;

        public bool HasTask => targetBp != null;
        public BlueprintEntity Target => targetBp;

        private void Awake() { movement = GetComponent<PawnMovement>(); }

        public void SetBlueprintTarget(BlueprintEntity bp)
        {
            if (targetBp != null && targetBp.ReservedBy == gameObject)
                targetBp.ReservedBy = null;
            targetBp = bp;
            if (bp != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, bp.transform.position));
                bp.ReservedBy = gameObject;
                movement.SetTarget(bp.transform.position);
            }
        }

        public void ClearTask()
        {
            if (targetBp != null && targetBp.ReservedBy == gameObject)
                targetBp.ReservedBy = null;
            targetBp = null;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (targetBp == null) return;
            // blueprint 사라졌으면 종료
            if (targetBp.gameObject == null) { targetBp = null; return; }
            if (targetBp.IsComplete) { ClearTask(); return; }

            float dist = Vector2.Distance(transform.position, targetBp.transform.position);
            // #199 B2 (R-1) - give up only on real unreachability/stall, not detour.
            if (dist > buildRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, giveUpAfterSec))
            {
                Debug.Log($"[Builder] {name} give up blueprint (dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                ClearTask();
                return;
            }
            if (dist <= buildRange)
            {
                movement.ClearTarget();
                var abil = GetComponent<PawnAbilities>();  // #120
                float mul = abil != null ? abil.constructionMul * abil.manipulation : 1f;
                // #164 - PawnTraits workSpeedMul (Industrious 1.30x / Lazy 0.75x)
                var traits = GetComponent<PawnTraits>();
                if (traits != null) mul *= traits.workSpeedMul;
                // #177 - Build skill level (+4%/lvl, lvl 10 = +40%)
                var skills = GetComponent<PawnSkills>();
                if (skills != null) mul *= 1f + skills.GetLevel(SkillKind.Build) * 0.04f;
                bool done = targetBp.AddWork(Time.deltaTime * mul);
                // build skill XP
                if (skills != null) skills.AddXP(SkillKind.Build, 5f * Time.deltaTime);
                if (done)
                {
                    Debug.Log($"[Builder] {name} 건설 완료 ({targetBp.Mode})");
                    targetBp = null;  // destroyed
                }
            }
            else
            {
                movement.SetTarget(targetBp.transform.position);
            }
        }
    }
}
