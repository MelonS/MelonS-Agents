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
        // I19 bug — tree unreachable (out-of-bounds 등) 일 때 영원히 시도.
        //  10s 동안 in-range 못 들어가면 포기.
        private float taskStartTime = -10f;
        private const float GiveUpAfterSec = 10f;

        public bool HasTask => targetTree != null;
        public TreeEntity Target => targetTree;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetTreeTarget(TreeEntity tree)
        {
            targetTree = tree;
            taskStartTime = Time.time;
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
            // I19 - unreachable target 포기 (PawnMovement clamp 으로 도달 못 하는 경우)
            if (Time.time - taskStartTime > GiveUpAfterSec && dist > chopRange)
            {
                Debug.Log($"[Chopper] {name} give up tree (unreachable after {GiveUpAfterSec}s, dist={dist:F2})");
                ClearTask();
                return;
            }
            if (dist <= chopRange)
            {
                // In range — stop walking, chop
                movement.ClearTarget();
                // #120 - PawnAbilities chop multiplier
                var abil = GetComponent<PawnAbilities>();
                float mul = abil != null ? abil.chopMul * abil.manipulation : 1f;
                // #164 - PawnTraits workSpeedMul (Lazy 0.75x, Industrious 1.30x).
                //  이전: traits 이 wired 되어 있지만 effect 없음 - 표시만.
                var traits = GetComponent<PawnTraits>();
                if (traits != null) mul *= traits.workSpeedMul;
                bool destroyed = targetTree.TakeChopDamage(chopDamagePerSec * Time.deltaTime * mul);
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
