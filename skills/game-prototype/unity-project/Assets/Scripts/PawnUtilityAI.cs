using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 4 utility AI — when the pawn is idle (no movement target,
    /// no chop task) for at least <see cref="decisionInterval"/> seconds,
    /// auto-pick the highest-utility action.
    ///
    /// Current decision tree (very simple):
    ///   1. If any TreeEntity exists -> assign chop on nearest tree.
    ///   2. Else wander a random short distance.
    ///
    /// Day 5+ will expand with needs-based prioritization (eat when
    /// hungry, sleep when tired, etc.) and the AI Director's event
    /// preferences.
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    [RequireComponent(typeof(PawnChopper))]
    public class PawnUtilityAI : MonoBehaviour
    {
        [SerializeField] private float decisionInterval = 1.5f;
        [SerializeField] private float idleWanderRadius = 3f;

        private PawnMovement movement;
        private PawnChopper chopper;
        private PawnNeeds needs;
        private float lastDecision = -999f;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
            chopper = GetComponent<PawnChopper>();
            needs = GetComponent<PawnNeeds>();
        }

        private void Update()
        {
            if (Time.timeSinceLevelLoad - lastDecision < decisionInterval) return;

            // Day 10: while sleeping, freeze — don't pick new tasks.
            if (needs != null && needs.IsSleeping)
            {
                movement.ClearTarget();
                chopper.ClearTask();
                lastDecision = Time.timeSinceLevelLoad;
                return;
            }

            // Don't override active task
            if (movement.IsMoving || chopper.HasTask) return;

            lastDecision = Time.timeSinceLevelLoad;
            Decide();
        }

        private void Decide()
        {
            TreeEntity tree = FindNearestTree();
            if (tree != null)
            {
                chopper.SetTreeTarget(tree);
                return;
            }
            // No trees -> wander
            Vector2 cur = transform.position;
            Vector2 offset = Random.insideUnitCircle * idleWanderRadius;
            movement.SetTarget(cur + offset);
        }

        private TreeEntity FindNearestTree()
        {
            TreeEntity[] trees = FindObjectsByType<TreeEntity>(FindObjectsSortMode.None);
            TreeEntity best = null;
            float bestDist = float.MaxValue;
            Vector2 me = transform.position;
            foreach (var t in trees)
            {
                if (t == null || t.IsDestroyed) continue;
                float d = Vector2.Distance(me, t.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = t;
                }
            }
            return best;
        }
    }
}
