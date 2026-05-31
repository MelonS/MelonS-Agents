using System.Collections.Generic;
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
        // #199 C1 — pawn now stands in an ADJACENT cell (RimWorld-style) instead
        //  of on top of the tree.  Standing in a diagonal neighbour leaves the pawn
        //  at center-to-center distance up to √2≈1.414 from the tree, so chopRange
        //  must accept that: bumped 1.2 → 1.5 (covers diagonal adjacency, still <2
        //  so the pawn can't work from a cell away).
        [SerializeField] private float chopRange = 1.5f;
        [SerializeField] private float chopDamagePerSec = 25f;

        private TreeEntity targetTree;
        private PawnMovement movement;
        // #236 도달 불가 나무를 포기한 직후 같은 걸 또 배정받아 무한 포기 루프(PawnMiner #221
        //  과 동일 패턴).  TreeChopDesignation.DispatchToIdleChoppers 가 이 cooldown 을
        //  소비해 한시적으로 그 나무를 건너뛴다.  영구 블랙리스트 대신 막힘 해소 가능성 위해 한시적.
        private readonly Dictionary<TreeEntity, float> _giveUpUntil
            = new Dictionary<TreeEntity, float>();
        private const float GiveUpCooldownSec = 60f;
        public bool IsRecentlyGivenUp(TreeEntity t)
            => t != null && _giveUpUntil.TryGetValue(t, out float u) && Time.time < u;
        // #199 B2 (R-1) — give-up now keys on real path-unreachability
        //  (PawnMovement.LastPathFailed) + a no-progress stall, NOT raw
        //  dist>range (which false-trips while the pawn legitimately detours
        //  around obstacles under A*).  See WorkGiveUp.
        private WorkGiveUp giveUp;
        private const float GiveUpAfterSec = 10f;
        // #199 C2 — the stand cell this chopper has RESERVED next to the tree.
        //  Locked once on commit and reused every frame (fixes C1's per-frame
        //  recompute) and stops a second pawn pathing to the same adjacent cell.
        //  Released on ClearTask.
        private Vector2Int standCell = PawnMovement.INVALID_CELL;

        public bool HasTask => targetTree != null;
        public TreeEntity Target => targetTree;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetTreeTarget(TreeEntity tree)
        {
            // #199 C2 — switching target: release the previous tree + stand cell so
            //  they free up for other pawns (RimWorld job-switch release).
            if (targetTree != null && targetTree != tree)
                MelonS.GameProto.AI.ReservationManager.Release(targetTree, gameObject);
            ReleaseStandCell();
            targetTree = tree;
            if (tree != null)
            {
                // The reservation is taken by ChopTreeAction (AI) before SetTreeTarget;
                //  re-assert here so manual/direct callers also hold it (idempotent).
                MelonS.GameProto.AI.ReservationManager.TryReserve(tree, gameObject);
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, tree.transform.position));
                WalkToWork();
            }
        }

        // #199 C1/C2 — walk to a RESERVED adjacent walkable cell next to the tree
        //  (RimWorld reach-over), not onto the tree.  The cell is reserved once and
        //  reused; if none is reachable/free the tree is unreachable → give up.
        private void WalkToWork()
        {
            if (targetTree == null) return;
            if (PawnMovement.TryReserveWorkStandPos(targetTree.transform.position,
                    new Vector2Int(1, 1), transform.position, gameObject, ref standCell, out Vector2 stand))
                movement.SetTarget(stand);
            else
            {
                if (targetTree != null) _giveUpUntil[targetTree] = Time.time + GiveUpCooldownSec;  // #236
                Debug.Log($"[Chopper] {name} give up tree (no free adjacent stand cell — unreachable/occupied)");
                ClearTask();
            }
        }

        private void ReleaseStandCell()
        {
            if (standCell.x != PawnMovement.INVALID_CELL.x)
            {
                MelonS.GameProto.AI.ReservationManager.ReleaseCell(standCell, gameObject);
                standCell = PawnMovement.INVALID_CELL;
            }
        }

        public void ClearTask()
        {
            // #199 C2 — release the tree + stand cell so other pawns can claim them.
            if (targetTree != null)
                MelonS.GameProto.AI.ReservationManager.Release(targetTree, gameObject);
            ReleaseStandCell();
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
                if (targetTree != null) _giveUpUntil[targetTree] = Time.time + GiveUpCooldownSec;  // #236
                Debug.Log($"[Chopper] {name} give up tree (unreachable/stalled, dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                ClearTask();
                return;
            }
            // #199 C2 — in range if within chopRange OR standing in the reserved
            //  stand cell (the lock can leave the pawn an arriveDistance short of the
            //  cell centre, exactly on the chopRange boundary; standing in the cell
            //  means it's in work position, RimWorld-style).
            if (dist <= chopRange || movement.AtStandCell(standCell))
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
                // Keep walking toward the adjacent stand cell (re-target every
                //  frame; SetTarget caches the A* path so this is not a per-frame
                //  re-path — R-4).
                WalkToWork();
            }
        }
    }
}
