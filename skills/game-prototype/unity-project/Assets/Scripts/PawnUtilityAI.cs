using UnityEngine;

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

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
            chopper = GetComponent<PawnChopper>();
            gatherer = GetComponent<PawnGatherer>();
            hunter = GetComponent<PawnHunter>();
            cook = GetComponent<PawnCook>();
            needs = GetComponent<PawnNeeds>();
            entity = GetComponent<PawnEntity>();
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
            // Day 11: food priority — pawn 자기 식량 부족 + 베리부시 있음
            if (needs != null && gatherer != null && needs.food < foodHungryThreshold)
            {
                BerryBushEntity bush = FindNearestBush();
                if (bush != null) { gatherer.SetBushTarget(bush); return; }
            }

            // Day 24: hunt — global 식량 비축 부족 + 동물 있음
            if (hunter != null && ResourceManager.Instance != null
                && ResourceManager.Instance.food < globalFoodLowThreshold)
            {
                AnimalEntity deer = FindNearestAnimal();
                if (deer != null) { hunter.SetAnimalTarget(deer); return; }
            }

            // Day 26: cook when stockpile food has surplus (>5) AND stove exists.
            if (cook != null && ResourceManager.Instance != null
                && ResourceManager.Instance.food > 5)
            {
                StoveEntity stove = FindNearestStove();
                if (stove != null) { cook.SetStoveTarget(stove); return; }
            }

            TreeEntity tree = FindNearestTree();
            if (tree != null) { chopper.SetTreeTarget(tree); return; }

            Vector2 cur2 = transform.position;
            movement.SetTarget(cur2 + Random.insideUnitCircle * idleWanderRadius);
        }

        private StoveEntity FindNearestStove()
        {
            var arr = FindObjectsByType<StoveEntity>(FindObjectsSortMode.None);
            StoveEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = transform.position;
            foreach (var s in arr)
            {
                if (s == null) continue;
                float sq = ((Vector2)s.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = s; }
            }
            return best;
        }

        private AnimalEntity FindNearestAnimal()
        {
            var arr = FindObjectsByType<AnimalEntity>(FindObjectsSortMode.None);
            AnimalEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = transform.position;
            foreach (var a in arr)
            {
                if (a == null || a.IsDead) continue;
                float sq = ((Vector2)a.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = a; }
            }
            return best;
        }

        private BerryBushEntity FindNearestBush()
        {
            var arr = FindObjectsByType<BerryBushEntity>(FindObjectsSortMode.None);
            BerryBushEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = transform.position;
            foreach (var b in arr)
            {
                if (b == null || b.IsDepleted) continue;
                float sq = ((Vector2)b.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }
            return best;
        }

        private TreeEntity FindNearestTree()
        {
            var arr = FindObjectsByType<TreeEntity>(FindObjectsSortMode.None);
            TreeEntity best = null;
            float bestSq = float.MaxValue;
            Vector2 me = transform.position;
            foreach (var t in arr)
            {
                if (t == null || t.IsDestroyed) continue;
                float sq = ((Vector2)t.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = t; }
            }
            return best;
        }

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
