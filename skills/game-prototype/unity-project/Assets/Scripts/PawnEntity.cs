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
        public int AttackDamage => stats != null ? stats.attackDamage : 1;
        public float AttackRange => stats != null ? stats.attackRange : 1.0f;
        public float AttackInterval => stats != null ? stats.attackInterval : 1.0f;

        private SpriteRenderer spriteRenderer;
        private bool selected;

        public string PawnName => pawnName;
        public bool IsSelected => selected;
        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;

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
            GetComponent<PawnHunter>()?.ClearTask();
            GetComponent<PawnCook>()?.ClearTask();
            GetComponent<PawnHauler>()?.ClearTask();
            GetComponent<PawnBuilder>()?.ClearTask();
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

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            // R2: SO 없으면 default 인스턴스 — game 멈추지 X
            if (stats == null) stats = PawnStats.CreateDefault();
            Hp = stats.maxHp;
            ApplyVisual();
        }

        private void Update()
        {
            if (IsDead) return;
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
            nearest.TakeDamage(stats.attackDamage, gameObject);
            // Day 20: Combat XP per attack tick
            var skills = GetComponent<PawnSkills>();
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
                    return;
                }
                if (reduced < dmg)
                    Debug.Log($"[Pawn:{pawnName}] armor {armor:F2}x → dmg {dmg}→{reduced}");
                dmg = reduced;
            }
            // Day 45: route damage to PawnHealth body parts if present.
            var health = GetComponent<PawnHealth>();
            if (health != null)
            {
                var part = health.TakeDamage(dmg);
                if (part != null)
                    Debug.Log($"[Pawn:{pawnName}] {part.nameKr}({part.hp}/{part.maxHp}) -{dmg} dmg");
                if (health.IsDead)
                {
                    Hp = 0;
                    Debug.Log($"[Pawn:{pawnName}] DOWN (body-part death)");
                    // #199 C2 — dead/downed pawn frees its work for others.
                    ClearAllWorkTasks();
                    MelonS.GameProto.AI.ReservationManager.ReleaseAll(gameObject);
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
            // Day 1: tint to indicate selection. Real outline shader = later.
            // Day 48: drafted pawn tinted cyan (manual-control mode).
            if (IsDrafted)
                spriteRenderer.color = new Color(0.55f, 0.85f, 1.0f, 1f);  // cyan
            else
                spriteRenderer.color = selected ? selectedOutlineColor : unselectedColor;
        }
    }
}
