using UnityEngine;
using MelonS.GameProto.Data;

namespace MelonS.GameProto
{
    /// <summary>
    /// A single colonist (pawn) entity.  Day 1 = idle stand, click-to-select.
    /// Day 2+ will add: movement, needs (food/sleep/mood), utility AI.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PawnEntity : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string pawnName = "Colonist";

        [Header("Selection visual")]
        [SerializeField] private Color selectedOutlineColor = new Color(1f, 0.9f, 0.3f, 1f);
        [SerializeField] private Color unselectedColor = Color.white;

        // R2: combat stats 외부화 - PawnStats ScriptableObject.
        //  inspector 비워두면 Awake 에서 CreateDefault() 로 30/1/1.0/1.0 채움
        //  (legacy 빌드 호환).  asset 만들고 wire 하면 그 값 사용.
        [Header("Combat stats (R2 - PawnStats SO)")]
        [SerializeField] private PawnStats stats;

        // Read-only accessors (외부 코드 ePerty 변경 X)
        public int MaxHp => stats != null ? stats.maxHp : 30;
        public int AttackDamage => stats != null ? stats.attackDamage : 5;  // #8 fallback 도 PawnStats 기본(5)과 동기화
        public float AttackRange => stats != null ? stats.attackRange : 1.0f;
        public float AttackInterval => stats != null ? stats.attackInterval : 1.0f;

        private SpriteRenderer spriteRenderer;
        private bool selected;

        // 운영자 fb (2026-05-31): 상태별 시각 처리.  ROOT 렌더러 색은 PawnSpriteBob 가
        //  자식으로 mirror 하므로, dead/downed tint 를 root 에 쓰면 자식 body 에도 반영됨.
        //  Dead   = 회색조 corpse tint, Downed = desaturate dim, 그 외 = drafted/selection.
        [Header("Health-state tint (R - 2026-05-31)")]
        [SerializeField] private Color deadTint   = new Color(0.42f, 0.40f, 0.40f, 1f); // 회색조 corpse
        [SerializeField] private Color downedTint = new Color(0.62f, 0.55f, 0.62f, 1f); // 의식불명 누움
        // State change 를 polling 으로 감지(이벤트 구독 race 회피, bug-pattern #7).
        private PawnHealth healthRef;
        private int lastSeenHealthVersion = -1;

        public string PawnName => pawnName;
        public bool IsSelected => selected;
        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;

        // #버그헌트(2026-06-04): PawnHealth 경로 사망(출혈/부위 HP=0)을 PawnEntity.Hp 에 동기화.
        //  출혈 사망은 PawnHealth.Update→CheckDeath 로 일어나 PawnEntity.TakeDamage 를 안 거치므로
        //  Hp 가 >0 그대로 남아 IsDead(=Hp<=0)가 false → BanditEnemy/WolfEnemy 가 시체를 살아있는
        //  타겟으로 보고 영원히 헛공격(멀쩡한 다른 폰 무시)했다.  PawnHealth.CheckDeath 가 사망
        //  확정 시 호출(컴포넌트가 곧 disable 돼도 메서드 호출/프로퍼티는 동작).
        public void ForceSyncDead()
        {
            Hp = 0;
            // #버그헌트2(2026-06-04): 출혈/부위HP=0 사망 시 시신을 회색조(deadTint)로.  ForceSyncDead
            //  가 Hp 만 0 으로 두고 ApplyVisual 을 안 불러, 출혈 사망 corpse 가 살아있을 때 색(선택
            //  노랑/징집 시안/기본)으로 굳었다(즉사는 TakeDamage 경로라 정상).  CheckDeath 가 곧
            //  PawnEntity 를 disable 해도 같은 컴포넌트 메서드 호출은 동작 → 여기서 색 확정.
            if (healthRef != null) lastSeenHealthVersion = healthRef.StateVersion;
            ApplyVisual();
        }

