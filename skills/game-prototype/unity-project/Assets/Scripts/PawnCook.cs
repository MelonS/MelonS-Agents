using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>Day 26: idle pawn walks to a Stove and cooks meals.
    /// Same shape as PawnGatherer.  Cooks 1 meal per cookInterval if
    /// stockpile food >= 3.  Stops when food < 3 or stove destroyed.</summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnCook : MonoBehaviour
    {
        [SerializeField] private float cookRange = 1.0f;
        [SerializeField] private float cookInterval = 1.5f;

        private StoveEntity targetStove;
        private PawnMovement movement;
        private float lastCookTime = -10f;
        // #199 B2 (R-1) - path-aware give-up (see WorkGiveUp).
        private WorkGiveUp giveUp;
        private const float GiveUpAfterSec = 10f;

        public bool HasTask => targetStove != null;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetStoveTarget(StoveEntity s)
        {
            targetStove = s;
            if (s != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, s.transform.position));
                movement.SetTarget(s.transform.position);
            }
        }

        public void ClearTask()
        {
            targetStove = null;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (targetStove == null) return;
            if (ResourceManager.Instance == null || ResourceManager.Instance.food < 3)
            {
                ClearTask();
                return;
            }
            float dist = Vector2.Distance(transform.position, targetStove.transform.position);
            // #199 B2 (R-1) - give up only on real unreachability/stall, not detour.
            if (dist > cookRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, GiveUpAfterSec))
            {
                ClearTask();
                return;
            }
            if (dist <= cookRange)
            {
                movement.ClearTarget();
                // #164 - PawnTraits workSpeedMul 적용 (cook interval 단축).
                var traits = GetComponent<PawnTraits>();
                float traitMul = traits != null ? traits.workSpeedMul : 1f;
                float effectiveInterval = cookInterval / Mathf.Max(0.1f, traitMul);
                if (Time.time - lastCookTime >= effectiveInterval)
                {
                    // #131/#172 - 이전: Build skill 5+ 면 fine meal (wiki 와 mismatch).
                    //  지금: wiki - cooking skill 영향.  PawnAbilities.cookingMul 1.10+ 이면 fine.
                    //  (Cook skill 별도 추가는 #172 PawnSkills 확장 시 진행.)
                    var abil = GetComponent<PawnAbilities>();
                    bool fine = abil != null && abil.cookingMul >= 1.10f;
                    if (targetStove.CookOne(fine))
                    {
                        lastCookTime = Time.time;
                        // legacy: Build skill XP 도 부여 (PawnSkills 확장 전 임시)
                        var sk = GetComponent<PawnSkills>();
                        if (sk != null) sk.AddXP(SkillKind.Build, 8f);
                    }
                }
            }
            else
            {
                movement.SetTarget(targetStove.transform.position);
            }
        }
    }
}
