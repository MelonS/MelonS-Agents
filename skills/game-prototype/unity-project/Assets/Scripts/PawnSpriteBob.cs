using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Polish Wave v3 — V1 walk-bob + idle-breathe.
    ///
    /// CRITICAL ROOT-TRANSFORM RULE (this is a RETRY of a desync bug):
    ///   PathGrid.WorldToCell reads the pawn ROOT transform for cells /
    ///   reservations / eject (PawnMovement: RegisterWallCell, WorldToCell,
    ///   the Update standing safety-net), and PawnFloatingBars + PawnNameLabel
    ///   + PawnShadow all anchor to the ROOT.  Therefore this component
    ///   NEVER moves / scales / rotates the root pawn transform.  It only
    ///   writes the LOCAL Y of a SPRITE-CHILD transform.  The child's local
    ///   offset is purely visual and does not feed into world position used by
    ///   pathfinding (which reads transform.position of the ROOT).
    ///
    /// TINT-CONTRACT decision (documented per task):
    ///   GameManager + PawnEntity tint the pawn via GetComponent<SpriteRenderer>()
    ///   on the ROOT (per-pawn variant color / selection-yellow / drafted-cyan).
    ///   SceneSetup.Pawn.cs keeps the ROOT SpriteRenderer exactly as-is so all
    ///   that tint logic still finds it on the root, UNTOUCHED.  The VISIBLE body
    ///   sprite is moved onto a child "PawnSpriteBob" GameObject (so we can bob
    ///   it without touching the root).  The root renderer is left present as the
    ///   tint anchor but disabled from drawing (enabled = false) so there is no
    ///   double-draw; this component MIRRORS the root renderer's color onto the
    ///   child each frame so existing tint writes (which target the root) remain
    ///   visible.  Net effect: callers keep tinting the root, the player sees the
    ///   tint on the bobbing child, and nothing moves the root.
    /// </summary>
    [DisallowMultipleComponent]
    public class PawnSpriteBob : MonoBehaviour
    {
        [Header("Refs (wired by SceneSetup.Pawn)")]
        // The CHILD whose LOCAL position we bob.  Never the root.
        [SerializeField] private Transform spriteChild;
        // The child renderer that actually draws the body (gets the mirrored tint).
        [SerializeField] private SpriteRenderer childRenderer;
        // EXPLICIT ref to the ROOT's PawnMovement (this component lives on the
        //  CHILD, so GetComponent here would find nothing — that was the RED bug:
        //  movement resolved null, IsMoving always false, walk-bob never fired).
        //  Wired by SceneSetup to the root pawnGo's PawnMovement.
        [SerializeField] private PawnMovement movementSource;
        // EXPLICIT ref to the ROOT SpriteRenderer tint-anchor (left present but
        //  draw-disabled by SceneSetup).  GetComponent<SpriteRenderer>() on the
        //  CHILD returns the child's OWN renderer (the other half of the RED bug:
        //  tint never mirrored).  Wired by SceneSetup to the root sr.
        [SerializeField] private SpriteRenderer rootTintRenderer;

        [Header("Walk bob (vertical sine)")]
        // ~1-1.5 px at PPU16 (1px = 1/16 = 0.0625 world unit).
        [SerializeField] private float walkAmplitude = 0.05f;
        // 6-8 Hz footfall cadence.
        [SerializeField] private float walkFrequencyHz = 7f;

        [Header("Idle breathe (slow, tiny)")]
        // Much smaller than walk — a gentle chest rise.
        [SerializeField] private float idleAmplitude = 0.012f;
        // ~1 Hz resting breath.
        [SerializeField] private float idleFrequencyHz = 1f;

        [Header("Easing")]
        // How fast the live amplitude eases toward its walk/idle target on
        //  start/stop, so the offset EASES to zero on stop (no popping).
        [SerializeField] private float amplitudeLerpSpeed = 8f;

        // Cached so we never allocate / GetComponent per frame.
        //  Resolved from movementSource (explicit ROOT ref) in Awake — NEVER via
        //  GetComponent on this (child) GameObject.
        private PawnMovement movement;
        // The child's authored local position (everything except our Y offset).
        private Vector3 baseLocalPos;
        // Phase accumulator — advanced by the ACTIVE frequency so a walk→idle
        //  transition doesn't jump phase (we keep one continuous clock).
        private float phase;
        // Eased amplitude (lerps between idle and walk amplitude targets).
        private float liveAmplitude;
        // Eased frequency target tracker (for smooth cadence change).
        private float liveFrequency;
        // Last color we pushed to the child, to skip redundant writes.
        private Color lastPushedColor;
        private bool hasPushedColor;

        private void Awake()
        {
            // Use the EXPLICIT root ref (wired by SceneSetup) — NOT GetComponent,
            //  because this component sits on the CHILD bob GameObject and the
            //  PawnMovement lives on the ROOT.  Null-guarded everywhere it's read.
            movement = movementSource;
            if (spriteChild != null) baseLocalPos = spriteChild.localPosition;
            liveAmplitude = idleAmplitude;
            liveFrequency = idleFrequencyHz;
        }

        private void Update()
        {
            if (spriteChild == null) return;

            // IsMoving is the existing public read-only getter on PawnMovement
            //  (target.HasValue) — we only READ it, never change movement logic.
            bool moving = movement != null && movement.IsMoving;

            float targetAmp = moving ? walkAmplitude : idleAmplitude;
            float targetFreq = moving ? walkFrequencyHz : idleFrequencyHz;

            // Ease amplitude (and frequency) so STOP fades the bob to the idle
            //  breathe instead of snapping.  Frame-rate independent lerp.
            float t = 1f - Mathf.Exp(-amplitudeLerpSpeed * Time.deltaTime);
            liveAmplitude = Mathf.Lerp(liveAmplitude, targetAmp, t);
            liveFrequency = Mathf.Lerp(liveFrequency, targetFreq, t);

            // Advance phase by the eased frequency (continuous clock).
            phase += liveFrequency * Mathf.PI * 2f * Time.deltaTime;
            // Keep phase bounded to avoid float precision drift over long runs.
            if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;

            float yOffset = Mathf.Sin(phase) * liveAmplitude;

            // ONLY the child's local Y changes.  Root transform untouched →
            //  pathfinding / reservations / eject / bars / shadow all stay put.
            spriteChild.localPosition = new Vector3(
                baseLocalPos.x, baseLocalPos.y + yOffset, baseLocalPos.z);

            // Mirror the ROOT renderer's tint onto the visible child so existing
            //  variant / selection / drafted tint (written to the root by
            //  GameManager + PawnEntity) shows on the bobbing sprite.
            MirrorRootTint();
        }

        private void MirrorRootTint()
        {
            if (childRenderer == null) return;
            // Root renderer is the tint anchor (left present + draw-disabled by
            //  SceneSetup).  Use the EXPLICIT rootTintRenderer ref — GetComponent
            //  here would read THIS child's own renderer (the RED bug), not the
            //  root anchor that GameManager + PawnEntity actually write tint to.
            if (rootTintRenderer == null) return;
            Color c = rootTintRenderer.color;
            if (hasPushedColor && c == lastPushedColor) return;
            childRenderer.color = c;
            lastPushedColor = c;
            hasPushedColor = true;
        }
    }
}