        // #버그헌트2(2026-06-04): 부상-but-생존 폰 로드 복원 시 PawnEntity.Hp 를 PawnHealth 와 동기화.
        //  Awake 가 Hp=maxHp(풀피)로 깔고, RestorePartState 는 죽지 않으면 ForceSyncDead 미호출 →
        //  빈사로 저장→로드한 폰이 다음 피격 전까지 풀피로 표시됐다.  GameSaveButtons.OnLoad 가
        //  RestorePartState 직후 호출.
        public void SyncHpFromHealth()
        {
            if (healthRef == null || stats == null) return;
            if (healthRef.IsDead) { Hp = 0; return; }
            Hp = Mathf.Max(1, Mathf.RoundToInt(healthRef.TotalHpRatio * stats.maxHp));
        }

        // ─── Melee-hit signal for CombatLungeDriver (운영자 fb 2026-05-31) ───────
        //  B10 FLAG-B10-3 fulfilled: a public, frame-accurate "an attack just landed"
        //  signal so the lunge driver fires per-swing instead of guessing from
        //  proximity.  RegisterMeleeHit stamps Time.time + a unit direction toward the
        //  target on EACH actual attack tick (called from this entity's auto-attack
        //  hit below).  CombatLungeDriver POLLS LastMeleeHitTime (no event subscription
        //  — bug-pattern #7) and edge-detects a new value to trigger a fresh lunge.
        //  LastAttackWasMelee lets the driver suppress the lunge on a ranged (bow) shot
        //  (an arrow shows the attack, not a jab).
        public float   LastMeleeHitTime { get; private set; } = -999f;
        public Vector2 LastMeleeHitDir  { get; private set; } = Vector2.right;
        public bool    LastAttackWasMelee { get; private set; } = true;

        /// <summary>Record an attack tick for the visual lunge.  targetWorldPos = the
        ///  thing being hit (for the jab direction).  melee=false for a ranged shot
        ///  (suppresses the lunge; the arrow is the visual).  Direction degenerates to
        ///  the last value when the target overlaps the pawn exactly.</summary>
        public void RegisterMeleeHit(Vector2 targetWorldPos, bool melee = true)
        {
            LastMeleeHitTime = Time.time;
            LastAttackWasMelee = melee;
            Vector2 d = targetWorldPos - (Vector2)transform.position;
            if (d.sqrMagnitude > 1e-6f) LastMeleeHitDir = d.normalized;
        }

        // B10 FLAG-B10-3: public "currently mid-swing" window derived from the auto-
        //  attack timer.  True for the first half of the attack interval after a tick.
        public bool IsAttacking =>
            Time.time < nextAttackTime &&
            Time.time < LastMeleeHitTime + (stats != null ? stats.attackInterval : 1f) * 0.5f;

        // 운영자 피드백: 우클릭 이동이 AI 에 즉시 override 됐던 문제.
        //  ClickSelector 가 우클릭 시 이 값을 Time.time + 5 로 set.
        //  PawnUtilityAI.Update 가 Time.time < ManualMoveUntil 이면 AI Decide skip.
        public float ManualMoveUntil { get; set; } = -10f;
        public bool IsUnderManualControl => Time.time < ManualMoveUntil;

        // Day 48: drafted (manual control) state.  When drafted:
        //  - PawnUtilityAI suspended
        //  - Right-click on enemy = attack target
        //  - Right-click on ground = manual move
        //  - Visual: cyan outline tint
        public bool IsDrafted { get; private set; }
        public BanditEnemy DraftedAttackTarget { get; set; }
        public AnimalEntity DraftedHuntTarget   { get; set; }
        public WolfEnemy DraftedWolfTarget      { get; set; }  // Day 64

