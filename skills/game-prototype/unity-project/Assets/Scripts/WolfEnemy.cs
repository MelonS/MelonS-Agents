using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 64 — Wolf predator.  Wanders by default; if a pawn enters detection
    /// radius (5 unit), charges + attacks at melee range.  Drops 8 food on death.
    /// Spawned by AIDirector when wolf_pack event fires (or pre-placed for demo).
    /// </summary>
    public class WolfEnemy : MonoBehaviour
    {
        [SerializeField] private int maxHp = 18;
        [SerializeField] private int attackDamage = 4;
        [SerializeField] private float attackRange = 0.9f;
        [SerializeField] private float attackInterval = 1.2f;
        [SerializeField] private float detectionRadius = 5.0f;
        // #200 RimWorld fidelity ripple: pawn moveSpeed rose 3.0→4.6, so wolf
        //  chase must be >= 4.6 or a predator can never catch a fleeing pawn.
        //  RimWorld timber wolf ~4.6 c/s; set 5.0 so wolves stay a credible threat.
        [SerializeField] private float chaseSpeed = 5.0f;
        [SerializeField] private float wanderSpeed = 0.8f;
        [SerializeField] private int dropFood = 8;

        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;

        private Vector3 wanderTarget;
        private float nextWanderPick = -1f;
        private float nextAttackTime = -1f;
        private float nextTargetSearch = -1f;
        private PawnEntity cachedTarget;
        // #163 - hit-flash 시각 피드백 (BanditEnemy 와 같은 패턴).
        //  이전: 늑대가 공격받아도 시각 변화 0 → 플레이어가 데미지 들어가는지 알 수 없음.
        private SpriteRenderer sr;
        private Color baseColor = Color.white;
        private float flashUntil = -1f;
        private const float HitFlashSeconds = 0.08f;

        private void Awake()
        {
            Hp = maxHp;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
            PickNewWanderTarget();
        }

        private void Update()
        {
            if (IsDead) return;
            // #163 - hit-flash 복원
            if (sr != null && flashUntil > 0 && Time.time > flashUntil)
            {
                sr.color = baseColor;
                flashUntil = -1f;
            }
            // Find target every 0.4s
            if (Time.time > nextTargetSearch)
            {
                cachedTarget = FindNearestPawn();
                nextTargetSearch = Time.time + 0.4f;
            }
            // Combat mode
            if (cachedTarget != null && !cachedTarget.IsDead)
            {
                Vector3 dir = cachedTarget.transform.position - transform.position;
                float dist = dir.magnitude;
                if (dist <= attackRange)
                {
                    if (Time.time > nextAttackTime)
                    {
                        nextAttackTime = Time.time + attackInterval;
                        cachedTarget.TakeDamage(attackDamage, gameObject);
                    }
                    return;
                }
                // Chase
                Vector3 step = dir.normalized * chaseSpeed * Time.deltaTime;
                Vector3 newPos = transform.position + step;
                Vector2 clamped = PawnMovement.ClampToWorld(newPos);  // Step 81
                transform.position = new Vector3(clamped.x, clamped.y, newPos.z);
                return;
            }
            // Wander
            if (Time.time > nextWanderPick) PickNewWanderTarget();
            Vector3 d = wanderTarget - transform.position;
            if (d.magnitude < 0.1f) { PickNewWanderTarget(); return; }
            Vector3 wstep = d.normalized * wanderSpeed * Time.deltaTime;
            Vector3 wnew = transform.position + wstep;
            Vector2 wc = PawnMovement.ClampToWorld(wnew);  // Step 81
            transform.position = new Vector3(wc.x, wc.y, wnew.z);
        }

        private PawnEntity FindNearestPawn()
        {
            PawnEntity[] all = GameObject.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0) return null;
            PawnEntity best = null;
            float bestSq = detectionRadius * detectionRadius;
            Vector3 me = transform.position;
            foreach (var p in all)
            {
                if (p == null || p.IsDead) continue;
                float sq = (p.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p; }
            }
            return best;
        }

        private void PickNewWanderTarget()
        {
            float r = Random.Range(2f, 4f);
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector2 raw = (Vector2)transform.position + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            Vector2 clamped = PawnMovement.ClampToWorld(raw);  // Step 81
            wanderTarget = new Vector3(clamped.x, clamped.y, 0f);
            nextWanderPick = Time.time + Random.Range(3f, 6f);
        }

        public void TakeDamage(int dmg, GameObject source = null)
        {
            if (IsDead) return;
            Hp = Mathf.Max(0, Hp - dmg);
            // #163 - 시각 피드백
            if (sr != null)
            {
                sr.color = Color.white;
                flashUntil = Time.time + HitFlashSeconds;
            }
            if (Hp <= 0)
            {
                Debug.Log($"[Wolf] killed → +{dropFood} 고기 (pile drop)");
                // 운영자 fb v4 - 림월드 정상 흐름: meat pile 만 drop.
                if (MeatPileEntity.SharedSprite != null)
                {
                    MeatPileEntity.Spawn(transform.position, dropFood, MeatPileEntity.SharedSprite);
                }
                else if (ResourceManager.Instance != null)
                {
                    ResourceManager.Instance.AddFood(dropFood);
                }
                Destroy(gameObject, 0.5f);
            }
        }
    }
}
