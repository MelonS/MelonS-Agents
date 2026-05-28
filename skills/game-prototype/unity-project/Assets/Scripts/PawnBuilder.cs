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
        // #199 C2 — reserved stand cell next to the blueprint footprint.
        private Vector2Int standCell = PawnMovement.INVALID_CELL;

        public bool HasTask => targetBp != null;
        public BlueprintEntity Target => targetBp;

        private void Awake() { movement = GetComponent<PawnMovement>(); }

        public void SetBlueprintTarget(BlueprintEntity bp)
        {
            // #199 C2 — release previous blueprint target + stand cell on switch.
            //  Keep BlueprintEntity.ReservedBy in sync (BuildBlueprintAction still
            //  reads it) AND mirror into the central ReservationManager.
            if (targetBp != null && targetBp != bp)
            {
                if (targetBp.ReservedBy == gameObject) targetBp.ReservedBy = null;
                MelonS.GameProto.AI.ReservationManager.Release(targetBp, gameObject);
            }
            ReleaseStandCell();
            targetBp = bp;
            if (bp != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, bp.transform.position));
                bp.ReservedBy = gameObject;
                MelonS.GameProto.AI.ReservationManager.TryReserve(bp, gameObject);
                WalkToWork();
            }
        }

        // #199 C1/C2 — walk to a RESERVED walkable cell ADJACENT to the blueprint
        //  footprint (RimWorld).  Multi-cell aware (1×2 bed / 2×1 bench): adjacent to
        //  any footprint cell, never inside it.
        private void WalkToWork()
        {
            if (targetBp == null) return;
            if (PawnMovement.TryReserveWorkStandPos(targetBp.transform.position, targetBp.Footprint,
                    transform.position, gameObject, ref standCell, out Vector2 stand))
                movement.SetTarget(stand);
            else
            {
                Debug.Log($"[Builder] {name} give up blueprint (no free adjacent stand cell — unreachable/occupied)");
                ClearTask();
            }
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
            if (targetBp != null && targetBp.ReservedBy == gameObject)
                targetBp.ReservedBy = null;
            if (targetBp != null)
                MelonS.GameProto.AI.ReservationManager.Release(targetBp, gameObject);
            ReleaseStandCell();
            targetBp = null;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (targetBp == null) return;
            // blueprint 사라졌으면 종료 (#199 C2 — release reservations via ClearTask so
            //  the destroyed-blueprint case can't leak the stand cell).
            if (targetBp.gameObject == null) { ClearTask(); return; }
            if (targetBp.IsComplete) { ClearTask(); return; }

            // #199 C1 — measure to the NEAREST footprint cell (multi-cell beds /
            //  benches), not the transform centre, so a builder standing adjacent
            //  to one cell of a 1×2 bed counts as in-range.
            float dist = PawnMovement.DistanceToFootprint(
                targetBp.transform.position, targetBp.Footprint, transform.position);
            // #199 B2 (R-1) - give up only on real unreachability/stall, not detour.
            if (dist > buildRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, giveUpAfterSec))
            {
                Debug.Log($"[Builder] {name} give up blueprint (dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                ClearTask();
                return;
            }
            if (dist <= buildRange || movement.AtStandCell(standCell))  // #199 C2 stand-cell in-range
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
                    // #199 C2 — release target + stand cell on completion (the
                    //  blueprint is destroyed by AddWork→Complete; ClearTask's
                    //  fake-null guards handle that and free the stand cell).
                    ClearTask();
                }
            }
            else
            {
                WalkToWork();
            }
        }
    }
}
