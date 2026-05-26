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
        private PawnNeeds needs;
        private float lastDecision = -999f;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
            chopper = GetComponent<PawnChopper>();
            gatherer = GetComponent<PawnGatherer>();
            hunter = GetComponent<PawnHunter>();
            needs = GetComponent<PawnNeeds>();
        }

        private void Update()
        {
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

            TreeEntity tree = FindNearestTree();
            if (tree != null) { chopper.SetTreeTarget(tree); return; }

            Vector2 cur2 = transform.position;
            movement.SetTarget(cur2 + Random.insideUnitCircle * idleWanderRadius);
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
    }
}
