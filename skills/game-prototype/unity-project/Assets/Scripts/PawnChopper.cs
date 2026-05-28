using UnityEngine;
using MelonS.GameProto.AI;

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
        // #199 B2 (R-1) — give-up now keys on real path-unreachability
        //  (PawnMovement.LastPathFailed) + a no-progress stall, NOT raw
        //  dist>range (which false-trips while the pawn legitimately detours
        //  around obstacles under A*).  See WorkGiveUp.
        private WorkGiveUp giveUp;
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
            if (tree != null)
            {
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, tree.transform.position));
                movement.SetTarget(tree.transform.position);
            }
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
            // #199 B2 (R-1) - give up only on real unreachability or a genuine
            //  stall, not on dist>range during a legitimate A* detour.
            if (dist > chopRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, GiveUpAfterSec))
            {
                Debug.Log($"[Chopper] {name} give up tree (unreachable/stalled, dist={dist:F2}, pathFailed={movement.LastPathFailed})");
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
                var traits = GetComponent<PawnTraits>();
                if (traits != null) mul *= traits.workSpeedMul;
                // #177 - Chop skill level (+4%/lvl, lvl 10 = +40%).
                var skills = GetComponent<PawnSkills>();
                if (skills != null) mul *= 1f + skills.GetLevel(SkillKind.Chop) * 0.04f;
                bool destroyed = targetTree.TakeChopDamage(chopDamagePerSec * Time.deltaTime * mul);
                // Day 19: Chop XP — granted each frame proportional to dmg
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