        public void SetDrafted(bool value)
        {
            if (IsDrafted == value) return;
            IsDrafted = value;
            if (IsDrafted)
            {
                // #199 C2 — a drafted pawn drops all work; release its reservations
                //  (target + stand cell) so other colonists can claim that work
                //  while this one is under manual control.  ClearTask on each worker
                //  releases the central reservations; ReleaseAll is the backstop.
                ClearAllWorkTasks();
                MelonS.GameProto.AI.ReservationManager.ReleaseAll(gameObject);
            }
            if (!IsDrafted)
            {
                DraftedAttackTarget = null;
                DraftedHuntTarget = null;
                DraftedWolfTarget = null;
            }
            ApplyVisual();
            Debug.Log($"[Pawn:{pawnName}] draft={IsDrafted}");
        }

        // #199 C2 — clear every worker component's task (each ClearTask releases its
        //  own reservations).  Used on draft and death/downed.
        private void ClearAllWorkTasks()
        {
            GetComponent<PawnChopper>()?.ClearTask();
            GetComponent<PawnGatherer>()?.ClearTask();
            GetComponent<PawnHarvester>()?.ClearTask();  // #14 누락 보강 — 수확 중 징집/사망 시 작물 예약 누수 방지
            GetComponent<PawnHunter>()?.ClearTask();
            GetComponent<PawnCook>()?.ClearTask();
            GetComponent<PawnHauler>()?.ClearTask();
            GetComponent<PawnBuilder>()?.ClearTask();
            GetComponent<PawnBuilder>()?.ClearDeconstructTask();   // 철거도 워커 일종 — 누락 보강
            GetComponent<PawnBuilder>()?.ClearRoofTask();          // 소크 r2 #1 - 지붕 시공
            GetComponent<PawnMiner>()?.ClearTask();
            GetComponent<PawnDoctor>()?.ClearTask();
        }

        // #199 C2 — release ALL reservations on destroy so a despawned/dead pawn
        //  never leaks a target/cell lock (R-5).  Backstop to the per-worker
        //  ClearTask releases that fire on the normal death/draft paths.
        private void OnDestroy()
        {
            MelonS.GameProto.AI.ReservationManager.ReleaseAll(gameObject);
        }

        private float nextAttackTime = -1f;

        // Bandit-target cache — refresh at most every 0.25s rather than every
        // frame.  Lesson #4: FindObjectsOfType per-Update on every PawnEntity
        // is O(n*m) and was the suspected cause of the Day 3 06:00 hang/NRE
        // when the first BanditEnemy spawned (combined effect: each of 3 pawns
        // scans ALL pawns+bandits every frame, then the bandit scans again).
        private BanditEnemy cachedBandit;
        private float nextBanditSearchTime = -1f;
        private const float BanditSearchInterval = 0.25f;

        /// <summary>r2 #8 (2026-06-12) — 런타임 합류 림(방랑자/포로)의 콜로니스트 키트.
        ///  조립 누락으로 건축·채광·수확·치료 영구 불가 + 특성/장비/무드 무사고이던 것 —
        ///  시작 림과 동일 세트 보장 (있으면 no-op).</summary>
        public static void AttachColonistKit(GameObject go)
        {
            void Ensure<T>() where T : Component { if (go.GetComponent<T>() == null) go.AddComponent<T>(); }
            Ensure<PawnBuilder>(); Ensure<PawnMiner>(); Ensure<PawnHarvester>();
            Ensure<PawnDoctor>(); Ensure<PawnSchedule>(); Ensure<PawnTraits>();
            Ensure<PawnThoughts>(); Ensure<PawnAbilities>(); Ensure<PawnEquipment>();
            Ensure<PawnNameLabel>(); Ensure<PawnFloatingBars>();
        }

        // 합류 림 한글 이름 풀 (운영 소크 발견 2026-06-12: 합류자가 전부 기본값
        //  "Colonist"로 남아 사망 카드까지 영문 노출).  시작 림(씬 직렬화 민지/서연/지훈)과
        //  겹치지 않는 풀에서 현재 폰 수 기반 결정론 선택 — UnityEngine.Random 비소비
        //  (재현 rng 체인 불변).
        private static readonly string[] JoinerNames =
            { "하준", "도윤", "은우", "시우", "수아", "하린", "지아", "예준", "윤서", "채원" };

