using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 11: pawn behavior — walk to assigned berry bush and gather until
    /// depleted.  Mirrors PawnChopper's single-task model exactly (one bush
    /// target at a time, no queue).  Utility AI auto-picks bushes when food
    /// need is low.
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnGatherer : MonoBehaviour
    {
        [SerializeField] private float gatherRange = 1.2f;
        [SerializeField] private float gatherInterval = 0.8f;

        private BerryBushEntity targetBush;
        private PawnMovement movement;
        private float lastGatherTime = -999f;
        // #199 B2 (R-1) - path-aware give-up (see WorkGiveUp).
        private WorkGiveUp giveUp;
        private const float GiveUpAfterSec = 10f;

        public bool HasTask => targetBush != null;
        public BerryBushEntity Target => targetBush;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetBushTarget(BerryBushEntity bush)
        {
            targetBush = bush;
            if (bush != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, bush.transform.position));
                movement.SetTarget(bush.transform.position);
            }
        }

        public void ClearTask()
        {
            targetBush = null;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (targetBush == null) return;
            if (targetBush.IsDepleted)
            {
                ClearTask();
                return;
            }

            float dist = Vector2.Distance(transform.position, targetBush.transform.position);
            // #199 B2 (R-1) - give up only on real unreachability/stall, not detour.
            if (dist > gatherRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, GiveUpAfterSec))
            {
                ClearTask();
                return;
            }
            if (dist <= gatherRange)
            {
                // In range — stop walking, gather on interval (NOT every frame —
                // lesson #4 audio-buzz-style throttle pattern, same shape).
                movement.ClearTarget();
                // #164 - PawnTraits workSpeedMul 적용 (Industrious 1.30x = 짧은 interval).
                // #174 - PawnAbilities.plantsMul × manipulation (이전: SET-only, gather 영향 0).
                // #177 - Gather skill level (+4%/lvl)
                var traits = GetComponent<PawnTraits>();
                var abil = GetComponent<PawnAbilities>();
                var skills = GetComponent<PawnSkills>();
                float traitMul = traits != null ? traits.workSpeedMul : 1f;
                float abilMul = abil != null ? abil.plantsMul * abil.manipulation : 1f;
                float skillMul = skills != null ? 1f + skills.GetLevel(SkillKind.Gather) * 0.04f : 1f;
                float effectiveInterval = gatherInterval / Mathf.Max(0.1f, traitMul * abilMul * skillMul);
                if (Time.time - lastGatherTime >= effectiveInterval)
                {
                    int got = targetBush.TakeBerry();
                    lastGatherTime = Time.time;
                    if (got > 0)
                    {
                        ResourceManager.Instance?.AddFood(got);
                        // Day 19: Gather XP per berry (skills 위에서 GetComponent 이미 됨)
                        if (skills != null) skills.AddXP(SkillKind.Gather, 8f * got);
                    }
                    if (targetBush.IsDepleted) ClearTask();
                }
            }
            else
            {
                // Keep walking toward bush (re-target every frame — bush is
                // static but safe; same pattern as PawnChopper).
                movement.SetTarget(targetBush.transform.position);
            }
        }
    }
}
