using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M6-04 (B5) — autodoor variant of the plain door (wiki row B5: vanilla
    /// RimWorld Door-tab variety — a faster door at a higher Stuff cost).
    ///
    /// An autodoor IS a DoorEntity (so it inherits EVERYTHING: the trigger
    /// pass-through collider, B9's NotifyPassing()→AudioBank.PlayDoor() SFX, the
    /// brighten-on-pass hint, and every read path that does GetComponent
    /// &lt;DoorEntity&gt;() — CellOccupied no-stack, EntityInspectorPanel,
    /// HoverTooltip, DeconstructDesignation, PawnMovement's IsInsideDoor /
    /// PassMulAt).  The ONLY difference is a HIGHER per-instance passMul: the
    /// autodoor slows a crossing pawn LESS, so a pawn crosses it FASTER than a
    /// plain door (B5 binary acceptance: autodoor.PassMul &gt; plainDoor.PassMul).
    ///
    /// passMul is raised in Awake (after base setup) via DoorEntity.SetPassMul,
    /// so an AutodoorEntity dropped into a scene with NO BuildManager (e.g. the
    /// isolated V16 verify test, which AddComponent&lt;AutodoorEntity&gt;()'s it)
    /// still reports the faster value.  BuildManager.EnsureAutodoorPrefab uses
    /// THIS component (not a bare DoorEntity) so the catalogue autodoor and the
    /// test agree on type and value.
    /// </summary>
    public class AutodoorEntity : DoorEntity
    {
        /// <summary>
        /// Autodoor pass-through multiplier — HIGHER than DoorEntity.DefaultPassMul
        /// (0.65) so the crossing pawn is slowed LESS (faster cross).  ≈0.95 ≈
        /// "barely slows".  SerializeField so the designer can tune day-1 feel.
        /// Mirrors BuildManager.autodoorPassMul (kept in sync; both default 0.95).
        /// </summary>
        [SerializeField] private float autodoorPassMul = 0.95f;

        protected override void Awake()
        {
            base.Awake();                 // trigger collider + sprite cache (plain-door setup)
            SetPassMul(autodoorPassMul);  // raise above DefaultPassMul → faster cross
        }
    }
}