        /// <summary>림월드갭 TOP-2 (2026-06-12) — 우클릭 '우선' 명령이 기존 작업을
        /// 안 끊고 task 만 추가해 이동 줄다리기로 씹히던 것: 수동 배정 직전 전 워커
        /// task 해제 (레퍼런스: 수동 명령 = 현재 작업 즉시 취소).</summary>
        public static void ClearAllWork(GameObject go)
        {
            if (go == null) return;
            go.GetComponent<PawnChopper>()?.ClearTask();
            go.GetComponent<PawnGatherer>()?.ClearTask();
            go.GetComponent<PawnHunter>()?.ClearTask();
            go.GetComponent<PawnCook>()?.ClearTask();
            go.GetComponent<PawnHauler>()?.ClearTask();
            go.GetComponent<PawnBuilder>()?.ClearTask();
            go.GetComponent<PawnMiner>()?.ClearTask();
            go.GetComponent<PawnHarvester>()?.ClearTask();
            go.GetComponent<PawnDoctor>()?.ClearTask();
        }

        public static string NextJoinerName()
        {
            int n = UnityEngine.Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None).Length;
            return JoinerNames[n % JoinerNames.Length];
        }

        public void SetPawnName(string n)
        {
            if (!string.IsNullOrEmpty(n)) pawnName = n;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            BlobShadow.Attach(gameObject, 0.8f, -0.42f);  // D1 몰입 — 발밑 접지 그림자
            if (GetComponent<MotionFx>() == null) gameObject.AddComponent<MotionFx>();  // D2 걷기 모션
            if (GetComponent<PawnSpriteAnimator>() == null)
                gameObject.AddComponent<PawnSpriteAnimator>();  // 아트 v2 — 프레임 애니메이터
            healthRef = GetComponent<PawnHealth>();
            // R2: SO 없으면 default 인스턴스 — game 멈추지 X
            if (stats == null) stats = PawnStats.CreateDefault();
            Hp = stats.maxHp;
            ApplyVisual();
        }

        private void Update()
        {
            // 상태 변화(downed/회복 등) polling 감지 → root tint 갱신.  Dead 진입은
            //  TakeDamage 에서 이 컴포넌트가 disable 되기 직전에 ApplyVisual 로 처리.
            if (healthRef != null && healthRef.StateVersion != lastSeenHealthVersion)
            {
                lastSeenHealthVersion = healthRef.StateVersion;
                ApplyVisual();
            }
            if (IsDead) return;
            // #버그헌트(2026-06-04): 다운(의식불명)이면 자동공격 정지.  PawnUtilityAI 가 이동/작업을
            //  멈춰도 이 자동공격 루프는 별도 경로라, 게이트 없으면 다운된 폰이 계속 적을 때렸다.
            if (healthRef != null && healthRef.IsDowned) return;
            // #버그헌트2(2026-06-04): 징집/수동제어 중에는 자동공격 정지.  전투는 HandleDraftedCombat
            //  단일 경로여야 하는데, 게이트가 없어 징집 폰이 (a) 후퇴/이동 명령 무시하고 사거리 적
            //  자동타격, (b) 지정 타겟 외 더 가까운 적도 동시 타격했다('징집=완전 수동' 설계 위반).
            if (IsDrafted || IsUnderManualControl) return;
            // Day 13: defensive auto-attack on nearby bandit (throttled, NOT
            // per-frame — lesson #4 firewall).  We use distance polling rather
            // than collider Enter/Stay because pawns and bandits both move on
            // transform; Stay fires only when both colliders are touching,
            // which is unreliable here without rigidbodies.
            //
            // Early-out before the search: if no BanditEnemy can possibly exist
            // (Day < 3 case), the search returns null fast — but we still pay
            // the FindObjectsOfType cost.  The cache below gates that.
            if (cachedBandit == null || cachedBandit.IsDead || Time.time >= nextBanditSearchTime)
            {
                cachedBandit = FindNearestBandit();
                nextBanditSearchTime = Time.time + BanditSearchInterval;
            }

            BanditEnemy nearest = cachedBandit;
            if (nearest == null) return;
            // Re-check post-cache: bandit could have been killed by another
            // pawn this same frame.  Without this, transform access NREs.
            if (nearest.IsDead) return;
            if (Vector3.Distance(transform.position, nearest.transform.position) > stats.attackRange) return;
            if (Time.time < nextAttackTime) return;
            nextAttackTime = Time.time + stats.attackInterval;
            // #277 자동공격도 무기/근접배수/전투스킬 반영 — 이전엔 stats.attackDamage 만 써
            //  drafted 공식((base+wpn)*ml*sk)과 2~6배 비대칭이었다(rank10, 승인).  draft 토글은
            //  스윙 *간격*만 다르고 스윙당 데미지는 동일해진다.
            var skills = GetComponent<PawnSkills>();
            var equip = GetComponent<PawnEquipment>();
            var abil = GetComponent<PawnAbilities>();
            float wpn = equip != null ? equip.TotalMeleeDamageBonus() : 0f;
            float ml = abil != null ? abil.meleeMul : 1f;
            float sk = skills != null ? (1f + skills.GetLevel(SkillKind.Combat) * 0.03f) : 1f;
            int admg = Mathf.Max(1, Mathf.RoundToInt((stats.attackDamage + wpn) * ml * sk));
            nearest.TakeDamage(admg, gameObject);
            // 운영자 fb 2026-05-31: stamp the actual hit so CombatLungeDriver shows a
            //  forward jab on this bare-fist / melee swing (it polls LastMeleeHitTime).
            RegisterMeleeHit(nearest.transform.position, melee: true);
            // Day 20: Combat XP per attack tick
            if (skills != null) skills.AddXP(SkillKind.Combat, 5f);
        }

        private BanditEnemy FindNearestBandit()
        {
            BanditEnemy[] bandits = GameObject.FindObjectsByType<BanditEnemy>(FindObjectsSortMode.None);
            if (bandits == null || bandits.Length == 0) return null;
            BanditEnemy nearest = null;
            float bestSq = float.MaxValue;
            Vector3 myPos = transform.position;
            foreach (var b in bandits)
            {
                if (b == null) continue;
                if (b.IsDead) continue;
                float sq = (b.transform.position - myPos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; nearest = b; }
            }
            return nearest;
        }

        public void TakeDamage(int dmg, GameObject source = null)
        {
            if (IsDead) return;
            // #173 - PawnEquipment.TotalMelArmor 감소율 적용 (이전: armor SET 만 됨, 효과 0).
            //  체인메일 0.45 + 철 헬멧 0.30 → 0.75 (cap 0.85) → 데미지 25% 만 통과.
            var equip = GetComponent<PawnEquipment>();
            if (equip != null)
            {
                float armor = equip.TotalMelArmor();  // 0~0.85
                int reduced = Mathf.Max(0, Mathf.RoundToInt(dmg * (1f - armor)));
                if (reduced <= 0 && dmg > 0)
                {
                    Debug.Log($"[Pawn:{pawnName}] armor 완전 방어 ({dmg}→0, armor={armor:F2})");
                    // 감사 rank5: 완전방어 시각 피드백.
                    FloatingText.Spawn(transform.position + Vector3.up * 0.6f, "막음", new Color(0.72f, 0.74f, 0.78f));
                    return;
                }
                if (reduced < dmg)
                    Debug.Log($"[Pawn:{pawnName}] armor {armor:F2}x → dmg {dmg}→{reduced}");
                dmg = reduced;
            }
            AudioBank.Instance?.PlayHit();   // #게임필2 — 림 피격이 들린다 (0.25s 스로틀 내장)
            ApplyDamage(dmg);
        }

        /// <summary>방어구를 무시하는 직격 데미지 — 아사/내부 출혈 등 '장비로 못 막는'
        ///  피해 경로 (#생존압박 2026-06-11: TakeDamage(1)이 armor 0.5 라운딩으로 0 이
        ///  되어 굶주림이 '막음' 처리되던 버그).  전투 사운드 없이 본체 적용만.</summary>
        public void TakeTrueDamage(int dmg)
        {
            if (IsDead) return;
            ApplyDamage(dmg);
        }

        private void ApplyDamage(int dmg)
        {
            // Day 45: route damage to PawnHealth body parts if present.
            var health = GetComponent<PawnHealth>();
            if (health != null)
            {
                var part = health.TakeDamage(dmg);
                if (part != null)
                {
                    Debug.Log($"[Pawn:{pawnName}] {part.nameKr}({part.hp}/{part.maxHp}) -{dmg} dmg");
                    // 감사 rank5(B6 미연결 fix): 부위명+데미지 플로팅 숫자(이미 구현된 FloatingText 연결).
                    FloatingText.Spawn(transform.position + Vector3.up * 0.6f,
                        $"{part.nameKr} -{dmg}", new Color(0.95f, 0.30f, 0.25f));
                }
                if (health.IsDead)
                {
                    Hp = 0;
                    Debug.Log($"[Pawn:{pawnName}] DOWN (body-part death)");
                    // #199 C2 — dead/downed pawn frees its work for others.
                    ClearAllWorkTasks();
                    MelonS.GameProto.AI.ReservationManager.ReleaseAll(gameObject);
                    // 죽는 순간 corpse 회색조 tint 를 root 에 적용(PawnSpriteBob 가
                    //  자식으로 mirror).  이 컴포넌트가 disable 되기 직전에 호출해야
                    //  Update poll 이 못 도는 사망 상태에서도 색이 반영된다.
                    lastSeenHealthVersion = health.StateVersion;
                    ApplyVisual();
                    enabled = false;
                    return;
                }
                // Sync legacy Hp from total ratio for UI compat
                Hp = Mathf.Max(1, Mathf.RoundToInt(health.TotalHpRatio * stats.maxHp));
                return;
            }
            // Fallback: legacy single-pool HP
            Hp = Mathf.Max(0, Hp - dmg);
            Debug.Log($"[Pawn:{pawnName}] took {dmg} dmg (HP={Hp})");
            if (Hp <= 0)
            {
                Debug.Log($"[Pawn:{pawnName}] DOWN");
                // #199 C2 — dead/downed pawn frees its work for others.
                ClearAllWorkTasks();
                MelonS.GameProto.AI.ReservationManager.ReleaseAll(gameObject);
                enabled = false;
            }
        }

        public void SetSelected(bool value)
        {
            if (selected == value) return;
            selected = value;
            ApplyVisual();
            if (selected) AudioBank.Instance?.PlaySelect();
            Debug.Log($"[Pawn:{pawnName}] selected={selected}");
        }

        private void ApplyVisual()
        {
            if (spriteRenderer == null) return;
            // 우선순위: Dead > Downed > Drafted > Selected > Unselected.
            //  health-state tint 는 root 에 쓰고 PawnSpriteBob 가 자식으로 mirror 한다.
            //  Dead 의 90° 쓰러짐 회전은 PawnPoseDriver(자식 transform lane)가 담당.
            if (healthRef != null)
            {
                var st = healthRef.State;
                if (st == PawnHealth.PoseState.Dead)
                {
                    spriteRenderer.color = deadTint;     // 회색조 corpse
                    return;
                }
                if (st == PawnHealth.PoseState.Downed)
                {
                    spriteRenderer.color = downedTint;   // 의식불명 누움
                    return;
                }
            }
            // Day 1: tint to indicate selection. Real outline shader = later.
            // Day 48: drafted pawn tinted cyan (manual-control mode).
            if (IsDrafted)
                spriteRenderer.color = new Color(0.55f, 0.85f, 1.0f, 1f);  // cyan
            else
                spriteRenderer.color = selected ? selectedOutlineColor : unselectedColor;
        }
    }
}
