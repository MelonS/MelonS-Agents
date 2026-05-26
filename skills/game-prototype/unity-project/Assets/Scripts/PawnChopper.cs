using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Pawn behavior: walk to assigned tree and chop until destroyed.
    /// Day 3 = single-task model (one tree target at a time, no queue).
    /// Day 4+ utility AI may auto-pick trees.
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnChopper : MonoBehaviour
    {
        [SerializeField] private float chopRange = 1.2f;
        [SerializeField] private float chopDamagePerSec = 25f;

        private TreeEntity targetTree;
        private PawnMovement movement;

        public bool HasTask => targetTree != null;
        public TreeEntity Target => targetTree;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetTreeTarget(TreeEntity tree)
        {
            targetTree = tree;
            if (tree != null) movement.SetTarget(tree.transform.position);
        }

        public void ClearTask()
        {
            targetTree = null;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (targetTree == null) return;
            if (targetTree.IsDestroyed)
            {
                ClearTask();
                return;
            }

            float dist = Vector2.Distance(transform.position, targetTree.transform.position);
            if (dist <= chopRange)
            {
                // In range — stop walking, chop
                movement.ClearTarget();
                bool destroyed = targetTree.TakeChopDamage(chopDamagePerSec * Time.deltaTime);
                // Day 19: Chop XP — granted each frame proportional to dmg
                var skills = GetComponent<PawnSkills>();
                if (skills != null) skills.AddXP(SkillKind.Chop, chopDamagePerSec * Time.deltaTime * 0.5f);
                if (destroyed) ClearTask();
            }
            else
            {
                // Keep walking toward tree (re-target every frame in case tree moved — it doesn't, but safe)
                movement.SetTarget(targetTree.transform.position);
            }
        }
    }
}
