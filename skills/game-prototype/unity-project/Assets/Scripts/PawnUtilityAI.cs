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
        private PawnHauler hauler;  // #116 — wood pile pickup
        private PawnBuilder builder;  // #118 — blueprint 건설
        private PawnMiner miner;      // #119 — 채광
        private PawnDoctor doctor;    // #125 — 의료
        private PawnSchedule schedule; // #126 — 시간대별 행동
        private PawnNeeds needs;
        private PawnEntity entity;  // Day 48 — drafted state check
        private PawnWorkSettings workSettings;  // #114 — per-pawn work priority
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
            hauler = GetComponent<PawnHauler>();  // #116
            builder = GetComponent<PawnBuilder>();  // #118
            miner = GetComponent<PawnMiner>();      // #119
            doctor = GetComponent<PawnDoctor>();    // #125
            schedule = GetComponent<PawnSchedule>();// #126
            needs = GetComponent<PawnNeeds>();
            entity = GetComponent<PawnEntity>();
            workSettings = GetComponent<PawnWorkSettings>();  // #114
            // R5: ctx + action priority list
            ctx = new PawnContext
            {
                entity = entity, movement = movement, chopper = chopper,
                gatherer = gatherer, hunter = hunter, cook = cook,
                hauler = hauler,
                builder = builder,
                miner = miner,
                doctor = doctor,
                needs = needs, skills = GetComponent<PawnSkills>(),
                transform = transform,
                idleWanderRadius = idleWanderRadius,
            };
            actions = new List<IPawnAction>
            {
                new TendPatientAction(),       // #125 - 부상 동료 치료 최우선
                new EatBerryAction   { foodThreshold = foodHungryThreshold },
                new HuntAnimalAction { globalFoodThreshold = globalFoodLowThreshold },
                // #137 운영자 fb fix: foodSurplus 5 → 15.  starter food=10 일 때
                //  모든 pawn 이 cook 만 → food 떨어지면 hunt → 다시 cook 무한 loop,
                //  ChopTree 영영 안 됨 = "목재 안 캐짐" 진짜 원인.
                new CookMealAction   { foodSurplus = 15f },
                new BuildBlueprintAction(),  // #118 - 청사진 건설 (chop 보다 우선)
                new HaulWoodAction(),     // #116 - 벌목 후 떨어진 wood pile 운반 (chop 보다 우선)
                new HaulStoneAction(),    // #119 - 채광 후 떨어진 stone chunk 운반
                new HaulMeatAction(),     // #129 - 사냥 후 떨어진 meat pile 운반
                new MineStoneAction(),    // #119 - 광맥 채광 (chop 과 동급)
                new ChopTreeAction(),
                new WanderAction(),
            };
        }

        private void Update()
        {
            // Day 48: drafted pawn skip utility AI — manual control only.
            if (entity != null && entity.IsDrafted)
            {
                HandleDraftedCombat();
                return;
            }
            // 운영자 피드백: 우클릭 이동이 AI 에 즉시 override 됐던 문제 fix.
            //  ClickSelector 가 ManualMoveUntil 을 Time.time+5 로 설정하면
            //  그 동안 AI Decide skip (사용자 이동 명령 존중).
            if (entity != null && entity.IsUnderManualControl) return;

            if (Time.timeSinceLevelLoad - lastDecision < decisionInterval) return;

            if (needs != null && needs.IsSleeping)
            {
                movement.ClearTarget();
                chopper.ClearTask();
                if (gatherer != null) gatherer.ClearTask();
                if (hunter != null) hunter.ClearTask();
                if (hauler != null) hauler.ClearTask();
                if (builder != null) builder.ClearTask();
                if (miner != null) miner.ClearTask();
                if (doctor != null) doctor.ClearTask();
                lastDecision = Time.timeSinceLevelLoad;
                return;
            }
            if (needs != null && needs.IsBreaking)
            {
                chopper.ClearTask();
                if (gatherer != null) gatherer.ClearTask();
                if (hunter != null) hunter.ClearTask();
                if (hauler != null) hauler.ClearTask();
                if (builder != null) builder.ClearTask();
                if (miner != null) miner.ClearTask();
                if (doctor != null) doctor.ClearTask();
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
            if (hauler != null && hauler.HasTask) return;
            if (builder != null && builder.HasTask) return;
            if (miner != null && miner.HasTask) return;
            if (doctor != null && doctor.HasTask) return;

            // #126 → 운영자 fb fix: Schedule slot 이 work 를 hard-block 하면
            //  startHour=6 (Sleep) 일 때 nothing 하는 회귀 발생.
            //  Schedule 은 UI 표시 + hint 만, work 막지 X.
            //  실제 휴식은 needs.IsSleeping (sleep<30 && night) 가 처리.

            lastDecision = Time.timeSinceLevelLoad;
            Decide();
        }

        private void Decide()
        {
            // R5: Strategy pattern — priority list 순회.  첫 TryStart 가 true 반환 시 종료.
            // #114: PawnWorkSettings 가 disable 한 work 는 skip.  priority 1(highest) 부터.
            //  순서: 베리채집(Gather) → 사냥(Hunt) → 요리(Cook) → 벌목(Chop) → 어슬렁(fallback)
            //  새 action 추가 = AI/PawnActions.cs 에 class + Awake actions 리스트에 등록
            if (workSettings == null)
            {
                foreach (var action in actions)
                    if (action.TryStart(ctx)) return;
                return;
            }
            // priority 1 → 2 → 3 → 4 순서로 시도.  0 (disabled) 은 skip.
            for (int p = 1; p <= 4; p++)
            {
                foreach (var action in actions)
                {
                    if (workSettings.GetPriority(action.Kind) != p) continue;
                    if (action.TryStart(ctx)) return;
                }
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
