using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 50 — Arrow projectile.  Travels in a straight line, damages
    /// the first WolfEnemy/BanditEnemy/AnimalEntity it overlaps, then despawns.
    /// Auto-despawn after lifetime expires (3 sec).
    /// </summary>
    public class ArrowProjectile : MonoBehaviour
    {
        public Vector2 direction;
        public float speed = 8f;
        public int damage = 4;
        public float lifetime = 3f;
        public GameObject shooter;

        private float spawnTime;

        public static GameObject SpawnArrow(Vector3 origin, Vector2 dir, int damage, GameObject shooter, Sprite arrowSprite)
        {
            // #176 - PawnAbilities.shootingAccuracy perturbation (이전: 100% 명중).
            //  accuracy 0.80~1.20 분포 → perturb up to (1 - accuracy) × π/8 (~22.5°).
            //  accuracy=1.0 perfect, accuracy=0.8 → ±4.5° spread.
            Vector2 perturbedDir = dir.normalized;
            if (shooter != null)
            {
                var abil = shooter.GetComponent<PawnAbilities>();
                if (abil != null && abil.shootingAccuracy < 1.2f)
                {
                    float maxOff = Mathf.Max(0f, 1.2f - abil.shootingAccuracy) * (Mathf.PI / 8f);
                    float off = UnityEngine.Random.Range(-maxOff, maxOff);
                    float cs = Mathf.Cos(off), sn = Mathf.Sin(off);
                    perturbedDir = new Vector2(
                        perturbedDir.x * cs - perturbedDir.y * sn,
                        perturbedDir.x * sn + perturbedDir.y * cs);
                }
            }
            GameObject go = new GameObject("Arrow");
            go.transform.position = origin;
            float ang = Mathf.Atan2(perturbedDir.y, perturbedDir.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0, 0, ang);
            // 12x4 arrow at PPU 16 = 0.75 x 0.25 world unit.  Scale up slightly.
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = arrowSprite;
            sr.sortingOrder = 22;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.15f;
            col.isTrigger = true;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            var ap = go.AddComponent<ArrowProjectile>();
            ap.direction = perturbedDir;
            ap.damage = damage;
            ap.shooter = shooter;
            AudioBank.Instance?.PlayShoot();  // wiki B9: arrow fire/launch SFX
            return go;
        }

        private void Start()
        {
            spawnTime = Time.time;
        }

        private void Update()
        {
            if (Time.time - spawnTime > lifetime) { Destroy(gameObject); return; }
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || other.gameObject == shooter) return;
            // Don't hit other pawns
            if (other.GetComponent<PawnEntity>() != null) return;
            WolfEnemy wolf = other.GetComponent<WolfEnemy>();
            BanditEnemy bandit = other.GetComponent<BanditEnemy>();
            AnimalEntity animal = other.GetComponent<AnimalEntity>();
            bool hit = false;
            if (wolf != null) { wolf.TakeDamage(damage, shooter); hit = true; }
            else if (bandit != null) { bandit.TakeDamage(damage, shooter); hit = true; }
            else if (animal != null) { animal.TakeDamage(damage); hit = true; }
            if (hit)
            {
                AudioBank.Instance?.PlayHit();  // Day 80
                Destroy(gameObject);
            }
        }
    }
}
