using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb (2026-05-31): "처음엔 맨주먹으로 싸우는데 싸우는 모션이 아예 없음."
    ///
    /// ROOT CAUSE (why the B10 PawnPoseDriver lunge never showed):
    ///   1. PawnSpriteBob.Update writes the body child's localPosition EVERY frame
    ///      as `new Vector3(baseLocalPos.x, baseY + bob, baseZ)` — it hard-resets X
    ///      to baseLocalPos.x.  PawnPoseDriver's lunge also writes localPosition.x in
    ///      Update.  Two Update writers + a full-vector reset → the lunge X is
    ///      clobbered the same frame.  Net visible motion: none.
    ///   2. PawnPoseDriver's lunge is PROXIMITY-gated (`attacking && !lungeActive`).
    ///      `attacking` stays continuously true while a bandit sits in range, so the
    ///      lunge fires AT MOST ONCE then never re-triggers per swing.  It also only
    ///      watches BanditEnemy (no wolf / animal) and DraftedAttackTarget (bandit).
    ///
    /// FIX (this driver, no edit to PawnSpriteBob / PawnPoseDriver — not our lane):
    ///   • Write the lunge X in LateUpdate.  LateUpdate runs strictly AFTER every
    ///     pose driver's Update (all four use Update), so we are the authoritative
    ///     LAST writer of bodyChild.localPosition.x for the frame.  We READ the
    ///     post-bob localPosition and add ONLY our X offset, preserving the bob's Y
    ///     and the Z — never fighting PawnSpriteBob's Y or PawnPoseDriver's corpse
    ///     rotation.  Root transform is NEVER touched (PathGrid.WorldToCell / bars /
    ///     name / shadow anchor to root — identical firewall to PawnFacing).
    ///   • The lunge is HIT-driven, not proximity-driven.  PawnEntity.RegisterMeleeHit
    ///     fires on each ACTUAL attack tick (auto-attack on bandits) and stamps
    ///     LastMeleeHitTime + a unit direction.  We edge-detect a new hit by polling
    ///     LastMeleeHitTime (poll, NOT event-subscribe — bug-pattern #7) so each swing
    ///     re-fires a fresh lunge.  Throttled internally so a tight loop can never
    ///     spam (bug-pattern #4 spirit: per-swing, not per-frame).
    ///   • A drafted-melee fallback: when PawnEntity reports a live drafted target in
    ///     melee range (bandit / wolf / animal — DraftedAttackTarget public), we
    ///     edge-fire on our own cadence so wolf/animal swings animate too without
    ///     editing PawnUtilityAI (out of lane).  PawnEntity-stamped hits win when
    ///     present (frame-accurate); the fallback only covers paths PawnEntity does
    ///     not stamp.
    ///
    /// HIT-FLASH:
    ///   A brief WHITE-ish brightness pulse on the BODY CHILD renderer at the lunge
    ///   peak.  PawnSpriteBob.MirrorRootTint also writes the child color, but only
    ///   when the ROOT color CHANGES (it early-outs on equal color).  We therefore
    ///   drive the flash in LateUpdate (after the mirror) and, when the flash ends,
    ///   restore the child color to the current ROOT color so the mirror's cached
    ///   "lastPushedColor" stays consistent and re-asserts on the next root change.
    ///   The flash is short (flashSec) and never overlaps the dead/downed tint path
    ///   (we early-out on Dead/Downed, leaving the corpse tint to PawnEntity /
    ///   PawnPoseDriver).
    ///
    /// RANGED (bow): handled by ArrowProjectile (PawnUtilityAI spawns the arrow) — a
    ///   ranged attack shows the arrow, NOT a lunge.  This driver suppresses the lunge
    ///   while a ranged shot is the active attack (target out of melee range but within
    ///   ranged range), so an archer does not jab the air.  Melee (incl. bare fists) =
    ///   lunge; ranged = arrow.
    ///
    /// Self-attach: [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + PawnMovement
    ///   sweep behind GameSceneGate.RunWhenGameScene (no MainMenu leak), identical to
    ///   PawnFacing / PawnPoseDriver / FootstepDriver.  [DisallowMultipleComponent] +
    ///   idempotency guard.  Throttle is Time-based inside LateUpdate (NOT a
    ///   WaitForSeconds coroutine) so it cannot freeze QA when the window loses focus
    ///   (bug-pattern #9 — no always-on background timer introduced).
    ///
    /// Child-property ownership (extends the B10 table; this driver adds one):
    ///   localPosition.Y  → PawnSpriteBob       (we read, preserve, never set)
    ///   localPosition.X  → CombatLungeDriver   (LateUpdate, authoritative last writer)
    ///   localRotation    → PawnPoseDriver      (corpse roll; we early-out on dead/downed)
    ///   flipX            → PawnFacing          (we READ to mirror lunge direction)
    ///   color            → PawnSpriteBob mirror (+ brief flash here, restored after)
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatLungeDriver : MonoBehaviour
    {
        [Header("Lunge")]
        // Peak forward offset on the body child's local X, world units.  Operator
        //  asked for "~0.2 sprite 전진".  Body sprite ~ 1 world unit, so 0.2 = ~20%
        //  of the sprite forward — a clearly readable jab without flinging the pawn.
        [SerializeField] private float lungeDistance = 0.2f;
        // Seconds to ease the lunge from peak back to zero.  Short = snappy jab.
        [SerializeField] private float lungeReturnSec = 0.16f;
        // Minimum seconds between lunges (re-trigger throttle).  Slightly under the
        //  fastest melee cadence (PawnEntity.attackInterval ~1.0s, drafted 0.8s) so a
        //  fast swing always animates, but a per-frame spam can never fire (#4 spirit).
        [SerializeField] private float lungeMinInterval = 0.18f;

        [Header("Hit-flash")]
        // Brightness multiplier at the flash peak (>1 = brighten the body sprite).
        [SerializeField] private float flashBrightness = 1.8f;
        // Flash duration (seconds).  Shorter than the lunge so it reads as an impact.
        [SerializeField] private float flashSec = 0.08f;

        [Header("Work swing")]
        // 운영자 2026-06-02 "일하는 모션": 비전투 작업(벌목/채굴/건축/수확/채집/요리/치료/
        //  사냥) 중 정지 상태에서 작업 대상 방향으로 주기적 jab.  전투 lunge 와 같은 X-offset
        //  메커니즘을 LateUpdate(여기, 권위 작성자)에서 재사용 — PawnPoseDriver(Update)는
        //  PawnSpriteBob 에 덮여 안 보였다.  flash 는 작업엔 안 켠다(피격처럼 보임 방지).
        [SerializeField] private float workSwingInterval = 0.5f;
        // 작업 jab 은 전투보다 살짝 작게(망치질 톤).  lungeDistance * 이 비율.
        [SerializeField] private float workSwingScale = 0.7f;
        private float lastWorkSwing = -999f;

        [Header("Drafted fallback")]
        // Melee range for the drafted-target fallback (matches PawnUtilityAI const 1.2).
        [SerializeField] private float draftedMeleeRange = 1.2f;
        // Ranged range above which a drafted bow shot (not a lunge) is assumed, so an
        //  archer at range does not jab.  Matches PawnUtilityAI RangedAttackRange.
        [SerializeField] private float rangedRange = 5.0f;
        // Cadence for the drafted fallback edge-fire (matches DraftAttackInterval 0.8).
        [SerializeField] private float draftedFallbackInterval = 0.8f;

        private const string BodyChildName = "PawnSpriteBob";

        private Transform bodyChild;
        private SpriteRenderer bodySr;
        private SpriteRenderer rootSr;
        private PawnEntity pawnEntity;
        private PawnHealth health;
        private PawnUtilityAI ai;           // 작업 대상 좌표(작업 스윙 방향)
        private PawnMovement movement;      // 정지 상태 게이트(이동 중엔 작업 스윙 안 함)

        // Lunge state.
        private float lungeTimer;          // counts down from lungeReturnSec
        private bool lungeActive;
        private int lungeSign = 1;         // +1 = lunge right, -1 = lunge left
        private float lastLungeStart = -999f;
        private float curLungeDistance;    // 이번 lunge 의 peak offset(전투=full, 작업=scaled)

        // Hit-edge detection (poll PawnEntity.LastMeleeHitTime — bug #7 safe).
        private float lastSeenHitTime = -999f;

        // Drafted fallback edge timer.
        private float lastFallbackFire = -999f;

        // Flash state.
        private float flashTimer;          // counts down from flashSec
        private bool flashActive;

        private void Awake()
        {
            pawnEntity = GetComponent<PawnEntity>();
            health     = GetComponent<PawnHealth>();
            ai         = GetComponent<PawnUtilityAI>();
            movement   = GetComponent<PawnMovement>();
            rootSr     = GetComponent<SpriteRenderer>();
            bodyChild  = ResolveBodyChild();
            if (bodyChild != null) bodySr = bodyChild.GetComponent<SpriteRenderer>();
        }

        private Transform ResolveBodyChild()
        {
            Transform body = transform.Find(BodyChildName);
            if (body != null) return body;
            // Fallback: first non-root child with a SpriteRenderer that is not the
            //  shadow / bundle overlay.
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform c = transform.GetChild(i);
                if (c == null) continue;
                if (c.name == "PawnShadow" || c.name == "PawnCarryBundle") continue;
                var sr = c.GetComponent<SpriteRenderer>();
                if (sr == null || sr == rootSr) continue;
                return c;
            }
            return null;
        }

        // LateUpdate so we are the LAST writer of bodyChild.localPosition.x this frame
        //  (after PawnSpriteBob's Update bob write and PawnPoseDriver's Update writes).
        private void LateUpdate()
        {
            if (bodyChild == null) return;

            // Dead / downed: leave the corpse pose (rotation + tint) entirely to
            //  PawnPoseDriver / PawnEntity.  Reset our lunge so a revived pawn starts
            //  clean, and never write color (would fight the corpse tint).
            if (health != null)
            {
                var st = health.State;
                if (st == PawnHealth.PoseState.Dead || st == PawnHealth.PoseState.Downed)
                {
                    if (lungeActive) { lungeActive = false; lungeTimer = 0f; }
                    if (flashActive) { flashActive = false; flashTimer = 0f; RestoreBodyColor(); }
                    return;
                }
            }

            DetectAndTriggerLunge();
            ApplyLunge();
            UpdateFlash();
        }

        private void DetectAndTriggerLunge()
        {
            bool fire = false;
            Vector2 dir = Vector2.right;

            // Path 1 (authoritative): PawnEntity stamped an actual melee hit.  Edge-
            //  detect by comparing the stamped time to the last one we saw.  This is
            //  the auto-attack path (bare fists on a bandit) and is frame-accurate.
            if (pawnEntity != null && pawnEntity.LastMeleeHitTime > lastSeenHitTime + 1e-4f)
            {
                lastSeenHitTime = pawnEntity.LastMeleeHitTime;
                // Only treat as a melee swing (not a ranged shot).
                if (pawnEntity.LastAttackWasMelee)
                {
                    fire = true;
                    dir = pawnEntity.LastMeleeHitDir;
                }
            }

            // Path 2 (fallback): drafted melee on a live target in melee range, on our
            //  own cadence — covers wolf/animal/ drafted-bandit paths that PawnUtilityAI
            //  does not stamp.  Suppressed when the target is at RANGED distance (an
            //  archer shoots an arrow there, no lunge).
            if (!fire && pawnEntity != null && pawnEntity.IsDrafted)
            {
                Vector2 me = transform.position;
                if (TryGetDraftedTargetPos(out Vector2 tpos))
                {
                    float d = Vector2.Distance(me, tpos);
                    if (d <= draftedMeleeRange &&
                        Time.time - lastFallbackFire >= draftedFallbackInterval)
                    {
                        lastFallbackFire = Time.time;
                        fire = true;
                        dir = (tpos - me);
                    }
                }
            }

            // Path 3 (work swing): 비전투 작업 중 정지 + 작업 대상 있음 → 작업 cadence 로
            //  대상 방향 jab.  전투(path1/2)가 잡혔으면 건너뜀.  flash 는 안 켠다.
            bool workSwing = false;
            if (!fire && pawnEntity != null && !pawnEntity.IsDrafted
                && (movement == null || !movement.IsMoving)
                && ai != null && ai.TryGetWorkTargetPos(out Vector3 wt))
            {
                if (Time.time - lastWorkSwing >= workSwingInterval)
                {
                    lastWorkSwing = Time.time;
                    fire = true;
                    workSwing = true;
                    dir = (Vector2)wt - (Vector2)transform.position;
                }
            }

            if (!fire) return;
            if (Time.time - lastLungeStart < lungeMinInterval) return;  // throttle

            lastLungeStart = Time.time;
            lungeActive = true;
            lungeTimer = lungeReturnSec;
            // 작업 스윙은 전투보다 짧은 jab; 전투는 full lungeDistance.
            curLungeDistance = workSwing ? lungeDistance * workSwingScale : lungeDistance;
            // Lunge sign from the world direction toward the target.  If the target is
            //  directly above/below (dir.x ~ 0), mirror the facing (flipX) instead so
            //  the jab still reads.
            if (Mathf.Abs(dir.x) > 0.01f)
                lungeSign = dir.x >= 0f ? 1 : -1;
            else
                lungeSign = (bodySr != null && bodySr.flipX) ? -1 : 1;

            // Kick off the impact flash too (peaks with the lunge) — 전투 타격만.
            if (!workSwing)
            {
                flashActive = true;
                flashTimer = flashSec;
            }
        }

        private bool TryGetDraftedTargetPos(out Vector2 pos)
        {
            pos = Vector2.zero;
            if (pawnEntity == null) return false;
            var bandit = pawnEntity.DraftedAttackTarget;
            if (bandit != null && !bandit.IsDead) { pos = bandit.transform.position; return true; }
            var wolf = pawnEntity.DraftedWolfTarget;
            if (wolf != null && !wolf.IsDead) { pos = wolf.transform.position; return true; }
            var animal = pawnEntity.DraftedHuntTarget;
            if (animal != null && !animal.IsDead) { pos = animal.transform.position; return true; }
            return false;
        }

        private void ApplyLunge()
        {
            float offsetX = 0f;
            if (lungeActive)
            {
                lungeTimer -= Time.deltaTime;
                // Ease: quick out to peak handled implicitly (we start AT peak and
                //  ease back), so a single Lerp from peak→0 over the window.
                float t = Mathf.Clamp01(1f - (lungeTimer / lungeReturnSec));
                offsetX = Mathf.Lerp(curLungeDistance, 0f, t) * lungeSign;
                if (lungeTimer <= 0f) { lungeActive = false; offsetX = 0f; }
            }

            // Read the post-bob localPosition (PawnSpriteBob set Y this frame in
            //  Update) and write ONLY X.  Y and Z are preserved untouched.
            Vector3 local = bodyChild.localPosition;
            if (!Mathf.Approximately(local.x, offsetX))
                bodyChild.localPosition = new Vector3(offsetX, local.y, local.z);
        }

        private void UpdateFlash()
        {
            if (bodySr == null) return;
            if (!flashActive) return;

            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
            {
                flashActive = false;
                RestoreBodyColor();
                return;
            }
            // Pulse: brighten toward the peak at the start, fade as the timer drains.
            float t = Mathf.Clamp01(flashTimer / flashSec);            // 1 → 0
            float bright = Mathf.Lerp(1f, flashBrightness, t);          // peak→1
            // Multiply the CURRENT root color (preserves variant/selection hue) — never
            //  a flat white overwrite (TintHelper lesson #167).
            Color baseC = rootSr != null ? rootSr.color : Color.white;
            bodySr.color = new Color(baseC.r * bright, baseC.g * bright, baseC.b * bright, baseC.a);
        }

        // Restore the body child to the current root color so PawnSpriteBob's mirror
        //  (which early-outs on equal color) re-asserts cleanly on the next root change.
        private void RestoreBodyColor()
        {
            if (bodySr == null) return;
            Color baseC = rootSr != null ? rootSr.color : Color.white;
            if (bodySr.color != baseC) bodySr.color = baseC;
        }
    }

    /// <summary>
    /// Self-attach bootstrap + runtime sweep for CombatLungeDriver.  Copies the
    /// PawnPoseDriver / FootstepDriver pattern: hidden DontDestroyOnLoad host GO behind
    /// GameSceneGate.RunWhenGameScene (no MainMenu leak), FindFirstObjectByType
    /// idempotency guard, periodic PawnMovement sweep.  Throttle is Time-based inside
    /// Update (no WaitForSeconds) → cannot freeze QA on focus loss (bug-pattern #9).
    /// </summary>
    internal static class CombatLungeDriverBootstrap
    {
        private const float ScanInterval = 1.0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindSweepDriver() != null) return;
                var go = new GameObject("~CombatLungeDriver");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                go.AddComponent<CombatLungeSweepDriver>();
            });
        }

        private static CombatLungeSweepDriver FindSweepDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<CombatLungeSweepDriver>();
#else
            return Object.FindObjectOfType<CombatLungeSweepDriver>();
#endif
        }

        internal static void EnsureOn(PawnMovement pm)
        {
            if (pm == null) return;
            if (pm.GetComponent<CombatLungeDriver>() == null)
                pm.gameObject.AddComponent<CombatLungeDriver>();
        }

        private sealed class CombatLungeSweepDriver : MonoBehaviour
        {
            private float nextScan;
            private static readonly List<PawnMovement> scratch = new List<PawnMovement>(32);

            private void Update()
            {
                if (Time.unscaledTime < nextScan) return;
                nextScan = Time.unscaledTime + ScanInterval;
                scratch.Clear();
#if UNITY_2023_1_OR_NEWER
                var found = Object.FindObjectsByType<PawnMovement>(FindObjectsSortMode.None);
#else
                var found = Object.FindObjectsOfType<PawnMovement>();
#endif
                for (int i = 0; i < found.Length; i++)
                    EnsureOn(found[i]);
            }
        }
    }
}
