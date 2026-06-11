using System.Collections.Generic;
using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// #119 - 레퍼런스 콜로니심 채광 작업.  PawnChopper 와 동일 패턴.
    ///  StoneVeinEntity target 설정 후 mineRange 도달 → 데미지 누적 → 소진 시 chunks drop.
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnMiner : MonoBehaviour
    {
        // #199 C1 — stand adjacent (the reference sim).  1.2 → 1.5 for diagonal adjacency.
        [SerializeField] private float mineRange = 1.5f;
        [SerializeField] private float mineDamagePerSec = 20f;  // 광맥 HP 200 → 10s
        [SerializeField] private float giveUpAfterSec = 12f;

        private StoneVeinEntity targetVein;
        private PawnMovement movement;
        // #221 QA fix — 도달 불가 광맥을 포기한 직후 같은 걸 또 골라 무한 포기 루프(로그에서
        //  give-up vein 358회)를 돌던 문제.  포기한 광맥에 쿨다운을 줘 그 동안 다른 광맥을
        //  고르게 한다(막힘이 풀릴 수 있으니 영구 블랙리스트 대신 한시적).
        private readonly Dictionary<StoneVeinEntity, float> _giveUpUntil
            = new Dictionary<StoneVeinEntity, float>();
        private const float GiveUpCooldownSec = 60f;  // #221b 20→60: 도달불가 광맥 재시도 churn ↓
        public bool IsRecentlyGivenUp(StoneVeinEntity v)
            => v != null && _giveUpUntil.TryGetValue(v, out float t) && Time.time < t;
        // #199 B2 (R-1) - path-aware give-up (see WorkGiveUp).
        private WorkGiveUp giveUp;
        // #199 C2 — reserved stand cell next to the vein.
        private Vector2Int standCell = PawnMovement.INVALID_CELL;

        public bool HasTask => targetVein != null;
        public StoneVeinEntity Target => targetVein;

        private void Awake() { movement = GetComponent<PawnMovement>(); }

        public void SetVeinTarget(StoneVeinEntity vein)
        {
            if (targetVein != null && targetVein != vein)
                MelonS.GameProto.AI.ReservationManager.Release(targetVein, gameObject);
            ReleaseStandCell();
            targetVein = vein;
            if (vein != null)
            {
                MelonS.GameProto.AI.ReservationManager.TryReserve(vein, gameObject);
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, vein.transform.position));
                WalkToWork();
            }
        }

        // #199 C1/C2 — walk to a RESERVED adjacent walkable cell next to the vein.
        private void WalkToWork()
        {
            if (targetVein == null) return;
            if (PawnMovement.TryReserveWorkStandPos(targetVein.transform.position,
                    new Vector2Int(1, 1), transform.position, gameObject, ref standCell, out Vector2 stand))
                movement.SetTarget(stand);
            else
            {
                if (targetVein != null) _giveUpUntil[targetVein] = Time.time + GiveUpCooldownSec;  // #221
                Debug.Log($"[Miner] {name} give up vein (no free adjacent stand cell — unreachable/occupied)");
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
            if (targetVein != null)
                MelonS.GameProto.AI.ReservationManager.Release(targetVein, gameObject);
            ReleaseStandCell();
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
                _giveUpUntil[targetVein] = Time.time + GiveUpCooldownSec;  // #221
                Debug.Log($"[Miner] {name} give up vein (dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                ClearTask();
                return;
            }
            if (dist <= mineRange || movement.AtStandCell(standCell))  // #199 C2 stand-cell in-range
            {
                movement.ClearTarget();
                var abil = GetComponent<PawnAbilities>();  // #120
                float mul = abil != null ? abil.miningMul * abil.manipulation : 1f;
                // #164 - PawnTraits workSpeedMul 적용
                var traits = GetComponent<PawnTraits>();
                if (traits != null) mul *= traits.workSpeedMul;
                // 게임루프 차순위 (2026-06-12) — 팔 부상 → 작업속도 (죽은 프로퍼티 배선).
                var hlt = GetComponent<PawnHealth>();
                if (hlt != null) mul *= hlt.WorkSpeedMultiplier();
                // #177 - Chop skill level 재활용 (Mining 별도 skill 없음, +4%/lvl)
                var skills = GetComponent<PawnSkills>();
                if (skills != null) mul *= 1f + skills.GetLevel(SkillKind.Chop) * 0.04f;
                bool done = targetVein.TakeMineDamage(mineDamagePerSec * Time.deltaTime * mul);
                if (skills != null) skills.AddXP(SkillKind.Chop, mineDamagePerSec * Time.deltaTime * 0.5f);
                if (done) ClearTask();
            }
            else
            {
                WalkToWork();
            }
        }
    }
}
