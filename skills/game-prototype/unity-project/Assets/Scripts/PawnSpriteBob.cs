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

        [Header("Squash & stretch (운영자 2026-06-02 모션)")]
        // 걸을 때 bob 에 동기화된 스쿼시·스트레치(2D 캐릭터 juice).  bob 꼭대기=스트레치
        //  (살짝 키↑·폭↓), 바닥(발딛음)=스쿼시(키↓·폭↑).  walkAmplitude 비례로 fade →
        //  idle 엔 거의 0.  child localScale 레인은 비어 있어 충돌 없음(소유 단독).
        [SerializeField] private float walkSquash = 0.09f;       // 걷기 스쿼시 최대 비율
        // 방향전환 팝: flipX 가 바뀌는 순간 가로로 짧게 눌렀다 펴 "발 딛고 도는" 느낌.
        [SerializeField] private float turnPopAmount = 0.13f;
        [SerializeField] private float turnPopDur = 0.12f;

        // Cached so we never allocate / GetComponent per frame.
        //  Resolved from movementSource (explicit ROOT ref) in Awake — NEVER via
        //  GetComponent on this (child) GameObject.
        private PawnMovement movement;
        // The child's authored local position (everything except our Y offset).
        private Vector3 baseLocalPos;
        // The child's authored local scale (squash/stretch multiplies this).
        private Vector3 baseScale = Vector3.one;
        // Turn-pop state — flipX edge detection + remaining pop time.
        private bool lastFlipX;
        private bool hasLastFlip;
        private float turnPopT;
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
            if (spriteChild != null)
            {
                baseLocalPos = spriteChild.localPosition;
                baseScale = spriteChild.localScale;
            }
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

            // ── Squash & stretch (운영자 2026-06-02) — child localScale 단독 소유 ──
            //  bob 위상에 동기화: 꼭대기(sin=+1)=스트레치(키↑·폭↓), 바닥(sin=-1, 발딛음)=
            //  스쿼시(키↓·폭↑).  walkFactor(걷기 비중)로 fade → idle 엔 거의 0(숨쉬기만).
            float denom = Mathf.Max(0.0001f, walkAmplitude - idleAmplitude);
            float walkFactor = Mathf.Clamp01((liveAmplitude - idleAmplitude) / denom);
            float squash = Mathf.Sin(phase) * walkSquash * walkFactor;
            float sx = 1f - squash * 0.5f;
            float sy = 1f + squash;

            // Turn-pop: childRenderer.flipX (PawnFacing 소유)가 바뀌는 순간 가로 squash.
            if (childRenderer != null)
            {
                bool fx = childRenderer.flipX;
                if (hasLastFlip && fx != lastFlipX) turnPopT = turnPopDur;
                lastFlipX = fx;
                hasLastFlip = true;
            }
            if (turnPopT > 0f)
            {
                turnPopT -= Time.deltaTime;
                float tp = Mathf.Clamp01(turnPopT / Mathf.Max(0.0001f, turnPopDur));  // 1→0
                sx -= turnPopAmount * tp;
                sy += turnPopAmount * 0.5f * tp;
            }
            // baseScale 에 곱 — 작성자는 이 컴포넌트 단독(다른 lane 과 충돌 없음).
            spriteChild.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z);

            // Mirror the ROOT renderer's tint onto the visible child so existing
            //  variant / selection / drafted tint (written to the root by
            //  GameManager + PawnEntity) shows on the bobbing sprite.
            MirrorRootTint();
        }

        // 사망 시 PawnHealth.StopBodyBob 이 이 컴포넌트를 끈다 → Update 가 안 돌아
        //  마지막 squash 상태로 굳지 않도록, 비활성화 시 자식 scale 을 원본으로 복원.
        //  (corpse 회전은 PawnPoseDriver, 위치는 그대로 — scale 만 정리.)
        private void OnDisable()
        {
            if (spriteChild != null) spriteChild.localScale = baseScale;
        }

        private void MirrorRootTint()
        {
            if (childRenderer == null) return;
            // Root renderer is the tint anchor (left present + draw-disabled by
            //  SceneSetup).  Use the EXPLICIT rootTintRenderer ref — GetComponent
            //  here would read THIS child's own renderer (the RED bug), not the
            //  root anchor that GameManager + PawnEntity actually write tint to.
            if (rootTintRenderer == null) return;
            // #54 콜로니스트 외형 다양화: 과거엔 color 만 mirror 해서, GameManager 가 root
            //  tint-anchor 에 배정한 per-pawn 변형 sprite 가 가시 자식엔 안 나타났다(전원 동일).
            //  sprite 도 mirror(참조 비교로 변경 시에만) → 변형 sprite 가 실제로 보인다.
            //  color 미변경 early-return 보다 먼저 실행(아래 return 에 막히지 않게).
            if (rootTintRenderer.sprite != null && childRenderer.sprite != rootTintRenderer.sprite)
                childRenderer.sprite = rootTintRenderer.sprite;

            Color c = rootTintRenderer.color;
            if (hasPushedColor && c == lastPushedColor) return;
            childRenderer.color = c;
            lastPushedColor = c;
            hasPushedColor = true;
        }
    }
}
