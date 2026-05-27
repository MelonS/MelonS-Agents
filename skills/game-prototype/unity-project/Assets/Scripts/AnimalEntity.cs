using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Day 23: peaceful wandering animal.  HP 12, drops 5 food
    /// when killed.  Wanders random 0.5 unit/sec, idle 2-5s between.
    /// Currently no flee-when-hit (Day 24+).  Pawns don't auto-attack
    /// animals — only attack hostile (BanditEnemy).  Operator can
    /// manually order via Day 24 drafted commands (future).
    /// </summary>
    /// <summary>#132 - 동물 종류.</summary>
    public enum AnimalSpecies { Deer, Boar, Chicken, Rabbit }

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
        [SerializeField] private AnimalSpecies species = AnimalSpecies.Deer;

        public AnimalSpecies Species => species;
        public string SpeciesKr => species switch {
            AnimalSpecies.Deer    => "사슴",
            AnimalSpecies.Boar    => "멧돼지",
            AnimalSpecies.Chicken => "닭",
            AnimalSpecies.Rabbit  => "토끼",
            _ => "동물",
        };

        // 종류별 stats (HP, food drop, speed, tame rate, scale, color tint)
        public static readonly (int hp, int food, float spd, float tame, float scale, Color tint)[] SpeciesStats = {
            (12, 5, 0.6f, 0.30f, 1.0f, new Color(1f, 1f, 1f, 1f)),               // Deer
            (25, 8, 0.5f, 0.15f, 1.1f, new Color(0.55f, 0.45f, 0.40f, 1f)),     // Boar 갈회색
            (4,  2, 0.4f, 0.60f, 0.7f, new Color(1.0f, 0.95f, 0.85f, 1f)),      // Chicken 흰
            (3,  1, 0.9f, 0.50f, 0.6f, new Color(0.80f, 0.75f, 0.72f, 1f)),     // Rabbit 옅은 회색
        };

        public void SetSpecies(AnimalSpecies s)
        {
            species = s;
            var (hp, food, spd, tame, scale, tint) = SpeciesStats[(int)s];
            maxHp = hp; foodDrop = food; wanderSpeed = spd; tameSuccessRate = tame;
            transform.localScale = new Vector3(scale, scale, 1);
            Hp = hp;
            var sR = GetComponent<SpriteRenderer>();
            if (sR != null) sR.color = tint;
            // #162 - baseColor 도 species tint 로 갱신 (hit-flash 후 복원에 사용)
            baseColor = tint;
        }

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
        // #162 - 종 tint 보존 (#156 lesson): hit-flash 끝나면 baseColor 로 복원.
        //  이전 sr.color = Color.white 가 species tint (멧돼지 갈회색 등) 영구 손실.
        private Color baseColor = Color.white;

        private void Awake()
        {
            Hp = maxHp;
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
            PickNewTarget();
        }

        private void Update()
        {
            if (IsDead) return;
            if (sr != null && flashUntil > 0 && Time.time > flashUntil)
            {
                // #162 - tamed 면 푸른빛 유지, 아니면 species baseColor 로 복원.
                sr.color = IsTamed ? new Color(0.75f, 0.85f, 1.0f, 1f) : baseColor;
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
                // 운영자 fb v4 - 림월드 정상 흐름: 즉시 +N 안 함. meat pile 만 drop.
                if (MeatPileEntity.SharedSprite != null)
                {
                    MeatPileEntity.Spawn(transform.position, foodDrop, MeatPileEntity.SharedSprite);
                }
                else
                {
                    if (ResourceManager.Instance != null) ResourceManager.Instance.AddFood(foodDrop);
                }
                Destroy(gameObject);
            }
        }
    }
}
