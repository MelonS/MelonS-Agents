using UnityEngine;

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
        // I19 safety - 10s 안에 stove 못 가면 포기 (settlement 정상이면 항상 도달, edge case 만 fire)
        private float taskStartTime = -10f;
        private const float GiveUpAfterSec = 10f;

        public bool HasTask => targetStove != null;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetStoveTarget(StoveEntity s)
        {
            targetStove = s;
            taskStartTime = Time.time;
            if (s != null) movement.SetTarget(s.transform.position);
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
            if (Time.time - taskStartTime > GiveUpAfterSec && dist > cookRange)
            {
                ClearTask();
                return;
            }
            if (dist <= cookRange)
            {
                movement.ClearTarget();
                if (Time.time - lastCookTime >= cookInterval)
                {
                    // #131 - Build skill 5+ pawn 이 만들면 fine meal (mood +20)
                    var sk = GetComponent<PawnSkills>();
                    bool fine = sk != null && sk.GetLevel(SkillKind.Build) >= 5;
                    if (targetStove.CookOne(fine))
                    {
                        lastCookTime = Time.time;
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
