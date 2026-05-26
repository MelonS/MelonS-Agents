using UnityEngine;

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

        [Header("Combat (Day 13)")]
        [SerializeField] private int maxHp = 30;
        [SerializeField] private int attackDamage = 1;
        [SerializeField] private float attackRange = 1.0f;
        [SerializeField] private float attackInterval = 1.0f;

        private SpriteRenderer spriteRenderer;
        private bool selected;

        public string PawnName => pawnName;
        public bool IsSelected => selected;
        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;

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
            Hp = maxHp;
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
            if (Vector3.Distance(transform.position, nearest.transform.position) > attackRange) return;
            if (Time.time < nextAttackTime) return;
            nextAttackTime = Time.time + attackInterval;
            nearest.TakeDamage(attackDamage, gameObject);
            // Day 20: Combat XP per attack tick
            var skills = GetComponent<PawnSkills>();
            if (skills != null) skills.AddXP(SkillKind.Combat, 5f);
        }

        private BanditEnemy FindNearestBandit()
        {
            BanditEnemy[] bandits = GameObject.FindObjectsOfType<BanditEnemy>();
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
                    enabled = false;
                    return;
                }
                // Sync legacy Hp from total ratio for UI compat
                Hp = Mathf.Max(1, Mathf.RoundToInt(health.TotalHpRatio * maxHp));
                return;
            }
            // Fallback: legacy single-pool HP
            Hp = Mathf.Max(0, Hp - dmg);
            Debug.Log($"[Pawn:{pawnName}] took {dmg} dmg (HP={Hp})");
            if (Hp <= 0)
            {
                Debug.Log($"[Pawn:{pawnName}] DOWN");
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
            spriteRenderer.color = selected ? selectedOutlineColor : unselectedColor;
        }
    }
}
