using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// #202 SURVIVAL-LOOP FIX — idle pawn walks to a RIPE CropEntity and harvests
    /// it (+food to the global stockpile).  This is the missing link in the
    /// farming→food→cook→eat chain: before this, ripe crops could ONLY be
    /// harvested by a manual right-click, so idle pawns never harvested → global
    /// food stayed stuck → no cook ingredients → starvation.
    ///
    /// Same shape as PawnChopper / PawnCook (the established worker pattern):
    ///   - reserve the crop + an adjacent stand cell (ReservationManager / C2)
    ///   - walk to the reserved adjacent walkable cell (RimWorld reach-over)
    ///   - when in range, Harvest() once the crop is ripe
    ///   - path-aware give-up (WorkGiveUp) on real unreachability / stall
    ///   - clear the task once the crop is harvested (no longer ripe) or destroyed
    ///
    /// The crop is NOT destroyed by Harvest() — growth resets to 0 and it regrows,
    /// so harvesting is sustainable (the same tile ripens again ~every cycle).
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnHarvester : MonoBehaviour
    {
        // #199 C1 — stand adjacent to the crop (RimWorld).  1.5 covers diagonal
        //  adjacency (center dist √2≈1.414) yet stays < 2 so a pawn can't harvest
        //  from a full cell away.  Matches PawnChopper.chopRange / PawnCook.cookRange.
        [SerializeField] private float harvestRange = 1.5f;

        private CropEntity targetCrop;
        private PawnMovement movement;
        // #199 B2 (R-1) — path-aware give-up shared helper.
        private WorkGiveUp giveUp;
        private const float GiveUpAfterSec = 10f;
        // #199 C2 — reserved stand cell next to the crop.
        private Vector2Int standCell = PawnMovement.INVALID_CELL;

        public bool HasTask => targetCrop != null;
        public CropEntity Target => targetCrop;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
        }

        public void SetCropTarget(CropEntity crop)
        {
            if (targetCrop != null && targetCrop != crop)
                MelonS.GameProto.AI.ReservationManager.Release(targetCrop, gameObject);
            ReleaseStandCell();
            targetCrop = crop;
            if (crop != null)
            {
                // Reservation is taken by HarvestCropAction (AI) before SetCropTarget;
                //  re-assert here so manual/direct callers also hold it (idempotent).
                MelonS.GameProto.AI.ReservationManager.TryReserve(crop, gameObject);
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, crop.transform.position));
                WalkToWork();
            }
        }

        // #199 C1/C2 — walk to a RESERVED adjacent walkable cell next to the crop.
        private void WalkToWork()
        {
            if (targetCrop == null) return;
            if (PawnMovement.TryReserveWorkStandPos(targetCrop.transform.position,
                    new Vector2Int(1, 1), transform.position, gameObject, ref standCell, out Vector2 stand))
                movement.SetTarget(stand);
            else
                ClearTask();   // no free adjacent stand cell → crop unreachable/occupied
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
            if (targetCrop != null)
                MelonS.GameProto.AI.ReservationManager.Release(targetCrop, gameObject);
            ReleaseStandCell();
            targetCrop = null;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (targetCrop == null) return;
            // Crop destroyed (e.g. wall built over it) — abandon.
            if (targetCrop.gameObject == null)
            {
                ClearTask();
                return;
            }
            // No longer ripe — either we just harvested it, or someone else did.
            //  Releasing lets the AI pick the next ripe crop next decision tick.
            if (!targetCrop.IsRipe)
            {
                ClearTask();
                return;
            }

            float dist = Vector2.Distance(transform.position, targetCrop.transform.position);
            // #199 B2 (R-1) — give up only on real unreachability/stall, not detour.
            if (dist > harvestRange && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, GiveUpAfterSec))
            {
                Debug.Log($"[Harvester] {name} give up crop (unreachable/stalled, dist={dist:F2}, pathFailed={movement.LastPathFailed})");
                ClearTask();
                return;
            }
            if (dist <= harvestRange || movement.AtStandCell(standCell))
            {
                movement.ClearTarget();
                // Harvest is instantaneous (one ripe crop → +food).  Harvest()
                //  resets growth to 0 (crop regrows) and plays the throttled
                //  PlayHarvest SFX internally (AudioBank per-key throttle — no
                //  audio-buzz: lesson #4).  ClearTask next frame via !IsRipe.
                int gained = targetCrop.Harvest();
                if (gained > 0)
                {
                    // Day-19 style XP: harvesting is gather work → Chop skill slot
                    //  (the prototype's generic labor skill; no Plants skill yet).
                    var skills = GetComponent<PawnSkills>();
                    if (skills != null) skills.AddXP(SkillKind.Chop, 6f);
                }
                ClearTask();   // crop now growth=0 → not ripe; release immediately.
            }
            else
            {
                WalkToWork();
            }
        }
    }
}
