// BanditEnemy.cs — Day 13 raid hostile.
//
// Subclass of the enemy-entity template pattern (HP + TakeDamage + IsDead).
// Movement: each tick, find nearest active PawnEntity within the map and
// MoveTowards on transform.  On contact (distance < 1.0) apply contactDamage
// to the pawn — throttled to 1 hit/sec, NEVER per-frame (lesson #4: don't
// hammer per-frame state changes; lesson #6 doesn't apply here because we
// poll distance rather than rely on collider Enter/Stay).
//
// On death, the base Destroy(gameObject) call is enough; the raid spawn is
// transient so OnKilled stays trivial.

using UnityEngine;

namespace MelonS.GameProto
{
    [RequireComponent(typeof(Collider2D))]
    public class BanditEnemy : MonoBehaviour
    {
        [SerializeField] private int maxHp = 20;
        [SerializeField] private int contactDamage = 2;
        [SerializeField] private float moveSpeed = 1.2f;
        [SerializeField] private float contactRange = 1.0f;
        [SerializeField] private float hitFlashSeconds = 0.06f;
        [SerializeField] private float damageInterval = 1.0f;

        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;
        // #135 - downed (hp 1/4 이하) 면 capture 시도 가능.  진짜 죽이는 대신.
        public bool IsDowned => Hp > 0 && Hp <= maxHp / 4;

        /// <summary>운영자 우클릭 capture - 50% 확률 colonist 합류.</summary>
        public bool TryCapture()
        {
            if (!IsDowned) return false;  // downed 상태만 capture
            if (Random.value < 0.5f)
            {
                // 성공 - 가까운 PawnEntity sprite + name 복제 (간단 spawn)
                var existingPawn = Object.FindFirstObjectByType<PawnEntity>();
                if (existingPawn != null)
                {
                    var go = new GameObject($"포로_{System.DateTime.Now.Ticks % 1000}");
                    go.transform.position = transform.position;
                    var sr2 = go.AddComponent<SpriteRenderer>();
                    var srcSr = existingPawn.GetComponent<SpriteRenderer>();
                    if (srcSr != null) { sr2.sprite = srcSr.sprite; sr2.sortingOrder = 10; }
                    go.AddComponent<BoxCollider2D>().size = new Vector2(2f, 2f);
                    go.AddComponent<PawnEntity>();
                    go.AddComponent<PawnMovement>();
                    go.AddComponent<PawnNeeds>();
                    go.AddComponent<PawnChopper>();
                    go.AddComponent<PawnGatherer>();
                    go.AddComponent<PawnHunter>();
                    go.AddComponent<PawnCook>();
                    go.AddComponent<PawnHauler>();
                    go.AddComponent<PawnSkills>();
                    go.AddComponent<PawnHealth>();
                    go.AddComponent<PawnWorkSettings>();
                    go.AddComponent<PawnUtilityAI>();
                    Debug.Log($"[Capture] 강도 포섭 성공!");
                }
                Destroy(gameObject);
                return true;
            }
            else
            {
                Debug.Log($"[Capture] 강도 포섭 실패, 도망");
                Destroy(gameObject);  // 실패 = 도망
                return false;
            }
        }

        private SpriteRenderer sr;
        private Color baseColor = Color.white;
        private float flashUntil = -1f;
        private float nextHitTime = -1f;

        // Target cache — refresh at most every 0.25s instead of every frame.
        // Lesson #4: FindObjectsOfType is O(n) and hammering it per-Update on
        // every BanditEnemy was the suspected cause of the Day 3 06:00 stall.
        private PawnEntity cachedTarget;
        private float nextTargetSearchTime = -1f;
        private const float TargetSearchInterval = 0.25f;

        private void Awake()
        {
            Hp = maxHp;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
        }

        private void Update()
        {
            if (IsDead) return;

            // Hit-flash decay
            if (sr != null && flashUntil > 0 && Time.time > flashUntil)
            {
                sr.color = baseColor;
                flashUntil = -1f;
            }

            // Re-acquire target only periodically OR when cache is stale (target
            // destroyed / died).  Unity == null also catches "fake null" on
            // destroyed objects, so this is a safe staleness check.
            if (cachedTarget == null || cachedTarget.IsDead || Time.time >= nextTargetSearchTime)
            {
                cachedTarget = FindNearestPawn();
                nextTargetSearchTime = Time.time + TargetSearchInterval;
            }

            PawnEntity target = cachedTarget;
            if (target == null) return;
            // Defensive re-check: target may have been destroyed between the
            // search and now (race with another BanditEnemy on same frame).
            if (target.IsDead) return;

            Vector3 myPos = transform.position;
            Vector3 tgtPos = target.transform.position;
            float dist = Vector3.Distance(myPos, tgtPos);

            if (dist > contactRange)
            {
                transform.position = Vector3.MoveTowards(
                    myPos, tgtPos, moveSpeed * Time.deltaTime);
            }
            else
            {
                // In contact — throttled damage application (lesson #4 firewall:
                // never apply state changes every frame; gate by interval).
                if (Time.time >= nextHitTime)
                {
                    nextHitTime = Time.time + damageInterval;
                    target.TakeDamage(contactDamage, gameObject);
                }
            }
        }

        private PawnEntity FindNearestPawn()
        {
            PawnEntity[] pawns = GameObject.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            if (pawns == null || pawns.Length == 0) return null;
            PawnEntity nearest = null;
            float bestSq = float.MaxValue;
            Vector3 myPos = transform.position;
            foreach (var p in pawns)
            {
                if (p == null) continue;
                if (p.IsDead) continue;
                float sq = (p.transform.position - myPos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; nearest = p; }
            }
            return nearest;
        }

        public void TakeDamage(int dmg, GameObject source = null)
        {
            if (IsDead) return;
            Hp = Mathf.Max(0, Hp - dmg);
            if (sr != null)
            {
                sr.color = Color.white;
                flashUntil = Time.time + hitFlashSeconds;
            }
            if (Hp <= 0)
            {
                Debug.Log($"[BanditEnemy] killed by {(source != null ? source.name : "?")}");
                Destroy(gameObject);
            }
        }

        public int GetContactDamage() => contactDamage;
    }
}
