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
        // #199 C1 — stand adjacent to the stove (the reference sim).  1.0 was too tight to
        //  even reach a stove from an orthogonal neighbour (center dist 1.0) and
        //  impossible from a diagonal (1.414); bumped to 1.5.
        [SerializeField] private float cookRange = 1.5f;
        [SerializeField] private float cookInterval = 1.5f;

        private StoveEntity targetStove;
        private PawnMovement movement;
        private float lastCookTime = -10f;
        // #199 B2 (R-1) - path-aware give-up (see WorkGiveUp).
        private WorkGiveUp giveUp;
        private const float GiveUpAfterSec = 10f;
        // #199 C2 — reserved stand cell next to the stove.
        private Vector2Int standCell = PawnMovement.INVALID_CELL;

        public bool HasTask => targetStove != null;
        public StoveEntity Target => targetStove;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetStoveTarget(StoveEntity s)
        {
            if (targetStove != null && targetStove != s)
                MelonS.GameProto.AI.ReservationManager.Release(targetStove, gameObject);
            ReleaseStandCell();
            targetStove = s;
            if (s != null)
            {
                MelonS.GameProto.AI.ReservationManager.TryReserve(s, gameObject);
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, s.transform.position));
                WalkToWork();
            }
        }

        // #199 C1/C2 — walk to a RESERVED adjacent walkable cell next to the stove.
        private void WalkToWork()
        {
            if (targetStove == null) return;
            if (PawnMovement.TryReserveWorkStandPos(targetStove.transform.position,
                    new Vector2Int(1, 1), transform.position, gameObject, ref standCell, out Vector2 stand))
                movement.SetTarget(stand);
            else
                ClearTask();   // no free adjacent stand cell → stove unreachable/occupied
        }

        private void ReleaseStandCell()
        {
            if (standCell.x != PawnMovement.INVALID_CELL.x)
            {
                MelonS.GameProto.AI.ReservationManager.ReleaseCell(standCell, gameObject);
                standCell = PawnMovement.INVALID_CELL;
            }
        }

        public void ClearTask()
        {
            if (targetStove != null)
                MelonS.GameProto.AI.ReservationManager.Release(targetStove, gameObject);
            ReleaseStandCell();
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
            if (dist <= cookRange || movement.AtStandCell(standCell))  // #199 C2 stand-cell in-range
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
                    else
                    {
                        // #275 조리 불가(자재 부족/화덕 불가) — 무한 재시도로 stove 앞에 멈추지
                        //  않게 즉시 작업 정리.  AI 가 다른 일을 잡는다.
                        ClearTask();
                        return;
                    }
                }
            }
            else
            {
                WalkToWork();
            }
        }
    }
}
