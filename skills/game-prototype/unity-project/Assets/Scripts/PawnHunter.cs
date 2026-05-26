using UnityEngine;

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

        public bool HasTask => targetAnimal != null && !targetAnimal.IsDead;
        public AnimalEntity Target => targetAnimal;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetAnimalTarget(AnimalEntity animal)
        {
            targetAnimal = animal;
            if (animal != null) movement.SetTarget(animal.transform.position);
        }

        public void ClearTask()
        {
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
            float dist = Vector2.Distance(transform.position, targetAnimal.transform.position);
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
