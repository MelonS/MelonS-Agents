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
        // #199 C1 — stand adjacent (RimWorld).  1.2 → 1.5 to accept diagonal
        //  adjacency (√2≈1.414 center-to-center).
        [SerializeField] private float gatherRange = 1.5f;
        [SerializeField] private float gatherInterval = 0.8f;

        private BerryBushEntity targetBush;
        private PawnMovement movement;
        private float lastGatherTime = -999f;
        // #199 B2 (R-1) - path-aware give-up (see WorkGiveUp).
        private WorkGiveUp giveUp;
        private const float GiveUpAfterSec = 10f;
        // #199 C2 — reserved stand cell next to the bush.
        private Vector2Int standCell = PawnMovement.INVALID_CELL;

        public bool HasTask => targetBush != null;
        public BerryBushEntity Target => targetBush;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetBushTarget(BerryBushEntity bush)
        {
            if (targetBush != null && targetBush != bush)
                MelonS.GameProto.AI.ReservationManager.Release(targetBush, gameObject);
            ReleaseStandCell();
            targetBush = bush;
            if (bush != null)
            {
                MelonS.GameProto.AI.ReservationManager.TryReserve(bush, gameObject);
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, bush.transform.position));
                WalkToWork();
            }
        }

        // #199 C1/C2 — walk to a RESERVED adjacent walkable cell next to the bush.
        private void WalkToWork()
        {
            if (targetBush == null) return;
            if (PawnMovement.TryReserveWorkStandPos(targetBush.transform.position,
                    new Vector2Int(1, 1), transform.position, gameObject, ref standCell, out Vector2 stand))
                movement.SetTarget(stand);
            else
                ClearTask();   // no free adjacent stand cell → bush unreachable/occupied
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
            if (targetBush != null)
                MelonS.GameProto.AI.ReservationManager.Release(targetBush, gameObject);
            ReleaseStandCell();
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
            if (dist <= gatherRange || movement.AtStandCell(standCell))  // #199 C2 stand-cell in-range
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
                        // #215 운영자 fb "자원 순간이동" — 즉시 AddFood(텔레포트) 대신 물리 식량
                        //  더미를 덤불 자리에 떨어뜨린다.  hauler 가 저장고로 운반해야 적립되고,
                        //  배고픈 림은 그 자리서 직접 집어먹는다(crop/wood/stone 과 동일 흐름).
                        MeatPileEntity.Spawn(targetBush.transform.position, got, MeatPileEntity.SharedSprite);
                        // Day 19: Gather XP per berry (skills 위에서 GetComponent 이미 됨)
                        if (skills != null) skills.AddXP(SkillKind.Gather, 8f * got);
                    }
                    if (targetBush.IsDepleted) ClearTask();
                }
            }
            else
            {
                // Keep walking toward the adjacent stand cell (SetTarget caches A*).
                WalkToWork();
            }
        }
    }
}
