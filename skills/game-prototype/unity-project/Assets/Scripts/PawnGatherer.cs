using UnityEngine;

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
        [SerializeField] private float gatherRange = 1.2f;
        [SerializeField] private float gatherInterval = 0.8f;

        private BerryBushEntity targetBush;
        private PawnMovement movement;
        private float lastGatherTime = -999f;

        public bool HasTask => targetBush != null;
        public BerryBushEntity Target => targetBush;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetBushTarget(BerryBushEntity bush)
        {
            targetBush = bush;
            if (bush != null) movement.SetTarget(bush.transform.position);
        }

        public void ClearTask()
        {
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
            if (dist <= gatherRange)
            {
                // In range — stop walking, gather on interval (NOT every frame —
                // lesson #4 audio-buzz-style throttle pattern, same shape).
                movement.ClearTarget();
                if (Time.time - lastGatherTime >= gatherInterval)
                {
                    int got = targetBush.TakeBerry();
                    lastGatherTime = Time.time;
                    if (got > 0)
                    {
                        ResourceManager.Instance?.AddFood(got);
                    }
                    if (targetBush.IsDepleted) ClearTask();
                }
            }
            else
            {
                // Keep walking toward bush (re-target every frame — bush is
                // static but safe; same pattern as PawnChopper).
                movement.SetTarget(targetBush.transform.position);
            }
        }
    }
}
