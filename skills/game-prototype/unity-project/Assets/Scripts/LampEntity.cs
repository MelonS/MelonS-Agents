using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M4-04 Lane A — wiki Dimension 4 (건축) #19:
    ///   "Add torch/standing-lamp buildable (night light pool)."
    ///   Acceptance: a built lamp emits a light glow at night.
    ///
    /// The placed standing-lamp / torch.  A passive structure entity in the same
    /// family as StoveEntity / WallEntity: a sprite + a 1×1 collider footprint.
    /// It carries NO state of its own beyond "I am a lamp" — the night lighting it
    /// casts is drawn by two self-attached driver layers that both scan for this
    /// marker, so this file stays a thin marker component:
    ///   - LampGlowDriver.cs : warm amber floor pool UNDER the night overlay
    ///                         (sortingOrder 8) — ambience/colour on the ground.
    ///   - LampLight.cs      : soft additive light disc ABOVE the night overlay
    ///                         (sortingOrder 26) — actually lifts the darkness so
    ///                         the map reads as LIT around the lamp at night
    ///                         (operator fb "조명이 실제로 맵을 밝히는 효과 필요").
    /// The two layers together give RimWorld-style "light pushes back the dark".
    ///
    /// Why a marker, not a glow-owner:
    ///   NightLightPoolDriver.cs scans ONLY for StoveEntity and gates on
    ///   stove.MealsAvailable/CanCookOne — it cannot detect a lamp without an
    ///   edit, and the W-M4-04 lane contract forbids editing it.  So dedicated
    ///   LampGlowDriver / LampLight drivers scan for LampEntity instead, reusing
    ///   the identical procedural + day/night-alpha pattern.  This component is
    ///   the thing those drivers scan for; FlameHeightCells / IsLit are the only
    ///   surface they read.
    ///
    /// Lit model (prototype scope):
    ///   A standing lamp / torch is ALWAYS LIT — it has no fuel or power loop in
    ///   this prototype (RimWorld torches burn fuel; standing lamps need power —
    ///   both are out of scope per the over-scoping guardrails).  IsLit returns
    ///   true so the glow shows every night.  A future power/fuel wave can flip
    ///   this to a real state without touching the driver.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class LampEntity : MonoBehaviour
    {
        // World-space height (in cells) of the flame head above the lamp's
        // transform origin.  The sprite is 16×16 = 1 cell; the flame sits in
        // the top ~quarter, so the glow centre is lifted toward it rather than
        // sitting on the foot.  LampGlowDriver reads this to centre the pool.
        public const float FlameHeightCells = 0.30f;

        /// <summary>
        /// A standing lamp / torch is always lit in this prototype (no fuel /
        /// power loop).  Mirrors how NightLightPoolDriver gates a stove via
        /// IsStoveLit, but a lamp's whole purpose is light, so it is always on.
        /// </summary>
        public bool IsLit => true;
    }
}
