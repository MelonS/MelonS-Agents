using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Wave W-M6-02 / Lane B10 — pawn carry-pose + attack-lunge (wiki B10).
    ///
    /// Wiki acceptance criterion (genre-comparison-v2.md B10):
    ///   "A hauling pawn visibly carries something (a carried-item sprite appears)
    ///    AND a melee/attack shows a brief forward jab offset; no pawn drifts off
    ///    its cell over 10s of walking (root untouched — same firewall as PawnFacing)."
    ///
    /// CRITICAL ROOT-TRANSFORM FIREWALL (IDENTICAL to PawnFacing / PawnSpriteBob):
    ///   PathGrid.WorldToCell reads the pawn ROOT transform for cell reservations /
    ///   eject / standing-safety.  PawnFloatingBars, PawnNameLabel, PawnShadow all
    ///   anchor to the ROOT.  Therefore this component NEVER moves / rotates /
    ///   scales the ROOT transform.  It writes ONLY:
    ///     (a) a CHILD SpriteRenderer GameObject's enabled state + sprite
    ///         (the "carry bundle" overlay, a second child sprite ABOVE the body),
    ///     (b) the BODY CHILD transform's LOCAL position X offset for the lunge.
    ///   The body child's local Y belongs to PawnSpriteBob (walk-bob). We ADD a
    ///   lungeOffsetX to whatever localPosition the body child currently has — we
    ///   do NOT set the full localPosition, so we never fight PawnSpriteBob's Y.
    ///   flipX belongs to PawnFacing — we never touch it.  .color belongs to
    ///   SleepPoseDriver — we never touch it.
    ///
    /// CARRY-BUNDLE CHILD SPRITE:
    ///   We spawn a new child GameObject named "PawnCarryBundle" on the pawn root
    ///   the first time EnsureOn is called.  It gets its own SpriteRenderer loaded
    ///   with carry_bundle.png via Resources.Load (placed in a Resources/ folder
    ///   by the QA/Build-Engineer SceneSetup pass; FLAG below if wiring is absent).
    ///   The child is positioned at localPosition (0, 0.55, -0.1) — half a cell
    ///   above the pawn center, slightly in front (negative Z) so it draws over the
    ///   body.  It is toggled enabled/disabled to show or hide the bundle, keeping
    ///   the overhead minimal.  We never touch the root renderer.
    ///
    ///   FLAG QA / Build-Engineer:
    ///     carry_bundle.png MUST be placed at:
    ///       Assets/Resources/Sprites/carry_bundle.png
    ///     (or any Resources/ subfolder) for Resources.Load to work at runtime.
    ///     The sprite is currently at Assets/Sprites/carry_bundle.png (generator
    ///     output).  QA: copy or move to a Resources/ folder and confirm
    ///     Resources.Load("Sprites/carry_bundle") returns non-null.
    ///     If Resources.Load returns null, the carry-bundle child renderer stays
    ///     disabled (graceful no-op); the lunge still functions.
    ///
    /// HAULER CARRY STATE:
    ///   PawnHauler.HasTask is public but returns true even before the pawn picks
    ///   up the item (GoToItem phase).  There is NO public IsCarrying accessor
    ///   that distinguishes "walking toward item" from "holding item in transit".
    ///   FLAG QA / Programmer: add a public bool IsCarrying { get; } to PawnHauler
    ///   that returns (carryingWood > 0 || carryingStone > 0 || carryingFood > 0).
    ///   Until that accessor exists, this driver uses HasTask as a CONSERVATIVE
    ///   approximation (bundle shows whenever a haul job is assigned, which includes
    ///   the walk-to-item phase).  This is acceptable for the B10 visual effect;
    ///   it never shows the bundle on a pawn with no haul job.
    ///
    /// MELEE/ATTACK STATE:
    ///   PawnEntity.IsDrafted is public.  PawnEntity.DraftedAttackTarget is public.
    ///   There is NO public IsAttacking or IsInMeleeSwing accessor on PawnEntity
    ///   (the attack timer nextAttackTime is private).
    ///   FLAG QA / Programmer: add a public bool IsAttacking { get; } to PawnEntity
    ///   that returns (Time.time < nextAttackTime + attackInterval * 0.5f) so the
    ///   lunge window matches the swing, or expose a public float LastAttackTime.
    ///   Until that accessor exists, this driver detects attack intent from:
    ///     IsDrafted == true AND (DraftedAttackTarget != null AND !DraftedAttackTarget.IsDead)
    ///   OR there is a living BanditEnemy within AttackRange of the pawn.
    ///   This is a CONSERVATIVE approximation: the lunge fires whenever a drafted
    ///   pawn has a live target in range (which closely matches "is actively attacking").
    ///   Auto-attack (undrafted) is detected by the presence of any live BanditEnemy
    ///   within AttackRange — same check PawnEntity.Update uses, polled every
    ///   AttackCheckInterval seconds to avoid per-frame FindObjectsOfType.
    ///
    /// Self-attach pattern:
    ///   [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] bootstrap (identical to
    ///   PawnFacing / SleepPoseDriver).  A hidden DontDestroyOnLoad host GO sweeps
    ///   for PawnMovement instances lacking a PawnPoseDriver and attaches one.
    ///   [DisallowMultipleComponent] + idempotency guard make the sweep safe to run
    ///   on every scan interval.
    ///
    /// Ownership table (no two systems fight over the same field):
    ///   PawnSpriteBob   → child transform localPosition.Y
    ///   PawnFacing      → child SpriteRenderer.flipX
    ///   SleepPoseDriver → child SpriteRenderer.color
    ///   PawnPoseDriver  → child transform localPosition.X (lunge offset, eased to 0)
    ///                   + new "PawnCarryBundle" child GameObject enabled state
    ///   (Root transform: NEVER touched by any of these four.)
    /// </summary>
    [DisallowMultipleComponent]
    public class PawnPoseDriver : MonoBehaviour
    {
        // ─────────────── Lunge parameters ───────────────────────────────────
        [Header("Attack lunge")]
        // Peak forward offset on the body child's local X when a melee hit fires.
        // 0.06–0.10 world units = 1–1.5 px at PPU16 — noticeable but not jarring.
        [SerializeField] private float lungeDistance = 0.08f;
        // Time (seconds) for the lunge to ease back to zero after peaking.
        [SerializeField] private float lungeReturnSec = 0.15f;
        // How often (seconds) we poll for BanditEnemy presence (auto-attack check).
        // Mirrors PawnEntity's own BanditSearchInterval to avoid redundant scans.
        [SerializeField] private float attackCheckInterval = 0.25f;

        // ─────────────── Carry bundle position ──────────────────────────────
        [Header("Carry bundle")]
        // Local position of the carry-bundle child relative to the pawn root.
        // Y=0.55 puts it just above the pawn head; Z=-0.1 draws in front.
        [SerializeField] private Vector3 bundleLocalPos = new Vector3(0f, 0.55f, -0.1f);

        // ─────────────── Death / downed corpse pose (운영자 fb 2026-05-31) ────
        // 운영자: "죽었을 때 이미지 변화 없음 + 상태별 처리 필요".
        //  Dead   → body 자식을 90° 눕혀 corpse 처럼 + 머리위 바/이름 숨김 + walk-bob
        //            정지(정지는 PawnHealth.StopBodyBob 이 state-source 에서 게이트).
        //  Downed → 의식불명: 살짝 기운 누움(부분 회전)으로 Dead 와 구분, 살아있음.
        //  회전은 자식 transform.localRotation 에만 적용(root 는 절대 안 건드림 —
        //   PathGrid.WorldToCell / 바 / 이름 / shadow 가 root 를 anchor).  Dead 림은
        //   movement 가 이미 disable 됐지만, root 회전은 바/이름 anchor 까지 기울이므로
        //   여전히 금지.  Tint(회색조/desaturate)는 PawnEntity.ApplyVisual 이 root 색에
        //   쓰고 PawnSpriteBob 가 자식으로 mirror 한다(이 driver 는 색을 안 건드림).
        [Header("Corpse pose")]
        // 사망 시 body 자식을 눕히는 각도(Z).  90° = 완전히 옆으로 쓰러짐.
        [SerializeField] private float deadRollDeg = 90f;
        // 의식불명(downed) 시 기울임 각도.  Dead 보다 작게 → 시각적으로 구분.
        [SerializeField] private float downedRollDeg = 55f;
        // 회전이 목표각으로 ease 되는 속도(deg/sec 비례).  쓰러짐이 톡 튀지 않게.
        [SerializeField] private float rollEaseSpeed = 8f;

        // ─────────────── Runtime state ───────────────────────────────────────
        // Names from SceneSetup.Pawn (shared with PawnFacing / SleepPoseDriver).
        private const string BodyChildName   = "PawnSpriteBob";
        private const string ShadowChildName = "PawnShadow";
        private const string BundleChildName = "PawnCarryBundle";
        // Resource path for the carry bundle sprite (must be in a Resources/ folder).
        private const string BundleSpritePath = "Sprites/carry_bundle";

        // The body CHILD transform whose localPosition.X we offset for the lunge.
        private Transform bodyChild;
        private SpriteRenderer cachedChildSr;   // #audit4 #3 — bodyChild 의 SpriteRenderer 캐시
        // Carry-bundle child SpriteRenderer (created once in Awake).
        private SpriteRenderer bundleRenderer;

        // Components on the pawn ROOT (GetComponent from THIS root-attached driver).
        private PawnHauler hauler;
        private PawnEntity pawnEntity;
        private PawnHealth health;            // 상태 source (poll, 구독 X — bug #7)
        // 사망 시 숨길 머리위 바(HpBg/HpFill/MoodBg/MoodFill) 와 이름/상태 라벨은
        //  자식 GameObject 이름으로 직접 토글(아래 SetBarsAndLabelVisible).
        // 이름/상태 라벨 자식 렌더러(PawnNameLabel 가 만든 child GO).
        private const string NameLabelChild   = "NameLabel";
        private const string StatusLabelChild = "StatusLabel";
        private const string NamePlateChild   = "NamePlate";

        // Corpse-pose runtime state.
        private float currentRoll;               // eased Z roll currently applied
        private bool barsHidden;                 // 바/이름 숨김 1회만 적용

        // Lunge state — current eased X offset applied on top of bodyChild.localPosition.
        private float currentLungeX;
        // Whether a lunge was triggered this attack window.
        private bool lungeActive;
        // Timer counting down from lungeReturnSec to 0 after the lunge peak.
        private float lungeTimer;

        // Attack polling.
        private float nextAttackCheck;
        private bool cachedInRange; // is there a living BanditEnemy within AttackRange?

        private void Awake()
        {
            hauler     = GetComponent<PawnHauler>();
            pawnEntity = GetComponent<PawnEntity>();
            health     = GetComponent<PawnHealth>();
            bodyChild  = ResolveBodyChild();
            // #audit4 #3 — 자식 SpriteRenderer 캐시.  UpdateAttackLunge 가 매 프레임
            //  bodyChild.GetComponent<SpriteRenderer>() 를 호출하던 것 1회 캐시로 대체.
            cachedChildSr = bodyChild != null ? bodyChild.GetComponent<SpriteRenderer>() : null;
            bundleRenderer = EnsureBundleChild();
        }

        /// <summary>
        /// Locate the body child transform (the same "PawnSpriteBob" child that
        /// PawnFacing and SleepPoseDriver use).  Null-guarded; returns null if
        /// the named child is absent (graceful no-op for the lunge path).
        /// </summary>
        private Transform ResolveBodyChild()
        {
            Transform body = transform.Find(BodyChildName);
            if (body != null) return body;

            // Fallback: first child that is not the shadow and has a SpriteRenderer.
            SpriteRenderer rootSr = GetComponent<SpriteRenderer>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;
                if (child.name == ShadowChildName) continue;
                if (child.name == BundleChildName) continue;
                var sr = child.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                if (sr == rootSr) continue;
                return child;
            }

            return null;
        }

        /// <summary>
        /// Find or create the "PawnCarryBundle" child SpriteRenderer.
        /// We create it once in Awake; subsequent scans by the bootstrap will
        /// find it already present ([DisallowMultipleComponent] on PawnPoseDriver
        /// ensures only one PawnPoseDriver per root, so this path runs once).
        /// </summary>
        private SpriteRenderer EnsureBundleChild()
        {
            // Check if already present (scene reload or hot-reload path).
            Transform existing = transform.Find(BundleChildName);
            if (existing != null)
            {
                var existingSr = existing.GetComponent<SpriteRenderer>();
                if (existingSr != null)
                {
                    existing.localPosition = bundleLocalPos;
                    existingSr.enabled = false;
                    return existingSr;
                }
            }

            // Create a new child GameObject for the carry-bundle overlay.
            var bundleGo = new GameObject(BundleChildName);
            bundleGo.transform.SetParent(transform, worldPositionStays: false);
            bundleGo.transform.localPosition = bundleLocalPos;
            bundleGo.transform.localRotation = Quaternion.identity;
            bundleGo.transform.localScale    = Vector3.one;

            var sr = bundleGo.AddComponent<SpriteRenderer>();
            // SortingOrder: one above the body child so the bundle draws on top.
            sr.sortingOrder = 11;

            // Load the carry_bundle sprite from Resources.
            // FLAG QA / Build-Engineer: Assets/Sprites/carry_bundle.png must be
            // copied to Assets/Resources/Sprites/carry_bundle.png for this load
            // to succeed.  If null, the bundle SR stays disabled (graceful no-op).
            Sprite bundleSprite = Resources.Load<Sprite>(BundleSpritePath);
            if (bundleSprite != null)
                sr.sprite = bundleSprite;

            sr.enabled = false; // hidden by default; enabled when carrying
            return sr;
        }

        private void Update()
        {
            // 상태별 처리: Dead/Downed 면 corpse-pose lane 만 돌고 carry/lunge 는 정지.
            //  PawnHealth.State 를 매 프레임 read(이벤트 구독 X — bug-pattern #7 회피).
            PawnHealth.PoseState state = (health != null)
                ? health.State : PawnHealth.PoseState.Alive;

            if (state == PawnHealth.PoseState.Dead || state == PawnHealth.PoseState.Downed)
            {
                UpdateCorpsePose(state);
                return; // 죽은/의식불명 림은 짐 들기/공격 lunge 안 함
            }

            // 살아있으면 corpse 회전을 0 으로 되돌리고(회복/부활 대비) 바/이름 복구.
            if (currentRoll != 0f || barsHidden) RestoreFromCorpse();

            UpdateCarryBundle();
            UpdateAttackLunge();
        }

        // ─────────────── Corpse / downed pose (운영자 fb 2026-05-31) ──────────
        // Dead   → body 자식을 deadRollDeg 만큼 눕히고, 머리위 바/이름을 숨긴다.
        // Downed → downedRollDeg 만큼 기울여 Dead 와 구분(살아있으므로 바/이름 유지).
        // 회전은 자식 transform.localRotation.Z 에만 적용(root 절대 안 건드림).
        // walk-bob 정지는 PawnHealth.StopBodyBob 이 state-source 에서 게이트하므로
        //  자식 localPosition.Y 가 더 안 흔들려, 여기 회전과 충돌하지 않는다.
        private void UpdateCorpsePose(PawnHealth.PoseState state)
        {
            float targetRoll = (state == PawnHealth.PoseState.Dead)
                ? deadRollDeg : downedRollDeg;

            // 회전 각을 부드럽게 ease (톡 튀지 않게).  unscaledDeltaTime → 일시정지/
            //  타임스케일 변화에도 일관 (lunge 와 동일 규칙).
            currentRoll = Mathf.MoveTowards(
                currentRoll, targetRoll, rollEaseSpeed * 60f * Time.unscaledDeltaTime);

            if (bodyChild != null)
            {
                // 자식 localRotation 의 Z 만 set.  X 의 lunge offset 은 corpse 상태에선
                //  적용 안 하므로(위 early-return) 여기서 그대로 둬도 충돌 없음.
                bodyChild.localRotation = Quaternion.Euler(0f, 0f, currentRoll);

                // Dead 면 PawnSpriteBob 컴포넌트가 PawnHealth.StopBodyBob 에 의해
                //  disable 되어 root tint 를 자식으로 mirror 하지 못한다 → 자식 body 가
                //  alive 색에 멈춤.  그러면 corpse 가 회색조로 안 보이므로, 이 driver 가
                //  root 렌더러(PawnEntity 가 deadTint 로 set)의 색을 자식에 직접 mirror
                //  한다.  PawnSpriteBob 가 꺼져 있으므로 색 writer 충돌 없음(단독 writer).
                //  Downed 는 PawnSpriteBob 가 살아있어 스스로 mirror 하므로 여기선 안 건드림.
                if (state == PawnHealth.PoseState.Dead)
                    MirrorRootColorToBody();
            }

            // Dead 일 때만 머리위 바/이름 숨김(downed 는 정보 유지).
            if (state == PawnHealth.PoseState.Dead && !barsHidden)
                SetBarsAndLabelVisible(false);
            else if (state == PawnHealth.PoseState.Downed && barsHidden)
                SetBarsAndLabelVisible(true); // dead→downed 회복 경로 대비
        }

        // Dead 일 때 root 렌더러 색(PawnEntity 가 deadTint 로 set)을 자식 body
        //  렌더러에 직접 복사.  PawnSpriteBob 가 꺼진 동안의 단독 색 writer.
        private void MirrorRootColorToBody()
        {
            if (bodyChild == null) return;
            var bodySr = bodyChild.GetComponent<SpriteRenderer>();
            if (bodySr == null) return;
            var rootSr = GetComponent<SpriteRenderer>();
            if (rootSr == null) return;
            if (bodySr.color != rootSr.color) bodySr.color = rootSr.color;
        }

        // 살아난(또는 처음부터 alive) 림의 corpse 흔적을 원복.
        private void RestoreFromCorpse()
        {
            currentRoll = Mathf.MoveTowards(
                currentRoll, 0f, rollEaseSpeed * 60f * Time.unscaledDeltaTime);
            if (bodyChild != null)
                bodyChild.localRotation = Quaternion.Euler(0f, 0f, currentRoll);
            if (barsHidden) SetBarsAndLabelVisible(true);
        }

        // 머리위 HP/mood 바 + 이름/상태 라벨 표시 토글.  PawnFloatingBars 컴포넌트는
        //  바 4개 SpriteRenderer 의 부모이므로 컴포넌트 enabled 만으로는 그림이 안
        //  사라진다 → 자식 렌더러를 직접 토글.  이름은 PawnNameLabel 가 만든
        //  NameLabel/StatusLabel/NamePlate 자식 렌더러를 토글.
        private void SetBarsAndLabelVisible(bool visible)
        {
            barsHidden = !visible;
            // 바: PawnFloatingBars 가 만든 자식 GameObject 이름으로만 토글.
            //  GetComponentsInChildren 로 싹 토글하면 body(PawnSpriteBob)/shadow 까지
            //  꺼져 corpse 자체가 사라지므로, 바 4개 자식 이름만 정확히 끈다.
            ToggleBarRenderer("HpBg",   visible);
            ToggleBarRenderer("HpFill", visible);
            ToggleBarRenderer("MoodBg", visible);
            ToggleBarRenderer("MoodFill", visible);
            // 이름/상태/plate 자식.
            ToggleChildRenderers(NameLabelChild, visible);
            ToggleChildRenderers(StatusLabelChild, visible);
            ToggleChildRenderers(NamePlateChild, visible);
        }

        private void ToggleChildRenderers(string childName, bool visible)
        {
            Transform t = transform.Find(childName);
            if (t == null) return;
            var mr = t.GetComponent<MeshRenderer>();   // TextMesh → MeshRenderer
            if (mr != null) mr.enabled = visible;
            var sr = t.GetComponent<SpriteRenderer>(); // NamePlate
            if (sr != null) sr.enabled = visible;
        }

        // 바 자식(직속 child) SpriteRenderer 1개를 토글.
        private void ToggleBarRenderer(string childName, bool visible)
        {
            Transform t = transform.Find(childName);
            if (t == null) return;
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = visible;
        }

        // ─────────────── Carry bundle ────────────────────────────────────────
        private void UpdateCarryBundle()
        {
            if (bundleRenderer == null) return;

            // PawnHauler.HasTask is the available public accessor.
            // FLAG QA / Programmer: add PawnHauler.IsCarrying (carryingWood>0 ||
            // carryingStone>0 || carryingFood>0) for a tighter "holding item" gate.
            bool shouldShow = hauler != null && hauler.HasTask;
            if (bundleRenderer.enabled != shouldShow)
                bundleRenderer.enabled = shouldShow;
        }

        // ─────────────── Attack lunge ────────────────────────────────────────
        private void UpdateAttackLunge()
        {
            if (bodyChild == null) return;

            bool attacking = DetectAttack();

            if (attacking && !lungeActive)
            {
                // Trigger a new lunge.
                lungeActive = true;
                currentLungeX = lungeDistance;
                lungeTimer = lungeReturnSec;
            }

            if (lungeActive)
            {
                // Ease the lunge back toward zero over lungeReturnSec.
                lungeTimer -= Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(1f - (lungeTimer / lungeReturnSec));
                // Lerp from peak lunge to 0 as timer counts down.
                currentLungeX = Mathf.Lerp(lungeDistance, 0f, t);

                if (lungeTimer <= 0f)
                {
                    currentLungeX = 0f;
                    lungeActive = false;
                }
            }

            // Apply the lunge offset to the body child's local X.
            // We READ the current localPosition (which PawnSpriteBob may be writing
            // to in the same frame on Y) and add our X offset, leaving Y untouched.
            // We never write the root transform.
            Vector3 local = bodyChild.localPosition;
            float targetX = lungeActive ? currentLungeX : 0f;
            // Determine lunge direction: face right = positive X lunge; face left =
            // negative X.  PawnFacing writes the child SR's flipX, not the transform
            // localScale.  We read the child SR's flipX to mirror the lunge direction.
            SpriteRenderer childSr = cachedChildSr != null ? cachedChildSr : (bodyChild != null ? bodyChild.GetComponent<SpriteRenderer>() : null);  // #audit4 #3 캐시
            if (childSr != null && childSr.flipX)
                targetX = -targetX;

            bodyChild.localPosition = new Vector3(targetX, local.y, local.z);
        }

        /// <summary>
        /// Returns true when this pawn is actively in melee/attack range of a target.
        /// Uses public accessors only; flags missing ones in comments above.
        ///
        /// Detection strategy (most-specific first):
        ///   1. Drafted + DraftedAttackTarget alive and within AttackRange.
        ///   2. Throttled BanditEnemy proximity scan (mirrors PawnEntity.Update logic).
        /// </summary>
        private bool DetectAttack()
        {
            if (pawnEntity == null || pawnEntity.IsDead) return false;

            // Path 1: drafted melee — target explicitly assigned.
            if (pawnEntity.IsDrafted && pawnEntity.DraftedAttackTarget != null
                && !pawnEntity.DraftedAttackTarget.IsDead)
            {
                float dist = Vector2.Distance(transform.position,
                    pawnEntity.DraftedAttackTarget.transform.position);
                if (dist <= pawnEntity.AttackRange) return true;
            }

            // Path 2: auto-attack — throttled BanditEnemy range poll.
            // FLAG QA / Programmer: add PawnEntity.IsAttacking (wraps nextAttackTime)
            // for a single-frame-accurate window; until then, range proximity is the
            // best read-only signal available without editing PawnEntity.
            if (Time.unscaledTime >= nextAttackCheck)
            {
                nextAttackCheck = Time.unscaledTime + attackCheckInterval;
                cachedInRange = HasLiveBanditInRange();
            }

            return cachedInRange;
        }

        /// <summary>
        /// Throttled check: is any live BanditEnemy within this pawn's AttackRange?
        /// Only called every attackCheckInterval seconds.
        /// </summary>
        private bool HasLiveBanditInRange()
        {
            if (pawnEntity == null) return false;
            float rangeSq = pawnEntity.AttackRange * pawnEntity.AttackRange;
            Vector3 myPos = transform.position;
#if UNITY_2023_1_OR_NEWER
            var bandits = Object.FindObjectsByType<BanditEnemy>(FindObjectsSortMode.None);
#else
            var bandits = Object.FindObjectsOfType<BanditEnemy>();
#endif
            foreach (var b in bandits)
            {
                if (b == null || b.IsDead) continue;
                if ((b.transform.position - myPos).sqrMagnitude <= rangeSq) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Self-attach bootstrap + runtime sweep for PawnPoseDriver.
    /// Copies the FlickerLight.cs / PawnFacing.cs / SleepPoseDriver.cs pattern
    /// exactly: hidden DontDestroyOnLoad host GO + FindFirstObjectByType idempotency
    /// guard + periodic PawnMovement sweep.
    ///
    /// runInBackground note: driver throttles via Time INSIDE Update (not a
    /// WaitForSeconds coroutine) — same reasoning as PawnFacing / SleepPoseDriver.
    /// </summary>
    internal static class PawnPoseDriverBootstrap
    {
        private const float ScanInterval = 1.0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindSweepDriver() != null) return;

                var go = new GameObject("~PawnPoseDriver");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                go.AddComponent<PawnPoseSweepDriver>();
                    });
        }

        private static PawnPoseSweepDriver FindSweepDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<PawnPoseSweepDriver>();
#else
            return Object.FindObjectOfType<PawnPoseSweepDriver>();
#endif
        }

        internal static void EnsureOn(PawnMovement pm)
        {
            if (pm == null) return;
            // PawnMovement lives on the pawn ROOT — AddComponent lands PawnPoseDriver
            // on the ROOT, exactly where it can GetComponent<PawnHauler/PawnEntity>
            // and Find("PawnSpriteBob") child.  [DisallowMultipleComponent] + this
            // guard make the call idempotent.
            if (pm.GetComponent<PawnPoseDriver>() == null)
                pm.gameObject.AddComponent<PawnPoseDriver>();
        }

        private sealed class PawnPoseSweepDriver : MonoBehaviour
        {
            private float nextScan;
            // Reused scratch buffer — no per-frame allocation.
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
