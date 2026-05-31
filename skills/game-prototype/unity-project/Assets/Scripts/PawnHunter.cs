using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>Day 24: pawn auto-hunts animals when stockpile food low.
    /// Same shape as PawnChopper / PawnGatherer.  Walks to nearest
    /// AnimalEntity, attacks 2 dmg every 0.5s until dead; animal then
    /// drops food 5 (handled by AnimalEntity).</summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnHunter : MonoBehaviour
    {
        [SerializeField] private float attackRange = 1.0f;
        [SerializeField] private float attackInterval = 0.5f;
        [SerializeField] private int attackDamage = 2;

        private AnimalEntity targetAnimal;
        private PawnMovement movement;
        private float lastAttackTime = -10f;
        // #199 B2 (R-1) - path-aware give-up.  Animals MOVE, so the target cell
        //  changes; give-up keys on real unreachability + a no-progress stall
        //  (pawn never closes on a fleeing-but-faster animal), not dist>range.
        private WorkGiveUp giveUp;
        private const float GiveUpAfterSec = 15f;
        // #225 QA fix — give-up 은 dist>attackRange 일 때만 검사돼서, 사정거리 안에서 공격
        //  중(no-move)인데 동물이 안 죽는 경우(도주 oscillation / 도달불가 인접 / 비정상
        //  무적)엔 빠져나가 영구 stuck (LongPlay '서연 사냥 no-move 180s').  사냥 1건이
        //  비정상적으로 오래 끌면(45s) 무조건 포기하는 절대 timeout.
        private float huntStartTime = -999f;
        private const float MaxHuntSec = 45f;

        public bool HasTask => targetAnimal != null && !targetAnimal.IsDead;
        public AnimalEntity Target => targetAnimal;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetAnimalTarget(AnimalEntity animal)
        {
            // #199 C2 — release previous animal on switch (RimWorld job-switch).
            if (targetAnimal != null && targetAnimal != animal)
                MelonS.GameProto.AI.ReservationManager.Release(targetAnimal, gameObject);
            targetAnimal = animal;
            if (animal != null)
            {
                MelonS.GameProto.AI.ReservationManager.TryReserve(animal, gameObject);
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, animal.transform.position));
                huntStartTime = Time.time;  // #225 절대 timeout 기준
                movement.SetTarget(animal.transform.position);
            }
        }

        public void ClearTask()
        {
            if (targetAnimal != null)
                MelonS.GameProto.AI.ReservationManager.Release(targetAnimal, gameObject);
            targetAnimal = null;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (targetAnimal == null) return;
            if (targetAnimal.IsDead)
            {
                ClearTask();
                // Combat XP for the kill
                var sk = GetComponent<PawnSkills>();
                if (sk != null) sk.AddXP(SkillKind.Combat, 20f);
                return;
            }
            // #225 절대 사냥 timeout — 사정거리 안 공격 중이든 밖이든, 45s 안에 못 잡으면 포기
            //  (동물 도주 oscillation / 도달불가 / 비정상으로 안 죽는 경우 stuck 방지).
            if (Time.time - huntStartTime > MaxHuntSec)
            {
                Debug.Log($"[Hunter] {name} 사냥 timeout({MaxHuntSec}s) → 포기");
                ClearTask();
                return;
            }
            float dist = Vector2.Distance(transform.position, targetAnimal.transform.position);
            // #199 B2 (R-1) - give up only on real unreachability/stall.
            if (dist > attackRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, GiveUpAfterSec))
            {
                ClearTask();
                return;
            }
            if (dist <= attackRange)
            {
                movement.ClearTarget();
                if (Time.time - lastAttackTime >= attackInterval)
                {
                    targetAnimal.TakeDamage(attackDamage, gameObject);
                    lastAttackTime = Time.time;
                    var sk = GetComponent<PawnSkills>();
                    if (sk != null) sk.AddXP(SkillKind.Combat, 1f);
                }
            }
            else
            {
                movement.SetTarget(targetAnimal.transform.position);
            }
        }
    }
}
