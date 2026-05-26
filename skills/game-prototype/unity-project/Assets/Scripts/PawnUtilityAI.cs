using System.Collections.Generic;
using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 4 utility AI + Day 11 food + Day 20 mood break + Day 24 hunt.
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    [RequireComponent(typeof(PawnChopper))]
    public class PawnUtilityAI : MonoBehaviour
    {
        [SerializeField] private float decisionInterval = 1.5f;
        [SerializeField] private float idleWanderRadius = 3f;
        [Header("Day 11: food gather priority")]
        [SerializeField] private float foodHungryThreshold = 40f;
        [Header("Day 24: hunt when stockpile food low")]
        [SerializeField] private float globalFoodLowThreshold = 10f;

        private PawnMovement movement;
        private PawnChopper chopper;
        private PawnGatherer gatherer;
        private PawnHunter hunter;
        private PawnCook cook;
        private PawnNeeds needs;
        private PawnEntity entity;  // Day 48 — drafted state check
        private float lastDecision = -999f;
        private float lastDraftAttackTime = -999f;
        private const float DraftAttackInterval = 0.8f;
        // Day 50: bow ranged attack
        [SerializeField] private Sprite arrowSprite;
        private const float RangedAttackRange = 5.0f;
        private const float RangedAttackInterval = 1.5f;
        private float lastRangedAttackTime = -999f;
        public void SetArrowSprite(Sprite s) { arrowSprite = s; }

        // R5: Strategy pattern — Decide() priority list + reusable context
        private PawnContext ctx;
        private List<IPawnAction> actions;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
            chopper = GetComponent<PawnChopper>();
            gatherer = GetComponent<PawnGatherer>();
            hunter = GetComponent<PawnHunter>();
            cook = GetComponent<PawnCook>();
            needs = GetComponent<PawnNeeds>();
            entity = GetComponent<PawnEntity>();
            // R5: ctx + action priority list
            ctx = new PawnContext
            {
                entity = entity, movement = movement, chopper = chopper,
                gatherer = gatherer, hunter = hunter, cook = cook,
                needs = needs, skills = GetComponent<PawnSkills>(),
                transform = transform,
                idleWanderRadius = idleWanderRadius,
            };
            actions = new List<IPawnAction>
            {
                new EatBerryAction   { foodThreshold = foodHungryThreshold },
                new HuntAnimalAction { globalFoodThreshold = globalFoodLowThreshold },
                new CookMealAction   { foodSurplus = 5f },
                new ChopTreeAction(),
                new WanderAction(),
            };
        }

        private void Update()
        {
            // Day 48: drafted pawn skip utility AI — manual control only.
            //  But it still needs combat tick: if it has DraftedAttackTarget,
            //  chase + attack.  If DraftedHuntTarget, chase + attack.
            if (entity != null && entity.IsDrafted)
            {
                HandleDraftedCombat();
                return;
            }

            if (Time.timeSinceLevelLoad - lastDecision < decisionInterval) return;

            if (needs != null && needs.IsSleeping)
            {
                movement.ClearTarget();
                chopper.ClearTask();
                if (gatherer != null) gatherer.ClearTask();
                if (hunter != null) hunter.ClearTask();
                lastDecision = Time.timeSinceLevelLoad;
                return;
            }
            if (needs != null && needs.IsBreaking)
            {
                chopper.ClearTask();
                if (gatherer != null) gatherer.ClearTask();
                if (hunter != null) hunter.ClearTask();
                if (!movement.IsMoving)
                {
                    Vector2 cur = transform.position;
                    movement.SetTarget(cur + Random.insideUnitCircle * idleWanderRadius);
                }
                lastDecision = Time.timeSinceLevelLoad;
                return;
            }

            if (movement.IsMoving || chopper.HasTask) return;
            if (gatherer != null && gatherer.HasTask) return;
            if (hunter != null && hunter.HasTask) return;
            if (cook != null && cook.HasTask) return;

            lastDecision = Time.timeSinceLevelLoad;
            Decide();
        }

        private void Decide()
        {
            // R5: Strategy pattern — priority list 순회.  첫 TryStart 가 true 반환 시 종료.
            //  순서: 베리채집 → 사냥 → 요리 → 벌목 → 어슬렁
            //  새 action 추가 = AI/PawnActions.cs 에 class + Awake actions 리스트에 등록
            foreach (var action in actions)
            {
                if (action.TryStart(ctx)) return;
            }
        }

        // R5: FindNearestStove/Animal/Bush/Tree moved to AI/PawnActions.cs (각 action 내부)

        // Day 48: drafted pawn 전투 처리.  manual move target는 이미
        //  ClickSelector 가 PawnMovement에 박았음.  여기선 attack/hunt
        //  target 가 있으면 추격 + 공격.
        private void HandleDraftedCombat()
        {
            if (entity == null) return;
            BanditEnemy bandit = entity.DraftedAttackTarget;
            AnimalEntity animal = entity.DraftedHuntTarget;
            WolfEnemy wolf = entity.DraftedWolfTarget;
            // Clean up dead/null targets
            if (bandit != null && bandit.IsDead) { entity.DraftedAttackTarget = null; bandit = null; }
            if (wolf != null && wolf.IsDead) { entity.DraftedWolfTarget = null; wolf = null; }
            if (animal != null && animal.gameObject == null)
            { entity.DraftedHuntTarget = null; animal = null; }
            if (bandit == null && animal == null && wolf == null) return;
            Vector2 me = transform.position;
            const float attackRange = 1.2f;
            // Day 50: 활 연구 완료 + arrow sprite 존재 시 ranged 시도 (melee보다 우선)
            bool canShoot = arrowSprite != null
                            && ResearchManager.Instance != null
                            && ResearchManager.Instance.IsUnlocked("simple_bow");
            Vector2 targetPos;
            bool inRange;
            if (canShoot)
            {
                Vector2 rPos = Vector2.zero; bool haveTarget = false; int rDmg = 4;
                if (bandit != null) { rPos = bandit.transform.position; haveTarget = true; rDmg = 4; }
                else if (wolf != null) { rPos = wolf.transform.position; haveTarget = true; rDmg = 5; }
                else if (animal != null) { rPos = animal.transform.position; haveTarget = true; rDmg = 3; }
                if (haveTarget)
                {
                    float d = Vector2.Distance(me, rPos);
                    if (d > attackRange && d <= RangedAttackRange)
                    {
                        movement.ClearTarget();
                        if (Time.time - lastRangedAttackTime > RangedAttackInterval)
                        {
                            lastRangedAttackTime = Time.time;
                            Vector2 dir = (rPos - me).normalized;
                            ArrowProjectile.SpawnArrow(new Vector3(me.x, me.y, 0f), dir, rDmg, gameObject, arrowSprite);
                            var skills = GetComponent<PawnSkills>();
                            if (skills != null) skills.AddXP(SkillKind.Combat, 12f);
                        }
                        return;
                    }
                }
            }
            if (bandit != null)
            {
                targetPos = bandit.transform.position;
                inRange = Vector2.Distance(me, targetPos) <= attackRange;
                if (!inRange) movement.SetTarget(targetPos);
                else
                {
                    movement.ClearTarget();
                    if (Time.time - lastDraftAttackTime > DraftAttackInterval)
                    {
                        lastDraftAttackTime = Time.time;
                        bandit.TakeDamage(2, gameObject);
                        var skills = GetComponent<PawnSkills>();
                        if (skills != null) skills.AddXP(SkillKind.Combat, 8f);
                    }
                }
            }
            else if (wolf != null)
            {
                targetPos = wolf.transform.position;
                inRange = Vector2.Distance(me, targetPos) <= attackRange;
                if (!inRange) movement.SetTarget(targetPos);
                else
                {
                    movement.ClearTarget();
                    if (Time.time - lastDraftAttackTime > DraftAttackInterval)
                    {
                        lastDraftAttackTime = Time.time;
                        wolf.TakeDamage(3, gameObject);
                        var skills = GetComponent<PawnSkills>();
                        if (skills != null) skills.AddXP(SkillKind.Combat, 10f);
                    }
                }
            }
            else if (animal != null)
            {
                targetPos = animal.transform.position;
                inRange = Vector2.Distance(me, targetPos) <= attackRange;
                if (!inRange) movement.SetTarget(targetPos);
                else
                {
                    movement.ClearTarget();
                    if (Time.time - lastDraftAttackTime > DraftAttackInterval)
                    {
                        lastDraftAttackTime = Time.time;
                        animal.TakeDamage(2);
                        var skills = GetComponent<PawnSkills>();
                        if (skills != null) skills.AddXP(SkillKind.Combat, 5f);
                    }
                }
            }
        }
    }
}
