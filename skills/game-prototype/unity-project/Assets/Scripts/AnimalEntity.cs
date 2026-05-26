using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Day 23: peaceful wandering animal.  HP 12, drops 5 food
    /// when killed.  Wanders random 0.5 unit/sec, idle 2-5s between.
    /// Currently no flee-when-hit (Day 24+).  Pawns don't auto-attack
    /// animals — only attack hostile (BanditEnemy).  Operator can
    /// manually order via Day 24 drafted commands (future).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class AnimalEntity : MonoBehaviour
    {
        [SerializeField] private int maxHp = 12;
        [SerializeField] private int foodDrop = 5;
        [SerializeField] private float wanderSpeed = 0.6f;
        [SerializeField] private float wanderMin = 2f;
        [SerializeField] private float wanderMax = 5f;
        [SerializeField] private float wanderRadius = 4f;

        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;

        // Stretch: Animal taming
        public bool IsTamed { get; private set; }
        [SerializeField] private float tameSuccessRate = 0.3f;
        [SerializeField] private int tameFoodCost = 1;

        /// <summary>
        /// food 소모 + 30% 성공 확률.  성공 시 IsTamed = true,
        /// 푸른빛 tint 로 시각 구별, AnimalEntity Update 가 owner 따라감(향후).
        /// </summary>
        public bool TryTame()
        {
            var rm = MelonS.GameProto.Core.Services.Get<ResourceManager>();
            if (rm == null) return false;
            if (IsTamed) { Debug.Log("[Tame] 이미 길들임"); return false; }
            if (rm.food < tameFoodCost) { Debug.Log("[Tame] 식량 부족"); return false; }
            rm.AddFood(-tameFoodCost);
            float roll = Random.value;
            if (roll < tameSuccessRate)
            {
                IsTamed = true;
                if (sr != null) sr.color = new Color(0.75f, 0.85f, 1.0f, 1f);  // 푸른빛 = tamed
                Debug.Log($"[Tame] 성공! (roll={roll:F2} < {tameSuccessRate})");
                return true;
            }
            Debug.Log($"[Tame] 실패 (roll={roll:F2} ≥ {tameSuccessRate}), food -{tameFoodCost}");
            return false;
        }

        private Rigidbody2D rb;
        private Vector2 target;
        private float nextIdleEnd = 0f;
        private bool walking = false;

        private SpriteRenderer sr;
        private float flashUntil = -1f;

        private void Awake()
        {
            Hp = maxHp;
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            sr = GetComponent<SpriteRenderer>();
            PickNewTarget();
        }

        private void Update()
        {
            if (IsDead) return;
            if (sr != null && flashUntil > 0 && Time.time > flashUntil)
            {
                sr.color = Color.white;
                flashUntil = -1f;
            }
            if (walking)
            {
                Vector2 me = rb.position;
                Vector2 dir = target - me;
                if (dir.sqrMagnitude < 0.05f)
                {
                    walking = false;
                    nextIdleEnd = Time.time + Random.Range(wanderMin, wanderMax);
                    return;
                }
                rb.MovePosition(me + dir.normalized * wanderSpeed * Time.deltaTime);
            }
            else
            {
                if (Time.time > nextIdleEnd) PickNewTarget();
            }
        }

        private void PickNewTarget()
        {
            // Step 81: 맵 안쪽으로 clamp — 외곽으로 wander 못 나감
            target = PawnMovement.ClampToWorld(
                (Vector2)transform.position + Random.insideUnitCircle * wanderRadius);
            walking = true;
        }

        public void TakeDamage(int dmg, GameObject source = null)
        {
            if (IsDead) return;
            Hp = Mathf.Max(0, Hp - dmg);
            if (sr != null) { sr.color = Color.white; flashUntil = Time.time + 0.06f; }
            if (AudioBank.Instance != null) AudioBank.Instance.PlayChop();
            if (Hp <= 0)
            {
                if (ResourceManager.Instance != null) ResourceManager.Instance.AddFood(foodDrop);
                Destroy(gameObject);
            }
        }
    }
}
