using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #118 - 레퍼런스 콜로니심 Construction 작업.
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

        // ----------------------------------------------------------------------
        // W-M4-01 Lane A / wiki #15 Deconstruct — the deconstruct ACTION lives on
        //  the EXISTING builder so it REUSES the build/haul pathing wholesale: the
        //  same WorkGiveUp, the same TryReserveWorkStandPos adjacent-stand-cell
        //  logic, the same DistanceToFootprint in-range test, the same AddWork-style
        //  per-frame accumulation.  NO new pathfinding is introduced.
        //
        //  Dispatch is owned by DeconstructDesignation.cs (the self-bootstrapping
        //  manager): each frame it poll-finds idle builders and hands them a
        //  DeconstructTarget, exactly the way BuildBlueprintAction hands them a
        //  BlueprintEntity — but without touching PawnUtilityAI/PawnContext/
        //  PawnActions (those are out of this lane), so the manager assigns the
        //  target directly via SetDeconstructTarget below.
        //
        //  A builder works EITHER a blueprint OR a deconstruct target, never both;
        //  HasTask reports either so the utility AI / DeconstructDesignation both
        //  see the builder as busy and don't double-assign.
        private DeconstructTarget targetDe;
        private Vector2Int deStandCell = PawnMovement.INVALID_CELL;
        private WorkGiveUp deGiveUp;

        public bool HasTask => targetBp != null || targetDe != null;
        public BlueprintEntity Target => targetBp;
        public DeconstructTarget DeconstructTarget => targetDe;
        public bool HasDeconstructTask => targetDe != null;

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
        //  footprint (the reference sim).  Multi-cell aware (1×2 bed / 2×1 bench): adjacent to
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
            ClearDeconstructTask();
            movement.ClearTarget();
        }

        // ======================================================================
        //  W-M4-01 Lane A / wiki #15 — Deconstruct action (REUSES build pathing).
        // ======================================================================

        /// <summary>
        /// Assign a deconstruct target.  Called by DeconstructDesignation when it
        /// hands an idle builder a marked structure.  Mirrors SetBlueprintTarget:
        /// release the previous target + stand cell, reserve the new one through the
        /// SAME central ReservationManager (the structure's GameObject is the key),
        /// then walk to a reserved adjacent stand cell via the existing pathing.
        /// </summary>
        public void SetDeconstructTarget(DeconstructTarget de)
        {
            if (targetDe != null && targetDe != de)
                targetDe.ReleaseFrom(gameObject);
            ReleaseDeStandCell();
            targetDe = de;
            if (de != null)
            {
                // A deconstruct target and a build target are mutually exclusive —
                //  drop any blueprint work so the builder commits to the removal.
                if (targetBp != null)
                {
                    if (targetBp.ReservedBy == gameObject) targetBp.ReservedBy = null;
                    MelonS.GameProto.AI.ReservationManager.Release(targetBp, gameObject);
                    ReleaseStandCell();
                    targetBp = null;
                }
                deGiveUp.Reset(Time.time, Vector2.Distance(transform.position, de.transform.position));
                de.AssignTo(gameObject);
                WalkToDeconstruct();
            }
        }

        private void WalkToDeconstruct()
        {
            if (targetDe == null) return;
            // Footprint 1×1 (walls/doors/stoves) — beds are 1×2; read it off the
            //  target so the adjacent-stand-cell math is correct for any structure.
            if (PawnMovement.TryReserveWorkStandPos(targetDe.transform.position, targetDe.Footprint,
                    transform.position, gameObject, ref deStandCell, out Vector2 stand))
                movement.SetTarget(stand);
            else
            {
                Debug.Log($"[Builder] {name} give up deconstruct (no free adjacent stand cell)");
                ClearDeconstructTask();
            }
        }

        private void ReleaseDeStandCell()
        {
            if (deStandCell.x != PawnMovement.INVALID_CELL.x)
            {
                MelonS.GameProto.AI.ReservationManager.ReleaseCell(deStandCell, gameObject);
                deStandCell = PawnMovement.INVALID_CELL;
            }
        }

        public void ClearDeconstructTask()
        {
            if (targetDe != null)
                targetDe.ReleaseFrom(gameObject);
            ReleaseDeStandCell();
            targetDe = null;
        }

        private void Update()
        {
            // Deconstruct work takes the dedicated branch (it never coexists with a
            //  blueprint — SetDeconstructTarget drops any build task first).
            if (targetDe != null) { UpdateDeconstruct(); return; }
            // #버그헌트4(2026-06-05): 외부에서 청사진 파괴(컨텍스트/철거박스 취소) → fake-null 이면
            //  이전엔 그냥 return 해 WalkToWork 가 잡은 stand-cell 예약이 누수됐다.  ReleaseStandCell 로
            //  해제.  주의: targetBp==null 은 '건설중 아님'(거의 매 프레임)에도 true 이므로 movement 를
            //  건드리는 ClearTask 를 부르면 모든 비-건설 폰의 이동이 매 프레임 wipe 돼 전 폰이 마비된다.
            //  ReleaseStandCell 은 movement 무관 + standCell INVALID 면 no-op(매 프레임 안전).
            if (targetBp == null) { ReleaseStandCell(); return; }
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

        // ----------------------------------------------------------------------
        //  wiki #15 — per-frame deconstruct work.  Structurally identical to the
        //  build branch above (same give-up, same in-range test, same skill-scaled
        //  per-frame accumulation), but it BURNS DOWN the structure's HP-as-work
        //  instead of building progress, and on completion REFUNDS ~50% material
        //  and removes the structure.  Removing the WallEntity reopens its PathGrid
        //  cell automatically (WallEntity.OnDestroy → PathGrid.SetStructureBlocked
        //  false → ref-count decrement → Version bump → in-flight pawns re-path),
        //  mirroring EXACTLY how WallEntity.Start INCREMENTED it on build.
        private void UpdateDeconstruct()
        {
            // Structure already gone (destroyed by something else / another pawn) →
            //  release and let the AI pick fresh work.
            if (targetDe == null || targetDe.gameObject == null || targetDe.IsRemoved)
            {
                ClearDeconstructTask();
                movement.ClearTarget();
                return;
            }

            float dist = PawnMovement.DistanceToFootprint(
                targetDe.transform.position, targetDe.Footprint, transform.position);

            if (dist > buildRange && deGiveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, giveUpAfterSec))
            {
                Debug.Log($"[Builder] {name} give up deconstruct (dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                ClearDeconstructTask();
                movement.ClearTarget();
                return;
            }

            if (dist <= buildRange || movement.AtStandCell(deStandCell))
            {
                movement.ClearTarget();
                // Same construction-skill scaling as building — deconstruct is a
                //  Construction job in the reference sim too.
                var abil = GetComponent<PawnAbilities>();
                float mul = abil != null ? abil.constructionMul * abil.manipulation : 1f;
                var traits = GetComponent<PawnTraits>();
                if (traits != null) mul *= traits.workSpeedMul;
                var skills = GetComponent<PawnSkills>();
                if (skills != null) mul *= 1f + skills.GetLevel(SkillKind.Build) * 0.04f;

                bool done = targetDe.AddWork(Time.deltaTime * mul);
                if (skills != null) skills.AddXP(SkillKind.Build, 5f * Time.deltaTime);
                if (done)
                {
                    Debug.Log($"[Builder] {name} 해체 완료 ({targetDe.name})");
                    // CompleteRemoval refunds material + destroys the structure
                    //  (which clears the PathGrid cell via WallEntity.OnDestroy).
                    targetDe.CompleteRemoval();
                    ClearDeconstructTask();
                }
            }
            else
            {
                WalkToDeconstruct();
            }
        }
    }
}
